[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$manifestPath = Join-Path $root 'tests/contracts/codegen/schema-manifest.json'
$schemaRoot = Join-Path $root 'tests/contracts/schemas/v1'
$manifest = Get-Content -Raw $manifestPath | ConvertFrom-Json

foreach ($entry in $manifest.files.psobject.Properties | Sort-Object Name) {
    $path = Join-Path $schemaRoot $entry.Name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing schema snapshot: $($entry.Name)"
    }

    $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $entry.Value) {
        throw "Schema snapshot hash mismatch: $($entry.Name)"
    }
}

$actualNames = @(Get-ChildItem -LiteralPath $schemaRoot -File | Select-Object -ExpandProperty Name | Sort-Object)
$expectedNames = @($manifest.files.psobject.Properties.Name | Sort-Object)
if ((Compare-Object $expectedNames $actualNames).Count -gt 0) {
    throw 'Schema snapshot contains an unexpected file.'
}

Write-Output "Verified $($expectedNames.Count) pinned v1 schema files from $($manifest.commit)."
