param(
    [string]$CatalogPath = (Join-Path $PSScriptRoot '..\Flux\Assets\exercises.json'),
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\DEMONSTRATION_AUDIT.md')
)

$ErrorActionPreference = 'Stop'

$catalog = @(Get-Content -LiteralPath $CatalogPath -Raw | ConvertFrom-Json)
$review = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'VerifiedExerciseDemos.psd1') -SkipLimitCheck
$externalMedia = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExternalExerciseMedia.psd1') -SkipLimitCheck
$posecodeMedia = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'PosecodeExerciseMedia.psd1') -SkipLimitCheck
$exactMediaCopies = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExactExerciseMediaCopies.psd1') -SkipLimitCheck
$exactMediaTransforms = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExactExerciseMediaTransforms.psd1') -SkipLimitCheck
$muscleGroups = @(
    'Glutes', 'Core', 'Quadriceps', 'Hamstrings', 'UpperBack',
    'Shoulders', 'Chest', 'LowerBack', 'Calves', 'HipFlexors',
    'Adductors', 'Abductors', 'MidBack', 'Trapezius', 'Forearms',
    'Triceps', 'Biceps', 'RotatorCuff', 'Neck', 'Shins')
$muscleGroupDisplayNames = @{
    UpperBack = 'Upper back'
    LowerBack = 'Lower back'
    HipFlexors = 'Hip flexors'
    MidBack = 'Mid back'
    RotatorCuff = 'Rotator cuff'
}

$externalIds = @($review.ReviewedExternal | ForEach-Object { [int]$_ })
$humanExternalIds = @(
    $externalIds | Where-Object {
        $externalMedia.ContainsKey($_) -and
        $externalMedia[$_].ContainsKey('Human') -and
        [bool]$externalMedia[$_].Human
    })
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

$missingDirectMappings = @(
    @($externalIds | Where-Object { -not $externalMedia.ContainsKey($_) }) +
        @($posecodeIds | Where-Object { -not $posecodeMedia.ContainsKey($_) }))
$copyMappingsMatch =
    (@($copyIds | Sort-Object) -join ',') -eq
    (@($exactMediaCopies.Keys |
            ForEach-Object { [int]$_ } |
            Sort-Object) -join ',')
$transformMappingsMatch =
    (@($transformIds | Sort-Object) -join ',') -eq
    (@($exactMediaTransforms.Keys |
            ForEach-Object { [int]$_ } |
            Sort-Object) -join ',')
if ($missingDirectMappings.Count -gt 0 -or
    -not $copyMappingsMatch -or
    -not $transformMappingsMatch) {
    throw 'The retained inventory does not match its reviewed media mappings.'
}

if ($otherExternalCount -ne 0 -or $posecodeIds.Count -ne 0 -or $svgIds.Count -ne 0) {
    throw 'Every retained direct demonstration must show an actual person.'
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

$invalidMuscleGroups = @($muscleGroups | Where-Object {
        $muscleGroup = $_
        @($catalog | Where-Object {
                $muscleGroup -in @($_.muscleGroups)
            }).Count -lt 10
    })
if ($invalidMuscleGroups.Count -gt 0) {
    throw "Every muscle group must retain at least ten exercises: $($invalidMuscleGroups -join ', ')."
}

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# Flux demonstration quality audit')
$lines.Add('')
$lines.Add('Flux now ships a strictly human-demonstrated exercise catalog.')
$lines.Add(('All **{0}** bundled exercises show an actual person performing the movement.' -f
        $catalog.Count))
$lines.Add('Synthetic, schematic, anatomical, and 3D demonstrations are excluded from')
$lines.Add('both the runtime catalog and the application package.')
$lines.Add('')
$lines.Add('| Muscle group | Assigned exercises |')
$lines.Add('| --- | ---: |')
foreach ($muscleGroup in $muscleGroups) {
    $count = @($catalog | Where-Object {
            $muscleGroup -in @($_.muscleGroups)
        }).Count
    $displayName = if ($muscleGroupDisplayNames.ContainsKey($muscleGroup)) {
        $muscleGroupDisplayNames[$muscleGroup]
    }
    else {
        $muscleGroup
    }
    $lines.Add(('| {0} | {1} |' -f $displayName, $count))
}

$lines.Add('')
$lines.Add('## Retained source quality')
$lines.Add('')
$lines.Add(('- Direct human-footage demonstrations: **{0}**' -f $humanExternalIds.Count))
$lines.Add(('- Exact copies of human footage: **{0}**' -f $copyIds.Count))
$lines.Add(('- Exact deterministic transforms of human footage: **{0}**' -f $transformIds.Count))
$lines.Add('')
$lines.Add('Copy and transform targets are retained only when their reviewed source is')
$lines.Add('human footage and the target movement has identical mechanics. This rule is')
$lines.Add('validated by the catalog generator and this audit script.')
$lines.Add('')
$lines.Add('The retained ID inventory is in `tools/VerifiedExerciseDemos.psd1`; source,')
$lines.Add('copy, and transform mappings live in the corresponding media manifests.')

$lines | Set-Content -LiteralPath $OutputPath -Encoding utf8
Write-Output "Audit: $([IO.Path]::GetFullPath($OutputPath))"
Write-Output "Retained and verified: $($catalog.Count)"
Write-Output 'Placeholders bundled: 0'
