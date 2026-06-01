<div align="center">
  <img src="Jellyfin.Plugin.UserManagement/Assets/Logo.png" alt="User Management" width="160" />
  <h1>Jellyfin User Management</h1>
  <p>Group policy templates, account lifecycle, and password hygiene for users who already exist on your server.</p>
</div>

---

## What it does

User Management brings the *management* half of tools like JFA-Go inside the Jellyfin dashboard — no second process, no extra port, no reverse proxy. It manages accounts that already exist; self-service invites are deliberately out of scope.

Three jobs:

1. **Policy** — named **Groups** carry a `UserPolicy` shape (library access, max sessions, download/transcode flags, admin flag). Members inherit it, and a scheduled sync re-applies edits to every member — real enforcement, not a one-shot stamp.
2. **Lifecycle** — per-user **account expiry** (temp accounts) and **auto-disable of inactive users**. Accounts are disabled, never deleted.
3. **Password hygiene** — enforce length and character-class **requirements**, optionally **lock** password changes, and optionally reject breached passwords via **HaveIBeenPwned** (k-anonymity — only a SHA-1 prefix ever leaves the server).

Everything is administered from **Dashboard → Plugins → User Management** plus the plugin's own REST endpoints.

## Admin exemption (lockout safety)

Plugin-enforced restrictions never apply to administrators by default (`ExemptAdministrators = true`). This is a lockout-prevention hatch, not a convenience toggle — flip it off to enforce strictly once you trust your configuration.

## Architecture

One assembly, organized into feature modules so each area is built, reasoned about, and ported in isolation:

| Module | Seam | Risk |
|--------|------|------|
| `Groups`, `Lifecycle`, `Api`, `Common` | own REST controller + `IScheduledTask` + `IEventConsumer`, writing to `UserPolicy` | Low — sanctioned APIs |
| `Passwords` | `IAuthenticationProvider` | Medium — owns login for assigned users |

The version-sensitive surface (`User` entity, `ICryptoProvider`) is isolated inside `Passwords/` so a future Jellyfin major port is a small diff, not a rewrite.

## Building

```bash
./build.sh            # Release build + zip + md5 in ./dist
dotnet build -c Release
```

Targets **Jellyfin 10.11.x** (`net9.0`, ABI `10.11.0.0`).

## Installing (dev)

Drop `Jellyfin.Plugin.UserManagement.dll` into the Jellyfin `plugins/` folder and restart, or install from a plugin repo manifest pointing at `manifest.json`.

> **Note:** Implementing `IAuthenticationProvider` only makes it *available*. The plugin migrates existing users' `AuthenticationProviderId` at startup and catches new users via `UserCreatedEventArgs`, so password rules actually run.

## License

See [LICENSE](LICENSE).
