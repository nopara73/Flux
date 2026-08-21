param(
    [string]$ReviewPath = (
        Join-Path $PSScriptRoot 'ExerciseMirrorRelationships.psd1'),
    [string]$CatalogPath = (
        [System.IO.Path]::Combine(
            $PSScriptRoot,
            '..',
            'Flux',
            'Assets',
            'exercises.json'))
)

$ErrorActionPreference = 'Stop'

$requiredBenefitsGreatlyCriteria = @(
    'TechnicalMartialArts'
    'DanceAndAlignmentSensitivePoses'
    'ComplexSingleLegAlignment'
    'LivePlaneOrSymmetryCorrection'
)

$review = Import-PowerShellDataFile -LiteralPath $ReviewPath -SkipLimitCheck
$benefitsGreatlyByCriterion = $review.BenefitsGreatlyByCriterion
if ($benefitsGreatlyByCriterion -isnot [System.Collections.IDictionary] -or
    @(Compare-Object `
            $requiredBenefitsGreatlyCriteria `
            @($benefitsGreatlyByCriterion.Keys)).Count -gt 0) {
    throw 'BenefitsGreatly exercises must use exactly the approved narrow audit criteria.'
}

$benefitsGreatlyIds = @(
    foreach ($criterion in $requiredBenefitsGreatlyCriteria) {
        $criterionIds = @(
            $benefitsGreatlyByCriterion[$criterion] |
                ForEach-Object { [int]$_ })
        if ($criterionIds.Count -eq 0) {
            throw "BenefitsGreatly criterion '$criterion' must not be empty."
        }
        $criterionIds
    })

$reviewedIdsByRelationship = [ordered]@{
    MirrorOnly = @($review.MirrorOnly | ForEach-Object { [int]$_ })
    BenefitsGreatly = $benefitsGreatlyIds
    Agnostic = @($review.Agnostic | ForEach-Object { [int]$_ })
}
$allReviewedIds = @(
    $reviewedIdsByRelationship.Values |
        ForEach-Object { $_ })
if ($allReviewedIds.Count -ne @($allReviewedIds | Sort-Object -Unique).Count) {
    throw 'Every exercise must appear exactly once in the mirror audit.'
}

$catalog = @(Get-Content -LiteralPath $CatalogPath -Raw | ConvertFrom-Json)
$catalogIds = @($catalog | ForEach-Object { [int]$_.id })
if (@(Compare-Object `
        @($allReviewedIds | Sort-Object) `
        @($catalogIds | Sort-Object)).Count -gt 0) {
    throw 'The mirror audit and packaged exercise catalog do not cover the same IDs.'
}

foreach ($relationship in $reviewedIdsByRelationship.Keys) {
    $reviewedRelationshipIds = @(
        $reviewedIdsByRelationship[$relationship] | Sort-Object)
    $catalogRelationshipIds = @(
        $catalog |
            Where-Object { $_.mirrorRelationship -eq $relationship } |
            ForEach-Object { [int]$_.id } |
            Sort-Object)
    if (@(Compare-Object `
            $reviewedRelationshipIds `
            $catalogRelationshipIds).Count -gt 0) {
        throw "The '$relationship' review does not match the packaged catalog."
    }
}

$invalidEquipment = @(
    $catalog | Where-Object {
        ($_.mirrorRelationship -eq 'MirrorOnly' -and $_.equipment -ne 'Mirror') -or
        ($_.mirrorRelationship -ne 'MirrorOnly' -and $_.equipment -ne 'None')
    })
if ($invalidEquipment.Count -gt 0) {
    throw 'Mirror equipment and relationship metadata contradict each other.'
}

Write-Output (
    'Mirror audit valid: {0} MirrorOnly, {1} BenefitsGreatly, {2} Agnostic.' -f
        $reviewedIdsByRelationship.MirrorOnly.Count,
        $reviewedIdsByRelationship.BenefitsGreatly.Count,
        $reviewedIdsByRelationship.Agnostic.Count)
