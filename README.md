# ![User Management](Jellyfin.Plugin.UserManagement/Assets/Logo.png)

**A Jellyfin plugin for managing Jellyfin users via groups. This plugin manages:**

- **Permissions** — Enforce the same permissions across multiple users using groups.
- **Password Validation** — Enforce password length and complexity for all users in a group.
- **User Expiry** — Disable or delete user accounts based on a group set date.
- **User Inactivity** — Disable inactive user accounts based on a group time limit.
- **Session Cleanup** — Log out devices that have gone too long without checking back into the server.
- **Invite Links** — Create an invite link to create new accounts on your server and automatically assign them a group.

## How It Works
User Management uses groups to assign permissions and rules to all users who are a part of this group. New users can be onboarded from an invite link and has group permissions assigned to them on creation. On update, this plugin finds all users in these groups and updates their `UserDto.userConfiguration` using all overridden permissions and settings. Anything not found in the base `UserDto` is stored and referenced from the plugin storage.

### Groups
A group is a set of permissions and settings applied to many users at once. Each setting is either an **override** where it's forced onto every member or left alone, so the member keeps whatever they already have. Members re-sync when you save and on a schedule, correcting any manual drift.

This action is performed by looping over all users assigned to a group and updating their `UserDto.Policy` to conform to your group settings. Any elements that are *not set* to override will be retained from the original `UserDto.Policy` from the current user configuration.

### Passwords
A group can enforce password rules on its members, such as minimum length and required character types. When a member sets a new password it must meet the rules or the change is rejected. This plugin does not change the standard login process so all else will be kept the same.

A **Password changes** dropdown controls whether members may set or change their own password:

* **Allowed** — Members manage their own password, subject to the group's rules.
* **Initial password only** — A member whose password is *currently empty* may set one, but an *existing password* cannot be changed.
* **Disallowed** — Members can neither set nor change a password. Only administrators can.

| Action | Allowed | Initial password only | Disallowed |
| --- | --- | --- | --- |
| Member changes their existing password | ✅ If it meets the rules | ❌ Blocked | ❌ Blocked |
| Member with an empty password sets one | ✅ If it meets the rules | ✅ If it meets the rules | ❌ Blocked |
| Invite signup chooses the account password | ✅ If it meets the rules | ✅ If it meets the rules | ✅ If it meets the rules |
| Administrator changes a member's password | ✅ Always | ✅ Always | ✅ Always |

#### Note:

* **Administrators are exempt.** Group password rules *never* apply to admin accounts, and admins can never be enrolled in enforcement.
* **Allowing empty passwords takes priority.** When **Disallow empty passwords** is false, an empty password is accepted even if it fails the other rules, because an empty password means the account deliberately has no password. This is why **Disallow empty passwords** is true by default.
* **Disallowed groups cannot take invites.** A group whose members may never set a password is admin managed by definition, so it cannot be chosen for invite links. Switching a group to **Disallowed** also disables its outstanding invites, including default group invites when it is the default group. An invite is only allows if a user can set their password *at least on creation*.

### User Expiration
Give a group an expiration date and its members are disabled once that date passes. This is checked by the `Process expired and inactive users` **Scheduled Task** so you determine what time of day these expirations occur. 

There is an optional setting to change the disable action into a **deletion**. **Please note, this is irreversible, so use it with care!**

### User Inactivity
Set an inactivity limit for users in a group. Users who have not been active within this window are set to disabled when checked by the `Process expired and inactive users` **Scheduled Task**. Accounts that have never signed in are ignored to prevent first time users from being disabled.

### Session Management
Enable **Clean up old sessions** on a group and its members' stale devices are logged out by the `Clean Expired Sessions` **Scheduled Task**. A device is stale when it has not checked back into the server within a rule's window, so devices in active use are never touched.

Cleanup is driven by one or more **rules**. Each rule sets a number of days and which clients it covers:

* **All Clients** — Every client is cleaned up by this rule.
* **Only These Clients** — Only sessions on the selected clients are removed during cleanup.
* **All Except These Clients** — All sessions are cleaned up except sessions on the selected clients.

