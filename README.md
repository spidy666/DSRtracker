# DSR Tracker

Zeigt im Stream an, welche Bosse und Minibosse der Punkteliste in **Dark Souls Remastered** besiegt sind, und rechnet die Punkte zusammen.

Das Tool **liest** den Spielspeicher nur (Event-Flags) und schreibt nichts hinein.

## Screenshots

Overlay „Liste“: alle Gebiete mit allen Gegnern, oben die Gesamtpunkte mit Spielzeit.

![Overlay Liste](docs/screenshots/overlay-liste.png)

| Kompakt | Nächstes Ziel | Aktuelles Gebiet |
|---|---|---|
| ![Kompakt](docs/screenshots/overlay-kompakt.png) | ![Nächstes Ziel](docs/screenshots/overlay-naechstes-ziel.png) | ![Aktuelles Gebiet](docs/screenshots/overlay-aktuelles-gebiet.png) |

Laufband für den Bildschirmrand:

![Laufband](docs/screenshots/overlay-laufband.png)

Steuerseite: Gegner abhaken, Overlay-URL zusammenklicken, Splits und Flag-Monitor.

![Steuerseite](docs/screenshots/steuerseite.png)

<details>
<summary>Einrichtungsassistent und Guide</summary>

![Einrichtungsassistent](docs/screenshots/einrichtung.png)

![Guide](docs/screenshots/guide.png)

</details>

Die Screenshots zeigen einen nachgestellten Run (ohne Spielbild, der Overlay-Hintergrund ist in OBS durchsichtig).

## Bauen (einmalig)

1. .NET SDK installieren (8 oder neuer): https://dotnet.microsoft.com/download/dotnet/8.0
2. `build.bat` doppelklicken
3. Ergebnis: `dist\DSRTracker.exe` (eigenständig, braucht kein installiertes .NET)

## Benutzen

1. `DSRTracker.exe` starten (vor oder nach dem Spiel). Beim **ersten Start** öffnet sich ein **Einrichtungsassistent** (Verbindungstest, Overlay-Auswahl, OBS-Einrichtung mit Test-Einblendung), danach die Steuerseite. Der Assistent ist jederzeit unter `http://localhost:8765/setup` erreichbar.
2. In OBS: Quelle → **Browser** → URL `http://localhost:8765/overlay`
   - Optionen lassen sich auf der Steuerseite zusammenklicken (Spalten, Größe, nur offene Gegner, Kompaktansicht …).
3. Spielen. Erkannte Kills werden automatisch abgehakt und eingeblendet.

## Automatisch vs. manuell

- 72 von 84 Gegnern haben ein dauerhaftes Kill-Flag im Spielstand und werden automatisch erkannt.
- 12 Gegner respawnen und haben kein Flag (Black Knights im Ofen, Kanalisierer + Golem im Archiv, Berenike-Ritter in Sens Festung und Gemalter Welt, Titanitdämon in Izalith). Die auf der Steuerseite per Klick abhaken.
- Jeder Gegner kann zusätzlich manuell abgehakt werden, falls ein Flag mal nicht auslöst.

## Dateien

- `dsr_enemies.json` – Gegnerliste mit Punkten und Flag-IDs (kann angepasst werden, liegt neben der exe)
- `state.json` – gespeicherte manuelle Haken

## Startoptionen

- `--port 8766` – anderer Port
- `--no-browser` – Steuerseite nicht automatisch öffnen
- `--web <ordner>` – HTML-Dateien aus einem Ordner laden statt der eingebauten (zum Anpassen ohne Neu-Bauen)
- `--selftest` – prüft die Flag-IDs der Gegnerliste

## Herkunft der Daten

Flag-IDs aus den Event-Skripten von DSR (ausgelesen mit Soulstruct). Speicher-Signaturen und Flag-Adressierung nach JKAnderson (DSR-Gadget) und FrankvdStam (SoulMemory / SoulSplitter).

## Lizenz

Copyright (C) 2026 spidy666

Dieses Projekt steht unter der **GNU General Public License v3.0** (siehe `LICENSE`).
Es verwendet Speicher-Signaturen und die Event-Flag-Adressierung aus
[SoulMemory / SoulSplitter](https://github.com/FrankvdStam/SoulSplitter) (GPL-3.0)
sowie Erkenntnisse aus [DSR-Gadget](https://github.com/JKAnderson/DSR-Gadget).
