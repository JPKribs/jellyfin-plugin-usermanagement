# ![User Management](Jellyfin.Plugin.UserManagement/Assets/Logo.png)

**A Jellyfin plugin for managing Jellyfin users via groups. This plugin manages:**

- **Permissions** — Enforce the same permissions across multiple users using groups.
- **Password Validation** — Enforce password length and complexity for all users in a group.
- **User Expiry** — Disable or delete user accounts based on a group set date.
- **User Inactivity** — Disable inactive user accounts based on a group time limit.
- **Invite Links** — Create an invite link to create new accounts on your server and automatically assign them a group.

## How It Works
User Management uses groups to assign permissions and rules to all users who are a part of this group. New users can be onboarded from an invite link and has group permissions assigned to them on creation. On update, this plugin finds all users in these groups and updates their `UserDto.userConfiguration` using all overridden permissions and settings. Anything not found in the base `UserDto` is stored and referenced from the plugin storage.

### Groups
A group is a set of permissions and settings applied to many users at once. Each setting is either an **override** where it's forced onto every member or left alone, so the member keeps whatever they already have. Members re-sync when you save and on a schedule, correcting any manual drift.

This action is performed by looping over all users assigned to a group and updating their `UserDto.Policy` to conform to your group settings. Any elements that are *not set* to override will be retained from the original `UserDto.Policy` from the current user configuration.

### Passwords
A group can enforce password rules on its members, such as minimum length and required character types. When a member sets a new password it must meet the rules or the change is rejected. This plugin does not change the standard login process so all else will be kept the same.

### User Expiration
Give a group an expiration date and its members are disabled once that date passes. This is checked by the `Process expired and inactive users` **Scheduled Task** so you determine what time of day these expirations occur. 

There is an optional setting to change the disable action into a **deletion**. **Please note, this is irreversible, so use it with care!**

### User Inactivity
Set an inactivity limit for users in a group. Users who have not been active within this window are set to disabled when checked by the `Process expired and inactive users` **Scheduled Task**. Accounts that have never signed in are ignored to prevent first time users from being disabled.

### Invites
Create a shareable signup link tied to a group. Anyone with the link can create their own account on your server. All users that use the link will be created using the group assigned to it. For added security, there is an optionally PIN that can be set and the user will have to provide it to use the link. Additionally, you can set a rate limit of how many times the link can be used over a period of time to avoid spam.

**To prevent abuse, it is recommended to create a new link for each user that you want to onboard!**

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
