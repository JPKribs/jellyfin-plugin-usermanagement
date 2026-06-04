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
    private readonly ILogger<UserManagementController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserManagementController"/> class.
    /// </summary>
    public UserManagementController(
        GroupService groupService,
        InviteService inviteService,
        ILogger<UserManagementController> logger)
    {
        _groupService = groupService;
        _inviteService = inviteService;
        _logger = logger;
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

    /// <summary>Lists all invites.</summary>
    /// <returns>The invites.</returns>
    [HttpGet("Invites")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<List<Invite>> GetInvites()
    {
        var invites = Plugin.Instance?.ReadConfiguration(c => c.Invites.ToList()) ?? new List<Invite>();
        return Ok(invites);
    }

    /// <summary>Creates an invite and returns it.</summary>
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

        if (request.UseDefaultGroup)
        {
            var hasDefault = plugin.ReadConfiguration(c =>
                c.DefaultGroupId is { } id && c.Groups.Any(g => g.Id.Equals(id)));
            if (!hasDefault)
            {
                return BadRequest(new { Error = "No default group is configured. Set one on the Groups tab, or choose a specific group for this invite." });
            }
        }
        else
        {
            var validGroup = request.GroupId is { } gid && plugin.ReadConfiguration(c => c.Groups.Any(g => g.Id.Equals(gid)));
            if (!validGroup)
            {
                return BadRequest(new { Error = "Choose a group for this invite." });
            }
        }

        var invite = _inviteService.Create(
            request.Label,
            request.Pin,
            request.UseDefaultGroup,
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

        if (!_inviteService.SetEnabled(id, request.Enabled))
        {
            return NotFound(new { Error = "Invite not found." });
        }

        return Ok(new { Success = true });
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
