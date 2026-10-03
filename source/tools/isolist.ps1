param([string]$Bin, [string]$OutDir)
# Reads a raw MODE1/2352 Saturn track, lists the ISO9660 files, and optionally extracts them.
$fs = [System.IO.File]::OpenRead($Bin)
function Read-Sector([long]$lba) {
    $buf = New-Object byte[] 2048
    $fs.Seek($lba * 2352 + 16, 'Begin') | Out-Null
    $fs.Read($buf, 0, 2048) | Out-Null
    return ,$buf
}
function Read-Extent([long]$lba, [long]$size) {
    $ms = New-Object System.IO.MemoryStream
    $remaining = $size
    $l = $lba
    while ($remaining -gt 0) {
        $s = Read-Sector $l
        $n = [Math]::Min(2048, $remaining)
        $ms.Write($s, 0, $n)
        $remaining -= $n
        $l++
    }
    return ,$ms.ToArray()
}
function Walk([long]$lba, [long]$size, [string]$path) {
    $data = Read-Extent $lba $size
    $i = 0
    while ($i -lt $data.Length) {
        $len = $data[$i]
        if ($len -eq 0) { $i = ([Math]::Floor($i / 2048) + 1) * 2048; continue }
        $ext = [BitConverter]::ToUInt32($data, $i + 2)
        $sz = [BitConverter]::ToUInt32($data, $i + 10)
        $flags = $data[$i + 25]
        $nl = $data[$i + 32]
        $name = [System.Text.Encoding]::ASCII.GetString($data, $i + 33, $nl)
        if (-not ($nl -eq 1 -and ($data[$i + 33] -eq 0 -or $data[$i + 33] -eq 1))) {
            $name = $name -replace ';1$', ''
            $full = "$path/$name"
            if ($flags -band 2) { Walk $ext $sz $full }
            else {
                "{0,-30} LBA {1,6}  {2,10} bytes" -f $full, $ext, $sz
                if ($OutDir) {
                    $dest = Join-Path $OutDir ($full.TrimStart('/'))
                    New-Item -ItemType Directory -Force (Split-Path $dest) | Out-Null
                    [System.IO.File]::WriteAllBytes($dest, (Read-Extent $ext $sz))
                }
            }
        }
        $i += $len
    }
}
$pvd = Read-Sector 16
$rootLba = [BitConverter]::ToUInt32($pvd, 156 + 2)
$rootSz = [BitConverter]::ToUInt32($pvd, 156 + 10)
$hdr = Read-Sector 0
"Header: " + [System.Text.Encoding]::ASCII.GetString($hdr, 0, 256)
Walk $rootLba $rootSz ""
$fs.Close()
