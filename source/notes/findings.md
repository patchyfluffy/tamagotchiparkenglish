# Technical findings

## The disc

- Two tracks: Track 1 is the data (MODE1/2352, about 7.8 MB). Track 2 is a short audio track.
- Disc header: `SEGA SEGASATURN`, product `T-13325G`, version `V1.002`, dated `19971205`, title `TAMAGOTCH PARK`.
- 27 files. `0EXEC.BIN` is the main program. Almost everything else is in `DATA\`.
- `DSK_ABST.TXT` says the game is a "Tamagotchi-style game", and `DSK_BIBL.TXT` names Tamagotchi and Tenshitchi (the angel Tamagotchi).
- File names hint at what's inside: `BOKUZYOU` (牧場, ranch), `OHAKA` (お墓, graves), `HISTORY`, `MINIGAME`, `FOOD`, `GOTTI` and `TAMA` (character sprites), `NOWLOAD` (loading screen), `USER` (options screen).

## The main program

- `0EXEC.BIN` loads at address `0x06010000` (from the disc header's "1st read address"). The CPU is the Saturn's Hitachi SH-2, big-endian.
- To find code in `work\exec.asm`: file offset = address − `0x06010000`.

## The "BZ.w" compression

The unpacking routine is at **`0x06011468`**. The header check is at `0x060113B0`.

**Block layout:**

| Bytes | Meaning |
|---|---|
| 0–3 | `42 5A 2E 77` = `"BZ.w"` |
| 4–5 | Unknown, always `01 FF` so far |
| 6–7 | Mode. Always `00 01`; the game rejects anything ≥ `0x100` |
| 8– | Commands, each a 16-bit big-endian word |

**Commands.** All counts are in 2-byte words. `n` = the low 13 bits.

| Top bits | Meaning |
|---|---|
| `1LL` | Copy `LL+2` words from `n` words back in the output. Any following words that start with `011` keep copying from the same place for `n` more words. |
| `01x` | Repeat the next word `n` times (bit `x` is ignored) |
| `001` | Copy the next `n` words exactly as they are |
| `000` | End. If bit `0x1000` is set, drop the last output byte (odd-length output) |

**Where the blocks are:** 43 blocks in total. Some files are a single block (`*.BZ`, `HISTORY.BIN`). Others hold blocks inside larger files at 2 KB-aligned offsets, e.g. `BOKUZYOU.BIN` has 15. See `work\unpacked-blocks.txt` for the full list with offsets and sizes.

**Unpacked sizes say what's inside:**
- 71,680 bytes = 320×224 pixels at 1 byte per pixel, a full Saturn screen
- 131,072 bytes = 512×256, a sprite or background sheet
- 128,000 bytes (`HISTORY.BIN`) = maybe 320×400; it doesn't look right at that size yet
- 81,920 bytes = program code (`MINI0x.BZ`, `CONT*.BZ`, `HISMINI.BZ` all start with SH-2 code)

**Repacking:** `bz.exe pack` uses an optimal parse (a dynamic program over literal runs, repeats and back-references). Every block on the disc round-trips byte-for-byte, and the total comes out about 12% smaller than the game's own packer (1,305,562 vs 1,488,314 bytes). So an edited picture normally fits back in its original slot without moving anything.

Watch out: a back-reference may be followed by "continuation" words starting with `011`, so a repeat command right after a back-reference must use `010` (bit 13 clear). Our packer always does.

## Colors (palettes)

- Colors are Saturn RGB555: big-endian 16-bit `0bbbbbgggggrrrrr`. Palettes seen so far are sorted brightest to darkest, with index 0 a magenta "transparent" key that pictures don't use.
- **Options screen** (`USER.BIN@014000`): palette at `USER.BIN` offset `0x1F000` (512 bytes). It sits in the gap between the options picture and the BANDAI logo block at `0x1F800`.
- **Boot / title background** (`TITLE.BIN`): an uncompressed 320×224 picture (71,680 bytes) followed by its palette at offset `0x11800`.
- Other screens probably follow the same pattern: palette right after the picture data. `palfind.exe` wasn't precise enough to find them blind.

## The options screen in detail

- A 4×3 checkerboard (column edges at x = 80/160/240, row edges at y = 76/156) with a radial glow centered at (159, 112). Green and brown squares alternate.
- Each pixel's background color depends only on its square color and distance from the center. That's how `optedit` predicts the background under the old word.
- The word is stamped 12 times: rows y = 27–47, 107–127, 182–202, one per column. The middle row is partly hidden behind the two characters.
- The word's tint is roughly "R and G boosted, B halved". `optedit` learns it separately for the green and brown squares.

## Writing to the disc

- Track 1 is raw 2352-byte Mode 1 sectors. Each has a 4-byte EDC checksum and 276 bytes of ECC (error correction). `discpatch.exe` recomputes both for every sector it touches. `selftest` confirms the math matches all 3,323 sectors of the original disc.
- So far every patch fits in its original slot, so no files move and the filesystem doesn't need rebuilding.

## Where the text is

There are two kinds of Japanese in this game:

1. **Painted into pictures**: decorative words in background art (e.g. the options screen's "おぷしょん" pattern, speech bubbles). Per the project's priorities, these stay Japanese unless they matter for playing.
2. **Real text drawn with the game's font**: menus, settings, messages (e.g. the boot warning). This is most of what needs translating, and it's the easy kind: swap the text, no image editing.

### The font (USER.BIN)

- `USER.BIN` 0x0000: 192 characters, each 16×12 pixels, 4 bits per pixel, single color (96 bytes per character).
- `USER.BIN` 0x7000: a bold copy of the same font. The game draws the bold copy in black, then the normal one in white on top, which gives the outlined look.
- Order: 0x00–0x3F = ASCII 0x20–0x5F (space, punctuation, digits, **A–Z capitals only**), 0x40–0x7F = katakana, 0x80–0xBF = hiragana (both in JIS X 0201 half-width order: 。「」、・ヲァィ…ワン゛゜).
- Between the fonts (0x4800–0x7000, 0xB800–0xE000) is more single-color data, not yet identified.
- Look at it with `glyphs.exe work\disc\DATA\USER.BIN 0 192 16 12 16 out.png 2`.

### The text encoding

- One byte per letter. `0x20–0x5F` = ASCII (so English capitals are written as-is). `0xA1–0xDF` = kana in JIS X 0201 order.
- `@` (0x40) switches between hiragana and katakana. Katakana words are written `@…@` (e.g. `@すてれお@` = ステレオ).
- Voiced kana are two bytes: the letter, then ゛ (0xDE) or ゜ (0xDF). `0x00` ends a line, and lines are padded with `0xFF` to a multiple of 4 bytes.
- Standard Shift-JIS searches miss all of it, because these are the codes for half-width katakana.

### Where it lives

- In the main program `0EXEC.BIN`, mostly around offsets 0x30000–0x33000. `strings.exe` lists all 447 lines with their slot sizes and pointers in `notes\script-0EXEC.tsv`.
- The program finds lines by address (0x06010000 + offset), stored in pointer tables or code literal pools. 179 lines have a direct pointer. Those can be moved to make room for longer English, and `textpatch.exe` does this automatically.
- Options menu: title `おぷしょん` at 0x3167C (pointer in the code at 0x1B254), items and values at 0x316E4–0x3173C (pointer table at 0x34394, 11 entries).
- A second similar block at 0x3123C (じかんのながれ, ちゅうだん, speeds; pointers at 0x32154 in 12-byte records) belongs to some other menu, not identified yet.
- Boot warning ("Tamagotchi Memory not inserted") lines: around 0x318xx.

### How the game draws text

- `0x06014EDA` draws a line: it reads one byte at a time (so lines don't need any alignment), handles `@`, and advances **13 pixels per letter** (12 in one mode).
- `0x0601B9F4` measures a line. `0x0601BA24` draws text centered on a point. `0x0601BA76`/`0x0601BAE4`/`0x0601BAF6` draw a yellow box sized to the text, then the text centered in it. So boxes grow to fit longer English automatically.
- The options menu's item labels start at a fixed left edge, so long labels run into whatever is to their right (the SPEED icon).
- Some screens (e.g. the boot warning) draw with **only the bold font, in a dark color**. In the bold font, `# $ % '` hold different symbols (and `@` is the katakana switch), so avoid those characters.
- The font's P has a very tall loop and a 1-pixel stem, so it looks a lot like D, especially in bold. Kept as-is for now. An optional redraw is in `notes\font-fixes.txt` (`fontpatch.exe`).
- `textpreview.exe` draws text with the game's font at real screen positions, to check fit before building.

