🇩🇪 **Deutsch** | 🇬🇧 [English](ROADMAP.en.md)

# Weg zu Version 1.0

Version 1.0 bedeutet: **Ein kompletter DLDU-Run lief mit dem Tracker fehlerfrei durch.** Bis dahin bleibt das Projekt bei 0.x. Der Funktionsumfang ist vollständig, offen ist nur noch der Praxistest im echten Spiel.

## Offene Tests

Getestet mit der exe, Dark Souls Remastered 1.03.1 (Steam):

- [ ] **Kompletter Run** mit allen 84 Gegnern der Liste
- [ ] **Automatische Erkennung** aller 72 Gegner mit Flag, besonders der Minibosse (z. B. Black Knights, Kristallgolems) und der Invader (z. B. Kirk, Leeroy)
- [ ] **Manuelle Gegner:** Die 12 respawnenden Gegner lassen sich sauber abhaken (auch über die Schnellleiste)
- [ ] **Gebietserkennung** in allen 26 Gebieten, vor allem an den Übergängen zwischen Gebieten
- [ ] **Overlay im Run:** Einblendungen, „Wechselnde Gebiete“ springt zum Gebiet und hält, Lückenbalken und ausgelassene Gegner stimmen
- [ ] **Unterbrechungen:** Spiel oder Tracker zwischendurch neu starten, Spielstand neu laden – der Stand bleibt richtig

Fällt dabei etwas auf: Screenshot (bei der Gebietserkennung mit der Position aus der Steuerseite) und eine kurze Beschreibung als [Issue](../../issues).

## Bereits getestet

- Kill-Erkennung und Einblendung im echten Spiel (Asyl-Dämon)
- „Wechselnde Gebiete“: Sprung zum Gebiet und Halten nach dem Kill, auch wenn das Gebiet dadurch komplett erledigt ist
- Erkennung der Spielversion (1.03.1 Steam)
- `update.bat`: Download, Prüfsumme, Herkunftsnachweis, Ersetzen der exe
- Web-Version: OBS-Dock und Browserquelle teilen sich den Stand; Abhaken, Run-Uhr und Overlay im echten OBS

## Ideen für später (nicht nötig für 1.0)

- Fernsteuerung der Web-Version durch einen Mod von einem anderen PC
- Steuerseite und Einrichtungsassistent auf Englisch (die Overlays können es schon)
- Automatische Tests auf GitHub (Rechenlogik und Browser-Tests der Seiten)
