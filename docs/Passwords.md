# Passwords

A group can enforce password rules on its members, such as minimum length and required character types. When a member sets a new password it must meet the rules or the change is rejected. This plugin does not change the standard login process, so all else is kept the same.

## Password Changes

A **Password changes** dropdown controls whether members may set or change their own password:

* **Allowed**: Members manage their own password, subject to the group's rules.
* **Initial password only**: A member whose password is *currently empty* may set one, but an *existing password* cannot be changed.
* **Disallowed**: Members can neither set nor change a password. Only administrators can.

| Action | Allowed | Initial password only | Disallowed |
| --- | --- | --- | --- |
| Member changes their existing password | ✅ If it meets the rules | ❌ Blocked | ❌ Blocked |
| Member with an empty password sets one | ✅ If it meets the rules | ✅ If it meets the rules | ❌ Blocked |
| Invite signup chooses the account password | ✅ If it meets the rules | ✅ If it meets the rules | ✅ If it meets the rules |
| Administrator changes a member's password | ✅ Always | ✅ Always | ✅ Always |

## Notes

* **Administrators are exempt.** Group password rules *never* apply to admin accounts, and admins can never be enrolled in enforcement. A member who is promoted to administrator is dropped from their group's member list on the next save and put back on their original authentication provider by the next sync, so a promotion can never strand them on this plugin's provider.
* **Allowing empty passwords takes priority.** When **Disallow empty passwords** is false, an empty password is accepted even if it fails the other rules, because an empty password means the account deliberately has no password. This is why **Disallow empty passwords** is true by default.
* **Disallowed groups cannot take invites.** A group whose members may never set a password is admin managed by definition, so it cannot be chosen for [invite links](Invites.md). Switching a group to **Disallowed** also disables its outstanding invites, including default group invites when it is the default group. An invite is only allowed if a user can set their password *at least on creation*.

!!! warning "Before uninstalling"
    Members of groups that enforce password requirements are moved onto this plugin's authentication provider. Read [Uninstalling](Uninstalling.md) before removing the plugin.
