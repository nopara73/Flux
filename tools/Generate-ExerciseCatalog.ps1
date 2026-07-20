param(
    [string]$OutputRoot = (Join-Path $PSScriptRoot '..\Flux\Assets'),
    [ValidateRange(1, 1000)]
    [int]$StartExercise = 1,
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

$catalogNames = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'RealExerciseCatalog.psd1')

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

function Get-Practice {
    param([string]$Name)

    switch -Regex ($Name) {
        '^Tai Chi' { return 'Tai Chi' }
        '^Qigong|^Eight Brocades|^Five-Animals' { return 'Qigong' }
        '^Bagua' { return 'Baguazhang' }
        '^Xingyi' { return 'Xingyiquan' }
        '^Karate' { return 'Karate' }
        '^Wing Chun' { return 'Wing Chun' }
        '^Boxing|^Lead Hook|^Rear Hook|^Lead Uppercut|^Rear Uppercut|^Shovel Hook|^Overhand Punch|^Long-Guard|^High-Guard|^Peekaboo|^Body Jab|^Body Cross' { return 'Boxing' }
        '^Capoeira' { return 'Capoeira' }
        '^Taekwondo' { return 'Taekwondo' }
        '^Muay-Thai' { return 'Muay Thai' }
        '^Fencing' { return 'Fencing' }
        '^Kalaripayattu' { return 'Kalaripayattu' }
        '^Ballet|^Pirouette' { return 'Ballet' }
        '^Belly-Dance|^Egyptian-Dance' { return 'Belly dance' }
        '^Salsa|^Rumba|^Cha-Cha|^Mambo|^Bachata|^Merengue|^Samba|^Foxtrot|^Waltz|^Ballroom|^Tango' { return 'Partner-dance technique' }
        '^Flamenco' { return 'Flamenco' }
        '^Bharatanatyam' { return 'Bharatanatyam' }
        '^Odissi' { return 'Odissi' }
        '^Kathak' { return 'Kathak' }
        '^Graham' { return 'Graham technique' }
        '^Horton' { return 'Horton technique' }
        'Mudra$|Drishti$|Pose$|^Warrior|^Yoga|Garudasana|Gomukhasana|Upward Salute|Extended Mountain|Humble Warrior|Crescent Lunge|Half Moon|Dancer|Tree-Pose|Eagle-Pose|Goddess-Pose|One-Legged Mountain' { return 'Yoga' }
        'VOR|Saccade|Smooth Pursuit|Gaze Stabilization|Gaze Shift|Near-Far Focus|Convergence|Peripheral-Awareness' { return 'Oculomotor and vestibular rehabilitation' }
        'Tendon|Finger|Thumb|Wrist|Hand|Fist|Palm|Prayer' { return 'Hand therapy and mobility' }
        default { return 'Standing mobility and movement practice' }
    }
}

