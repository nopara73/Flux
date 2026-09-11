# Preserved, unaltered decoded footage is an explicit source, never an implicit
# fallback to an already generated destination. This makes corrections repeatable
# when the publisher no longer serves an old source download.
function Resolve-ReviewedSourceMedia {
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary]$Media,
        [Parameter(Mandatory)][string]$ToolsRoot
    )
    $name = [string]$Media.LocalSourceFile
    $expectedHash = [string]$Media.LocalSourceSha256
    if ($name -cnotmatch '^[A-Za-z0-9][A-Za-z0-9._-]*\.(gif|mp4)$' -or
        $expectedHash -cnotmatch '^[a-f0-9]{64}$') {
        throw 'A reviewed local source requires a plain media filename and its SHA-256.'
    }
    $path = Join-Path (Join-Path $ToolsRoot 'ReviewedSourceMedia') $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Reviewed source media is missing: $name"
    }
    $actualHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -cne $expectedHash) {
        throw "Reviewed source media changed: $name"
    }
    return $path
}
