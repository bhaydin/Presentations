#!/usr/bin/env bash
# Scout AgentOps demo driver -- Data-Driven Wisconsin 2026
#
# Each beat is one command. Run them ONE AT A TIME from separate terminal
# invocations, or use ./demo.sh <beat> to fire a single beat.
#
#   ./demo.sh reset     back to green. run this before every rehearsal.
#   ./demo.sh 1         green baseline: agent answers correctly
#   ./demo.sh 2         green evals: 20/20, gate clear
#   ./demo.sh 3         THE CHANGE: firmware 2.2 rollout
#   ./demo.sh 4         ask again: confident, wrong, zero errors
#   ./demo.sh 5         evals go red: 15/20, temporal category only
#   ./demo.sh 6         trace archaeology: the smoking gun
#   ./demo.sh 7         data eval names the root cause
#   ./demo.sh 8         approval gate holds a write
#   ./demo.sh 9         kill switch
#   ./demo.sh all       every beat, pausing between each (rehearsal)
#
# Font check before you present: everything here fits in 72 columns.

set -euo pipefail
cd "$(dirname "$0")"

# Prefer the project's private environment if setup created one, so this
# behaves identically to demo.ps1 on Windows. Override with PY=... if needed.
if [[ -z "${PY:-}" ]]; then
  if   [[ -x .venv/bin/python ]];        then PY=.venv/bin/python
  elif [[ -x .venv/Scripts/python.exe ]]; then PY=.venv/Scripts/python.exe
  else PY=python3
  fi
fi

Q="what time should I sit Bean Field in October 2025?"

pause() { echo; read -rp "   [enter]" _ ; echo; }
banner() { echo; echo "============================================================"; echo "  $1"; echo "============================================================"; }

case "${1:-help}" in

reset)
  banner "RESET -- rebuilding green warehouse"
  $PY -m scout.controls reset
  rm -f traces.jsonl
  $PY -m scout.build_db
  ;;

1)
  banner "BEAT 1 -- Scout, doing its job"
  $PY -m scout.agent "$Q"
  ;;

2)
  banner "BEAT 2 -- the golden suite, green"
  $PY -m evals.run_evals || true
  ;;

3)
  banner "BEAT 3 -- vendor firmware 2.2 hits the fleet"
  $PY -m scout.apply_firmware_rollout
  ;;

4)
  banner "BEAT 4 -- same question. same agent. same code."
  $PY -m scout.agent "$Q" --trace --highlight-firmware
  ;;

5)
  banner "BEAT 5 -- the golden suite, red"
  $PY -m evals.run_evals || true
  ;;

6)
  banner "BEAT 6 -- what actually hit the warehouse"
  $PY -m scout.agent "$Q" --trace --highlight-firmware
  ;;

7)
  banner "BEAT 7 -- data eval: root cause, no human required"
  $PY -m evals.run_data_evals || true
  ;;

8)
  banner "BEAT 8 -- the agent tries to text the crew"
  $PY -m scout.agent "Text the crew we're sitting Bean Field Saturday at six" --trace
  echo
  echo "--- pending queue ---"
  $PY -m scout.controls status
  ;;

9)
  banner "BEAT 9 -- kill switch"
  $PY -m scout.controls kill agent "eval gate red: temporal aggregations failing"
  echo
  $PY -m scout.agent "$Q"
  echo
  echo "   (release with: python3 -m scout.controls release agent)"
  ;;

release)
  $PY -m scout.controls release agent
  ;;

status)
  $PY -m scout.controls status
  ;;

all)
  for b in reset 1 2 3 4 5 6 7 8 9; do
    "$0" "$b"
    pause
  done
  "$0" release
  ;;

*)
  sed -n '2,19p' "$0"
  ;;
esac
