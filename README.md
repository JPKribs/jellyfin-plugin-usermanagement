# ![User Management](Jellyfin.Plugin.UserManagement/Assets/Logo.png)

A Jellyfin plugin that brings group permission templates, account lifecycle, and password hygiene to the users you already have. Define reusable permission groups with live sync, hand out self-service signup invites, and enforce password requirements — all from the dashboard, with no second process or extra port.

---

## How It Works
User Management adds a **User Management** page to your Jellyfin dashboard with three tabs. Groups push a chosen set of permissions onto their members and keep them reconciled on a schedule. Invites let new people create their own non-administrator accounts behind a PIN. Password requirements are enforced through a dedicated authentication provider on the members you choose.

Policy is applied directly inside Jellyfin as a set of services and scheduled tasks. Administrators are never modified, so a group can never strip your own rights.

## Use At Your Own Risk
This plugin modifies user policies, password enrollment, and can disable or permanently delete accounts in your Jellyfin server. While it is built to leave administrators untouched and to validate every change, I cannot account for every server configuration or edge case. **Always maintain backups of your Jellyfin data and configuration.** By using this plugin, you accept full responsibility for any account changes or issues that may occur.

---

## Features

### Groups
Reusable permission templates applied to existing accounts.

Each permission in a group is either *overridden* (the group's value is pushed onto every member) or *inherited* (left exactly as the user has it), so a group only touches what you tell it to. Members are reconciled back to the template on a schedule, repairing any drift from manual dashboard edits. A user belongs to at most one group at a time, and administrators can never be added.

**Group Expiry:**
- **Disable**: on the group's expiry date, member accounts are disabled
- **Delete**: on the group's expiry date, member accounts are permanently removed

### Invites
Shareable, self-service signup links.

Generate a link that lets a new person create their own account, optionally protected by a PIN, with an expiry date and a usage limit. New accounts are placed in the invite's group and are **never** administrators. The invite locks itself after too many wrong PIN attempts and stops working once it expires or is fully used.

**PIN Handling:**
- **Hashed**: the PIN is stored only as a salted hash, never in plaintext
- **Locked**: the invite disables itself after a configurable number of wrong attempts

### Settings
Server-wide configuration.

- **Default Group**: the group new users, and accounts created outside an invite, are automatically placed in
- **Invite URL**: the public base URL used to build invite links, for when users reach the server through a reverse proxy
- **Password Requirements**: minimum length and required character classes, enforced on invite signups and on the members of any password-enabled group

## Enforcement Architecture

User Management applies policy through four cooperating pieces, so changes stay consistent whether they come from the dashboard, an invite, or a background task:

### Layer 1: Group Templates (Override / Inherit)
The source of truth for permissions. Each group records, per permission, whether it is managed (overridden) or inherited. Only managed permissions are ever written to a member's policy; everything else is left exactly as the user has it.

### Layer 2: Scheduled Reconciliation
Background tasks keep reality in line with the templates.

**Tasks:**
- **Apply group permissions**: re-pushes each group's managed permissions to its members (default: every 12 hours)
- **Add users to groups**: places any account not yet in a group into the default group (default: every 12 hours)
- **Apply group expiry**: disables or deletes members of groups that have reached their expiry date (default: every 24 hours)

### Layer 3: Password-Rule Enforcement
Members of a password-enabled group are moved to a dedicated authentication provider that validates new passwords against the group's rules. Login verification is unchanged — it delegates to Jellyfin's built-in cryptography — and a user's original provider is recorded so it can be restored exactly when they leave the enforcing group.

### Layer 4: Self-Service Invites
The only anonymous, public-facing surface. All validation — token, PIN, expiry, usage — happens server-side, redemption is single-threaded to prevent double-use, and every created account is forced to be a non-administrator.

### Reconciliation Pipeline
Administrators are exempt at every layer, and configuration is read and written under a process-wide lock, so concurrent dashboard edits and background tasks can't corrupt each other. The modular design makes it straightforward to add new managed permissions or lifecycle actions.

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
