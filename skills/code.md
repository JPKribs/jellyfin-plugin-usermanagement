# Code

How to write, comment, and test code in this plugin. It covers C#, JavaScript, HTML, CSS, shell, and YAML.

## Layout

| Path | Holds | Namespace |
| :--- | :--- | :--- |
| `Plugin.cs` | The plugin class and its config pages. | root |
| `PluginServiceRegistrator.cs` | DI registration. | root |
| `Api/` | The dashboard API, its strings route, and the anonymous invite routes. | `.Api` |
| `Configuration/` | `PluginConfiguration.cs` only, with no web files. | `.Configuration` |
| `Web/` | Every file served to a browser, the config pages and the public invite page. Embedded as `Jellyfin.Plugin.UserManagement.Web.<file>`. | n/a (embedded) |
| `Assets/` | `Logo.png`, bundled into the release zip. | n/a |
| `Localization/` | The plugin's own strings, one file per language. See [translations.md](translations.md). | n/a (embedded) |
| `Models/` | Data classes, records, and enums. | `.Models` |
| `Services/` | Groups, invites, passwords, sessions, reset codes, and the activity log. | `.Services` |
| `Tasks/` | `IScheduledTask` implementations. | `.Tasks` |
| `Utilities/` | Static helpers and extension methods. | `.Utilities` |

At the repository root, `Jellyfin.Plugin.UserManagement.Tests/` holds the tests, `docs/` the MkDocs site, `skills/` the guides, and `tools/` development tools that never ship.

## Base First

