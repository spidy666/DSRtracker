🇩🇪 [Deutsch](ROADMAP.md) | 🇬🇧 **English**

> This is a translation of the German document. If the two differ, the German version applies.

# Road to version 1.0

Version 1.0 means: **a complete run went through with the tracker without errors.** Until then the project stays at 0.x. The feature set is complete; only the practical test in the real game is still open.

## Open tests

Tested with the exe, Dark Souls Remastered 1.03.1 (Steam):

- [ ] **Complete run** with all 84 enemies of the list
- [ ] **Automatic detection** of all 72 enemies with a flag, especially the minibosses (e.g. Black Knights, Crystal Golems) and the invaders (e.g. Kirk, Leeroy)
- [ ] **Manual enemies:** the 12 respawning enemies can be checked off cleanly (also via the quick bar)
- [ ] **Area detection** in all 26 areas, especially at the transitions between areas
- [ ] **Overlay during the run:** notifications, "Rotating areas" jumps to the area and holds, gap bar and skipped enemies are correct
- [ ] **Interruptions:** restart the game or the tracker in between, reload the save – the state stays correct

If something comes up: a screenshot (for area detection with the position from the control page) and a short description as an [issue](../../issues).

## Already tested

- Kill detection and notification in the real game (Asylum Demon)
- "Rotating areas": jump to the area and hold after the kill, even if this completes the area
- Game version detection (1.03.1 Steam)
- `update.bat`: download, checksum, attestation, replacing the exe
- Web version: OBS dock and browser source share the state; checking off, run clock and overlay in real OBS

## Ideas for later (not needed for 1.0)

- Remote control of the web version by a mod from another PC
- Control page and setup wizard in English (the overlays already support it)
- Automatic tests on GitHub (calculation logic and browser tests of the pages)
