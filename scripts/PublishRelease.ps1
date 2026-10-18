[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:GITHUB_REF -ne 'refs/heads/main' -or
    $env:GITHUB_EVENT_NAME -notin @('push', 'workflow_dispatch') -or
    $env:GITHUB_SHA -notmatch '^[a-f0-9]{40}$' -or [string]::IsNullOrWhiteSpace($env:GH_TOKEN)) {
    throw 'Release publishing requires the trusted main-branch GitHub Actions job.'
}

function Invoke-GitHub([string[]]$Arguments) {
    $result = @(& gh @Arguments)
    if ($LASTEXITCODE -ne 0) { throw 'GitHub release command failed; inspect the release state before retrying.' }
    return $result
}

function Get-VersionRelease([string]$Repository, [string]$Tag) {
    # The tag endpoint does not return unpublished drafts.
    $filter = '.[] | select(.tag_name == "' + $Tag + '")'
    $json = (Invoke-GitHub @('api', "repos/$Repository/releases?per_page=100", '--paginate', '--jq', $filter)) -join "`n"
    if (-not [string]::IsNullOrWhiteSpace($json)) { return ($json | ConvertFrom-Json) }
}

Push-Location -LiteralPath $projectRoot
try {
    $commit = & git rev-parse HEAD
    if ($LASTEXITCODE -ne 0 -or $commit -cne $env:GITHUB_SHA) { throw 'Checked-out source differs from the tested commit.' }
    [xml]$project = [IO.File]::ReadAllText((Join-Path $projectRoot 'src/MyKa.Spoofer/MyKa.Spoofer.csproj'))
    $version = [string]$project.Project.PropertyGroup.Version
    if ($version -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') {
        throw 'A stable release requires a major.minor.patch project version.'
    }
    $tag = 'v' + $version
    $repository = $env:GITHUB_REPOSITORY
    $archiveName = 'myka-' + $version + '-win-x64.zip'
    $release = Get-VersionRelease $repository $tag
    if ($null -ne $release -and -not $release.draft) {
        Write-Output ('Release already published: ' + $release.html_url)
        return
    }
    if ($null -ne $release -and ($release.author.login -cne 'github-actions[bot]' -or
        @($release.assets | Where-Object { $_.name -cnotin @($archiveName, ($archiveName + '.sha256')) }).Count -ne 0)) {
        throw 'The existing draft has a different author or unexpected assets; inspect it before retrying.'
    }
    $tagRefs = @(& git ls-remote --tags origin "refs/tags/$tag" "refs/tags/$tag^{}")
    if ($LASTEXITCODE -ne 0) { throw 'Could not check existing release tags.' }
    if ($tagRefs.Count -ne 0) { throw 'The version tag already exists without a published release; review it before proceeding.' }

    $artifactDirectory = Join-Path $projectRoot '.artifacts/release'
    $archive = Join-Path $artifactDirectory $archiveName
    $checksum = $archive + '.sha256'
    foreach ($path in @($archive, $checksum)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw 'The verified Windows package or checksum is missing.' }
    }
    $expected = [IO.File]::ReadAllText($checksum).Trim()
    $actualHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
    if ($expected -cne ($actualHash + '  ' + $archiveName)) { throw 'The Windows package does not match its build checksum.' }

    $changelog = [IO.File]::ReadAllText((Join-Path $projectRoot 'CHANGELOG.md'))
    $section = [regex]::Match($changelog, '(?ms)^## ' + [regex]::Escape($version) + '(?=[ \t\r\n]|$)[^\r\n]*\r?\n(.*?)(?=^## |\z)')
    if (-not $section.Success -or [string]::IsNullOrWhiteSpace($section.Groups[1].Value)) {
        throw 'The project version needs a nonempty changelog entry.'
    }
    $notes = "Windows x64 package. Requires the .NET 10 Desktop Runtime; account sign-in also requires WebView2 Runtime.`n`n" +
        $section.Groups[1].Value.Trim() + "`n`n[Setup and supported scope](https://github.com/$repository/blob/$tag/README.md)."
    $notesPath = Join-Path $artifactDirectory 'release-notes.md'
    [IO.File]::WriteAllText($notesPath, $notes, (New-Object Text.UTF8Encoding($false)))

    if ($null -ne $release) {
        Invoke-GitHub @('release', 'edit', $tag, '--repo', $repository, '--target', $commit,
            '--title', ("MyKa's Roblox Spoofer " + $version), '--notes-file', $notesPath) | Out-Null
        Invoke-GitHub @('release', 'upload', $tag, $archive, $checksum, '--repo', $repository, '--clobber') | Out-Null
    }
    else {
        Invoke-GitHub @('release', 'create', $tag, $archive, $checksum, '--repo', $repository,
            '--target', $commit, '--title', ("MyKa's Roblox Spoofer " + $version), '--notes-file', $notesPath, '--draft') | Out-Null
        $release = Get-VersionRelease $repository $tag
    }
    if ($null -eq $release -or $release.id -le 0) { throw 'The release draft could not be resolved.' }
    $draft = ((Invoke-GitHub @('api', "repos/$repository/releases/$($release.id)")) -join "`n") | ConvertFrom-Json
    if (-not $draft.draft -or $draft.tag_name -cne $tag -or $draft.target_commitish -cne $commit -or $draft.assets.Count -ne 2) {
        throw 'The release draft does not match the tested commit and expected assets.'
    }
    foreach ($path in @($archive, $checksum)) {
        $file = Get-Item -LiteralPath $path
        $asset = @($draft.assets | Where-Object { $_.name -ceq $file.Name })
        $digest = 'sha256:' + (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($asset.Count -ne 1 -or $asset[0].state -ne 'uploaded' -or $asset[0].size -ne $file.Length -or
            $asset[0].digest -cne $digest) { throw 'An uploaded release asset failed size or SHA-256 verification.' }
    }
    Invoke-GitHub @('release', 'edit', $tag, '--repo', $repository, '--draft=false', '--latest') | Out-Null
    Write-Output ('Published release: https://github.com/' + $repository + '/releases/tag/' + $tag)
}
finally {
    Pop-Location
}
