#!/usr/bin/env bash
# Runs every case in ops/evals/cases through a fresh, tool-less Claude session.
# Usage: ops/evals/run_reviews.sh <label> <runs> <model> [context-file]
#   label        output folder name under ops/evals/runs/
#   runs         how many passes over the whole set
#   model        model id passed to `claude --model`
#   context-file optional file appended to the system prompt (the one variable for the context run)
# Each review sees only the review prompt and the diff: no tools, no MCP servers,
# no settings files, no CLAUDE.md (it runs from an empty directory), no saved session.
set -euo pipefail

label="$1"; runs="$2"; model="$3"; context_file="${4:-}"
evals_dir="$(cd "$(dirname "$0")" && pwd)"
out_dir="$evals_dir/runs/$label"
empty_dir="$(mktemp -d)"
mkdir -p "$out_dir"

review_prompt='Review this diff for defects. For each finding give the file, the line, what goes wrong, and how confident you are. If you find nothing, say so explicitly rather than listing general observations.'

context_args=()
if [[ -n "$context_file" ]]; then
  context_args=(--append-system-prompt "$(cat "$context_file")")
fi

review_one() {
  local run="$1" case_file="$2"
  local case_name; case_name="$(basename "$case_file" .diff)"
  mkdir -p "$out_dir/run-$run"
  { printf '%s\n\n' "$review_prompt"; cat "$case_file"; } |
    (cd "$empty_dir" && claude -p --model "$model" --tools "" --strict-mcp-config \
      --setting-sources "" --no-session-persistence --output-format json \
      ${context_args[@]+"${context_args[@]}"}) > "$out_dir/run-$run/$case_name.json"
  echo "done run=$run $case_name"
}

for run in $(seq 1 "$runs"); do
  for case_file in "$evals_dir"/cases/case-*.diff; do
    review_one "$run" "$case_file" &
    # Four reviews at a time.
    while (( $(jobs -rp | wc -l) >= 4 )); do sleep 1; done
  done
done
wait
rm -rf "$empty_dir"
echo "all runs written to $out_dir"
