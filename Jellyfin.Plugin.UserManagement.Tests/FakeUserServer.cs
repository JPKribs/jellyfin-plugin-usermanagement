using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Data;
using Jellyfin.Data.Events.Users;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.UserManagement.Services;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Users;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Jellyfin.Plugin.UserManagement.Tests;

/// <summary>
/// A substituted <see cref="IUserManager"/> that keeps real per user state (accounts, policies,
/// passwords) and raises the user created event through the plugin's consumer the way core does, inside
/// <see cref="IUserManager.CreateUserAsync"/>. Used to follow a whole invite redemption end to end.
/// </summary>
internal sealed class FakeUserServer
{
    public const string DefaultProviderId = "Jellyfin.Server.Implementations.Users.DefaultAuthenticationProvider";

    public FakeUserServer()
    {
        Um = TestSupport.NewUserManager();
        Groups = TestSupport.NewGroupService(Um);
        Consumer = new GroupEventConsumer(Groups, Um, Substitute.For<ILogger<GroupEventConsumer>>());

        Um.GetUsers().Returns(_ => Users.Values.ToList());
        Um.GetUserById(Arg.Any<Guid>()).Returns(ci => EntitySnapshots ? Load(ci.Arg<Guid>()) : Users.GetValueOrDefault(ci.Arg<Guid>()));
        Um.GetUserByName(Arg.Any<string>()).Returns(ci => Users.Values.FirstOrDefault(u => u.Username == ci.Arg<string>()));
        Um.GetUserDto(Arg.Any<User>(), Arg.Any<string>()).Returns(ci =>
        {
            var user = ci.Arg<User>();
            var policy = EntitySnapshots && _loaded.TryGetValue(user, out var snapshot) ? snapshot : Policies[user.Id];
            return new UserDto
            {
                Id = user.Id,
                Policy = Copy(policy),
                Configuration = new UserConfiguration()
            };
        });
        Um.UpdatePolicyAsync(Arg.Any<Guid>(), Arg.Any<UserPolicy>()).Returns(ci =>
        {
            if (FailPolicyUpdate?.Invoke(ci.Arg<UserPolicy>()) == true)
            {
                throw new InvalidOperationException("policy store unavailable");
            }

            var id = ci.Arg<Guid>();
            var policy = Copy(ci.Arg<UserPolicy>());
            Policies[id] = policy;
            var user = Users[id];
            user.AuthenticationProviderId = policy.AuthenticationProviderId;
            user.SetPermission(PermissionKind.IsDisabled, policy.IsDisabled);
            user.SetPermission(PermissionKind.IsAdministrator, policy.IsAdministrator);
            Events.Add("policy:" + (policy.IsDisabled ? "disabled" : "enabled"));
            return Task.CompletedTask;
        });
        Um.CreateUserAsync(Arg.Any<string>()).Returns(async ci =>
        {
            var user = TestSupport.NewUser(ci.Arg<string>());
            Users[user.Id] = user;

            // Jellyfin's own defaults for a new account: every library, remote access, enabled.
            Policies[user.Id] = new UserPolicy
            {
                AuthenticationProviderId = DefaultProviderId,
                EnableAllFolders = true,
                EnableRemoteAccess = true
            };
            Events.Add("create");
            OnCreated?.Invoke(user);
            await Consumer.OnEvent(new UserCreatedEventArgs(user)).ConfigureAwait(false);
            return user;
        });
        Um.ChangePassword(Arg.Any<Guid>(), Arg.Any<string>()).Returns(async ci =>
        {
            if (FailPasswordChange)
            {
                throw new InvalidOperationException("password store unavailable");
            }

            var id = ci.Arg<Guid>();
            if (BeforePassword is { } gate)
            {
                await gate().ConfigureAwait(false);
            }

            Passwords[id] = ci.ArgAt<string>(1);
            Events.Add("password:" + (Policies[id].IsDisabled ? "while-disabled" : "while-enabled"));
        });
        Um.DeleteUserAsync(Arg.Any<Guid>()).Returns(ci =>
        {
            Users.Remove(ci.Arg<Guid>());
            Events.Add("delete");
            return Task.CompletedTask;
        });
    }

    private readonly Dictionary<User, UserPolicy> _loaded = new(ReferenceEqualityComparer.Instance);

    public IUserManager Um { get; }

    public GroupService Groups { get; }

    public GroupEventConsumer Consumer { get; }

    public Dictionary<Guid, User> Users { get; } = new();

    public Dictionary<Guid, UserPolicy> Policies { get; } = new();

    public Dictionary<Guid, string> Passwords { get; } = new();

    public List<string> Events { get; } = new();

    public bool FailPasswordChange { get; set; }

    /// <summary>Gets or sets a predicate; a policy write it matches throws instead of saving.</summary>
    public Func<UserPolicy, bool>? FailPolicyUpdate { get; set; }

    /// <summary>Gets or sets a hook awaited inside the password change, to pause a redemption there.</summary>
    public Func<Task>? BeforePassword { get; set; }

    public Action<User>? OnCreated { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether loaded accounts behave like core's entities: each
    /// <see cref="IUserManager.GetUserById"/> returns a new entity, <see cref="IUserManager.GetUserDto"/>
    /// builds the policy from the entity it is given as it was when loaded, and a policy write never
    /// reaches entities loaded before it.
    /// </summary>
    public bool EntitySnapshots { get; set; }

    public User Single(string name) => Users.Values.Single(u => u.Username == name);

    /// <summary>Loads a new entity for the account, carrying its stored state at this moment.</summary>
    /// <param name="id">The account id.</param>
    /// <returns>The loaded entity, or null when there is no such account.</returns>
    public User? Load(Guid id)
    {
        if (!Users.TryGetValue(id, out var stored))
        {
            return null;
        }

        var policy = Policies[id];
        var user = TestSupport.NewUser(stored.Username, id);
        user.AuthenticationProviderId = policy.AuthenticationProviderId;
        user.SetPermission(PermissionKind.IsDisabled, policy.IsDisabled);
        user.SetPermission(PermissionKind.IsAdministrator, policy.IsAdministrator);
        _loaded.Add(user, Copy(policy));
        return user;
    }

    private static UserPolicy Copy(UserPolicy policy)
        => JsonSerializer.Deserialize<UserPolicy>(JsonSerializer.Serialize(policy))!;
}
