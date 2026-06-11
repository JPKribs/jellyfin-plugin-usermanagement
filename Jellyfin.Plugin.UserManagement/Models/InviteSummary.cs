using System;

namespace Jellyfin.Plugin.UserManagement.Models;

/// <summary>
/// The invite shape returned to the dashboard. Mirrors <see cref="Invite"/> minus the PIN hash,
/// which has no reason to leave the server, replaced by a boolean the dashboard can display.
/// </summary>
public class InviteSummary
{
    /// <summary>Gets or sets the stable identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the high-entropy URL token that identifies this invite.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Gets or sets an admin-facing label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether a PIN is required to redeem.</summary>
    public bool HasPin { get; set; }

    /// <summary>Gets or sets a value indicating whether new accounts are placed in the default group.</summary>
    public bool UseDefaultGroup { get; set; }

    /// <summary>Gets or sets the explicit group new accounts are placed in when not using the default group.</summary>
    public Guid? GroupId { get; set; }

    /// <summary>Gets or sets the moment the invite stops working. Null = never expires.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Gets or sets the maximum number of accounts that may be created. 0 = unlimited.</summary>
    public int MaxUses { get; set; }

    /// <summary>Gets or sets how many accounts have been created with this invite.</summary>
    public int UsedCount { get; set; }

    /// <summary>Gets or sets a value indicating whether the invite is active.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets when the invite was created (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Builds the dashboard shape from a stored invite and its runtime status.</summary>
    /// <param name="invite">The stored invite.</param>
    /// <param name="status">The invite's runtime status, or <c>null</c> when it has never been used.</param>
    /// <returns>The redacted summary.</returns>
    public static InviteSummary FromInvite(Invite invite, InviteStatus? status = null)
    {
        ArgumentNullException.ThrowIfNull(invite);
        return new InviteSummary
        {
            Id = invite.Id,
            Token = invite.Token,
            Label = invite.Label,
            HasPin = !string.IsNullOrEmpty(invite.PinHash),
            UseDefaultGroup = invite.UseDefaultGroup,
            GroupId = invite.GroupId,
            ExpiresAt = invite.ExpiresAt,
            MaxUses = invite.MaxUses,
            UsedCount = status?.UsedCount ?? 0,
            Enabled = invite.Enabled,
            CreatedAt = invite.CreatedAt
        };
    }
}
