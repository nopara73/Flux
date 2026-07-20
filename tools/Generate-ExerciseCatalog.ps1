param(
    [string]$OutputRoot = (Join-Path $PSScriptRoot '..\Flux\Assets'),
    [ValidateRange(1, 1000)]
    [int]$StartExercise = 1,
    [ValidateRange(0, 1000)]
    [int]$MaxExercises = 0,
    [ValidateRange(1, 1000)]
    [int[]]$ExerciseIds = @(),
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
$bilateralExerciseNames = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'BilateralExerciseNames.psd1')
$holdExerciseFrames = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'HoldExerciseFrames.psd1')
$externalExerciseMedia = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExternalExerciseMedia.psd1')
$posecodeExerciseMedia = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'PosecodeExerciseMedia.psd1')

if ($bilateralExerciseNames.Count -eq 0 -or @(
        $bilateralExerciseNames.GetEnumerator() | Where-Object {
            [int]$_.Key -lt 1 -or
            [int]$_.Key -gt 1000 -or
            [string]::IsNullOrWhiteSpace([string]$_.Value)
        }).Count -gt 0) {
    throw 'The bilateral catalog replacement map contains an invalid entry.'
}

if ($holdExerciseFrames.Count -eq 0 -or @(
        $holdExerciseFrames.GetEnumerator() | Where-Object {
            [int]$_.Key -lt 1 -or
            [int]$_.Key -gt 1000 -or
            [int]$_.Value -lt 1 -or
            [int]$_.Value -gt 99
        }).Count -gt 0) {
    throw 'The reviewed hold-frame map contains an invalid entry.'
}

if ($externalExerciseMedia.Count -ne 81) {
    throw 'The reviewed external-media map must contain exactly 81 entries.'
}

