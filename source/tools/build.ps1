# Builds a patched copy of the game in ..\build. The original disc image is only read, never changed.
# Run extract-all.ps1 first (it creates work\disc).
#   - Text: every line in notes\translations.tsv is written into 0EXEC.BIN (textpatch.exe).
#   - Code: the small changes in notes\code-patches.txt are applied after the text (codepatch.exe).
#   - Font: characters redrawn in notes\font-fixes.txt are written into USER.BIN (fontpatch.exe).
#   - Overlays: each notes\overlays\NAME.tsv translates the game overlay NAME.BZ (mini-games, contests).
#   - Pictures: edited pictures listed in $pictures are packed and written back into their files.
# Usage:  & ".\tools\build.ps1"
param(
    [string]$Orig = "$env:USERPROFILE\Downloads\Sega-Saturn-de-Hakken-Tamagotchi-Park_SEGA-Saturn_JA",
    [string]$Name = "Tamagotchi Park (English test)",
    # -Unlock: also apply notes\test-unlock-patches.txt (title menu always shows ENDING/CHARACTER/SOUNDTEST)
    [switch]$Unlock
)
$ErrorActionPreference = 'Stop'
$tools = $PSScriptRoot
$root = Split-Path $tools
$work = Join-Path $root 'work'
$out = Join-Path $root "build\$Name"
$t1 = Join-Path $Orig "Sega Saturn de Hakken!! Tamagotchi Park (Japan) (Track 1).bin"
$t2 = Join-Path $Orig "Sega Saturn de Hakken!! Tamagotchi Park (Japan) (Track 2).bin"
New-Item -ItemType Directory -Force $out, "$work\edited" | Out-Null
$newT1 = Join-Path $out "$Name (Track 1).bin"
$newT2 = Join-Path $out "$Name (Track 2).bin"

# Edited pictures: the edited, unpacked picture; where its packed block goes; and where its slot ends
# (the next thing in the file that must not be overwritten).
# The options-screen background repaint (optedit.exe) is switched off: decorative Japanese in the
# art stays, and only text the player needs to read gets translated. To switch it back on:
#   @{ Edited = "$work\edited\USER.BIN@014000.unp"; File = '/DATA/USER.BIN'; Offset = '14000'; SlotEnd = '1F000'; What = 'Options screen background' }
$pictures = @()

Copy-Item $t1 $newT1 -Force

"Font: applying notes\font-fixes.txt"
& "$tools\fontpatch.exe" "$work\disc\DATA\USER.BIN" "$root\notes\font-fixes.txt" "$work\edited\USER.BIN"
if (-not $?) { throw "font patching failed" }
$usize = (Get-Item "$work\edited\USER.BIN").Length
& "$tools\discpatch.exe" patch $newT1 $newT1 '/DATA/USER.BIN' '0' "$work\edited\USER.BIN" ('{0:X}' -f $usize)
if (-not $?) { throw "writing USER.BIN failed" }

"Text: applying notes\translations.tsv"
$overlays = Get-ChildItem "$work\unpacked\*.BZ@*.unp" | ForEach-Object FullName
& "$tools\textpatch.exe" "$work\disc\0EXEC.BIN" "$root\notes\translations.tsv" "$work\edited\0EXEC.BIN" @overlays
if (-not $?) { throw "text patching failed" }
"Code: applying notes\code-patches.txt"
& "$tools\codepatch.exe" "$work\edited\0EXEC.BIN" "$root\notes\code-patches.txt" "$work\edited\0EXEC.BIN"
if (-not $?) { throw "code patching failed" }
if ($Unlock) {
    "Code: applying notes\test-unlock-patches.txt (test build: hidden title menu options always on)"
    & "$tools\codepatch.exe" "$work\edited\0EXEC.BIN" "$root\notes\test-unlock-patches.txt" "$work\edited\0EXEC.BIN"
    if (-not $?) { throw "unlock patching failed" }
}
$size = (Get-Item "$work\edited\0EXEC.BIN").Length
& "$tools\discpatch.exe" patch $newT1 $newT1 '/0EXEC.BIN' '0' "$work\edited\0EXEC.BIN" ('{0:X}' -f $size)
if (-not $?) { throw "writing 0EXEC.BIN failed" }

