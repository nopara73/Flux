param(
    [string]$AssetsRoot = (Join-Path $PSScriptRoot '..\Flux\Assets')
)

$ErrorActionPreference = 'Stop'

$resolvedAssetsRoot = [IO.Path]::GetFullPath($AssetsRoot)
$catalogPath = Join-Path $resolvedAssetsRoot 'exercises.json'
$catalog = @(Get-Content -LiteralPath $catalogPath -Raw | ConvertFrom-Json)
$failures = [System.Collections.Generic.List[string]]::new()
$reviewedWorkoutCadenceDurationRanges = @{
    251 = @{ Minimum = 3.9; Maximum = 4.1 }
    231 = @{ Minimum = 7.9; Maximum = 8.1 }
    681 = @{ Minimum = 5.3; Maximum = 5.7 }
    684 = @{ Minimum = 8.1; Maximum = 8.3 }
    685 = @{ Minimum = 2.9; Maximum = 3.1 }
    687 = @{ Minimum = 8.1; Maximum = 8.3 }
}

if ($catalog.Count -lt 30) {
    throw "Expected at least 30 catalog records, found $($catalog.Count)."
}

$canonicalTaxonomy = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'CanonicalMuscleGroups.psd1') -SkipLimitCheck
$canonicalGroups = @($canonicalTaxonomy.Groups | Sort-Object { [int]$_.Id })
$canonicalGroupKeys = @(
    $canonicalGroups | ForEach-Object { [string]$_.StableKey })
if ($canonicalGroups.Count -ne 30 -or
    @(Compare-Object @($canonicalGroups.Id) @(1..30)).Count -gt 0 -or
    @($canonicalGroupKeys | Sort-Object -Unique).Count -ne 30) {
    throw 'The canonical muscle-group taxonomy is incomplete or invalid.'
}
$canonicalAssignmentSource = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExerciseCanonicalGroups.psd1') -SkipLimitCheck

$invalidAssignments = @($catalog | Where-Object {
        'muscleGroups' -in @($_.PSObject.Properties.Name) -or
        'primaryCanonicalGroup' -notin @($_.PSObject.Properties.Name) -or
        'secondaryCanonicalGroups' -notin @($_.PSObject.Properties.Name) -or
        [string]::IsNullOrWhiteSpace([string]$_.primaryCanonicalGroup) -or
        [string]$_.primaryCanonicalGroup -notin $canonicalGroupKeys -or
        @($_.secondaryCanonicalGroups | Sort-Object -Unique).Count -ne
            @($_.secondaryCanonicalGroups).Count -or
        [string]$_.primaryCanonicalGroup -in @($_.secondaryCanonicalGroups) -or
        @($_.secondaryCanonicalGroups | Where-Object {
                [string]$_ -notin $canonicalGroupKeys
            }).Count -gt 0
    })
if ($invalidAssignments.Count -gt 0) {
    throw 'Every exercise must have exactly one recognized primary canonical group, unique recognized secondaries, and no legacy muscleGroups field.'
}

$catalogIds = @($catalog.id | ForEach-Object { [int]$_ })
if (@($catalogIds | Where-Object { $_ -lt 1 -or $_ -gt 1000 }).Count -gt 0 -or
    @($catalogIds | Sort-Object -Unique).Count -ne $catalog.Count) {
    throw 'Catalog exercise IDs must be unique stable IDs from 1 through 1000.'
}

$sourceAssignmentIds = @(
    $canonicalAssignmentSource.Keys | ForEach-Object { [int]$_ } | Sort-Object)
$assignmentDrift = @(
    Compare-Object ($catalogIds | Sort-Object) $sourceAssignmentIds)
$assignmentDrift += @(
    $catalog | Where-Object {
        $assignment = $canonicalAssignmentSource[[int]$_.id]
        $assignment -isnot [System.Collections.IDictionary] -or
        [string]$_.primaryCanonicalGroup -ne [string]$assignment.Primary -or
        (@($_.secondaryCanonicalGroups | Sort-Object) -join "`n") -ne
            (@($assignment.Secondary | ForEach-Object { [string]$_ } |
                    Sort-Object) -join "`n")
    })
