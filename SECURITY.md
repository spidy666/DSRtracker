# Sicherheit / Security Policy

## Unterstützte Versionen

Sicherheitskorrekturen gibt es nur für die jeweils **neueste Version** auf der [Release-Seite](https://github.com/spidy666/DSRtracker/releases/latest). Bitte vor einer Meldung prüfen, ob das Problem dort noch besteht.

## Sicherheitslücke melden

Bitte **kein öffentliches Issue** anlegen, solange die Lücke nicht behoben ist.

Stattdessen vertraulich über GitHub melden:
**Security → Report a vulnerability** ([direkt zum Formular](https://github.com/spidy666/DSRtracker/security/advisories/new)).

Hilfreich sind:
- betroffene Version und Windows-Version
- was genau passiert und wie man es nachstellen kann
- welche Auswirkung du erwartest (z. B. Zugriff von außen, Datenverlust)

Ich bestätige den Eingang so schnell wie möglich und melde mich, sobald eine Einschätzung oder Korrektur vorliegt. Das ist ein Hobbyprojekt einer einzelnen Person, bitte etwas Geduld.

## Was als Sicherheitsproblem zählt

Zum Beispiel:
- Der lokale Webserver ist von einem anderen Rechner oder von einer fremden Webseite aus erreichbar bzw. steuerbar.
- Der Tracker schreibt in den Spielspeicher, verändert Spieldateien oder greift auf andere Prozesse zu.
- Dateien außerhalb des Datenordners (`%APPDATA%\DSRTracker`) werden gelesen oder geschrieben.
- Die Release-exe weicht vom Quellcode ab (Prüfsumme oder Herkunftsnachweis stimmen nicht).

Wie der Tracker abgesichert ist, steht in der [README](README.md#sicherheit).

---

**English:** Please report security issues privately via **Security → Report a vulnerability** on GitHub, not as a public issue. Only the latest release is supported.
