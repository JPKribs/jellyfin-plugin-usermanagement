# Translations

How this plugin shows its text in the viewer's language. Base builds the strings in and checks them on every build. The full reference is Base's [Translations](https://jpkribs.github.io/jellyfin-plugin-base/Translations/) guide.

## Where the Strings Live

| Strings | Where |
| :--- | :--- |
| Jellyfin's own | `<JellyfinString Include="Key" />` items in `Jellyfin.Plugin.UserManagement/Jellyfin.Plugin.UserManagement.csproj` |
| This plugin's own | `Jellyfin.Plugin.UserManagement/Localization/en-us.json`, one file per language |
| Words kept as written | `Jellyfin.Plugin.UserManagement/Localization/casing.json` |
| The endpoint pages load them from | `GET /UserManagement/Translations`, which answers with `BaseTranslations.Serve` |
| The handle the dashboard tabs read | `t` in `Web/usermanagement_shared.js`, loaded once before a tab's `bind` and `load` run |

## Rules

* Every word a person reads comes from a key. No hard coded English in a page, a script, or a message the server shows.
* Use a Jellyfin string whenever Jellyfin has the words, in any case. Search the web client's `src/strings/en-us.json` and the server's `Localization/Core/en-US.json` before adding a key.
* Add a local key only when Jellyfin has no string for it. Put it in `en-us.json` with its US English.
* One string, one key. The build fails on a key said twice or English said twice.
* Keep placeholders as `{0}`, `{1}`. Never build a sentence from pieces, since word order differs by language.
* A key is PascalCase and says what the text is, such as `HeaderQueue`, `LabelApiKey`, `ButtonRetry`, `ToastSaved`, `ErrorServerUnreachable`.
* Log lines and exception text for admins in the log stay in English and need no key.
* A Jellyfin string keeps Jellyfin's wording and casing.
* Base's components translate their own default text, such as a dialog's Cancel and the Save Bar's lines. Load the strings before building a component, which `createPage` does. Pass a text option only when it says something different from Base's default, such as `savedText: t.tr('ToastGroupsSaved')`.

## Add a String

1. Search Jellyfin's strings for the words.
2. Found: add `<JellyfinString Include="Key" />` to the csproj.
3. Not found: add `"Key": "English"` to `Localization/en-us.json`.
4. Use it.
    * Markup: `data-str="Key"`, `data-str-title`, `data-str-label`, or `data-str-placeholder`, with the English as the element's own text.
    * Script: `t.tr('Key')` or `t.format('Key', value)` on the `t` that `usermanagement_shared.js` exports, which `loadTranslations('UserManagement/Translations')` fills. Write the key as a literal in the call, never in a variable, so the key test finds it.
    * Signup page: it imports Base's bundle itself and loads `/UserManagement/Translations` with no signed in user. Script text uses `t.tr('Key', 'English')`, so the page still reads when the strings fail to load.
    * C#: `BaseTranslations.Get(TranslationKeys.Key, language)` or `BaseTranslations.Format(...)`. A route reads the page's language with `BaseTranslations.RequestLanguage(Request)`, which takes the `language` query, then the `X-Jellyfin-Plugin-Language` header that `Shared.apiRequest` sends, then `Accept-Language`. Text for everyone, such as an activity log entry, a task name, or a password change message, uses `ServerLanguage.Current`.
    * Activity summary: an `ActivityOutcome` built with `nameOne` and `nameMany` keys, which hold no count, and an overview that takes the language. `UserManagementActivity` gives its summary `ServerLanguage.Current`.
5. Build. Fix every `JPKT` error and warning.
6. Run `dotnet test`. `TranslationsTests` fails on a key a page uses that has no string.

## Casing

Every string is in sentence case, mirroring Jellyfin, such as "Allow media playback".

* **Local strings.** Write them in sentence case in `en-us.json`. Capitalize the first word and proper nouns only.
* **Words kept as written.** Add a product name, service, or acronym this plugin uses to `Localization/casing.json`, a JSON array such as `["PIN", "Quick Connect", "DLNA"]`. Leave out Base's defaults, such as Jellyfin, URL, and HTTPS. The build fails on an empty entry or a word listed twice.
* **Jellyfin strings.** Keep Jellyfin's casing. Add `Case="sentence"` or `Case="title"` to a `JellyfinString` only when one reads wrong beside its neighbors.
* **Text built at runtime.** Convert it with `ToSentenceCase` or `ToTitleCase` in C#, and `sentenceCase` or `titleCase` on a page, which keep the words in the casing list.

## Add a Language

Add `Localization/<code>.json`, such as `de.json` or `pt-br.json`, with only keys `en-us.json` has. A missing key falls back to the language's base and then to US English. Jellyfin's strings come in every language Jellyfin has, with no file.

## Checklist

* [ ] No English in a page, script, or server message outside `Localization/en-us.json`.
* [ ] Jellyfin's string used wherever it has the words.
* [ ] The build has no `JPKT` error or warning.
* [ ] The pages show the strings, checked once in another language such as `de`.
