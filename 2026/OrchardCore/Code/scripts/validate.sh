#!/usr/bin/env bash
# Runs the test suite, then every demo command in sequence, and diffs stdout
# against the committed golden files. This is what catches an accidental
# re-format at 2am.
set -uo pipefail

cd "$(dirname "$0")/.."
export NO_COLOR=1

RETAG=(dotnet run --project src/Retag --)
GOLDEN=tests/golden
failures=0

step() { printf '\n\033[1m%s\033[0m\n' "$1"; }

step "dotnet test"
if ! dotnet test --nologo -v q; then
  echo "FAIL: test suite"
  failures=$((failures + 1))
fi

# name:args, matching the golden file names the suite writes.
run_case() {
  local name="$1"; shift
  local expected="$GOLDEN/$name.txt"
  local actual
  actual=$(mktemp)

  "${RETAG[@]}" "$@" >"$actual" 2>/dev/null

  if [[ ! -f "$expected" ]]; then
    echo "FAIL: no golden file for $name"
    failures=$((failures + 1))
  elif diff -u "$expected" "$actual" >/dev/null; then
    printf '  ok    %s\n' "$name"
  else
    printf '  DIFF  %s\n' "$name"
    diff -u "$expected" "$actual" | head -30
    failures=$((failures + 1))
  fi

  # Nothing may exceed 72 columns.
  local wide
  wide=$(awk 'length($0) > 72 { print FNR": "length($0)" cols" }' "$actual")
  if [[ -n "$wide" ]]; then
    printf '  WIDE  %s\n%s\n' "$name" "$wide"
    failures=$((failures + 1))
  fi

  # Both counters, always.
  if ! grep -q 'ERRORS:' "$actual" || ! grep -q 'HELD BY POLICY:' "$actual"; then
    printf '  FOOTER MISSING  %s\n' "$name"
    failures=$((failures + 1))
  fi

  rm -f "$actual"
}

step "demo commands vs golden"
"${RETAG[@]}" reset >/dev/null 2>&1

run_case "help" --help
run_case "seed" seed
run_case "run_taxonomy_consolidated-2026_workers_12" run --taxonomy consolidated-2026 --workers 12
run_case "propose_item_north-shore-council-emergency-services" \
  propose --item north-shore-council/emergency-services
run_case "trace_show_item_north-shore-council-emergency-services_attrs" \
  trace show --item north-shore-council/emergency-services --attrs
run_case "traces_group-by_taxonomy-version" traces group-by taxonomy.version
run_case "replay_from_replay-v41-txt_taxonomy_42" replay --from replay-v41.txt --taxonomy 42
run_case "run_lot_tenant_canary_40_release_18" run --lot tenant --canary 40 --release 18
run_case "run_lot_tenant_canary_40_release_17" run --lot tenant --canary 40 --release 17
run_case "run_lot_tenant_gate_tenant-boundary" run --lot tenant --gate tenant-boundary
run_case "stop_job_retag-2026_reason_argued-cases-red" stop --job retag-2026 --reason "argued cases red"
run_case "release_job_retag-2026" release --job retag-2026
run_case "reset" reset

step "exit codes"
"${RETAG[@]}" run --lot tenant --gate tenant-boundary >/dev/null 2>&1
if [[ $? -ne 0 ]]; then
  echo "FAIL: a policy hold must exit 0"
  failures=$((failures + 1))
else
  echo "  ok    policy hold exits 0"
fi

"${RETAG[@]}" reset >/dev/null 2>&1

if [[ $failures -eq 0 ]]; then
  printf '\n\033[32mVALIDATE OK\033[0m\n'
  exit 0
fi

printf '\n\033[31mVALIDATE FAILED: %d problem(s)\033[0m\n' "$failures"
exit 1
