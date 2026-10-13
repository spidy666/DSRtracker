# DSR Tracker

Zeigt im Stream an, welche Bosse und Minibosse der Punkteliste in **Dark Souls Remastered** besiegt sind, und rechnet die Punkte zusammen.

Das Tool **liest** den Spielspeicher nur (Event-Flags) und schreibt nichts hinein.

## Was das Tool tut und was nicht

**Es tut:**
- Es öffnet `DarkSoulsRemastered.exe` **nur zum Lesen** (`PROCESS_VM_READ | PROCESS_QUERY_LIMITED_INFORMATION`) und liest mit `ReadProcessMemory` die Kill-Flags, die Spielzeit und die Spielerposition.
- Es startet einen kleinen Webserver, der **nur auf diesem Rechner** erreichbar ist (`http://localhost:8765`), für Steuerseite und OBS-Overlay.
- Es speichert manuelle Haken und Einstellungen in `%APPDATA%\DSRTracker\state.json`.

**Es tut nicht:**
- Kein Schreiben in den Spielspeicher (`WriteProcessMemory` kommt im Code nicht vor), keine Code- oder DLL-Injection, keine Änderungen an Spieldateien oder Spielständen.
- Keine Administratorrechte nötig.
- Keine Verbindungen ins Internet: keine Telemetrie, keine automatischen Updates, keine nachgeladenen Skripte. Auch die Schriftarten werden lokal mitgeliefert.

Die beiden Stellen, die man zum Prüfen lesen sollte: [`GameReader.cs`](GameReader.cs) (Zugriff auf das Spiel) und [`WebServer.cs`](WebServer.cs) (lokaler Webserver).

## Screenshots

Overlay „Wechselnde Gebiete“: Gesamtpunkte mit Lückenbalken, darunter wechselt alle paar Sekunden das Gebiet. Ausgelassene Gegner sind rot markiert, die Linie unten zeigt, wann die Seite wechselt.

![Overlay Wechselnde Gebiete](docs/screenshots/overlay-wechselnde-gebiete.png)

Overlay „Liste“: alle Gebiete mit allen Gegnern, oben die Gesamtpunkte mit Spielzeit.

![Overlay Liste](docs/screenshots/overlay-liste.png)

| Kompakt | Nächstes Ziel | Aktuelles Gebiet |
|---|---|---|
| ![Kompakt](docs/screenshots/overlay-kompakt.png) | ![Nächstes Ziel](docs/screenshots/overlay-naechstes-ziel.png) | ![Aktuelles Gebiet](docs/screenshots/overlay-aktuelles-gebiet.png) |

Laufband für den Bildschirmrand:

![Laufband](docs/screenshots/overlay-laufband.png)

Steuerseite: Gegner abhaken, Overlay-URL zusammenklicken, Splits und Flag-Monitor.

![Steuerseite](docs/screenshots/steuerseite.png)

Lückenbalken (`bar=gaps`): ein Stück pro Gegner in Routen-Reihenfolge, ausgelassene Gegner bleiben als rötliche Lücke sichtbar.

![Kompakt mit Lückenbalken](docs/screenshots/overlay-luecken.png)

<details>
<summary>Overlay-Optionen, Schnellstart, Einrichtungsassistent und Guide</summary>

Overlay-Optionen der Steuerseite (es erscheinen nur die Optionen, die zur gewählten Ansicht passen):

![Overlay-Optionen](docs/screenshots/steuerseite-optionen.png)

Schnellstart im Einrichtungsassistenten:

![Bin überfordert, mach einfach](docs/screenshots/einrichtung-mach-einfach.png)

![Einrichtungsassistent](docs/screenshots/einrichtung.png)

![Guide](docs/screenshots/guide.png)

</details>

Die Screenshots zeigen einen nachgestellten Run (ohne Spielbild, der Overlay-Hintergrund ist in OBS durchsichtig).

## Download und Prüfsumme

Die `DSRTracker.exe` an den [Releases](../../releases) wird von **GitHub Actions** öffentlich aus genau diesem Quellcode gebaut (siehe [`.github/workflows/release.yml`](.github/workflows/release.yml) und das Build-Protokoll im Reiter „Actions“). Daneben liegt `DSRTracker.exe.sha256` mit der Prüfsumme. Prüfen unter Windows:

```
certutil -hashfile DSRTracker.exe SHA256
```

Der ausgegebene Wert muss mit dem in `DSRTracker.exe.sha256` übereinstimmen. Hinweis: Eine selbst gebaute exe hat eine andere Prüfsumme als die vom Release, weil Builds nicht bitgenau gleich sind. Wer ganz sichergehen will, baut selbst (siehe unten).

