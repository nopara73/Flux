param(
    [string]$OutputRoot = (Join-Path $PSScriptRoot '..\Flux\Assets'),
    [ValidateRange(0, 1000)]
    [int]$MaxExercises = 0,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$regions = @(
    'FEET',
    'LEGS',
    'HANDS',
    'ARMS',
    'HEAD',
    'SHOULDERS',
    'HIPS',
    'CHEST',
    'BACK',
    'CORE'
)

$actions = [ordered]@{
    FEET = @(
        'Heel-to-Toe Weight Shift',
        'Side-to-Side Foot Pressure Shift',
        'Alternating Heel Raise',
        'Alternating Forefoot Raise',
        'Ankle Circle Balance',
        'Staggered-Stance Foot Rock',
        'Single-Foot Hover',
        'Quiet Step-and-Return',
        'Diagonal Foot Tap',
        'Clock-Face Foot Tap'
    )
    LEGS = @(
        'Standing Knee Bend',
        'Alternating Knee Lift',
        'Standing Hamstring Curl',
        'Side Leg Lift',
        'Back Leg Reach',
        'Front Leg Extension',
        'Shallow Split Squat',
        'Lateral Weight Transfer',
        'Controlled March in Place',
        'Skater Step Without Hop'
    )
    HANDS = @(
        'Finger Fan and Close',
        'Alternating Fist Open',
        'Thumb-to-Finger Sequence',
        'Wrist Circle with Open Hands',
        'Palm Turnover',
        'Finger Wave',
        'Quiet Hand Shakeout',
        'Prayer-Hand Press',
        'Interlaced-Finger Stretch',
        'Air-Piano Finger Pattern'
    )
    ARMS = @(
        'Forward Arm Circle',
        'Backward Arm Circle',
        'Alternating Front Reach',
        'Overhead Arm Sweep',
        'Side Arm Raise',
        'Elbow Flex and Extend',
        'Cross-Body Arm Sweep',
        'Standing Shadow Punch',
        'Overhead Triceps Reach',
        'Figure-Eight Arm Flow'
    )
    HEAD = @(
        'Gentle Head Turn',
        'Side Head Tilt',
        'Chin Tuck',
        'Up-and-Down Gaze',
        'Diagonal Gaze Sweep',
        'Eye-Tracking Circle',
        'Head-Led Figure Eight',
        'Ear-to-Shoulder Hover',
        'Nose-Drawn Square',
        'Horizon Scan'
    )
    SHOULDERS = @(
        'Forward Shoulder Roll',
        'Backward Shoulder Roll',
        'Shoulder Shrug and Release',
        'Scapular Squeeze',
        'Alternating Shoulder Lift',
        'Shoulder-Blade Glide',
        'Elbow-Led Shoulder Circle',
        'Overhead Shoulder Reach',
        'Cross-Body Shoulder Reach',
        'Standing Y-to-W Arm Flow'
    )
    HIPS = @(
        'Hip Circle',
        'Standing Hip Hinge',
        'Side Hip Shift',
        'Front-to-Back Hip Shift',
        'Standing Pelvic Clock',
        'Standing Hip Abduction',
        'Standing Hip Extension',
        'Knee-Led Hip Circle',
        'Figure-Eight Hip Flow',
        'Staggered-Stance Hip Rock'
    )
    CHEST = @(
        'Standing Chest Open and Close',
        'Palm-to-Palm Chest Press',
        'Chest-Led Forward Pulse',
        'Ribcage Side Expansion',
        'Standing Fly Motion',
        'Diagonal Chest Reach',
        'Standing Chest Rotation',
        'Elbow-Back Chest Opener',
        'Hands-Behind-Head Chest Open',
        'Standing Push-Pull Mime'
    )
    BACK = @(
        'Standing Spine Roll',
        'Gentle Standing Back Extension',
        'Standing Torso Rotation',
        'Standing Side Bend',
        'Hip-Hinge Back Sweep',
        'Shoulder-Blade Back Reach',
        'Standing Bird-Dog',
        'Diagonal Backline Reach',
        'Upper-Back Round and Open',
        'Standing Swimmer Arms'
    )
    CORE = @(
        'Standing Knee-to-Elbow',
        'Standing Cross-Body Crunch',
        'Standing Side Crunch',
        'Tall-Stance Brace Hold',
        'Standing Pelvic Tilt',
        'Slow Torso Twist',
        'Standing Contralateral Reach',
        'Single-Leg Core Balance',
        'Standing Oblique Reach',
        'Controlled High-Knee Hold'
    )
}

$variants = @(
    'Slow Tempo',
    'Four-Count Tempo',
    'End-Range Pause',
    'Half Range',
    'Full Range',
    'Left Lead',
    'Right Lead',
    'Alternating Rhythm',
    'Precision Repetitions',
    'Continuous Flow'
)

$regionColors = @(
    '#00A896',
    '#0096C7',
    '#7B61FF',
    '#E76F51',
    '#F4A261',
    '#2A9D8F',
    '#E9C46A',
    '#E63946',
    '#457B9D',
    '#6A994E'
)

function Get-RoundedInt {
    param([double]$Value)
    return [int][Math]::Round($Value, [MidpointRounding]::AwayFromZero)
}

function New-ExerciseFrameSvg {
    param(
        [int]$ExerciseId,
        [int]$RegionIndex,
        [int]$ActionIndex,
        [int]$VariantIndex,
        [int]$FrameIndex
    )

    $phase = (2 * [Math]::PI * $FrameIndex / 8) + ($ActionIndex * 0.17)
    $wave = [Math]::Sin($phase)
    $counterWave = [Math]::Cos($phase)
    $amplitude = 5.0 + ($VariantIndex * 0.55) + (($ActionIndex % 3) * 1.2)
    $mode = $ActionIndex % 5

    $headX = 128
    $headY = 43
    $noseOffsetX = 15
    $noseOffsetY = 2
    $leftShoulderX = 102
    $leftShoulderY = 79
    $rightShoulderX = 154
    $rightShoulderY = 79
    $leftElbowX = 91
    $leftElbowY = 116
    $rightElbowX = 165
    $rightElbowY = 116
    $leftHandX = 85
    $leftHandY = 151
    $rightHandX = 171
    $rightHandY = 151
    $hipX = 128
    $hipY = 134
    $leftKneeX = 106
    $leftKneeY = 176
    $rightKneeX = 150
    $rightKneeY = 176
    $leftFootX = 92
    $leftFootY = 220
    $rightFootX = 164
    $rightFootY = 220

    switch ($regions[$RegionIndex]) {
        'FEET' {
            if ($mode -in @(0, 3)) {
                $leftFootX += Get-RoundedInt ($wave * $amplitude)
                $rightFootX -= Get-RoundedInt ($wave * $amplitude)
            }
            elseif ($mode -in @(1, 4)) {
                $lift = [Math]::Max(0, $wave) * $amplitude * 2.2
                $leftFootY -= Get-RoundedInt $lift
                $leftKneeY -= Get-RoundedInt ($lift * 0.45)
            }
            else {
                $leftFootX += Get-RoundedInt ($counterWave * $amplitude)
                $rightFootX += Get-RoundedInt ($wave * $amplitude)
            }
        }
        'LEGS' {
            if ($mode -in @(0, 3)) {
                $bend = ([Math]::Abs($wave) * $amplitude * 1.8)
                $hipY += Get-RoundedInt $bend
                $leftKneeY += Get-RoundedInt ($bend * 0.35)
                $rightKneeY += Get-RoundedInt ($bend * 0.35)
            }
            else {
                $lift = [Math]::Max(0, $wave) * $amplitude * 3.1
                $leftKneeY -= Get-RoundedInt $lift
                $leftFootY -= Get-RoundedInt ($lift * 0.75)
                $leftKneeX += Get-RoundedInt ($counterWave * $amplitude)
            }
        }
        'HANDS' {
            $leftHandX += Get-RoundedInt ($counterWave * $amplitude * 1.5)
            $leftHandY += Get-RoundedInt ($wave * $amplitude)
            $rightHandX -= Get-RoundedInt ($counterWave * $amplitude * 1.5)
            $rightHandY -= Get-RoundedInt ($wave * $amplitude)
        }
        'ARMS' {
            $leftElbowY -= Get-RoundedInt ($wave * $amplitude * 1.4)
            $rightElbowY += Get-RoundedInt ($wave * $amplitude * 1.4)
            $leftHandX += Get-RoundedInt ($counterWave * $amplitude * 2.2)
            $leftHandY -= Get-RoundedInt ($wave * $amplitude * 2.5)
            $rightHandX -= Get-RoundedInt ($counterWave * $amplitude * 2.2)
            $rightHandY += Get-RoundedInt ($wave * $amplitude * 2.5)
        }
        'HEAD' {
            $headX += Get-RoundedInt ($wave * $amplitude * 1.2)
            $headY += Get-RoundedInt ($counterWave * $amplitude * 0.55)
            $noseOffsetX = Get-RoundedInt (15 + ($counterWave * 3))
            $noseOffsetY = Get-RoundedInt ($wave * 7)
        }
        'SHOULDERS' {
            $leftShoulderY += Get-RoundedInt ($wave * $amplitude)
            $rightShoulderY -= Get-RoundedInt ($wave * $amplitude)
            $leftElbowY += Get-RoundedInt ($wave * $amplitude)
            $rightElbowY -= Get-RoundedInt ($wave * $amplitude)
        }
        'HIPS' {
            $hipX += Get-RoundedInt ($wave * $amplitude * 1.5)
            $hipY += Get-RoundedInt ($counterWave * $amplitude * 0.65)
            $leftKneeX += Get-RoundedInt ($wave * $amplitude * 0.8)
            $rightKneeX += Get-RoundedInt ($wave * $amplitude * 0.8)
        }
        'CHEST' {
            $spread = Get-RoundedInt ($wave * $amplitude)
            $leftShoulderX -= $spread
            $rightShoulderX += $spread
            $leftElbowX -= $spread
            $rightElbowX += $spread
        }
        'BACK' {
            $lean = Get-RoundedInt ($wave * $amplitude * 1.35)
            $leftShoulderX += $lean
            $rightShoulderX += $lean
            $headX += $lean
            $leftHandY -= Get-RoundedInt ($counterWave * $amplitude)
            $rightHandY += Get-RoundedInt ($counterWave * $amplitude)
        }
        'CORE' {
            $twist = Get-RoundedInt ($wave * $amplitude * 1.2)
            $leftShoulderX += $twist
            $rightShoulderX += $twist
            $hipX -= Get-RoundedInt ($wave * $amplitude * 0.7)
            if ($mode -in @(0, 1, 4)) {
                $lift = [Math]::Max(0, $counterWave) * $amplitude * 2.3
                $rightKneeY -= Get-RoundedInt $lift
                $rightFootY -= Get-RoundedInt ($lift * 0.72)
            }
        }
    }

    $torsoTopX = Get-RoundedInt (($leftShoulderX + $rightShoulderX) / 2)
    $torsoTopY = Get-RoundedInt (($leftShoulderY + $rightShoulderY) / 2)
    $leftHipX = $hipX - 12
    $rightHipX = $hipX + 12
    $accent = $regionColors[$RegionIndex]
    $body = '#17324D'
    $skin = '#F4A261'
    $muted = '#C9D7E3'

    $feetColor = if ($RegionIndex -eq 0) { $accent } else { $body }
    $legsColor = if ($RegionIndex -eq 1) { $accent } else { $body }
    $handsColor = if ($RegionIndex -eq 2) { $accent } else { $skin }
    $armsColor = if ($RegionIndex -eq 3) { $accent } else { $body }
    $headColor = if ($RegionIndex -eq 4) { $accent } else { $skin }
    $shoulderColor = if ($RegionIndex -eq 5) { $accent } else { $body }
    $hipColor = if ($RegionIndex -eq 6) { $accent } else { $body }
    $chestColor = if ($RegionIndex -eq 7) { $accent } else { $body }
    $backColor = if ($RegionIndex -eq 8) { $accent } else { $body }
    $coreColor = if ($RegionIndex -eq 9) { $accent } else { $body }

    return @"
<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
  <metadata>Flux exercise $ExerciseId frame $FrameIndex</metadata>
  <rect width="256" height="256" rx="28" fill="#F7FAFC" />
  <circle cx="128" cy="126" r="102" fill="$accent" opacity="0.08" />
  <line x1="40" y1="224" x2="216" y2="224" stroke="$muted" stroke-width="4" stroke-linecap="round" />
  <polyline points="$leftHipX,$hipY $leftKneeX,$leftKneeY $leftFootX,$leftFootY" fill="none" stroke="$legsColor" stroke-width="12" stroke-linecap="round" stroke-linejoin="round" />
  <polyline points="$rightHipX,$hipY $rightKneeX,$rightKneeY $rightFootX,$rightFootY" fill="none" stroke="$legsColor" stroke-width="12" stroke-linecap="round" stroke-linejoin="round" />
  <line x1="$($leftFootX - 12)" y1="$leftFootY" x2="$($leftFootX + 9)" y2="$leftFootY" stroke="$feetColor" stroke-width="9" stroke-linecap="round" />
  <line x1="$($rightFootX - 12)" y1="$rightFootY" x2="$($rightFootX + 9)" y2="$rightFootY" stroke="$feetColor" stroke-width="9" stroke-linecap="round" />
  <line x1="$torsoTopX" y1="$torsoTopY" x2="$hipX" y2="$hipY" stroke="$backColor" stroke-width="15" stroke-linecap="round" />
  <line x1="$torsoTopX" y1="$($torsoTopY + 15)" x2="$hipX" y2="$($hipY - 8)" stroke="$coreColor" stroke-width="9" stroke-linecap="round" opacity="0.95" />
  <line x1="$leftShoulderX" y1="$leftShoulderY" x2="$rightShoulderX" y2="$rightShoulderY" stroke="$chestColor" stroke-width="13" stroke-linecap="round" />
  <circle cx="$leftShoulderX" cy="$leftShoulderY" r="8" fill="$shoulderColor" />
  <circle cx="$rightShoulderX" cy="$rightShoulderY" r="8" fill="$shoulderColor" />
  <polyline points="$leftShoulderX,$leftShoulderY $leftElbowX,$leftElbowY $leftHandX,$leftHandY" fill="none" stroke="$armsColor" stroke-width="10" stroke-linecap="round" stroke-linejoin="round" />
  <polyline points="$rightShoulderX,$rightShoulderY $rightElbowX,$rightElbowY $rightHandX,$rightHandY" fill="none" stroke="$armsColor" stroke-width="10" stroke-linecap="round" stroke-linejoin="round" />
  <circle cx="$leftHandX" cy="$leftHandY" r="7" fill="$handsColor" />
  <circle cx="$rightHandX" cy="$rightHandY" r="7" fill="$handsColor" />
  <circle cx="$hipX" cy="$hipY" r="10" fill="$hipColor" />
  <line x1="$torsoTopX" y1="$($torsoTopY - 3)" x2="$headX" y2="$($headY + 16)" stroke="$body" stroke-width="8" stroke-linecap="round" />
  <circle cx="$headX" cy="$headY" r="18" fill="$headColor" />
  <line x1="$headX" y1="$headY" x2="$($headX + $noseOffsetX)" y2="$($headY + $noseOffsetY)" stroke="$body" stroke-width="4" stroke-linecap="round" />
</svg>
"@
}

$resolvedOutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$gifOutputRoot = Join-Path $resolvedOutputRoot 'exercise_gifs'
$catalogPath = Join-Path $resolvedOutputRoot 'exercises.json'
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("FluxExerciseFrames-" + [Guid]::NewGuid().ToString('N'))

New-Item -ItemType Directory -Force -Path $resolvedOutputRoot | Out-Null
New-Item -ItemType Directory -Force -Path $gifOutputRoot | Out-Null
New-Item -ItemType Directory -Path $tempRoot | Out-Null

$records = [System.Collections.Generic.List[object]]::new(1000)

for ($regionIndex = 0; $regionIndex -lt $regions.Count; $regionIndex++) {
    $region = $regions[$regionIndex]

    if ($actions[$region].Count -ne 10) {
        throw "$region must define exactly 10 base movements."
    }

    for ($actionIndex = 0; $actionIndex -lt 10; $actionIndex++) {
        for ($variantIndex = 0; $variantIndex -lt 10; $variantIndex++) {
            $exerciseId = ($regionIndex * 100) + ($actionIndex * 10) + $variantIndex + 1
            $gifFileName = 'exercise_{0:D4}.gif' -f $exerciseId
            $gifRelativePath = "exercise_gifs/$gifFileName"

            $records.Add([ordered]@{
                id = $exerciseId
                name = "$($actions[$region][$actionIndex]) — $($variants[$variantIndex])"
                gif = $gifRelativePath
                dominantRegion = $region
                score = 0
                onlyFeetTouchGround = $true
                shoeAgnostic = $true
                maxSpaceMeters = 3
                equipment = 'None'
                silent = $true
            })

            if ($MaxExercises -gt 0 -and $exerciseId -gt $MaxExercises) {
                continue
            }

            $gifPath = Join-Path $gifOutputRoot $gifFileName

            if ((Test-Path -LiteralPath $gifPath) -and -not $Force) {
                continue
            }

            $framePaths = @()

            for ($frameIndex = 0; $frameIndex -lt 8; $frameIndex++) {
                $framePath = Join-Path $tempRoot ('frame_{0:D2}.svg' -f $frameIndex)
                $svg = New-ExerciseFrameSvg `
                    -ExerciseId $exerciseId `
                    -RegionIndex $regionIndex `
                    -ActionIndex $actionIndex `
                    -VariantIndex $variantIndex `
                    -FrameIndex $frameIndex
                Set-Content -LiteralPath $framePath -Value $svg -Encoding utf8
                $framePaths += $framePath
            }

            $delay = @(18, 14, 20, 16, 11, 15, 15, 13, 17, 10)[$variantIndex]
            $magickArguments = @($framePaths) + @(
                '-set', 'delay', $delay.ToString(),
                '-set', 'dispose', 'background',
                '-set', 'comment', "Flux exercise $exerciseId",
                '-loop', '0',
                '-layers', 'Optimize',
                $gifPath
            )

            & magick @magickArguments

            if ($LASTEXITCODE -ne 0) {
                throw "ImageMagick failed while generating $gifFileName."
            }

            if ($exerciseId % 50 -eq 0) {
                Write-Output "Generated $exerciseId / 1000 exercise GIFs"
            }
        }
    }
}

if ($records.Count -ne 1000) {
    throw "Expected 1000 exercise records but generated $($records.Count)."
}

$duplicateNames = $records | Group-Object { $_['name'] } | Where-Object Count -ne 1
$duplicateGifs = $records | Group-Object { $_['gif'] } | Where-Object Count -ne 1
$duplicateIds = $records | Group-Object { $_['id'] } | Where-Object Count -ne 1
$invalidRegionCounts = $records |
    Group-Object { $_['dominantRegion'] } |
    Where-Object Count -ne 100
$constraintViolations = $records | Where-Object {
    -not $_['onlyFeetTouchGround'] -or
    -not $_['shoeAgnostic'] -or
    $_['maxSpaceMeters'] -le 0 -or
    $_['maxSpaceMeters'] -gt 3 -or
    $_['equipment'] -ne 'None' -or
    -not $_['silent'] -or
    $_['score'] -ne 0
}

if ($duplicateNames -or $duplicateGifs -or $duplicateIds -or
    $invalidRegionCounts -or $constraintViolations) {
    throw 'The generated catalog failed its IDs, uniqueness, region, or constraint checks.'
}

if ($MaxExercises -eq 0) {
    $missingGifs = $records | Where-Object {
        -not (Test-Path -LiteralPath (Join-Path $resolvedOutputRoot $_['gif']))
    }

    if ($missingGifs) {
        throw 'At least one catalog record is missing its GIF asset.'
    }
}

$records | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $catalogPath -Encoding utf8

Get-ChildItem -LiteralPath $tempRoot -File | Remove-Item -Force
Remove-Item -LiteralPath $tempRoot

Write-Output "Catalog: $catalogPath"
Write-Output "Records: $($records.Count)"
Write-Output "GIF directory: $gifOutputRoot"
