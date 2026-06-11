using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.UserManagement.Services;
using Jellyfin.Plugin.UserManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UserManagement.Api;

/// <summary>
/// Admin REST surface for the User Management plugin such as group apply & invite management.
/// </summary>
[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("UserManagement")]
[Produces(MediaTypeNames.Application.Json)]
public class UserManagementController : ControllerBase
{
    private readonly GroupService _groupService;
    private readonly InviteService _inviteService;
    private readonly ResetCodeService _resetCodes;
    private readonly ILogger<UserManagementController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserManagementController"/> class.
    /// </summary>
    public UserManagementController(
        GroupService groupService,
        InviteService inviteService,
        ResetCodeService resetCodes,
        ILogger<UserManagementController> logger)
    {
        _groupService = groupService;
        _inviteService = inviteService;
        _resetCodes = resetCodes;
        _logger = logger;
    }

    /// <summary>
    /// Returns the pending password reset codes from the server's reset files. Codes are only included
    /// while reset code extraction is enabled in the plugin configuration, so they never leave the
    /// server by default.
    /// </summary>
    /// <returns>Whether extraction is enabled, and the codes when it is.</returns>
    [HttpGet("Resets")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetResets()
    {
        var enabled = Plugin.Instance?.ReadConfiguration(c => c.EnableResetCodeExtraction) ?? false;
        if (!enabled)
        {
            return Ok(new { Enabled = false, Resets = new List<ResetCodeInfo>() });
        }

        return Ok(new { Enabled = true, Resets = _resetCodes.ReadAll() });
    }

    /// <summary>
    /// Reconciles every member to their group's permissions and password-rule enrollment on apply.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>Success status.</returns>
    [HttpPost("Apply")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> Apply(CancellationToken cancellationToken)
    {
        try
        {
            _groupService.AssignUnassignedToDefault();
            await _groupService.SyncAllAsync(null, cancellationToken).ConfigureAwait(false);
            return Ok(new { Success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying group policies");
            return StatusCode(500, new { Error = "An internal error occurred. Check server logs for details." });
        }
    }

    /// <summary>Lists all invites, with PIN hashes redacted to a boolean and usage merged in.</summary>
    /// <returns>The invites.</returns>
    [HttpGet("Invites")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<List<InviteSummary>> GetInvites()
        => Ok(_inviteService.GetSummaries());

    /// <summary>Creates an invite and returns it, with the PIN hash redacted to a boolean.</summary>
    /// <param name="request">The invite parameters.</param>
    /// <returns>The created invite.</returns>
    [HttpPost("Invites")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<InviteSummary> CreateInvite([FromBody] CreateInviteRequest request)
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

        if (!InviteService.IsValidPin(request.Pin))
        {
            return BadRequest(new { Error = "The PIN must be exactly 6 digits, like a Quick Connect code." });
        }

        foreach (var resource in request.Resources)
        {
            if (string.IsNullOrWhiteSpace(resource.Title) || !InviteService.IsValidResourceUrl(resource.Url))
            {
                return BadRequest(new { Error = "Each resource needs a title and a full http(s) URL." });
            }
        }

        GroupDefinition? targetGroup;
        if (request.UseDefaultGroup)
        {
            targetGroup = plugin.ReadConfiguration(c =>
                c.DefaultGroupId is { } id ? c.Groups.FirstOrDefault(g => g.Id.Equals(id)) : null);
            if (targetGroup is null)
            {
                return BadRequest(new { Error = "No default group is configured. Set one on the Groups tab, or choose a specific group for this invite." });
            }
        }
        else
        {
            targetGroup = request.GroupId is { } gid
                ? plugin.ReadConfiguration(c => c.Groups.FirstOrDefault(g => g.Id.Equals(gid)))
                : null;
            if (targetGroup is null)
            {
                return BadRequest(new { Error = "Choose a group for this invite." });
            }
        }

        if (targetGroup.BlocksInvites())
        {
            return BadRequest(new { Error = "This group disallows all password changes, so it cannot be used for invites. Members of it are managed by administrators." });
        }

        var invite = _inviteService.Create(request);
        return Ok(InviteSummary.FromInvite(invite));
    }

    /// <summary>Deletes an invite.</summary>
    /// <param name="id">The invite ID.</param>
    /// <returns>Success status.</returns>
    [HttpDelete("Invites/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult DeleteInvite(Guid id)
    {
        if (!_inviteService.Delete(id))
        {
            return NotFound(new { Error = "Invite not found." });
        }

        return Ok(new { Success = true });
    }

    /// <summary>Enables or disables an invite, also clearing its PIN lockout when enabling.</summary>
    /// <param name="id">The invite ID.</param>
    /// <param name="request">The desired enabled state.</param>
    /// <returns>Success status.</returns>
    [HttpPost("Invites/{id}/Enabled")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult SetInviteEnabled(Guid id, [FromBody] SetInviteEnabledRequest request)
    {
        if (request is null)
        {
            return BadRequest(new { Error = "Missing request body." });
        }

        return _inviteService.SetEnabled(id, request.Enabled) switch
        {
            InviteToggleResult.NotFound => NotFound(new { Error = "Invite not found." }),
            InviteToggleResult.Expired => BadRequest(new { Error = "This invite's expiration date has passed. Move the expiration forward to re-enable it." }),
            InviteToggleResult.GroupBlocksInvites => BadRequest(new { Error = "This invite's group disallows all password changes, so the invite cannot be enabled. Change the group's password mode first." }),
            _ => Ok(new { Success = true })
        };
    }

    /// <summary>Changes an invite's expiry date, reviving it when moved to a future date.</summary>
    /// <param name="id">The invite ID.</param>
    /// <param name="request">The new expiry date.</param>
    /// <returns>Success status.</returns>
    [HttpPost("Invites/{id}/Expiry")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult SetInviteExpiry(Guid id, [FromBody] SetInviteExpiryRequest request)
    {
        if (request is null)
        {
            return BadRequest(new { Error = "Missing request body." });
        }

        if (!_inviteService.SetExpiry(id, request.ExpiresAt))
        {
            return NotFound(new { Error = "Invite not found." });
        }

        return Ok(new { Success = true });
    }
}
