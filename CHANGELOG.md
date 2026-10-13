# Änderungen

## Unveröffentlicht

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
