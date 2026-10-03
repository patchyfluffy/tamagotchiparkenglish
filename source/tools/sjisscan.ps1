param([string]$Dir, [int]$MinChars = 3, [int]$MaxPerFile = 40)
# Scans files for runs of Shift-JIS double-byte text (kana/kanji/fullwidth) and prints them.
$sjis = [System.Text.Encoding]::GetEncoding(932)
foreach ($f in Get-ChildItem -Recurse -File $Dir) {
    $b = [System.IO.File]::ReadAllBytes($f.FullName)
    $runs = @(); $i = 0; $total = 0
    while ($i -lt $b.Length - 1) {
        $start = $i; $n = 0
        while ($i -lt $b.Length - 1) {
            $hi = $b[$i]; $lo = $b[$i + 1]
            $okHi = ($hi -ge 0x81 -and $hi -le 0x84) -or ($hi -ge 0x88 -and $hi -le 0x9F) -or ($hi -ge 0xE0 -and $hi -le 0xEA)
            $okLo = ($lo -ge 0x40 -and $lo -le 0xFC -and $lo -ne 0x7F)
            if ($okHi -and $okLo) { $n++; $i += 2 } else { break }
        }
        if ($n -ge $MinChars) {
            $total++
            if ($runs.Count -lt $MaxPerFile) { $runs += ("  0x{0:X6}: {1}" -f $start, $sjis.GetString($b, $start, $n * 2)) }
        }
        if ($n -eq 0) { $i = $start + 1 }
    }
    if ($total -gt 0) {
        "=== $($f.Name)  ($total text runs)"
        $runs
    }
}
