# Änderungen

## Unveröffentlicht

**Neu**
- Overlay-Ansicht „Wechselnde Gebiete“ (`view=rotate`): Titel, Gesamtpunkte mit Balken, darunter ein Gebiet nach dem anderen (alle 5 Sekunden). Bei einem Kill springt die Anzeige in dessen Gebiet und bleibt dort 15 Sekunden stehen. Optionen: `title=` (eigener Titel), `interval=` (Sekunden pro Seite), `hold=` (Sekunden nach einem Kill), `order=route`, `hideCleared=1`
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
