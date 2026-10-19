using System;
using System.Collections.Generic;
using System.Threading;
using Jellyfin.Plugin.UserManagement.Models;

namespace Jellyfin.Plugin.UserManagement.Services;

/// <summary>
/// In memory, per client throttle for the anonymous invite endpoints. Every request counts, whatever
/// its outcome, so a client probing tokens, PINs or usernames is slowed down as much as one signing up.
/// It sits in front of the per invite limits (PIN lockout, maximum uses, the configurable signup rate
/// limit), which only see one invite at a time. Counts live in memory only and reset on restart.
/// </summary>
/// <remarks>
/// Clients are kept in least recently used order, so expired clients are always at the front and are
/// dropped a few at a time as requests arrive, and the table never holds more than
/// <see cref="MaxTrackedClients"/> entries: past that the least recently seen client is forgotten.
/// </remarks>
public sealed class InviteAttemptLimiter
{
    /// <summary>The sliding window every limit is counted over.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    /// <summary>Signup attempts one client may make within <see cref="Window"/>.</summary>
    public const int MaxRedeemAttempts = 10;

    /// <summary>Invite lookups (the signup page's validity check) one client may make within <see cref="Window"/>.</summary>
    public const int MaxInfoRequests = 60;

    /// <summary>The most clients tracked at once, across both endpoints.</summary>
    public const int MaxTrackedClients = 10_000;

    private readonly TimeProvider _time;
    private readonly int _capacity;
    private readonly object _lock = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _order = new();
    private int _proxyWarningClaimed;

    /// <summary>
    /// Initializes a new instance of the <see cref="InviteAttemptLimiter"/> class.
    /// </summary>
    public InviteAttemptLimiter()
        : this(TimeProvider.System)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InviteAttemptLimiter"/> class with a given clock and
    /// table size.
    /// </summary>
    /// <param name="time">The clock.</param>
    /// <param name="capacity">The most clients tracked at once.</param>
    internal InviteAttemptLimiter(TimeProvider time, int capacity = MaxTrackedClients)
    {
        _time = time;
        _capacity = Math.Max(1, capacity);
    }

    /// <summary>Gets the number of clients currently tracked.</summary>
    internal int TrackedClients
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>
    /// Records an attempt from a client and reports whether it is within the limit. A refused attempt is
    /// not recorded, so a client that keeps retrying is let back in once its oldest counted attempt ages
    /// out of the window rather than being locked out for as long as it keeps trying.
    /// </summary>
    /// <param name="endpoint">The endpoint being called.</param>
    /// <param name="client">The client key, normally from <see cref="Utilities.ClientAddress.Key"/>.</param>
    /// <returns><c>true</c> when the attempt may proceed.</returns>
    public bool TryAcquire(InviteEndpoint endpoint, string? client)
    {
        var max = endpoint == InviteEndpoint.Redeem ? MaxRedeemAttempts : MaxInfoRequests;
        var key = Key(endpoint, client);
        var now = _time.GetUtcNow();
        var cutoff = now - Window;

        lock (_lock)
        {
            DropExpired(cutoff);

            if (_entries.TryGetValue(key, out var entry))
            {
                _order.Remove(entry.Node);
            }
            else
            {
                while (_entries.Count >= _capacity && _order.First is { } oldest)
                {
                    _entries.Remove(oldest.Value);
                    _order.RemoveFirst();
                }

                entry = new Entry(new LinkedListNode<string>(key));
                _entries[key] = entry;
            }

            entry.LastSeen = now;
            _order.AddLast(entry.Node);

            while (entry.Times.Count > 0 && entry.Times.Peek() <= cutoff)
            {
                entry.Times.Dequeue();
            }

            if (entry.Times.Count >= max)
            {
                return false;
            }

            entry.Times.Enqueue(now);
            return true;
        }
    }

    /// <summary>
    /// Seconds until a refused client may try again, for the Retry-After header.
    /// </summary>
    /// <param name="endpoint">The endpoint that refused.</param>
    /// <param name="client">The client key.</param>
    /// <returns>Whole seconds, at least one.</returns>
    public int RetryAfterSeconds(InviteEndpoint endpoint, string? client)
    {
        lock (_lock)
        {
            if (!_entries.TryGetValue(Key(endpoint, client), out var entry) || entry.Times.Count == 0)
            {
                return 1;
            }

            var wait = entry.Times.Peek() + Window - _time.GetUtcNow();
            return Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds));
        }
    }

    /// <summary>
    /// Claims the one time warning about a reverse proxy the server does not trust. Returns <c>true</c>
    /// to exactly one caller for the life of the process.
    /// </summary>
    /// <returns>Whether this caller should log the warning.</returns>
    public bool ClaimProxyWarning() => Interlocked.Exchange(ref _proxyWarningClaimed, 1) == 0;

    private static string Key(InviteEndpoint endpoint, string? client)
        => endpoint + "|" + (string.IsNullOrEmpty(client) ? "unknown" : client);

    // Entries are ordered by last request, so every client whose last request left the window sits at
    // the front. Each call only pays for the entries it actually removes.
    private void DropExpired(DateTimeOffset cutoff)
    {
        while (_order.First is { } oldest && _entries[oldest.Value].LastSeen <= cutoff)
        {
            _entries.Remove(oldest.Value);
            _order.RemoveFirst();
        }
    }

    private sealed class Entry
    {
        public Entry(LinkedListNode<string> node) => Node = node;

        public LinkedListNode<string> Node { get; }

        public Queue<DateTimeOffset> Times { get; } = new();

        public DateTimeOffset LastSeen { get; set; }
    }
}
