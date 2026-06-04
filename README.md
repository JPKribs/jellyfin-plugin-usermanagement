# ![User Management](Jellyfin.Plugin.UserManagement/Assets/Logo.png)

A Jellyfin plugin for managing the users using bulk tooling. This includes, reusable permission groups, self-service signup invites, password rules, and account expiration, all from the dashboard with no second process or extra port.

---

## How It Works
User Management adds a page to your dashboard with **Groups** and **Invites** configuration. Everything runs inside Jellyfin itself, with no extra service or port, and administrators are never touched, so a group can't lock you out of your own server.

### Groups
A group is a set of permissions and settings applied to many users at once. Each setting is either an **override** where it's forced onto every member or left alone, so the member keeps whatever they already have. Members re-sync when you save and on a schedule, correcting any manual drift.

### Invites
A shareable signup link tied to a group. Anyone with the link can create their own account, optionally behind a PIN and with an expiration date and usage limit. New accounts join the invite's group and are never administrators.

### Password
A group can enforce password rules on its members, such as minimum length and required character types. When a member sets a new password it must meet the rules or the change is rejected; nothing else about how they sign in changes.

### User Expiration
Give a group an expiration date and its members are disabled once that date passes, checked by a daily task. You can choose to delete members instead. **Please note, this is irreversible, so use it with care!**

### User Inactivity
Set an inactivity limit for users in a group. Users who have not been active within this window are set to disabled when checked by a daily task. Accounts that have never signed in are left alone.

## Use At Your Own Risk
This plugin changes user policies and password enrollment, and can disable or permanently delete accounts. It's built to leave administrators untouched and to validate every change, but I can't account for every server or edge case. **Always keep backups of your Jellyfin data and configuration.** By using it, you accept responsibility for any account changes that result.

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
