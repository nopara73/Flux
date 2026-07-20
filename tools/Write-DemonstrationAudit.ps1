param(
    [string]$CatalogPath = (Join-Path $PSScriptRoot '..\Flux\Assets\exercises.json'),
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\DEMONSTRATION_AUDIT.md')
)

$ErrorActionPreference = 'Stop'

$catalog = @(Get-Content -LiteralPath $CatalogPath -Raw | ConvertFrom-Json)
$review = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'VerifiedExerciseDemos.psd1')
$externalMedia = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExternalExerciseMedia.psd1')
$posecodeMedia = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'PosecodeExerciseMedia.psd1')
$regions = @(
    'FEET', 'LEGS', 'HANDS', 'ARMS', 'HEAD',
    'SHOULDERS', 'HIPS', 'CHEST', 'BACK', 'CORE')

$externalIds = @($review.ReviewedExternal | ForEach-Object { [int]$_ })
$posecodeIds = @($review.ReviewedPosecode | ForEach-Object { [int]$_ })
$svgIds = @($review.PurposeBuiltSvg | ForEach-Object { [int]$_ })
$verifiedIds = @($externalIds + $posecodeIds + $svgIds)

if ($catalog.Count -ne 1000) {
    throw "Expected 1000 catalog records, found $($catalog.Count)."
}

$duplicateVerifiedIds = @($verifiedIds | Group-Object | Where-Object Count -ne 1)
$unknownVerifiedIds = @($verifiedIds | Where-Object { $_ -notin $catalog.id })
if ($duplicateVerifiedIds.Count -gt 0 -or $unknownVerifiedIds.Count -gt 0) {
    throw 'The verified-demonstration inventory has duplicate or unknown IDs.'
}

$externalMappingDifference = @(Compare-Object `
        ($externalIds | Sort-Object) `
        (@($externalMedia.Keys | ForEach-Object { [int]$_ }) | Sort-Object))
$posecodeMappingDifference = @(Compare-Object `
        ($posecodeIds | Sort-Object) `
        (@($posecodeMedia.Keys | ForEach-Object { [int]$_ }) | Sort-Object))
if ($externalMappingDifference.Count -gt 0 -or
    $posecodeMappingDifference.Count -gt 0) {
    throw 'The verified inventory does not match its reviewed media mappings.'
}

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# Flux demonstration accuracy audit')
$lines.Add('')
$lines.Add('This is a deliberately conservative visual audit. **Verified** means the')
$lines.Add('animation directly demonstrates the named movement, not merely the same body')
$lines.Add('region or a vaguely related motion. Every other bundled GIF remains usable as')
$lines.Add('a temporary placeholder, but is not claimed to be a perfect demonstration.')
$lines.Add('')
$lines.Add(('Verified: **{0} / 1,000**. Still requiring exact media: **{1} / 1,000**.' -f `
            $verifiedIds.Count, (1000 - $verifiedIds.Count)))
$lines.Add('')
$lines.Add('| Region | Verified | Still imperfect |')
$lines.Add('| --- | ---: | ---: |')

foreach ($region in $regions) {
    $regionRecords = @($catalog | Where-Object dominantRegion -eq $region)
    $verifiedCount = @($regionRecords | Where-Object id -in $verifiedIds).Count
    $lines.Add(('| {0} | {1} | {2} |' -f $region, $verifiedCount,
                ($regionRecords.Count - $verifiedCount)))
}

$lines.Add('')
$lines.Add('## Verified sources')
$lines.Add('')
$lines.Add(('- Reviewed real-person clips: **{0}**' -f $externalIds.Count))
$lines.Add(('- Reviewed Posecode 3D renders: **{0}**' -f $posecodeIds.Count))
$lines.Add(('- Purpose-built SVG demonstrations: **{0}**' -f $svgIds.Count))
$lines.Add('')
$lines.Add('The real-person source mapping is in `tools/ExternalExerciseMedia.psd1`.')
$lines.Add('The reviewed 3D mapping is in `tools/PosecodeExerciseMedia.psd1`. The exact')
$lines.Add('ID inventory used to produce this report is in `tools/VerifiedExerciseDemos.psd1`.')
$lines.Add('')
$lines.Add('## Demonstrations still requiring exact media')
$lines.Add('')
$lines.Add('These are the exercises for which neither a perfect reviewed clip nor a')
$lines.Add('sufficiently exact purpose-built animation was available in this pass.')

foreach ($region in $regions) {
    $remaining = @($catalog |
        Where-Object dominantRegion -eq $region |
        Where-Object id -notin $verifiedIds)
    $lines.Add('')
    $lines.Add(('### {0} ({1})' -f $region, $remaining.Count))
    $lines.Add('')
    foreach ($exercise in $remaining) {
        $lines.Add(('- {0:D4} — {1}' -f [int]$exercise.id, $exercise.name))
    }
}

$lines.Add('')
$lines.Add('## Known fallback limitations')
$lines.Add('')
$lines.Add('- Hand poses are generic and are not yet semantic finger or mudra demonstrations.')
$lines.Add('- Generic chest motion cannot reliably show depth, contact, or breathing mechanics.')
$lines.Add('- Many school-specific dance, martial-arts, yoga, tai-chi, and qigong names')
$lines.Add('  still use a broad family-level body schematic.')
$lines.Add('- The fallback stick figure can telescope limb segments and cannot reliably')
$lines.Add('  communicate sagittal depth or every compound movement component.')
$lines.Add('- Static and isometric techniques need dedicated visual treatment before they')
$lines.Add('  can be marked verified.')

$lines | Set-Content -LiteralPath $OutputPath -Encoding utf8
Write-Output "Audit: $([IO.Path]::GetFullPath($OutputPath))"
Write-Output "Verified: $($verifiedIds.Count)"
Write-Output "Remaining: $(1000 - $verifiedIds.Count)"
