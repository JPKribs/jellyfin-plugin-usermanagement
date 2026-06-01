using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.UserManagement.Services;
using Jellyfin.Plugin.UserManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.UserManagement.Api;

/// <summary>
/// Anonymous, public-facing invite redemption surface. This is the only un-authenticated entry
/// point in the plugin: it serves the signup page and validates/redeems invites server-side. Every
/// action is explicitly <see cref="AllowAnonymousAttribute"/>; no admin action lives here.
/// </summary>
[ApiController]
[Route("UserManagement/Invite")]
public class InviteController : ControllerBase
{
    private static string? _pageHtml;

    private readonly InviteService _inviteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="InviteController"/> class.
    /// </summary>
    public InviteController(InviteService inviteService)
    {
        _inviteService = inviteService;
    }

    /// <summary>Serves the public signup page (static HTML; reads the token from the URL client-side).</summary>
    /// <param name="token">The invite token (used by the page's script, not here).</param>
    /// <returns>The HTML page.</returns>
    [HttpGet("{token}")]
    [AllowAnonymous]
    [Produces("text/html")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ContentResult Page(string token)
        => Content(GetPageHtml(), "text/html");

    /// <summary>Reports whether an invite can be redeemed, without revealing why if it cannot.</summary>
    /// <param name="token">The invite token.</param>
    /// <returns>Validity, label, and whether a PIN is required.</returns>
    [HttpGet("{token}/Info")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult Info(string token)
    {
        var invite = _inviteService.FindByToken(token);
        if (invite is null || !InviteService.IsRedeemable(invite))
        {
            return Ok(new { Valid = false });
        }

        return Ok(new
        {
            Valid = true,
            Label = invite.Label,
            RequiresPin = !string.IsNullOrEmpty(invite.PinHash)
        });
    }

    /// <summary>Redeems an invite, creating a new (non-admin) account on success.</summary>
    /// <param name="token">The invite token.</param>
    /// <param name="request">The PIN, username, and password.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>Success flag and a user-facing message.</returns>
    [HttpPost("{token}/Redeem")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> Redeem(
        string token,
        [FromBody] RedeemInviteRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _inviteService
            .RedeemAsync(token, request?.Pin, request?.Username, request?.Password, cancellationToken)
            .ConfigureAwait(false);

        return Ok(new { result.Success, result.Message });
    }

    private static string GetPageHtml()
    {
        if (_pageHtml is not null)
        {
            return _pageHtml;
        }

        var assembly = typeof(Plugin).Assembly;
        var resourceName = typeof(Plugin).Namespace + ".Configuration.usermanagement_invite_public.html";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return "<!DOCTYPE html><html><body>Invite page unavailable.</body></html>";
        }

        using var reader = new StreamReader(stream);
        _pageHtml = reader.ReadToEnd();
        return _pageHtml;
    }
}
