# Start Here

Read this file before you change anything in this repository. It applies to people and to AI assistants alike.

## The Guides

Each guide in [skills](skills/) covers one kind of work. Read the guide that matches the task before you start it.

| Guide | Read it before you |
| :--- | :--- |
| [skills/glossary.md](skills/glossary.md) | Name anything, in the UI, the docs, or code. |
| [skills/style.md](skills/style.md) | Build or change anything a user sees, including labels, messages, and layout. |
| [skills/code.md](skills/code.md) | Write or change code or comments in any language. |
| [skills/testing.md](skills/testing.md) | Write or run tests, or check a page in a browser. |
| [skills/profiling.md](skills/profiling.md) | Find or fix slow C#, or change code on a hot path. Profile only for a reported symptom, a hot path change, or a number that grew. |
| [skills/documentation.md](skills/documentation.md) | Write or change the README, the docs site, screenshots, or release notes. |
| [skills/review.md](skills/review.md) | Call any change done. |
| [skills/release.md](skills/release.md) | Cut a release. |
| [skills/branches.md](skills/branches.md) | Pick a branch, sync the two, and promote unstable. |
| [skills/translations.md](skills/translations.md) | Add or change a string a person reads. |
| [skills/logging.md](skills/logging.md) | Write an activity log entry or a server log line. |

A task that touches several areas needs every matching guide. A new group setting, for example, needs the glossary, style, code, testing, documentation, and review guides.

## Ground Rules

* **Base comes first.** Anything reusable lives in [JPKribs.Jellyfin.Base](https://github.com/JPKribs/jellyfin-plugin-base). Search it before you build anything, use what it has, and never copy or override it locally.
* **Describe the current release only.** Docs, labels, and comments say what the plugin does today. Release notes are the only place for what changed.
* **Keep it short.** Every label, message, and comment is as brief as it can be while staying clear.
* **One word per concept.** Use the term in the glossary, everywhere.
* **Prove it with a test.** A change is not done until a test shows that its inputs give the right output.

## Verify Before Done

Work is done when it has been checked, not when it has been written.

* Run every check the change needs, and read its output. A check that did not run has not passed, and neither has one that ran before the last change.
* Look at every changed page in a running Jellyfin, or in a browser harness when no test server is available, and say which.
* Read the whole diff before reporting, using [skills/review.md](skills/review.md).
* Check every new string against the style checklist and the glossary.
* Check every claim in a doc against the code or the running plugin.
* Report what was checked, what was not, and why. Never call work done while a check fails.

## Stop and Ask

Stop and ask before any of these, even when the task seems to call for it.

* **User accounts.** Anything that creates, disables, or deletes a Jellyfin user, or changes how expiry and inactivity pick which users to act on.
* **Passwords.** A change to password rules, the password change modes, the authentication provider, or provider enrollment.
* **Invites.** A change to invite tokens, PINs and their lockout, the rate limits, redemption, or which group an invite signs users into.
* **Reset codes.** A change to how reset codes are read, shown, or copied on the **Resets** tab.
* **The public invite page.** Any change to `usermanagement_invite_public.html` or the anonymous `Invite` routes, since anyone with a link can reach them.
* **Stored data.** A change to the plugin configuration's shape or the invite status file, `Jellyfin.Plugin.UserManagement.InviteStatus.json`, or anything that resets, moves, or deletes them.
* **Security.** A change to authentication, route authorization, secrets, or how a client address is read and limited.
* **Base.** Any change to Base, since every plugin takes it.
* **Settings.** Renaming, removing, or changing the default of a setting users already have.
* **Releases.** Raising the version, publishing, or pushing to `master`.
* **The rules.** Any case where following a guide would make the result worse.

Explain what you would change, why, and what it risks. Wait for an answer before you act.

## The Repository

| Path | What it holds |
| :--- | :--- |
| `Jellyfin.Plugin.UserManagement/` | The plugin source. |
| `Jellyfin.Plugin.UserManagement/Configuration/` | The plugin configuration. |
| `Jellyfin.Plugin.UserManagement/Web/` | The dashboard pages and the public invite page, as HTML, JS, and CSS. |
| `Jellyfin.Plugin.UserManagement/Api/` | The dashboard API, its strings route, and the anonymous invite routes. |
| `Jellyfin.Plugin.UserManagement/Localization/` | The plugin's own strings, one file per language. |
| `Jellyfin.Plugin.UserManagement/Services/` | Groups, invites, passwords, sessions, reset codes, and the activity log. |
| `Jellyfin.Plugin.UserManagement/Tasks/` | The scheduled tasks. |
| `Jellyfin.Plugin.UserManagement/Models/` | Groups, invites, password rules, and other stored data. |
| `Jellyfin.Plugin.UserManagement/Utilities/` | Static helpers and extension methods. |
| `Jellyfin.Plugin.UserManagement.Tests/` | The unit and controller tests. |
| `docs/` | The documentation site, built with MkDocs. |
| `build.yaml` | The version, the artifacts, and the release notes. |
| `build.sh` | Builds the plugin zip into `dist/`. |
| `skills/` | The guides. |
| `tools/` | Development tools that never ship, such as the screenshot harness. |
| `../Shared/jellyfin-plugin-base/` | A local checkout of Base, when present. Read it, and never change it from work on this plugin. |
