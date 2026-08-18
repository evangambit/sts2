#!/usr/bin/env python3
"""Generate the C# run-generation capture tests from the committed fixtures.

Expected values here come from the **game**, never from the emulator. That
distinction is the whole point: regenerating a fixture from a live save and letting
the assertions follow is just re-reading ground truth, and it cannot mask an
emulator regression because only the game side moves — the emulator is still
compared against it. Deriving expectations from our own output would be the
rubber stamp, and this script never does that.

These assertions used to be hand-transcribed, which was lossy (the map only ever
got a node count) and went stale the moment a fixture was re-captured. Now
`--save-fixture` followed by this script propagates a re-capture all the way into
the C# suite.

    python scripts/generate_capture_tests.py
"""

from __future__ import annotations

import importlib.util
import json
import re
from pathlib import Path
from types import ModuleType

REPO = Path(__file__).resolve().parent.parent
FIXTURES = REPO / "tests" / "fixtures" / "run_generation"
OUT = REPO / "src" / "Sts2Emulator.Tests" / "RunGenerationCaptures.g.cs"


def _load(name: str) -> ModuleType:
    """Import a sibling script by path.

    Raises:
        RuntimeError: if the script is missing or cannot be loaded.

    """
    spec = importlib.util.spec_from_file_location(name, REPO / "scripts" / f"{name}.py")
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Could not load {name}.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


verify = _load("verify_run_generation")

ACT_CONSTANT = {"ACT.OVERGROWTH": "ActOvergrowth", "ACT.UNDERDOCKS": "ActUnderdocks"}


def squash(name: str) -> str:
    """Letters only, lowercased — keeps the weak/normal suffix."""
    return re.sub(r"[^A-Za-z0-9]", "", name.replace("ENCOUNTER.", "")).lower()


def resolve(live_name: str, names: dict[int, str]) -> int:
    """Live encounter id -> emulator id.

    Exact match first, so NIBBITS_WEAK and NIBBITS_NORMAL resolve to their own
    entries. Only then fall back to the variant-stripped form, which is how names
    like SHRINKER_BEETLE_WEAK reach the emulator's `ShrinkerBeetle`. Ambiguity is an
    error rather than a guess.

    Raises:
        SystemExit: if the live name matches no emulator id, or more than one.

    """
    exact = [i for i, n in names.items() if squash(n) == squash(live_name)]
    if len(exact) == 1:
        return exact[0]

    target = verify.normalize(live_name)
    loose = [i for i, n in names.items() if verify.normalize(n) == target]
    if len(loose) == 1:
        return loose[0]
    raise SystemExit(
        f"cannot resolve {live_name!r} to a single emulator encounter "
        f"(exact={[names[i] for i in exact]}, loose={[names[i] for i in loose]})",
    )


def method_name(seed: str) -> str:
    return "".join(c for c in seed.title() if c.isalnum())


def main() -> None:
    names = verify.encounter_names()
    fixtures = sorted(FIXTURES.glob("*.json"))
    if not fixtures:
        raise SystemExit(f"no fixtures in {FIXTURES}")

    blocks = []
    for path in fixtures:
        save = json.loads(path.read_text())
        act = save["acts"][save["current_act_index"]]
        seed = save["rng"]["seed"]
        rooms = act["rooms"]

        normal = [resolve(n, names) for n in rooms["normal_encounter_ids"]]
        elite = [resolve(n, names) for n in rooms["elite_encounter_ids"]]
        boss = resolve(rooms["boss_id"], names)

        smap = act["saved_map"]
        rows: dict[int, int] = {}
        for pt in smap["points"]:
            rows[pt["coord"]["row"]] = rows.get(pt["coord"]["row"], 0) + 1
        for key in ("start", "boss"):
            node = smap.get(key)
            if node:
                rows[node["coord"]["row"]] = rows.get(node["coord"]["row"], 0) + 1
        per_row = [rows.get(r, 0) for r in range(max(rows) + 1)]
        stamp = save.get("game") or {}

        blocks.append(f"""    /// <summary>
    /// Live capture: seed "{seed}" at ascension {save.get("ascension")},
    /// {act["id"]}, game {stamp.get("release", "?")} (build {stamp.get("steam_buildid", "?")}).
    /// Source: tests/fixtures/run_generation/{path.name}
    /// </summary>
    [Fact]
    public void RunGeneration_MatchesCapture_{method_name(seed)}()
    {{
        var engine = new RunEngine();
        engine.Reset("{seed}");
        var s = engine.State;

        Assert.Equal(RunConstants.{ACT_CONSTANT[act["id"]]}, s.Act);
        Assert.Equal(new[] {{ {", ".join(map(str, normal))} }}, s.NormalEncounterSequence);
        Assert.Equal(new[] {{ {", ".join(map(str, elite))} }}, s.EliteEncounterSequence);
        Assert.Equal({boss}, s.BossEncounterId);
        Assert.Equal({sum(per_row)}, s.MapNodes.Count);
        Assert.Equal(
            new[] {{ {", ".join(map(str, per_row))} }},
            Enumerable
                .Range(0, {len(per_row)})
                .Select(row => s.MapNodes.Values.Count(n => n.Row == row))
                .ToArray()
        );
    }}""")

    OUT.write_text(
        "// AUTO-GENERATED — do not edit. Re-run scripts/generate_capture_tests.py.\n"
        "//\n"
        "// Expected values come from live game captures in tests/fixtures/run_generation/,\n"
        "// never from the emulator. Re-capture a fixture, re-run the generator, and these\n"
        "// follow automatically. The full row/column/type map comparison lives in\n"
        "// tests/python/test_live_fixtures.py against the same fixtures.\n"
        "using Sts2Emulator.Core.Run;\n"
        "using Xunit;\n\n"
        "namespace Sts2Emulator.Tests;\n\n"
        "public class RunGenerationCaptureTests\n{\n" + "\n\n".join(blocks) + "\n}\n",
        encoding="utf-8",
    )
    print(f"wrote {OUT.relative_to(REPO)} ({len(blocks)} captures)")
    for path in fixtures:
        print(f"  from {path.name}")


if __name__ == "__main__":
    main()