function Get-MotionProfile {
    param(
        [string]$Region,
        [string]$Name
    )

    switch ($Region) {
        'FEET' {
            if ($Name -match 'Ankle.*(Circle|Rotation|Alphabet)') { return 'AnkleCircle' }
            if ($Name -match 'Heel Raise|Plantarflexion') { return 'HeelRaise' }
            if ($Name -match 'Forefoot|Dorsiflexion') { return 'ForefootRaise' }
            if ($Name -match '^Ankle') { return 'AnkleSweep' }
            if ($Name -match 'Balance|Stance|Reach|Pendulum') { return 'FootBalance' }
            if ($Name -match 'Weight Shift|Foot Rock|Tripod') { return 'WeightShift' }
            if ($Name -match 'Turn|Pivot|Circle Walk|Kou Bu|Bai Bu') { return 'TurnStep' }
            if ($Name -match 'Cross|Grapevine|Carioca|Pas de Bourree') { return 'CrossStep' }
            if ($Name -match 'Side|L-Step|T-Step') { return 'SideStep' }
            return 'WalkingStep'
        }
        'LEGS' {
            if ($Name -match 'Squat|Plie|Chair Pose|Goddess|Horse|Duck Walk') { return 'Squat' }
            if ($Name -match 'Lunge|Warrior I|Warrior II|Side Angle') { return 'Lunge' }
            if ($Name -match 'Hamstring Curl|Heel Flick') { return 'KneeCurl' }
            if ($Name -match 'Knee|March|Passe|Retire') { return 'KneeLift' }
            if ($Name -match 'Side|Abduction|Adduction|a la Seconde') { return 'LegSide' }
            if ($Name -match 'Back|Extension|Derriere|Arabesque|Attitude') { return 'LegBack' }
            if ($Name -match 'Kick|Front|Flexion|Devant|Tendu|Degage|Toy Soldier') { return 'LegFront' }
            return 'LegBalance'
        }
        'HANDS' {
            if ($Name -match 'Wrist|Pronation|Supination|Turnover') { return 'WristMotion' }
            if ($Name -match 'Thumb') { return 'ThumbMotion' }
            if ($Name -match 'Isometric|Press|Stretch|Pull-Apart') { return 'HandIsometric' }
            if ($Name -match 'Mudra$') { return 'Mudra' }
            if ($Name -match 'Wing Chun|Karate|Claw|Beak|Fist Formation|Spear|Knife|Ridge') { return 'MartialHand' }
            return 'FingerMotion'
        }
        'ARMS' {
            if ($Name -match 'Circle|Figure Eight|Windmill') { return 'ArmCircle' }
            if ($Name -match 'Punch|Jab|Cross$|Zuki|Straight') { return 'StraightPunch' }
            if ($Name -match 'Hook|Uppercut|Empi|Uraken') { return 'BentArmStrike' }
            if ($Name -match 'Uke|Parry|Catch|Guard|Sau') { return 'ArmBlock' }
            if ($Name -match 'Tai Chi|Qigong') { return 'FlowingArms' }
            if ($Name -match 'Port de Bras|Allonge|Arabesque') { return 'PortDeBras' }
            if ($Name -match 'Backstroke|Freestyle|Breaststroke|Butterfly|Kayak|Ski|Skater') { return 'SportArmCycle' }
            if ($Name -match 'Raise|Reach|Overhead') { return 'ArmRaise' }
            if ($Name -match 'Curl|Flexion|Extension|Triceps') { return 'ElbowMotion' }
            return 'ArmSweep'
        }
        'HEAD' {
            if ($Name -match 'Flexion|Extension|Nod|Kampita') { return 'HeadNod' }
            if ($Name -match 'Rotation|Turn|Looks Back|Gazes Back|Paravritta|Spotting|Flick') { return 'HeadTurn' }
            if ($Name -match 'Lateral|Tilt|Ear-to|Griva|Parivahita') { return 'HeadTilt' }
            if ($Name -match 'Circle|Figure Eight|Infinity|Alphabet|Alolita|Roll') { return 'HeadCircle' }
            if ($Name -match 'Translation|Slide|Protraction|Retraction|Turtle') { return 'HeadTranslate' }
            if ($Name -match 'Horizontal|Parsva') { return 'GazeHorizontal' }
            if ($Name -match 'Vertical|Urdhva|Nabi|Padayor') { return 'GazeVertical' }
            if ($Name -match 'Circular|Clock|Square|Triangle') { return 'GazeCircle' }
            return 'GazeDiagonal'
        }
        'SHOULDERS' {
            if ($Name -match 'Roll|Circle|Clock|Figure Eight|CAR') { return 'ShoulderCircle' }
            if ($Name -match 'Scapular|Shoulder-Blade|Serratus') { return 'ScapularGlide' }
            if ($Name -match 'Rotation|Cuban|Goalpost|Cactus') { return 'ShoulderRotation' }
            if ($Name -match 'Stretch|Eagle|Cow-Face|Garudasana|Gomukhasana|Prayer|Hands-Behind') { return 'ShoulderStretch' }
            if ($Name -match 'Shimmy|Accent|Epaulement|Natyarambhe|Chowka|Kathak') { return 'ShoulderDance' }
            if ($Name -match 'Backstroke|Freestyle|Breaststroke|Butterfly|Ski') { return 'ShoulderCycle' }
            return 'ShoulderRaise'
        }
        'HIPS' {
            if ($Name -match 'Circle|Clock|Figure Eight|Umi|Maya|Taqsim|CAR') { return 'HipCircle' }
            if ($Name -match 'Pelvic Tilt|Tuck|Undulation|Samba') { return 'PelvicTilt' }
            if ($Name -match 'Shift|Slide|Drop|Lift|Bump|Sway|Shimmy') { return 'HipShift' }
            if ($Name -match 'Hinge|Good Morning|Deadlift|Pigeon|Stretch|Penche') { return 'HipHinge' }
            if ($Name -match 'Leg Swing|Pendulum|Rond|Developpe|Attitude|Arabesque|Passe|Retire') { return 'HipLegArc' }
            if ($Name -match 'Rotation|Kua|Coil|Hip Snap|Hip Action|Cuban Motion') { return 'HipRotation' }
            return 'HipOpenClose'
        }
        'CHEST' {
            if ($Name -match 'Breath|Breathing|Expansion') { return 'ChestBreathing' }
            if ($Name -match 'Circle|Figure Eight|Ribcage Isolation|Slide') { return 'ChestCircle' }
            if ($Name -match 'Press|Strike|Punch|Squeeze|Svend') { return 'ChestPress' }
            if ($Name -match 'Fly|Hug|Wing|Crane') { return 'ChestFly' }
            if ($Name -match 'Open|Prayer|Salute|Warrior|Mountain|Cactus|Goalpost|Cow-Face|Eagle') { return 'ChestOpen' }
            if ($Name -match 'Tai Chi|Qigong|Brocades') { return 'ChestFlow' }
            return 'ChestIsolation'
        }
        'BACK' {
            if ($Name -match 'Flexion|Roll|Fold|Contraction') { return 'SpineFlexion' }
            if ($Name -match 'Extension|Backbend|Cobra|Locust|Cambre Back|Release') { return 'SpineExtension' }
            if ($Name -match 'Side|Lateral|Banana|Crescent-Moon|Esquiva') { return 'SpineSideBend' }
            if ($Name -match 'Rotation|Twist|Turn|Coil|Rollback|Cloud|Shuttle') { return 'SpineRotation' }
            if ($Name -match 'Wave|Figure Eight|Circle|Cat-Cow|Dragon|Undulation') { return 'SpineWave' }
            if ($Name -match 'Hinge|Good Morning|Flat Back|Needle|Sea Bottom') { return 'BackHinge' }
            if ($Name -match 'Row|Pulldown|Fly|Scapular|Lat') { return 'BackArmPull' }
            if ($Name -match 'Slip|Roll|Weave|Lean|Cocorinha') { return 'BoxingEvasion' }
            return 'BackBalance'
        }
        'CORE' {
            if ($Name -match 'Crunch|Knee-to-Elbow|Knee Tuck|Knee Drive|Bicycle') { return 'CoreCrunch' }
            if ($Name -match 'Side Bend|Windmill|Side Crunch|Anti-Tilt') { return 'CoreSideBend' }
            if ($Name -match 'Chop|Canoe|Kayak|Russian|Rotation|Twist|Dantian|Waist') { return 'CoreRotation' }
            if ($Name -match 'Balance|Pose|Stance|Single-Leg|High-Knee|Knee Chamber') { return 'CoreBalance' }
            if ($Name -match 'Reach|Bird|Cross-Crawl|Mountain-Climber|Bear-Crawl|March') { return 'CoreCrossCrawl' }
            if ($Name -match 'Self-Resisted|Palm-to-Knee|Isometric') { return 'CoreIsometric' }
            return 'CoreBrace'
        }
    }
}

