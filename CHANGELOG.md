🇩🇪 **Deutsch** | 🇬🇧 [English](CHANGELOG.en.md)

# Änderungen

## Unveröffentlicht

- Gegnerliste: abgeschnittener Tipp der drei Kanalisierer im Archiv ergänzt („Im Archiv, u. a. beim Aufzug zu Beginn und in der Bibliothek.“)
- Offline-Liste `DSR-Liste.html`: die Punkteliste als einzelne Datei zum Herunterladen und Abhaken, ohne Server, Internet und OBS (Original- und Randomizer-Liste, Punktestand, Uhr, Suche, Sortierung nach Route, vier Farbschemata). Download auf der Web-Seite

## 0.7.0 – 2026-10-18

- Randomizer-Liste: Auf der Steuerseite lässt sich die Gegnerliste von „Original“ auf „Randomizer“ umschalten (exe und Web-Version). Gleiche Plätze, Flags und Punkte, aber die Gegnernamen der Randomizer-Version, dazu 8 zusätzliche Plätze zum Abhaken, 2 Plätze im Ofen entfallen (90 Gegner, 324 Punkte). Die Overlays zeigen dann „DSR Randomizer“
- Splits: Gegner, die beim Start des Trackers schon besiegt waren (z. B. Spielstand mit erledigtem Boss geladen), erscheinen als Zeile „vorher erledigt“. Die Summenspalte endet damit immer beim Gesamtstand
- Guide: genauere Wegbeschreibung der beiden Untoten-Köchinnen (Küche mit Truhe; Raum mit Laurentius im Fass)
- `ROADMAP.md`: Ergebnisse des ersten Testabends (11 Gegner, 7 Gebiete bestätigt) und Abschnitt „Hilfsmittel zum Testen“ mit DSR-Gadget; Hinweis darauf im README

## 0.6.1 – 2026-10-17

- Einrichtungsassistent: Manche Vorschaubilder blieben leer, bei jedem Wechsel des Farbschemas ein anderes. Ursache war das Verbindungslimit des Browsers (6 gleichzeitige Verbindungen). Die Vorschauen fragen den Stand jetzt kurz ab, statt dauerhaft verbunden zu bleiben
- `ROADMAP.md`: offene Tests bis Version 1.0 (kompletter Run, Erkennung aller Bosse, Gebietserkennung)

## 0.6.0 – 2026-10-17

- Steuerseite (exe und Web-Version): Schnellleiste oben mit „✓ Nächstes Ziel“ (ein Klick hakt den nächsten offenen Gegner der Route ab), „↶ Rückgängig“ (letzter manueller Haken) und Suchfeld (Enter hakt ab, wenn genau ein Gegner passt). In der Web-Version zusätzlich die Run-Uhr. Gebiete und die Overlay-Einstellungen lassen sich per Klick auf die Überschrift einklappen; im schmalen OBS-Dock kompaktere Darstellung
- Web-Version: „Aktuelles Gebiet“ zeigt „keins“ statt „unbekannt (auto)“, eine automatische Erkennung gibt es dort nicht
- README: Screenshot der Web-Version als OBS-Dock
- `WICHTIG_LIES_MICH.txt` aktualisiert: drei Wege (fertige exe, selbst bauen, Web-Version), aktueller Dateiumfang

## 0.5.4 – 2026-10-17

- Web-Version ohne exe, online unter https://spidy666.github.io/DSRtracker/: Steuerseite als OBS-Dock, Overlays als Browserquellen, Abhaken von Hand, Run-Uhr statt Spielzeit, Stand als Datei sichern/laden. Wird aus denselben Seiten gebaut (`tools/build_pages.py`) und auf GitHub Pages veröffentlicht; läuft auch auf jedem eigenen Webserver
- Quelltext ausführlich kommentiert, dazu `docs/CODE.md` mit einer Erklärung aller Teile (Programmverhalten unverändert)
- Dokumentation auf Englisch: `README.en.md`, `SECURITY.en.md`, `CHANGELOG.en.md`, `docs/CODE.en.md`, mit Sprachwechsel oben in jeder Datei. Deutsch bleibt Hauptsprache, die Code-Kommentare bleiben deutsch
- Wechselnde Gebiete: Nach einem Kill, der ein Gebiet komplett erledigt, bleibt dieses Gebiet jetzt die eingestellte Haltezeit lang sichtbar, auch mit „erledigte ausblenden“. Vorher verschwand es sofort, und die Haltezeit lief auf dem nächsten Gebiet ab (betraf z. B. den Asylum-Dämon als einzigen Gegner von Kapitel 1)

