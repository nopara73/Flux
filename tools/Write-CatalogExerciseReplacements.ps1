param(
    [string]$InputPath = (Join-Path $PSScriptRoot '..\docs\catalog-audit\catalog_replacements.csv'),
    [string]$OutputPath = (Join-Path $PSScriptRoot 'CatalogExerciseReplacements.psd1')
)

$ErrorActionPreference = 'Stop'

function ConvertTo-Psd1String {
    param([AllowEmptyString()][string]$Value)
    return "'" + $Value.Replace("'", "''") + "'"
}

function ConvertTo-Psd1Value {
    param($Value)
    if ($Value -is [bool]) {
        if ($Value) {
            return '$true'
        }
        return '$false'
    }
    if ($Value -is [byte] -or $Value -is [int16] -or
        $Value -is [int32] -or $Value -is [int64] -or
        $Value -is [single] -or $Value -is [double] -or
        $Value -is [decimal]) {
        return [Convert]::ToString($Value, [Globalization.CultureInfo]::InvariantCulture)
    }
    return ConvertTo-Psd1String ([string]$Value)
}

$resolvedInputPath = [IO.Path]::GetFullPath($InputPath)
$resolvedOutputPath = [IO.Path]::GetFullPath($OutputPath)
$rows = @(Import-Csv -LiteralPath $resolvedInputPath)
$audit = @(Import-Csv -LiteralPath (
    Join-Path $PSScriptRoot '..\docs\catalog-audit\exercise_usability_audit.csv'))
$retiredRows = @($audit | Where-Object decision -eq 'REMOVE')
$retiredById = @{}
foreach ($row in $retiredRows) {
    $retiredById[[int]$row.id] = $row
}
$existingMedia = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExternalExerciseMedia.psd1') -SkipLimitCheck

$rowIds = @($rows | ForEach-Object { [int]$_.id })
if ($rows.Count -ne $retiredRows.Count -or
    $rowIds.Count -ne @($rowIds | Sort-Object -Unique).Count -or
    @(Compare-Object ($retiredById.Keys | Sort-Object) ($rowIds | Sort-Object)).Count -gt 0 -or
    @($rows.name | Sort-Object -Unique).Count -ne $rows.Count) {
    throw 'The replacement catalog must replace every audited removal exactly once with unique names.'
}

$mediaKeys = @(
    'File',
    'Url',
    'SourcePage',
    'Human',
    'Youtube',
    'Video',
    'StartSeconds',
    'DurationSeconds',
    'FramesPerSecond',
    'DelayCentiseconds',
    'Crop',
    'MaskTop',
    'PingPong',
    'MirrorForAlternation')
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('@{')
$lines.Add('    # Complete identities for exercises that replace retired catalog entries.')
$lines.Add('    # Existing installations delete these slots first, so every replacement starts at score zero.')

foreach ($row in @($rows | Sort-Object { [int]$_.id })) {
    $exerciseId = [int]$row.id
    $secondary = @(
        [string]$row.secondary -split '\|' |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $media = if (-not [string]::IsNullOrWhiteSpace([string]$row.media_source_id)) {
        $sourceId = [int]$row.media_source_id
        if (-not $existingMedia.ContainsKey($sourceId)) {
            throw "Replacement $exerciseId names missing media source $sourceId."
        }
        $existingMedia[$sourceId]
    }
    else {
        if ([string]::IsNullOrWhiteSpace([string]$row.file) -or
            [string]::IsNullOrWhiteSpace([string]$row.url) -or
            [string]::IsNullOrWhiteSpace([string]$row.start_seconds) -or
            [string]::IsNullOrWhiteSpace([string]$row.duration_seconds)) {
            throw "Replacement $exerciseId has incomplete new media metadata."
        }
        @{
            File = [string]$row.file
            Url = [string]$row.url
            SourcePage = [string]$row.url
            Human = $true
            Youtube = $true
            Video = $true
            StartSeconds = [double]::Parse(
                [string]$row.start_seconds,
                [Globalization.CultureInfo]::InvariantCulture)
            DurationSeconds = [double]::Parse(
                [string]$row.duration_seconds,
                [Globalization.CultureInfo]::InvariantCulture)
            FramesPerSecond = if ([string]::IsNullOrWhiteSpace(
                    [string]$row.frames_per_second)) {
                8
            }
            else {
                [int]$row.frames_per_second
            }
            Crop = [string]$row.crop
            PingPong = [string]$row.ping_pong -eq 'true'
            MirrorForAlternation =
                [string]$row.mirror_for_alternation -eq 'true'
        }
    }

    $lines.Add("    $exerciseId = @{")
    $lines.Add('        RetiredName = ' + (
        ConvertTo-Psd1String ([string]$retiredById[$exerciseId].name)))
    $lines.Add('        Name = ' + (ConvertTo-Psd1String ([string]$row.name)) + '')
    $lines.Add('        Practice = ' + (ConvertTo-Psd1String ([string]$row.practice)) + '')
    $lines.Add('        MotionProfile = ' + (
        ConvertTo-Psd1String ([string]$row.motion_profile)))
    $lines.Add('        Primary = ' + (ConvertTo-Psd1String ([string]$row.primary)))
    $secondaryText = if ($secondary.Count -eq 0) {
        '@()'
    }
    else {
        '@(' + (($secondary | ForEach-Object { ConvertTo-Psd1String $_ }) -join ', ') + ')'
    }
    $lines.Add("        Secondary = $secondaryText")
    $lines.Add('        SideSequence = ' + (
        ConvertTo-Psd1String ([string]$row.side_sequence)))
    $lines.Add('        Mode = ' + (ConvertTo-Psd1String ([string]$row.mode)))
    $lines.Add('        Presentation = ' + (
        ConvertTo-Psd1String ([string]$row.presentation)))
    $lines.Add('        HoldFramePercent = ' + [int]$row.hold_frame_percent)
    $lines.Add('        Media = @{')
    foreach ($key in $mediaKeys) {
        if ($media.ContainsKey($key) -and $null -ne $media[$key] -and
            -not ($media[$key] -is [string] -and
                [string]::IsNullOrWhiteSpace([string]$media[$key]))) {
            $lines.Add("            $key = " + (ConvertTo-Psd1Value $media[$key]))
        }
    }
    $lines.Add('        }')
    $lines.Add('    }')
}

$lines.Add('}')
$outputDirectory = Split-Path -Parent $resolvedOutputPath
if (-not (Test-Path -LiteralPath $outputDirectory)) {
    throw "The replacement output directory does not exist: $outputDirectory"
}
$lines | Set-Content -LiteralPath $resolvedOutputPath -Encoding utf8
Write-Host "Replacement definitions: $resolvedOutputPath"
Write-Host "Entries: $($rows.Count)"
