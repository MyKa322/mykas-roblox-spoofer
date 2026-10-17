[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $projectRoot '.artifacts/publish'
$executable = Join-Path $publish 'myka.exe'
$testedAssembly = Join-Path $projectRoot 'tests/MyKa.Spoofer.Tests/bin/Release/net10.0-windows/myka.dll'
$publishedAssembly = Join-Path $publish 'myka.dll'
foreach ($path in @($executable, $testedAssembly, $publishedAssembly)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw ('Build/test output is missing: ' + $path) }
}
if ((Get-FileHash -LiteralPath $testedAssembly).Hash -ne (Get-FileHash -LiteralPath $publishedAssembly).Hash) {
    throw 'Published code differs from the assembly used by the tests.'
}
[xml]$project = [IO.File]::ReadAllText((Join-Path $projectRoot 'src/MyKa.Spoofer/MyKa.Spoofer.csproj'))
$version = [string]$project.Project.PropertyGroup.Version
$helpText = @(& $executable --help)
if ($LASTEXITCODE -ne 0 -or ($helpText -join "`n") -notmatch [regex]::Escape("MyKa's Roblox Spoofer " + $version)) {
    throw 'Published help/version check failed.'
}
$logoText = @(& $executable --plain logo)
if ($LASTEXITCODE -ne 0) { throw 'Published logo command failed.' }
$expectedLogo = [IO.File]::ReadAllText((Join-Path $projectRoot 'assets/logo.txt')).TrimEnd("`r", "`n")
if (($logoText -join "`n").TrimEnd("`r", "`n") -cne $expectedLogo.Replace("`r`n", "`n")) {
    throw 'Published plain logo differs from the checked-in asset.'
}
& (Join-Path $PSScriptRoot 'ValidateDocs.ps1')
Write-Output ("Release {0} verified: tested assembly, help/version and logo match." -f $version)
