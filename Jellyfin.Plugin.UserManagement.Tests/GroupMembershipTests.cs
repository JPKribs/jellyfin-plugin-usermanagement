using System;
using System.Collections.Generic;
using Jellyfin.Plugin.UserManagement.Models;
using Jellyfin.Plugin.UserManagement.Utilities;
using Xunit;

namespace Jellyfin.Plugin.UserManagement.Tests;

/// <summary>
/// Tests for the server side single membership enforcement applied before a configuration save.
/// </summary>
public class GroupMembershipTests
{
    private static GroupDefinition Group(params Guid[] members)
    {
        var group = new GroupDefinition { Id = Guid.NewGuid() };
        group.MemberIds.AddRange(members);
        return group;
    }

    [Fact]
    public void EnforceSingleMembership_CrossGroupDuplicate_FirstGroupKeepsTheMember()
    {
        var user = Guid.NewGuid();
        var first = Group(user);
        var second = Group(user, Guid.NewGuid());
        var groups = new List<GroupDefinition> { first, second };

        var removed = GroupMembership.EnforceSingleMembership(groups);

        Assert.Equal(1, removed);
        Assert.Contains(user, first.MemberIds);
        Assert.DoesNotContain(user, second.MemberIds);
        Assert.Single(second.MemberIds);
    }

    [Fact]
    public void EnforceSingleMembership_WithinGroupDuplicate_IsRemoved()
    {
        var user = Guid.NewGuid();
        var group = Group(user, user);

        var removed = GroupMembership.EnforceSingleMembership(new[] { group });

        Assert.Equal(1, removed);
        Assert.Single(group.MemberIds);
    }

    [Fact]
    public void EnforceSingleMembership_NoDuplicates_LeavesEverythingAlone()
    {
        var first = Group(Guid.NewGuid(), Guid.NewGuid());
        var second = Group(Guid.NewGuid());

        var removed = GroupMembership.EnforceSingleMembership(new[] { first, second });

        Assert.Equal(0, removed);
        Assert.Equal(2, first.MemberIds.Count);
        Assert.Single(second.MemberIds);
    }

    [Theory]
    [InlineData(false, PasswordChangeMode.Disallowed, false)]
    [InlineData(true, PasswordChangeMode.Allowed, false)]
    [InlineData(true, PasswordChangeMode.InitialOnly, false)]
    [InlineData(true, PasswordChangeMode.Disallowed, true)]
    public void BlocksInvites_OnlyForEnforcedDisallowedPolicies(bool enabled, PasswordChangeMode mode, bool expected)
    {
        var group = new GroupDefinition
        {
            Id = Guid.NewGuid(),
            Password = new PasswordPolicy { Enabled = enabled, ChangeMode = mode }
        };

        Assert.Equal(expected, group.BlocksInvites());
    }

    [Fact]
    public void BlocksInvites_NullGroupOrNoPolicy_False()
    {
        Assert.False(((GroupDefinition?)null).BlocksInvites());
        Assert.False(new GroupDefinition { Id = Guid.NewGuid() }.BlocksInvites());
    }
}
