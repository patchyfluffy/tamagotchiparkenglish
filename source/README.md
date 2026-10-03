# Tamagotchi Park: English Translation Project

A fan project to translate **Sega Saturn de Hakken!! Tamagotchi Park** (Sega Saturn, Japan, 1998, T-13325G) into English.

**Goal:** everything the player needs to read is in English. Decorative Japanese painted into background art can stay.

## Where things are

| Folder / file | What's in it |
|---|---|
| `notes\translations.tsv` | **The translation sheet.** Japanese line, English line. Edit this, then rebuild. |
| `notes\code-patches.txt` | Small changes to the game's code (TCHI name ending, ABC-first name entry, top bar layout, season names). Commented; each can be switched off with `#` |
| `notes\script-0EXEC.tsv` | Every line of the game's font text, with offsets, slot sizes and pointers (made by `strings.exe`) |
| `notes\findings.md` | How the game works inside: compression, colors, font, text encoding, drawing code |
| `notes\text-inventory.md` | Screens: what's done, what's left, what stays Japanese |
| `tools\` | The programs written for this project, plus their source code (`.cs` files) |
| `work\` | Files pulled out of the game, edits and previews. **Safe to delete**: `extract-all.ps1` remakes it |
| `build\` | Patched copies of the game, ready to load in an emulator |

The original game files are in `Downloads\Sega-Saturn-de-Hakken-Tamagotchi-Park_SEGA-Saturn_JA` and are never changed.

## Making a build

In PowerShell, from this folder:

```powershell
& ".\tools\extract-all.ps1"     # once, or after deleting work\
& ".\tools\build.ps1"           # applies translations.tsv and writes build\Tamagotchi Park (English test)
```

If the emulator still has the last build open, Windows won't let it be replaced. Close the game in the emulator, or build under a new name: `& ".\tools\build.ps1" -Name "Tamagotchi Park (English test 3)"`.

## Saving (in Ymir or on a real Saturn)

The game can't create its own save file. It shipped with a backup cartridge that already held a blank one
(33,988 zero bytes named `TAMAGOTCHI_`), and without that file it warns at boot and won't save.
`save\TAMAGOTCHI_.BUP` is that blank file, rebuilt here (a 64-byte "Vmem" header, then zeros; no game data).
Easiest in Ymir: System menu → Cartridge → **Insert backup RAM...** → pick `save\tamagotchi-cartridge.bin`.
That is a blank 4 Mbit cartridge image (Ymir's normal, non-interleaved format: block 0 "BackUpRam Format",
512-byte blocks) that already holds `TAMAGOTCHI_` in blocks 2-69. Your saves then go into that file, so keep it.
Alternative: insert any Backup RAM cartridge and import `TAMAGOTCHI_.BUP` with the Backup Memory Manager.
Either way it needs a cartridge: the save is a little bigger than the Saturn's built-in memory.

## The tools

| Tool | What it does |
|---|---|
| `extract-all.ps1` | Pulls everything off the disc. Start here. |
| `build.ps1` | Builds the patched disc: text from the sheet, plus any edited pictures in its `$pictures` list |
| `textpatch.exe` | Writes the sheet's English into `0EXEC.BIN`, moving lines into freed space and fixing the pointers |
| `strings.exe` | Lists all of the game's font text (makes `notes\script-0EXEC.tsv`) |
| `relsearch.exe` | Finds text by the font's letter order (how the text was found) |
| `bz.exe` | Unpacks and packs the game's "BZ.w" compressed files |
| `discpatch.exe` | Writes data into a file inside the disc image and fixes sector error-correction codes. `selftest` checks a whole disc. |
| `gfx.exe`, `glyphs.exe` | Draw raw picture data or font characters as PNGs |
| `optedit.exe` | Repaints the options screen's background word (works, but currently unused) |
| `isolist.ps1`, `sh2dis.exe` | List/copy disc files; disassemble the SH-2 program |
| `palfind.exe`, `optdiag.exe`, `sjisscan.ps1`, `patsearch.exe` | Earlier investigation helpers |

The `.exe` tools are built from their `.cs` files with the C# compiler that comes with Windows:
`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /optimize /r:System.Drawing.dll /out:tools\NAME.exe tools\NAME.cs`
(`textpatch` also needs `tools\strings.cs` and `/main:TextPatch`.)

## Status (2 October 2026)

- [x] Read the disc, crack the compression, write a repacker (all 43 blocks round-trip)
- [x] Patch the disc safely (sector error-correction codes recomputed and checked)
- [x] Proof of concept: repainted the options background (works in Ymir; now switched off by choice)
- [x] Found the game's font and text encoding; listed all 447 lines of font text
- [x] Options menu translated in the sheet (`build\Tamagotchi Park (English test 2)`), checked in Ymir: all good
- [x] Boot warning screen translated, checked in Ymir (test 3)
- [x] 134 lines of menus and messages translated (`build\Tamagotchi Park (English test 4)`), every pointer verified
- [x] Test 4 checked in Ymir; feedback fixed in test 5: name entry buttons, 4-letter stat labels, "TCHI" after every name, name entry starts in ABC mode, top bar day/time/season in English
- [x] Overnight: character names (58), title screen menu, save/load screen day/hour (`build\Tamagotchi Park (English test 6)`, 200 lines + 18 code changes, all verified)
- [x] Test 6 checked: title menu and names OK; names ran past boxes → test 7 uses a squeezed TCHI (`[]`, 2 letter spaces)
- [x] Test 7/8 checked: squeezed TCHI works ("better than not working"); MOOD UP GAME text OK in test 8
- [x] Games, contests, history (test 9): all 8 overlay files translated (`notes\overlays\*.tsv`, 222 lines + 15 instruction/control boxes + 4 code changes); every moved pointer decoded and checked on the built disc. Also in test 9: new boot warning wording, LEFT/RIGHT arrows swapped, redrawn 9
- [x] Test 9 checked (3 Oct): 9 good; history title too long; stray の in the lunch contest intro; speed box too narrow (trailing spaces lost); P too close to D on the panels; lunch contest hard to understand
- [x] Test 10b: history titles ORIGINAL / NEW SPECIES; の removed (lunch + race intros); speed box fixed; P redrawn; lunch contest instructions reworded, title CLEAN PLATE LUNCH!, places shown as NO.1-NO.4 (code change); panel contest renamed SUPER PANEL BANG!
- [x] Test 10b checked: lunch contest wording back to the test 9 version, PANEL BANG BANG! kept; asked for 12-hour clock, MENU, fuller names, a fuller GRAVES box, and how to save
- [x] Test 11: clock reads "11 AM" / "12 PM" (code group G); pet info panels 112 -> 124 px (group H) so names can be 9 letter spaces (KUCHIPA[], GINJIRO[], TARAKO[], TONMARU[], TONGARI[], HASHIZO[], FURIKI[], OPAOPA[]); GRAVEYARD; MENU; save comment "TAMAGOTCHI" (10 letters max); blank save file in `save\`
- [x] Test 11 checked: saving works with `save\tamagotchi-cartridge.bin` (a save state doesn't count as a game save)
- [x] Test 12: save screen seasons SPR/RNY/SUM/FAL/WIN (code group I), empty slots say EMPTY
- [x] Test 12 checked: save screen works
- [x] Test 13: options labels SPEED / SOUND / EXIT (code group J: they're one-letter texts in waving-letter lists, not the 0x31720 lines); egg room bubble "CHOOSE AN EGG!" (`notes\pictures\TAMA-27700-egg-text.txt`, a text grid of colour numbers written raw into TAMA.BIN by build.ps1)
- [x] Test 13 checked: options labels good; bubble OK but the user prefers the game's own font
- [x] Test 14: bubble redrawn with the game's bold font (centred, brown); EGG ROOM bar centred (code group K, x 121 -> 108)
- [x] Test 14 checked: bubble and bar OK
- [x] Test 15: one-line name labels (places etc.) now a box sized to the name with the name centred (code group L, 0x0601BE64 calls 0x0601BAE4); grave card: stat labels fit before the bars, names 4 px further from the border, seasons SPR/RNY/SUM/FAL/WIN, age unit 才 -> YR (`notes\pictures\USER-12480-age-unit.txt`; g stays)
- [x] Test 15 checked: grave card OK; found a garbled season on a save slot (second slot layout, used for a save with a dead pet, still read the old 3-byte table)
- [x] Test 16: second save-slot season spot patched (group I, 0x20662); grave card contest results NO.1 / NO.n / OUT / --- (place digit moved to the 4th letter, group L 0x0C792)
- [x] Test 16 checked; test 17: save-slot hour moved 4 px right (gap after the season)
- [x] Test 18 (+ UNLOCKED): character viewer texts moved out of 0x315B0 (textpatch note `keepold`) and translated, 30 animation names, SOUND TEST title; UNLOCKED build's cursor no longer skips ENDING
- [x] Test 18 UNLOCKED checked: ENDING works, credits OK
- [x] Test 19 (+ UNLOCKED): OBAKETCHI 2 (two name slots); credits with full names (companies BANDAI / SEGA, GRAPHICS / MUSIC, two error messages blanked; 1 byte left in HISMINI)
- [x] Test 19 checked: credits with full names look good
- [x] Test 21 checked: save screen OK; FITNESS UP, rocket, BURY/RESTS HERE, MINI EGG HOUSE, SHOT/PILL, sandbox/toilet/slide, grave OUT all fine
- [x] Test 23 (+ UNLOCKED): GUTS UP game title SHOOTING STAR!; angel info panel unit 日め -> DAYS (`notes\pictures\USER-11E00/11E80-days-*.txt`, two overlapping 16x16 sprites, frame 0x49). The TP icon (pattern 0x41) is already Latin letters. Note: UNLOCKED builds now include the angel-menu patch too.
- [x] Test builds can also give every pet the angels' care menu (PRAISE, GUTS UP GAME): 0x0A81A in test-unlock-patches.txt ("English test 22 UNLOCKED ANGEL MENU"). Pets: 8 objects x 72 bytes at 0x06045E1C, type word at +0 (3 = angel), data pointer at +56; a real angel in a save would need the save's pet records decoded.
- [x] Test 22 (+ UNLOCKED): panel games' letters made unique across all sets (PARK! / 12345 / JUMBLE): shared A/E/P caused false matches, since the game matches by panel number. GUTS UP GAME is in the angels' care menu (with PRAISE)
- [x] Test 21 (+ UNLOCKED): save screen has FOUR slot layouts; the last two still read the old season table (garbled RNY/SUM) and now use the new one (group I 0x2081E/0x209DE, hours 0x20878/0x20A38); rocket ("return to the stars") lines cut to 9 letters per line (box beside the rocket)
- [x] Park stage found: byte 0x06045E06 (state + 18), 0/1/2, pet limits 3/5/8 at 0x06042DB4; grows at a season change when the park is full (0x06022D56). Test builds (`-Unlock`) skip the "full" check: "English test 20 UNLOCKED"
- [x] Remaining untranslated 0EXEC text reviewed (3 Oct): only debug/developer text (stat readout 0x30FD4-0x31034, debug menus 0x31330-0x313A8, error asserts, だみー entries, "すて" screen), code fragments, the now-unused old season lists, and かな/カナ (kept by choice)
- [ ] Optional: ending picture おわり ("THE END"), a full-screen painting
- [x] Hidden title options (ENDING/CHARACTER/SOUNDTEST) normally need A+B+C+X+Y+Z+L+R held at boot. Test builds can force them on: `build.ps1 -Unlock` applies `notes\test-unlock-patches.txt` (built: "English test 17 UNLOCKED")
- [ ] Credits: shown via the title menu's ENDING option, which appears when bit 0 of the save's unlock flags (0x06045DF3 -> 0x06049B50) is set; CHARACTER/SOUNDTEST need 0x06049B51. What sets them in play isn't found yet. A test build could force ENDING on (title menu code at 0x060223DC and 0x06022554).
- [ ] Still Japanese: history book name plates (pictures; e.g. ミカチュー, ばんぞーはかせ), title logo (optional)
- [ ] Egg room bubble + options white labels (pictures). Leads, 3 Oct: sprite sets have a header [u32 0x10][u32 frames][u32 pattern-table offset][u32 patterns], patterns = [u32 data offset][u16 w][u16 h], 4bpp. `TAMA.BIN` @0x29000 has banner-sized patterns 0x0C/0x0D (224x24) and 0x14 (192x24): likely the egg room bubble; TAMA.BIN is loaded at 0x06020EF8 (chunks via 0x6011948, r5 = offset>>8 / length). USER.BIN sprites (data 0xE000, table 0x12800) are icons only; options patterns 0x2A-0x2F are SPEED/speaker icons, no Japanese. Need a screenshot of which options screen has the white Japanese labels.
- [ ] Options menu labels (pictures), egg room bubble (picture), 才/g, title logo (optional)
- [ ] Save-screen seasons, contest place, status words (see `notes\text-inventory.md`)
- [ ] Decide on the font's P (redraw ready in `notes\font-fixes.txt`, currently off)
- [ ] Translate the rest of `script-0EXEC.tsv`, screen by screen
- [ ] Check other screens for Japanese that matters in pictures
- [x] Release v1.0 (3 Oct 2026): `build\Tamagotchi Park (English v1.0)` (same content as test 23, normal build) and `release\`: IPS patch for Track 1 (made and self-checked by `tools\ipsmake.exe`), player README.txt with file hashes, fresh blank save files. Remake the patch after any change: build, then `ipsmake <original Track 1> <new Track 1> <out.ips>`.
