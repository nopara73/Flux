param(
    [string]$AssetsRoot = (Join-Path $PSScriptRoot '..\Flux\Assets')
)

$ErrorActionPreference = 'Stop'

$resolvedAssetsRoot = [IO.Path]::GetFullPath($AssetsRoot)
$catalogPath = Join-Path $resolvedAssetsRoot 'exercises.json'
$catalog = @(Get-Content -LiteralPath $catalogPath -Raw | ConvertFrom-Json)
$failures = [System.Collections.Generic.List[string]]::new()

if ($catalog.Count -ne 1000) {
    throw "Expected 1,000 catalog records, found $($catalog.Count)."
}

$videoPaths = @($catalog.video)
if (@($videoPaths | Where-Object { $_ -notmatch '^exercise_videos/exercise_\d{4}\.mp4$' }).Count -gt 0 -or
    @($videoPaths | Sort-Object -Unique).Count -ne 1000) {
    throw 'Catalog video paths are missing, malformed, or duplicated.'
}

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) (
    'FluxVideoVerification-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null

try {
    foreach ($exercise in $catalog) {
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

        if ([int]$exercise.id % 100 -eq 0) {
            Write-Output "Verified $($exercise.id) / 1000 MP4 files"
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

Write-Output 'Verified: 1000 / 1000 MP4 assets'
Write-Output 'Holds: every final video frame matches its reviewed static target'