[JPKribs.Jellyfin.Base](https://github.com/JPKribs/jellyfin-plugin-base) holds everything two plugins could share. Its README lists every component and helper. Read it before you write anything new.

### Use What Base Has

* Use the Base type or component when one fits. Never write a local version, a wrapper that renames it, or a CSS override of a `base-` class.
* When Base almost fits, change Base. Never patch around it locally.
* Delete a local copy as soon as Base offers the same thing. Do it in the commit that moves to that Base version.
* Never shadow a Base name. A local `FormatUtilities` beside Base's `FormatUtilities` is ambiguous.
* Read Base from the package this plugin builds against. The C# source is in `~/.nuget/packages/jpkribs.jellyfin.base/<version>/src/`, and the JS typings are in `assets/jpkribs_shared.d.ts` beside it.
* Treat the local Base checkout as read only. Base has its own owner and its own release, so report a gap and wait for the version that closes it.
* When Base differs from the local code it replaces, keep the local behavior in a small step around the Base call, and cover it with a test.

| Need | Use from Base |
| :--- | :--- |
| Plugin class and config lock | `PluginBase<TPlugin, TConfiguration>` |
| Scheduled task | `PluginScheduledTask` |
| Outbound HTTP | `BaseHttp` and `HttpResult` |
| HTTP handlers | `BaseRetryHandler` with `BaseRetryOptions`, and `RedirectRefusingHandler` |
| Addresses another server or a user supplied | `SsrfGuard`, with `requireHttpsForPublic: true` |
| Retry and backoff | `RetryPolicy`, `BackoffPolicy`, `CircuitBreaker` |
| Parallel work | `ConcurrentTaskRunner` |
| Secrets | `SecretProtector`, `AddPluginSecretProtector` |
| Comparing a presented secret | `SecretCompare.FixedTimeEquals` |
| Writing a file whole or not at all | `AtomicFile`, with `ownerOnly: true` for a key or secret |
| Paths inside a folder | `PathContainment` |
| Streams | `StreamUtilities.CopyWithIdleTimeoutAsync` and `CopyThrottledAsync`, `SpeedBucket`, `ThrottledReadStream`, `CappedReadStream`, `ProgressReportingStream`, `StreamLimits` |
| Hashes and sizes | `HashUtilities`, `ByteSize` |
| The clock | `TimeProvider`, and `UtcTime` for stored times |
| Activity log | `ActivityLogger`, `ActivitySummary`, `ActivityText` |
| Paged lists | `PagedResult<T>`, `PagedQuery` |
| SQLite versioning | `DatabaseSchema` |
| JSON state file | `JsonFileStore<T>` |
| Formatting and names | `FormatUtilities`, `ItemTitles`, `RateUtilities`, `FileNameSanitizer` |
| Config page lifecycle | `createConfigPage`, through `createPage` in `usermanagement_shared.js` |
| Page helpers | `createShared` |
| Tables | `createPaginatedTable` |
| Lists and row editors | `createCheckboxList`, `createRepeater`, `createKeyValueEditor`, `createTagInput`, `createOrderedList`, `createSortableCardList` |
| Users | `createUserSelector`, `createUserMultiSelector` |
| Access schedules | `createAccessSchedule` |
| A value to copy, in a form or inline in a row | `createCopyField`, with `inline: true` inside a row |
| The public signup page shell | `StatusPage.Render`, `PublicPage.ApplyHardeningHeaders`, `PublicPage.Favicon` |
| Charts | `createPieChart`, `createUsageMeter`, `createBarChart` |
| Dialogs | `openDialog`, `confirmDialog`, `promptDialog`, `openEditorDialog`, `bindDialog` |
| Count cards and badges | `shared.renderCards`, `shared.statusBadge`, `shared.badge` |
| Saving | `createSaveBar({ fill: true })`, with `setDirty` from `shared.createChangeTracker` |

### Move Code Into Base

Code belongs in Base when another plugin could use it unchanged. A helper with no User Management types in its signature is a candidate.

1. Add it to `../Shared/jellyfin-plugin-base/src/`, one file per component, following Base's [code guide](https://github.com/JPKribs/jellyfin-plugin-base/blob/main/skills/code.md).
2. Add tests in `JPKribs.Jellyfin.Base.Tests/`, and JS tests in `JPKribs.Jellyfin.Base.Tests/js/`.
3. Document it in Base's docs site inventory.
4. Pack it locally with `scripts/pack.sh` and build this plugin against it with `-p:RestoreAdditionalProjectSources=<base>/nupkg`.
5. Commit and push to Base, then run Base's **Release** workflow.
6. Raise the `JPKribs.Jellyfin.Base` version in this plugin's csproj, delete the local copy, and add a `developer` changelog line naming the Base version.

Never commit this plugin against an unreleased Base package.

## Comments

### The Rules

* Every class, method, function, and component gets a header comment.
* The header is the name, then a summary of one or two sentences, leaning to one.
* List inputs and outputs when there are any. One line each, as `name [type]: What or why`.
* Write no inline comments, with one exception. A branch whose reason is not obvious gets one short line above it.
* A branch comment says why, not what. Keep it under 15 words.
* Never comment what the code already says. `// Check if values differ` is noise.
* Never leave commented out code, TODO, FIXME, or HACK. Open an issue instead.
* Never describe history. "Used to", "now", and "was changed to" belong in commit messages.
* Use no em dashes or semicolons in comments.

```text
<Name>
<What it does, in one sentence.>

Inputs
name [type]: What it is or why it is needed.

Output
[type]: What comes back.
```

Leave out **Inputs** when there are none, and **Output** when nothing comes back.

### C#

C# uses XML docs so the compiler and IntelliSense read them. The member name is the title. The type comes from the signature, so a `param` gives only the what or why.

```csharp
/// <summary>
/// Reads a client's address as the key the per address limit counts by.
/// </summary>
/// <param name="address">The address the request came from, or null when Jellyfin does not know it.</param>
/// <returns>The key, or null when there is no address.</returns>
public static string? Key(IPAddress? address)
{
    if (address is null)
    {
        return null;
    }

    if (address.IsIPv4MappedToIPv6)
    {
        address = address.MapToIPv4();
    }

    // One home network holds a whole IPv6 /64, so the block is counted as one client.
    if (address.AddressFamily != AddressFamily.InterNetworkV6)
    {
        return address.ToString();
    }

    var bytes = address.GetAddressBytes();
    for (var i = 8; i < bytes.Length; i++)
    {
        bytes[i] = 0;
    }

    return new IPAddress(bytes).ToString() + "/64";
}
```

* Every type and every member that is not private gets `<summary>`, every `<param>`, and `<returns>` when it returns a value.
* Private members get a `<summary>` when the name does not say enough.
* Use `<inheritdoc/>` on an interface or override member.
* Write `true` and `false` as `<c>true</c>` and `<c>false</c>`.
* A constructor summary is "Initializes a new instance of the <see cref="Name"/> class."
* Skip tautologies. `<param name="item">The item.</param>` says nothing, so write what the item is for.
* Put one type in each file, named after the type. A small enum used by one class may share its file.
* Match the namespace to the folder.

### JavaScript

```js
// createPage
// Wires a tab page's lifecycle through Base's createConfigPage, with the plugin's id, API prefix, and tab bar filled in.
//
// Inputs
// view [HTMLElement]: The page the dashboard passes to the module.
// tabIndex [number]: The page's place in the tab bar.
// opts [object]: The createConfigPage options the page adds, such as bind, load, and isDirty.
//
// Output
// [object]: The page handle, whose shared is the helper bag.
export function createPage(view, tabIndex, opts) {
```

* Comment every top level function, every function inside a page's default export, and every exported function.
* Do not comment the methods inside a returned handle. List them in the Output line instead.
* Open each file with a header naming the file and saying what it holds in one sentence. Use no `// ====` banners.
* Only `usermanagement_shared.js` imports Base, by the relative path `./configurationpage?name=usermanagement_jpkribs_shared.js`. It exports each Base builder a page uses and the strings handle `t`, and every tab imports from `usermanagement_shared.js`. The signup page is not a tab, so it imports Base's bundle itself for its strings.

### HTML

```html
<!-- Manage Access -->
<!-- Sets the public Server URL for links and the per invite rate limit. -->
<div class="base-collapsible-section">
```

* One comment line naming each section or dialog where it starts, and an optional second line saying what it is for.
* Use no inline `style` attributes.

### CSS

```css
/* um-intro */
/* The one paragraph under a page title, with room before the first component. */
/* Example: <div class="fieldDescription um-intro">Create a shareable signup link.</div> */
.um-intro { ... }
```

* Three lines above each component, the name, what it looks like, and one example.
* Prefix every class with `um-` in kebab case. `base-` is reserved for Base.
* Use Base tokens such as `--base-border` and `--base-fg` for colors. Never write a hex value for something Base already colors.

### Shell and YAML

* A shell script opens with a comment saying what it does and how to run it.
* A YAML file opens with a comment saying what it is for. Comment a step only when its reason is not obvious.

## C# Conventions

* Target `net10.0` with `Nullable` on, `TreatWarningsAsErrors` on, and `AnalysisMode` set to `AllEnabledByDefault`.
* Keep `jellyfin.ruleset` identical across plugins. Raise a rule change in every plugin at once.
* Suppress a rule only at the narrowest scope, with a reason. `#pragma warning disable CA2100 // SQL is internal and parameterized.`
* Use file scoped namespaces, `sealed` classes, and records for data.
* Name private fields `_camelCase`.
* End every async method name in `Async`, pass the `CancellationToken` through, and use `ConfigureAwait(false)` on every await.
* Guard public arguments with `ArgumentNullException.ThrowIfNull`.
* Log through `ILogger<T>` with static, named templates. Pass untrusted text through `FormatUtilities.SanitizeForLog`.
* Catch a specific exception, or filter with `when`. Rethrow `OperationCanceledException` when the token was cancelled.
* Read the clock once in a method and reuse the value. Two reads can straddle a second and disagree.
* Take a `TimeProvider` wherever time decides an outcome, so a test can move it.
* Register services in `PluginServiceRegistrator`.
* Change the plugin configuration only through `Plugin.Instance` and its config lock, never by writing the XML.

## JavaScript Conventions

* Each config page is an ES module whose default export calls `createPage`, which wraps Base's `createConfigPage` and holds the tab list.
* Keep one handle for each Base component a page draws. Call `destroy` before drawing it again, and `setValue` when only its numbers change.
* Drop an answer that arrives after the page moved on. Remember the picked key before the request and compare it when the answer lands.
* Use `const` and `let`, never `var`. Arrow functions are fine for callbacks.
* Escape every value placed in HTML with `escapeHtml`.
* Report results with `shared.setStatus`, and errors with `shared.errorText`.
* Register every poller and observer with the page handle so `viewhide` stops it.

## Security

* Store an invite PIN only as its salted hash in `PinHash`. Never store, log, or return the PIN itself.
* Compare a token or other secret a caller presents in constant time, with `SecretCompare.FixedTimeEquals` from Base or `CryptographicOperations.FixedTimeEquals`, never `==`.
* Make tokens from the system's cryptographic generator, such as `SecretCompare.NewTokenHex` from Base.
* Keep anonymous routes in `InviteController` only. Mark each with `[AllowAnonymous]`, count it against `InviteAttemptLimiter`, and send `PublicPage.ApplyHardeningHeaders`. The one exception is `UserManagement/Translations`, which serves only the plugin's strings.
* Guard every dashboard route with `[Authorize(Policy = "RequiresElevation")]`, as `UserManagementController` does. Its `Translations` route alone allows anonymous requests, since the signup page has no signed in user.
* Read a client's address with `ClientAddress.Key`, which counts an IPv6 /64 as one client.
* Never let group rules, expiry, inactivity, or session cleanup touch an administrator. Check with `AdminExemption.IsExempt`.
* Accept only absolute http and https URLs for invite resources, so a link on the signup page can never carry another scheme.
* Pass every value from a request or a file through `FormatUtilities.SanitizeForLog` before logging it.
* Escape every value placed in HTML with `escapeHtml`.
* Never put a secret or a user's data in a URL, except the invite token an invite link must carry.

## Tests

Every source file has a test file. See [testing.md](testing.md) for each layer, how to write tests, and the shared fakes.

## Checklist

* [ ] Base's source and typings were searched, nothing here duplicates Base, and anything reusable went to Base first.
* [ ] Every class, method, function, and component has a header.
* [ ] Inline comments appear only above branches, and say why.
* [ ] Every changed source file has tests that pass, per [testing.md](testing.md).
* [ ] The [security](#security) rules hold.
* [ ] `dotnet build` shows no warnings.