function New-HandExerciseFrameSvg {
    param(
        [int]$ExerciseId,
        [string]$MotionProfile,
        [int]$MovementIndex,
        [int]$FrameIndex,
        [string]$Accent
    )

    $phase = 2 * [Math]::PI * $FrameIndex / 8
    $wave = [Math]::Sin($phase)
    $open = if ($MotionProfile -eq 'FingerMotion') { (1 + $wave) / 2 } else { 0.75 }
    $shape = $MovementIndex % 6
    $gap = if ($MotionProfile -in @('HandIsometric', 'Mudra')) { 8 + (6 * [Math]::Abs($wave)) } else { 34 }
    $leftPalmX = 128 - $gap - 26
    $rightPalmX = 128 + $gap + 26
    $rotation = if ($MotionProfile -eq 'WristMotion') { Get-RoundedInt ($wave * 24) } else { 0 }
    $fingerLines = [System.Text.StringBuilder]::new()

    foreach ($side in @(-1, 1)) {
        $palmX = if ($side -eq -1) { $leftPalmX } else { $rightPalmX }
        for ($finger = 0; $finger -lt 4; $finger++) {
            $fingerOpen = $open
            if ($MotionProfile -eq 'FingerMotion' -and $shape -in @(1, 2, 4)) {
                $fingerOpen = (1 + [Math]::Sin($phase + ($finger * [Math]::PI / 2))) / 2
            }

            $baseX = $palmX + (($finger - 1.5) * 10)
            $spread = ($finger - 1.5) * (4 + (8 * $fingerOpen))
            $tipX = Get-RoundedInt ($baseX + $spread)
            $length = 25 + ($finger * 3) + (13 * $fingerOpen)
            $tipY = Get-RoundedInt (112 - $length)

            if ($MotionProfile -eq 'Mudra' -and $finger -eq ($shape % 4)) {
                $tipX = Get-RoundedInt ($palmX + ($side * 30))
                $tipY = 116
            }
            elseif ($MotionProfile -eq 'Mudra' -and $shape -eq 5 -and $finger -in @(1, 2)) {
                $tipX = Get-RoundedInt ($palmX + (($finger - 1.5) * 5))
                $tipY = 124
            }
            elseif ($MotionProfile -eq 'MartialHand' -and ($wave -lt 0 -or $finger -lt ($shape % 4))) {
                $tipX = Get-RoundedInt ($palmX + (($finger - 1.5) * 5))
                $tipY = 116
            }

            [void]$fingerLines.AppendLine(('<line x1="{0}" y1="116" x2="{1}" y2="{2}" stroke="{3}" stroke-width="8" stroke-linecap="round" />' -f $baseX, $tipX, $tipY, $Accent))
        }

        $thumbBaseX = $palmX + ($side * 22)
        $thumbWave = if ($MotionProfile -eq 'ThumbMotion') { $wave * 18 } else { $side * 10 }
        $thumbTipX = Get-RoundedInt ($thumbBaseX + ($side * 14) + $thumbWave)
        $thumbTipY = if ($MotionProfile -eq 'Mudra') { 116 } else { 136 }
        [void]$fingerLines.AppendLine(('<line x1="{0}" y1="130" x2="{1}" y2="{2}" stroke="{3}" stroke-width="9" stroke-linecap="round" />' -f $thumbBaseX, $thumbTipX, $thumbTipY, $Accent))
    }

    return @"
<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
  <metadata>Flux real hand exercise $ExerciseId frame $FrameIndex</metadata>
  <rect width="256" height="256" rx="28" fill="#F7FAFC" />
  <circle cx="128" cy="126" r="102" fill="$Accent" opacity="0.08" />
  <g transform="rotate($rotation 128 132)">
    <rect x="$($leftPalmX - 25)" y="112" width="50" height="76" rx="22" fill="$Accent" opacity="0.88" />
    <rect x="$($rightPalmX - 25)" y="112" width="50" height="76" rx="22" fill="$Accent" opacity="0.88" />
    $fingerLines
  </g>
  <line x1="40" y1="220" x2="216" y2="220" stroke="#C9D7E3" stroke-width="4" stroke-linecap="round" />
</svg>
"@
}

