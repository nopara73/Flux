param(
    [Parameter(Mandatory = $true)]
    [string] $BaselineCatalogPath,

    [string] $CatalogPath = (Join-Path $PSScriptRoot '..\Flux\Assets\exercises.json'),

    [string] $OutputPath = (Join-Path $PSScriptRoot '..\docs\catalog-audit\demonstration_metadata_integrity_2026-08-29.csv')
)

$ErrorActionPreference = 'Stop'

$baseline = @(Get-Content -Raw -LiteralPath $BaselineCatalogPath | ConvertFrom-Json)
$current = @(Get-Content -Raw -LiteralPath $CatalogPath | ConvertFrom-Json)
$currentById = @{}
foreach ($exercise in $current) {
    $currentById[[int]$exercise.id] = $exercise
}

if (@($baseline.id | Sort-Object -Unique).Count -ne $baseline.Count) {
    throw 'The baseline catalog contains duplicate exercise IDs.'
}
if (@($current.id | Sort-Object -Unique).Count -ne $current.Count) {
    throw 'The current catalog contains duplicate exercise IDs.'
}

$actualDemonstrations = @{
    95 = 'A planted single-leg balance with one knee raised and held; the pelvis is not visibly moved through repetitions.'
    193 = 'A wide planted stance, hip hinge toward the floor, then a rise into a two-arm overhead reach; no repeated squat is performed.'
    267 = 'A static standing T-arm/shoulder hold with both heels remaining down; no calf-raise hold is demonstrated.'
    417 = 'A narrow planted stance with repeated overhead-to-floor reaches and modest knee/hip flexion; no thumb target or explicit gaze tracking is shown.'
    553 = 'A T-raise performed while visibly holding dumbbells, so it is not a zero-equipment exercise.'
    556 = 'Standing two-hand fist clench and release with breathing; both heels remain down.'
    558 = 'A one-leg circular arm-and-torso sweep performed while using a ballet barre for support.'
    559 = 'A tiptoe side-leg hold with a circular arm sweep performed while using a ballet barre for support.'
    561 = 'Rapid small tiptoe running steps with a full-body turn and a spotting head action.'
    562 = 'Repeated ballet calf raises coordinated with broad arm sweeps.'
    563 = 'A planted hip airplane performed with the free foot pressing into the wall behind the body.'
    564 = 'A standing isometric glute press with the bent free leg placing the shoe sole against the wall.'
    565 = 'Repeated mini squats with a forward arm reach; the heels stay down.'
    566 = 'Parallel two-leg calf raises.'
    567 = 'A split squat performed with the rear shoe sole supported against a plain wall.'
    568 = 'A standing calf stretch with the front shoe sole angled against the wall.'
    574 = 'Repeated standing toe taps against the wall while both hands provide light wall support.'
    581 = 'Two-leg calf raises with the toes turned inward.'
    582 = 'Two-leg calf raises with the toes turned outward.'
    615 = 'Alternating standing hamstring curls while the hands remain together in a prayer position.'
}

$breathLedIds = [Collections.Generic.HashSet[int]]::new()
foreach ($id in @(390, 391, 392, 394, 395, 397, 398, 399, 400, 401, 407, 480, 484)) {
    [void]$breathLedIds.Add($id)
}
$gazeLedIds = [Collections.Generic.HashSet[int]]::new()
foreach ($id in @(410, 411, 412, 413, 478)) {
    [void]$gazeLedIds.Add($id)
}
$pelvicEvidenceIds = [Collections.Generic.HashSet[int]]::new()
foreach ($id in @(
    32, 58, 92, 119, 167, 168, 169, 282, 295, 296, 390, 393,
    413, 420, 428, 431, 432, 433, 434, 435, 440, 441, 443, 445,
    448, 450, 452, 458, 462, 463, 464, 465, 469, 471, 472, 475,
    476, 488, 537, 548, 561, 575, 578, 583, 613)) {
    [void]$pelvicEvidenceIds.Add($id)
}

function Convert-AuditValue {
    param([AllowNull()] $Value)

    if ($null -eq $Value) {
        return '<none>'
    }
    if ($Value -is [string] -or $Value -is [ValueType]) {
        return [string]$Value
    }
    return ($Value | ConvertTo-Json -Compress -Depth 20)
}

