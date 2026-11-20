# Documentation

How to write the README, the docs site, screenshots, and release notes for this plugin. It applies to every page under `docs/`, to `README.md`, and to the `changelog` in `build.yaml`.

## The One Rule

Documentation describes the release it ships with. It reads as if this version were the only one that ever existed.

* Write what the plugin does. Never write what it used to do, what it no longer does, or what it now does.
* Write how a feature works. Never write that a bug was fixed, a problem is gone, or a limit was lifted.
* History belongs in the release notes. See [Release Notes](#release-notes).

| Do not write | Write |
| :--- | :--- |
| Invites now keep their PIN after a restart. | Invites keep their PIN after a restart. |
| The old Lifecycle tab was removed. | Leave it out. |
| No longer applies rules to administrators. | Group rules never apply to administrators. |
| Fixed an issue where expired invites stayed active. | An invite past its expiration stops working. |
| New in 12.4.3.0, session cleanup logs out... | Session cleanup logs out... |
| This release carries no upgrade steps. | Version 12.4.3.0 carries no upgrade steps. |

"Now" and "no longer" are fine when they describe state rather than history. "The user is no longer in the group" describes what the plugin sees, so it stays.

A version number belongs in docs only when the reader needs it to act, such as the minimum Jellyfin version or the oldest release that can upgrade directly. Keep those facts in [Installation](../docs/Installation.md) and [Versioning](../docs/Versioning.md), and name the version outright instead of "this release" or "an older release".

Remove notes about Jellyfin versions the plugin no longer supports. The plugin requires Jellyfin 12.4, so text about 12.2 describes servers that cannot run it.

## Writing Style

### Sentences

* Use present tense and plain declarative sentences.
* Describe the system in the third person. "User Management applies the group to its members."
* Address the reader in the second person only in steps. "Turn on **Enable reset code extraction** on the **Resets** tab."
* State one fact per sentence. Aim for 20 words or fewer and split anything over 30.
* Use no contractions. Write "cannot", not "can't".
* Use no em dashes, en dashes as dashes, or semicolons. Split the sentence, or join it with a comma and "and".
* Use colons only to introduce a list or a code block.
* Use parentheses rarely. Move the aside into its own sentence.
* End every sentence with a period.
* Write compounds as separate words the way the UI does, such as "one time setup" and "sign in".
* Write one paragraph per line. Never hard wrap prose.
* Use no marketing words such as "powerful", "seamless", "easily", or "simply".
* Write examples with generic names. Use `alice`, `viewer`, `Family`, and `jellyfin.example.com`, never a real server, person, or address.

### Headings

* Use Title Case for every heading. "## Disabled Invites".
* Page titles are `#` and appear once per page.
* Name the thing, not the action, unless the section is a procedure. "## Rules" and "## Add the Plugin Repository" are both correct.
* Never rename a heading without fixing its anchors. `mkdocs build --strict` fails on a broken anchor.

### UI Names

* Write a label exactly as the page shows it, in bold. **Invite rate limit**.
* Write a path through the UI with `>` between the steps, in bold. **Dashboard > Plugins > User Management > Invites**.
* Write a tab as its label, in bold. **Groups**, **Invites**, **Resets**.
* Quote activity log names and on-screen messages in double quotes. "Too many attempts".
* Write routes, files, settings keys, and values in code. `POST /UserManagement/Apply`.

A docs page and the UI must use the same word for the same thing. When a label changes, search `docs/` for the old one and change it everywhere in the same commit.

## Site Structure

The docs site is MkDocs with the Material theme, built from `docs/` by `mkdocs.yml` and published to `https://jpkribs.github.io/jellyfin-plugin-usermanagement/` by the **Publish Docs** workflow.

### Navigation

Keep the nav in this order.

1. **Overview** (`index.md`). What the plugin is, what it manages, and how it works.
2. **Getting Started**. Installation.
3. **Features**. Groups, Passwords, Expiration, Inactivity, Sessions, Invites, Activity Log, and Password Resets, one page each.
4. **Reference**. Uninstalling and Versioning.

Name page files in PascalCase with no spaces, such as `PasswordResets.md`.

## Page Shapes

A feature page follows this shape. It opens with one sentence on what the feature is, then one section per question a reader asks, then the limits and warnings where they apply.

```markdown
# Sessions

One sentence that says what the feature does.

![Session cleanup rules](screenshots/Sessions.png)

## Rules

* **All clients** cleans up every client.
```

### Settings References

List settings as bullets in the order the page shows them. Start each with the label in bold and follow it with a verb.

```markdown
* **Minimum length** sets the fewest characters a password may have.
```

When a page has many settings with defaults, use a table instead.

| Setting | What it does | Default |
| :--- | :--- | :--- |
| **Disallow empty passwords** | Refuses an empty password | On |

Table cells have no closing period. Keep them to one sentence.

### Troubleshooting

Write trouble as a table of what the reader sees and what to do.

| Reason | What to do |
| :--- | :--- |
| Too many attempts | Wait 15 minutes, or add the reverse proxy to **Known proxies** when every visitor shares one address |

Put the exact on-screen text in the first column. Use italics for a placeholder inside it, such as *invite*.

### Admonitions

Give every admonition a short title.

```markdown
!!! warning "Use at your own risk"
    One or two sentences.
```

* `note` for a detail the reader needs.
* `tip` for a better way to do something.
* `warning` for anything that can lose data or break a server.
* `info` for a requirement, such as both servers running the plugin.

### Links

* Link pages relatively with their `.md` name, such as `[password changes](Passwords.md#password-changes)`.
* End a Getting Started page with one "Next" line, such as `Next: [Groups](Groups.md).`
* Link to Base's README for a shared component instead of describing it again.

## README

The README is a landing page. The docs site holds the detail.

1. The icon, `<img src="docs/assets/icon.png" alt="User Management" width="96" align="left">`, then a one paragraph pitch and `<br clear="left">`.
2. `## Documentation`. One sentence on what the docs cover, a bold link to the site, then the repository URL for Jellyfin in a code block.
3. Any notice the owner keeps at the end, unchanged.

Use `##` for README headings. Keep the README under 40 lines.

## Screenshots

### What to Capture

* Capture every page and dialog a docs page describes.
* Capture the dark theme only, which is Jellyfin's default.
* Capture with the screenshot kit, whose users, groups, and invites have generic names.
* Capture with groups, invites, and pending resets in place so lists, counts, and badges are filled.
* Hide nothing with editing. When a value is private, change the test data instead.

### The Screenshot Kit

`tools/screenshots/` takes every shot from the plugin's own pages with generic sample data, and Activity Log from a throwaway Docker server. Its [README](../tools/screenshots/README.md) lists what it needs and how to run it.

```bash
cd tools/screenshots
python3 -I server.py &
PLAYWRIGHT_CORE=/path/to/node_modules/playwright-core/index.mjs node shoot.mjs
```

`python3 -I activity.py` takes Activity Log. `node shoot.mjs "Invite List"` retakes only the named files. A new subject is one more entry in the `shots` list of `shoot.mjs`.

### Sizes

| Kind | Size | How |
| :--- | :--- | :--- |
| Dialog | 800 wide | The browser at 800 px, cropped to the dialog |
| Full page | 1920 wide | The browser at 1920 px, cropped to the content area |
| Phone | 375 wide at 2x | The browser's device mode at 375 px |

Save every capture as PNG. Crop out the browser frame and the Jellyfin header unless the header is the subject.

### Names and Placement

* Save to `docs/screenshots/`.
* Name the file in Title Case after what it shows, with spaces. `Create Invite.png`, `Invite List.png`.
* Link with `%20` for spaces and alt text that names the subject. `![Create Invite form](screenshots/Create%20Invite.png)`.
* Place two related shots side by side in a table whose header row holds the captions.

```markdown
| Invite List | Invite List on a phone |
| :---: | :---: |
| ![Invite List](screenshots/Invite%20List.png) | ![Invite List on a phone](screenshots/Invite%20List%20Phone.png) |
```

### When to Retake

Retake a screenshot in the same commit as any change that alters what it shows, including a label, a tab, a layout, or the Jellyfin version in the header. A new Base version changes how every component looks, so retake every screenshot when it is raised, with the kit for those it covers. A screenshot that shows a tab or label the plugin no longer has is a bug.

## Installing a Build by Hand

```bash
./build.sh
```

The zip lands in `dist/`. Unzip it into `<config>/plugins/User Management_<version>/` on a Jellyfin 12.4 test server and restart the server.

## Building the Docs

```bash
pip install mkdocs==1.6.1 mkdocs-material==9.7.7
```

```bash
mkdocs serve -a 127.0.0.1:8765
```

```bash
mkdocs build --strict
```

Run the strict build before every docs commit. CI runs the same build and refuses broken links, anchors, and nav entries.

## Release Notes

Release notes live in the `changelog` of `build.yaml`, under four keys. The publish workflow copies them into `manifest.json`, so every line reaches users' plugin catalogs.

| Key | Holds | Voice |
| :--- | :--- | :--- |
| `added` | A feature that did not exist | The feature name, then what it does |
| `changed` | Behavior a user will notice | The new behavior, in present tense |
| `fixed` | A bug | What went wrong in past tense, then the fix in present tense |
| `developer` | Routes, schema, tests, and tooling | Plain statements for other developers |

```yaml
changelog:
  added:
    - Session cleanup rules, which log out a group's stale devices per client
  fixed:
    - The arrows on collapsible sections are back to their usual size
```

* This is the one place that may say "now", "no longer", and "was".
* Write full sentences with no closing period.
* Write one entry per change a user would notice. Fold internal steps into the change they serve.
* Name UI labels exactly as the page shows them, without bold or backticks.
* Name the Base version in `developer`, with what it replaced locally.
* Give a new or changed route or query parameter a `developer` line.

## Commit Messages

* Write an imperative sentence in sentence case with no closing period. "Sum activity log entries per outcome and give them one shape".
* Describe the behavior, not the files. Use no prefixes such as `fix:` or `feat:`.
* Add a body of plain paragraphs, wrapped at 72 characters, when the reason is not obvious.
* Commit docs and screenshots with the change they describe.

## Checklist

* [ ] Nothing describes an earlier version, a removed feature, or a fixed bug.
* [ ] Every UI label matches the page exactly and is bold.
* [ ] Every heading is Title Case.
* [ ] No em dashes, semicolons, or contractions.
* [ ] Every screenshot matches the current UI and uses generic test data.
* [ ] Every term matches [glossary.md](glossary.md).
* [ ] `mkdocs build --strict` passes.
* [ ] `build.yaml` has a changelog entry for any change a user would notice.
