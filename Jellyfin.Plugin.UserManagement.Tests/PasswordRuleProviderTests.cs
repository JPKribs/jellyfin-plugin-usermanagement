using System;
using System.Threading.Tasks;
using Jellyfin.Plugin.UserManagement.Models;
using Jellyfin.Plugin.UserManagement.Services;
using Jellyfin.Plugin.UserManagement.Utilities;
using MediaBrowser.Model.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Jellyfin.Plugin.UserManagement.Tests;

/// <summary>
/// Tests for the password rule provider's change-password enforcement, in particular that a rule
/// violation surfaces as <see cref="ArgumentException"/> (HTTP 400 through Jellyfin's exception
/// middleware) rather than an authentication failure (401), which the web client mishandles.
/// </summary>
[Collection("Plugin")]
public class PasswordRuleProviderTests
{
    // ICryptoProvider takes ReadOnlySpan parameters, which NSubstitute cannot proxy, so a small
    // hand-rolled fake stands in for it.
    private sealed class FakeCryptoProvider : ICryptoProvider
    {
        public string DefaultHashMethod => "Test";

        public PasswordHash CreatePasswordHash(ReadOnlySpan<char> password)
            => new("Test", new byte[] { 1, 2, 3 });

        public bool Verify(PasswordHash hash, ReadOnlySpan<char> password) => true;

        public byte[] GenerateSalt() => new byte[] { 1 };

        public byte[] GenerateSalt(int length) => new byte[length];
    }

    private static PasswordRuleAuthenticationProvider NewProvider(IHttpContextAccessor? accessor = null)
        => new(
            new FakeCryptoProvider(),
            TestSupport.NewActivityLogger(),
            Substitute.For<ILogger<PasswordRuleAuthenticationProvider>>(),
            accessor);

