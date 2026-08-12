<#
  Scout AgentOps demo -- one-time setup.

      .\setup.ps1

  Creates a private Python environment in .venv, installs the two
  dependencies (duckdb, PyYAML), and builds the green warehouse.

  Run it once. After that you only ever run .\demo.ps1

  WHY THIS SCRIPT LOOKS PARANOID
  On this machine `python` is a Microsoft Store placeholder that does nothing,
  and the `py` launcher still points at a half-removed install. So rather than
  trust either name, this script PROVES an interpreter works before using it:
  it must import the standard library, and it must have venv and pip. That
  check catches environment problems before the demo sequence is run.
#>

[CmdletBinding()]
param(
    # Delete and rebuild the environment from scratch.
    [switch]$Force,

    # Build the OPTIONAL Microsoft Agent Framework environment (.venv-maf)
    # instead of the demo environment. Kept separate on purpose so the stub
    # planner stays bootable even if the MAF stack breaks.
    [switch]$Maf
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

# A stale PYTHONHOME is another way to break an otherwise fine interpreter.
$env:PYTHONHOME = ''

function Write-Step($text) { Write-Host "`n==> $text" -ForegroundColor Cyan }
function Write-Good($text) { Write-Host "    $text" -ForegroundColor Green }
function Write-Warn($text) { Write-Host "    $text" -ForegroundColor Yellow }

# ----------------------------------------------------------------------
# 1. Find an interpreter that actually works
# ----------------------------------------------------------------------

function Test-Python {
    <#  Returns $true only if this exe can import the stdlib AND has venv+pip.
        A broken install fails here instead of halfway through setup.  #>
    param([string]$Exe)

    if (-not $Exe) { return $false }
    try {
        $probe = & $Exe -c "import venv, ensurepip, sys; print(sys.version_info[:2])" 2>&1
        return ($LASTEXITCODE -eq 0) -and ($probe -match '\(3, (1[0-9]|[9])\)')
    } catch {
        return $false
    }
}

Write-Step "Looking for a working Python 3"

$candidates = @()

# Real installs first -- these are the ones least likely to be a shim.
$candidates += Get-ChildItem "$env:LOCALAPPDATA\Programs\Python" -Directory -ErrorAction SilentlyContinue |
               Sort-Object Name -Descending |
               ForEach-Object { Join-Path $_.FullName 'python.exe' }
$candidates += Get-ChildItem "C:\Program Files\Python*" -Directory -ErrorAction SilentlyContinue |
               Sort-Object Name -Descending |
               ForEach-Object { Join-Path $_.FullName 'python.exe' }

# Then the launcher and whatever is on PATH.
foreach ($tag in @('-3.13', '-3')) {
    $viaLauncher = & { py $tag -c "import sys; print(sys.executable)" 2>$null }
    if ($LASTEXITCODE -eq 0 -and $viaLauncher) { $candidates += $viaLauncher.Trim() }
}
$onPath = (Get-Command python -ErrorAction SilentlyContinue).Source
if ($onPath) { $candidates += $onPath }

$python = $null
foreach ($candidate in ($candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -Unique)) {
    if (Test-Python $candidate) { $python = $candidate; break }
    Write-Warn "skipping (not usable): $candidate"
}

if (-not $python) {
    Write-Host ""
    Write-Host "No working Python 3 found." -ForegroundColor Red
    Write-Host "Install one with:  winget install Python.Python.3.13" -ForegroundColor Red
    Write-Host "then run .\setup.ps1 again." -ForegroundColor Red
    exit 1
}

Write-Good "using $python"

# ----------------------------------------------------------------------
# 2. Build the private environment
# ----------------------------------------------------------------------

$venvName = if ($Maf) { '.venv-maf' } else { '.venv' }
$reqFile  = if ($Maf) { 'requirements-maf.txt' } else { 'requirements.txt' }
$venvPython = Join-Path $PSScriptRoot "$venvName\Scripts\python.exe"

if ($Force -and (Test-Path $venvName)) {
    Write-Step "Removing existing $venvName (-Force)"
    Remove-Item $venvName -Recurse -Force
}

if (-not (Test-Path $venvPython)) {
    Write-Step "Creating $venvName"
    & $python -m venv $venvName
    if ($LASTEXITCODE -ne 0) { throw "venv creation failed" }
} else {
    Write-Step "Reusing existing $venvName"
}

Write-Step "Installing dependencies from $reqFile"
& $venvPython -m pip install --upgrade pip --quiet --disable-pip-version-check
& $venvPython -m pip install -r $reqFile --quiet --disable-pip-version-check
if ($LASTEXITCODE -ne 0) { throw "dependency install failed" }
Write-Good $(if ($Maf) { "Microsoft Agent Framework installed" } else { "duckdb + PyYAML installed" })

# This folder lives in OneDrive. If OneDrive dehydrates the venv to save space,
# imports fail at the worst possible moment. Pin it to stay on disk.
try {
    attrib +P /S /D "$PSScriptRoot\$venvName" 2>$null | Out-Null
    Write-Good "pinned $venvName locally (OneDrive will not evict it)"
} catch {
    Write-Warn "could not pin $venvName for offline use -- not fatal"
}

# ----------------------------------------------------------------------
# 3. Build the green warehouse
# ----------------------------------------------------------------------

Write-Step "Building the green warehouse"
& $venvPython -m scout.build_db
if ($LASTEXITCODE -ne 0) { throw "build_db failed" }

Write-Host ""
Write-Host "Setup complete." -ForegroundColor Green
if ($Maf) {
    Write-Host ""
    Write-Host "The MAF planner needs model credentials. Set these, then test:" -ForegroundColor Yellow
    Write-Host '    $env:AZURE_OPENAI_ENDPOINT   = "https://<resource>.openai.azure.com"' -ForegroundColor Yellow
    Write-Host '    $env:AZURE_OPENAI_API_KEY    = "<key>"' -ForegroundColor Yellow
    Write-Host '    $env:AZURE_OPENAI_DEPLOYMENT = "<deployment name>"' -ForegroundColor Yellow
    Write-Host ""
    Write-Host "    .\demo.ps1 4 -Maf        same beat, real model" -ForegroundColor Green
    Write-Host "    .\demo.ps1 5 -Maf        same twenty cases, real model" -ForegroundColor Green
} else {
    Write-Host "Run the full sequence:  .\demo.ps1 all" -ForegroundColor Green
    Write-Host "Restore the baseline:   .\demo.ps1 reset" -ForegroundColor Green
}
Write-Host ""
