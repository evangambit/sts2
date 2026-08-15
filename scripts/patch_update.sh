#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"

echo "=== Step 1: Decompile ==="
bash "$REPO_ROOT/scripts/decompile.sh" "$@"

echo "=== Step 2: Extract data ==="
python "$REPO_ROOT/scripts/extract_data.py"

echo "=== Step 3: Diff patch ==="
python "$REPO_ROOT/scripts/diff_patch.py"

echo "=== Step 4: Build ==="
bash "$REPO_ROOT/scripts/build.sh"

echo "=== Step 5: Test ==="
dotnet test "$REPO_ROOT/src/Sts2Emulator.Tests/"

echo "=== Done ==="