### Fixed-size text tables (translated in place with `room=N`)

- Title menu (0x32E48): 10-byte entries, drawn at x=130 by 0x06022514. Shows CONTINUE only with a save; ENDING / CHARACTER / SOUNDTEST appear after unlock flags (0x06049B50, 0x06049B51).
- Character data records start with a **10-byte name field** (9 letters max): 44-byte records at 0x337F8 and 0x33B10, 40-byte angel records at 0x33EC4, and a plain 10-byte name list at 0x33DA4.
- Park place names (0x341DC): 10-byte entries.
- Top bar templates (0x31610 "DAY     ", 0x31619 "AM    ") and save-screen templates (0x31844, 0x3185A) are copied into fixed buffers, so their lengths must not change. The top bar copies the day line with 4-byte reads, so it must stay put (4-byte aligned).
- In the bold font, `^` is the kanji 号 ("No."), as in おばけっち2号.

### Code changes (`notes\code-patches.txt`)

- **A. TCHI:** the name builder at 0x0602AA84 appends っち when called with r5≠0. It now jumps to a new routine at 0x060415B0 that writes T,C,H,I. That space held debug-only text, used only by 0x060229F0–0x06022A3C.
- **B. ABC first:** the name entry mode byte is at 0x06082AA4 (0 hiragana, 1 katakana, 2 English). Setup at 0x0602ABB4 now stores 2, and the caller's reset (0x06029B3E) is removed.
- **C. Name entry layout:** name and cursor x 124→114, ending x 202→192.
- **D/F. Top bar** (code 0x06022DEC): day digits move to positions 4–6, and seasons come from a new 8-byte-per-entry table at 0x060415D0, drawn at x=120.
- **E. Save/load screen** (code 0x060302DC): day digits move to positions 3–5.

