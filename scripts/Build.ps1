[CmdletBinding()]
param([switch]$NoRestore)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$previousResolver = $env:MSBuildEnableWorkloadResolver
$env:MSBuildEnableWorkloadResolver = 'false'
Push-Location -LiteralPath $projectRoot
try {
    # This is the default platform for net10.0-windows, made explicit for SDK consistency.
    $platform = '-p:TargetPlatformMoniker=Windows%2CVersion=7.0'
    if (-not $NoRestore) {
        & dotnet restore tests/MyKa.Spoofer.Tests/MyKa.Spoofer.Tests.csproj --configfile NuGet.Config --nologo $platform
        if ($LASTEXITCODE -ne 0) { throw 'Package restore failed.' }
    }
    & dotnet test tests/MyKa.Spoofer.Tests/MyKa.Spoofer.Tests.csproj -c Release --no-restore --nologo $platform
    if ($LASTEXITCODE -ne 0) { throw 'Release tests failed.' }
    $artifacts = [IO.Path]::GetFullPath((Join-Path $projectRoot '.artifacts'))
    $publish = [IO.Path]::GetFullPath((Join-Path $artifacts 'publish'))
    if (Test-Path -LiteralPath $publish) {
        foreach ($path in @($artifacts, $publish)) {
            if ((Get-Item -LiteralPath $path -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw 'Refusing to move linked build output.'
            }
        }
        $previousPublish = [IO.Path]::GetFullPath((Join-Path $artifacts ('publish.previous-' + [Guid]::NewGuid().ToString('N'))))
        $prefix = $artifacts.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        if (-not $publish.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -or
            -not $previousPublish.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Build output must stay inside this workspace artifact directory.'
        }
        # Preserve unrelated files while ensuring only fresh output enters this release.
        [IO.Directory]::Move($publish, $previousPublish)
    }
    & dotnet publish src/MyKa.Spoofer/MyKa.Spoofer.csproj -c Release --no-restore --nologo $platform -p:PublishDir=../../.artifacts/publish/
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    foreach ($notice in @('LICENSE', 'THIRD-PARTY-NOTICES.md')) {
        $path = Join-Path $projectRoot $notice
        if (Test-Path -LiteralPath $path) { Copy-Item -LiteralPath $path -Destination $publish -Force }
    }
    if (Test-Path -LiteralPath 'licenses') { Copy-Item -LiteralPath 'licenses' -Destination $publish -Recurse -Force }
    & (Join-Path $PSScriptRoot 'VerifyRelease.ps1')
    [xml]$project = [IO.File]::ReadAllText((Join-Path $projectRoot 'src/MyKa.Spoofer/MyKa.Spoofer.csproj'))
    $version = [string]$project.Project.PropertyGroup.Version
    $archive = Join-Path $projectRoot ('.artifacts/myka-' + $version + '-win-x64.zip')
    Compress-Archive -LiteralPath @(Get-ChildItem -LiteralPath $publish | ForEach-Object { $_.FullName }) -DestinationPath $archive -Force
    (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash + '  ' + [IO.Path]::GetFileName($archive) |
        Set-Content -LiteralPath ($archive + '.sha256') -Encoding ASCII
    Write-Output ('Windows package: ' + $archive)
}
finally {
    Pop-Location
    $env:MSBuildEnableWorkloadResolver = $previousResolver
}
