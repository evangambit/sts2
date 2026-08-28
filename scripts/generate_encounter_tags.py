#!/usr/bin/env python3
"""Generate each encounter's tags, from the game's own encounter models.

An encounter's ``Tags`` are what ``AddWithoutRepeatingTags`` avoids repeating, and the
avoidance is not cosmetic: ``GrabBag.GrabIndex`` REJECTION-SAMPLES, so a missing tag
changes how many draws a grab COSTS, and every draw after it -- the boss, the act's
ancient, the whole of the next act's generation -- lands somewhere else. E66 is what that
looks like from the outside: act 2 opened on the wrong ancient and no list size or
ordering could explain it, because the roll itself was read at the wrong stream position.

The table was hand-transcribed and had drifted by four entries, all of them silent:
KnightsElite, both halves of ScrollsOfBiting, and TunnelerNormal -- which also carries a
SECOND tag the weak version does not. Three of the four are act-3 encounters, so the same
defect was sitting in wait for Glory that E66 sprang on Hive.

The emulator names its encounters with its own enum, which mostly but not always matches
the model class name, so the aliases below are stated once, here. A tagged model that
cannot be mapped is an ERROR rather than a skip: silently dropping one is precisely the
failure this file exists to prevent.

    uv run python scripts/generate_encounter_tags.py
    uv run python scripts/generate_encounter_tags.py --check
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
ENCOUNTERS = REPO / "decompiled" / "MegaCrit.Sts2.Core.Models.Encounters"
FACTORY = REPO / "src" / "Sts2Emulator" / "Core" / "CombatFactory.cs"
OUT = REPO / "src" / "Sts2Emulator" / "Generated" / "EncounterTags.g.cs"

# Model class name -> the emulator's ActOneEncounter name, where the two differ.
#
# Verified one at a time against the rosters both sides build, not by their names:
# TunnelerAndChomper is TunnelerNormal because the game's GenerateMonsters returns
# (Chomper, Tunneler) and the emulator builds exactly that pair, in that order.
# CorpseSlugs and Seapunk collapse a Weak/Normal pair into one emulator entry; both
# halves carry the same tag, so the collapse cannot lose one.
ALIASES = {
    "BowlbugsNormal": "Bowlbugs",
    "ChompersNormal": "Chompers",
    "CorpseSlugsNormal": "CorpseSlugs",
    "CorpseSlugsWeak": "CorpseSlugs",
    "ExoskeletonsNormal": "ExoskeletonsNormal",
    "ExoskeletonsWeak": "Exoskeletons",
    "FuzzyWurmCrawlerWeak": "FuzzyWurmCrawler",
    "AxebotsNormal": "Axebot",
    "DecimillipedeElite": "Decimillipede",
    "PunchOffEventEncounter": "PunchOff",
    "KnightsElite": "Knights",
    "ScrollsOfBitingNormal": "Scrolls",
    "ScrollsOfBitingWeak": "ScrollsWeak",
    "SeapunkNormal": "Seapunk",
    "SeapunkWeak": "Seapunk",
    "ShrinkerBeetleWeak": "ShrinkerBeetle",
    "SlumberingBeetleNormal": "SlumberingBeetle",
    "ThievingHopperWeak": "ThievingHopper",
    "TunnelerNormal": "TunnelerAndChomper",
    "TunnelerWeak": "Tunneler",
}


def encounter_ids() -> dict[str, int]:
    """Read the emulator's ActOneEncounter ordinals, which are its encounter ids.

    Raises:
        SystemExit: if the enum cannot be found in CombatFactory.cs.

    """
    text = FACTORY.read_text(encoding="utf-8")
    body = re.search(
        r"internal enum ActOneEncounter\s*\{(.*?)\n    \}", text, re.DOTALL,
    )
    if body is None:
        raise SystemExit("could not find the ActOneEncounter enum in CombatFactory.cs")
    names = [
        line.strip().rstrip(",")
        for line in body.group(1).splitlines()
        if line.strip() and not line.strip().startswith("//")
    ]
    return {name: index for index, name in enumerate(names)}


def model_tags() -> dict[str, list[str]]:
    """Every encounter model that declares Tags, and which ones."""
    tagged: dict[str, list[str]] = {}
    for path in sorted(ENCOUNTERS.glob("*.cs")):
        text = path.read_text(encoding="utf-8")
        block = re.search(r"IEnumerable<EncounterTag> Tags =>.*?;", text, re.DOTALL)
        if block is None:
            continue
        tags = re.findall(r"EncounterTag\.(\w+)", block.group(0))
        if tags:
            tagged[path.stem] = tags
    return tagged


ACTS = REPO / "decompiled" / "MegaCrit.Sts2.Core.Models.Acts"

# Act model name -> the emulator's act id (RunConstants.Act*).
ACT_IDS = {"Overgrowth": 1, "Underdocks": 2, "Hive": 3, "Glory": 4}

KINDS = ("Weak", "Normal", "Elite", "Boss")


def emulator_name(model: str) -> str:
    """Map an encounter model to its ActOneEncounter name, by rule then by alias.

    Most models are the enum name plus a kind suffix. The rest are listed in ALIASES;
    a model that matches neither is an error, because a pool that silently drops an
    encounter is a pool the whole act generates against the wrong list.

    Raises:
        SystemExit: if the model matches no enum entry and has no alias.

    """
    if model in ALIASES:
        return ALIASES[model]
    ids = encounter_ids()
    if model in ids:
        return model
    for suffix in KINDS:
        if not model.endswith(suffix):
            continue
        base = model[: -len(suffix)]
        for candidate in (base, base + "s", "The" + base, base.removeprefix("The")):
            if candidate in ids:
                return candidate
    raise SystemExit(f"no ActOneEncounter entry for {model} (add an alias)")


def encounter_kind(model: str) -> str:
    """Weak, Normal, Elite or Boss -- the game's RoomType, plus its IsWeak override."""
    text = (ENCOUNTERS / f"{model}.cs").read_text(encoding="utf-8")
    room = re.search(r"RoomType RoomType => RoomType\.(\w+)", text)
    room_type = room.group(1) if room else "Monster"
    if room_type in {"Elite", "Boss"}:
        return room_type
    return "Weak" if "IsWeak => true" in text else "Normal"