## 0.5.3 – 2026-10-13

Programm unverändert gegenüber 0.5.2.
- `update.bat`: lädt die neueste exe von der Release-Seite, prüft SHA256 und (mit GitHub CLI) den Herkunftsnachweis, ersetzt erst dann die alte exe und behält sie als `.bak`. Zeigt jeden Schritt an und wartet am Ende auf einen Tastendruck. Wird ab jetzt an jeden Release angehängt

## 0.5.2 – 2026-10-13

Programm unverändert gegenüber 0.5.1.
- Release-exe erhält einen von GitHub signierten Herkunftsnachweis (Build-Provenance), prüfbar mit `gh attestation verify`
- `SECURITY.md`: Sicherheitslücken vertraulich über GitHub melden
- README: Schritt-für-Schritt-Anleitung „So bekommst du eine saubere exe“

## 0.5.1 – 2026-10-13

- Spielversion wird über die SHA256-Prüfsumme der `DarkSoulsRemastered.exe` erkannt (bekannt: 1.03.1 Steam) und in Konsole und Steuerseite angezeigt. Die Dateiversion der DSR-exe ist immer „1.0.0.0“ und taugt dafür nicht. Unbekannte exe-Dateien werden weiterhin gelesen und als „unbekannt“ angezeigt.

## 0.5.0 – 2026-10-13

**Sicherheit**
- Webserver nimmt nur Anfragen vom eigenen Rechner an und prüft den Host-Header (Schutz gegen DNS-Rebinding)
- CORS-Freigabe (`Access-Control-Allow-Origin: *`) am Live-Datenstrom entfernt: Andere Webseiten können den Stand nicht mehr mitlesen
- Ändernde Anfragen (Abhaken, Reset, Gebiet, Einrichtung) nur noch von der eigenen Seite: fremde `Origin` oder `Sec-Fetch-Site: cross-site` werden mit 403 abgewiesen (Schutz gegen CSRF)
- Sicherheits-Header auf allen Antworten: Content-Security-Policy (nur eigene Inhalte, Einbetten nur durch eigene Seiten), `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`
- Spielprozess wird mit `PROCESS_QUERY_LIMITED_INFORMATION` statt `PROCESS_QUERY_INFORMATION` geöffnet (minimal nötige Rechte); vor dem Lesen wird geprüft, dass es wirklich `DarkSoulsRemastered.exe` ist, die Dateiversion wird angezeigt. Bleibt die Speicherstruktur unauffindbar, weist der Tracker auf eine evtl. nicht unterstützte Version hin
- Hinweise „als Administrator starten“ entfernt: Administratorrechte sind nicht nötig
- Schriftarten werden lokal mitgeliefert (SIL Open Font License) statt von Google Fonts geladen: keine Verbindungen nach außen mehr
- Daten liegen jetzt in `%APPDATA%\DSRTracker` (`--portable` für den alten Ort neben der exe); ein alter `state.json` wird einmalig übernommen
- Gegnerliste ist fest eingebaut; eine eigene `dsr_enemies.json` im Datenordner wird beim Laden geprüft und bei Fehlern ignoriert. Die alte Datei neben der exe wird nicht mehr verwendet, das Löschen nach Updates entfällt
- GitHub Actions: Release-exe wird öffentlich aus dem Quellcode gebaut und mit SHA256-Prüfsumme an den Release gehängt; Build-Prüfung bei jedem Push; CodeQL-Analyse; Dependabot
- README: Abschnitte „Was das Tool tut und was nicht“, „Download und Prüfsumme“, „Sicherheit“ und „Entstehung“ (Hinweis auf KI-Unterstützung)

## 0.3.0 – 2026-10-13

