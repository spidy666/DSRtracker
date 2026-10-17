🇩🇪 **Deutsch** | 🇬🇧 [English](ROADMAP.en.md)

# Weg zu Version 1.0

Version 1.0 bedeutet: **Ein kompletter Run lief mit dem Tracker fehlerfrei durch.** Bis dahin bleibt das Projekt bei 0.x. Der Funktionsumfang ist vollständig, offen ist nur noch der Praxistest im echten Spiel.

## Offene Tests

Getestet mit der exe, Dark Souls Remastered 1.03.1 (Steam):

- [ ] **Kompletter Run** mit allen 84 Gegnern der Liste
- [ ] **Automatische Erkennung** aller 72 Gegner mit Flag, besonders der Minibosse (z. B. Black Knights, Kristallgolems) und der Invader (z. B. Kirk, Leeroy). Stand: 11 von 72 bestätigt (siehe unten)
- [ ] **Manuelle Gegner:** Die 12 respawnenden Gegner lassen sich sauber abhaken (auch über die Schnellleiste)
- [ ] **Gebietserkennung** in allen 26 Gebieten, vor allem an den Übergängen zwischen Gebieten. Stand: 7 von 26 Gebieten bestätigt (siehe unten)
- [ ] **Overlay im Run:** Einblendungen, „Wechselnde Gebiete“ springt zum Gebiet und hält, Lückenbalken und ausgelassene Gegner stimmen
- [ ] **Unterbrechungen:** Spiel oder Tracker zwischendurch neu starten, Spielstand neu laden – der Stand bleibt richtig

Fällt dabei etwas auf: Screenshot (bei der Gebietserkennung mit der Position aus der Steuerseite) und eine kurze Beschreibung als [Issue](../../issues).

## Hilfsmittel zum Testen

Wer nicht jeden Boss regulär schaffen will, kann zum Testen [DSR-Gadget](https://github.com/JKAnderson/DSR-Gadget) verwenden. Das ist ein frei verfügbares Testwerkzeug für Dark Souls Remastered und gehört nicht zu diesem Projekt; es wird nicht mehr weiterentwickelt, läuft aber mit 1.03.1. Nützlich sind:

- **Player → Bonfire + Warp:** direkt zu jedem Leuchtfeuer springen, z. B. für die Gebietserkennung
- **Player → Store / Restore:** Position merken und zurückspringen
- **Cheats → No Death, Exterminate, Infinite Stamina** und **Enemies → Disable AI:** Bosse schnell besiegen

Wichtig:
- **Nur offline spielen.** Das Gadget schreibt in den Spielspeicher; online kann das zu einer Sperre führen.
- **Keine Event-Flags über das Gadget setzen** (Reiter Misc) und **„Player + Enemies → No Death“ nicht einschalten** – sonst sterben die Bosse nicht regulär und der Test sagt nichts über die Erkennung aus.
- **Vorher den Spielstand sichern** (`Dokumente\NBGI\DARK SOULS REMASTERED\<Nummer>\DRAKS0005.sl2`), am besten mit einem eigenen Test-Charakter.
- Der Tracker selbst liest weiterhin nur; das Gadget ist ein getrenntes Programm.

## Bereits getestet

- Kill-Erkennung und Einblendung im echten Spiel (Asyl-Dämon)
- Automatisch erkannt (Test am 17.10.2026): Asyl-Dämon, Taurus-Dämon, Havel, Reißkeiler (Bezirk), Ziegendämon, Klaffdrache, Untoten-Köchin 1 und 2, Seath, Manus, Gwyn
- Gebietserkennung richtig: Nördliches Asyl, Stadt der Untoten, Bezirk der Untoten (auch Untere Stadt und Ziegendämon-Arena), Untiefen, Kristallhöhle, Schlund des Abgrunds, Ofen der Ersten Flamme; der Tunnel zwischen Unterer Stadt und Feuerband-Schrein zählt zum Schrein. Bekannte Kleinigkeit: auf den ersten Stufen hinter der Tür von den Untiefen in die Untere Stadt steht noch kurz „Untiefen“
- Splits mit Spielzeit für jeden Kill
- „Wechselnde Gebiete“: Sprung zum Gebiet und Halten nach dem Kill, auch wenn das Gebiet dadurch komplett erledigt ist
- Erkennung der Spielversion (1.03.1 Steam)
- `update.bat`: Download, Prüfsumme, Herkunftsnachweis, Ersetzen der exe
- Web-Version: OBS-Dock und Browserquelle teilen sich den Stand; Abhaken, Run-Uhr und Overlay im echten OBS

## Ideen für später (nicht nötig für 1.0)

- Fernsteuerung der Web-Version durch einen Mod von einem anderen PC
- Steuerseite und Einrichtungsassistent auf Englisch (die Overlays können es schon)
- Automatische Tests auf GitHub (Rechenlogik und Browser-Tests der Seiten)