def act_pools() -> dict[str, dict[str, list[tuple[int, str]]]]:
    """Each act's four encounter pools, in the act's own declaration order.

    The order is load-bearing and is NOT the act's ``BossDiscoveryOrder``: the game
    builds its pools by filtering ``GenerateAllEncounters()``, which is declared
    alphabetically, and the grab bags are dealt in that order.

    Raises:
        SystemExit: if an act model has no GenerateAllEncounters to read.

    """
    ids = encounter_ids()
    pools: dict[str, dict[str, list[tuple[int, str]]]] = {}
    for act in ACT_IDS:
        text = (ACTS / f"{act}.cs").read_text(encoding="utf-8")
        body = re.search(r"GenerateAllEncounters\(\).*?\n\t\}", text, re.DOTALL)
        if body is None:
            raise SystemExit(f"could not find GenerateAllEncounters in {act}.cs")
        by_kind: dict[str, list[tuple[int, str]]] = {kind: [] for kind in KINDS}
        for model in re.findall(r"ModelDb\.Encounter<(\w+)>", body.group(0)):
            by_kind[encounter_kind(model)].append((ids[emulator_name(model)], model))
        pools[act] = by_kind
    return pools


ROLLS_OWN_RNG = re.compile(r"base\.Rng|Rng\)")


def slugify(name: str) -> str:
    r"""Slugify a class name the way the game does, giving its ``Id.Entry``.

    The pattern is ``([A-Za-z0-9]|\\G(?!^))([A-Z])`` -> ``$1_$2``, upper-cased: an
    underscore before
    every capital that follows an alphanumeric. Verified against the eight entries that
    were hand-transcribed into ``EncounterRng`` and checked against live captures.
    """
    out: list[str] = []
    for index, char in enumerate(name.strip()):
        if char.isupper() and index > 0 and name[index - 1].isalnum():
            out.append("_")
        out.append(char)
    return "".join(out).upper()


def rolls_own_composition() -> dict[str, str]:
    """Encounter models whose ``GenerateMonsters`` draws, mapped to their ``Id.Entry``.

    An encounter that picks its own monsters -- or their opening moves, or their starting
    HP -- draws from ``EncounterModel.Rng``, a stream seeded per encounter and per floor.
    Rolling any of it on the combat rng gets the roster right by luck only, and there is
    no capture for act 2 or act 3 to notice.
    """
    rolling: dict[str, str] = {}
    for path in sorted(ENCOUNTERS.glob("*.cs")):
        body = re.search(
            r"GenerateMonsters\(\).*?\n\t\}",
            path.read_text(encoding="utf-8"),
            re.DOTALL,
        )
        if body is not None and ROLLS_OWN_RNG.search(body.group(0)):
            rolling[path.stem] = slugify(path.stem)
    return rolling


def collect() -> list[tuple[int, str, list[str]]]:
    ids = encounter_ids()
    rows: dict[int, tuple[str, list[str]]] = {}
    missing: list[str] = []
    for model, tags in sorted(model_tags().items()):
        name = ALIASES.get(model, model)
        if name not in ids:
            missing.append(f"{model} -> {name}")
            continue
        encounter_id = ids[name]
        if encounter_id in rows and rows[encounter_id][1] != tags:
            # Two models collapsed onto one emulator entry must agree, or the collapse
            # is losing a tag rather than deduplicating one.
            raise SystemExit(
                f"{model} and {rows[encounter_id][0]} both map to {name} "
                f"with different tags: {tags} vs {rows[encounter_id][1]}",
            )
        rows[encounter_id] = (name, tags)
    if missing:
        raise SystemExit(
            "tagged encounters with no ActOneEncounter entry (add an alias):\n  "
            + "\n  ".join(missing),
        )
    return [(eid, name, tags) for eid, (name, tags) in sorted(rows.items())]


