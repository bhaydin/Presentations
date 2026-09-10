#requires -Version 7
# Runs the test suite, then every demo command in sequence, and diffs stdout
# against the committed golden files. This is what catches an accidental
# re-format at 2am.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Continue'

Set-Location (Join-Path $PSScriptRoot '..')
$env:NO_COLOR = '1'

$golden = 'tests/golden'
$script:failures = 0

function Step([string]$Title) {
    Write-Host ''
    Write-Host $Title -ForegroundColor White
}

function Invoke-Retag {
    param([string[]]$RetagArgs)
    $output = & dotnet run --project src/Retag -- @RetagArgs 2>$null
    return ($output -join "`n")
}

function Test-Case {
    param([string]$Name, [string[]]$RetagArgs)

    $expectedPath = Join-Path $golden "$Name.txt"
    $actual = (Invoke-Retag -RetagArgs $RetagArgs).TrimEnd("`n")

    if (-not (Test-Path $expectedPath)) {
        Write-Host "  FAIL: no golden file for $Name" -ForegroundColor Red
        $script:failures++
    }
    else {
        $expected = ((Get-Content $expectedPath -Raw) -replace "`r`n", "`n").TrimEnd("`n")
        if ($expected -eq $actual) {
            Write-Host "  ok    $Name"
        }
        else {
            Write-Host "  DIFF  $Name" -ForegroundColor Red
            $diff = Compare-Object ($expected -split "`n") ($actual -split "`n")
            $diff | Select-Object -First 20 | Format-Table -AutoSize | Out-String | Write-Host
            $script:failures++
        }
    }

    # Nothing may exceed 72 columns.
    $wide = ($actual -split "`n") | Where-Object { $_.Length -gt 72 }
    if ($wide) {
        Write-Host "  WIDE  $Name" -ForegroundColor Red
        $wide | ForEach-Object { Write-Host ("        {0} cols: {1}" -f $_.Length, $_) }
        $script:failures++
    }

    # Both counters, always.
    if ($actual -notmatch 'ERRORS:' -or $actual -notmatch 'HELD BY POLICY:') {
        Write-Host "  FOOTER MISSING  $Name" -ForegroundColor Red
        $script:failures++
    }
}

Step 'dotnet test'
& dotnet test --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Host 'FAIL: test suite' -ForegroundColor Red
    $script:failures++
}

Step 'demo commands vs golden'
Invoke-Retag -RetagArgs @('reset') | Out-Null

Test-Case 'help' @('--help')
Test-Case 'seed' @('seed')
Test-Case 'run_taxonomy_consolidated-2026_workers_12' @('run', '--taxonomy', 'consolidated-2026', '--workers', '12')
Test-Case 'propose_item_north-shore-council-emergency-services' @('propose', '--item', 'north-shore-council/emergency-services')
Test-Case 'trace_show_item_north-shore-council-emergency-services_attrs' @('trace', 'show', '--item', 'north-shore-council/emergency-services', '--attrs')
Test-Case 'traces_group-by_taxonomy-version' @('traces', 'group-by', 'taxonomy.version')
Test-Case 'replay_from_replay-v41-txt_taxonomy_42' @('replay', '--from', 'replay-v41.txt', '--taxonomy', '42')
Test-Case 'run_lot_tenant_canary_40_release_18' @('run', '--lot', 'tenant', '--canary', '40', '--release', '18')
Test-Case 'run_lot_tenant_canary_40_release_17' @('run', '--lot', 'tenant', '--canary', '40', '--release', '17')
Test-Case 'run_lot_tenant_gate_tenant-boundary' @('run', '--lot', 'tenant', '--gate', 'tenant-boundary')
Test-Case 'stop_job_retag-2026_reason_argued-cases-red' @('stop', '--job', 'retag-2026', '--reason', 'argued cases red')
Test-Case 'release_job_retag-2026' @('release', '--job', 'retag-2026')
Test-Case 'reset' @('reset')

Step 'exit codes'
& dotnet run --project src/Retag -- run --lot tenant --gate tenant-boundary *> $null
if ($LASTEXITCODE -ne 0) {
    Write-Host 'FAIL: a policy hold must exit 0' -ForegroundColor Red
    $script:failures++
}
else {
    Write-Host '  ok    policy hold exits 0'
}

Invoke-Retag -RetagArgs @('reset') | Out-Null

Write-Host ''
if ($script:failures -eq 0) {
    Write-Host 'VALIDATE OK' -ForegroundColor Green
    exit 0
}

Write-Host ("VALIDATE FAILED: {0} problem(s)" -f $script:failures) -ForegroundColor Red
exit 1
