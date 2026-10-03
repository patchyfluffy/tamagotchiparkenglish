Tamagotchi Park English translation - project files
===================================================

These are the files used to make the patch, so anyone (especially Japanese speakers!) can improve the
translation and rebuild it. They contain no game data: you need your own copy of the game.

WHAT'S HERE
- notes\translations.tsv     The main translation sheet: offset, original Japanese, English, note.
- notes\overlays\*.tsv       Sheets for the mini-games, contests and history book (+ small code changes
                             in NAME-code.txt). "box" blocks lay out multi-line instruction boxes.
- notes\code-patches.txt     Small changes to the game's program (name endings, clock, seasons, layout).
- notes\font-fixes.txt       Redrawn font characters (the 9, the P, and the squeezed TC/HI of "TCHI").
- notes\pictures\*.txt       Small pictures redrawn as text grids (egg room bubble, YR, DAYS).
- notes\test-unlock-patches.txt   Test-build-only extras (hidden menu always on, faster park stages).
- notes\findings.md          How the game works inside (compression, text format, fonts, saves).
- tools\                     Source code for the tools (C#, built with the C# compiler that comes with
                             Windows: C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe) and
                             PowerShell scripts. README.md has the compile command and more details.

HOW TO REBUILD (Windows)
1. Compile the tools (see README.md). extract-all.ps1 compiles the ones it needs by itself.
2. Run tools\extract-all.ps1 -Bin "<path to your original Track 1 .bin>". It creates a work\ folder.
3. Run tools\build.ps1 -Orig "<folder with your original .bin/.cue>" -Name "My build".
   The patched game appears in build\My build. Add -Unlock for a test build.
4. Make patches: tools\bpsmake.exe <original Track 1> <new Track 1> <out.bps> (or ipsmake for .ips).

EDITING TEXT
- Change the English column and rebuild. The Japanese column must match the game exactly (it's a
  safety check). The font has capital letters, digits and basic punctuation only.
- Notes: "room=N" = fixed-size slot (N-1 letters max); "keepold" = old slot reused for code;
  "{empty}" = replace with nothing. Each sheet's header and tools\textpatch.cs explain the details.
- Letters are 13 pixels apart; many boxes have a fixed width, so keep the comments' length limits.
