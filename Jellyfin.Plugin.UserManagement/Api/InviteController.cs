using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.UserManagement.Services;
using Jellyfin.Plugin.UserManagement.Models;
using JPKribs.Jellyfin.Base;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.UserManagement.Api;

/// <summary>
/// Anonymous, public-facing invite redemption surface.
/// </summary>
[ApiController]
[Route("Invite")]
public class InviteController : ControllerBase
{
    private static string? _pageHtml;

    private readonly InviteService _inviteService;
    private readonly IServerApplicationPaths _paths;

    /// <summary>
    /// Initializes a new instance of the <see cref="InviteController"/> class.
    /// </summary>
    public InviteController(InviteService inviteService, IServerApplicationPaths paths)
    {
        _inviteService = inviteService;
        _paths = paths;
    }

    /// <summary>
    /// Serves the web client's favicon. The shared status template links a relative
    /// <c>favicon.ico</c>, which resolves to this sibling route. Without it the request would fall
    /// into the <c>{token}</c> page route and return HTML, leaving the invite page without an icon.
    /// </summary>
    /// <returns>The favicon bytes, or 404 when none could be located.</returns>
    [HttpGet("favicon.ico")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Favicon() => PublicPage.Favicon(_paths, Response);

    /// <summary>Serves the public signup page (static HTML; reads the token from the URL client-side).</summary>
    /// <param name="token">The invite token (used by the page's script, not here).</param>
    /// <returns>The HTML page.</returns>
    [HttpGet("{token}")]
    [AllowAnonymous]
    [Produces("text/html")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ContentResult Page(string token)
    {
        PublicPage.ApplyHardeningHeaders(Response);
        return Content(GetPageHtml(), "text/html");
    }

    /// <summary>Reports whether an invite can be redeemed, without revealing why if it cannot.</summary>
    /// <param name="token">The invite token.</param>
    /// <returns>Validity, label, and whether a PIN is required.</returns>
    [HttpGet("{token}/Info")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult Info(string token)
    {
        PublicPage.ApplyHardeningHeaders(Response);
        var invite = _inviteService.FindByToken(token);
        if (invite is null || !_inviteService.IsRedeemableNow(invite))
        {
            return Ok(new { Valid = false });
        }

        // The admin-facing name (Label) deliberately stays server-side.
        return Ok(new
        {
            Valid = true,
            Message = invite.Message,
            RequiresPin = !string.IsNullOrEmpty(invite.PinHash)
        });
    }

    /// <summary>Redeems an invite, creating a new account on success.</summary>
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
        PublicPage.ApplyHardeningHeaders(Response);
        var result = await _inviteService
            .RedeemAsync(token, request?.Pin, request?.Username, request?.Password, cancellationToken)
            .ConfigureAwait(false);

        return Ok(new { result.Success, result.Message, result.Resources });
    }

    private static string GetPageHtml()
    {
        if (_pageHtml is not null)
        {
            return _pageHtml;
        }

        var assembly = typeof(Plugin).Assembly;
        var resourceName = typeof(Plugin).Namespace + ".Configuration.usermanagement_invite_public.html";
        var content = "Invite page unavailable.";
        using (var stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream is not null)
            {
                using var reader = new StreamReader(stream);
                content = reader.ReadToEnd();
            }
        }

        // The page's script fills the card, so the heading and message stay empty and there is no button.
        _pageHtml = StatusPage.Render(string.Empty, string.Empty, title: "Invite", buttonText: null, content: content);
        return _pageHtml;
    }
}
