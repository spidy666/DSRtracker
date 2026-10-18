#!/usr/bin/env python3
"""Baut die Web-Version (ohne exe) für GitHub Pages oder einen eigenen Webserver.

Nimmt die Seiten aus web/ unverändert und passt nur Pfade an. Ein kleines Skript
(pages/local-backend.js) ersetzt im Browser den Tracker-Server. Ergebnis: ein Ordner
mit statischen Dateien, der auf jedem Webserver läuft.

    python3 tools/build_pages.py [Zielordner]      (Standard: _site)
"""
import shutil
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = Path(sys.argv[1] if len(sys.argv) > 1 else ROOT / "_site").resolve()
BACKEND = '<script src="local-backend.js"></script>\n</head>'


def rewrite(src: Path, dst: str, rules):
    text = src.read_text(encoding="utf-8")
    for old, new in rules:
        if old not in text:
            sys.exit(f"build_pages: '{old}' nicht in {src.name} gefunden – Seite geändert? Regel anpassen.")
        text = text.replace(old, new)
    (OUT / dst).write_text(text, encoding="utf-8")


def main():
    if OUT.exists():
        shutil.rmtree(OUT)
    OUT.mkdir(parents=True)
    for f in (ROOT / "pages").iterdir():
        if f.is_file():
            shutil.copy2(f, OUT / f.name)
    shutil.copytree(ROOT / "web" / "fonts", OUT / "fonts")
    shutil.copy2(ROOT / "dsr_enemies.json", OUT / "dsr_enemies.json")

    fonts = ('href="/fonts/fonts.css"', 'href="fonts/fonts.css"')
    rewrite(ROOT / "web" / "control.html", "control.html", [
        fonts,
        ('href="/setup"', 'href="index.html"'),
        ('href="/guide"', 'href="guide.html"'),
        ("`${location.origin}/overlay${", '`${location.href.replace(/[^/]*$/, "")}overlay.html${'),
        ("</head>", BACKEND),
        ("</body>", '<script src="control-web.js"></script>\n</body>'),
    ])
    rewrite(ROOT / "web" / "overlay.html", "overlay.html", [fonts, ("</head>", BACKEND)])
    rewrite(ROOT / "guide.html", "guide.html", [('href="web/fonts/fonts.css"', 'href="fonts/fonts.css"')])
    # Offline-Liste (eine Datei zum Herunterladen, läuft ohne Server)
    sys.dont_write_bytecode = True
    sys.path.insert(0, str(ROOT / "tools"))
    import build_liste
    build_liste.build(OUT / "DSR-Liste.html")
    (OUT / ".nojekyll").write_text("")
    print(f"Web-Version gebaut: {OUT}")


if __name__ == "__main__":
    main()
