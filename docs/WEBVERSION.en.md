🇩🇪 [Deutsch](WEBVERSION.md) | 🇬🇧 **English**

> This is a translation of the German document. If the two differ, the German version applies.

# Web version without exe

The web version shows the same overlays as the exe but needs **nothing to install or run**. It runs entirely in OBS: the control page is an OBS dock, the overlays are normal browser sources. Its interface (control page, start page) is German; the overlays can show English names with `lang=en`.

**The difference from the exe:** there is **no automatic detection**. A browser cannot read the game memory. All enemies are checked off manually in the dock.

| | exe | Web version |
|---|---|---|
| Detecting kills | automatic (72 of 84) | manual |
| Time in the overlay | in-game time | run clock to start/pause |
| Current area | automatic by position | manual |
| Installation | start the exe | none, just URLs in OBS |
| State saved | `%APPDATA%\DSRTracker\state.json` | in the OBS browser storage, save/load as a file |

## Hosted

The current version is on GitHub Pages: **https://spidy666.github.io/DSRtracker/**

The start page there explains the setup in OBS in four steps. It is built automatically from the source code on every change in `main` (workflow `pages.yml`).

If you only want to check things off or follow a run, without OBS: download [DSR-Liste.html](https://spidy666.github.io/DSRtracker/DSR-Liste.html). This single file needs neither a web server nor internet and opens with a double click in the browser.

## Self-hosting

The web version consists only of **static files**: HTML, JavaScript, CSS, fonts and the enemy list as JSON. There is no server code, no database and no login. Any web server that can serve files is enough.

### 1. Build the files

Requires Python 3 (no further packages). In the project folder:

```
python tools/build_pages.py C:\path\to\target\folder
```

Without a target the result goes to `_site\`. The script takes the pages from `web\` unchanged and only adjusts the paths. If an adjustment no longer fits, it aborts with a message instead of producing half-working files.

### 2. Copy to the web server

Copy the contents of the target folder into a directory of the web server. All paths are relative, so a subfolder like `https://example.org/dsr/` works just like the root.

Examples:

**nginx**
```nginx
location /dsr/ {
    alias /var/www/dsr/;
    add_header Cache-Control "no-cache";
}
```

**Apache** (`.htaccess` in the folder, `mod_headers` enabled)
```apache
Header set Cache-Control "no-cache"
```

**Caddy**
```
example.org {
    handle_path /dsr/* {
        root * /var/www/dsr
        file_server
        header Cache-Control "no-cache"
    }
}
```

**IIS:** create the folder as a website or virtual directory. If fonts don't load: add the MIME type `.woff2` → `font/woff2`.

**Locally, just to try it out:**
```
cd _site
python -m http.server 8080
```
Then open `http://localhost:8080/`.

`Cache-Control: no-cache` isn't required, but makes OBS load the new version immediately after an update.

### 3. Set up in OBS

As described on the start page, just with your address:

1. **Docks** → **Custom Browser Docks** → URL `https://your-address/control.html`
2. In the dock under "OBS-Overlay" choose the view and click **"URL kopieren"** (copy URL)
3. In the scene **Add source → Browser** → paste the copied URL

## Things to watch out for

- **Everything from the same address.** Dock and overlays communicate via the OBS browser storage, which is separate per address (domain + port). If the dock comes from `example.org` and the overlay from `github.io`, they don't see each other.
- **Moving = new state.** If you change the address, the state starts at zero. Save it as a file in the dock under "Stand sichern" (save state) beforehand and load it at the new address.
- **Don't open as a file.** `file:///…` doesn't work reliably in OBS. A web server is needed, if necessary Python's local one.
- **HTTPS recommended** for a public address. In a home network HTTP is fine too.
- **One PC.** The state lives in OBS on the streaming PC. Remote control by a mod from another PC is planned but not included yet.

## Security

- The pages send **nothing** to the internet. Only what is on your server is loaded (pages, fonts, `dsr_enemies.json`).
- Data is stored only in the browser storage of OBS or the browser. When a backup file is loaded, its content is validated and only known fields are taken over.
- Recommended headers on your own server (the same as the exe uses):

```
Content-Security-Policy: default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; font-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'self'; form-action 'self'; base-uri 'none'
X-Content-Type-Options: nosniff
Referrer-Policy: no-referrer
```

## Updating

Get the new version of the repo, run `python tools/build_pages.py <target folder>` again and replace the files on the server. The state in OBS is kept as long as the address stays the same.

## How it works technically

The web version uses **the same pages** as the exe (`web/control.html`, `web/overlay.html`), so new overlay features automatically arrive here too. Two small scripts complement them:

- [`pages/local-backend.js`](../pages/local-backend.js) takes over the role of the exe in the browser. It answers `/events` and `/api/…` locally, keeps the state in `localStorage` and syncs dock and overlays (storage event, BroadcastChannel and, as a safety net, every 0.7 seconds).
- [`pages/control-web.js`](../pages/control-web.js) adapts the control page: run clock, save/load, exe-specific parts hidden.

[`tools/build_pages.py`](../tools/build_pages.py) puts both together. Details are in [CODE.en.md, section 15](CODE.en.md#15-web-version-without-exe).
