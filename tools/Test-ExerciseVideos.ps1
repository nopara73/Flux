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

$regions = @(
    'FEET', 'LEGS', 'HANDS', 'ARMS', 'HEAD',
    'SHOULDERS', 'HIPS', 'CHEST', 'BACK', 'CORE')
$invalidRegionCounts = @($regions | Where-Object {
        @($catalog | Where-Object dominantRegion -eq $_).Count -lt 3
    })
if ($invalidRegionCounts.Count -gt 0) {
    throw "Every region must contain at least three exercises: $($invalidRegionCounts -join ', ')."
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

try {
    foreach ($exercise in $catalog) {
        $checkedCount++
        $videoPath = Join-Path $resolvedAssetsRoot ([string]$exercise.video)
        if (-not (Test-Path -LiteralPath $videoPath)) {
            $failures.Add("$($exercise.id): missing $videoPath")
            continue
        }

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

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    throw "$($failures.Count) exercise MP4 verification checks failed."
}

Write-Output "Verified: $($catalog.Count) / $($catalog.Count) MP4 assets"
Write-Output 'Holds: every final video frame matches its reviewed static target'