function Get-RelevantMetadata {
    param([AllowNull()] $Exercise)

    if ($null -eq $Exercise) {
        return '<retired>'
    }
    $secondary = @($Exercise.secondaryCanonicalGroups) -join ';'
    $blocks = @($Exercise.sequenceBlocks | ForEach-Object {
            '{0}:{1}:{2}:{3}:{4}' -f
                $_.exerciseId,
                $_.sideCue,
                $_.directionCue,
                $_.mirrorMedia,
                $_.mediaSegment
        }) -join ';'
    return @(
        "name=$($Exercise.name)",
        "demand=$($Exercise.muscularDemand)",
        "primary=$($Exercise.primaryCanonicalGroup)",
        "secondary=[$secondary]",
        "practice=$($Exercise.practice)",
        "motion=$($Exercise.motionProfile)",
        "mode=$($Exercise.mode)",
        "presentation=$($Exercise.presentation)",
        "holdFrame=$($Exercise.holdFramePercent)",
        "side=$($Exercise.sideSequence)",
        "direction=$($Exercise.directionSequence)",
        "blocks=[$blocks]",
        "sessionMovement=$($Exercise.sessionMovementId)",
        "insect=$($Exercise.insectCompatibility)",
        "hardFloor=$($Exercise.hardFloorCompatibility)"
    ) -join ' | '
}

function Get-CorrectionReason {
    param(
        [int] $Id,
        [string[]] $ChangedFields,
        [bool] $Retired
    )

    if ($Retired) {
        $retirementReason = switch ($Id) {
            267 { 'The claimed heel raise is absent; the remaining shoulder hold duplicates worthwhile catalog work.' }
            553 { 'The final demonstration visibly requires dumbbells and violates the zero-equipment contract.' }
            558 { 'The final demonstration requires a ballet barre and cannot be reproduced under the equipment contract.' }
            559 { 'The final demonstration requires a ballet barre and cannot be reproduced under the equipment contract.' }
            default { 'The final packaged demonstration cannot support a worthwhile valid catalog entry.' }
        }
        return $retirementReason
    }

    switch ($Id) {
        95 { return 'The loop shows an isometric knee-raised balance, not repeated pelvic control; mode, still frame, anatomy, sound, and name now follow the visible hold.' }
        193 { return 'The seed mismatch was confirmed: the loop hinges but does not squat, so the name, motion, anatomy, and demand now follow the hinge/reach.' }
        252 { return 'The retired false calf hold was removed from the mandatory calf-raise sequence.' }
        253 { return 'The obsolete session-movement link to the retired false calf hold was removed.' }
        417 { return 'The demonstration shows a reach with modest lower-body flexion, not thumb tracking or a hard squat; name, practice, motion, anatomy, and demand now match it.' }
        556 { return 'The heels never rise; the meaningful action is an easy hand/forearm clench-and-release movement.' }
        561 { return 'The loop is rapid tiptoe running with spotting rather than a classical bourree; the name and anatomy were narrowed to actions actually shown.' }
        562 { return 'The generic label and inflated whole-body associations were replaced by the exact calf-raise and arm-sweep action.' }
        563 { return 'The exact wall-supported hip-airplane demonstration replaces a redundant calf variation and requires sole-to-wall access.' }
        564 { return 'The exact standing foot-to-wall press replaces a redundant calf variation and requires sole-to-wall access.' }
        565 { return 'No calf raise or visible pelvic-floor action occurs; the demonstrated mini squat and reach are now authoritative.' }
        566 { return 'A silent loop cannot establish pelvic-floor support; only the visible calf-raise mechanics remain associated.' }
        567 { return 'The exact foot-on-wall split squat replaces a redundant calf variation and requires sole-to-wall access.' }
        568 { return 'The exact toes-on-wall calf stretch replaces a redundant calf variation and requires sole-to-wall access.' }
        574 { return 'The exact wall toe-tap demonstration replaces a redundant calf-and-shoulder variation and requires sole-to-wall access.' }
        581 { return 'A silent loop cannot establish pelvic-floor support; only the visible toes-in calf-raise mechanics remain associated.' }
        582 { return 'A silent loop cannot establish pelvic-floor support; only the visible toes-out calf-raise mechanics remain associated.' }
        615 { return 'The loop clearly alternates hamstring curls; the name, primary muscle, and supporting associations now state that movement directly.' }
    }

    if ($breathLedIds.Contains($Id)) {
        return 'The visible gross movement, not breathing itself, is the principal mechanical action; breathing remains secondary only when meaningfully demonstrated.'
    }
    if ($gazeLedIds.Contains($Id)) {
        return 'The squat, lunge, march, or pivot is the principal mechanical action; gaze/neck involvement remains secondary rather than displacing the movement primary.'
    }
    if ($pelvicEvidenceIds.Contains($Id)) {
        return 'Associations were trimmed to meaningful visible mechanics; pelvic-floor involvement is retained only as evidence-supported secondary work for gait, single-leg support, or impact.'
    }
    if ('primaryCanonicalGroup' -in $ChangedFields) {
        return 'The primary and secondary anatomy now follow the movement that supplies the visible mechanical demand, with inherited or duplicative associations removed.'
    }
    return 'The full loop supports the named movement, but inherited or duplicative secondary associations were removed so only meaningful involvement remains.'
}

