param(
    [string]$AcumaticaSitePath = "C:\Program Files\Acumatica ERP\AcumaticaERP"
)

$ErrorActionPreference = "Stop"
$bin = Join-Path $AcumaticaSitePath "Bin"
$compiler = Join-Path $bin "roslyn\csc.exe"
$sourceDirectory = Join-Path (Split-Path $PSScriptRoot -Parent) "src"
$sources = @(Get-ChildItem -LiteralPath $sourceDirectory -Filter "*.cs" | Select-Object -ExpandProperty FullName)
$output = Join-Path $env:TEMP "ServaSoScannerLabels.compile.dll"
$references = @(
    "PX.Common.dll",
    "PX.Common.Std.dll",
    "PX.Data.dll",
    "PX.Data.BQL.Fluent.dll",
    "PX.Objects.dll",
    "PX.BarcodeProcessing.dll"
) | ForEach-Object { Join-Path $bin $_ }

foreach ($path in @($compiler) + $sources + $references) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required compile input was not found: $path"
    }
}

$version = (Get-Item -LiteralPath (Join-Path $bin "PX.Objects.dll")).VersionInfo.FileVersion
if (-not $version.StartsWith("26.1")) {
    throw "Expected an Acumatica 2026 R1 runtime, but PX.Objects.dll is version $version."
}

$arguments = @(
    "/nologo",
    "/target:library",
    "/langversion:latest",
    "/out:$output"
)
$arguments += $references | ForEach-Object { "/reference:$_" }
$arguments += $sources

try {
    & $compiler @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Serva customization compile failed with exit code $LASTEXITCODE."
    }
    Write-Output "Acumatica 2026 R1 compile passed against build $version"
}
finally {
    Remove-Item -LiteralPath $output -Force -ErrorAction SilentlyContinue
}
