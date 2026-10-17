🇩🇪 [Deutsch](ROADMAP.md) | 🇬🇧 **English**

> This is a translation of the German document. If the two differ, the German version applies.

# Road to version 1.0

Version 1.0 means: **a complete run went through with the tracker without errors.** Until then the project stays at 0.x. The feature set is complete; only the practical test in the real game is still open.

## Open tests

Tested with the exe, Dark Souls Remastered 1.03.1 (Steam):

- [ ] **Complete run** with all 84 enemies of the list
- [ ] **Automatic detection** of all 72 enemies with a flag, especially the minibosses (e.g. Black Knights, Crystal Golems) and the invaders (e.g. Kirk, Leeroy). Status: 11 of 72 confirmed (see below)
- [ ] **Manual enemies:** the 12 respawning enemies can be checked off cleanly (also via the quick bar)
- [ ] **Area detection** in all 26 areas, especially at the transitions between areas. Status: 7 of 26 areas confirmed (see below)
- [ ] **Overlay during the run:** notifications, "Rotating areas" jumps to the area and holds, gap bar and skipped enemies are correct
- [ ] **Interruptions:** restart the game or the tracker in between, reload the save – the state stays correct

If something comes up: a screenshot (for area detection with the position from the control page) and a short description as an [issue](../../issues).

## Tools for testing

If you don't want to beat every boss the regular way, you can use [DSR-Gadget](https://github.com/JKAnderson/DSR-Gadget) for testing. It is a freely available test tool for Dark Souls Remastered and not part of this project; it is no longer developed but works with 1.03.1. Useful:

- **Player → Bonfire + Warp:** jump straight to any bonfire, e.g. for area detection
- **Player → Store / Restore:** remember a position and jump back
- **Cheats → No Death, Exterminate, Infinite Stamina** and **Enemies → Disable AI:** beat bosses quickly

Important:
- **Play offline only.** The Gadget writes to game memory; online this can lead to a ban.
- **Don't set event flags with the Gadget** (Misc tab) and **don't turn on "Player + Enemies → No Death"** – otherwise bosses don't die the regular way and the test says nothing about detection.
- **Back up your save first** (`Documents\NBGI\DARK SOULS REMASTERED\<number>\DRAKS0005.sl2`), ideally use a separate test character.
- The tracker itself still only reads; the Gadget is a separate program.

## Already tested

- Kill detection and notification in the real game (Asylum Demon)
- Detected automatically (test on 2026-10-17): Asylum Demon, Taurus Demon, Havel, Fang Boar (Parish), Capra Demon, Gaping Dragon, Butcher 1 and 2, Seath, Manus, Gwyn
- Area detection correct: Northern Undead Asylum, Undead Burg, Undead Parish (including Lower Undead Burg and the Capra Demon arena), Depths, Crystal Cave, Chasm of the Abyss, Kiln of the First Flame; the tunnel between Lower Undead Burg and Firelink Shrine counts as the Shrine. Known minor issue: on the first steps behind the door from the Depths into Lower Undead Burg it still briefly shows "Depths"
- Splits with in-game time for every kill
- "Rotating areas": jump to the area and hold after the kill, even if this completes the area
- Game version detection (1.03.1 Steam)
- `update.bat`: download, checksum, attestation, replacing the exe
- Web version: OBS dock and browser source share the state; checking off, run clock and overlay in real OBS

## Ideas for later (not needed for 1.0)

- Remote control of the web version by a mod from another PC
- Control page and setup wizard in English (the overlays already support it)
- Automatic tests on GitHub (calculation logic and browser tests of the pages)
