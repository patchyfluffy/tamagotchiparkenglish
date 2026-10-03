# Rebuilds everything in ..\work from the original disc image:
#   work\disc      the 27 files on the disc
#   work\unpacked  every BZ.w block, unpacked
#   work\img       the unpacked pictures, drawn in grayscale (real palettes not found yet)
#   work\exec.asm  the game's main program, disassembled
# Run it from PowerShell:  & ".\tools\extract-all.ps1"
param(
    [string]$Bin = "$env:USERPROFILE\Downloads\Sega-Saturn-de-Hakken-Tamagotchi-Park_SEGA-Saturn_JA\Sega Saturn de Hakken!! Tamagotchi Park (Japan) (Track 1).bin"
)
$ErrorActionPreference = 'Stop'
$tools = $PSScriptRoot
$work = Join-Path (Split-Path $tools) 'work'
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

# Compile any tool whose .exe is missing or older than its source.
foreach ($t in 'sh2dis', 'bz', 'gfx') {
    $src = Join-Path $tools "$t.cs"; $exe = Join-Path $tools "$t.exe"
    if (-not (Test-Path $exe) -or (Get-Item $src).LastWriteTime -gt (Get-Item $exe).LastWriteTime) {
        "Compiling $t..."
        & $csc /nologo /optimize /r:System.Drawing.dll "/out:$exe" $src
        if (-not $?) { throw "compiling $t failed" }
    }
}

"Extracting disc files..."
New-Item -ItemType Directory -Force "$work\disc", "$work\unpacked", "$work\img" | Out-Null
& "$tools\isolist.ps1" -Bin $Bin -OutDir "$work\disc" | Out-File -Encoding utf8 "$work\disc-files.txt"

"Disassembling 0EXEC.BIN..."
& "$tools\sh2dis.exe" "$work\disc\0EXEC.BIN" 06010000 | Out-File -Encoding ascii "$work\exec.asm"

"Unpacking BZ.w blocks..."
& "$tools\bz.exe" unpackall "$work\disc" "$work\unpacked" | Out-File -Encoding utf8 "$work\unpacked-blocks.txt"

"Drawing pictures..."
foreach ($f in Get-ChildItem "$work\unpacked\*.unp") {
    # Width guesses from the unpacked size: 320x224 and 320x400 screens, 512-wide sheets.
    $w = switch ($f.Length) { 71680 { 320 } 128000 { 320 } 131072 { 512 } 114688 { 512 } default { 0 } }
    if ($w) { & "$tools\gfx.exe" $f.FullName "$work\img\$($f.BaseName).png" 8l $w | Out-Null }
}
"Done. Everything is in $work"