    private static IHttpContextAccessor AccessorFor(bool admin)
    {
        var claims = admin
            ? new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Administrator") }
            : Array.Empty<System.Security.Claims.Claim>();
        var principal = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(claims, "Test"));

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(new DefaultHttpContext { User = principal });
        return accessor;
    }

    // An anonymous request: a principal exists but carries no authenticated identity. This is the
    // shape of core's forgot password pin redemption and of invite redemption.
    private static IHttpContextAccessor AnonymousAccessor()
    {
        var principal = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity());

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(new DefaultHttpContext { User = principal });
        return accessor;
    }

    private static GroupDefinition EnforcingGroup(Guid memberId, PasswordPolicy policy)
    {
        var group = new GroupDefinition { Id = Guid.NewGuid(), Name = "Enforced", Password = policy };
        group.MemberIds.Add(memberId);
        return group;
    }

    [Fact]
    public async Task ChangePassword_RuleViolation_ThrowsArgumentExceptionWithReasons()
    {
        var plugin = TestSupport.NewPlugin();
        var user = TestSupport.NewUser();
        plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Add(EnforcingGroup(user.Id, new PasswordPolicy { Enabled = true, MinLength = 10 }));
            return true;
        });

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => NewProvider(AccessorFor(admin: false)).ChangePassword(user, "short"));

        Assert.Contains("at least 10 characters", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChangePassword_MeetsPolicy_SetsHashedPassword()
    {
        var plugin = TestSupport.NewPlugin();
        var user = TestSupport.NewUser();
        plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Add(EnforcingGroup(user.Id, new PasswordPolicy { Enabled = true, MinLength = 8 }));
            return true;
        });

        await NewProvider().ChangePassword(user, "longenoughpassword");

        Assert.False(string.IsNullOrEmpty(user.Password));
    }

    [Fact]
    public async Task ChangePassword_NoGroupPolicy_IsUnrestricted()
    {
        _ = TestSupport.NewPlugin();
        var user = TestSupport.NewUser();

        await NewProvider().ChangePassword(user, "x");

        Assert.False(string.IsNullOrEmpty(user.Password));
    }

    [Fact]
    public async Task ChangePassword_PolicyComesFromEnabledGroup_NotFirstGroupBlindly()
    {
        // Enrollment triggers on membership in any password enforcing group, so validation must find
        // that group's policy even when an earlier group in list order has none.
        var plugin = TestSupport.NewPlugin();
        var user = TestSupport.NewUser();
        plugin.MutateConfiguration(cfg =>
        {
            var unenforced = new GroupDefinition { Id = Guid.NewGuid(), Name = "First" };
            unenforced.MemberIds.Add(user.Id);
            cfg.Groups.Add(unenforced);
            cfg.Groups.Add(EnforcingGroup(user.Id, new PasswordPolicy { Enabled = true, MinLength = 10 }));
            return true;
        });

        await Assert.ThrowsAsync<ArgumentException>(
            () => NewProvider(AccessorFor(admin: false)).ChangePassword(user, "short"));
    }

    [Fact]
    public async Task ChangePassword_InitialOnly_BlocksSelfServiceChange()
    {
        var plugin = TestSupport.NewPlugin();
        var user = TestSupport.NewUser();
        user.Password = "existing-hash";
        plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Add(EnforcingGroup(user.Id, new PasswordPolicy { Enabled = true, ChangeMode = PasswordChangeMode.InitialOnly }));
            return true;
        });

        var provider = NewProvider(AccessorFor(admin: false));

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => provider.ChangePassword(user, "longenoughpassword"));

        Assert.Contains("disabled for your account", ex.Message, StringComparison.Ordinal);
        Assert.Equal("existing-hash", user.Password);
    }

    [Fact]
    public async Task ChangePassword_InitialOnly_AdminCallerCanStillChange()
    {
        var plugin = TestSupport.NewPlugin();
        var user = TestSupport.NewUser();
        user.Password = "existing-hash";
        plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Add(EnforcingGroup(user.Id, new PasswordPolicy { Enabled = true, ChangeMode = PasswordChangeMode.InitialOnly }));
            return true;
        });

        await NewProvider(AccessorFor(admin: true)).ChangePassword(user, "longenoughpassword");

        Assert.NotEqual("existing-hash", user.Password);
    }

    [Fact]
    public async Task ChangePassword_InitialOnly_InternalCallWithoutRequestIsAllowed()
    {
        var plugin = TestSupport.NewPlugin();
        var user = TestSupport.NewUser();
        user.Password = "existing-hash";
        plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Add(EnforcingGroup(user.Id, new PasswordPolicy { Enabled = true, ChangeMode = PasswordChangeMode.InitialOnly }));
            return true;
        });

        await NewProvider().ChangePassword(user, "longenoughpassword");

        Assert.NotEqual("existing-hash", user.Password);
    }

    [Fact]
    public async Task ChangePassword_InitialOnly_FirstPasswordIsStillAllowed()
    {
        // A member with no password yet may set their first one, which keeps invite signups working
        // when the creation event enrolls the new user before redemption sets the initial password.
        var plugin = TestSupport.NewPlugin();
        var user = TestSupport.NewUser();
        plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Add(EnforcingGroup(user.Id, new PasswordPolicy { Enabled = true, ChangeMode = PasswordChangeMode.InitialOnly }));
            return true;
        });

        await NewProvider(AccessorFor(admin: false)).ChangePassword(user, "longenoughpassword");

        Assert.False(string.IsNullOrEmpty(user.Password));
    }

    [Fact]
    public async Task ChangePassword_Disallowed_BlocksFirstPasswordToo()
    {
        var plugin = TestSupport.NewPlugin();
        var user = TestSupport.NewUser();
        plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Add(EnforcingGroup(user.Id, new PasswordPolicy { Enabled = true, ChangeMode = PasswordChangeMode.Disallowed }));
            return true;
        });

        await Assert.ThrowsAsync<ArgumentException>(
            () => NewProvider(AccessorFor(admin: false)).ChangePassword(user, "longenoughpassword"));

        Assert.True(string.IsNullOrEmpty(user.Password));
    }

    [Fact]
    public async Task ChangePassword_Disallowed_InviteRedemptionStillSetsIt()
    {
        // Redemption runs anonymously, so without the explicit scope the account creation password
        // would be indistinguishable from a member's self service attempt and every invite into the
        // group would fail.
        var plugin = TestSupport.NewPlugin();
        var user = TestSupport.NewUser();
        plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Add(EnforcingGroup(user.Id, new PasswordPolicy { Enabled = true, ChangeMode = PasswordChangeMode.Disallowed }));
            return true;
        });

        using (InviteRedemptionScope.Begin())
        {
            await NewProvider(AccessorFor(admin: false)).ChangePassword(user, "longenoughpassword");
        }

        Assert.False(string.IsNullOrEmpty(user.Password));
    }

    [Fact]
    public async Task ChangePassword_Disallowed_AdminCanStillSetIt()
    {
        var plugin = TestSupport.NewPlugin();
        var user = TestSupport.NewUser();
        plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Add(EnforcingGroup(user.Id, new PasswordPolicy { Enabled = true, ChangeMode = PasswordChangeMode.Disallowed }));
            return true;
        });

        await NewProvider(AccessorFor(admin: true)).ChangePassword(user, "longenoughpassword");

        Assert.False(string.IsNullOrEmpty(user.Password));
    }

    [Fact]
    public void InviteRedemptionScope_EndsWhenDisposed()
    {
        Assert.False(InviteRedemptionScope.IsActive);
        using (InviteRedemptionScope.Begin())
        {
            Assert.True(InviteRedemptionScope.IsActive);
        }

        Assert.False(InviteRedemptionScope.IsActive);
    }

    [Fact]
    public async Task ChangePassword_EmptyDisallowed_ThrowsArgumentExceptionForMember()
    {
        var plugin = TestSupport.NewPlugin();
        var user = TestSupport.NewUser();
        plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Add(EnforcingGroup(user.Id, new PasswordPolicy { Enabled = true, DisallowEmpty = true }));
            return true;
        });

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => NewProvider(AccessorFor(admin: false)).ChangePassword(user, string.Empty));

        Assert.Contains("password is required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChangePassword_AdminCaller_BypassesRuleValidation()
    {
        var plugin = TestSupport.NewPlugin();
        var user = TestSupport.NewUser();
        plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Add(EnforcingGroup(user.Id, new PasswordPolicy { Enabled = true, MinLength = 16, RequireSymbol = true }));
            return true;
        });

        await NewProvider(AccessorFor(admin: true)).ChangePassword(user, "short");

        Assert.False(string.IsNullOrEmpty(user.Password));
    }

    [Fact]
    public async Task ChangePassword_AdminCaller_CanResetToEmpty()
    {
        // The dashboard's Reset Password is a change to the empty string, so the validator must not
        // run for admin callers or a member locked out under Disallowed rules could never be rescued.
        var plugin = TestSupport.NewPlugin();
        var user = TestSupport.NewUser();
        user.Password = "existing-hash";
        plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Add(EnforcingGroup(user.Id, new PasswordPolicy
            {
                Enabled = true,
                DisallowEmpty = true,
                ChangeMode = PasswordChangeMode.Disallowed
            }));
            return true;
        });

        await NewProvider(AccessorFor(admin: true)).ChangePassword(user, string.Empty);

        Assert.Null(user.Password);
    }

    [Fact]
    public async Task ChangePassword_AnonymousCaller_BypassesEnforcement()
    {
        // Core's forgot password flow redeems a pin by setting the member's password to that pin in
        // an anonymous request. The pin never satisfies group rules and only an administrator can
        // read it off the server, so enforcement must not apply or the flow breaks for every
        // enrolled user in every mode.
        var plugin = TestSupport.NewPlugin();
        var user = TestSupport.NewUser();
        user.Password = "existing-hash";
        plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Add(EnforcingGroup(user.Id, new PasswordPolicy
            {
                Enabled = true,
                DisallowEmpty = true,
                MinLength = 16,
                RequireSymbol = true,
                ChangeMode = PasswordChangeMode.Disallowed
            }));
            return true;
        });

        await NewProvider(AnonymousAccessor()).ChangePassword(user, "A1B2C3D4");

        Assert.NotEqual("existing-hash", user.Password);
        Assert.False(string.IsNullOrEmpty(user.Password));
    }

    [Fact]
    public async Task Authenticate_UnresolvedUser_ThrowsAuthenticationException()
    {
        // Core probes every provider for usernames that match no account and only catches
        // AuthenticationException, so any other type becomes a 500 for the login request.
        await Assert.ThrowsAsync<MediaBrowser.Controller.Authentication.AuthenticationException>(
            () => NewProvider().Authenticate("ghost", "whatever", null));
    }
}
