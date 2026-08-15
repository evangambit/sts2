#!/usr/bin/env bash

set -euo pipefail # Exit on errors and undefined variables.

START_SECONDS="$SECONDS"

UV="${UV:-uv}"
if ! command -v "$UV" >/dev/null 2>&1; then
  for candidate in \
    "/mnt/c/Users/james/AppData/Local/Microsoft/WinGet/Packages/astral-sh.uv_Microsoft.Winget.Source_8wekyb3d8bbwe/uv.exe" \
    "/c/Users/james/AppData/Local/Microsoft/WinGet/Packages/astral-sh.uv_Microsoft.Winget.Source_8wekyb3d8bbwe/uv.exe"
  do
    if [ -x "$candidate" ]; then
      UV="$candidate"
      break
    fi
  done
fi

DOTNET="${DOTNET:-dotnet}"
if ! command -v "$DOTNET" >/dev/null 2>&1; then
  for candidate in \
  "/mnt/c/Program Files/dotnet/dotnet.exe" \
  "/c/Program Files/dotnet/dotnet.exe"
  do
    if [ -x "$candidate" ]; then
      DOTNET="$candidate"
      break
    fi
  done
fi

# Check Python formatting
"$UV" run black . --check --target-version py314

# Lint Python
"$UV" run ruff check .

# Type-check Python
"$UV" run ty check . --error-on-warning

# Check C# formatting
"$DOTNET" csharpier check .

# Build C#
"$DOTNET" build src/Sts2Emulator.Tests/Sts2Emulator.Tests.csproj --configuration Release
"$DOTNET" publish src/Sts2Emulator/Sts2Emulator.csproj -c Release -r win-x64 --self-contained -o out --nologo

# Test Python
"$UV" run python -m unittest tests/python/test_sts2_gym.py
"$UV" run python scripts/train.py --check --run-env
"$UV" run python scripts/evaluate.py --episodes 2 --run-env --policy first-valid --max-episode-steps 20

# Test the emulator
"$DOTNET" test src/Sts2Emulator.Tests/Sts2Emulator.Tests.csproj --nologo

ELAPSED_SECONDS="$((SECONDS - START_SECONDS))"
echo -e "\n$0 successfully completed in $ELAPSED_SECONDS seconds."
