using Jellyfin.Plugin.UserManagement.Models;
using Jellyfin.Plugin.UserManagement.Services;
using Jellyfin.Plugin.UserManagement.Utilities;
using Xunit;

namespace Jellyfin.Plugin.UserManagement.Tests;

/// <summary>
/// Tests for <see cref="PasswordValidator"/>.
/// </summary>
public class PasswordValidatorTests
{
    [Fact]
    public void Validate_NullPolicy_ReturnsNoErrors()
    {
        Assert.Empty(PasswordValidator.Validate("x", null));
    }

    [Fact]
    public void Validate_DisabledPolicy_ReturnsNoErrors()
    {
        var policy = new PasswordPolicy { Enabled = false, MinLength = 99, DisallowEmpty = true };
        Assert.Empty(PasswordValidator.Validate("x", policy));
    }

    [Fact]
    public void Validate_EmptyPassword_DisallowEmpty_ReturnsError()
    {
        var policy = new PasswordPolicy { Enabled = true, DisallowEmpty = true };
        var errors = PasswordValidator.Validate(string.Empty, policy);
        Assert.Single(errors);
    }

    [Fact]
    public void Validate_EmptyPassword_AllowedWhenNotDisallowed()
    {
        var policy = new PasswordPolicy { Enabled = true, DisallowEmpty = false, MinLength = 8 };
        Assert.Empty(PasswordValidator.Validate(string.Empty, policy));
    }

    [Fact]
    public void Validate_TooShort_ReturnsError()
    {
        var policy = new PasswordPolicy { Enabled = true, MinLength = 8 };
        var errors = PasswordValidator.Validate("abc", policy);
        Assert.Single(errors);
    }

    [Fact]
    public void Validate_MissingUppercase_ReturnsError()
    {
        var policy = new PasswordPolicy { Enabled = true, MinLength = 1, RequireUppercase = true };
        Assert.Single(PasswordValidator.Validate("abc1!", policy));
    }

    [Fact]
    public void Validate_MissingNumber_ReturnsError()
    {
        var policy = new PasswordPolicy { Enabled = true, MinLength = 1, RequireNumber = true };
        Assert.Single(PasswordValidator.Validate("Abcd!", policy));
    }

    [Fact]
    public void Validate_MissingSymbol_ReturnsError()
    {
        var policy = new PasswordPolicy { Enabled = true, MinLength = 1, RequireSymbol = true };
        Assert.Single(PasswordValidator.Validate("Abcd1", policy));
    }

    [Fact]
    public void Validate_AllRulesSatisfied_ReturnsNoErrors()
    {
        var policy = new PasswordPolicy
        {
            Enabled = true,
            MinLength = 8,
            DisallowEmpty = true,
            RequireUppercase = true,
            RequireNumber = true,
            RequireSymbol = true
        };
        Assert.Empty(PasswordValidator.Validate("Abcdef1!", policy));
    }

    [Fact]
    public void Validate_MultipleFailures_ReturnsOneMessagePerRule()
    {
        var policy = new PasswordPolicy
        {
            Enabled = true,
            MinLength = 10,
            RequireUppercase = true,
            RequireNumber = true,
            RequireSymbol = true
        };
        var errors = PasswordValidator.Validate("abc", policy);
        Assert.Equal(4, errors.Count);
    }
}
