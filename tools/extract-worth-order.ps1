$enumPath = "C:\Users\floor\Documents\coding\skyblock\dev\Data\Auctions\Enchantments.cs"
$constPath = "C:\Users\floor\Documents\coding\skyblock\dev\Data\Flipper\Constants.cs"
$ourEnumPath = "C:\Users\floor\Documents\coding\skyblock\SkyFlipperSolo\Models\Enchantment.cs"

$enumText = Get-Content -Raw $enumPath
$constText = Get-Content -Raw $constPath
$ourText = Get-Content -Raw $ourEnumPath

function Get-EnumNames([string]$text) {
    $m = [regex]::Match($text, "enum\s+EnchantmentType\s*\{(.*?)\n\s*\}", "Singleline")
    if (-not $m.Success) { throw "Enum block not found" }
    $names = @()
    foreach ($line in ($m.Groups[1].Value -split "`n")) {
        $l = $line.Trim()
        if (-not $l -or $l.StartsWith("//")) { continue }
        $l = ($l -split "//")[0].Trim()
        if (-not $l) { continue }
        $l = $l.TrimEnd(',')
        if (-not $l) { continue }
        if ($l -match "=") { $l = ($l -split "=")[0].Trim() }
        if ($l) { $names += $l }
    }
    return $names
}

function Get-EnumValueMap([string]$text) {
    $m = [regex]::Match($text, "enum\s+EnchantmentType\s*\{(.*?)\n\s*\}", "Singleline")
    if (-not $m.Success) { throw "Enum block not found" }
    $entries = @()
    foreach ($line in ($m.Groups[1].Value -split "`n")) {
        $l = $line.Trim()
        if (-not $l -or $l.StartsWith("//")) { continue }
        $l = ($l -split "//")[0].Trim()
        if (-not $l) { continue }
        $l = $l.TrimEnd(',')
        if (-not $l) { continue }
        if ($l -match "=") {
            $parts = $l -split "="
            $name = $parts[0].Trim()
            $val = [int]($parts[1].Trim())
            $entries += [pscustomobject]@{Name=$name; Value=$val; HasValue=$true}
        } else {
            $entries += [pscustomobject]@{Name=$l; Value=$null; HasValue=$false}
        }
    }
    $valueToName = @{}
    $current = $null
    foreach ($e in $entries) {
        if ($e.HasValue) { $current = $e.Value }
        else { if ($null -eq $current) { $current = 0 } else { $current = $current + 1 } }
        if (-not $valueToName.ContainsKey($current)) { $valueToName[$current] = $e.Name }
    }
    return $valueToName
}

$valueToName = Get-EnumValueMap $enumText
$ourNames = Get-EnumNames $ourText

$worthOrderMatch = [regex]::Match($constText, "WorthOrder\s*=\s*new\s+List<int>\s*\(\)\s*\{(.*?)\};", "Singleline")
if (-not $worthOrderMatch.Success) { throw "WorthOrder not found" }
$worthNums = $worthOrderMatch.Groups[1].Value -replace "`r", "" -replace "`n", "" -split "," | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" } | ForEach-Object { [int]$_ }

$wolMatch = [regex]::Match($constText, "WorthOrderLevels\s*=\s*new\s*\(\)\s*\{(.*?)\};", "Singleline")
if (-not $wolMatch.Success) { throw "WorthOrderLevels not found" }
$wolPairs = @()
$pairMatches = [regex]::Matches($wolMatch.Groups[1].Value, "\(([^\)]+)\)")
foreach ($pm in $pairMatches) {
    $parts = $pm.Groups[1].Value -split "," | ForEach-Object { $_.Trim() }
    if ($parts.Length -ge 2) { $wolPairs += [pscustomobject]@{Val=[int]$parts[0]; Level=[int]$parts[1]} }
}

$worthOrderNames = $worthNums | ForEach-Object { if ($valueToName.ContainsKey($_)) { $valueToName[$_] } else { "<UNKNOWN:$($_)>" } }
$worthLevelNames = $wolPairs | ForEach-Object {
    $n = $valueToName[$_.Val]
    if (-not $n) { $n = "<UNKNOWN:$($_.Val)>" }
    [pscustomobject]@{Name=$n; Level=$_.Level}
}

$missing = $worthOrderNames | Where-Object { $_ -notlike '<UNKNOWN:*' -and ($ourNames -notcontains $_) } | Sort-Object -Unique
$missingLevels = $worthLevelNames | Where-Object { $_.Name -notlike '<UNKNOWN:*' -and ($ourNames -notcontains $_.Name) } | Select-Object -ExpandProperty Name -Unique | Sort-Object

Write-Output "WORTH_ORDER_NAMES:"
Write-Output ($worthOrderNames -join ", ")
Write-Output ""
Write-Output "WORTH_ORDER_LEVELS:"
foreach ($item in $worthLevelNames) { Write-Output ("($($item.Name), $($item.Level))") }
Write-Output ""
Write-Output "MISSING_IN_OUR_ENUM:"
Write-Output ($missing -join ", ")
Write-Output ""
Write-Output "MISSING_IN_OUR_ENUM_LEVELS:"
Write-Output ($missingLevels -join ", ")
