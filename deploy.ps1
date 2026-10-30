<#
.SYNOPSIS
    Build ClarionMarkdownEditor and deploy it into a local Clarion install for testing.

.DESCRIPTION
    Copies the same file set the release zip ships (plus the .pdb) into
    <ClarionRoot>\accessory\addins\ClarionMarkdownEditor, then verifies every file by hash
    so a deploy either fully matches the build or fails loudly.

    Refuses to run while Clarion is open (the DLL is locked and the IDE would keep the old
    copy loaded), and warns if another addin folder declares the same Identity, which stops
    the IDE starting ("Identity name used by multiple addins").

.PARAMETER ClarionRoot
    Clarion install folder, e.g. C:\Clarion\Clarion12. If omitted, uses the CLARION_ROOT
    environment variable, then the parent of ClarionBin in the gitignored
    ClarionMarkdownEditor\Directory.Build.props.user (see the .example beside it), which is
    the same setting the build uses.

.PARAMETER SkipBuild
    Deploy the existing Release output without rebuilding.

.EXAMPLE
    .\deploy.ps1
    Deploys to the Clarion set in Directory.Build.props.user.

.EXAMPLE
    .\deploy.ps1 -ClarionRoot C:\Clarion\Clarion11.1
    Deploys to a specific Clarion, overriding the configured one.
#>
param(
    [string]$ClarionRoot = $env:CLARION_ROOT,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

$userProps = Join-Path $PSScriptRoot 'ClarionMarkdownEditor\Directory.Build.props.user'
if (-not $ClarionRoot -and (Test-Path $userProps)) {
    [xml]$props = Get-Content $userProps
    $configuredBin = @($props.Project.PropertyGroup | ForEach-Object { $_.ClarionBin } | Where-Object { $_ })[0]
    if ($configuredBin) { $ClarionRoot = Split-Path ([Environment]::ExpandEnvironmentVariables($configuredBin)) -Parent }
}
if (-not $ClarionRoot) {
    throw "No Clarion install configured. Copy ClarionMarkdownEditor\Directory.Build.props.user.example to " +
          "Directory.Build.props.user and set ClarionBin, set CLARION_ROOT, or pass -ClarionRoot."
}
$ClarionRoot = (Resolve-Path $ClarionRoot).Path
$clarionBin  = Join-Path $ClarionRoot 'bin'
if (-not (Test-Path (Join-Path $clarionBin 'ICSharpCode.Core.dll'))) {
    throw "$ClarionRoot does not look like a Clarion install (no bin\ICSharpCode.Core.dll)."
}

$running = Get-Process -Name 'Clarion*' -ErrorAction SilentlyContinue
if ($running) {
    throw "Clarion is running (PID $($running.Id -join ', ')). Close it and run the deploy again."
}

$repo   = $PSScriptRoot
$output = Join-Path $repo 'ClarionMarkdownEditor\bin\Release\net48'
$dest   = Join-Path $ClarionRoot 'accessory\addins\ClarionMarkdownEditor'

if (-not $SkipBuild) {
    & dotnet build (Join-Path $repo 'ClarionMarkdownEditor.sln') -c Release "-p:ClarionBin=$clarionBin" -nologo -v:q
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}

$files = @(
    'ClarionMarkdownEditor.addin',
    'ClarionMarkdownEditor.dll',
    'ClarionMarkdownEditor.pdb',
    'Microsoft.Web.WebView2.Core.dll',
    'Microsoft.Web.WebView2.WinForms.dll',
    'Microsoft.Web.WebView2.Wpf.dll',
    'WebView2Loader.dll'
) + (Get-ChildItem (Join-Path $output 'Resources') -File | ForEach-Object { "Resources\$($_.Name)" })

New-Item -ItemType Directory -Force (Join-Path $dest 'Resources') | Out-Null
foreach ($f in $files) {
    Copy-Item (Join-Path $output $f) (Join-Path $dest $f) -Force
}

$mismatched = $files | Where-Object {
    (Get-FileHash (Join-Path $output $_)).Hash -ne (Get-FileHash (Join-Path $dest $_)).Hash
}
if ($mismatched) { throw "Deployed files do not match the build: $($mismatched -join ', ')" }

[xml]$manifest = Get-Content (Join-Path $dest 'ClarionMarkdownEditor.addin')
$version = $manifest.AddIn.Manifest.Identity.version
Write-Host "Deployed ClarionMarkdownEditor $version to $dest ($($files.Count) files verified)" -ForegroundColor Green

# Any other folder declaring the same Identity stops the IDE starting
$addinsRoot = Join-Path $ClarionRoot 'accessory\addins'
Get-ChildItem $addinsRoot -Directory | Where-Object { $_.FullName -ne $dest } | ForEach-Object {
    Get-ChildItem $_.FullName -Filter *.addin -File -ErrorAction SilentlyContinue | Where-Object {
        (Get-Content $_.FullName -Raw) -match '<Identity\s+name="ClarionMarkdownEditor"'
    } | ForEach-Object {
        Write-Warning "Duplicate copy at $($_.DirectoryName): Clarion will refuse to start until it is removed."
    }
}
