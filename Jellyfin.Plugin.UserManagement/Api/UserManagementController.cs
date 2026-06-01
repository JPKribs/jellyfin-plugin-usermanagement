using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.UserManagement.Groups;
using Jellyfin.Plugin.UserManagement.Invites;
using Jellyfin.Plugin.UserManagement.Models;
using Jellyfin.Plugin.UserManagement.Passwords;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UserManagement.Api;

/// <summary>
/// Admin REST surface for the User Management plugin (group apply + invite management). All actions
/// require elevation. Anonymous invite redemption lives in <see cref="InviteController"/>.
/// </summary>
[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("UserManagement")]
[Produces(MediaTypeNames.Application.Json)]
public class UserManagementController : ControllerBase
{
    // The built-in provider users fall back to when not enforcing rules.
    private const string DefaultProviderId = "Jellyfin.Server.Implementations.Users.DefaultAuthenticationProvider";

    private readonly GroupService _groupService;
    private readonly InviteService _inviteService;
    private readonly IUserManager _userManager;
    private readonly ILogger<UserManagementController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserManagementController"/> class.
    /// </summary>
    public UserManagementController(
        GroupService groupService,
        InviteService inviteService,
        IUserManager userManager,
        ILogger<UserManagementController> logger)
    {
        _groupService = groupService;
        _inviteService = inviteService;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Reconciles every group member to their group's managed permissions immediately.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>Success status.</returns>
    [HttpPost("Apply")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> Apply(CancellationToken cancellationToken)
    {
        try
        {
            await _groupService.SyncAllAsync(null, cancellationToken).ConfigureAwait(false);
            return Ok(new { Success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying group policies");
            return StatusCode(500, new { Error = "An internal error occurred. Check server logs for details." });
        }
    }

    // ===== Invites (admin) =====

    /// <summary>Lists all invites.</summary>
    /// <returns>The invites.</returns>
    [HttpGet("Invites")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<List<Invite>> GetInvites()
    {
        var invites = Plugin.Instance?.ReadConfiguration(c => c.Invites.ToList()) ?? new List<Invite>();
        return Ok(invites);
    }

    /// <summary>Creates an invite and returns it (including its token).</summary>
    /// <param name="request">The invite parameters.</param>
    /// <returns>The created invite.</returns>
    [HttpPost("Invites")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<Invite> CreateInvite([FromBody] CreateInviteRequest request)
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return StatusCode(500, new { Error = "Plugin not initialized." });
        }

        if (request is null)
        {
            return BadRequest(new { Error = "Missing request body." });
        }

        var invite = _inviteService.Create(
            request.Label,
            request.Pin,
            request.GroupId,
            request.ExpiresAt,
            request.MaxUses);

        return Ok(invite);
    }

    /// <summary>Deletes an invite.</summary>
    /// <param name="id">The invite ID.</param>
    /// <returns>Success status.</returns>
    [HttpDelete("Invites/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult DeleteInvite(Guid id)
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return StatusCode(500, new { Error = "Plugin not initialized." });
        }

        var removed = plugin.ReadConfiguration(c => c.Invites.Any(i => i.Id.Equals(id)));
        if (!removed)
        {
            return NotFound(new { Error = "Invite not found." });
        }

        plugin.MutateConfiguration(cfg =>
        {
            cfg.Invites.RemoveAll(i => i.Id.Equals(id));
            return true;
        });
        return Ok(new { Success = true });
    }

    // ===== Password rule enforcement (auth provider assignment) =====

    /// <summary>
    /// Sets exactly which users are enrolled in password-rule enforcement: the listed (non-admin)
    /// users are enrolled, and any user currently enrolled but not listed is reverted to the default
    /// provider. Administrators and users on other custom providers are never touched.
    /// </summary>
    /// <param name="request">The desired enrolled set.</param>
    /// <returns>How many users were enrolled and reverted.</returns>
    [HttpPost("Passwords/Enrollment")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> SetPasswordEnrollment([FromBody] PasswordEnrollmentRequest request)
    {
        var providerId = typeof(PasswordRuleAuthenticationProvider).FullName!;
        var desired = new HashSet<Guid>(request?.UserIds ?? new List<Guid>());
        var enrolled = 0;
        var reverted = 0;

        foreach (var user in _userManager.Users)
        {
            // Never enroll administrators.
            if (user.HasPermission(PermissionKind.IsAdministrator))
            {
                continue;
            }

            var onOurProvider = string.Equals(user.AuthenticationProviderId, providerId, StringComparison.Ordinal);

            if (desired.Contains(user.Id))
            {
                if (!onOurProvider)
                {
                    user.AuthenticationProviderId = providerId;
                    await _userManager.UpdateUserAsync(user).ConfigureAwait(false);
                    enrolled++;
                }
            }
            else if (onOurProvider)
            {
                // Only revert users we manage; leave users on other providers alone.
                user.AuthenticationProviderId = DefaultProviderId;
                await _userManager.UpdateUserAsync(user).ConfigureAwait(false);
                reverted++;
            }
        }

        _logger.LogInformation("Password enrollment updated: {Enrolled} enrolled, {Reverted} reverted", enrolled, reverted);
        return Ok(new { Enrolled = enrolled, Reverted = reverted });
    }

    /// <summary>
    /// Reverts all users from the password-rule provider back to the built-in default provider.
    /// </summary>
    /// <returns>The number of users reverted.</returns>
    [HttpPost("Passwords/Unassign")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> UnassignPasswordProvider()
    {
        var providerId = typeof(PasswordRuleAuthenticationProvider).FullName!;
        var reverted = 0;

        foreach (var user in _userManager.Users)
        {
            if (string.Equals(user.AuthenticationProviderId, providerId, StringComparison.Ordinal))
            {
                user.AuthenticationProviderId = DefaultProviderId;
                await _userManager.UpdateUserAsync(user).ConfigureAwait(false);
                reverted++;
            }
        }

        _logger.LogInformation("Reverted {Count} user(s) to the default provider", reverted);
        return Ok(new { Reverted = reverted });
    }
}