if ($posecodeExerciseMedia.Count -ne 77) {
    throw 'The reviewed Posecode-media map must contain exactly 77 entries.'
}

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

    $practiceName = $Name -replace '^(Alternating|Bilateral|Symmetric)\s+', ''

    switch -Regex ($practiceName) {
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
            if ($Name -match 'Bilateral Bent-Knee Calf Raise') { return 'HeelRaise' }
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
            if ($Name -match 'Slow Blink|Eye Squeeze') { return 'EyeBlink' }
            if ($Name -match 'Cross-Pattern Saccades') { return 'GazeCrossSaccade' }
            if ($Name -match 'Cross-Pattern') { return 'GazeCross' }
            if ($Name -match 'Figure-Eight Smooth|Infinity Gaze') { return 'GazeFigureEight' }
            if ($Name -match 'Near-Far Focus') { return 'GazeNearFar' }
            if ($Name -match 'Vertical Near-Far') { return 'GazeVerticalNearFar' }
            if ($Name -match 'Vertical Gaze Ladder') { return 'GazeVerticalLadder' }
            if ($Name -match 'Convergence|Nasagra|Bhrumadhya|Angusthamadhye') { return 'GazeConvergence' }
            if ($Name -match 'Horizontal Gaze Shift Between Thumbs|Kathak Alternating Side Gaze') { return 'GazeHorizontalSaccade' }
            if ($Name -match 'Peripheral-Awareness') { return 'GazePeripheral' }
            if ($Name -match 'Four-Corner Saccades|Square-Path Saccades') { return 'GazeCornerSaccade' }
            if ($Name -match 'Clock-Face Saccades') { return 'GazeClockSaccade' }
            if ($Name -match 'Triangle-Path Saccades') { return 'GazeTriangleSaccade' }
            if ($Name -match 'Horizontal Saccades') { return 'GazeHorizontalSaccade' }
            if ($Name -match 'Vertical Saccades') { return 'GazeVerticalSaccade' }
            if ($Name -match 'Horizontal Gaze Stabilization|Horizontal VOR x1') { return 'VorHorizontal' }
            if ($Name -match 'Vertical Gaze Stabilization|Vertical VOR x1') { return 'VorVertical' }
            if ($Name -match 'Four-Direction Gaze Stabilization|Four-Direction VOR') { return 'VorFourDirection' }
            if ($Name -match 'Horizontal VOR Cancellation') { return 'VorCancellationHorizontal' }
            if ($Name -match 'Vertical VOR Cancellation') { return 'VorCancellationVertical' }
            if ($Name -match 'Horizontal VOR x2') { return 'VorX2Horizontal' }
            if ($Name -match 'Vertical VOR x2') { return 'VorX2Vertical' }
            if ($Name -match 'Nose Square') { return 'HeadSquare' }
            if ($Name -match 'Four-Direction Head Tilt|Four-Corner Dance Head Accent|Jazz Head Isolation') { return 'HeadFourDirection' }
            if ($Name -match 'Neck Elongation|Sama Shiro') { return 'HeadNeutral' }
            if ($Name -match 'Chin-Tuck') { return 'HeadTranslate' }
            if ($Name -match 'Dragon Surveys the Sea') { return 'HeadSurvey' }
            if ($Name -match 'Dhuta-Kampita') { return 'HeadTurnNod' }
            if ($Name -match 'Tiger Watches Prey|Dhuta Shiro') { return 'HeadTurn' }
            if ($Name -match 'Lateral|Side-Bend|Side-to-Side Dance Head Accent|Tilt|Ear-to|Griva|Parivahita') { return 'HeadTilt' }
            if ($Name -match 'Flexion|Extension|Nod|Kampita|Accent Front|Udvahita|Adhomukha') { return 'HeadNod' }
            if ($Name -match 'Rotation|Turn|Looks Back|Gazes Back|Paravritta|Spotting|Flick') { return 'HeadTurn' }
            if ($Name -match 'Circle|Figure Eight|Infinity|Alphabet|Alolita|Roll') { return 'HeadCircle' }
            if ($Name -match 'Translation|Slide|Protraction|Retraction|Turtle') { return 'HeadTranslate' }
            if ($Name -match 'Horizontal|Parsva') { return 'GazeHorizontal' }
            if ($Name -match 'Vertical|Urdhva|Nabi|Padayor') { return 'GazeVertical' }
            if ($Name -match 'Circular|Clock|Square|Triangle') { return 'GazeCircle' }
            return 'GazeDiagonal'
        }
        'SHOULDERS' {
            if ($Name -match 'Roll|Circle|Clock|Figure Eight|\bCAR\b') { return 'ShoulderCircle' }
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
            if ($Name -match 'Tai Chi Rollback') { return 'SpineRotation' }
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

    $phase = 2 * [Math]::PI * $FrameIndex / 16
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
        [string]$ExerciseName,
        [string]$MotionProfile,
        [int]$FrameIndex,
        [string]$Accent
    )

    $phase = 2 * [Math]::PI * $FrameIndex / 16
    $wave = [Math]::Sin($phase)
    $counterWave = [Math]::Cos($phase)
    $headX = 128
    $headY = 104
    $rotation = 0
    $leftPupilX = 0
    $rightPupilX = 0
    $leftPupilY = 0
    $rightPupilY = 0
    $turn = 0.0
    $blink = 0.0
    $sideProfile = $false
    $showTarget = $false
    $targetX = 128
    $targetY = 38
    $targetRadius = 5
    $targetPath = ''
    $stepHorizontal = if ($wave -ge 0) { 1 } else { -1 }
    $stepVertical = if ($counterWave -ge 0) { 1 } else { -1 }

    switch ($MotionProfile) {
        'HeadNod' {
            $sideProfile = $true
            if ($ExerciseName -match 'Extension|Udvahita' -and $ExerciseName -notmatch 'Flexion-Extension') {
                $rotation = Get-RoundedInt (-[Math]::Max(0, $wave) * 18)
            }
            elseif ($ExerciseName -match 'Flexion-Extension') {
                $rotation = Get-RoundedInt ($wave * 18)
            }
            else {
                $rotation = Get-RoundedInt ([Math]::Max(0, $wave) * 16)
            }
        }
        'HeadTurn' {
            $headWave = $wave
            $eyeWave = $wave
            if ($ExerciseName -match 'Opposite-Direction') {
                $eyeWave = -$wave
            }
            elseif ($ExerciseName -match 'Eyes-Lead') {
                $eyeWave = [Math]::Sin($phase + ([Math]::PI / 4))
            }
            elseif ($ExerciseName -match 'Head-Lead') {
                $headWave = [Math]::Sin($phase + ([Math]::PI / 4))
            }

            $turn = $headWave
            $leftPupilX = Get-RoundedInt ($eyeWave * 5)
            $rightPupilX = $leftPupilX
        }
        'HeadTilt' { $rotation = Get-RoundedInt ($wave * 18) }
        'HeadCircle' {
            $circlePhase = 2 * [Math]::PI * ($FrameIndex % 8) / 8
            $circleDirection = if ($FrameIndex -lt 8) { 1 } else { -1 }
            $headX += Get-RoundedInt ([Math]::Sin($circleDirection * $circlePhase) * 10)
            $headY += Get-RoundedInt ([Math]::Cos($circleDirection * $circlePhase) * 7)
        }
        'HeadSquare' {
            $squareSequence = @(0, 1, 2, 3, 0, 3, 2, 1)
            $squareIndex = $squareSequence[[int][Math]::Floor($FrameIndex / 2)]
            $headX += @(-11, 11, 11, -11)[$squareIndex]
            $headY += @(-8, -8, 8, 8)[$squareIndex]
        }
        'HeadFourDirection' {
            $directionIndex = [int][Math]::Floor($FrameIndex / 4)
            if ($directionIndex -eq 0) { $rotation = -16 }
            elseif ($directionIndex -eq 1) { $rotation = 16 }
            elseif ($directionIndex -eq 2) { $headY -= 7 }
            else { $headY += 7 }
        }
        'HeadSurvey' {
            $surveyPhase = 2 * [Math]::PI * ($FrameIndex % 8) / 8
            $surveyDirection = if ($FrameIndex -lt 8) { 1 } else { -1 }
            $turn = [Math]::Sin($surveyDirection * $surveyPhase)
            $rotation = Get-RoundedInt ([Math]::Cos($surveyDirection * $surveyPhase) * 8)
        }
        'HeadTurnNod' {
            if ($FrameIndex -lt 8) {
                $turn = [Math]::Sin(2 * [Math]::PI * $FrameIndex / 8)
            }
            else {
                $sideProfile = $true
                $rotation = Get-RoundedInt ([Math]::Sin(2 * [Math]::PI * ($FrameIndex - 8) / 8) * 16)
            }
        }
        'HeadNeutral' {
            $headY -= Get-RoundedInt ([Math]::Max(0, $wave) * 4)
            $blink = [Math]::Max(0, -$counterWave)
        }
        'HeadTranslate' {
            $sideProfile = $ExerciseName -notmatch 'Side-to-Side|Dance Head Slide'
            $headX += Get-RoundedInt ($wave * 18)
        }
        'EyeBlink' {
            $blink = (1 - $counterWave) / 2
        }
        'GazeHorizontal' {
            $showTarget = $true
            $targetX = Get-RoundedInt (128 + ($wave * 76))
            $targetY = $headY - 7
            $leftPupilX = Get-RoundedInt ($wave * 8)
            $rightPupilX = $leftPupilX
            $targetPath = '<line x1="52" y1="97" x2="204" y2="97" stroke="#E63946" stroke-width="2" stroke-dasharray="5 6" opacity="0.45" />'
        }
        'GazeVertical' {
            $showTarget = $true
            $targetX = 128
            $targetY = Get-RoundedInt ($headY - 7 + ($wave * 64))
            $leftPupilY = Get-RoundedInt ($wave * 7)
            $rightPupilY = $leftPupilY
            $targetPath = '<line x1="128" y1="33" x2="128" y2="161" stroke="#E63946" stroke-width="2" stroke-dasharray="5 6" opacity="0.45" />'
        }
        'GazeCircle' {
            $circlePhase = 2 * [Math]::PI * ($FrameIndex % 8) / 8
            $circleDirection = if ($FrameIndex -lt 8) { 1 } else { -1 }
            $circleWave = [Math]::Sin($circleDirection * $circlePhase)
            $circleCounterWave = [Math]::Cos($circleDirection * $circlePhase)
            $showTarget = $true
            $targetX = Get-RoundedInt (128 + ($circleWave * 68))
            $targetY = Get-RoundedInt ($headY - 7 + ($circleCounterWave * 55))
            $leftPupilX = Get-RoundedInt ($circleWave * 8)
            $rightPupilX = $leftPupilX
            $leftPupilY = Get-RoundedInt ($circleCounterWave * 6)
            $rightPupilY = $leftPupilY
            $targetPath = '<ellipse cx="128" cy="97" rx="68" ry="55" fill="none" stroke="#E63946" stroke-width="2" stroke-dasharray="5 6" opacity="0.45" />'
        }
        'GazeDiagonal' {
            $showTarget = $true
            $targetX = Get-RoundedInt (128 + ($wave * 66))
            $targetY = Get-RoundedInt ($headY - 7 + ($wave * 52))
            $leftPupilX = Get-RoundedInt ($wave * 8)
            $rightPupilX = $leftPupilX
            $leftPupilY = Get-RoundedInt ($wave * 6)
            $rightPupilY = $leftPupilY
            $targetPath = '<line x1="62" y1="45" x2="194" y2="149" stroke="#E63946" stroke-width="2" stroke-dasharray="5 6" opacity="0.45" />'
        }
        'GazeCross' {
            $localPhase = 2 * [Math]::PI * ($FrameIndex % 8) / 8
            $localWave = [Math]::Sin($localPhase)
            $diagonal = if ($FrameIndex -lt 8) { 1 } else { -1 }
            $showTarget = $true
            $targetX = Get-RoundedInt (128 + ($localWave * 66))
            $targetY = Get-RoundedInt ($headY - 7 + ($diagonal * $localWave * 52))
            $leftPupilX = Get-RoundedInt ($localWave * 8)
            $rightPupilX = $leftPupilX
            $leftPupilY = Get-RoundedInt ($diagonal * $localWave * 6)
            $rightPupilY = $leftPupilY
            $targetPath = '<path d="M62 45 L194 149 M194 45 L62 149" fill="none" stroke="#E63946" stroke-width="2" stroke-dasharray="5 6" opacity="0.45" />'
        }
        'GazeCrossSaccade' {
            $crossSequence = @(0, 2, 1, 3)
            $crossIndex = $crossSequence[[int][Math]::Floor($FrameIndex / 4)]
            $cornerX = @(-1, 1, 1, -1)[$crossIndex]
            $cornerY = @(-1, -1, 1, 1)[$crossIndex]
            $showTarget = $true
            $targetX = 128 + ($cornerX * 66)
            $targetY = $headY - 7 + ($cornerY * 52)
            $leftPupilX = $cornerX * 8
            $rightPupilX = $leftPupilX
            $leftPupilY = $cornerY * 6
            $rightPupilY = $leftPupilY
            $targetPath = '<path d="M62 45 L194 149 M194 45 L62 149" fill="none" stroke="#E63946" stroke-width="2" stroke-dasharray="5 6" opacity="0.45" />'
        }
        'GazeFigureEight' {
            $showTarget = $true
            $figureY = [Math]::Sin(2 * $phase)
            $targetX = Get-RoundedInt (128 + ($wave * 70))
            $targetY = Get-RoundedInt ($headY - 7 + ($figureY * 43))
            $leftPupilX = Get-RoundedInt ($wave * 8)
            $rightPupilX = $leftPupilX
            $leftPupilY = Get-RoundedInt ($figureY * 6)
            $rightPupilY = $leftPupilY
            $targetPath = '<path d="M58 97 C58 44 112 44 128 97 C144 150 198 150 198 97 C198 44 144 44 128 97 C112 150 58 150 58 97" fill="none" stroke="#E63946" stroke-width="2" stroke-dasharray="5 6" opacity="0.45" />'
        }
        'GazeNearFar' {
            $showTarget = $true
            $near = (1 + $wave) / 2
            $targetRadius = Get-RoundedInt (4 + ($near * 8))
            $targetY = Get-RoundedInt (45 + ($near * 22))
            $converge = Get-RoundedInt ($near * 5)
            $leftPupilX = $converge
            $rightPupilX = -$converge
        }
        'GazeVerticalNearFar' {
            $showTarget = $true
            $near = (1 + $wave) / 2
            $targetRadius = Get-RoundedInt (4 + ($near * 8))
            $targetY = Get-RoundedInt (38 + ($near * 105))
            $converge = Get-RoundedInt ($near * 5)
            $leftPupilX = $converge
            $rightPupilX = -$converge
            $leftPupilY = Get-RoundedInt (($targetY - ($headY - 7)) / 10)
            $rightPupilY = $leftPupilY
            $targetPath = '<line x1="128" y1="38" x2="128" y2="143" stroke="#E63946" stroke-width="2" stroke-dasharray="5 6" opacity="0.45" />'
        }
        'GazeVerticalLadder' {
            $ladderSequence = @(0, 1, 2, 3, 4, 3, 2, 1)
            $ladderIndex = $ladderSequence[[int][Math]::Floor($FrameIndex / 2)]
            $targetX = 128
            $targetY = 37 + ($ladderIndex * 29)
            $showTarget = $true
            $leftPupilY = Get-RoundedInt (($targetY - ($headY - 7)) / 9)
            $rightPupilY = $leftPupilY
            $targetPath = '<path d="M116 37 H140 M116 66 H140 M116 95 H140 M116 124 H140 M116 153 H140" fill="none" stroke="#E63946" stroke-width="2" stroke-dasharray="4 5" opacity="0.45" />'
        }
        'GazePeripheral' {
            $peripheralIndex = [int][Math]::Floor($FrameIndex / 4)
            $targetX = @(62, 194, 194, 62)[$peripheralIndex]
            $targetY = @(45, 45, 149, 149)[$peripheralIndex]
            $showTarget = $true
            $targetPath = '<circle cx="62" cy="45" r="5" fill="none" stroke="#E63946" stroke-width="2" opacity="0.35" /><circle cx="194" cy="45" r="5" fill="none" stroke="#E63946" stroke-width="2" opacity="0.35" /><circle cx="194" cy="149" r="5" fill="none" stroke="#E63946" stroke-width="2" opacity="0.35" /><circle cx="62" cy="149" r="5" fill="none" stroke="#E63946" stroke-width="2" opacity="0.35" />'
        }
        'GazeConvergence' {
            $showTarget = $true
            $targetX = 128
            $targetY = if ($ExerciseName -match 'Bhrumadhya') { 76 } elseif ($ExerciseName -match 'Nasagra') { 126 } else { 105 }
            $converge = Get-RoundedInt ((1 + $wave) * 3)
            $leftPupilX = $converge
            $rightPupilX = -$converge
            $leftPupilY = Get-RoundedInt (($targetY - ($headY - 7)) / 10)
            $rightPupilY = $leftPupilY
        }
        'GazeHorizontalSaccade' {
            $showTarget = $true
            $targetX = 128 + ($stepHorizontal * 76)
            $targetY = $headY - 7
            $leftPupilX = $stepHorizontal * 8
            $rightPupilX = $leftPupilX
        }
        'GazeVerticalSaccade' {
            $showTarget = $true
            $targetX = 128
            $targetY = $headY - 7 + ($stepHorizontal * 64)
            $leftPupilY = $stepHorizontal * 7
            $rightPupilY = $leftPupilY
        }
        'GazeCornerSaccade' {
            $cornerSequence = @(0, 1, 2, 3, 0, 3, 2, 1)
            $corner = $cornerSequence[[int][Math]::Floor($FrameIndex / 2)]
            $cornerX = @(-1, 1, 1, -1)[$corner]
            $cornerY = @(-1, -1, 1, 1)[$corner]
            $showTarget = $true
            $targetX = 128 + ($cornerX * 66)
            $targetY = $headY - 7 + ($cornerY * 52)
            $leftPupilX = $cornerX * 8
            $rightPupilX = $leftPupilX
            $leftPupilY = $cornerY * 6
            $rightPupilY = $leftPupilY
            $targetPath = '<rect x="62" y="45" width="132" height="104" fill="none" stroke="#E63946" stroke-width="2" stroke-dasharray="5 6" opacity="0.45" />'
        }
        'GazeClockSaccade' {
            $clockIndex = if ($FrameIndex -lt 8) { $FrameIndex } else { 15 - $FrameIndex }
            $clockPhase = 2 * [Math]::PI * $clockIndex / 8
            $clockX = [Math]::Sin($clockPhase)
            $clockY = -[Math]::Cos($clockPhase)
            $showTarget = $true
            $targetX = Get-RoundedInt (128 + ($clockX * 68))
            $targetY = Get-RoundedInt ($headY - 7 + ($clockY * 55))
            $leftPupilX = Get-RoundedInt ($clockX * 8)
            $rightPupilX = $leftPupilX
            $leftPupilY = Get-RoundedInt ($clockY * 6)
            $rightPupilY = $leftPupilY
        }
        'GazeTriangleSaccade' {
            $triangleSequence = @(0, 0, 1, 1, 1, 2, 2, 2, 0, 0, 2, 2, 2, 1, 1, 1)
            $triangleIndex = $triangleSequence[$FrameIndex]
            $targetX = @(128, 62, 194)[$triangleIndex]
            $targetY = @(40, 149, 149)[$triangleIndex]
            $showTarget = $true
            $leftPupilX = Get-RoundedInt (($targetX - 128) / 8.25)
            $rightPupilX = $leftPupilX
            $leftPupilY = Get-RoundedInt (($targetY - ($headY - 7)) / 9)
            $rightPupilY = $leftPupilY
            $targetPath = '<path d="M128 40 L62 149 L194 149 Z" fill="none" stroke="#E63946" stroke-width="2" stroke-dasharray="5 6" opacity="0.45" />'
        }
        'VorHorizontal' {
            $showTarget = $true
            $targetX = 128
            $targetY = 37
            $turn = $wave
            $leftPupilX = Get-RoundedInt (-$wave * 6)
            $rightPupilX = $leftPupilX
        }
        'VorVertical' {
            $showTarget = $true
            $targetX = 128
            $targetY = 37
            $sideProfile = $true
            $rotation = Get-RoundedInt ($wave * 12)
            $leftPupilY = Get-RoundedInt (-$wave * 5)
            $rightPupilY = $leftPupilY
        }
        'VorFourDirection' {
            $showTarget = $true
            $targetX = 128
            $targetY = 37
            $localPhase = 2 * [Math]::PI * ($FrameIndex % 8) / 8
            $localWave = [Math]::Sin($localPhase)
            if ($FrameIndex -lt 8) {
                $turn = $localWave
                $leftPupilX = Get-RoundedInt (-$localWave * 6)
                $rightPupilX = $leftPupilX
            }
            else {
                $sideProfile = $true
                $rotation = Get-RoundedInt ($localWave * 12)
                $leftPupilY = Get-RoundedInt (-$localWave * 5)
                $rightPupilY = $leftPupilY
            }
        }
        'VorCancellationHorizontal' {
            $showTarget = $true
            $turn = $wave
            $targetX = Get-RoundedInt (128 + ($wave * 62))
            $targetY = 37
        }
        'VorCancellationVertical' {
            $showTarget = $true
            $sideProfile = $true
            $rotation = Get-RoundedInt ($wave * 12)
            $targetX = 128
            $targetY = Get-RoundedInt (37 + ($wave * 48))
        }
        'VorX2Horizontal' {
            $showTarget = $true
            $turn = $wave
            $targetX = Get-RoundedInt (128 - ($wave * 62))
            $targetY = 37
            $leftPupilX = Get-RoundedInt (-$wave * 8)
            $rightPupilX = $leftPupilX
        }
        'VorX2Vertical' {
            $showTarget = $true
            $sideProfile = $true
            $rotation = Get-RoundedInt ($wave * 12)
            $targetX = 128
            $targetY = Get-RoundedInt (37 - ($wave * 48))
            $leftPupilY = Get-RoundedInt (-$wave * 6)
            $rightPupilY = $leftPupilY
        }
    }

    $eyeRy = [Math]::Max(1, (Get-RoundedInt (9 * (1 - $blink))))
    $pupilRadius = [Math]::Max(0, (Get-RoundedInt (4 * (1 - $blink))))
    $eyeSpacing = Get-RoundedInt (18 - ([Math]::Abs($turn) * 7))
    $faceShift = Get-RoundedInt ($turn * 8)
    $leftEyeX = $headX - $eyeSpacing + $faceShift
    $rightEyeX = $headX + $eyeSpacing + $faceShift
    $eyeY = $headY - 7

    if ($sideProfile) {
        $eyesSvg = @"
    <ellipse cx="$($headX + 13)" cy="$($headY - 8)" rx="11" ry="$eyeRy" fill="white" />
    <circle cx="$($headX + 16 + $rightPupilX)" cy="$($headY - 8 + $rightPupilY)" r="$pupilRadius" fill="#17324D" />
    <path d="M$($headX + 42) $($headY - 4) L$($headX + 55) $($headY + 2) L$($headX + 42) $($headY + 7)" fill="$Accent" stroke="#17324D" stroke-width="3" stroke-linejoin="round" />
"@
    }
    else {
        $eyesSvg = @"
    <ellipse cx="$leftEyeX" cy="$eyeY" rx="12" ry="$eyeRy" fill="white" />
    <ellipse cx="$rightEyeX" cy="$eyeY" rx="12" ry="$eyeRy" fill="white" />
    <circle cx="$($leftEyeX + $leftPupilX)" cy="$($eyeY + $leftPupilY)" r="$pupilRadius" fill="#17324D" />
    <circle cx="$($rightEyeX + $rightPupilX)" cy="$($eyeY + $rightPupilY)" r="$pupilRadius" fill="#17324D" />
"@
    }

    $turnNose = if ([Math]::Abs($turn) -gt 0.1) {
        $noseEndX = Get-RoundedInt ($headX + ($turn * 35))
        $noseEndY = $headY + 4
        '<line x1="{0}" y1="{1}" x2="{2}" y2="{3}" stroke="#17324D" stroke-width="3" stroke-linecap="round" />' -f ($headX + $faceShift), $headY, $noseEndX, $noseEndY
    }
    else { '' }

    $targetPathSvg = if ($showTarget) { $targetPath } else { '' }
    $targetDotSvg = if ($showTarget) {
        '<circle cx="{0}" cy="{1}" r="{2}" fill="#E63946" stroke="white" stroke-width="2" />' -f $targetX, $targetY, $targetRadius
    }
    else { '' }

    return @"
