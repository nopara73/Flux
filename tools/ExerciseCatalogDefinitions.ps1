function Get-ExerciseCatalogDefinitions {
    param([string]$ToolsRoot = $PSScriptRoot)

    $regions = @('FEET', 'LEGS', 'HANDS', 'ARMS', 'HEAD',
        'SHOULDERS', 'HIPS', 'CHEST', 'BACK', 'CORE')
    $legacy = Import-PowerShellDataFile -LiteralPath (
        Join-Path $ToolsRoot 'RealExerciseCatalog.psd1') -SkipLimitCheck
    $additional = Import-PowerShellDataFile -LiteralPath (
        Join-Path $ToolsRoot 'AdditionalExerciseCatalog.psd1') -SkipLimitCheck
    $definitions = [System.Collections.Generic.List[object]]::new()
    $seenIds = [System.Collections.Generic.HashSet[int]]::new()
    for ($regionIndex = 0; $regionIndex -lt $regions.Count; $regionIndex++) {
        $region = $regions[$regionIndex]
        $names = @($legacy[$region])
        if ($names.Count -ne 100) {
            throw "$region must define exactly 100 historical source identities."
        }
        for ($index = 0; $index -lt 100; $index++) {
            $null = $seenIds.Add($regionIndex * 100 + $index + 1)
            $definitions.Add([pscustomobject]@{
                Id = $regionIndex * 100 + $index + 1
                Name = [string]$names[$index]
                Region = $region
                Additional = $false
            })
        }
    }
    foreach ($entry in $additional.GetEnumerator() | Sort-Object { [int]$_.Key }) {
        $id = 0
        $value = $entry.Value
        if ([string]$entry.Key -notmatch '^[1-9][0-9]*$' -or
            -not [int]::TryParse([string]$entry.Key, [ref]$id) -or
            $id -le 1000 -or
            -not $seenIds.Add($id) -or
            $value -isnot [System.Collections.IDictionary] -or
            [string]$value.Region -notin $regions -or
            [string]::IsNullOrWhiteSpace([string]$value.Name) -or
            [string]::IsNullOrWhiteSpace([string]$value.Practice) -or
            [string]::IsNullOrWhiteSpace([string]$value.MotionProfile)) {
            throw "Invalid additional exercise definition '$($entry.Key)'."
        }
        $definitions.Add([pscustomobject]@{
            Id = $id
            Name = [string]$value.Name
            Region = [string]$value.Region
            Practice = [string]$value.Practice
            MotionProfile = [string]$value.MotionProfile
            Additional = $true
        })
    }
    return $definitions.ToArray()
}
