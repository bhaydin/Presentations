<#
  Scout AgentOps demo driver -- Data-Driven Wisconsin 2026.

      .\demo.ps1 reset     back to green. run this before every rehearsal.
      .\demo.ps1 1         green baseline: agent answers correctly
      .\demo.ps1 2         green evals: 20/20, gate clear
      .\demo.ps1 3         THE CHANGE: firmware 2.2 rollout
      .\demo.ps1 4         ask again: confident, wrong, zero errors
      .\demo.ps1 5         evals go red: 15/20, temporal only
      .\demo.ps1 6         trace archaeology: the smoking gun
      .\demo.ps1 7         data eval names the root cause
      .\demo.ps1 8         approval gate holds a write
      .\demo.ps1 9         kill switch
      .\demo.ps1 all       every beat, pausing between each (rehearsal)

  One beat per command. Run them one at a time from the stage.
  Everything printed fits in 72 columns.
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Beat = 'help',

    # Run the beat against the Microsoft Agent Framework planner instead of the
    # deterministic stub. Needs `.\setup.ps1 -Maf` and model credentials.
    # Beats 1, 4, 5, 6, 8 and 9 are the ones this changes.
    [switch]$Maf
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$env:PYTHONHOME = ''

# Colour survives redirection better when Python does not try to be clever.
$env:PYTHONIOENCODING = 'utf-8'

# Flush every line as it prints. Ordering is already correct in a live
# terminal; this only matters if you pipe or tee a beat to a file to review
# it later, where buffered output would otherwise arrive out of order.
$env:PYTHONUNBUFFERED = '1'

# The MAF path has its own environment so the demo environment stays pristine.
$venvName = if ($Maf) { '.venv-maf' } else { '.venv' }
$Python = Join-Path $PSScriptRoot "$venvName\Scripts\python.exe"
if (-not (Test-Path $Python)) {
    Write-Host ""
    Write-Host "No $venvName found. Run this first:" -ForegroundColor Red
    Write-Host $(if ($Maf) { "    .\setup.ps1 -Maf" } else { "    .\setup.ps1" }) -ForegroundColor Red
    Write-Host ""
    exit 1
}

# Extra flags appended to every scout.agent / run_evals call.
$PlannerArgs = if ($Maf) { @('--planner', 'maf') } else { @() }

if ($Maf) {
    Write-Host "  [planner: Microsoft Agent Framework]" -ForegroundColor Magenta
}

# The one question the whole talk turns on.
$Q = "what time should I sit Bean Field in October 2025?"

function Banner($text) {
    Write-Host ""
    Write-Host "============================================================" -ForegroundColor DarkGray
    Write-Host "  $text"
    Write-Host "============================================================" -ForegroundColor DarkGray
}

function Wait-Beat {
    Write-Host ""
    Read-Host "   [enter]" | Out-Null
    Write-Host ""
}

# The eval runners exit non-zero by design -- that is what makes them a CI
# gate. A red suite is the DEMO WORKING, so never let it stop the script.
function Invoke-Scout {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$ScoutArgs)
    & $Python @ScoutArgs
    $global:LASTEXITCODE = 0
}

switch ($Beat) {

    'reset' {
        Banner "RESET -- rebuilding green warehouse"
        Invoke-Scout -m scout.controls reset
        Remove-Item (Join-Path $PSScriptRoot 'traces.jsonl') -ErrorAction SilentlyContinue
        Invoke-Scout -m scout.build_db
    }

    '1' {
        Banner "BEAT 1 -- Scout, doing its job"
        Invoke-Scout -m scout.agent $Q @PlannerArgs
    }

    '2' {
        Banner "BEAT 2 -- the golden suite, green"
        Invoke-Scout -m evals.run_evals @PlannerArgs
    }

    '3' {
        Banner "BEAT 3 -- vendor firmware 2.2 hits the fleet"
        Invoke-Scout -m scout.apply_firmware_rollout
    }

    '4' {
        Banner "BEAT 4 -- same question. same agent. same code."
        Invoke-Scout -m scout.agent $Q @PlannerArgs --trace --highlight-firmware
    }

    '5' {
        Banner "BEAT 5 -- the golden suite, red"
        Invoke-Scout -m evals.run_evals @PlannerArgs
    }

    '6' {
        Banner "BEAT 6 -- what actually hit the warehouse"
        Invoke-Scout -m scout.agent $Q @PlannerArgs --trace --highlight-firmware
    }

    '7' {
        Banner "BEAT 7 -- data eval: root cause, no human required"
        Invoke-Scout -m evals.run_data_evals
    }

    '8' {
        Banner "BEAT 8 -- the agent tries to text the crew"
        Invoke-Scout -m scout.agent "Text the crew we're sitting Bean Field Saturday at six" @PlannerArgs --trace
        Write-Host ""
        Write-Host "--- pending queue ---"
        Invoke-Scout -m scout.controls status
    }

    '9' {
        Banner "BEAT 9 -- kill switch"
        Invoke-Scout -m scout.controls kill agent "eval gate red: temporal aggregations failing"
        Write-Host ""
        Invoke-Scout -m scout.agent $Q @PlannerArgs
        Write-Host ""
        Write-Host "   (release with: .\demo.ps1 release)" -ForegroundColor DarkGray
    }

    'release' {
        Invoke-Scout -m scout.controls release agent
    }

    'status' {
        Invoke-Scout -m scout.controls status
    }

    'all' {
        foreach ($b in @('reset', '1', '2', '3', '4', '5', '6', '7', '8', '9')) {
            & $PSCommandPath $b -Maf:$Maf
            Wait-Beat
        }
        & $PSCommandPath 'release'
    }

    default {
        @"

  Scout AgentOps demo -- Data-Driven Wisconsin 2026

    .\demo.ps1 reset     back to green. run before every rehearsal.
    .\demo.ps1 1         green baseline: agent answers correctly
    .\demo.ps1 2         green evals: 20/20, gate clear
    .\demo.ps1 3         THE CHANGE: firmware 2.2 rollout
    .\demo.ps1 4         ask again: confident, wrong, zero errors
    .\demo.ps1 5         evals go red: 15/20, temporal only
    .\demo.ps1 6         trace archaeology: the smoking gun
    .\demo.ps1 7         data eval names the root cause
    .\demo.ps1 8         approval gate holds a write
    .\demo.ps1 9         kill switch

    .\demo.ps1 all       every beat, pausing between each
    .\demo.ps1 release   clear a kill switch
    .\demo.ps1 status    show approvals and kill switches

  Add -Maf to any beat to run it against the Microsoft Agent
  Framework planner instead of the deterministic stub:

    .\demo.ps1 4 -Maf    same beat, real model, same failure

  Narration: run_of_show.md      Code tour: WALKTHROUGH.md

"@ | Write-Host
    }
}
