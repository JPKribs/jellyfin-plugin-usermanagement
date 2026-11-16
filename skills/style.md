# Style

How every page, dialog, label, and message in this plugin looks and reads. It is drawn from Base and from all the JPKribs plugins, so the same rules carry to each of them.

Base is [JPKribs.Jellyfin.Base](https://github.com/JPKribs/jellyfin-plugin-base). Its README documents each component's options. This guide says which component to use, when, and what to write in it.

## Principles

1. **One component per concept.** When Base has it, use it. Look before you build, per [Find the Component](#find-the-component). When Base lacks it and a second plugin needs it, add it to Base. Never build a second version.
2. **Short beats complete.** A label is a few words. A description is one sentence. A message is one sentence.
3. **The page explains itself.** A reader understands a setting from its label and its one line description, without the docs.
4. **The same thing has the same name everywhere.** The label, the docs, the message, and the activity log use one word for one concept.
5. **Follow Jellyfin.** Use Jellyfin's own markup and theme, so the plugin looks like part of the dashboard.

## Writing

### The Rules

These rules apply to every string a user can see. That includes HTML, JS, C# messages the API returns, and activity log entries.

* **No em dashes or en dashes as punctuation.** Split the sentence, or join it with a comma and "and". An en dash in a numeric range, such as `22–6`, is fine.
* **No semicolons.** Split the sentence.
* **Avoid colons.** Never end a label with one. In a sentence, rewrite it. "How fast files move: downloads and uploads." becomes "Sets how fast files move in each direction."
* **One sentence.** Two only when the second gives the fix or the consequence.
* **End every sentence with a period.** Labels, buttons, titles, tabs, and table cells are not sentences and take no period.
* **No "please", "successfully", "simply", or "just".**
* **No exclamation marks.**
* **No ALL CAPS for emphasis.** Say why it matters instead.
* **No "(s)".** Write the right plural for the count.
* **No contractions.** Write "cannot", not "can't".
* **No "e.g." or "i.e.".** Write "such as".
* **American spelling.** Color, behavior, favorite, center.
* **Use the reader's words.** "User", "group", and "invite", not "account", "template", and "token", unless the page is about the API.

### Casing

| Text | Case | Example |
| :--- | :--- | :--- |
| Tab | Title Case, one word | Groups |
| Section and collapsible title | Title Case | Password Requirements |
| Field label | Sentence case, as Jellyfin's user pages write it | Minimum length |
| Checkbox label | Sentence case, as Jellyfin's user pages write it | Allow media playback |
| Button | Title Case, verb first | Add Rule |
| Dialog title | Title Case | Delete Invite |
| Badge and status chip | Title Case | Locked |
| Activity log name | Title Case | 2 Users Disabled |
| Description, message, placeholder sentence | Sentence case | Only shown here, never to the invitee. |

Title Case capitalizes every word except short articles, conjunctions, and prepositions in the middle, such as "a", "and", "of", "to", and "in". Keep acronyms as written, such as "PIN", "URL", and "Mbps".

The **Groups** tab copies the labels of Jellyfin's own user settings, so a member's settings read the same in both places. Field and checkbox labels across the plugin follow that sentence case.

### Words to Use

Pick the word on the left every time.

| Use | Not | When |
| :--- | :--- | :--- |
| Remove | Delete | Takes a row out of an editor, such as a session cleanup rule or a resource |
| Delete | Remove | Destroys a group, an invite, or a user account, which cannot come back |
| Clear | Empty, Reset | Removes every entry in a list or log |
| Reset | Restore | Puts settings back to their defaults |
| Save | Apply, Update | Writes changes to the config |
| Done | OK, Close | Closes an editor and keeps its changes |
| Cancel | Close, No | Closes without keeping changes |
| Add | New, Create | Puts a new entry into a list on the page |
| New | Add, Create | Starts a new record in a record picker |
| Run Now | Start, Trigger | Starts a scheduled task right away |
| Select | Pick, Choose, Tick, Click | Any instruction to make a choice |
| Turn on, Turn off | Enable, Disable, Tick, Untick | An instruction about a checkbox or toggle |
| Leave blank | Leave empty | An empty field means the default |
| PIN | Pin, code, passcode | The six digit code an invite asks for |
| Server URL | Address, Host | The public address invite links are built from |
| Could not | Failed to, Unable to | An error message |

The control names its own result. Removing a session cleanup rule only changes the form, so it is Remove. Deleting an invite stops its link for good, so it is Delete.

### Labels

* Two to four words. Name the thing the field holds, not the action.
* Put a unit at the end in lowercase parentheses, such as **Per (minutes)** or **Internet streaming bitrate limit (Mbps)**. Use symbols for standard units, such as `Mbps`.
* No colons, periods, or question marks.
* A checkbox label names the feature it turns on, never "Enable X". The exceptions are labels copied from Jellyfin's user pages, and the master switch at the top of a feature, such as **Enable reset code extraction**.

### Descriptions

A `fieldDescription` sits under every field that is not obvious from its label.

* One sentence that says what the setting does, starting with a verb. "Shows the invitee a message under the signup heading."
* Add a second sentence only for the default or a consequence. "Set either to 0 to turn it off."
* Describe the current behavior only. Never "now" or "no longer".
* Never repeat the label.
* Put units, ranges, and what blank means here, never in the label or the placeholder.

| Bad | Good |
| :--- | :--- |
| Public address used to build invite links (e.g. reverse proxy). | Builds invite links from this address, such as a reverse proxy. |
| Whether members may set or change their own password; admins always can. | Sets whether members may set or change their own password. Administrators always can. |
| This setting enables the disabling of inactive users. | Disables members who have not been active for this many days. |

A section intro is one `fieldDescription` right under the section title. It says what the section is for in one sentence.

### Placeholders

A placeholder shows an example of a valid value. It is never the only place an instruction lives, since it disappears once the reader types.

* Show a realistic example with no prefix. `https://jellyfin.example.com`, not `e.g. https://...`.
* Use `example.com` addresses and generic names only.
* For an override that falls back to a default, show the value it falls back to. When that value is not known, write `Default`.
* For a search box, write "Search" and the noun, with no ellipsis. `Search items`.
* Never put a sentence in a placeholder. Move it to the description.

### Buttons

* A verb, or a verb and a noun. **Save**, **Add Rule**, **Create Invite**.
* Name the object when the page has more than one of that action. **Add Schedule** and **Add Rule** sit on the same page.
* Every Save reads **Save**, even when a page has more than one. Never **Save Settings** or another noun.
* An icon only button carries a `title` of one to three words, such as `Duplicate`, `Delete`, or `Show`. Never a sentence.
* A button that runs for a while shows progress in place. See [Progress](#progress).

### Messages

A message reports what happened in one sentence that ends with a period.

| Kind | Pattern | Example |
| :--- | :--- | :--- |
| Saved | `<Thing> saved.` or `Saved.` | Invite settings saved. |
| Done | `<Thing> <past verb>.` | Invite deleted. |
| Copied | `<Thing> copied.` | Link copied. |
| Working | `<Verb>ing…` | Saving… |
| Could not | `Could not <verb> the <thing>.` | Could not save the groups. |
| Could not, with a fix | Add one sentence with the fix | Could not load the groups. Reload the page. |
| Missing input | `Enter <the thing>.` or `Select <the thing>.` | Select a group for this invite. |
| Nothing to do | `No <things> <state>.` | No pending password resets. |

* Use the ellipsis character `…`, never three dots, and only for work in progress.
* When the server gave a reason, show it with `shared.errorText(err, 'Could not create the invite.')`.
* Name the count with a correct plural through `shared.plural`. "3 invites" and "1 invite".
* Never say "error occurred". Say what could not happen.

### Confirm Dialogs

A confirm asks one question and states the consequence.

```js
confirmDialog('Delete this invite? The link stops working.', {
    title: 'Delete Invite',
    confirmText: 'Delete',
    destructive: true
});
```

* The message is a question, then one sentence on the consequence. "Delete the group Family? Its members are unassigned."
* The title is the action in Title Case, such as **Delete Group**. Never leave the default **Confirm**.
* The confirm button repeats the action verb, such as **Delete** or **Save**. Never **OK** or **Yes**.
* Set `destructive: true` for anything that loses data.
* Never "Are you sure you want to". Ask the question directly.
* Never use `window.confirm` or `Dashboard.confirm`.

### Empty States

| Where | Pattern | Example |
| :--- | :--- | :--- |
| Record picker | `No <things> yet. Select New to add one.` | No groups yet. Select New to add one. |
| List under a form | `No <things> yet. Create one above.` | No invites yet. Create one above. |
| Live list | `No <things>.` | No pending password resets. |
| Filtered list | `No <things> match.` | No users match. |
| Feature off | `<Feature> is disabled. Enable it above to <result>.` | Reset code extraction is disabled. Enable it above to list pending codes. |

Use `shared.emptySection(text)` for a region and `shared.emptySection(text, { inline: true })` for a list.

### Activity Log

Use `ActivitySummary` and `ActivityText` from Base.

* The name is short and Title Case. "2 Users Disabled".
* The overview is one sentence with no closing period, as Base writes it. "2 users were disabled in Family (2)".
* Sum a burst or a scheduled run into one entry per outcome. A security event on one account stays its own entry.
* [Activity Log](../docs/ActivityLog.md) lists every entry. Add a new one there in the same commit.
* Use the same verb the UI uses for the action.

### Messages From C#

The API returns text that the page shows as is, so it follows the same rules.

* A full sentence that starts with a capital and ends with a period. "The group disallows all password changes, so it cannot be used for invites."
* Say what is wrong and, when it helps, what to do.
* The signup page shows these messages to anyone with a link, so they never name another user or say more about an account than signup needs.
* Never return an exception message, a stack trace, or a type name.

## Color

### Tokens

Use Base tokens. They follow the dashboard's theme, light themes included. Never write a hex value for something these cover.

| Token | Use |
| :--- | :--- |
| `--base-accent` | Links, focus rings, primary buttons, progress |
| `--base-accent-hover` | Hover on accent elements |
| `--base-fg` | Washes on elements that fade their own text |
| `--base-surface` | Panel and dialog backgrounds |
| `--base-border` | Every border and divider |
| `--base-gap-wide` | Space between components, such as one field row and the next |
| `--base-gap-narrow` | Space inside a component, such as between a label and its field |
| `--base-mono` | Code, ids, paths, and raw errors |
| `--base-success`, `--base-warning`, `--base-error` | Solid status colors |
| `--base-<color>-bg`, `--base-<color>-fg` | Tinted badge and card backgrounds and text, for green, red, orange, purple, blue, and gray |

### Meaning

A color means the same thing on every page and in every plugin.

| Color | Means | States |
| :--- | :--- | :--- |
| Green | Done and healthy | Active |
| Red | Failed, or about to lose data or access | Disabled users, Expired reset codes |
| Orange | Waiting, or needs attention soon | Expired invites, Expiring reset codes |
| Yellow | Drifting toward a rule | Inactive |
| Purple | Held back, or about to change | Locked, Expiring members |
| Blue | Informational | Admins |
| Gray | Off or not applicable | Used up |

* Map a status to a color through `BADGE_STATES`. Never keep a second map in the plugin.
* Used up is gray, since the invite is off rather than failed.
* Use "orange" for the warning color in class names, never "amber", "yellow", or "warning".
* Never use color alone. A badge always has a word, and a dot always has a `title`.

## Page Structure

### Shell

Every config page uses Jellyfin's page shell.

```html
<div id="UserManagementInvitesPage" data-role="page"
     class="page type-interior pluginConfigurationPage withTabs"
     data-require="emby-input,emby-button,emby-select,emby-checkbox"
     data-controller="__plugin/usermanagement_invites.js">
    <div data-role="content">
        <div class="content-primary">
            <form id="UserManagementInvitesForm">
                <div class="verticalSection verticalSection-extrabottompadding">
                    <div class="sectionTitleContainer flex align-items-center">
                        <h1 class="sectionTitle">Invites</h1>
                    </div>
                    <div class="fieldDescription um-intro">Create a shareable signup link.</div>
                </div>
            </form>
        </div>
    </div>
    <link rel="stylesheet" href="configurationpage?name=usermanagement_jpkribs_shared.css">
    <link rel="stylesheet" href="configurationpage?name=usermanagement_shared.css">
</div>
```

* List `data-require` in the order `emby-input,emby-button,emby-select,emby-checkbox`, dropping any the page does not use.
* Use no inline `style` attributes. Add a class to the plugin CSS, or to Base when it is reusable.
* The public signup page is not a dashboard page. It is the content of Base's status card, rendered by `StatusPage.Render` in `InviteController`, and uses the status card classes.

### Tabs

* One registered page per tab, wired with `createConfigPage` and `setTabs`.
* Tab names are one Title Case word.
* Keep the tab list in one place, `getTabs` in `usermanagement_shared.js`, never copied into each page.
* Order tabs from what is used most to what is set once. User Management is **Groups**, **Invites**, **Resets**.

### Sections

| Need | Component |
| :--- | :--- |
| A page with several independent groups of settings | `base-collapsible-section`, one per group |
| A group that is always open | `base-collapsible-section fixed`, whose header is a `div` with no icon, so it matches the collapsible sections around it |
| A heading inside a section | `base-subsection-title` |
| An always open group, such as the four parts of a group | `base-section` with `base-section-title` |
| An ordered setup | `base-collapsible-section numbered`, which Base numbers in order |
| Settings that only matter when a checkbox is on | `base-indent` under the checkbox, shown with `shared.bindReveal` |

* A collapsible header is a `button.base-collapsible-header` holding `base-collapsible-title` and `base-collapsible-icon`. Base replaces the icon with `expand_more`.
* Start collapsibles closed, except the first one on a page someone opens to act on.
* Give every top level group on one page the same header. A chart or a summary takes the `fixed` section, never a bare `base-subsection-title`.
* Hide a whole section while its feature is off or it has nothing to show. Never show an empty section.
* Put anything that depends on a picker under that picker, and redraw it when the pick changes.

### Saving

Pick one model per page and keep it.

| Model | Use it when | Save |
| :--- | :--- | :--- |
| One Save per page | Settings depend on each other, as on **Groups** | One Save Bar at the bottom of the page |
| One Save per section | Sections are independent, as on **Invites** | One Save Bar at the bottom of each section's content |
| Save on change | One switch, as on **Resets** | None. The checkbox saves itself |
| Editor dialog | Each record is edited on its own | **Done** in `openEditorDialog` |

* Every Save is Base's `createSaveBar` with `fill: true`, and its button reads **Save**. Never a hand placed Save button, a `small` Save, or a Save Bar without `fill`.
* The Save Bar is the last element of what it saves. Nothing sits beside it or below it in that section.
* Other actions that belong with a save, such as **Reset to Defaults** or **Test**, go in one `<div class="base-button-row">` directly above the Save Bar. Each is `raised base-button secondary fill`, or `destructive fill` when it deletes or resets data. A select that belongs there stays a normal select in the row. No `small` buttons in that row.
* Wire `setDirty` from the page's change tracking, and have `onSave` return the save promise. A rejected promise shows the failed line.
* The Save Bar shows the unsaved, saving, saved, and failed lines. Never add a separate status span or `base-unsaved` indicator for a save.
* Guard leaving the page by passing `isDirty` to `createPage`.
* Read the latest config with `shared.getConfig` before a save, and change only the fields the page owns.

## Components

Every component below is in Base. Use it as Base ships it. When it does not fit, change Base.

### Find the Component

Before writing markup for a list, a row editor, a chart, or a picker, look for it in Base.

```bash
grep -o "export function create[A-Za-z]*" ~/.nuget/packages/jpkribs.jellyfin.base/<version>/assets/jpkribs_shared.d.ts
```

* The typings in that file document every option of every component. Read the component's block before using it.
* Rows of fields with Add and Remove are a `createRepeater`. A list of checkboxes is a `createCheckboxList`. A used amount out of a limit is a `createUsageMeter`. Parts of a whole are a `createPieChart`.
* A hand built version of any of these is a defect, even when it looks the same.
* When a Base component lacks one thing the page needs, use the component and report the gap to Base. Never fall back to building it by hand.

### Fields

| Need | Use |
| :--- | :--- |
| Text, number, or URL | `inputContainer` with `<label for>`, `<input is="emby-input">`, and a `fieldDescription` |
| Choice from a short list | `selectContainer` with `label.selectLabel` and `<select is="emby-select">` |
| On or off | `checkboxContainer checkboxContainer-withDescription` with `emby-checkbox` and `fieldDescription checkboxFieldDescription` |
| A mode that needs a sentence per option | `createChoiceGroup` with `base-choice` cards |
| Several on or off options in a row | `base-toggle-group` with `base-toggle` pills |
| A few checkboxes with short, distinct labels | `base-check-grid` |
| Many items from a long or searchable list, or items whose names repeat, such as devices | `createCheckboxList`, with `renderRow` giving each row a `label` and a `sublabel` that tells repeats apart |
| Rows of several fields that are added and removed | `createRepeater` |
| Pairs of a name and a value | `createKeyValueEditor` |
| A list of plain text values | `createStringList` or `createTagInput` |
| Typeahead with removable chips | `createChipSelect`, with `single: true` for one choice |
| One user | `createUserSelector` |
| Several users | `createUserMultiSelector` |
| Several fields side by side | `base-field-row` with `base-field` |
| A secret | `bindSecretField`, `readSecretField`, and `clearSecretField` |
| A template with `{name}` tokens | `placeholderHint` and `bindPlaceholders` |
| Code or JSON, read only | `base-code-block` |
| A read only value with a copy button, such as an invite link | `createCopyField` |
| Day and time windows, such as an access schedule | `createAccessSchedule` |
| A setting that inherits until overridden | `createInheritField` |

* Use `emby-select`, never `base-selector-dropdown`, for a field. `base-selector-dropdown` is only for the record picker.
* Every input has a `<label for>`.

### Buttons

| Need | Class |
| :--- | :--- |
| Primary action | `raised base-button primary` |
| Destructive action | `raised base-button destructive` |
| Secondary action | `raised base-button secondary` |
| Actions that go with a Save | `raised base-button secondary fill` in one `base-button-row` above the Save Bar |
| Save | `createSaveBar({ fill: true })`. Never a hand placed button |
| Icon and label in a toolbar | `raised base-button` holding a `material-icons` span and a label |
| Icon only in a row | `base-row-btn`, with `.dirty` for unsaved and `.success` after saving |

* Every button is `<button is="emby-button" type="button">`.
* Icons are `<span class="material-icons" aria-hidden="true">name</span>`.
* Close a dialog with `base-dialog-close` and `&times;`. Never "✕" or another glyph.

| Action | Icon |
| :--- | :--- |
| Add | `add` |
| Edit | `edit` |
| Remove or delete | `delete` |
| Copy | `content_copy` |
| Move up and down | `keyboard_arrow_up`, `keyboard_arrow_down` |
| Drag | `drag_indicator` |
| Expand | `expand_more` |
| Retry | `refresh` |
| Open | `chevron_right` |
| Import and export | `upload`, `download` |
| Show | `visibility` |

### Records

| Need | Use |
| :--- | :--- |
| One record at a time from a list | `base-selector` with New, Rename, and Remove |
| Records in priority order | `createSortableCardList` |
| A short list in order, moved with up and down buttons | `createOrderedList` |
| A plain list of records | `base-card-list` with `base-card-item-*` |
| Rows that each open, or hold one control such as a copy field, like the invites and the pending resets | `createRecordList`, with `aside` for the control |
| Records with several fields, added and removed in place, like session cleanup rules | `createRepeater` with `layout: 'card'` |
| A setting that applies only when overridden, like every group setting | `createInheritField` with `toggle: 'checkbox'` |
| Edit one record in a dialog | `openEditorDialog` |
| A large searchable list | `createPaginatedTable` |
| Edit table rows in place | `base-bulk-edit-bar` and `base-edit-row` |
| Uploaded files | `base-asset-list` |
| A thumbnail in a table row | A column `image(item)` loader with `imageShape`, one shape for each column |
| A mark on a table row, such as picked | The table's `rowClass(item, index)` |

* A record picker's actions read **New**, **Rename**, **Delete**, then any action for the picked record, such as **Default**.
* Use `createPaginatedTable` for any list that can grow past one screen. Rows open with `actions.onRowClick`.

### Status

| Need | Use |
| :--- | :--- |
| The state of one item | `shared.statusBadge(text, state)` |
| A label on an item | `shared.badge(color, label)` |
| Counts at the top of a page | `shared.renderCards` with `base-cards`, and `compact` for many counts on a phone |
| Label and value pairs | `base-record-card` |
| A notice in the page | `base-info`, `base-success`, `base-warn`, `base-error`, `base-neutral` |
| Raw failure text | `base-error-message` |
| The result of an action | `shared.setStatus` |
| A row of reset actions | `base-reset-group` with `base-reset-row` |

* Never use `Dashboard.alert` for a result. It blocks the page. Use `setStatus`.
* A notice holds one sentence. Use a callout for something the reader must know before acting, not for a result.

### Charts

| Need | Use |
| :--- | :--- |
| Parts of a whole | `createPieChart`, with `donut` and the total in `centerText` |
| How much of a limit is used | `createUsageMeter` |
| Values compared across names | `createBarChart` |
| A value over time | `createLineChart` or `createSparkline` |
| One number with a label | `createStatCard` |

* Pick the chart by the question it answers. A single used of total bar is a Usage Meter, never a Bar Chart.
* Name each slice with a glossary term in Title Case, such as **Locked**.
* Take colors from Base's series colors. Set a color only when it carries a [meaning](#meaning).
* Give every chart a `label`, and an `emptyText` sentence for when it has nothing to draw.
* Scope a chart to what the page shows. A chart beside the group select counts that group only.

### Progress

* A task started from a button shows progress in that button with `shared.pollTaskProgress`.
* Progress inside a row uses `base-progress` and `base-progress-bar`.
* Never block the page with `Dashboard.showLoadingMsg`.

### Dialogs

| Need | Use |
| :--- | :--- |
| A yes or no question | `confirmDialog` |
| A few values | `promptDialog` |
| A record editor | `openEditorDialog`, with `wide: true` for a large one |
| A custom dialog | `openDialog` |
| A dialog already in the page markup | `bindDialog` |

* Build dialogs in JS with `openDialog` and its wrappers. Use `bindDialog` only when the markup is large and static.
* A dialog title is Title Case and names what the dialog does.
* Buttons read, from left to right, the destructive action, then **Cancel**, then the primary action.

## Layout

* Use Base's breakpoints, 768 px for tablet and 600 px for phone. Use 700 px only where Base does, for dialogs.
* Test every page at 375 px wide. Nothing scrolls sideways except a table.
* Look at every changed page on a test server, or in a browser harness as [testing.md](testing.md#pages-in-a-browser) describes, before calling it done. Reload the page after installing a build, since the dashboard caches scripts.
* Spacing comes from `--base-gap-wide` and `--base-gap-narrow`. Never add margins between fields.
* Never set a width in pixels on a field. Let it fill its container.

## Accessibility

* Every input has a label, and every icon only button has a `title` and an `aria-label`.
* Every icon span has `aria-hidden="true"`.
* Rows that open on click also open with Enter and Space. Base's table and card list do this already.
* Never rely on color alone.

## CSS in This Plugin

* Plugin classes use the `um-` prefix in kebab case. `base-` is reserved for Base.
* Keep plugin CSS for layout that only this plugin has. A component another plugin could use goes to Base.
* Never override a `base-` class. Change Base instead.
* When Base has a defect that breaks a page, report it and add one marked stopgap rule under a comment that names the defect. Delete the rule in the commit that moves to the Base version with the fix.
* Size a column in `rem` or `fr`. An `em` width changes with the font size of each row, so a smaller heading row falls out of line with its columns.
* Never define a color token. Use Base's.

## Hand Built Pieces

Nothing on the dashboard tabs is built by hand. The plugin CSS holds only spacing on Base tokens: `um-intro` under a page intro.

The signup page is the content of Base's status card, so it uses the card's own classes. Two rules in its `<style>` block still stand in for classes the card has, and wait on the owner's go ahead to change the signup page.

| Hand built today | Base class that replaces it |
| :--- | :--- |
| `.base-note.um-error`, the red line under the signup form when a signup is refused | `base-status-error` |
| `#resources`, which stacks the resource links full width | `base-btn-stack`, with `base-btn base-btn-secondary` on each resource link |

## Checklist

Run every new or changed string through this list.

* [ ] No em dashes, en dashes as punctuation, or semicolons.
* [ ] No colon at the end of a label, and none in a sentence unless it introduces a list.
* [ ] One sentence, or two when the second is the fix or the consequence.
* [ ] Sentences end with a period. Labels, buttons, and titles do not.
* [ ] Title Case for labels, buttons, titles, tabs, and badges. Sentence case for sentences.
* [ ] No "please", "successfully", exclamation marks, ALL CAPS, "(s)", or contractions.
* [ ] The word matches [Words to Use](#words-to-use).
* [ ] Base's typings were searched before any markup was built by hand.
* [ ] The component comes from Base, unchanged.
* [ ] Colors come from Base tokens and mean what [Meaning](#meaning) says.
* [ ] The docs use the same label.
