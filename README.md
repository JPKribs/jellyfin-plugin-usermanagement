# ![User Management](Jellyfin.Plugin.UserManagement/Assets/Logo.png)

A Jellyfin plugin that brings group permission templates, account lifecycle, and password hygiene to the users you already have. Define reusable permission groups with live sync, hand out self-service signup invites, and enforce password requirements — all from the dashboard, with no second process or extra port.

---

## How It Works
User Management adds a **User Management** page to your Jellyfin dashboard with two tabs — Groups and Invites. Everything is applied directly inside Jellyfin through services and scheduled tasks. Administrators are never modified, so a group can never strip your own rights.

### Groups
A group defines a set of user permissions and settings. When you apply changes — and whenever the scheduled task runs — every member of the group is set to those values. Only permissions marked as an **override** are written; anything not overridden keeps whatever each user already has set manually.

### Invites
A shareable signup link tied to a group. Anyone with the link can create their own account — optionally behind a PIN, with an expiration date and a usage limit. New accounts are placed in the invite's group and are never administrators.

### Password
Each group can act as a password authentication service for its members. When a member changes their password, the group's password conditions are enforced — a password that doesn't meet them is rejected. Otherwise nothing else about the account changes.

### User Expiration
When the expiry task runs, any group whose expiration date is on or before today has its members disabled. You can instead set a group to **delete** its members on expiration — be careful, as deletion is irreversible.

## Use At Your Own Risk
This plugin modifies user policies, password enrollment, and can disable or permanently delete accounts in your Jellyfin server. While it is built to leave administrators untouched and to validate every change, I cannot account for every server configuration or edge case. **Always maintain backups of your Jellyfin data and configuration.** By using this plugin, you accept full responsibility for any account changes or issues that may occur.

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

Claude Code was utilized in the creation of this project and first drafts of documentation. All code has been manually reviewed and revised after its generation.

**All code was reviewed and tested by humans.**
