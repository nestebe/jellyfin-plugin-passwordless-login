#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds the plugin, publishes a GitHub release and adds it to manifest.json.

.DESCRIPTION
    Reads version, targetAbi and changelog from build.yaml, runs the tests, zips the plugin DLL,
    creates the GitHub release v<version> with the zip attached (requires the GitHub CLI, logged in)
    and prepends the version to manifest.json with the zip's MD5 checksum, as Jellyfin expects.
    Commit and push manifest.json afterwards so servers using the repository see the new version.

.PARAMETER DryRun
    Build, test and zip only: no release is created and manifest.json is not changed.
#>
param(
    [switch] $DryRun
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$repository = 'nestebe/jellyfin-plugin-passwordless-login'
$project = 'Jellyfin.Plugin.PasswordlessLogin'

# build.yaml is the single source of truth for the release metadata.
$buildYaml = Get-Content -Raw build.yaml
function Get-YamlValue([string] $key) {
    if ($buildYaml -notmatch "(?m)^${key}:\s*`"?([^`"\r\n]+)`"?") {
        throw "Missing '$key' in build.yaml"
    }

    $Matches[1].Trim()
}

$version = Get-YamlValue 'version'
$targetAbi = Get-YamlValue 'targetAbi'
$changelog = if ($buildYaml -match '(?ms)^changelog:\s*>\s*\r?\n(.*?)(?=^\S|\z)') {
    (($Matches[1] -split '\r?\n') | ForEach-Object { $_.Trim() } | Where-Object { $_ }) -join ' '
} else {
    ''
}

$tag = "v$version"
Write-Host "Releasing $project $version (targetAbi $targetAbi)"

dotnet test -c Release
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }

$output = Join-Path 'artifacts' 'build'
dotnet build $project -c Release -o $output "-p:Version=$version" "-p:AssemblyVersion=$version" "-p:FileVersion=$version"
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

$zip = Join-Path 'artifacts' "passwordless-login_$version.zip"
Remove-Item $zip -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $output "$project.dll") -DestinationPath $zip
$checksum = (Get-FileHash $zip -Algorithm MD5).Hash.ToLowerInvariant()
Write-Host "Created $zip (md5 $checksum)"

if ($DryRun) {
    Write-Host 'Dry run: no release created, manifest.json unchanged.'
    return
}

gh release create $tag $zip --repo $repository --title "Passwordless Login $version" --notes $changelog
if ($LASTEXITCODE -ne 0) { throw 'gh release create failed' }

$manifest = Get-Content -Raw manifest.json | ConvertFrom-Json -AsHashtable -NoEnumerate
$plugin = $manifest[0]
$entry = [ordered]@{
    version   = $version
    changelog = $changelog
    targetAbi = $targetAbi
    sourceUrl = "https://github.com/$repository/releases/download/$tag/passwordless-login_$version.zip"
    checksum  = $checksum
    timestamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
}
$plugin['versions'] = @($entry) + @($plugin['versions'] | Where-Object { $_['version'] -ne $version })
ConvertTo-Json -InputObject $manifest -Depth 10 | Set-Content -Encoding utf8NoBOM manifest.json

Write-Host "Release $tag published. Commit and push manifest.json to make it available to servers."