Multiple rules let different clients age out on different schedules, for example cleaning up Jellyfin Web after a week while giving TV apps three months. When several rules cover the same client, **the shortest window wins**, and the dashboard points out which clients overlap. The client list is built from the devices that have connected to your server, plus any clients already selected in a rule so a saved selection never disappears when its devices do.

Administrators are exempt: their devices are never logged out by session cleanup.

### Invites
Create a shareable signup link tied to a group. Anyone with the link can create their own account on your server. All users that use the link will be created using the group assigned to it. For added security, there is an optionally PIN that can be set and the user will have to provide it to use the link. Additionally, you can set a rate limit of how many times the link can be used over a period of time to avoid spam.

Each invite can also carry:

* **Name** — An admin name for the invite. It is only shown on the dashboard so this field is for admin reference only.
* **Welcome message** — Shown to the invitee under the signup heading.
* **Resources** — Title and URL pairs presented as link buttons after the account is created, for example a request site or a getting started guide. Only absolute http(s) URLs are accepted.

A group whose password changes are set to **Disallowed** cannot be used for invites, and switching a group to that mode disables its outstanding invite links. A disabled invite (or one past its expiration) cannot be manually re-enabled until the cause is fixed.

**To prevent abuse, it is recommended to create a new link for each user that you want to onboard!**

### Activity Log
The plugin writes its notable events to Jellyfin's activity log (Dashboard → Activity), so administrative changes stay auditable without opening the server log.

* **Group created / deleted** — A group was added to or removed from the configuration.
* **Password rules enrolled / unenrolled** — A user was placed under, or released from, a group's password rules.
* **Password change blocked** — A self service change was attempted in a group that disallows them, logged as a warning.
* **Password change rejected** — A new password failed the group's rules, logged as a warning with the reasons.
* **Invite created / redeemed / consumed / expired** — The invite lifecycle, including the user each redemption created.
* **Incorrect PIN / invite locked** — A wrong PIN attempt and the lockout after too many, both logged as warnings.

### Password Resets
When a user starts Jellyfin's **Forgot Password** flow, the server writes a reset code to a file on its filesystem. The **Resets** tab can surface those codes so an administrator can pass one along without needing file access to the server. The feature is **off by default** and must be enabled on the tab. **Only enable it when the dashboard is reached over HTTPS**, since over plain http the codes are readable by anyone watching the connection. Codes are masked on screen until revealed, and can be copied directly.

---

## Uninstalling

**If a group enforces password requirements, turn that enforcement off (or delete the group) and let the sync run before uninstalling the plugin.** Members of such groups are switched onto this plugin's authentication provider while enrolled, and disabling the enforcement switches every member back to the provider they had before. If the plugin is removed while users are still enrolled, Jellyfin assigns those users an invalid authentication provider and **they cannot sign in** until an administrator reassigns the authentication provider on each user's profile page.

This only affects members of groups with password requirements enabled. Users outside those groups and administrators (who are never enrolled) are unaffected.

---

## Versioning

Releases use a four-part version, `JJ.JJ.F.B`, that matches the supported Jellyfin version with the plugin's own feature/bug count:

```
10.11.1.2
└───┘ └┬┘
  │    └── 1 = Plugin feature release
  │        2 = Plugin bug/patch release within that feature
  │
  └─── 10.11 = Jellyfin version this build was tested/released for
```

Targets **Jellyfin 10.11.x** (`net9.0`, ABI `10.11.0.0`).

## Installation

### Step 1: Add Plugin Repository

* Open Jellyfin and navigate to Dashboard → Plugins → Repositories
* Click Add Repository
* Enter the following repository URL: `https://raw.githubusercontent.com/JPKribs/jellyfin-plugin-usermanagement/master/manifest.json`
* Click Save

### Step 2: Install Plugin

* Go to the Catalog tab in the Plugins section
* Find User Management in the catalog
* Click Install
* Wait for installation to complete

### Step 3: Restart Jellyfin

* Restart your Jellyfin server completely
* Wait for Jellyfin to fully start up

### Verification Check

* After restart, navigate to Dashboard → Plugins → User Management to confirm the plugin configuration page loads properly.

---

## AI Disclaimer

Claude Code was utilized in the initial structure of this project and first drafts of documentation. All code has been manually reviewed, tested, and revised after its generation. This disclaimer exists in the interest of transparency.

**All code was reviewed and tested by humans.**