if ($assignmentDrift.Count -gt 0) {
    throw 'The generated canonical assignments have drifted from ExerciseCanonicalGroups.psd1.'
}

$videoPaths = @($catalog.video)
if (@($videoPaths | Where-Object { $_ -notmatch '^exercise_videos/exercise_\d{4}\.mp4$' }).Count -gt 0 -or
    @($videoPaths | Sort-Object -Unique).Count -ne $catalog.Count -or
    @($catalog | Where-Object {
            [string]$_.video -ne
                ('exercise_videos/exercise_{0:D4}.mp4' -f [int]$_.id)
        }).Count -gt 0) {
    throw 'Catalog video paths are missing, malformed, or duplicated.'
}

$expectedVideoNames = @($videoPaths | ForEach-Object { Split-Path -Leaf $_ })
$actualVideoNames = @(Get-ChildItem -LiteralPath (
        Join-Path $resolvedAssetsRoot 'exercise_videos') -File -Filter '*.mp4' |
        Select-Object -ExpandProperty Name)
$expectedGifNames = @($catalog | ForEach-Object {
        'exercise_{0:D4}.gif' -f [int]$_.id
    })
$actualGifNames = @(Get-ChildItem -LiteralPath (
        Join-Path $resolvedAssetsRoot 'exercise_gifs') -File -Filter '*.gif' |
        Select-Object -ExpandProperty Name)
$expectedHoldNames = @($catalog |
    Where-Object mode -eq 'Hold' |
    ForEach-Object { 'exercise_{0:D4}.png' -f [int]$_.id })
$actualHoldNames = @(Get-ChildItem -LiteralPath (
        Join-Path $resolvedAssetsRoot 'exercise_hold_frames') -File -Filter '*.png' |
        Select-Object -ExpandProperty Name)
$directionExercises = @($catalog | Where-Object directionSequence -ne 'None')
$invalidDirectionExercises = @($directionExercises | Where-Object {
        [string]$_.sideSequence -ne 'Continuous' -or
        [string]$_.mode -ne 'Repetition' -or
        [string]$_.presentation -ne 'Motion' -or
        [string]$_.directionSequence -notin @(
            'ForwardThenBackward',
            'BackwardThenForward',
            'ClockwiseThenCounterclockwise',
            'CounterclockwiseThenClockwise',
            'InwardThenOutward',
            'OutwardThenInward')
    })
$expectedDirectionNames = @($directionExercises | ForEach-Object {
        'exercise_{0:D4}.mp4' -f [int]$_.id
    })
$directionVideoRoot = Join-Path $resolvedAssetsRoot 'exercise_direction_videos'
$actualDirectionNames = if (Test-Path -LiteralPath $directionVideoRoot) {
    @(Get-ChildItem -LiteralPath $directionVideoRoot -File -Filter '*.mp4' |
        Select-Object -ExpandProperty Name)
}
else {
    @()
}
$directionAssetsDiffer = if ($expectedDirectionNames.Count -eq 0 -and
    $actualDirectionNames.Count -eq 0) {
    $false
}
else {
    @(Compare-Object ($expectedDirectionNames | Sort-Object) `
            ($actualDirectionNames | Sort-Object)).Count -gt 0
}

if (@(Compare-Object ($expectedVideoNames | Sort-Object) `
            ($actualVideoNames | Sort-Object)).Count -gt 0 -or
    @(Compare-Object ($expectedGifNames | Sort-Object) `
            ($actualGifNames | Sort-Object)).Count -gt 0 -or
    @(Compare-Object ($expectedHoldNames | Sort-Object) `
            ($actualHoldNames | Sort-Object)).Count -gt 0 -or
    $directionAssetsDiffer -or
    $invalidDirectionExercises.Count -gt 0) {
    throw 'The media directories contain missing or orphaned exercise assets.'
}

