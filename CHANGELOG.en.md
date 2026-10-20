🇩🇪 [Deutsch](CHANGELOG.md) | 🇬🇧 **English**

> This is a translation of the German changelog. If the two differ, the German version applies.

# Changelog

All notable changes to this project are documented in this file. The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

## [0.7.3] - 2026-10-18

### Added
- The run clock can be set to any value (h:mm:ss or mm:ss), not just reset to 0: in the web version (control page, "Run clock" card) and in the offline list (✎ button). The exe reads the game time from the game and has no clock of its own

### Security
- Control page: saved overlay settings are validated when loaded, before they are applied (fixes four CodeQL code-scanning alerts)

## [0.7.2] - 2026-10-18

### Changed
- The exe is built without compression (release and `build.bat`): about 65 instead of 35 MB, but in a VirusTotal test only 1 instead of 3 false positives

## [0.7.1] - 2026-10-18

### Added
- Offline list `DSR-Liste.html`: the points list as a single file to download and check off, without server, internet or OBS (original and randomizer list, score, clock, search, route order, four color schemes). Download on the web page. Loads nothing from outside, even the tab icon is embedded; README with checks on urlscan.io and VirusTotal
- Release: `DSR-Liste.html` is attached to every release with checksum and signed provenance attestation; README section "Verifying the offline list"

### Fixed
- Enemy list: completed the cut-off tip of the three Channelers in the Duke's Archives

## [0.7.0] - 2026-10-18

### Added
- Randomizer list: on the control page the enemy list can be switched from "Original" to "Randomizer" (exe and web version). Same places, flags and points, but the enemy names of the randomizer version, plus 8 additional places to check off, 2 places in the Kiln are dropped (90 enemies, 324 points). The overlays then show "DSR Randomizer"
- `ROADMAP.md`: results of the first test evening (11 enemies, 7 areas confirmed) and section "Tools for testing" with DSR-Gadget; pointer to it in the README

### Changed
- Splits: enemies that were already defeated when the tracker started (e.g. a save with a defeated boss loaded) appear as a "vorher erledigt" (done before) row. The total column now always ends at the overall score
- Guide: more precise directions for the two Butchers (kitchen with chest; room with Laurentius in the barrel)

## [0.6.1] - 2026-10-17

### Added
- `ROADMAP.md`: open tests until version 1.0 (complete run, detection of all bosses, area detection)

### Fixed
- Setup wizard: some preview images stayed empty, a different one on every color scheme change. The cause was the browser's connection limit (6 simultaneous connections). The previews now poll the state briefly instead of staying connected

## [0.6.0] - 2026-10-17

### Added
- Control page (exe and web version): quick bar at the top with "✓ next target" (one click checks off the next open enemy of the route), "↶ undo" (last manual checkmark) and a search field (Enter checks off if exactly one enemy matches). The web version also shows the run clock there. Areas and the overlay settings can be collapsed by clicking the heading; more compact layout in a narrow OBS dock
- README: screenshot of the web version as an OBS dock

### Changed
- Web version: "Current area" shows "keins" (none) instead of "unbekannt (auto)"; there is no automatic detection there
- `WICHTIG_LIES_MICH.txt` (German) updated: three ways (ready-made exe, build yourself, web version), current file list

## [0.5.4] - 2026-10-17

### Added
- Web version without exe, online at https://spidy666.github.io/DSRtracker/: control page as an OBS dock, overlays as browser sources, manual checking, run clock instead of in-game time, save/load the state as a file. Built from the same pages (`tools/build_pages.py`) and published on GitHub Pages; also runs on any own web server
- Source code commented in detail, plus `docs/CODE.md` explaining every part (program behavior unchanged)
- English documentation: `README.en.md`, `SECURITY.en.md`, `CHANGELOG.en.md`, `docs/CODE.en.md`, with a language switch at the top of each file. German remains the main language; code comments stay German

### Fixed
- Rotating areas: after a kill that completes an area, that area now stays visible for the configured hold time, even with "hide completed". Before, it disappeared immediately and the hold time ran out on the next area (this affected e.g. the Asylum Demon as the only enemy of chapter 1)

## [0.5.3] - 2026-10-13

Program unchanged from 0.5.2.

### Added
- `update.bat`: downloads the latest exe from the release page, checks SHA256 and (with the GitHub CLI) the provenance attestation, only then replaces the old exe and keeps it as `.bak`. Shows every step and waits for a key press at the end. From now on attached to every release

## [0.5.2] - 2026-10-13

Program unchanged from 0.5.1.

### Added
- Release exe gets a provenance attestation (build provenance) signed by GitHub, verifiable with `gh attestation verify`
- `SECURITY.md`: report security vulnerabilities confidentially via GitHub
- README: step-by-step guide "How to get a clean exe"

## [0.5.1] - 2026-10-13

### Added
- The game version is detected via the SHA256 checksum of `DarkSoulsRemastered.exe` (known: 1.03.1 Steam) and shown in the console and on the control page. The file version of the DSR exe is always "1.0.0.0" and useless for this. Unknown exe files are still read and shown as "unknown".

## [0.5.0] - 2026-10-13

### Added
- GitHub Actions: the release exe is built publicly from source and attached to the release with a SHA256 checksum; build check on every push; CodeQL analysis; Dependabot
- README: sections "What the tool does and doesn't do", "Download and checksum", "Security" and "Origin" (note on AI assistance)

