# ![User Management](Jellyfin.Plugin.UserManagement/Assets/Logo.png)

A Jellyfin plugin for managing the users you already have: define reusable permission groups, hand out self-service signup invites, and enforce password requirements — all from the dashboard, with no second process or extra port.

---

## How It Works
User Management adds a **User Management** page to your Jellyfin dashboard with three tabs. **Groups** are permission templates: for each permission you choose whether the group *overrides* it (the value is pushed onto every member) or *inherits* it (left as the user has it), and a scheduled sync keeps members in line. **Invites** generate a shareable signup link — protected by a PIN, with an expiry and a usage limit — that lets a new person create their own account. **Settings** holds the default group for new users, the public invite URL, and password requirements you can enforce on the users you choose. Administrators are never modified by the plugin, so a group can't strip your own rights.

## Use At Your Own Risk
This plugin is still very early and much of the scaffolding was created by Claude Code. **This is not ready for usage and should not be used in a production environment!**

---

## Getting Started

### 1. Groups
Create permission templates and assign members.

1. On the **Groups** tab, click **New** and give the group a name.
2. Expand a permission section and flip **Override** on a permission to have the group control it; leave it off to inherit the user's existing setting.
3. Under **Members**, tick the users this group applies to. A user can only be in one group at a time, and administrators can't be added.
4. Click **Save** — the group's managed permissions are applied to its members immediately, and a scheduled task keeps them reconciled.

> Removing a user from a group stops future syncs but does **not** revert permissions already applied to them.

### 2. Invites
Hand out self-service signup links.

1. On the **Invites** tab, set an optional **PIN**, **expiry**, **max uses**, and a **group** for new accounts, then click **Create Invite**.
2. Copy the generated link and share it. Opening it shows a signup page where the person enters the PIN and chooses a username and password.
3. New accounts are added to the invite's group and are **never** administrators. The invite locks itself after too many wrong PIN attempts, and stops working once it expires or is fully used.

### 3. Settings
General configuration, grouped into collapsible sections.

- **Default Group** — the group new users are automatically placed in.
- **Invites** — the public base URL used to build invite links (set this to the address your users reach the server at, e.g. through a reverse proxy).
- **Password Requirements** — minimum length and required character classes, whether empty passwords are allowed, whether new users are enrolled automatically, and a per-user list of exactly who the rules apply to. Rules are always enforced on invite signups; for existing users they apply to whoever you enroll here. A **Revert** button returns everyone to Jellyfin's built-in provider.

---

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

## AI Disclaimer

Claude Code was used extensively to build this plugin. At this time, it is **NOT RECOMMENDED FOR USAGE!**
