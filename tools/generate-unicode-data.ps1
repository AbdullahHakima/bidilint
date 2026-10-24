# Regenerates src/BidiLint/Unicode/UnicodeTables.g.cs from the pinned UCD.
# Usage: pwsh tools/generate-unicode-data.ps1
# The script verifies every source checksum before parsing and is safe to
# rerun; downloaded files are cached in tools/.unicode-cache.
[CmdletBinding()]
param(
    [string]$DataDirectory = (Join-Path $PSScriptRoot '.unicode-cache'),
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\src\BidiLint\Unicode\UnicodeTables.g.cs')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Version = '17.0.0'
$BaseUrl = "https://www.unicode.org/Public/$Version/ucd"
$Sources = [ordered]@{
    'DerivedBidiClass.txt' = @{
        Url = "$BaseUrl/extracted/DerivedBidiClass.txt"
        Sha256 = '4867b4b7f0731ed1bfcd34cc6251211ff1542541fce0734b6fbda139ee80b3a4'
    }
    'Scripts.txt' = @{
        Url = "$BaseUrl/Scripts.txt"
        Sha256 = '9f5e50d3abaee7d6ce09480f325c706f485ae3240912527e651954d2d6b035bf'
    }
    'BidiBrackets.txt' = @{
        Url = "$BaseUrl/BidiBrackets.txt"
        Sha256 = 'dadbaf38a0d0246e5b805bf8725cb81b7c621f93d030595635f5ba2c2f179428'
    }
    'BidiMirroring.txt' = @{
        Url = "$BaseUrl/BidiMirroring.txt"
        Sha256 = 'a2f16fb873ab4fcdf3221cb1a8a85a134ddd6ed03603181823ff5206af3741ce'
    }
}

New-Item -ItemType Directory -Path $DataDirectory -Force | Out-Null

foreach ($name in $Sources.Keys) {
    $source = $Sources[$name]
    $path = Join-Path $DataDirectory $name
    $download = $true
    if (Test-Path $path) {
        $existing = (Get-FileHash -Algorithm SHA256 -Path $path).Hash.ToLowerInvariant()
        $download = $existing -ne $source.Sha256
    }

    if ($download) {
        Write-Host "Downloading $name"
        Invoke-WebRequest -Uri $source.Url -OutFile $path -UseBasicParsing
    }

    $actual = (Get-FileHash -Algorithm SHA256 -Path $path).Hash.ToLowerInvariant()
    if ($actual -ne $source.Sha256) {
        throw "Checksum mismatch for $name. Expected $($source.Sha256), got $actual."
    }
}

function Get-DataLines([string]$path) {
    Get-Content -Path $path | Where-Object { $_ -match '^[0-9A-Fa-f]' }
}

function Get-Range([string]$text) {
    $parts = $text.Trim() -split '\.\.'
    if ($parts.Length -eq 1) {
        $start = [Convert]::ToInt32($parts[0], 16)
        return @($start, $start)
    }

    return @([Convert]::ToInt32($parts[0], 16), [Convert]::ToInt32($parts[1], 16))
}

function Fill-Range([Array]$values, [int]$start, [int]$end, [int]$value) {
    for ($i = $start; $i -le $end; $i++) {
        $values[$i] = [byte]$value
    }
}

function Format-CodePoint([int]$value) {
    return '0x{0:X4}' -f $value
}

$BidiNames = @{}
foreach ($name in @('L', 'R', 'AL', 'EN', 'ES', 'ET', 'AN', 'CS', 'NSM', 'BN', 'B', 'S', 'WS', 'ON',
        'LRE', 'LRO', 'RLE', 'RLO', 'PDF', 'LRI', 'RLI', 'FSI', 'PDI')) {
    $BidiNames[$name] = $name
}
$BidiNames['Left_To_Right'] = 'L'
$BidiNames['Right_To_Left'] = 'R'
$BidiNames['Arabic_Letter'] = 'AL'
$BidiNames['European_Number'] = 'EN'
$BidiNames['European_Separator'] = 'ES'
$BidiNames['European_Terminator'] = 'ET'
$BidiNames['Arabic_Number'] = 'AN'
$BidiNames['Common_Separator'] = 'CS'
$BidiNames['Nonspacing_Mark'] = 'NSM'
$BidiNames['Boundary_Neutral'] = 'BN'
$BidiNames['Paragraph_Separator'] = 'B'
$BidiNames['Segment_Separator'] = 'S'
$BidiNames['White_Space'] = 'WS'
$BidiNames['Other_Neutral'] = 'ON'
$BidiNames['Left_To_Right_Embedding'] = 'LRE'
$BidiNames['Left_To_Right_Override'] = 'LRO'
$BidiNames['Right_To_Left_Embedding'] = 'RLE'
$BidiNames['Right_To_Left_Override'] = 'RLO'
$BidiNames['Pop_Directional_Format'] = 'PDF'
$BidiNames['Left_To_Right_Isolate'] = 'LRI'
$BidiNames['Right_To_Left_Isolate'] = 'RLI'
$BidiNames['First_Strong_Isolate'] = 'FSI'
$BidiNames['Pop_Directional_Isolate'] = 'PDI'

$BidiOrder = @('L', 'R', 'AL', 'EN', 'ES', 'ET', 'AN', 'CS', 'NSM', 'BN', 'B', 'S', 'WS', 'ON',
    'LRE', 'LRO', 'RLE', 'RLO', 'PDF', 'LRI', 'RLI', 'FSI', 'PDI')
$BidiIds = @{}
for ($i = 0; $i -lt $BidiOrder.Count; $i++) {
    $BidiIds[$BidiOrder[$i]] = $i
}

$BidiValues = New-Object 'byte[]' 0x110000
Fill-Range $BidiValues 0 0x10FFFF $BidiIds['L']

$bidiPath = Join-Path $DataDirectory 'DerivedBidiClass.txt'
foreach ($line in Get-Content -Path $bidiPath) {
    if ($line -notmatch '^#\s*@missing:\s*([0-9A-Fa-f]+)\.\.([0-9A-Fa-f]+);\s*(\S+)') {
        continue
    }

    $start = [Convert]::ToInt32($Matches[1], 16)
    $end = [Convert]::ToInt32($Matches[2], 16)
    $name = $Matches[3]
    if (-not $BidiNames.ContainsKey($name)) {
        throw "Unknown bidi class '@missing' value: $name"
    }
    Fill-Range $BidiValues $start $end $BidiIds[$BidiNames[$name]]
}

foreach ($line in Get-DataLines $bidiPath) {
    $parts = $line.Split(';')
    $range = Get-Range $parts[0]
    $name = $parts[1].Split('#')[0].Trim()
    if (-not $BidiNames.ContainsKey($name)) {
        throw "Unknown bidi class value: $name"
    }
    Fill-Range $BidiValues $range[0] $range[1] $BidiIds[$BidiNames[$name]]
}

$BidiRanges = New-Object System.Collections.Generic.List[string]
$start = 0
$current = [int]$BidiValues[0]
for ($i = 1; $i -lt 0x110000; $i++) {
    $value = [int]$BidiValues[$i]
    if ($value -eq $current) {
        continue
    }

    $BidiRanges.Add("        new($(Format-CodePoint $start), $(Format-CodePoint ($i - 1)), BidiClass.$($BidiOrder[$current])),")
    $start = $i
    $current = $value
}
$BidiRanges.Add("        new($(Format-CodePoint $start), 0x10FFFF, BidiClass.$($BidiOrder[$current])),")

$scriptPath = Join-Path $DataDirectory 'Scripts.txt'
$ScriptNames = @(Get-DataLines $scriptPath |
    ForEach-Object { ($_ -split ';')[1].Split('#')[0].Trim() } |
    Sort-Object -Unique)
$ScriptNames = @('Unknown') + $ScriptNames | Sort-Object -Unique
$ScriptIds = @{}
for ($i = 0; $i -lt $ScriptNames.Count; $i++) {
    $ScriptIds[$ScriptNames[$i]] = $i
}
if (-not $ScriptIds.ContainsKey('Unknown')) {
    throw 'The script table must include the Unknown default.'
}

$ScriptValues = [uint16[]]::new(0x110000)
for ($i = 0; $i -lt 0x110000; $i++) {
    $ScriptValues[$i] = [uint16]$ScriptIds['Unknown']
}

foreach ($line in Get-DataLines $scriptPath) {
    $parts = $line.Split(';')
    $range = Get-Range $parts[0]
    $name = $parts[1].Split('#')[0].Trim()
    if (-not $ScriptIds.ContainsKey($name)) {
        throw "Unknown script value: $name"
    }
    for ($i = $range[0]; $i -le $range[1]; $i++) {
        $ScriptValues[$i] = [uint16]$ScriptIds[$name]
    }
}

$ScriptRanges = New-Object System.Collections.Generic.List[string]
$start = 0
$current = [int]$ScriptValues[0]
for ($i = 1; $i -lt 0x110000; $i++) {
    $value = [int]$ScriptValues[$i]
    if ($value -eq $current) {
        continue
    }

    $ScriptRanges.Add("        new($(Format-CodePoint $start), $(Format-CodePoint ($i - 1)), UnicodeScript.$($ScriptNames[$current])),")
    $start = $i
    $current = $value
}
$ScriptRanges.Add("        new($(Format-CodePoint $start), 0x10FFFF, UnicodeScript.$($ScriptNames[$current])),")

$Brackets = foreach ($line in Get-DataLines (Join-Path $DataDirectory 'BidiBrackets.txt')) {
    if ($line -notmatch '^([0-9A-Fa-f]+);\s*([0-9A-Fa-f]+);\s*([oc])') {
        throw "Unexpected BidiBrackets line: $line"
    }
    $type = if ($Matches[3] -eq 'o') { 'Open' } else { 'Close' }
    [pscustomobject]@{
        CodePoint = [Convert]::ToInt32($Matches[1], 16)
        Paired = [Convert]::ToInt32($Matches[2], 16)
        Type = $type
    }
}
$BracketLines = New-Object System.Collections.Generic.List[string]
foreach ($bracket in ($Brackets | Sort-Object CodePoint)) {
    $BracketLines.Add("        new($(Format-CodePoint $bracket.CodePoint), $(Format-CodePoint $bracket.Paired), BracketType.$($bracket.Type)),")
}

$Mirrors = foreach ($line in Get-DataLines (Join-Path $DataDirectory 'BidiMirroring.txt')) {
    if ($line -notmatch '^([0-9A-Fa-f]+);\s*([0-9A-Fa-f]+)') {
        throw "Unexpected BidiMirroring line: $line"
    }
    [pscustomobject]@{
        CodePoint = [Convert]::ToInt32($Matches[1], 16)
        Mirrored = [Convert]::ToInt32($Matches[2], 16)
    }
}
$MirrorLines = New-Object System.Collections.Generic.List[string]
foreach ($mirror in ($Mirrors | Sort-Object CodePoint)) {
    $MirrorLines.Add("        new($(Format-CodePoint $mirror.CodePoint), $(Format-CodePoint $mirror.Mirrored)),")
}

$builder = New-Object System.Text.StringBuilder
[void]$builder.AppendLine('// <auto-generated />')
[void]$builder.AppendLine("// Generated from Unicode $Version by tools/generate-unicode-data.ps1. Do not edit.")
[void]$builder.AppendLine()
[void]$builder.AppendLine('namespace BidiLint;')
[void]$builder.AppendLine()
[void]$builder.AppendLine('internal enum BidiClass : byte')
[void]$builder.AppendLine('{')
foreach ($name in $BidiOrder) {
    [void]$builder.AppendLine("    $name,")
}
[void]$builder.AppendLine('}')
[void]$builder.AppendLine()
[void]$builder.AppendLine('internal enum UnicodeScript : ushort')
[void]$builder.AppendLine('{')
foreach ($name in $ScriptNames) {
    [void]$builder.AppendLine("    $name,")
}
[void]$builder.AppendLine('}')
[void]$builder.AppendLine()
[void]$builder.AppendLine('internal enum BracketType : byte')
[void]$builder.AppendLine('{')
[void]$builder.AppendLine('    Open,')
[void]$builder.AppendLine('    Close,')
[void]$builder.AppendLine('}')
[void]$builder.AppendLine()
[void]$builder.AppendLine('internal readonly record struct BidiRange(int Start, int End, BidiClass Value);')
[void]$builder.AppendLine('internal readonly record struct ScriptRange(int Start, int End, UnicodeScript Value);')
[void]$builder.AppendLine('internal readonly record struct BracketPair(int CodePoint, int PairedCodePoint, BracketType Type);')
[void]$builder.AppendLine('internal readonly record struct MirrorPair(int CodePoint, int MirroredCodePoint);')
[void]$builder.AppendLine()
[void]$builder.AppendLine('internal static class UnicodeTables')
[void]$builder.AppendLine('{')
[void]$builder.AppendLine("    public const string Version = `"$Version`";")
[void]$builder.AppendLine()
[void]$builder.AppendLine('    internal static readonly BidiRange[] BidiRanges =')
[void]$builder.AppendLine('    [')
foreach ($line in $BidiRanges) { [void]$builder.AppendLine($line) }
[void]$builder.AppendLine('    ];')
[void]$builder.AppendLine()
[void]$builder.AppendLine('    internal static readonly ScriptRange[] ScriptRanges =')
[void]$builder.AppendLine('    [')
foreach ($line in $ScriptRanges) { [void]$builder.AppendLine($line) }
[void]$builder.AppendLine('    ];')
[void]$builder.AppendLine()
[void]$builder.AppendLine('    internal static readonly BracketPair[] BracketPairs =')
[void]$builder.AppendLine('    [')
foreach ($line in $BracketLines) { [void]$builder.AppendLine($line) }
[void]$builder.AppendLine('    ];')
[void]$builder.AppendLine()
[void]$builder.AppendLine('    internal static readonly MirrorPair[] MirrorPairs =')
[void]$builder.AppendLine('    [')
foreach ($line in $MirrorLines) { [void]$builder.AppendLine($line) }
[void]$builder.AppendLine('    ];')
[void]$builder.AppendLine('}')

$outputDirectory = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$encoding = New-Object System.Text.UTF8Encoding($false)
$generated = $builder.ToString().Replace("`r`n", "`n")
[IO.File]::WriteAllText($OutputPath, $generated, $encoding)

Write-Host "Wrote $OutputPath"
Write-Host "Unicode $Version; $($BidiRanges.Count) bidi ranges; $($ScriptRanges.Count) script ranges; $($BracketLines.Count) brackets; $($MirrorLines.Count) mirror pairs."