### Game, contest and history overlays (MINI00-03.BZ, CONTIN/CONTLC/CONTST.BZ, HISMINI.BZ)

- Each is BZ.w-compressed code+data, 81,920 bytes unpacked, loaded at **0x00280000** (the Saturn's low work RAM). Their text uses the same encoding, and pointers to it look like 0x0028xxxx.
- List their lines: `strings.exe work\unpacked\MINI00.BZ@000000.unp notes\script-MINI00.tsv 0 00280000 ptr` (`ptr` = only lines the code points to; the text is chopped into small pieces).
- Instruction boxes are a little **layout script**: 12-byte entries `[style:2][?:2][x:2][y:2][text pointer:4]`, ending with style FFFF.
  - Style `08cc` starts a new line in color `cc` (03 red, 04 blue, 05 green).
  - `0000` starts a new line in the default color.
  - `C000` continues on the same line in the default color; `C8cc` continues in color `cc`.
  - x/y are pixel offsets inside the box: lines are 13 px apart, x centers each line in multiples of 13. The lunch contest's box is about 10 letters wide.
  - Because it's data, English lines can be laid out freely by editing the entries. Example: CONTLC's instructions at 0x02188.

### Options menu labels

- The white labels (じかんのながれ / さうんどせってい / おしまい) ARE font text, but drawn one letter at a time so the letters can bob up and down (0x0602B3E8). Each label is a zero-ended list of pointers to one-letter texts (list table at 0x3435C; lists at 0x34304/0x34324/0x34348; letters in 4-byte slots at 0x316A0-0x316E3). The whole-word copies at 0x31720/0x3172C/0x31738 aren't used by this screen. Translated in code-patches.txt group J.
- The sprite numbers 0x24-0x29 in the data at 0x34384 are the six SPEED icons (USER.BIN sprite frames -> patterns 0x2A-0x2F); 0x2A/0x2B are the mono/stereo speakers. No Japanese in them.

### Sprite sets (VDP1 pictures)

- A sprite set starts with [u32 0x10][u32 frame count][u32 pattern-table offset][u32 pattern count], then the frame offsets. A frame = [u32 piece count] then pieces [u16 pattern][u16 flags][s16 x][s16 y]. A pattern = [u32 offset into the pixel data][u16 width][u16 height], 4 bits per pixel.
- The pixel data is loaded separately: the loaders call 0x06011948 with r5 = (file offset >> 8) << 16 | length. E.g. TAMA.BIN: table at 0x29000, pixels at 0x24000 (0x4200 bytes), palette at 0x23800.
- Egg room bubble text = TAMA.BIN pattern 0x14 (192x24, pixels at 0x27700). Colours: F white, 9 dark brown, A soft edge. Replaced via notes\pictures\TAMA-27700-egg-text.txt.

### The boot warning (0x06030F70)

- Top box: one of three lines (memory not inserted / data doesn't exist / data damaged), centered at (160,64) in a box that sizes itself.
- Bottom box: fixed at x 20–300, y 93–168. Four lines left-aligned at x=24, y = 100/116/132/148, so **at most 21 letters per line**.
- For a damaged save, only the first two bottom lines are shown (at y 116/132), so they have to make sense alone.

## Open questions

- **The boot warning screen** (shown when no "Tamagotchi Memory" save cartridge is inserted). Its background is `TITLE.BIN`, and its text is real font text in `0EXEC.BIN` around 0x318xx. Translatable with the sheet, but it's long, so check how the box wraps lines first.
- **Lowercase letters**: the font only has capitals. If all-caps reads badly, lowercase could be drawn into unused font slots later.
- **Colors for the other screens** (see "Colors" above).
- **Some blocks don't render as clean pictures** at 8 bits per pixel (`GRPH.BIN@014800`, `NOWLOAD.BIN@056000`, `OHAKA.BIN@001800`, `BOKUZYOU.BIN@13A000`, `HISTORY.BIN`). They're probably 4-bit or 16-bit pixels, or a different width.
- **Uncompressed files not checked yet:** `TITLE.BIN`, `TAMA.BIN`, `GOTTI.BIN`, `FOOD.BIN`, `KOUKA.BIN`, and the uncompressed parts of `BOKUZYOU`, `USER`, `OHAKA`, `ROKE` and `NOWLOAD`.
- **Name entry:** if the game lets you name your pet, there must be a font somewhere. Not found yet.