# Game overlays (mini-games, contests, history): each sheet in notes\overlays\ is named after its file,
# with optional small code changes in notes\overlays\NAME-code.txt (offsets within the unpacked overlay).
# The overlay is unpacked, translated (it loads at 0x00280000), repacked, and written back over its file.
foreach ($sheet in Get-ChildItem "$root\notes\overlays\*.tsv" -ErrorAction SilentlyContinue) {
    $ov = $sheet.BaseName
    "Overlay: $ov"
    & "$tools\textpatch.exe" "$work\unpacked\$ov.BZ@000000.unp" $sheet.FullName "$work\edited\$ov.unp" --base 00280000
    if (-not $?) { throw "text patching $ov failed" }
    $codeFile = Join-Path $sheet.DirectoryName "$ov-code.txt"
    if (Test-Path $codeFile) {
        & "$tools\codepatch.exe" "$work\edited\$ov.unp" $codeFile "$work\edited\$ov.unp"
        if (-not $?) { throw "code patching $ov failed" }
    }
    & "$tools\bz.exe" pack "$work\edited\$ov.unp" "$work\edited\$ov.BZ"
    if (-not $?) { throw "packing $ov failed" }
    & "$tools\discpatch.exe" replace $newT1 $newT1 "/DATA/$ov.BZ" "$work\edited\$ov.BZ"
    if (-not $?) { throw "writing $ov failed" }
}

foreach ($pt in $pictures) {
    "Picture: $($pt.What)"
    $packed = [IO.Path]::ChangeExtension($pt.Edited, '.bz')
    & "$tools\bz.exe" pack $pt.Edited $packed
    if (-not $?) { throw "packing failed" }
    & "$tools\discpatch.exe" patch $newT1 $newT1 $pt.File $pt.Offset $packed $pt.SlotEnd
    if (-not $?) { throw "patching failed" }
}
# Uncompressed 4-bit pictures (sprite patterns), kept as text grids in notes\pictures\:
# named FILE-OFFSET-description.txt (e.g. TAMA-27700-egg-text.txt = DATA\TAMA.BIN at 0x27700).
# One hex digit per pixel (a palette colour), one line per row; '#' lines are comments.
# Each row's pixels are packed two per byte (left pixel in the high half) and written in place.
foreach ($grid in Get-ChildItem "$root\notes\pictures\*.txt" -ErrorAction SilentlyContinue) {
    $parts = $grid.BaseName.Split('-')
    $file = "/DATA/$($parts[0]).BIN"; $off = [Convert]::ToInt32($parts[1], 16)
    $rows = @(Get-Content $grid.FullName | Where-Object { $_ -and -not $_.StartsWith('#') })
    $bytes = New-Object System.Collections.Generic.List[byte]
    foreach ($r in $rows) {
        if ($r.Length % 2) { throw "$($grid.Name): rows must have an even number of pixels" }
        for ($i = 0; $i -lt $r.Length; $i += 2) { $bytes.Add([byte]([Convert]::ToInt32($r.Substring($i, 2), 16))) }
    }
    "Picture: $($grid.Name) -> $file at 0x$($parts[1]) ($($rows.Count) rows, $($bytes.Count) bytes)"
    $raw = "$work\edited\$($grid.BaseName).raw"
    [IO.File]::WriteAllBytes($raw, $bytes.ToArray())
    & "$tools\discpatch.exe" patch $newT1 $newT1 $file ('{0:X}' -f $off) $raw ('{0:X}' -f ($off + $bytes.Count))
    if (-not $?) { throw "writing $($grid.Name) failed" }
}

Copy-Item $t2 $newT2 -Force

@"
CATALOG 0000000000000
FILE "$Name (Track 1).bin" BINARY
  TRACK 01 MODE1/2352
    INDEX 01 00:00:00
FILE "$Name (Track 2).bin" BINARY
  TRACK 02 AUDIO
    INDEX 00 00:00:00
    INDEX 01 00:02:00
"@ | Out-File -Encoding ascii (Join-Path $out "$Name.cue")

"Checking every sector of the new disc..."
& "$tools\discpatch.exe" selftest $newT1
if (-not $?) { throw "the new disc failed its sector check" }
"Done: $out"
