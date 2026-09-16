[CmdletBinding()]
param([switch]$Verify)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$configPath = Join-Path $root 'tests/contracts/codegen/generator-config.json'
$config = Get-Content -Raw $configPath | ConvertFrom-Json
if ($config.generator -ne 'NJsonSchema.CodeGeneration.CSharp' -or $config.version -ne '11.3.2') {
    throw 'Generator configuration is not pinned to the evaluated version.'
}
& (Join-Path $PSScriptRoot 'verify-contract-schema-snapshot.ps1')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$schemaRoot = Join-Path $root 'tests/contracts/schemas/v1'
$output = Join-Path $root 'tests/AuctionOperationsPortal.Tests/ContractCodegen/Generated/IntegrationEventContracts.g.cs'
$generatorProject = Join-Path $root 'tools/ContractDtoGenerator/ContractDtoGenerator.csproj'
$schemas = Get-ChildItem -LiteralPath $schemaRoot -Filter '*.schema.json' -File | Sort-Object Name
foreach ($schema in $schemas) {
    $document = Get-Content -Raw $schema.FullName | ConvertFrom-Json
    if ([string]::IsNullOrWhiteSpace($document.'$id')) { throw "Schema has no stable id: $($schema.Name)" }
}

if ($schemas.Count -ne 6) { throw 'Generator schema inventory is incomplete.' }
if (-not (Test-Path -LiteralPath $generatorProject -PathType Leaf)) { throw "Generator project is missing: $generatorProject" }

if ($Verify) {
    $temporary = Join-Path ([System.IO.Path]::GetTempPath()) 'dbap-operations-generated-contracts.g.cs'
    & dotnet run --project $generatorProject --no-restore -- --output $temporary
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $expectedBytes = [System.IO.File]::ReadAllBytes($temporary)
    $actualBytes = [System.IO.File]::ReadAllBytes($output)
    if (-not [System.Linq.Enumerable]::SequenceEqual($expectedBytes, $actualBytes)) {
        Remove-Item -LiteralPath $temporary -Force -ErrorAction SilentlyContinue
        throw 'Generated DTO output differs from deterministic regeneration.'
    }
    Remove-Item -LiteralPath $temporary -Force
    Write-Output 'Generated contract DTO snapshot is deterministic and verified.'
    exit 0
}

& dotnet run --project $generatorProject --no-restore -- --output $output
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Output 'Generated contract DTO snapshot from the pinned local schema input.'