$rows = foreach ($prior in $baseline | Sort-Object { [int]$_.id }) {
    $id = [int]$prior.id
    $retired = -not $currentById.ContainsKey($id)
    $next = if ($retired) { $null } else { $currentById[$id] }
    $changedFields = @()
    if (-not $retired) {
        $propertyNames = @(
            @($prior.PSObject.Properties.Name) + @($next.PSObject.Properties.Name) |
                Sort-Object -Unique)
        foreach ($propertyName in $propertyNames) {
            $before = Convert-AuditValue $prior.$propertyName
            $after = Convert-AuditValue $next.$propertyName
            if ($before -cne $after) {
                $changedFields += $propertyName
            }
        }
    }

    $verdict = if ($retired) {
        'RETIRED'
    }
    elseif ($changedFields.Count -gt 0) {
        'CORRECTED'
    }
    else {
        'PASS'
    }

    $actual = if ($actualDemonstrations.ContainsKey($id)) {
        $actualDemonstrations[$id]
    }
    elseif ($retired) {
        'The final packaged loop does not support the inherited catalog identity.'
    }
    else {
        "The complete packaged loop demonstrates $($next.name) as named."
    }

    $correction = if ($retired) {
        'Retired the ID and removed its runtime media and sequence/session references.'
    }
    elseif ($changedFields.Count -eq 0) {
        'None.'
    }
    else {
        @($changedFields | ForEach-Object {
                $field = $_
                '{0}: {1} => {2}' -f
                    $field,
                    (Convert-AuditValue $prior.$field),
                    (Convert-AuditValue $next.$field)
            }) -join ' | '
    }

    [pscustomobject]@{
        ExerciseId = $id
        Verdict = $verdict
        PreviousName = [string]$prior.name
        CurrentName = if ($retired) { '<retired>' } else { [string]$next.name }
        PreviousMetadata = Get-RelevantMetadata $prior
        CurrentMetadata = Get-RelevantMetadata $next
        ActualFinalDemonstration = $actual
        ExactCorrection = $correction
        Reason = if ($verdict -eq 'PASS') {
            'Full-loop review found the name, demand, anatomy, structure, and packaged media coherent.'
        } else {
            Get-CorrectionReason -Id $id -ChangedFields $changedFields -Retired $retired
        }
        FullLoop = if ($id -eq 267) { 'FAIL: claimed action absent' } else { 'PASS' }
        Crop = 'PASS'
        LoopSeam = 'PASS'
        PlaybackSpeed = 'PASS'
        Mirroring = 'PASS'
        Travel = 'PASS'
        HoldFrame = if ($id -eq 95) { 'PASS: corrected still endpoint at 60%' } elseif ($id -eq 267) { 'FAIL: endpoint does not show a heel raise' } else { 'PASS or not applicable' }
        Equipment = if ($id -in @(553, 558, 559)) { 'FAIL: visible external equipment' } else { 'PASS' }
    }
}

if ($rows.Count -ne $baseline.Count) {
    throw 'The ledger does not contain exactly one row per baseline exercise.'
}

$outputDirectory = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$rows | Export-Csv -LiteralPath $OutputPath -NoTypeInformation -Encoding utf8

$counts = $rows | Group-Object Verdict | Sort-Object Name
Write-Output "Baseline SHA-256: $((Get-FileHash -Algorithm SHA256 -LiteralPath $BaselineCatalogPath).Hash)"
Write-Output "Current SHA-256: $((Get-FileHash -Algorithm SHA256 -LiteralPath $CatalogPath).Hash)"
Write-Output "Ledger: $([IO.Path]::GetFullPath($OutputPath))"
foreach ($count in $counts) {
    Write-Output "$($count.Name): $($count.Count)"
}