def render(rows: list[tuple[int, str, list[str]]], pools, rolling) -> str:
    lines = [
        "// AUTO-GENERATED — do not edit. Re-run scripts/generate_encounter_tags.py.",
        "namespace Sts2Emulator.GeneratedData;",
        "",
        "/// <summary>",
        "/// Each encounter's <c>EncounterTag</c>s, which <c>AddWithoutRepeatingTags</c>",
        "/// avoids repeating back to back.",
        "/// </summary>",
        "/// <remarks>",
        "/// Not cosmetic: <c>GrabBag.GrabIndex</c> rejection-samples, so a missing tag",
        "/// changes how many draws a grab COSTS and moves every draw after it — the boss,",
        "/// the ancient, the next act's whole generation. See E66, and E81 for the four",
        "/// entries the hand-written table had lost.",
        "/// </remarks>",
        "internal static class EncounterTags",
        "{",
        "    private static readonly string[] None = [];",
        "",
        "    /// <summary>The tags for an encounter id, empty when it declares none.</summary>",
        "    public static string[] For(int encounterId) =>",
        "        encounterId switch",
        "        {",
    ]
    for encounter_id, name, tags in rows:
        joined = ", ".join(f'"{tag}"' for tag in tags)
        lines.append(f"            {encounter_id} => [{joined}], // {name}")
    lines += [
        "            _ => None,",
        "        };",
        "",
    ]

    lines += [
        "    /// <summary>",
        "    /// The <c>Id.Entry</c> of every encounter whose GenerateMonsters draws.",
        "    /// </summary>",
        "    /// <remarks>",
        "    /// Generated because the hand-kept version of this table was the SILENT half",
        "    /// of the plumbing: a builder can be given its seed and still fall back to the",
        "    /// combat rng, because the seed only exists if the encounter is listed here.",
        "    /// Nothing errors — the roster just comes out of the wrong stream. See E90.",
        "    ///",
        "    /// Keyed by MODEL name rather than encounter id: two models can share one",
        "    /// emulator id (CorpseSlugs weak and normal do) and they have different",
        "    /// entries, so the id alone cannot answer.",
        "    /// </remarks>",
        "    public static string? EntryForModel(string model) =>",
        "        model switch",
        "        {",
    ]
    for model, entry in sorted(rolling.items()):
        lines.append(f'            "{model}" => "{entry}",')
    lines += [
        "            _ => null,",
        "        };",
        "",
    ]

    lines += [
        "    /// <summary>",
        "    /// Each act's four encounter pools, in the act's own declaration order.",
        "    /// </summary>",
        "    /// <remarks>",
        "    /// The order is load-bearing and is NOT the act's <c>BossDiscoveryOrder</c>:",
        "    /// the game filters <c>GenerateAllEncounters()</c>, which is declared",
        "    /// alphabetically, and the grab bags are dealt in that order. Generated for",
        "    /// every act because GLORY's were a placeholder reusing Hive's — reproducing",
        "    /// act 1's and Hive's exactly is what makes Glory's trustworthy.",
        "    /// </remarks>",
        "    public static int[] Pool(int actId, string kind) =>",
        "        (actId, kind) switch",
        "        {",
    ]
    for act, act_id in ACT_IDS.items():
        for kind in KINDS:
            entries = pools[act][kind]
            joined = ", ".join(str(eid) for eid, _ in entries)
            models = ", ".join(model for _, model in entries)
            lines.append(f'            ({act_id}, "{kind}") => [{joined}],')
            lines.append(f"            // {act} {kind}: {models}")
    lines += [
        "            _ => [],",
        "        };",
        "}",
        "",
    ]
    return "\n".join(lines)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--check",
        action="store_true",
        help="fail if the generated file is out of date, instead of writing it",
    )
    args = parser.parse_args()

    rendered = render(collect(), act_pools(), rolls_own_composition())
    if args.check:
        current = OUT.read_text(encoding="utf-8") if OUT.exists() else ""
        if current != rendered:
            print(f"{OUT.relative_to(REPO)} is out of date", file=sys.stderr)
            raise SystemExit(1)
        print(f"{OUT.relative_to(REPO)} is up to date")
        return
    OUT.write_text(rendered, encoding="utf-8")
    print(f"wrote {OUT.relative_to(REPO)}")


if __name__ == "__main__":
    main()