<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
  <metadata>Flux real head exercise $ExerciseId frame $FrameIndex</metadata>
  <rect width="256" height="256" rx="28" fill="#F7FAFC" />
  <circle cx="128" cy="126" r="102" fill="$Accent" opacity="0.08" />
  $targetPathSvg
  <path d="M62 220 Q72 166 108 158 L148 158 Q184 166 194 220" fill="#17324D" />
  <line x1="128" y1="160" x2="$headX" y2="$($headY + 40)" stroke="#17324D" stroke-width="18" stroke-linecap="round" />
  <g transform="rotate($rotation $headX $headY)">
    <circle cx="$headX" cy="$headY" r="49" fill="$Accent" />
    $eyesSvg
    $turnNose
    <line x1="$($headX - 9)" y1="$($headY + 22)" x2="$($headX + 9)" y2="$($headY + 22)" stroke="#17324D" stroke-width="4" stroke-linecap="round" />
  </g>
  $targetDotSvg
  <line x1="40" y1="224" x2="216" y2="224" stroke="#C9D7E3" stroke-width="4" stroke-linecap="round" />
</svg>
"@
}

function Get-RoundedInt {
    param([double]$Value)
    return [int][Math]::Round($Value, [MidpointRounding]::AwayFromZero)
}

function Get-YtDlpPath {
    param([string]$WorkingRoot)

    $ytDlpPath = Join-Path $WorkingRoot 'yt-dlp.exe'
    if (-not (Test-Path -LiteralPath $ytDlpPath)) {
        $ytDlpUrl = 'https://github.com/yt-dlp/yt-dlp/releases/' +
            'download/2026.07.04/yt-dlp.exe'
        Invoke-WebRequest -Uri $ytDlpUrl -OutFile $ytDlpPath
    }

    return $ytDlpPath
}

