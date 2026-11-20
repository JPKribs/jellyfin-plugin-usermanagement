# Translations

How this plugin shows its text in the viewer's language. Base builds the strings in and checks them on every build. The full reference is Base's [Translations](https://jpkribs.github.io/jellyfin-plugin-base/Translations/) guide.

## Where the Strings Live

| Strings | Where |
| :--- | :--- |
| Jellyfin's own | `<JellyfinString Include="Key" />` items in `Jellyfin.Plugin.UserManagement/Jellyfin.Plugin.UserManagement.csproj` |
| This plugin's own | `Jellyfin.Plugin.UserManagement/Localization/en-us.json`, one file per language |
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
* A Jellyfin string keeps Jellyfin's wording and casing, so a label it supplies can read in sentence case.
* Give a Base component its text through its options, such as `saveBarOptions` for the Save Bar and `cancelText` for a dialog. Its own defaults are US English.

## Add a String

1. Search Jellyfin's strings for the words.
2. Found: add `<JellyfinString Include="Key" />` to the csproj.
3. Not found: add `"Key": "English"` to `Localization/en-us.json`.
4. Use it.
    * Markup: `data-str="Key"`, `data-str-title`, `data-str-label`, or `data-str-placeholder`, with the English as the element's own text.
    * Script: `t.tr('Key')` or `t.format('Key', value)` on the `t` that `usermanagement_shared.js` exports, which `loadTranslations('UserManagement/Translations')` fills. Write the key as a literal in the call, never in a variable, so the key test finds it.
    * Signup page: it imports Base's bundle itself and loads `/UserManagement/Translations` with no signed in user. Script text uses `t.tr('Key', 'English')`, so the page still reads when the strings fail to load.
    * C#: `BaseTranslations.Get(TranslationKeys.Key, language)` or `BaseTranslations.Format(...)`. A dashboard route takes `language` from the query, which a page adds with `withLanguage(path)`. Text for everyone, such as an activity log entry, a task name, or a password change message, uses `ServerLanguage.Current`.
5. Build. Fix every `JPKT` error and warning.
6. Run `dotnet test`. `TranslationsTests` fails on a key a page uses that has no string.

## Add a Language

Add `Localization/<code>.json`, such as `de.json` or `pt-br.json`, with only keys `en-us.json` has. A missing key falls back to the language's base and then to US English. Jellyfin's strings come in every language Jellyfin has, with no file.

## Checklist

* [ ] No English in a page, script, or server message outside `Localization/en-us.json`.
* [ ] Jellyfin's string used wherever it has the words.
* [ ] The build has no `JPKT` error or warning.
* [ ] The pages show the strings, checked once in another language such as `de`.