### Changed
- Data now lives in `%APPDATA%\DSRTracker` (`--portable` for the old location next to the exe); an old `state.json` is imported once
- The enemy list is built in; a custom `dsr_enemies.json` in the data folder is validated on load and ignored if invalid. The old file next to the exe is no longer used, so deleting it after updates is no longer necessary

### Removed
- "Run as administrator" hints: administrator rights are not needed

### Security
- Web server only accepts requests from the own computer and checks the Host header (protection against DNS rebinding)
- CORS permission (`Access-Control-Allow-Origin: *`) removed from the live data stream: other websites can no longer follow the state
- Changing requests (check off, reset, area, setup) only from the own page: a foreign `Origin` or `Sec-Fetch-Site: cross-site` is rejected with 403 (protection against CSRF)
- Security headers on all responses: Content Security Policy (only own content, embedding only by own pages), `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`
- The game process is opened with `PROCESS_QUERY_LIMITED_INFORMATION` instead of `PROCESS_QUERY_INFORMATION` (minimal rights); before reading, it checks that it really is `DarkSoulsRemastered.exe`, and the file version is shown. If the memory structure cannot be found, the tracker points to a possibly unsupported version
- Fonts are bundled locally (SIL Open Font License) instead of loaded from Google Fonts: no more outgoing connections

## [0.3.0] - 2026-10-13

### Added
- Overlay view "Rotating areas" (`view=rotate`): title, total points with bar, below it one area after another (every 5 seconds). On a kill the display jumps to that enemy's area and stays there for 15 seconds. Options: `title=` (custom title), `interval=` (seconds per page), `hold=` (seconds after a kill), `merge=0` (don't combine small areas; by default areas with at most 3 enemies share a page if they are next to each other in the list and come directly after each other in the run according to the route), `order=route`, `hideCleared=1`
- Countdown bar below the page in "Rotating areas" shows when the page changes (`timer=0` hides it)
- Skipped enemies are marked reddish: still open, but before the last defeated enemy according to the route. Applies to List, Current area, Rotating areas and the gap bar (`missed=0` turns it off)
- Progress bar with gaps (`bar=gaps`): one segment per enemy in route order, width by points. Skipped enemies remain visible as a gap. Applies to List, Compact, Ticker and Rotating areas
- Setup wizard: quick start "Bin überfordert, mach einfach!" ("I'm overwhelmed, just do it!") on the welcome page. One click selects "Rotating areas" (gap bar, hide completed areas, English) and "Next target" (two targets, no tips) and jumps directly to the OBS links
- Setup wizard: in the "Set up OBS" step every source shows a true-to-scale live preview and an "Open preview" link

### Changed
- Rotating areas: bar only as wide as the header (the game HUD on the left stays free), subtle shade behind the text for contrast on a bright game image (`shade=0` turns it off), open enemies brighter
- Kill notification for "Rotating areas" bottom right by default so it doesn't cover the score (control page: "matching the view")
- German names aligned with the game texts: "Grabmal der Riesen", "Quelaags Sphäre", "Ofen der Ersten Flamme", "Ritter von Berenike", "Wendelin" in the chapter titles
- Control page: overlay options in groups (View, Appearance, Content, Ticker, Page change); only options that affect the selected view are shown, plus the matching OBS size

## [0.2.0] - 2026-10-13

### Added
- Setup wizard on first start (`/setup`): connection test with the game, overlay selection with live preview (several views at once possible), OBS instructions with test notification
- Overlay views: List, Compact, Next target, Current area, Ticker
- In-game time, points per hour and splits per kill
- Current area based on the player position, can be overridden manually
- Sorting by area or by route
- Color schemes: Gold, Ash & Ember, Moonlight, Abyss (overlay and control page)
- Guide with route, path and tactics for all 84 enemies (`/guide`)
- Version number in the console window
- Detection: 72 of 84 enemies are detected automatically via event flags, 12 respawning enemies are checked off manually

## 0.1.0

### Added
- First version: flag tracking, control page, OBS overlay

[Unreleased]: https://github.com/spidy666/DSRtracker/compare/0.7.3...HEAD
[0.7.3]: https://github.com/spidy666/DSRtracker/compare/0.7.2...0.7.3
[0.7.2]: https://github.com/spidy666/DSRtracker/compare/0.7.1...0.7.2
[0.7.1]: https://github.com/spidy666/DSRtracker/compare/0.7.0...0.7.1
[0.7.0]: https://github.com/spidy666/DSRtracker/compare/0.6.1...0.7.0
[0.6.1]: https://github.com/spidy666/DSRtracker/compare/0.6.0...0.6.1
[0.6.0]: https://github.com/spidy666/DSRtracker/compare/0.5.4...0.6.0
[0.5.4]: https://github.com/spidy666/DSRtracker/compare/0.5.3...0.5.4
[0.5.3]: https://github.com/spidy666/DSRtracker/compare/0.5.2...0.5.3
[0.5.2]: https://github.com/spidy666/DSRtracker/compare/0.5.1...0.5.2
[0.5.1]: https://github.com/spidy666/DSRtracker/compare/0.5.0...0.5.1
[0.5.0]: https://github.com/spidy666/DSRtracker/compare/0.3.0...0.5.0
[0.3.0]: https://github.com/spidy666/DSRtracker/compare/v0.2.0...0.3.0
[0.2.0]: https://github.com/spidy666/DSRtracker/releases/tag/v0.2.0
