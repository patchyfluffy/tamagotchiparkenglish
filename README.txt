Sega Saturn de Hakken!! Tamagotchi Park - English translation patch v1.1
=======================================================================

A fan translation of the Tamagotchi Sega Saturn game. Translation was made possible through the use of Opus 5.5, although I manually reviewed the text and played through to ensure it was functional and sensible. 
As far as I can tell, everything you need to read to play is in English: menus, care options, foods, messages, mini-games, contests, the save screens, the graveyard, the history book, the credits and the hidden test screens.

WHAT YOU NEED
- Your own copy of the game as a BIN/CUE disc image (two tracks). I recommend myabandonware. The patch is for Track 1:
    Sega Saturn de Hakken!! Tamagotchi Park (Japan) (Track 1).bin
    size 7,815,696 bytes, SHA-1 0D9244F0A62FB4C7B51F6CFC06FEC7F8841AAD05
  Track 2 (the audio track) is not changed.
- A patching tool (for example Floating IPS, or the web-based Rom Patcher JS).

HOW TO PATCH
1. Make a copy of your Track 1 .bin and apply ONE of the two patches to it (they make the same file):
   - "Tamagotchi Park English v1.1 (Track 1).bps" (recommended: the tool checks that you have the
     right original file before patching; original CRC32 3FEB672E, patched CRC32 F597BA54), or
   - "Tamagotchi Park English v1.1 (Track 1).ips" (for tools that only support IPS).
   The patched file's SHA-1 should be FA27C67DCE3A31BE79EA227AFB05A4597C076759.
2. Keep the .cue file next to the .bin files. If you renamed the patched .bin, edit the .cue so the
   Track 1 line uses the new file name.
3. Load the .cue in your emulator (tested in Ymir) or burn/copy it for real hardware.

SAVING (IMPORTANT)
The game can't create its own save file. It shipped with a memory cartridge that already held a blank
one, and without it the game warns at boot and won't save. Two ways to get that blank save:
- In Ymir: System menu > Cartridge > "Insert backup RAM..." and pick tamagotchi-cartridge.bin from
  the "blank save" folder (a blank 4 Mbit backup cartridge with the empty save already on it). Your saves then go
  into that file, so keep it, and pick the same file each time you play.
- Any emulator or tool that imports Saturn .BUP saves: import TAMAGOTCHI_.BUP onto a backup memory
  cartridge (not the console's internal memory: the save is slightly too big for it).
Both files contain only empty space, no game data. Save states are not game saves: use STOP in the
game to save.

NOTES
- I have only tested on Ymir and not real hardware, I'm hoping to do so soon
- This is again machine translated, if anyone who can speak Japanese is reading this or wants to make their own translation please do!!
- If you have any problems with this translation please contact me on Discord @elora and I'll try to fix!
- Names: Tamagotchi names end in a squeezed "TCHI" so they fit the game's name boxes. A few very long
  names use short forms (for example KTAMATCHI for Kuchitamatchi).
- The save screen and grave card use 3-letter seasons (SPR, RNY, SUM, FAL, WIN) and a 24-hour clock
  (15H = 3 PM). The top bar uses full season names and a 12-hour clock.
- Still in Japanese (decorative pictures): the title logo, the name plates in the history book, the
  "owari" (The End) screen, and some background art. The name entry screen keeps its kana/katakana
  buttons; it starts in ABC mode.
- Hidden test menu: hold A+B+C+X+Y+Z+L+R on the screen before the title to add ENDING, CHARACTER
  (animation viewer) and SOUND TEST to the title menu.

CHANGES
- v1.1: OTONOTEN name fixed; clearer Shooting Star instructions; race lanes read LANE 1 (was 1 LANE);
  MINIROMTCHI, MINIUNITCHI, KIDUNITCHI and BIGUNITCHI now have their TCHI endings;
  DISKTCHI is now DISCTCHI; MASKTCHI is now MASUKUTCHI (official spelling); OYATEN and GINTEN are now OYAJITEN and GINJITEN; a stray Japanese
  character after the pet's name on the rocket screen is gone.
- v1.0: first release.