Die exe ist nicht signiert. Windows SmartScreen kann deshalb beim ersten Start warnen („Weitere Informationen“ → „Trotzdem ausführen“).

## Bauen (einmalig)

1. .NET SDK installieren (8 oder neuer): https://dotnet.microsoft.com/download/dotnet/8.0
2. `build.bat` doppelklicken
3. Ergebnis: `dist\DSRTracker.exe` (eigenständig, braucht kein installiertes .NET)

## Benutzen

1. `DSRTracker.exe` starten (vor oder nach dem Spiel). Beim **ersten Start** öffnet sich ein **Einrichtungsassistent** (Verbindungstest, Overlay-Auswahl, OBS-Einrichtung mit Test-Einblendung), danach die Steuerseite. Wer nicht viel einstellen will, klickt dort auf **„Bin überfordert, mach einfach!“** und bekommt direkt die fertigen OBS-Links. Der Assistent ist jederzeit unter `http://localhost:8765/setup` erreichbar.
2. In OBS: Quelle → **Browser** → URL `http://localhost:8765/overlay`
   - Optionen lassen sich auf der Steuerseite zusammenklicken (Spalten, Größe, nur offene Gegner, Kompaktansicht …).
3. Spielen. Erkannte Kills werden automatisch abgehakt und eingeblendet.

## Automatisch vs. manuell

- 72 von 84 Gegnern haben ein dauerhaftes Kill-Flag im Spielstand und werden automatisch erkannt.
- 12 Gegner respawnen und haben kein Flag (Black Knights im Ofen, Kanalisierer + Golem im Archiv, Ritter von Berenike in Sens Festung und Gemalter Welt, Titanitdämon in Izalith). Die auf der Steuerseite per Klick abhaken.
- Jeder Gegner kann zusätzlich manuell abgehakt werden, falls ein Flag mal nicht auslöst.

## Dateien

Alle Daten liegen in `%APPDATA%\DSRTracker` (mit `--portable` stattdessen neben der exe):

- `state.json` – manuelle Haken, letzte Kills, Einstellungen. Ein alter Stand neben der exe wird beim ersten Start einmalig übernommen.
- `dsr_enemies.json` – optional. Die Gegnerliste ist in die exe eingebaut. Wer eine eigene Liste nutzen will, legt sie hier ab. Sie wird beim Laden geprüft; ist sie fehlerhaft, nutzt der Tracker die eingebaute Liste.

## Startoptionen

- `--port 8766` – anderer Port
- `--no-browser` – Steuerseite nicht automatisch öffnen
- `--portable` – Daten neben der exe speichern statt unter `%APPDATA%`
- `--web <ordner>` – HTML-Dateien aus einem Ordner laden statt der eingebauten (zum Anpassen ohne Neu-Bauen)
- `--selftest` – prüft die Flag-IDs der Gegnerliste

## Sicherheit

- Der Webserver lauscht nur auf `localhost`, nimmt nur Anfragen vom eigenen Rechner an und prüft den Host-Header (Schutz gegen DNS-Rebinding).
- Es gibt keine CORS-Freigaben. Andere Webseiten im Browser können den Stand weder lesen noch verändern: Ändernde Anfragen werden nur per POST von der eigenen Seite angenommen (Prüfung von `Origin` und `Sec-Fetch-Site`).
- Ausgeliefert werden nur fest eingetragene Dateien, Pfade aus Anfragen werden nie für Dateizugriffe verwendet.
- Die Seiten verbieten per Content-Security-Policy das Nachladen fremder Inhalte.
- Das Projekt hat keine NuGet-Abhängigkeiten. Builds, CodeQL-Analyse und Dependabot laufen über GitHub Actions.

Fehler oder Sicherheitsprobleme bitte als Issue melden.

## Entstehung

Der Tracker wurde mit Unterstützung von KI (Claude von Anthropic) entwickelt. Der gesamte Code ist offen einsehbar, und gerade deshalb lohnt sich ein Blick in `GameReader.cs` und `WebServer.cs`.

## Herkunft der Daten

Flag-IDs aus den Event-Skripten von DSR (ausgelesen mit Soulstruct). Speicher-Signaturen und Flag-Adressierung nach JKAnderson (DSR-Gadget) und FrankvdStam (SoulMemory / SoulSplitter).

## Lizenz

Copyright (C) 2026 spidy666

Dieses Projekt steht unter der **GNU General Public License v3.0** (siehe `LICENSE`).
Es verwendet Speicher-Signaturen und die Event-Flag-Adressierung aus
[SoulMemory / SoulSplitter](https://github.com/FrankvdStam/SoulSplitter) (GPL-3.0)
sowie Erkenntnisse aus [DSR-Gadget](https://github.com/JKAnderson/DSR-Gadget).
