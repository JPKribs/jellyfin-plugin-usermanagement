# Activity Log

The plugin writes its notable events to Jellyfin's activity log (**Dashboard → Activity**), so administrative changes stay auditable without opening the server log.

Plugin entries show up alongside Jellyfin's own, with warnings flagged in the Level column.

![Plugin entries in the activity log](screenshots/Activity%20Log.png)

Entries share one shape: a short title case name over one sentence saying what happened. Bursts and scheduled runs are summed into one entry per outcome that counts what changed and names who, instead of one entry per user or attempt. Security events on a single account stay as their own entries.

* **Group Created / Group Deleted**: A group was added to or removed from the configuration. The overview names the group.
* **3 Users Enrolled in Password Rules / 3 Users Removed from Password Rules**: Users were placed under, or released from, a group's password rules. Changes that arrive together, such as a group save applying to every member, are summed into one entry that names each user.
* **Password Change Blocked for alice**: A self service change was attempted in a group that disallows them, logged as a warning.
* **Password Change Rejected for alice**: A new password failed the group's rules, logged as a warning with the rules it broke.
* **Invite Created / Invite Used Up**: An invite was created, or reached its limit of uses. The overview names the invite.
* **Account Created for alice**: Someone redeemed an invite. The overview names the invite.
* **2 Invites Expired**: The scheduled task disabled invites past their expiry date, in one entry naming each invite.
* **3 PIN Attempts Failed**: Wrong invite PINs, summed and counted by invite, logged as a warning.
* **Invite Locked**: An invite was locked after too many incorrect PIN attempts, logged as a warning.
* **2 Users Disabled / 2 Users Deleted**: The scheduled task disabled or deleted members because their group's expiry date passed, or disabled them for inactivity. Each run writes one entry per outcome, counted by group and naming each user, logged as a warning.
* **3 Sessions Logged Out**: Session cleanup logged out stale sessions, counted by client app.
