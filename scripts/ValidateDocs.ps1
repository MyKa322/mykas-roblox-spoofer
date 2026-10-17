[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$docFiles = @(Get-ChildItem -LiteralPath $projectRoot -File -Filter '*.md')
foreach ($directory in @('docs', '.github', 'tools')) {
    $path = Join-Path $projectRoot $directory
    if (Test-Path -LiteralPath $path) {
        $docFiles += @(Get-ChildItem -LiteralPath $path -File -Recurse -Filter '*.md' |
            Where-Object { $_.FullName -notmatch '[/\\](bin|obj)[/\\]' })
    }
}
$problems = [Collections.Generic.List[string]]::new()
$linkCount = 0
foreach ($file in $docFiles) {
    $body = [IO.File]::ReadAllText($file.FullName)
    $fence = $null
    foreach ($line in ($body -split '\r?\n')) {
        if ($line -match '^\s*(`{3,}|~{3,})') {
            $marker = $Matches[1]
            if ($null -eq $fence) { $fence = $marker.Substring(0, 1) }
            elseif ($marker.StartsWith($fence)) { $fence = $null }
        }
    }
    if ($null -ne $fence) { $problems.Add($file.Name + ': unclosed code fence') }
    $targets = @([regex]::Matches($body, '\[[^\]]*\]\(([^)]+)\)') | ForEach-Object { $_.Groups[1].Value })
    $targets += @([regex]::Matches($body, '(?:src|href)\s*=\s*["'']([^"'']+)["'']') | ForEach-Object { $_.Groups[1].Value })
    foreach ($rawTarget in $targets) {
        $target = $rawTarget.Trim('<', '>')
        if ($target -match '^[a-zA-Z][a-zA-Z0-9+.-]*:') { continue }
        $linkCount++
        $parts = $target -split '#', 2
        $destination = if ($parts[0].Length -eq 0) { $file.FullName } else {
            [IO.Path]::GetFullPath((Join-Path $file.DirectoryName ([uri]::UnescapeDataString($parts[0]))))
        }
        if (-not (Test-Path -LiteralPath $destination)) { $problems.Add($file.Name + ': missing ' + $target); continue }
        if ($parts.Length -gt 1 -and $parts[1].Length -gt 0 -and $destination.EndsWith('.md')) {
            $anchors = foreach ($heading in [regex]::Matches([IO.File]::ReadAllText($destination), '(?m)^#{1,6}\s+(.+?)\s*$')) {
                $slug = [regex]::Replace($heading.Groups[1].Value.ToLowerInvariant(), '[^\p{L}\p{Nd}\s_-]', '')
                [regex]::Replace($slug.Trim(), '\s', '-')
            }
            if ($anchors -notcontains $parts[1]) { $problems.Add($file.Name + ': missing anchor ' + $target) }
        }
    }
}
$readme = [IO.File]::ReadAllText((Join-Path $projectRoot 'README.md'))
if ($readme -match '[\uD83C-\uD83E][\uDC00-\uDFFF]|[\u2600-\u27BF]') {
    $problems.Add('README.md: emoji or pictograph found')
}
if ($problems.Count -gt 0) {
    $problems | Write-Output
    throw 'Documentation validation failed.'
}
Write-Output ("Validated {0} Markdown files and {1} relative links/anchors; README has no emojis." -f $docFiles.Count, $linkCount)
