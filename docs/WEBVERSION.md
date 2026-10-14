# Web-Version ohne exe

Die Web-Version zeigt dieselben Overlays wie die exe, braucht aber **nichts zum Installieren oder Ausführen**. Sie läuft komplett in OBS: Die Steuerseite ist ein OBS-Dock, die Overlays sind normale Browserquellen.

**Der Unterschied zur exe:** Es gibt **keine automatische Erkennung**. Ein Browser kann den Spielspeicher nicht lesen. Alle Gegner werden im Dock von Hand abgehakt.

| | exe | Web-Version |
|---|---|---|
| Kills erkennen | automatisch (72 von 84) | von Hand |
| Zeit im Overlay | Spielzeit (In-Game-Time) | Run-Uhr zum Starten/Pausieren |
| Aktuelles Gebiet | automatisch nach Position | von Hand |
| Installation | exe starten | nichts, nur URLs in OBS |
| Stand gespeichert | `%APPDATA%\DSRTracker\state.json` | im Browser-Speicher von OBS, Sichern/Laden als Datei |

## Fertig gehostet

Die aktuelle Fassung liegt auf GitHub Pages: **https://spidy666.github.io/DSRtracker/**

Die Startseite dort erklärt die Einrichtung in OBS in vier Schritten. Sie wird bei jeder Änderung in `main` automatisch aus dem Quellcode gebaut (Workflow `pages.yml`).

## Selbst hosten

Die Web-Version besteht nur aus **statischen Dateien**: HTML, JavaScript, CSS, Schriften und die Gegnerliste als JSON. Es gibt keinen Server-Code, keine Datenbank und keine Anmeldung. Jeder Webserver, der Dateien ausliefern kann, reicht.

### 1. Dateien bauen

Voraussetzung ist Python 3 (keine weiteren Pakete). Im Projektordner:

```
python tools/build_pages.py C:\Pfad\zum\Zielordner
```

Ohne Zielangabe landet das Ergebnis in `_site\`. Das Skript nimmt die Seiten aus `web\` unverändert und passt nur die Pfade an. Es bricht mit einer Meldung ab, wenn eine Anpassung nicht mehr passt, statt halb funktionierende Dateien zu erzeugen.

### 2. Auf den Webserver kopieren

Den Inhalt des Zielordners in ein Verzeichnis des Webservers kopieren. Alle Pfade sind relativ, ein Unterordner wie `https://example.org/dsr/` funktioniert also genauso wie die Wurzel.

Beispiele:

**nginx**
```nginx
location /dsr/ {
    alias /var/www/dsr/;
    add_header Cache-Control "no-cache";
}
```

**Apache** (`.htaccess` im Ordner, `mod_headers` aktiv)
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

**IIS:** Ordner als Website oder virtuelles Verzeichnis anlegen. Falls Schriften nicht laden: MIME-Typ `.woff2` → `font/woff2` ergänzen.

**Nur lokal zum Ausprobieren:**
```
cd _site
python -m http.server 8080
```
Dann `http://localhost:8080/` öffnen.

`Cache-Control: no-cache` ist nicht nötig, sorgt aber dafür, dass OBS nach einem Update sofort die neue Fassung lädt.

### 3. In OBS einrichten

Wie auf der Startseite beschrieben, nur mit deiner Adresse:

1. **Docks** → **Benutzerdefinierte Browser-Docks** → URL `https://deine-adresse/control.html`
2. Im Dock unter „OBS-Overlay“ die Ansicht wählen und **URL kopieren**
3. In der Szene **Quelle hinzufügen → Browser** → kopierte URL einfügen

## Worauf du achten musst

- **Alles von derselben Adresse.** Dock und Overlays tauschen sich über den Browser-Speicher von OBS aus. Der ist pro Adresse (Domain + Port) getrennt. Kommt das Dock von `example.org` und das Overlay von `github.io`, sehen sie sich nicht.
- **Umzug = neuer Stand.** Wechselst du die Adresse, startet der Stand bei null. Vorher im Dock unter „Stand sichern“ als Datei speichern und an der neuen Adresse laden.
- **Nicht als Datei öffnen.** `file:///…` funktioniert in OBS nicht zuverlässig. Es braucht einen Webserver, notfalls den lokalen von Python.
- **HTTPS empfohlen** bei öffentlicher Adresse. Im Heimnetz geht auch HTTP.
- **Ein PC.** Der Stand lebt im OBS auf dem Streaming-PC. Fernsteuerung durch einen Mod von einem anderen PC ist geplant, aber noch nicht enthalten.

## Sicherheit

- Die Seiten senden **nichts** ins Internet. Geladen wird nur, was auf deinem Server liegt (Seiten, Schriften, `dsr_enemies.json`).
- Gespeichert wird nur im Browser-Speicher von OBS bzw. des Browsers. Beim Laden einer Sicherungsdatei wird der Inhalt geprüft und nur bekannte Felder übernommen.
- Empfohlene Header beim eigenen Server (entsprechen denen der exe):

```
Content-Security-Policy: default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; font-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'self'; form-action 'self'; base-uri 'none'
X-Content-Type-Options: nosniff
Referrer-Policy: no-referrer
```

## Aktualisieren

Neue Fassung des Repos holen, `python tools/build_pages.py <Zielordner>` erneut ausführen und die Dateien auf dem Server ersetzen. Der Stand in OBS bleibt dabei erhalten, solange die Adresse gleich bleibt.

## Wie es technisch funktioniert

Die Web-Version nutzt **dieselben Seiten** wie die exe (`web/control.html`, `web/overlay.html`). Neue Overlay-Funktionen kommen damit automatisch auch hier an. Zwei kleine Skripte ergänzen sie:

- [`pages/local-backend.js`](../pages/local-backend.js) übernimmt im Browser die Rolle der exe. Es beantwortet `/events` und `/api/…` lokal, hält den Stand im `localStorage` und gleicht Dock und Overlays ab (storage-Ereignis, BroadcastChannel und zur Sicherheit alle 0,7 Sekunden).
- [`pages/control-web.js`](../pages/control-web.js) passt die Steuerseite an: Run-Uhr, Sichern/Laden, Exe-spezifische Teile ausgeblendet.

[`tools/build_pages.py`](../tools/build_pages.py) setzt beides zusammen.
