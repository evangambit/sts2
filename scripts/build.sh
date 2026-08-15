#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
# Default runtime to the host platform; override: bash scripts/build.sh linux-x64
if [ -n "${1:-}" ]; then
  RUNTIME="$1"
else
  case "$(uname -sm)" in
    "Darwin arm64")  RUNTIME="osx-arm64" ;;
    "Darwin x86_64") RUNTIME="osx-x64" ;;
    "Linux x86_64")  RUNTIME="linux-x64" ;;
    *)               RUNTIME="win-x64" ;;
  esac
fi

echo "Building Sts2Emulator for $RUNTIME..."

dotnet publish "$REPO_ROOT/src/Sts2Emulator/Sts2Emulator.csproj" \
  -c Release \
  -r "$RUNTIME" \
  --self-contained \
  -o "$REPO_ROOT/out/"

echo "Output: $REPO_ROOT/out/"
ls "$REPO_ROOT/out/"
