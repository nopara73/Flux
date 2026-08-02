param(
    [string]$AssetsRoot = (Join-Path $PSScriptRoot '..\Flux\Assets')
)

$ErrorActionPreference = 'Stop'

$resolvedAssetsRoot = [IO.Path]::GetFullPath($AssetsRoot)
$catalogPath = Join-Path $resolvedAssetsRoot 'exercises.json'
$catalog = @(Get-Content -LiteralPath $catalogPath -Raw | ConvertFrom-Json)
$failures = [System.Collections.Generic.List[string]]::new()

if ($catalog.Count -lt 30) {
    throw "Expected at least 30 catalog records, found $($catalog.Count)."
}

$muscleGroups = @(
    'Glutes', 'Core', 'Quadriceps', 'Hamstrings', 'UpperBack',
    'Shoulders', 'Chest', 'LowerBack', 'Calves', 'HipFlexors',
    'Adductors', 'Abductors', 'MidBack', 'Trapezius', 'Forearms',
    'Triceps', 'Biceps', 'RotatorCuff', 'Neck', 'Shins')
$invalidMuscleGroupCounts = @($muscleGroups | Where-Object {
        $muscleGroup = $_
        @($catalog | Where-Object {
                $muscleGroup -in @($_.muscleGroups)
            }).Count -lt 10
    })
if ($invalidMuscleGroupCounts.Count -gt 0) {
    throw "Every muscle group must contain at least ten exercises: $($invalidMuscleGroupCounts -join ', ')."
}

$invalidAssignments = @($catalog | Where-Object {
        @($_.muscleGroups).Count -lt 1 -or
        @($_.muscleGroups | Sort-Object -Unique).Count -ne @($_.muscleGroups).Count -or
        @($_.muscleGroups | Where-Object { $_ -notin $muscleGroups }).Count -gt 0
    })
if ($invalidAssignments.Count -gt 0) {
    throw 'Every exercise must have one or more unique, recognized muscle groups.'
}

$videoPaths = @($catalog.video)
if (@($videoPaths | Where-Object { $_ -notmatch '^exercise_videos/exercise_\d{4}\.mp4$' }).Count -gt 0 -or
    @($videoPaths | Sort-Object -Unique).Count -ne $catalog.Count) {
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

if (@(Compare-Object ($expectedVideoNames | Sort-Object) `
            ($actualVideoNames | Sort-Object)).Count -gt 0 -or
    @(Compare-Object ($expectedGifNames | Sort-Object) `
            ($actualGifNames | Sort-Object)).Count -gt 0 -or
    @(Compare-Object ($expectedHoldNames | Sort-Object) `
            ($actualHoldNames | Sort-Object)).Count -gt 0) {
    throw 'The media directories contain missing or orphaned exercise assets.'
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