foreach ($exercise in $directionExercises) {
    $directionVideoPath = Join-Path $resolvedAssetsRoot (
        'exercise_direction_videos/exercise_{0:D4}.mp4' -f [int]$exercise.id)
    $directionProbeJson = & ffprobe `
        -v error `
        -show_entries 'stream=codec_type,codec_name,width,height,pix_fmt:format=duration' `
        -of json `
        $directionVideoPath
    if ($LASTEXITCODE -ne 0) {
        $failures.Add("$($exercise.id): directional ffprobe failed")
        continue
    }
    $directionProbe = $directionProbeJson | ConvertFrom-Json
    $directionVideoStreams = @(
        $directionProbe.streams | Where-Object codec_type -eq 'video')
    $directionAudioStreams = @(
        $directionProbe.streams | Where-Object codec_type -eq 'audio')
    $directionDuration = [double]::Parse(
        [string]$directionProbe.format.duration,
        [Globalization.CultureInfo]::InvariantCulture)
    if ($directionVideoStreams.Count -ne 1 -or
        $directionVideoStreams[0].codec_name -ne 'h264' -or
        $directionVideoStreams[0].width -ne 256 -or
        $directionVideoStreams[0].height -ne 256 -or
        $directionVideoStreams[0].pix_fmt -ne 'yuv420p' -or
        $directionAudioStreams.Count -ne 0 -or
        $directionDuration -lt 39.8 -or
        $directionDuration -gt 40.2) {
        $failures.Add(
            "$($exercise.id): invalid directional codec, dimensions, audio, or duration")
    }

    $directionKeyframeJson = & ffprobe `
        -v error `
        -select_streams 'v:0' `
        -skip_frame nokey `
        -show_frames `
        -show_entries 'frame=best_effort_timestamp_time' `
        -of json `
        $directionVideoPath
    if ($LASTEXITCODE -ne 0) {
        $failures.Add("$($exercise.id): directional keyframe probe failed")
        continue
    }
    $directionKeyframes = @(
        ($directionKeyframeJson | ConvertFrom-Json).frames |
            ForEach-Object {
                [double]::Parse(
                    [string]$_.best_effort_timestamp_time,
                    [Globalization.CultureInfo]::InvariantCulture)
            })
    if (-not ($directionKeyframes | Where-Object {
                [Math]::Abs($_ - 20.0) -le 0.025
            })) {
        $failures.Add(
            "$($exercise.id): no exact keyframe at the 20-second direction boundary")
    }
}

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) (
    'FluxVideoVerification-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null
$checkedCount = 0
$renderedVideoHashes = [System.Collections.Generic.List[object]]::new(
    $catalog.Count)

try {
    foreach ($exercise in $catalog) {
        $checkedCount++
        $videoPath = Join-Path $resolvedAssetsRoot ([string]$exercise.video)
        if (-not (Test-Path -LiteralPath $videoPath)) {
            $failures.Add("$($exercise.id): missing $videoPath")
            continue
        }

        $renderedVideoHashes.Add([pscustomobject]@{
                Hash = (Get-FileHash `
                        -LiteralPath $videoPath `
                        -Algorithm SHA256).Hash
                Id = [int]$exercise.id
                Name = [string]$exercise.name
            })

        $probeJson = & ffprobe `
            -v error `
            -show_entries 'stream=codec_type,codec_name,width,height,pix_fmt:format=duration' `
            -of json `
            $videoPath
        if ($LASTEXITCODE -ne 0) {
            $failures.Add("$($exercise.id): ffprobe failed")
            continue
        }

        $probe = $probeJson | ConvertFrom-Json
        $videoStreams = @($probe.streams | Where-Object codec_type -eq 'video')
        $audioStreams = @($probe.streams | Where-Object codec_type -eq 'audio')
        $duration = [double]::Parse(
            [string]$probe.format.duration,
            [Globalization.CultureInfo]::InvariantCulture)

        if ($videoStreams.Count -ne 1 -or
            $videoStreams[0].codec_name -ne 'h264' -or
            $videoStreams[0].width -ne 256 -or
            $videoStreams[0].height -ne 256 -or
            $videoStreams[0].pix_fmt -ne 'yuv420p' -or
            $audioStreams.Count -ne 0 -or
            $duration -lt 0.4 -or
            $duration -gt 60) {
            $failures.Add("$($exercise.id): invalid codec, dimensions, audio, or duration")
        }

        $cadenceRange = $reviewedWorkoutCadenceDurationRanges[[int]$exercise.id]
        if ($null -ne $cadenceRange -and
            ($duration -lt [double]$cadenceRange.Minimum -or
                $duration -gt [double]$cadenceRange.Maximum)) {
            $failures.Add(
                "$($exercise.id): reviewed workout-cadence duration regressed")
        }

        if ([string]$exercise.mode -eq 'Hold') {
            $lastFramePath = Join-Path $tempRoot ('exercise_{0:D4}.png' -f [int]$exercise.id)
            & ffmpeg `
                -hide_banner `
                -loglevel error `
                -y `
                -sseof -0.05 `
                -i $videoPath `
                -frames:v 1 `
                $lastFramePath
            if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $lastFramePath)) {
                $failures.Add("$($exercise.id): cannot decode final hold frame")
                continue
            }

            $targetFramePath = Join-Path $resolvedAssetsRoot (
                'exercise_hold_frames/exercise_{0:D4}.png' -f [int]$exercise.id)
            $metricOutput = @(& magick compare `
                    -metric RMSE `
                    $targetFramePath `
                    $lastFramePath `
                    'null:' 2>&1)
            if ($LASTEXITCODE -notin @(0, 1)) {
                $failures.Add("$($exercise.id): hold-frame comparison failed")
                continue
            }

            $metricText = $metricOutput -join ' '
            $normalizedMatch = [regex]::Match($metricText, '\((?<value>[0-9.]+)\)')
            if (-not $normalizedMatch.Success -or
                [double]::Parse(
                    $normalizedMatch.Groups['value'].Value,
                    [Globalization.CultureInfo]::InvariantCulture) -gt 0.08) {
                $failures.Add("$($exercise.id): final frame does not match reviewed hold target")
            }
        }

        if ($checkedCount % 25 -eq 0) {
            Write-Output "Verified $checkedCount / $($catalog.Count) MP4 files"
        }
    }
}
finally {
    $resolvedTempRoot = [IO.Path]::GetFullPath($tempRoot)
    $systemTempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if ($resolvedTempRoot.StartsWith(
            $systemTempRoot,
            [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $resolvedTempRoot -Recurse -Force
    }
}

$duplicateRenderedVideoGroups = @(
    $renderedVideoHashes |
        Group-Object Hash |
        Where-Object Count -gt 1 |
        Sort-Object Count -Descending)
if ($duplicateRenderedVideoGroups.Count -eq 0) {
    Write-Output 'Duplicate rendered-video SHA256 groups: none'
}
else {
    Write-Output (
        'Duplicate rendered-video SHA256 groups: {0}' -f
        $duplicateRenderedVideoGroups.Count)
    foreach ($duplicateGroup in $duplicateRenderedVideoGroups) {
        $movementLabels = @(
            $duplicateGroup.Group |
                Sort-Object Id |
                ForEach-Object { '{0}: {1}' -f $_.Id, $_.Name })
        Write-Output (
            '  {0} ({1} files): {2}' -f
            $duplicateGroup.Name,
            $duplicateGroup.Count,
            ($movementLabels -join '; '))
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    throw "$($failures.Count) exercise MP4 verification checks failed."
}

Write-Output "Verified: $($catalog.Count) / $($catalog.Count) MP4 assets"
Write-Output 'Holds: every final video frame matches its reviewed static target'
