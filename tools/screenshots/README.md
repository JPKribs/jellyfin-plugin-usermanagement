# Screenshot Kit

Takes every screenshot in `docs/screenshots/` from the plugin's own pages, drawn with Jellyfin's real stylesheets and generic sample data. Only Activity Log needs a server, and `activity.py` starts a throwaway one in Docker.

| File | Does |
| :--- | :--- |
| `server.py` | Serves the plugin pages, Base from the NuGet cache, and jellyfin-web, the way Jellyfin's `configurationpage` route does. It serves the signup page at `/invite/<token>` through Base's status template, the way `InviteController` does, and the plugin's strings at `UserManagement/Translations` in the language a page asks for |
| `groups.html` | Mounts the Groups tab with a stand-in `ApiClient` holding the sample groups, users, libraries, and devices |
| `invites.html` | Mounts the Invites tab with a stand-in `ApiClient` holding the sample invites |
| `resets.html` | Mounts the Resets tab with a stand-in `ApiClient` holding sample reset codes |
| `activity.py` | Starts Jellyfin in Docker with the plugin from `dist/`, makes invites, redeems one, enters a wrong PIN, and takes Activity Log from Jellyfin's own page |
| `upgrade.js` | Adds the classes and markup Jellyfin's `emby-*` elements add on upgrade, so the real CSS applies |
| `shoot.mjs` | Opens each subject, sets it up, and writes every PNG to `docs/screenshots/` |

## Requirements

* Python 3.
* Node.js and playwright-core. Set `PLAYWRIGHT_CORE` to its `index.mjs` when it is not installed where Node finds it.
* Chromium. Set `CHROMIUM` to its binary when it is not Playwright's `chromium-1148` in `~/Library/Caches/ms-playwright`.
* jellyfin-web. Set `JELLYFIN_WEB` when it is not the macOS app's `/Applications/Jellyfin.app/Contents/Resources/jellyfin-web`.
* Base in the NuGet cache at the version the plugin's csproj references. `dotnet restore` on the plugin puts it there.
* For Activity Log only, Docker and a plugin ZIP in `dist/` from `./build.sh`.

## Running

From this folder, start the server and take every shot.

```bash
python3 -I server.py &
PLAYWRIGHT_CORE=/path/to/node_modules/playwright-core/index.mjs node shoot.mjs
```

Activity Log is left out of that run. Take it with its own script, which removes the container when done.

```bash
PLAYWRIGHT_CORE=/path/to/node_modules/playwright-core/index.mjs python3 -I activity.py
```

Name files to retake only those.

```bash
node shoot.mjs "Invite List" "Invite List Phone"
```

`server.py 8843` and `PORT=8843` run both on another port. Look at every PNG the kit writes before committing it. A new subject is one more entry in the `shots` list of `shoot.mjs`, and new sample data goes in `groups.html`, `invites.html` or `resets.html`.
