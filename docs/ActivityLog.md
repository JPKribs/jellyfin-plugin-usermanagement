# Activity Log

The plugin writes its notable events to Jellyfin's activity log (**Dashboard → Activity**), so administrative changes stay auditable without opening the server log.

* **Group created / deleted**: A group was added to or removed from the configuration.
* **Password rules enrolled / unenrolled**: A user was placed under, or released from, a group's password rules.
* **Password change blocked**: A self service change was attempted in a group that disallows them, logged as a warning.
* **Password change rejected**: A new password failed the group's rules, logged as a warning with the reasons.
* **Invite created / redeemed / consumed / expired**: The invite lifecycle, including the user each redemption created.
* **Incorrect PIN / invite locked**: A wrong PIN attempt and the lockout after too many, both logged as warnings.
* **Account disabled / deleted by group expiry**: A member was disabled or deleted because their group's expiry date passed, logged as a warning.
* **Account disabled for inactivity**: A member was disabled by their group's inactivity window, logged as a warning.
