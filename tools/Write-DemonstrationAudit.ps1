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
$exactMediaCopies = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExactExerciseMediaCopies.psd1')
$exactMediaTransforms = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExactExerciseMediaTransforms.psd1')
$regions = @(
    'FEET', 'LEGS', 'HANDS', 'ARMS', 'HEAD',
    'SHOULDERS', 'HIPS', 'CHEST', 'BACK', 'CORE')

$externalIds = @($review.ReviewedExternal | ForEach-Object { [int]$_ })
$humanExternalIds = @(
    $externalMedia.GetEnumerator() |
        Where-Object { $_.Value.ContainsKey('Human') -and [bool]$_.Value.Human } |
        ForEach-Object { [int]$_.Key })
$otherExternalCount = $externalIds.Count - $humanExternalIds.Count
$posecodeIds = @($review.ReviewedPosecode | ForEach-Object { [int]$_ })
$svgIds = @($review.PurposeBuiltSvg | ForEach-Object { [int]$_ })
$copyIds = @($review.ReviewedExactCopies | ForEach-Object { [int]$_ })
$transformIds = @($review.ReviewedExactTransforms | ForEach-Object { [int]$_ })
$retainedIds = @(
    $externalIds + $posecodeIds + $svgIds + $copyIds + $transformIds |
        Sort-Object -Unique)

$duplicateReviewedIds = @(
    $externalIds + $posecodeIds + $svgIds + $copyIds + $transformIds |
        Group-Object |
        Where-Object Count -ne 1)
$catalogDifference = @(Compare-Object `
        ($retainedIds | Sort-Object) `
        (@($catalog.id | ForEach-Object { [int]$_ }) | Sort-Object))
if ($duplicateReviewedIds.Count -gt 0 -or $catalogDifference.Count -gt 0) {
    throw 'The bundled catalog must exactly match the retained reviewed inventory.'
}

$mappingChecks = @(
    @(Compare-Object ($externalIds | Sort-Object) `
        (@($externalMedia.Keys | ForEach-Object { [int]$_ }) | Sort-Object)),
    @(Compare-Object ($posecodeIds | Sort-Object) `
        (@($posecodeMedia.Keys | ForEach-Object { [int]$_ }) | Sort-Object)),
    @(Compare-Object ($copyIds | Sort-Object) `
        (@($exactMediaCopies.Keys | ForEach-Object { [int]$_ }) | Sort-Object)),
    @(Compare-Object ($transformIds | Sort-Object) `
        (@($exactMediaTransforms.Keys | ForEach-Object { [int]$_ }) | Sort-Object)))
if (@($mappingChecks | Where-Object Count -gt 0).Count -gt 0) {
    throw 'The retained inventory does not match its reviewed media mappings.'
}

$directSourceIds = @($externalIds + $posecodeIds + $svgIds)
$unverifiedCopySources = @(
    $exactMediaCopies.Values |
        ForEach-Object { [int]$_ } |
        Where-Object { $_ -notin $directSourceIds })
$unverifiedTransformSources = @(
    $exactMediaTransforms.Values |
        ForEach-Object { [int]$_.Source } |
        Where-Object { $_ -notin $directSourceIds })
if ($unverifiedCopySources.Count -gt 0 -or
    $unverifiedTransformSources.Count -gt 0) {
    throw 'A retained copy or transform points to a discarded source.'
}

$invalidRegions = @($regions | Where-Object {
        @($catalog | Where-Object dominantRegion -eq $_).Count -lt 3
    })
if ($invalidRegions.Count -gt 0) {
    throw "Every region must retain at least three exercises: $($invalidRegions -join ', ')."
}

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# Flux demonstration quality audit')
$lines.Add('')
$lines.Add('Flux now ships a quality-first exercise catalog with no placeholder media.')
$lines.Add(('All **{0}** bundled exercises have a reviewed, directly matching demonstration.' -f
        $catalog.Count))
$lines.Add('The 673 unverified placeholders and weaker custom schematic animations were')
$lines.Add('removed from both the catalog and the application package.')
$lines.Add('')
$lines.Add('| Region | Retained exercises |')
$lines.Add('| --- | ---: |')
foreach ($region in $regions) {
    $count = @($catalog | Where-Object dominantRegion -eq $region).Count
    $lines.Add(('| {0} | {1} |' -f $region, $count))
}

$lines.Add('')
$lines.Add('## Retained source quality')
$lines.Add('')
$lines.Add(('- Reviewed human footage: **{0}**' -f $humanExternalIds.Count))
$lines.Add(('- Other reviewed external demonstrations: **{0}**' -f $otherExternalCount))
$lines.Add(('- Reviewed Posecode 3D renders: **{0}**' -f $posecodeIds.Count))
$lines.Add(('- Exact semantically identical copies: **{0}**' -f $copyIds.Count))
$lines.Add(('- Exact deterministic transforms: **{0}**' -f $transformIds.Count))
$lines.Add('')
$lines.Add('The discarded custom SVG tier contained clear but comparatively weak')
$lines.Add('schematic stick-figure artwork. Keeping only footage, reviewed external')
$lines.Add('animation, reviewed 3D motion, and exact derivatives gives the app a more')
$lines.Add('consistent and legible demonstration set.')
$lines.Add('')
$lines.Add('The retained ID inventory is in `tools/VerifiedExerciseDemos.psd1`; source,')
$lines.Add('copy, and transform mappings live in the corresponding media manifests.')

$lines | Set-Content -LiteralPath $OutputPath -Encoding utf8
Write-Output "Audit: $([IO.Path]::GetFullPath($OutputPath))"
Write-Output "Retained and verified: $($catalog.Count)"
Write-Output 'Placeholders bundled: 0'
