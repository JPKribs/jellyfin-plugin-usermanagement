# Activity Log

The plugin writes its notable events to Jellyfin's activity log (**Dashboard → Activity**), so administrative changes stay auditable without opening the server log.

Plugin entries show up alongside Jellyfin's own, with warnings flagged in the Level column.

![Plugin entries in the activity log](screenshots/Activity%20Log.png)

Entries share one shape: a short sentence case name over one sentence saying what happened. Bursts and scheduled runs are summed into one entry per outcome that counts what changed and names who, instead of one entry per user or attempt. Security events on a single account stay as their own entries.

Entries read in the server's display language (**Dashboard → General → Preferred display language**).

* **Group created / Group deleted**: A group was added to or removed from the configuration. The overview names the group.
* **3 users enrolled in password rules / 3 users removed from password rules**: Users were placed under, or released from, a group's password rules. Changes that arrive together, such as a group save applying to every member, are summed into one entry that names each user.
* **Password change blocked for alice**: A self service change was attempted in a group that disallows them, logged as a warning.
* **Password change rejected for alice**: A new password failed the group's rules, logged as a warning with the rules it broke.
* **Invite created / Invite used up**: An invite was created, or reached its limit of uses. The overview names the invite.
* **Account created for alice**: Someone redeemed an invite. The overview names the invite.
* **2 invites expired**: The scheduled task disabled invites past their expiry date, in one entry naming each invite.
* **3 PIN attempts failed**: Wrong invite PINs, summed and counted by invite, logged as a warning.
* **Invite locked**: An invite was locked after too many incorrect PIN attempts, logged as a warning.
* **2 users disabled / 2 users deleted**: The scheduled task disabled or deleted members because their group's expiry date passed, or disabled them for inactivity. Each run writes one entry per outcome, counted by group and naming each user, logged as a warning.
* **3 sessions logged out**: Session cleanup logged out stale sessions, counted by client app.