function New-ExternalExerciseGif {
    param(
        [int]$ExerciseId,
        [string]$ExerciseName,
        [hashtable]$Media,
        [string]$GifPath,
        [string]$WorkingRoot
    )

    $sourceRoot = Join-Path $WorkingRoot 'external-sources'
    $frameRoot = Join-Path $WorkingRoot ('external-frames-{0:D4}' -f $ExerciseId)
    New-Item -ItemType Directory -Force -Path $sourceRoot, $frameRoot | Out-Null

    $sourcePath = Join-Path $sourceRoot ([string]$Media.File)
    if (-not (Test-Path -LiteralPath $sourcePath)) {
        $sourceUrl = if ($Media.ContainsKey('Url')) {
            [string]$Media.Url
        }
        else {
            'https://raw.githubusercontent.com/hasaneyldrm/' +
                'exercises-dataset/main/videos/' + $Media.File
        }

        if ($Media.ContainsKey('Youtube') -and [bool]$Media.Youtube) {
            $ytDlpPath = Get-YtDlpPath -WorkingRoot $WorkingRoot
            & $ytDlpPath `
                --no-playlist `
                --no-warnings `
                --no-progress `
                --retries 5 `
                --fragment-retries 5 `
                --retry-sleep 1 `
                --format 'bv[height<=480][vcodec^=avc1]/bv[height<=480]/b[height<=480]/worst' `
                --output $sourcePath `
                $sourceUrl

            if ($LASTEXITCODE -ne 0 -or
                -not (Test-Path -LiteralPath $sourcePath)) {
                throw "Could not download reviewed video for $ExerciseName."
            }
        }
        else {
            Invoke-WebRequest `
                -Uri $sourceUrl `
                -Headers @{ 'User-Agent' = 'Flux private exercise catalog/1.0' } `
                -OutFile $sourcePath
        }
    }

    $framePattern = Join-Path $frameRoot 'frame_%04d.png'
    if ($Media.ContainsKey('Video') -and [bool]$Media.Video) {
        $culture = [Globalization.CultureInfo]::InvariantCulture
        $framesPerSecond = if ($Media.ContainsKey('FramesPerSecond')) {
            [int]$Media.FramesPerSecond
        }
        else {
            10
        }
        $ffmpegArguments = @('-hide_banner', '-loglevel', 'error', '-y')

        if ($Media.ContainsKey('StartSeconds')) {
            $ffmpegArguments += @(
                '-ss',
                ([double]$Media.StartSeconds).ToString('0.###', $culture))
        }

        $ffmpegArguments += @('-i', $sourcePath)

        if ($Media.ContainsKey('DurationSeconds')) {
            $ffmpegArguments += @(
                '-t',
                ([double]$Media.DurationSeconds).ToString('0.###', $culture))
        }

        $videoFilter = "fps=$framesPerSecond," +
            'scale=256:256:force_original_aspect_ratio=decrease,' +
            'pad=256:256:(ow-iw)/2:(oh-ih)/2:color=0xF7FAFC'
        $ffmpegArguments += @('-vf', $videoFilter, '-an', $framePattern)
        & ffmpeg @ffmpegArguments
    }
    else {
        & magick $sourcePath `
            -coalesce `
            -resize '256x256' `
            -background '#F7FAFC' `
            -gravity center `
            -extent '256x256' `
            $framePattern
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Could not normalize external media for $ExerciseName."
    }

    $framePaths = @(
        Get-ChildItem -LiteralPath $frameRoot -Filter 'frame_*.png' |
            Sort-Object Name |
            Select-Object -ExpandProperty FullName)

    if ($framePaths.Count -lt 2) {
        throw "External media for $ExerciseName is not animated."
    }

    if ($Media.MirrorForAlternation) {
        $mirroredPaths = [System.Collections.Generic.List[string]]::new()
        for ($index = 0; $index -lt $framePaths.Count; $index++) {
            $mirroredPath = Join-Path $frameRoot ('mirror_{0:D4}.png' -f $index)
            & magick $framePaths[$index] -flop $mirroredPath
            if ($LASTEXITCODE -ne 0) {
                throw "Could not mirror external media for $ExerciseName."
            }
            $mirroredPaths.Add($mirroredPath)
        }
        $framePaths += @($mirroredPaths)
    }

    $isPingPong = $Media.ContainsKey('PingPong') -and [bool]$Media.PingPong
    if ($isPingPong) {
        $returnPaths = [System.Collections.Generic.List[string]]::new()
        for ($index = $framePaths.Count - 2; $index -ge 1; $index--) {
            $returnPaths.Add($framePaths[$index])
        }
        $framePaths += @($returnPaths)
    }

    $frameDelay = if ($Media.ContainsKey('DelayCentiseconds')) {
        [int]$Media.DelayCentiseconds
    }
    elseif ($Media.ContainsKey('Video') -and [bool]$Media.Video) {
        [Math]::Max(1, [int][Math]::Round(100 / [int]$Media.FramesPerSecond))
    }
    else {
        8
    }

    $gifInputPaths = if ($isPingPong) {
        @($framePaths)
    }
    else {
        $patterns = @((Join-Path $frameRoot 'frame_*.png'))
        if ($Media.MirrorForAlternation) {
            $patterns += (Join-Path $frameRoot 'mirror_*.png')
        }
        $patterns
    }

    $gifArguments = @($gifInputPaths) + @(
        '-set', 'delay', $frameDelay.ToString(),
        '-set', 'dispose', 'background',
        '-set', 'comment', "Flux reviewed exercise $ExerciseId - $ExerciseName",
        '-loop', '0',
        '-layers', 'Optimize',
        $GifPath)
    & magick @gifArguments

    if ($LASTEXITCODE -ne 0) {
        throw "Could not encode external media for $ExerciseName."
    }
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
            -ExerciseName $ExerciseName `
            -MotionProfile $MotionProfile `
            -FrameIndex $FrameIndex `
            -Accent $accent
    }

    $phase = 2 * [Math]::PI * $FrameIndex / 16
    $wave = [Math]::Sin($phase)
    $counterWave = [Math]::Cos($phase)
    if ($ExerciseName -match 'Bidirectional') {
        $localPhase = 2 * [Math]::PI * ($FrameIndex % 8) / 8
        $direction = if ($FrameIndex -lt 8) { 1 } else { -1 }
        $wave = [Math]::Sin($direction * $localPhase)
        $counterWave = [Math]::Cos($direction * $localPhase)
    }
    $leftAction = [Math]::Max(0, $wave)
    $rightAction = [Math]::Max(0, -$wave)
    $alternatingAction = [Math]::Abs($wave)
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
                    $sideFrame = $FrameIndex % 8
                    $sidePhase = 2 * [Math]::PI * $sideFrame / 8
                    $sideWave = [Math]::Sin($sidePhase)
                    $sideCounterWave = [Math]::Cos($sidePhase)
                    if ($FrameIndex -lt 8) {
                        $leftFootX += Get-RoundedInt ($sideWave * $amplitude)
                        $leftFootY -= Get-RoundedInt ((1 - $sideCounterWave) * $amplitude * 0.35)
                    }
                    else {
                        $rightFootX -= Get-RoundedInt ($sideWave * $amplitude)
                        $rightFootY -= Get-RoundedInt ((1 - $sideCounterWave) * $amplitude * 0.35)
                    }
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
                    $leftLift = $leftAction * $amplitude * 2.4
                    $rightLift = $rightAction * $amplitude * 2.4
                    $leftKneeY -= Get-RoundedInt $leftLift
                    $leftFootY -= Get-RoundedInt ($leftLift * 0.85)
                    $leftFootX += Get-RoundedInt ($leftAction * $amplitude)
                    $rightKneeY -= Get-RoundedInt $rightLift
                    $rightFootY -= Get-RoundedInt ($rightLift * 0.85)
                    $rightFootX -= Get-RoundedInt ($rightAction * $amplitude)
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
                'HeelRaise' {
                    $baseBend = $amplitude * 0.8
                    $rise = [Math]::Max(0, $wave) * $amplitude * 1.5
                    $hipY += Get-RoundedInt ($baseBend - $rise)
                    $leftKneeX -= Get-RoundedInt ($baseBend * 0.45)
                    $rightKneeX += Get-RoundedInt ($baseBend * 0.45)
                    $leftKneeY += Get-RoundedInt (($baseBend * 0.25) - $rise)
                    $rightKneeY += Get-RoundedInt (($baseBend * 0.25) - $rise)
                    $leftFootY -= Get-RoundedInt ($rise * 0.15)
                    $rightFootY -= Get-RoundedInt ($rise * 0.15)
                }
                'Lunge' {
                    $spread = $alternatingAction * $amplitude * 2.2
                    $leftFootX -= Get-RoundedInt ($leftAction * $spread)
                    $rightFootX += Get-RoundedInt ($leftAction * $spread)
                    $leftFootX += Get-RoundedInt ($rightAction * $spread)
                    $rightFootX -= Get-RoundedInt ($rightAction * $spread)
                    $leftKneeY += Get-RoundedInt ($rightAction * $amplitude * 1.35)
                    $rightKneeY += Get-RoundedInt ($leftAction * $amplitude * 1.35)
                    $hipY += Get-RoundedInt ($alternatingAction * $amplitude * 0.75)
                }
                'KneeCurl' {
                    $leftLift = $leftAction * $amplitude * 2.5
                    $rightLift = $rightAction * $amplitude * 2.5
                    $leftFootY -= Get-RoundedInt $leftLift
                    $leftFootX += Get-RoundedInt ($leftLift * 0.45)
                    $rightFootY -= Get-RoundedInt $rightLift
                    $rightFootX -= Get-RoundedInt ($rightLift * 0.45)
                }
                'KneeLift' {
                    $leftLift = $leftAction * $amplitude * 3.2
                    $rightLift = $rightAction * $amplitude * 3.2
                    $leftKneeY -= Get-RoundedInt $leftLift
                    $leftFootY -= Get-RoundedInt ($leftLift * 0.72)
                    $leftKneeX += Get-RoundedInt ($leftAction * $amplitude * 0.7)
                    $rightKneeY -= Get-RoundedInt $rightLift
                    $rightFootY -= Get-RoundedInt ($rightLift * 0.72)
                    $rightKneeX -= Get-RoundedInt ($rightAction * $amplitude * 0.7)
                }
                'LegSide' {
                    $leftFootX -= Get-RoundedInt ($leftAction * $amplitude * 3.1)
                    $leftFootY -= Get-RoundedInt ($leftAction * $amplitude * 1.25)
                    $leftKneeX -= Get-RoundedInt ($leftAction * $amplitude * 1.6)
                    $rightFootX += Get-RoundedInt ($rightAction * $amplitude * 3.1)
                    $rightFootY -= Get-RoundedInt ($rightAction * $amplitude * 1.25)
                    $rightKneeX += Get-RoundedInt ($rightAction * $amplitude * 1.6)
                }
                'LegBack' {
                    $leftFootX -= Get-RoundedInt ($leftAction * $amplitude * 2.1)
                    $leftFootY -= Get-RoundedInt ($leftAction * $amplitude * 1.55)
                    $rightFootX += Get-RoundedInt ($rightAction * $amplitude * 2.1)
                    $rightFootY -= Get-RoundedInt ($rightAction * $amplitude * 1.55)
                    $hipX += Get-RoundedInt (($leftAction - $rightAction) * $amplitude * 0.65)
                }
                'LegFront' {
                    $leftFootX += Get-RoundedInt ($leftAction * $amplitude * 2.8)
                    $leftFootY -= Get-RoundedInt ($leftAction * $amplitude * 2.1)
                    $leftKneeX += Get-RoundedInt ($leftAction * $amplitude * 1.4)
                    $rightFootX -= Get-RoundedInt ($rightAction * $amplitude * 2.8)
                    $rightFootY -= Get-RoundedInt ($rightAction * $amplitude * 2.1)
                    $rightKneeX -= Get-RoundedInt ($rightAction * $amplitude * 1.4)
                }
                default {
                    $leftLift = $leftAction * $amplitude * 2.6
                    $rightLift = $rightAction * $amplitude * 2.6
                    $leftKneeY -= Get-RoundedInt $leftLift
                    $leftFootY -= Get-RoundedInt ($leftLift * 0.8)
                    $leftFootX += Get-RoundedInt ($leftAction * $amplitude)
                    $rightKneeY -= Get-RoundedInt $rightLift
                    $rightFootY -= Get-RoundedInt ($rightLift * 0.8)
                    $rightFootX -= Get-RoundedInt ($rightAction * $amplitude)
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
                    $leftHandX += Get-RoundedInt ($leftAction * $amplitude * 3.3)
                    $leftHandY -= Get-RoundedInt ($leftAction * $amplitude * 0.7)
                    $leftElbowX += Get-RoundedInt ($leftAction * $amplitude * 1.6)
                    $rightHandX -= Get-RoundedInt ($rightAction * $amplitude * 3.3)
                    $rightHandY -= Get-RoundedInt ($rightAction * $amplitude * 0.7)
                    $rightElbowX -= Get-RoundedInt ($rightAction * $amplitude * 1.6)
                }
                'BentArmStrike' {
                    $leftElbowY -= Get-RoundedInt ($leftAction * $amplitude * 2.5)
                    $leftHandX += Get-RoundedInt ($leftAction * $amplitude * 1.8)
                    $leftHandY -= Get-RoundedInt ($leftAction * $amplitude * 1.5)
                    $rightElbowY -= Get-RoundedInt ($rightAction * $amplitude * 2.5)
                    $rightHandX -= Get-RoundedInt ($rightAction * $amplitude * 1.8)
                    $rightHandY -= Get-RoundedInt ($rightAction * $amplitude * 1.5)
                }
                'ArmBlock' {
                    $leftElbowX += Get-RoundedInt ($leftAction * $amplitude * 1.4)
                    $leftElbowY -= Get-RoundedInt ($leftAction * $amplitude * 1.5)
                    $leftHandX += Get-RoundedInt ($leftAction * $amplitude * 2.2)
                    $leftHandY -= Get-RoundedInt ($leftAction * $amplitude * 2.7)
                    $rightElbowX -= Get-RoundedInt ($rightAction * $amplitude * 1.4)
                    $rightElbowY -= Get-RoundedInt ($rightAction * $amplitude * 1.5)
                    $rightHandX -= Get-RoundedInt ($rightAction * $amplitude * 2.2)
                    $rightHandY -= Get-RoundedInt ($rightAction * $amplitude * 2.7)
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
                    $leftFootX += Get-RoundedInt ($leftAction * $amplitude * 2.5)
                    $leftFootY -= Get-RoundedInt ($leftAction * $amplitude * 1.15)
                    $leftKneeX += Get-RoundedInt ($leftAction * $amplitude * 1.25)
                    $rightFootX -= Get-RoundedInt ($rightAction * $amplitude * 2.5)
                    $rightFootY -= Get-RoundedInt ($rightAction * $amplitude * 1.15)
                    $rightKneeX -= Get-RoundedInt ($rightAction * $amplitude * 1.25)
                }
                'HipRotation' {
                    $hipX += Get-RoundedInt ($wave * $amplitude * 1.2)
                    $leftShoulderX -= Get-RoundedInt ($wave * $amplitude * 0.8)
                    $rightShoulderX -= Get-RoundedInt ($wave * $amplitude * 0.8)
                    $leftKneeX += Get-RoundedInt ($counterWave * $amplitude * 0.75)
                    $rightKneeX -= Get-RoundedInt ($counterWave * $amplitude * 0.75)
                }
                default {
                    $leftKneeX -= Get-RoundedInt ($leftAction * $amplitude * 1.6)
                    $leftKneeY -= Get-RoundedInt ($leftAction * $amplitude * 1.3)
                    $leftFootY -= Get-RoundedInt ($leftAction * $amplitude)
                    $rightKneeX += Get-RoundedInt ($rightAction * $amplitude * 1.6)
                    $rightKneeY -= Get-RoundedInt ($rightAction * $amplitude * 1.3)
                    $rightFootY -= Get-RoundedInt ($rightAction * $amplitude)
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
                    $leftFootY -= Get-RoundedInt ($leftAction * $amplitude * 1.7)
                    $rightFootY -= Get-RoundedInt ($rightAction * $amplitude * 1.7)
                    $leftHandY -= Get-RoundedInt ($alternatingAction * $amplitude * 2)
                    $rightHandY -= Get-RoundedInt ($alternatingAction * $amplitude * 2)
                }
            }
        }
        'CORE' {
            switch ($MotionProfile) {
                'CoreCrunch' {
                    $rightLift = $leftAction * $amplitude * 2.5
                    $leftLift = $rightAction * $amplitude * 2.5
                    $rightKneeY -= Get-RoundedInt $rightLift
                    $rightFootY -= Get-RoundedInt ($rightLift * 0.75)
                    $rightKneeX -= Get-RoundedInt ($rightLift * 0.38)
                    $leftElbowY += Get-RoundedInt ($rightLift * 0.5)
                    $leftElbowX += Get-RoundedInt ($rightLift * 0.35)
                    $leftKneeY -= Get-RoundedInt $leftLift
                    $leftFootY -= Get-RoundedInt ($leftLift * 0.75)
                    $leftKneeX += Get-RoundedInt ($leftLift * 0.38)
                    $rightElbowY += Get-RoundedInt ($leftLift * 0.5)
                    $rightElbowX -= Get-RoundedInt ($leftLift * 0.35)
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
                    $rightLift = $leftAction * $amplitude * 2.5
                    $leftLift = $rightAction * $amplitude * 2.5
                    $rightKneeY -= Get-RoundedInt $rightLift
                    $rightFootY -= Get-RoundedInt ($rightLift * 0.8)
                    $leftKneeY -= Get-RoundedInt $leftLift
                    $leftFootY -= Get-RoundedInt ($leftLift * 0.8)
                    $leftHandY -= Get-RoundedInt ($alternatingAction * $amplitude * 0.7)
                    $rightHandY -= Get-RoundedInt ($alternatingAction * $amplitude * 0.7)
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
                    $leftHandX += Get-RoundedInt ($leftAction * $amplitude * 2.8)
                    $rightKneeY -= Get-RoundedInt ($leftAction * $amplitude * 2.4)
                    $rightFootY -= Get-RoundedInt ($leftAction * $amplitude * 1.5)
                    $rightHandX -= Get-RoundedInt ($rightAction * $amplitude * 2.8)
                    $leftKneeY -= Get-RoundedInt ($rightAction * $amplitude * 2.4)
                    $leftFootY -= Get-RoundedInt ($rightAction * $amplitude * 1.5)
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

function New-HoldFrameImage {
    param(
        [string]$GifPath,
        [string]$OutputPath,
        [ValidateRange(1, 99)]
        [int]$FramePercent,
        [switch]$Overwrite
    )

    if ((Test-Path -LiteralPath $OutputPath) -and -not $Overwrite) {
        return
    }

    $frameCountLines = @(& magick identify -format "%n`n" $GifPath)
    if ($LASTEXITCODE -ne 0 -or $frameCountLines.Count -eq 0) {
        throw "ImageMagick could not inspect hold animation $GifPath."
    }

    $frameCount = [int]$frameCountLines[0]
    if ($frameCount -lt 1) {
        throw "Hold animation $GifPath has no frames."
    }

    $frameIndex = [Math]::Min(
        $frameCount - 1,
        [Math]::Floor(($frameCount - 1) * ($FramePercent / 100.0)))
    $magickArguments = @($GifPath, '-coalesce')
    if ($frameIndex -gt 0) {
        $magickArguments += @('-delete', "0-$($frameIndex - 1)")
    }
    $magickArguments += @('-delete', '1--1', '-strip', $OutputPath)

    & magick @magickArguments
    if ($LASTEXITCODE -ne 0) {
        throw "ImageMagick failed while rendering hold frame $OutputPath."
    }
}

$resolvedOutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$gifOutputRoot = Join-Path $resolvedOutputRoot 'exercise_gifs'
$holdFrameOutputRoot = Join-Path $resolvedOutputRoot 'exercise_hold_frames'
$catalogPath = Join-Path $resolvedOutputRoot 'exercises.json'
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("FluxExerciseFrames-" + [Guid]::NewGuid().ToString('N'))

New-Item -ItemType Directory -Force -Path $resolvedOutputRoot | Out-Null
New-Item -ItemType Directory -Force -Path $gifOutputRoot | Out-Null
New-Item -ItemType Directory -Force -Path $holdFrameOutputRoot | Out-Null
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
        $sourceExerciseName = $regionNames[$movementIndex]
        $exerciseName = if ($bilateralExerciseNames.ContainsKey($exerciseId)) {
            $bilateralExerciseNames[$exerciseId]
        }
        else {
            $sourceExerciseName
        }
        $practice = Get-Practice -Name $exerciseName
        $motionProfile = Get-MotionProfile -Region $region -Name $exerciseName
        $isHold = $holdExerciseFrames.ContainsKey($exerciseId)
        $exerciseMode = if ($isHold) { 'Hold' } else { 'Repetition' }
        $holdFramePercent = if ($isHold) {
            [int]$holdExerciseFrames[$exerciseId]
        }
        else {
            0
        }
        $gifFileName = 'exercise_{0:D4}.gif' -f $exerciseId
        $gifRelativePath = "exercise_gifs/$gifFileName"

        $records.Add([ordered]@{
            id = $exerciseId
            name = $exerciseName
            gif = $gifRelativePath
            dominantRegion = $region
            practice = $practice
            motionProfile = $motionProfile
            mode = $exerciseMode
            holdFramePercent = $holdFramePercent
            score = 0
            onlyFeetTouchGround = $true
            shoeAgnostic = $true
            maxSpaceMeters = 3
            equipment = 'None'
            silent = $true
        })

        $isSelected = if ($ExerciseIds.Count -gt 0) {
            $ExerciseIds -contains $exerciseId
        }
        else {
            $exerciseId -ge $StartExercise -and
                ($MaxExercises -eq 0 -or $exerciseId -le $MaxExercises)
        }

        if (-not $isSelected) {
            continue
        }

        $gifPath = Join-Path $gifOutputRoot $gifFileName
        $holdFramePath = Join-Path $holdFrameOutputRoot (
            'exercise_{0:D4}.png' -f $exerciseId)

        if ((Test-Path -LiteralPath $gifPath) -and -not $Force) {
            if ($isHold) {
                New-HoldFrameImage `
                    -GifPath $gifPath `
                    -OutputPath $holdFramePath `
                    -FramePercent $holdFramePercent
            }
            continue
        }

        if ($posecodeExerciseMedia.ContainsKey($exerciseId)) {
            if (-not (Test-Path -LiteralPath $gifPath)) {
                throw "The reviewed Posecode asset is missing for $exerciseName."
            }

            if ($isHold) {
                New-HoldFrameImage `
                    -GifPath $gifPath `
                    -OutputPath $holdFramePath `
                    -FramePercent $holdFramePercent `
                    -Overwrite:$Force
            }
            continue
        }

        if ($externalExerciseMedia.ContainsKey($exerciseId)) {
            New-ExternalExerciseGif `
                -ExerciseId $exerciseId `
                -ExerciseName $exerciseName `
                -Media $externalExerciseMedia[$exerciseId] `
                -GifPath $gifPath `
                -WorkingRoot $tempRoot
            if ($isHold) {
                New-HoldFrameImage `
                    -GifPath $gifPath `
                    -OutputPath $holdFramePath `
                    -FramePercent $holdFramePercent `
                    -Overwrite:$Force
            }
            continue
        }

        $framePaths = @()

        for ($frameIndex = 0; $frameIndex -lt 16; $frameIndex++) {
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

        if ($isHold) {
            New-HoldFrameImage `
                -GifPath $gifPath `
                -OutputPath $holdFramePath `
                -FramePercent $holdFramePercent `
                -Overwrite:$Force
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
    $_['mode'] -notin @('Repetition', 'Hold') -or
    ($_['mode'] -eq 'Repetition' -and $_['holdFramePercent'] -ne 0) -or
    ($_['mode'] -eq 'Hold' -and (
        $_['holdFramePercent'] -lt 1 -or $_['holdFramePercent'] -gt 99)) -or
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

if ($MaxExercises -eq 0 -and $ExerciseIds.Count -eq 0) {
    $missingGifs = $records | Where-Object {
        -not (Test-Path -LiteralPath (Join-Path $resolvedOutputRoot $_['gif']))
    }

    if ($missingGifs) {
        throw 'At least one catalog record is missing its GIF asset.'
    }

    $missingHoldFrames = $records | Where-Object {
        $_['mode'] -eq 'Hold' -and
        -not (Test-Path -LiteralPath (Join-Path $holdFrameOutputRoot (
                    'exercise_{0:D4}.png' -f $_['id'])))
    }

    if ($missingHoldFrames) {
        throw 'At least one hold record is missing its static countdown frame.'
    }
}

$records | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $catalogPath -Encoding utf8

$resolvedTempRoot = [IO.Path]::GetFullPath($tempRoot)
$systemTempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
if (-not $resolvedTempRoot.StartsWith(
        $systemTempRoot,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Refusing to remove a generator working directory outside the system temp folder.'
}
Remove-Item -LiteralPath $resolvedTempRoot -Recurse -Force

Write-Output "Catalog: $catalogPath"
Write-Output "Records: $($records.Count)"
Write-Output "GIF directory: $gifOutputRoot"
