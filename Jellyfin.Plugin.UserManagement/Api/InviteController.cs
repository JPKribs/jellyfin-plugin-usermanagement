using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.UserManagement.Services;
using Jellyfin.Plugin.UserManagement.Models;
using Jellyfin.Plugin.UserManagement.Utilities;
using JPKribs.Jellyfin.Base;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UserManagement.Api;

/// <summary>
/// Anonymous, public-facing invite redemption surface.
/// </summary>
[ApiController]
[Route("Invite")]
public class InviteController : ControllerBase
{
    private static string? _pageHtml;

    private const string TooManyAttempts = "Too many attempts from your network. Please wait a few minutes and try again.";

    private readonly InviteService _inviteService;
    private readonly InviteAttemptLimiter _limiter;
    private readonly IServerApplicationPaths _paths;
    private readonly ILogger<InviteController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InviteController"/> class.
    /// </summary>
    public InviteController(
        InviteService inviteService,
        InviteAttemptLimiter limiter,
        IServerApplicationPaths paths,
        ILogger<InviteController> logger)
    {
        _inviteService = inviteService;
        _limiter = limiter;
        _paths = paths;
        _logger = logger;
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
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public ActionResult Info(string token)
    {
        PublicPage.ApplyHardeningHeaders(Response);
        if (Throttled(InviteEndpoint.Info) is { } throttled)
        {
            return throttled;
        }

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
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> Redeem(
        string token,
        [FromBody] RedeemInviteRequest request,
        CancellationToken cancellationToken)
    {
        PublicPage.ApplyHardeningHeaders(Response);
        if (Throttled(InviteEndpoint.Redeem) is { } throttled)
        {
            return throttled;
        }

        var result = await _inviteService
            .RedeemAsync(token, request?.Pin, request?.Username, request?.Password, cancellationToken)
            .ConfigureAwait(false);

        return Ok(new { result.Success, result.Message, result.Resources });
    }

    /// <summary>
    /// Counts the request against the calling client and returns a 429 once it is over the limit. The
    /// body keeps the shape of the normal response, so the signup page shows the message as it would any
    /// other failure. The client is the remote address as the server resolved it, which behind a reverse
    /// proxy is only the real client when the proxy is listed in the server's known proxies.
    /// </summary>
    private ObjectResult? Throttled(InviteEndpoint endpoint)
    {
        var remote = HttpContext?.Connection.RemoteIpAddress;
        WarnIfProxyIsNotTrusted(remote);
        var client = ClientAddress.Key(remote);
        if (_limiter.TryAcquire(endpoint, client))
        {
            return null;
        }

        Response.Headers.RetryAfter = _limiter.RetryAfterSeconds(endpoint, client).ToString(CultureInfo.InvariantCulture);
        object body = endpoint == InviteEndpoint.Info
            ? new { Valid = false, Throttled = true, Message = TooManyAttempts }
            : new { Success = false, Message = TooManyAttempts, Resources = new List<InviteResource>() };
        return StatusCode(StatusCodes.Status429TooManyRequests, body);
    }

    /// <summary>
    /// The server consumes X-Forwarded-For only from a proxy listed in its known proxies. A request that
    /// still carries the header and comes from a private or loopback address is almost certainly from a
    /// reverse proxy the server does not trust, so every visitor shares the proxy's address and one
    /// attempt budget. Logged once per process, since the setup does not change between requests.
    /// </summary>
    private void WarnIfProxyIsNotTrusted(System.Net.IPAddress? remote)
    {
        if (!ClientAddress.IsPrivate(remote)
            || Request?.Headers.ContainsKey("X-Forwarded-For") != true
            || !_limiter.ClaimProxyWarning())
        {
            return;
        }

        _logger.LogWarning(
            "Invite requests arrive from {Address} with an X-Forwarded-For header, which looks like a reverse proxy that is not in Jellyfin's Known proxies. Every invitee then shares that address and one attempt limit, so a few failed signups can block invites for everyone for 15 minutes. Add the proxy under Dashboard > Networking > Known proxies.",
            remote);
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
