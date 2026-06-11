using System;
using System.Threading;

namespace Jellyfin.Plugin.UserManagement.Utilities;

/// <summary>
/// Marks the current async flow as an invite redemption, so the password rule provider can tell the
/// account creation password apart from a later self service change. Redemption runs anonymously, and
/// without this marker a group that disallows initial passwords would also break its own invites.
/// </summary>
public static class InviteRedemptionScope
{
    private static readonly AsyncLocal<bool> Active = new();

    /// <summary>Gets a value indicating whether an invite redemption is running on this async flow.</summary>
    public static bool IsActive => Active.Value;

    /// <summary>Begins a redemption scope. Dispose it to end the scope.</summary>
    /// <returns>A handle that ends the scope when disposed.</returns>
    public static IDisposable Begin()
    {
        Active.Value = true;
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        public void Dispose() => Active.Value = false;
    }
}