**Neu**
- Overlay-Ansicht „Wechselnde Gebiete“ (`view=rotate`): Titel, Gesamtpunkte mit Balken, darunter ein Gebiet nach dem anderen (alle 5 Sekunden). Bei einem Kill springt die Anzeige in dessen Gebiet und bleibt dort 15 Sekunden stehen. Optionen: `title=` (eigener Titel), `interval=` (Sekunden pro Seite), `hold=` (Sekunden nach einem Kill), `merge=0` (kleine Gebiete nicht zusammenfassen; standardmäßig teilen sich Gebiete mit höchstens 3 Gegnern eine Seite, wenn sie in der Liste nebeneinander stehen und im Run laut Route direkt nacheinander drankommen), `order=route`, `hideCleared=1`
- Wechselnde Gebiete: Balken nur so breit wie die Kopfzeile (die Spiel-HUD links bleibt frei), dezenter Schatten hinter dem Text für Kontrast auf hellem Spielbild (`shade=0` schaltet ihn ab), offene Gegner heller
- Kill-Einblendung bei „Wechselnde Gebiete“ standardmäßig unten rechts, damit sie den Punktestand nicht verdeckt (Steuerseite: „passend zur Ansicht“)
- Ablaufbalken unter der Seite in „Wechselnde Gebiete“ zeigt, wann die Seite wechselt (`timer=0` blendet ihn aus)
- Ausgelassene Gegner werden rötlich markiert: noch offen, aber laut Route vor dem zuletzt besiegten Gegner. Gilt für Liste, Aktuelles Gebiet, Wechselnde Gebiete und den Lückenbalken (`missed=0` schaltet es ab)
- Einrichtungsassistent: Schnellstart „Bin überfordert, mach einfach!“ auf der Willkommensseite. Ein Klick wählt „Wechselnde Gebiete“ (Lückenbalken, fertige Gebiete ausblenden, Englisch) und „Nächstes Ziel“ (zwei Ziele, ohne Tipps) und springt direkt zu den OBS-Links
- Einrichtungsassistent: Im Schritt „OBS einrichten“ zeigt jede Quelle eine maßstabsgetreue Live-Vorschau und einen Link „Vorschau öffnen“
- Deutsche Namen mit den Spieltexten abgeglichen: „Grabmal der Riesen“, „Quelaags Sphäre“, „Ofen der Ersten Flamme“, „Ritter von Berenike“, „Wendelin“ in den Kapiteltiteln
- Steuerseite: Overlay-Optionen in Gruppen (Ansicht, Aussehen, Inhalt, Laufband, Seitenwechsel); es werden nur die Optionen angezeigt, die für die gewählte Ansicht etwas bewirken, dazu die passende OBS-Größe
- Fortschrittsbalken mit Lücken (`bar=gaps`): ein Stück pro Gegner in Routen-Reihenfolge, Breite nach Punkten. Ausgelassene Gegner bleiben als Lücke sichtbar. Gilt für Liste, Kompakt, Laufband und Wechselnde Gebiete

## 0.2.0 – 2026-10-13

**Neu**
- Einrichtungsassistent beim ersten Start (`/setup`): Verbindungstest mit dem Spiel, Overlay-Auswahl mit Live-Vorschau (auch mehrere Ansichten gleichzeitig), OBS-Anleitung mit Test-Einblendung
- Overlay-Ansichten: Liste, Kompakt, Nächstes Ziel, Aktuelles Gebiet, Laufband
- Spielzeit (In-Game-Time), Punkte pro Stunde und Splits je Kill
- Aktuelles Gebiet anhand der Spielerposition, manuell überschreibbar
- Sortierung nach Gebiet oder nach Route
- Farbschemata: Gold, Asche & Glut, Mondlicht, Abgrund (Overlay und Steuerseite)
- Guide mit Route, Weg und Taktik zu allen 84 Gegnern (`/guide`)
- Versionsnummer im Konsolenfenster

**Erkennung**
- 72 von 84 Gegnern werden automatisch über Event-Flags erkannt, 12 respawnende Gegner werden manuell abgehakt

## 0.1.0

- Erste Version: Flag-Tracking, Steuerseite, OBS-Overlay
