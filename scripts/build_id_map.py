#!/usr/bin/env python3
"""Freeze the current Generated/*.g.cs entity ids into data/id_map.json.

Why this exists
---------------
Every extractor in extract_data.py used to assign ids from a running counter over
`sorted(glob(...))`. That makes an id a function of *how many things sort before
it*, so adding, removing or renaming a single card renumbers everything after it —
and every hand-written constant (IC.StrikeIronclad = 472), every committed fixture
and every test literal silently points at the wrong entity.

Freezing the map decouples ids from sort order: known names keep the id they have
today, new names append, and removed names keep their id reserved so it is never
recycled onto different content.

Run once to seed the map (already done); re-run only to re-freeze deliberately.
"""

from __future__ import annotations

import json
import re
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
GENERATED = REPO / "src" / "Sts2Emulator" / "Generated"
ID_MAP = REPO / "data" / "id_map.json"

# file -> (regex capturing id and name, key in the map)
SOURCES = {
    "Cards.g.cs": (re.compile(r'new CardDef\(Id: (\d+), Name: "([^"]+)"'), "cards"),
    "Enemies.g.cs": (re.compile(r'new EnemyDef\(Id: (\d+), Name: "([^"]+)"'), "enemies"),
    "Powers.g.cs": (re.compile(r'new PowerDef\(Id: (\d+), Name: "([^"]+)"'), "powers"),
    "Relics.g.cs": (re.compile(r'new RelicDef\(Id: (\d+), Name: "([^"]+)"'), "relics"),
    "Potions.g.cs": (re.compile(r'new PotionDef\(Id: (\d+), Name: "([^"]+)"'), "potions"),
}


def main() -> None:
    id_map: dict[str, dict[str, int]] = {}
    for filename, (pattern, key) in SOURCES.items():
        path = GENERATED / filename
        if not path.exists():
            raise SystemExit(f"missing {path}")
        entries = pattern.findall(path.read_text(encoding="utf-8"))
        mapping: dict[str, int] = {}
        for raw_id, name in entries:
            if name in mapping and mapping[name] != int(raw_id):
                raise SystemExit(
                    f"{filename}: {name} appears with conflicting ids "
                    f"{mapping[name]} and {raw_id}",
                )
            mapping[name] = int(raw_id)
        id_map[key] = dict(sorted(mapping.items(), key=lambda kv: (kv[1], kv[0])))
        print(f"  {key}: {len(mapping)} ids (max {max(mapping.values())})")

    ID_MAP.parent.mkdir(parents=True, exist_ok=True)
    ID_MAP.write_text(json.dumps(id_map, indent=2) + "\n", encoding="utf-8")
    print(f"wrote {ID_MAP}")


if __name__ == "__main__":
    main()
