#!/usr/bin/env bash
set -euo pipefail

# ilspycmd targets .NET 8 but we only have .NET 9+ — allow roll-forward
export DOTNET_ROLL_FORWARD=LatestMajor

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
GAME_DIR="${1:-}"

if [ -z "$GAME_DIR" ]; then
  case "$(uname -s)" in
    Darwin)
      GAME_DIR="$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2" ;;
    *)
      STEAM_ROOT="/c/Program Files (x86)/Steam"
      VDF="$STEAM_ROOT/steamapps/libraryfolders.vdf"
      GAME_DIR=$(grep '"path"' "$VDF" 2>/dev/null \
      | sed 's/.*"\(.*\)".*/\1/' \
      | sed 's|\\\\|/|g' \
      | while read -r lib; do
          candidate="$lib/steamapps/common/Slay the Spire 2"
          [ -d "$candidate" ] && echo "$candidate" && break
      done || true)
      GAME_DIR="${GAME_DIR:-$STEAM_ROOT/steamapps/common/Slay the Spire 2}" ;;
  esac
fi

# Locate sts2.dll under whichever data_sts2_<platform> folder exists (win/mac/linux),
# whether GAME_DIR is the game root, the macOS .app, or the Resources dir.
DLL="$(find "$GAME_DIR" -maxdepth 5 -type f -name sts2.dll -path '*data_sts2_*' 2>/dev/null | head -1)"

if [ -z "$DLL" ] || [ ! -f "$DLL" ]; then
  echo "Error: could not find sts2.dll under $GAME_DIR"
  echo "Pass the game directory as an argument: bash scripts/decompile.sh \"/path/to/Slay the Spire 2\""
  exit 1
fi
echo "Using assembly: $DLL"

if command -v sha256sum >/dev/null 2>&1; then
  HASH=$(sha256sum "$DLL" | awk '{print $1}')
else
  HASH=$(shasum -a 256 "$DLL" | awk '{print $1}')
fi
STORED=$(cat "$REPO_ROOT/decompiled/.version" 2>/dev/null || echo "")

if [ "$HASH" = "$STORED" ]; then
  echo "sts2.dll unchanged ($HASH) — skipping decompile."
  exit 0
fi

echo "New version detected ($HASH). Decompiling sts2.dll..."
mkdir -p "$REPO_ROOT/decompiled"
ilspycmd "$DLL" --outputdir "$REPO_ROOT/decompiled/" --project
echo "$HASH" > "$REPO_ROOT/decompiled/.version"
echo "Done. Review decompiled/ and commit to record this version."
