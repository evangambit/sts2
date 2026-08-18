"""Detect the installed StS2 version, for stamping and checking fixtures.

A captured fixture is only ground truth for the patch it came from. Without a
stamp, an old fixture silently keeps "passing" against a new game build — or
starts failing for reasons that look like emulator bugs but are really a content
patch. Stamping makes that difference explicit.

Sources, most to least reliable:
  * Steam's appmanifest `buildid` — changes on every patch, always present if the
    game is installed, and needs nothing to have run.
  * The game log's `release=vX.Y.Z` — the human-readable version, but only after
    the game has been launched at least once.
  * The save's `schema_version` — structural compatibility of the save format we
    parse; a bump here can break the capture pipeline itself.
"""

from __future__ import annotations

import re
from pathlib import Path
from typing import Any

APPMANIFEST = Path.home() / (
    "Library/Application Support/Steam/steamapps/appmanifest_2868840.acf"
)
GAME_LOG = Path.home() / "Library/Application Support/SlayTheSpire2/logs/godot.log"


def steam_buildid() -> str | None:
    if not APPMANIFEST.exists():
        return None
    m = re.search(r'"buildid"\s*"(\d+)"', APPMANIFEST.read_text(errors="replace"))
    return m.group(1) if m else None


def release_string() -> str | None:
    """e.g. "v0.107.1", parsed from the most recent game log."""
    for log in (GAME_LOG, *sorted(GAME_LOG.parent.glob("godot2*.log"), reverse=True)):
        if not log.exists():
            continue
        m = re.search(r"release=(v[\d.]+)", log.read_text(errors="replace"))
        if m:
            return m.group(1)
    return None


def detect(save: dict[str, Any] | None = None) -> dict[str, Any]:
    """Build the stamp to embed in a fixture, or compare one against."""
    stamp: dict[str, Any] = {
        "steam_buildid": steam_buildid(),
        "release": release_string(),
    }
    if save is not None:
        stamp["save_schema_version"] = save.get("schema_version")
    return stamp


def describe(stamp: dict[str, Any]) -> str:
    return (
        f"release={stamp.get('release') or '?'} "
        f"buildid={stamp.get('steam_buildid') or '?'} "
        f"save_schema={stamp.get('save_schema_version') or '?'}"
    )


def check(fixture_stamp: dict[str, Any] | None) -> bool:
    """Warn when a fixture predates the installed game. True when they agree."""
    if not fixture_stamp:
        print(
            "\n!! This fixture carries no game version stamp, so there is no way to "
            "tell which patch it describes. Re-capture it.",
        )
        return False

    current = detect()
    # Only compare fields we could actually detect — absence is not a mismatch.
    fields = [
        k
        for k in ("steam_buildid", "release")
        if current.get(k) is not None and fixture_stamp.get(k) is not None
    ]
    mismatched = [k for k in fields if current[k] != fixture_stamp[k]]
    if not fields:
        print("\n!! Could not detect the installed game version; stamp unverified.")
        return True
    if mismatched:
        print(
            f"\n!! GAME VERSION MISMATCH on {', '.join(mismatched)}.\n"
            f"   fixture:   {describe(fixture_stamp)}\n"
            f"   installed: {describe(current)}\n"
            "   This fixture is ground truth for a DIFFERENT patch. Differences below\n"
            "   may be content changes rather than emulator bugs — re-capture before\n"
            "   drawing conclusions.",
        )
        return False
    return True
