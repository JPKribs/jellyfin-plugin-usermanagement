# Activity Log

The plugin writes its notable events to Jellyfin's activity log (**Dashboard → Activity**), so administrative changes stay auditable without opening the server log.

Plugin entries show up alongside Jellyfin's own, with warnings flagged in the Level column.

![Plugin entries in the activity log](screenshots/Activity%20Log.png)

Entries share one shape: a short, generic name in sentence case over one sentence that says who, what, and where. The name holds no user and no count, and the overview states each fact once. Bursts and scheduled runs are summed into one entry per outcome that names who changed, instead of one entry per user or attempt. Security events on a single account stay as their own entries.

Entries read in the server's display language (**Dashboard → General → Preferred display language**).

* **Group created / Group deleted**: A group was added to or removed from the configuration. The overview names the group.
* **Users enrolled in password rules / Users removed from password rules**: Users were placed under, or released from, a group's password rules. Changes that arrive together, such as a group save applying to every member, are summed into one entry that names each user, such as "Enrolled 2 users in group password rules: alice, bob".
* **Password change blocked**: A user tried a self service change in a group that disallows them, logged as a warning. The overview names the user.
* **Password change rejected**: A new password failed the group's rules, logged as a warning. The overview names the user and the rules it broke.
* **Invite created / Invite used up**: An invite was created, or reached its limit of uses. The overview names the invite.
* **Account created**: Someone redeemed an invite, such as "alice signed up with the invite Family".
* **Invites expired**: The scheduled task disabled invites past their expiration date, in one entry naming each invite.
* **PIN attempts failed**: Wrong invite PINs, summed and counted by invite, logged as a warning.
* **Invite locked**: An invite was locked after too many incorrect PIN attempts, logged as a warning.
* **Users disabled / Users deleted**: The scheduled task disabled or deleted members because their group's expiry date passed, or disabled them for inactivity. Each run writes one entry per outcome naming each user and their group, logged as a warning.
* **Sessions logged out**: Session cleanup logged out stale sessions, named by user and client app, such as "Logged out 3 stale sessions for alice on Infuse (2) and bob on Swiftfin".

An entry that covers one item uses the singular name, such as "Session logged out".
