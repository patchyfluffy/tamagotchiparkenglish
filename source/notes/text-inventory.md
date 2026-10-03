# Text inventory

Every picture with Japanese on it, with a draft English version. "Block" is the file name in `work\unpacked` and `work\img`.

**This list is incomplete.** So far it only covers the blocks that came out as clean pictures. See "Open questions" in `findings.md`.

**Font text** (menus, messages) is translated in `translations.tsv`, the sheet the build reads. The full list of the game's font text is `script-0EXEC.tsv`. This file tracks screens and pictures.

## Done (font text, in translations.tsv)

| Screen | What | Status |
|---|---|---|
| Options menu | Title, SPEED / SOUND / EXIT, speed values (SLOW…ULTRA FAST), MONO / STEREO | Checked in Ymir (test build 2): looks good |
| Boot warning (save problems) | NO TAMAGOTCHI MEMORY / NO TAMAGOTCHI DATA / SAVE DATA IS DAMAGED, plus the 4-line explanation | Checked in Ymir (test build 3): looks good |
| Care menu, food, games, contests, stats, save/load, graves, save errors, release dialogue, park places | 134 lines | Test 4 checked in Ymir; layout fixes in test 5 |
| Status panel labels | FOOD / MOOD / DISC / IQ / BODY (4-letter column) | Test 5+ |
| Name entry | Starts in ABC mode; かな/カナ buttons kept Japanese (2-letter slots); TCHI fits in the box | Test 5+ (code changes B, C) |
| Pet names everywhere | End in TCHI instead of っち | Test 5+ (code change A) |
| Top bar | 9 AM / 12 PM (12-hour, test 11, code change G), SPRING/RAINY/SUMMER/FALL/WINTER, DAY 1 | Test 5+ (code changes D, F) |
| Character names | 58 names, e.g. BABYTCHI, MAMETCHI, KUCHIPA, plus Saturn-only Sega joke characters and angels | Test 6 |
| Save/load screen day and hour | DAY 1, 9H | Test 6 (code change E) |
| Title screen menu | NEW GAME, CONTINUE, OPTIONS (+ unlockable ENDING, CHARACTER, SOUNDTEST) | Test 6. Probably the "main menu" from test 4 feedback, to confirm |
| MOOD UP GAME (MINI00) | LOOK THIS WAY!, instructions, < LEFT / RIGHT > | Test 8 checked; arrows swapped in test 9 |
| IQ UP GAME (MINI01) | PANEL BANG!, panels A-E / PANELS, TRIES LEFT box (widened), results | Test 9 |
| FITNESS UP GAME (MINI02) | JUMPING HURDLES!, TARGET / TIME / SEC, controls, results | Test 9 |
| GUTS UP GAME (MINI03) | CRUISING STAR!, GUTS, controls, PERFECT! | Test 9 |
| IQ CONTEST (CONTIN) | PANEL BANG BANG!, RANK, panels PARK! / A-E / PANELS, results | Test 9 |
| MANNERS CONTEST (CONTLC) | CLEAN PLATE!, intro lines, LUNCH MENU, 15 rival names, ENTRY NO., results | Test 9 |
| FITNESS CONTEST (CONTST) | READY, SET, RACE!, ON YOUR MARKS / SET / GO!!, ? LANE, 23 rival names, SPD / STAM, results | Test 9 |
| History book (HISMINI) | Series page titles, staff credits (first names as initials: full names don't fit) | Test 9 |

## Not done yet

| What | Why it's tricky |
|---|---|
| Egg room thought bubble | Need a screenshot to find it (probably a picture) |
| Age and weight units in the status box (`0才  5g`) | Not font text: likely small sprites drawn with the numbers. Needs picture editing |
| Save/load screen seasons (はる/つゆ… at 0x3184B) | 2 letters max, and the code has no spare room to point at the new season table |
| Contest place (` ばん`, "No. 3") | Assembled byte by byte into a buffer (code at 0x0601C71C); needs a closer look |
| Status words at 0x30FD4–0x31034 (じゅみょう, びょうき, ふとん, おいのり…) | Might be a debug readout or the history hall's causes of death; need to see where they show up |
| おばけっち2号 (two entries) | Names a ghost "No. 2"; uses the bold font's 号 symbol. Skipped for now |
| Debug/test screens | Players never see them; skipped on purpose (some debug text space now holds new code) |
| History book name plates (ミカチュー, ばんぞーはかせ…) | Part of the page pictures in HISMINI, not font text |
| Species names over 9 letter spaces (KUCHITAMATCHI, SHIROBABYTCHI, HASHITAMATCHI, SEKITORITCHI…) | The name field holds 9 letters and the (widened, test 11) info panel 9 letter spaces; these keep short forms |

## Decided: leave in Japanese (decorative art)

| Block | Screen | Japanese | Notes |
|---|---|---|---|
| `USER.BIN@014000` | Options screen background | おぷしょん (repeating pattern) and the two speech bubbles | A repaint to "OPTIONS" works (`optedit.exe`, switched off in `build.ps1`), but it looked a little off, and decorative art can stay Japanese |

## Needs translating

| Where | Screen | Japanese | Draft English | Notes |
|---|---|---|---|---|
| `0EXEC.BIN` 0x3123C… (font text) | Unknown menu with じかんのながれ, ちゅうだん ("pause"), speeds | | | Find which screen this is |
| `USER.BIN@014000` | Options screen | ラブリーアーップ (left speech bubble) | Lovely Up! | Character catchphrase. Check the wording against an emulator screenshot |
| `USER.BIN@014000` | Options screen | Right speech bubble, katakana | ? | Too small to read at this size. Need a closer look |
| `MINIGAME.BIN@007800` | Race minigame track | すたーと (vertical, left) | START | Written vertically; English may need to be stacked letters |
| `MINIGAME.BIN@007800` | Race minigame track | ごーる (vertical, right) | GOAL | Same |
| `GRPH.BIN@000000` | Room with a TV | Handwritten-style message on the TV screen | ? | Several lines. Need a closer look |
| `GRPH.BIN@007800` | Park / main screen | ひろよっち (name label) | Hiroyotchi? | Could be a character name or a pet name. Check in game |

## Already English or no text (nothing to do)

| Block | Screen |
|---|---|
| `NOWLOAD.BIN@019800` | "Now Loading..." screen |
| `MINIGAME.BIN@000000` | "Go Go" screen |
| `MINIGAME.BIN@050000` | Numbers and stars |
| `MINIGAME.BIN@029000`, `@03D800`, `@048800` | Backgrounds, no text |
| `USER.BIN@01F800` | BANDAI logo |
| `ROKE.BIN@00A000` | Classroom scene, blackboard is blank |
| `ROKE.BIN@011000` | Blank / black |