function New-HeadExerciseFrameSvg {
    param(
        [int]$ExerciseId,
        [string]$MotionProfile,
        [int]$FrameIndex,
        [string]$Accent
    )

    $phase = 2 * [Math]::PI * $FrameIndex / 8
    $wave = [Math]::Sin($phase)
    $counterWave = [Math]::Cos($phase)
    $headX = 128
    $headY = 102
    $rotation = 0
    $pupilX = Get-RoundedInt ($wave * 8)
    $pupilY = Get-RoundedInt ($counterWave * 5)

    switch ($MotionProfile) {
        'HeadNod' { $rotation = Get-RoundedInt ($wave * 13); $headY += Get-RoundedInt ($wave * 5) }
        'HeadTurn' { $headX += Get-RoundedInt ($wave * 16); $pupilX = Get-RoundedInt ($wave * 4) }
        'HeadTilt' { $rotation = Get-RoundedInt ($wave * 18) }
        'HeadCircle' { $headX += Get-RoundedInt ($wave * 10); $headY += Get-RoundedInt ($counterWave * 7) }
        'HeadTranslate' { $headX += Get-RoundedInt ($wave * 18); $pupilX = 0; $pupilY = 0 }
        'GazeHorizontal' { $pupilY = 0 }
        'GazeVertical' { $pupilX = 0; $pupilY = Get-RoundedInt ($wave * 7) }
        'GazeCircle' { }
        'GazeDiagonal' { $pupilY = Get-RoundedInt ($wave * 6) }
    }

    return @"
<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
  <metadata>Flux real head exercise $ExerciseId frame $FrameIndex</metadata>
  <rect width="256" height="256" rx="28" fill="#F7FAFC" />
  <circle cx="128" cy="126" r="102" fill="$Accent" opacity="0.08" />
  <path d="M62 220 Q72 166 108 158 L148 158 Q184 166 194 220" fill="#17324D" />
  <line x1="128" y1="160" x2="$headX" y2="$($headY + 40)" stroke="#17324D" stroke-width="18" stroke-linecap="round" />
  <g transform="rotate($rotation $headX $headY)">
    <circle cx="$headX" cy="$headY" r="49" fill="$Accent" />
    <ellipse cx="$($headX - 18)" cy="$($headY - 7)" rx="12" ry="9" fill="white" />
    <ellipse cx="$($headX + 18)" cy="$($headY - 7)" rx="12" ry="9" fill="white" />
    <circle cx="$($headX - 18 + $pupilX)" cy="$($headY - 7 + $pupilY)" r="4" fill="#17324D" />
    <circle cx="$($headX + 18 + $pupilX)" cy="$($headY - 7 + $pupilY)" r="4" fill="#17324D" />
    <line x1="$($headX - 9)" y1="$($headY + 22)" x2="$($headX + 9)" y2="$($headY + 22)" stroke="#17324D" stroke-width="4" stroke-linecap="round" />
  </g>
  <line x1="40" y1="224" x2="216" y2="224" stroke="#C9D7E3" stroke-width="4" stroke-linecap="round" />
</svg>
"@
}

function Get-RoundedInt {
    param([double]$Value)
    return [int][Math]::Round($Value, [MidpointRounding]::AwayFromZero)
}

