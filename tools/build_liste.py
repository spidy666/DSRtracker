#!/usr/bin/env python3
"""Baut die Offline-Liste: eine einzelne HTML-Datei zum Abhaken, ohne Server und ohne Internet.

Setzt in tools/liste.template.html die Gegnerliste (aus dsr_enemies.json, ohne Zonenkarte)
und die Schriften (als data:-URL eingebettet) ein.

    python3 tools/build_liste.py [Zieldatei]      (Standard: DSR-Liste.html)
"""
import base64
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
FONTS = [  # (Familie, Gewicht, Datei) – nur was die Liste braucht
    ("Cinzel", 700, "cinzel-latin-700-normal.woff2"),
    ("Alegreya Sans", 400, "alegreya-sans-latin-400-normal.woff2"),
    ("Alegreya Sans", 700, "alegreya-sans-latin-700-normal.woff2"),
]


def build(out: Path):
    template = (ROOT / "tools" / "liste.template.html").read_text(encoding="utf-8")
    enemies = json.loads((ROOT / "dsr_enemies.json").read_text(encoding="utf-8"))
    data = {k: enemies[k] for k in ("areas", "route", "randomizer") if k in enemies}
    # "</" maskieren, damit kein Name das <script> vorzeitig beenden kann
    data_js = json.dumps(data, ensure_ascii=False, separators=(",", ":")).replace("</", "<\\/")
    fonts_css = "\n".join(
        f'  @font-face {{ font-family: "{fam}"; font-weight: {w}; font-display: swap; '
        f'src: url(data:font/woff2;base64,{base64.b64encode((ROOT / "web" / "fonts" / f).read_bytes()).decode()}) format("woff2"); }}'
        for fam, w, f in FONTS)
    for marker in ("/*DATA*/", "/*FONTS*/"):
        if template.count(marker) != 1:
            sys.exit(f"build_liste: Platzhalter {marker} fehlt in liste.template.html")
    html = template.replace("/*DATA*/", data_js).replace("/*FONTS*/", fonts_css)
    out.write_text(html, encoding="utf-8")
    return out


if __name__ == "__main__":
    p = build(Path(sys.argv[1] if len(sys.argv) > 1 else ROOT / "DSR-Liste.html").resolve())
    print(f"Offline-Liste gebaut: {p} ({p.stat().st_size // 1024} KB)")