function New-ExerciseFrameSvg {
    param(
        [int]$ExerciseId,
        [int]$RegionIndex,
        [string]$ExerciseName,
        [string]$MotionProfile,
        [int]$MovementIndex,
        [int]$FrameIndex
    )

    $accent = $regionColors[$RegionIndex]

    if ($regions[$RegionIndex] -eq 'HANDS') {
        return New-HandExerciseFrameSvg `
            -ExerciseId $ExerciseId `
            -MotionProfile $MotionProfile `
            -MovementIndex $MovementIndex `
            -FrameIndex $FrameIndex `
            -Accent $accent
    }

    if ($regions[$RegionIndex] -eq 'HEAD') {
        return New-HeadExerciseFrameSvg `
            -ExerciseId $ExerciseId `
            -MotionProfile $MotionProfile `
            -FrameIndex $FrameIndex `
            -Accent $accent
    }

    $phase = 2 * [Math]::PI * $FrameIndex / 8
    $wave = [Math]::Sin($phase)
    $counterWave = [Math]::Cos($phase)
    $amplitude = 7.0 + (($MovementIndex % 4) * 1.4)

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
            switch ($MotionProfile) {
                'AnkleCircle' {
                    $leftFootX += Get-RoundedInt ($wave * $amplitude)
                    $leftFootY -= Get-RoundedInt ((1 + $counterWave) * $amplitude * 0.35)
                }
                'HeelRaise' {
                    $rise = [Math]::Max(0, $wave) * $amplitude
                    $hipY -= Get-RoundedInt $rise
                    $leftKneeY -= Get-RoundedInt $rise
                    $rightKneeY -= Get-RoundedInt $rise
                    $leftFootY -= Get-RoundedInt ($rise * 0.15)
                    $rightFootY -= Get-RoundedInt ($rise * 0.15)
                }
                'ForefootRaise' {
                    $leftFootY -= Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 0.65)
                    $rightFootY -= Get-RoundedInt ([Math]::Max(0, -$wave) * $amplitude * 0.65)
                }
                'AnkleSweep' {
                    $leftFootX += Get-RoundedInt ($wave * $amplitude * 1.2)
                    $rightFootX -= Get-RoundedInt ($wave * $amplitude * 1.2)
                }
                'FootBalance' {
                    $lift = [Math]::Max(0, $wave) * $amplitude * 2.4
                    $leftKneeY -= Get-RoundedInt $lift
                    $leftFootY -= Get-RoundedInt ($lift * 0.85)
                    $leftFootX += Get-RoundedInt ($counterWave * $amplitude)
                }
                'WeightShift' {
                    $hipX += Get-RoundedInt ($wave * $amplitude * 1.5)
                    $leftShoulderX += Get-RoundedInt ($wave * $amplitude * 0.7)
                    $rightShoulderX += Get-RoundedInt ($wave * $amplitude * 0.7)
                }
                'TurnStep' {
                    $leftFootX += Get-RoundedInt ($wave * $amplitude * 1.7)
                    $rightFootX += Get-RoundedInt ($counterWave * $amplitude * 1.2)
                    $hipX += Get-RoundedInt ($wave * $amplitude * 0.6)
                }
                'CrossStep' {
                    $leftFootX += Get-RoundedInt ((1 + $wave) * $amplitude * 1.7)
                    $rightFootX -= Get-RoundedInt ((1 - $wave) * $amplitude * 1.1)
                }
                'SideStep' {
                    $leftFootX -= Get-RoundedInt ((1 + $wave) * $amplitude)
                    $rightFootX += Get-RoundedInt ((1 - $wave) * $amplitude)
                }
                default {
                    $leftLift = [Math]::Max(0, $wave) * $amplitude * 1.25
                    $rightLift = [Math]::Max(0, -$wave) * $amplitude * 1.25
                    $leftFootY -= Get-RoundedInt $leftLift
                    $rightFootY -= Get-RoundedInt $rightLift
                    $leftFootX += Get-RoundedInt ($wave * $amplitude)
                    $rightFootX -= Get-RoundedInt ($wave * $amplitude)
                }
            }
        }
        'LEGS' {
            switch ($MotionProfile) {
                'Squat' {
                    $bend = [Math]::Max(0, $wave) * $amplitude * 2.1
                    $hipY += Get-RoundedInt $bend
                    $leftKneeX -= Get-RoundedInt ($bend * 0.55)
                    $rightKneeX += Get-RoundedInt ($bend * 0.55)
                    $leftKneeY += Get-RoundedInt ($bend * 0.28)
                    $rightKneeY += Get-RoundedInt ($bend * 0.28)
                }
                'Lunge' {
                    $leftFootX -= Get-RoundedInt ($amplitude * 1.4)
                    $rightFootX += Get-RoundedInt ($amplitude * 2.2)
                    $bend = [Math]::Max(0, $wave) * $amplitude * 1.35
                    $rightKneeY += Get-RoundedInt $bend
                    $hipY += Get-RoundedInt ($bend * 0.55)
                }
                'KneeCurl' {
                    $lift = [Math]::Max(0, $wave) * $amplitude * 2.5
                    $leftFootY -= Get-RoundedInt $lift
                    $leftFootX += Get-RoundedInt ($lift * 0.45)
                }
                'KneeLift' {
                    $lift = [Math]::Max(0, $wave) * $amplitude * 3.2
                    $leftKneeY -= Get-RoundedInt $lift
                    $leftFootY -= Get-RoundedInt ($lift * 0.72)
                    $leftKneeX += Get-RoundedInt ($amplitude * 0.7)
                }
                'LegSide' {
                    $leftFootX -= Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 3.1)
                    $leftFootY -= Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 1.25)
                    $leftKneeX -= Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 1.6)
                }
                'LegBack' {
                    $leftFootX -= Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 2.1)
                    $leftFootY -= Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 1.55)
                    $hipX += Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 0.65)
                }
                'LegFront' {
                    $leftFootX += Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 2.8)
                    $leftFootY -= Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 2.1)
                    $leftKneeX += Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 1.4)
                }
                default {
                    $lift = [Math]::Max(0, $wave) * $amplitude * 2.6
                    $leftKneeY -= Get-RoundedInt $lift
                    $leftFootY -= Get-RoundedInt ($lift * 0.8)
                    $leftFootX += Get-RoundedInt ($counterWave * $amplitude)
                }
            }
        }
        'HANDS' {
            $leftHandX += Get-RoundedInt ($counterWave * $amplitude * 1.5)
            $leftHandY += Get-RoundedInt ($wave * $amplitude)
            $rightHandX -= Get-RoundedInt ($counterWave * $amplitude * 1.5)
            $rightHandY -= Get-RoundedInt ($wave * $amplitude)
        }
        'ARMS' {
            switch ($MotionProfile) {
                'ArmCircle' {
                    $leftHandX += Get-RoundedInt ($wave * $amplitude * 3)
                    $leftHandY += Get-RoundedInt ($counterWave * $amplitude * 3)
                    $rightHandX -= Get-RoundedInt ($wave * $amplitude * 3)
                    $rightHandY -= Get-RoundedInt ($counterWave * $amplitude * 3)
                }
                'StraightPunch' {
                    $leftHandX += Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 3.3)
                    $leftHandY -= Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 0.7)
                    $leftElbowX += Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 1.6)
                }
                'BentArmStrike' {
                    $leftElbowY -= Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 2.5)
                    $leftHandX += Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 1.8)
                    $leftHandY -= Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 1.5)
                }
                'ArmBlock' {
                    $leftElbowX += Get-RoundedInt ($wave * $amplitude * 1.4)
                    $leftElbowY -= Get-RoundedInt ($counterWave * $amplitude * 1.5)
                    $leftHandX += Get-RoundedInt ($wave * $amplitude * 2.2)
                    $leftHandY -= Get-RoundedInt ($counterWave * $amplitude * 2.7)
                    $rightHandX -= Get-RoundedInt ($wave * $amplitude)
                }
                'FlowingArms' {
                    $leftHandX += Get-RoundedInt ($counterWave * $amplitude * 2.4)
                    $leftHandY -= Get-RoundedInt ($wave * $amplitude * 2)
                    $rightHandX += Get-RoundedInt ($wave * $amplitude * 1.8)
                    $rightHandY += Get-RoundedInt ($counterWave * $amplitude * 1.5)
                }
                'PortDeBras' {
                    $leftHandX -= Get-RoundedInt ($wave * $amplitude * 1.7)
                    $leftHandY -= Get-RoundedInt ((1 + $wave) * $amplitude * 2.7)
                    $rightHandX += Get-RoundedInt ($wave * $amplitude * 1.7)
                    $rightHandY -= Get-RoundedInt ((1 + $wave) * $amplitude * 2.7)
                }
                'SportArmCycle' {
                    $leftHandX += Get-RoundedInt ($wave * $amplitude * 2.7)
                    $leftHandY += Get-RoundedInt ($counterWave * $amplitude * 3.2)
                    $rightHandX -= Get-RoundedInt ($wave * $amplitude * 2.7)
                    $rightHandY -= Get-RoundedInt ($counterWave * $amplitude * 3.2)
                }
                'ArmRaise' {
                    $raise = (1 + $wave) * $amplitude * 2.5
                    $leftHandY -= Get-RoundedInt $raise
                    $rightHandY -= Get-RoundedInt $raise
                    $leftElbowY -= Get-RoundedInt ($raise * 0.5)
                    $rightElbowY -= Get-RoundedInt ($raise * 0.5)
                }
                'ElbowMotion' {
                    $leftHandY -= Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 2.5)
                    $rightHandY -= Get-RoundedInt ([Math]::Max(0, -$wave) * $amplitude * 2.5)
                    $leftHandX += Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude)
                    $rightHandX -= Get-RoundedInt ([Math]::Max(0, -$wave) * $amplitude)
                }
                default {
                    $leftHandX += Get-RoundedInt ($counterWave * $amplitude * 2.2)
                    $leftHandY -= Get-RoundedInt ($wave * $amplitude * 2.5)
                    $rightHandX -= Get-RoundedInt ($counterWave * $amplitude * 2.2)
                    $rightHandY += Get-RoundedInt ($wave * $amplitude * 2.5)
                }
            }
        }
        'HEAD' {
            $headX += Get-RoundedInt ($wave * $amplitude * 1.2)
            $headY += Get-RoundedInt ($counterWave * $amplitude * 0.55)
            $noseOffsetX = Get-RoundedInt (15 + ($counterWave * 3))
            $noseOffsetY = Get-RoundedInt ($wave * 7)
        }
        'SHOULDERS' {
            switch ($MotionProfile) {
                'ShoulderCircle' {
                    $leftShoulderX += Get-RoundedInt ($wave * $amplitude)
                    $leftShoulderY += Get-RoundedInt ($counterWave * $amplitude)
                    $rightShoulderX -= Get-RoundedInt ($wave * $amplitude)
                    $rightShoulderY -= Get-RoundedInt ($counterWave * $amplitude)
                }
                'ScapularGlide' {
                    $leftShoulderX += Get-RoundedInt ($wave * $amplitude)
                    $rightShoulderX -= Get-RoundedInt ($wave * $amplitude)
                    $leftElbowX += Get-RoundedInt ($wave * $amplitude)
                    $rightElbowX -= Get-RoundedInt ($wave * $amplitude)
                }
                'ShoulderRotation' {
                    $leftElbowY -= Get-RoundedInt ($wave * $amplitude * 1.6)
                    $rightElbowY -= Get-RoundedInt ($wave * $amplitude * 1.6)
                    $leftHandX += Get-RoundedInt ($counterWave * $amplitude * 1.8)
                    $rightHandX -= Get-RoundedInt ($counterWave * $amplitude * 1.8)
                }
                'ShoulderStretch' {
                    $leftHandX += Get-RoundedInt ((1 + $wave) * $amplitude * 1.3)
                    $leftHandY -= Get-RoundedInt ((1 + $wave) * $amplitude * 1.8)
                    $rightHandX -= Get-RoundedInt ((1 + $wave) * $amplitude * 1.3)
                    $rightHandY -= Get-RoundedInt ((1 + $wave) * $amplitude * 1.8)
                }
                'ShoulderDance' {
                    $leftShoulderY += Get-RoundedInt ($wave * $amplitude)
                    $rightShoulderY -= Get-RoundedInt ($wave * $amplitude)
                    $leftElbowY += Get-RoundedInt ($wave * $amplitude)
                    $rightElbowY -= Get-RoundedInt ($wave * $amplitude)
                }
                'ShoulderCycle' {
                    $leftHandY += Get-RoundedInt ($counterWave * $amplitude * 3)
                    $rightHandY -= Get-RoundedInt ($counterWave * $amplitude * 3)
                    $leftHandX += Get-RoundedInt ($wave * $amplitude * 2)
                    $rightHandX -= Get-RoundedInt ($wave * $amplitude * 2)
                }
                default {
                    $raise = (1 + $wave) * $amplitude * 2
                    $leftHandY -= Get-RoundedInt $raise
                    $rightHandY -= Get-RoundedInt $raise
                    $leftElbowY -= Get-RoundedInt ($raise * 0.55)
                    $rightElbowY -= Get-RoundedInt ($raise * 0.55)
                }
            }
        }
        'HIPS' {
            switch ($MotionProfile) {
                'HipCircle' {
                    $hipX += Get-RoundedInt ($wave * $amplitude * 1.7)
                    $hipY += Get-RoundedInt ($counterWave * $amplitude * 0.85)
                }
                'PelvicTilt' {
                    $hipY += Get-RoundedInt ($wave * $amplitude)
                    $torsoShift = Get-RoundedInt ($wave * $amplitude * 0.45)
                    $leftShoulderX -= $torsoShift
                    $rightShoulderX -= $torsoShift
                }
                'HipShift' {
                    $hipX += Get-RoundedInt ($wave * $amplitude * 1.9)
                    $leftKneeX += Get-RoundedInt ($wave * $amplitude * 0.75)
                    $rightKneeX += Get-RoundedInt ($wave * $amplitude * 0.75)
                }
                'HipHinge' {
                    $lean = Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 2.2)
                    $leftShoulderX += $lean
                    $rightShoulderX += $lean
                    $headX += $lean
                    $hipX -= Get-RoundedInt ($lean * 0.35)
                }
                'HipLegArc' {
                    $leftFootX += Get-RoundedInt ($wave * $amplitude * 2.5)
                    $leftFootY -= Get-RoundedInt ((1 + $counterWave) * $amplitude * 1.15)
                    $leftKneeX += Get-RoundedInt ($wave * $amplitude * 1.25)
                }
                'HipRotation' {
                    $hipX += Get-RoundedInt ($wave * $amplitude * 1.2)
                    $leftShoulderX -= Get-RoundedInt ($wave * $amplitude * 0.8)
                    $rightShoulderX -= Get-RoundedInt ($wave * $amplitude * 0.8)
                    $leftKneeX += Get-RoundedInt ($counterWave * $amplitude * 0.75)
                }
                default {
                    $leftKneeX -= Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 1.6)
                    $leftKneeY -= Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 1.3)
                    $leftFootY -= Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude)
                }
            }
        }
        'CHEST' {
            switch ($MotionProfile) {
                'ChestBreathing' {
                    $spread = Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 1.25)
                    $leftShoulderX -= $spread
                    $rightShoulderX += $spread
                    $leftElbowX -= Get-RoundedInt ($spread * 0.6)
                    $rightElbowX += Get-RoundedInt ($spread * 0.6)
                }
                'ChestCircle' {
                    $leftShoulderX += Get-RoundedInt ($wave * $amplitude)
                    $rightShoulderX += Get-RoundedInt ($wave * $amplitude)
                    $leftShoulderY += Get-RoundedInt ($counterWave * $amplitude * 0.55)
                    $rightShoulderY += Get-RoundedInt ($counterWave * $amplitude * 0.55)
                }
                'ChestPress' {
                    $leftHandX += Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 2.4)
                    $rightHandX -= Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 2.4)
                    $leftHandY -= Get-RoundedInt ($amplitude * 0.7)
                    $rightHandY -= Get-RoundedInt ($amplitude * 0.7)
                }
                'ChestFly' {
                    $spread = Get-RoundedInt ($wave * $amplitude * 1.8)
                    $leftHandX -= $spread
                    $rightHandX += $spread
                    $leftElbowX -= Get-RoundedInt ($spread * 0.7)
                    $rightElbowX += Get-RoundedInt ($spread * 0.7)
                }
                'ChestOpen' {
                    $spread = Get-RoundedInt ((1 + $wave) * $amplitude)
                    $leftShoulderX -= $spread
                    $rightShoulderX += $spread
                    $leftElbowX -= $spread
                    $rightElbowX += $spread
                    $leftHandY -= Get-RoundedInt ($spread * 0.7)
                    $rightHandY -= Get-RoundedInt ($spread * 0.7)
                }
                'ChestFlow' {
                    $leftHandX += Get-RoundedInt ($counterWave * $amplitude * 2)
                    $leftHandY -= Get-RoundedInt ($wave * $amplitude * 1.7)
                    $rightHandX -= Get-RoundedInt ($counterWave * $amplitude * 2)
                    $rightHandY += Get-RoundedInt ($wave * $amplitude * 1.7)
                }
                default {
                    $leftShoulderX += Get-RoundedInt ($wave * $amplitude)
                    $rightShoulderX += Get-RoundedInt ($wave * $amplitude)
                }
            }
        }
        'BACK' {
            switch ($MotionProfile) {
                'SpineFlexion' {
                    $lean = Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 2)
                    $leftShoulderX += $lean
                    $rightShoulderX += $lean
                    $headX += Get-RoundedInt ($lean * 1.25)
                    $leftShoulderY += Get-RoundedInt ($lean * 0.35)
                    $rightShoulderY += Get-RoundedInt ($lean * 0.35)
                }
                'SpineExtension' {
                    $lean = Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 1.45)
                    $leftShoulderX -= $lean
                    $rightShoulderX -= $lean
                    $headX -= Get-RoundedInt ($lean * 1.2)
                    $leftHandY -= $lean
                    $rightHandY -= $lean
                }
                'SpineSideBend' {
                    $lean = Get-RoundedInt ($wave * $amplitude * 1.6)
                    $leftShoulderX += $lean
                    $rightShoulderX += $lean
                    $headX += Get-RoundedInt ($lean * 1.25)
                    $leftHandY += Get-RoundedInt ($wave * $amplitude * 1.5)
                    $rightHandY -= Get-RoundedInt ($wave * $amplitude * 1.5)
                }
                'SpineRotation' {
                    $twist = Get-RoundedInt ($wave * $amplitude * 1.35)
                    $leftShoulderX += $twist
                    $rightShoulderX += $twist
                    $leftHandX += Get-RoundedInt ($twist * 1.4)
                    $rightHandX += Get-RoundedInt ($twist * 1.4)
                }
                'SpineWave' {
                    $leftShoulderX += Get-RoundedInt ($wave * $amplitude * 1.3)
                    $rightShoulderX += Get-RoundedInt ($wave * $amplitude * 1.3)
                    $headX += Get-RoundedInt ($counterWave * $amplitude)
                    $hipX -= Get-RoundedInt ($wave * $amplitude * 0.8)
                }
                'BackHinge' {
                    $lean = Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 2.5)
                    $leftShoulderX += $lean
                    $rightShoulderX += $lean
                    $headX += $lean
                    $hipX -= Get-RoundedInt ($lean * 0.4)
                }
                'BackArmPull' {
                    $leftElbowX -= Get-RoundedInt ($wave * $amplitude * 1.5)
                    $rightElbowX += Get-RoundedInt ($wave * $amplitude * 1.5)
                    $leftHandY -= Get-RoundedInt ($counterWave * $amplitude * 1.6)
                    $rightHandY -= Get-RoundedInt ($counterWave * $amplitude * 1.6)
                }
                'BoxingEvasion' {
                    $lean = Get-RoundedInt ($wave * $amplitude * 1.8)
                    $leftShoulderX += $lean
                    $rightShoulderX += $lean
                    $headX += Get-RoundedInt ($lean * 1.3)
                    $hipY += Get-RoundedInt ([Math]::Abs($wave) * $amplitude)
                }
                default {
                    $leftFootY -= Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 1.7)
                    $leftHandY -= Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 2)
                    $rightHandY -= Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 2)
                }
            }
        }
        'CORE' {
            switch ($MotionProfile) {
                'CoreCrunch' {
                    $lift = [Math]::Max(0, $wave) * $amplitude * 2.5
                    $rightKneeY -= Get-RoundedInt $lift
                    $rightFootY -= Get-RoundedInt ($lift * 0.75)
                    $rightKneeX += Get-RoundedInt ($lift * 0.38)
                    $leftElbowY += Get-RoundedInt ($lift * 0.5)
                    $leftElbowX += Get-RoundedInt ($lift * 0.35)
                }
                'CoreSideBend' {
                    $lean = Get-RoundedInt ($wave * $amplitude * 1.5)
                    $leftShoulderX += $lean
                    $rightShoulderX += $lean
                    $headX += Get-RoundedInt ($lean * 1.2)
                    $hipX -= Get-RoundedInt ($lean * 0.5)
                }
                'CoreRotation' {
                    $twist = Get-RoundedInt ($wave * $amplitude * 1.4)
                    $leftShoulderX += $twist
                    $rightShoulderX += $twist
                    $leftHandX += Get-RoundedInt ($twist * 1.5)
                    $rightHandX += Get-RoundedInt ($twist * 1.5)
                    $hipX -= Get-RoundedInt ($twist * 0.6)
                }
                'CoreBalance' {
                    $lift = [Math]::Max(0, $wave) * $amplitude * 2.5
                    $rightKneeY -= Get-RoundedInt $lift
                    $rightFootY -= Get-RoundedInt ($lift * 0.8)
                    $leftHandY -= Get-RoundedInt ($lift * 0.7)
                    $rightHandY -= Get-RoundedInt ($lift * 0.7)
                }
                'CoreCrossCrawl' {
                    $rightLift = [Math]::Max(0, $wave) * $amplitude * 2.3
                    $leftLift = [Math]::Max(0, -$wave) * $amplitude * 2.3
                    $rightKneeY -= Get-RoundedInt $rightLift
                    $rightFootY -= Get-RoundedInt ($rightLift * 0.7)
                    $leftKneeY -= Get-RoundedInt $leftLift
                    $leftFootY -= Get-RoundedInt ($leftLift * 0.7)
                    $leftHandY += Get-RoundedInt ($rightLift * 0.55)
                    $rightHandY += Get-RoundedInt ($leftLift * 0.55)
                }
                'CoreIsometric' {
                    $leftHandX += Get-RoundedInt ((1 + $wave) * $amplitude * 1.4)
                    $rightKneeY -= Get-RoundedInt ((1 + $wave) * $amplitude * 1.2)
                    $rightFootY -= Get-RoundedInt ((1 + $wave) * $amplitude * 0.75)
                }
                default {
                    $leftShoulderX += Get-RoundedInt ($wave * $amplitude * 0.55)
                    $rightShoulderX += Get-RoundedInt ($wave * $amplitude * 0.55)
                    $hipX -= Get-RoundedInt ($wave * $amplitude * 0.35)
                }
            }
        }
    }

    $torsoTopX = Get-RoundedInt (($leftShoulderX + $rightShoulderX) / 2)
    $torsoTopY = Get-RoundedInt (($leftShoulderY + $rightShoulderY) / 2)
    $leftHipX = $hipX - 12
    $rightHipX = $hipX + 12
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
    $regionNames = @($catalogNames[$region])

    if ($regionNames.Count -ne 100) {
        throw "$region must define exactly 100 real movements."
    }

    for ($movementIndex = 0; $movementIndex -lt 100; $movementIndex++) {
        $exerciseId = ($regionIndex * 100) + $movementIndex + 1
        $exerciseName = $regionNames[$movementIndex]
        $practice = Get-Practice -Name $exerciseName
        $motionProfile = Get-MotionProfile -Region $region -Name $exerciseName
        $gifFileName = 'exercise_{0:D4}.gif' -f $exerciseId
        $gifRelativePath = "exercise_gifs/$gifFileName"

        $records.Add([ordered]@{
            id = $exerciseId
            name = $exerciseName
            gif = $gifRelativePath
            dominantRegion = $region
            practice = $practice
            motionProfile = $motionProfile
            score = 0
            onlyFeetTouchGround = $true
            shoeAgnostic = $true
            maxSpaceMeters = 3
            equipment = 'None'
            silent = $true
        })

        if ($exerciseId -lt $StartExercise -or
            ($MaxExercises -gt 0 -and $exerciseId -gt $MaxExercises)) {
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
                -ExerciseName $exerciseName `
                -MotionProfile $motionProfile `
                -MovementIndex $movementIndex `
                -FrameIndex $frameIndex
            Set-Content -LiteralPath $framePath -Value $svg -Encoding utf8
            $framePaths += $framePath
        }

        $delay = @(18, 14, 20, 16, 11, 15, 15, 13, 17, 10)[$movementIndex % 10]
        $magickArguments = @($framePaths) + @(
            '-set', 'delay', $delay.ToString(),
            '-set', 'dispose', 'background',
            '-set', 'comment', "Flux real exercise $exerciseId - $exerciseName",
            '-loop', '0',
            '-layers', 'Optimize',
            $gifPath
        )

        & magick @magickArguments

        if ($LASTEXITCODE -ne 0) {
            throw "ImageMagick failed while generating $gifFileName."
        }

        if ($exerciseId % 50 -eq 0) {
            Write-Output "Generated $exerciseId / 1000 real-exercise GIFs"
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
    [string]::IsNullOrWhiteSpace($_['practice']) -or
    [string]::IsNullOrWhiteSpace($_['motionProfile']) -or
    $_['score'] -ne 0
}
$syntheticNames = $records | Where-Object {
    $_['name'] -match ' — ' -or
    $_['name'] -match 'Slow Tempo|Four-Count Tempo|End-Range Pause|Half Range|Full Range|Left Lead|Right Lead|Precision Repetitions|Continuous Flow'
}

if ($duplicateNames -or $duplicateGifs -or $duplicateIds -or
    $invalidRegionCounts -or $constraintViolations -or $syntheticNames) {
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
