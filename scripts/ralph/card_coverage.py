"""Report card coverage signals for Ralph batch planning.

This script is intentionally read-only. It compares generated card definitions,
explicit native card-effect cases, card IDs referenced by run-level reward/shop
pools, and card-like entries in retained full-run traces.
"""

from __future__ import annotations

import argparse
import json
import re
from collections import Counter, defaultdict
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any

REPO_ROOT = Path(__file__).resolve().parents[2]
GENERATED_CARDS = REPO_ROOT / "src" / "Sts2Emulator" / "Generated" / "Cards.g.cs"
CARD_EFFECTS = (
    REPO_ROOT / "src" / "Sts2Emulator" / "Core" / "Effects" / "CardEffects.cs"
)
RUN_REWARD_GENERATOR = (
    REPO_ROOT / "src" / "Sts2Emulator" / "Core" / "Run" / "RunRewardGenerator.cs"
)
TRACE_DIR = REPO_ROOT / "traces" / "full-run"

CARD_DEF_RE = re.compile(
    r'new CardDef\(Id: (?P<id>-?\d+), Name: "(?P<name>[^"]+)", '
    r"Cost: (?P<cost>-?\d+), BaseDamage: (?P<damage>-?\d+), "
    r"BaseBlock: (?P<block>-?\d+), UpgradeDamage: (?P<upgrade_damage>-?\d+), "
    r"UpgradeBlock: (?P<upgrade_block>-?\d+), Type: CardType\.(?P<type>\w+)"
    r"(?P<flags>[^)]*)\)",
)
CONST_RE = re.compile(r"public const int (?P<name>\w+)\s*=\s*(?P<id>-?\d+);")
CASE_RE = re.compile(r"^\s*case (?P<ref>[A-Z][A-Z0-9]*\.\w+|-?\d+)\s*:", re.MULTILINE)
CS_ARRAY_RE = re.compile(
    r"public static ReadOnlySpan<int> (?P<name>\w+)\s*=>\s*\[(?P<body>.*?)\];",
    re.DOTALL,
)
CARD_RARITY_RE = re.compile(r"^\s*(?P<id>\d+):\s*CARD_RARITY_", re.MULTILINE)
CARD_CONSTANT_RE = re.compile(
    r"^(?P<name>[A-Z0-9_]*CARD[A-Z0-9_]*)\s*=\s*(?P<id>\d+)\s*$",
    re.MULTILINE,
)

CARD_CONTEXT_KEYS = {
    "card",
    "card_id",
    "card_ids",
    "card_reward",
    "card_rewards",
    "draw_pile",
    "deck",
    "discard_pile",
    "exhaust_pile",
    "hand",
    "library",
    "master_deck",
    "reward_cards",
    "selected_card",
}
SCALAR_CARD_LIST_KEYS = {
    "card_ids",
    "draw_pile",
    "deck",
    "discard_pile",
    "exhaust_pile",
    "hand",
    "master_deck",
}
RUN_CARD_POOL_NAMES = {
    "IroncladRewardPool",
    "ColorlessRewardPool",
    "ShopAttackCards",
    "ShopSkillCards",
    "ShopPowerCards",
}


@dataclass(frozen=True)
class CardDef:
    id: int
    name: str
    cost: int
    base_damage: int
    base_block: int
    upgrade_damage: int
    upgrade_block: int
    type: str
    flags: str


@dataclass(frozen=True)
class Candidate:
    id: int
    name: str
    type: str
    cost: int
    score: int
    trace_count: int
    run_pool: bool
    tags: tuple[str, ...]


def normalize_name(value: str) -> str:
    return re.sub(r"[^a-z0-9]", "", value.lower())


def parse_generated_cards(path: Path) -> dict[int, CardDef]:
    cards: dict[int, CardDef] = {}
    for match in CARD_DEF_RE.finditer(path.read_text()):
        card = CardDef(
            id=int(match.group("id")),
            name=match.group("name"),
            cost=int(match.group("cost")),
            base_damage=int(match.group("damage")),
            base_block=int(match.group("block")),
            upgrade_damage=int(match.group("upgrade_damage")),
            upgrade_block=int(match.group("upgrade_block")),
            type=match.group("type"),
            flags=match.group("flags"),
        )
        cards[card.id] = card
    return cards


def parse_native_case_ids(path: Path) -> set[int]:
    source = path.read_text()
    constants = {
        match.group("name"): int(match.group("id"))
        for match in CONST_RE.finditer(source)
    }
    case_ids: set[int] = set()
    for match in CASE_RE.finditer(source):
        ref = match.group("ref")
        if "." in ref:
            _, name = ref.split(".", 1)
            if name in constants:
                case_ids.add(constants[name])
        else:
            case_ids.add(int(ref))
    return case_ids


def parse_run_pool_card_ids(path: Path, generated_ids: set[int]) -> set[int]:
    source = path.read_text()
    ids: set[int] = set()
    for match in CS_ARRAY_RE.finditer(source):
        if match.group("name") not in RUN_CARD_POOL_NAMES:
            continue
        ids.update(int(value) for value in re.findall(r"\b\d+\b", match.group("body")))
    ids.update(int(match.group("id")) for match in CARD_RARITY_RE.finditer(source))
    ids.update(
        int(match.group("id"))
        for match in CARD_CONSTANT_RE.finditer(source)
        if "RARITY" not in match.group("name")
        and (
            match.group("name").endswith("_CARD")
            or match.group("name").endswith("_CARDS")
        )
    )
    return ids & generated_ids


def trace_card_counts(trace_dir: Path, cards: dict[int, CardDef]) -> Counter[int]:
    generated_ids = set(cards)
    ids_by_name = {normalize_name(card.name): card.id for card in cards.values()}
    counts: Counter[int] = Counter()
    for path in sorted(trace_dir.glob("*.json")):
        try:
            data = json.loads(path.read_text())
        except json.JSONDecodeError:
            continue
        collect_trace_cards(data, counts, generated_ids, ids_by_name)
    return counts


def collect_trace_cards(
    value: Any,
    counts: Counter[int],
    generated_ids: set[int],
    ids_by_name: dict[str, int],
) -> None:
    if isinstance(value, dict):
        for key, child in value.items():
            if key in {"card_id", "cardId", "def_id", "defId"}:
                collect_cardish_value(child, counts, generated_ids, ids_by_name)
            elif key in CARD_CONTEXT_KEYS or "card" in key.lower():
                collect_card_container(
                    child,
                    counts,
                    generated_ids,
                    ids_by_name,
                    allow_scalar=key in SCALAR_CARD_LIST_KEYS,
                )
            else:
                collect_trace_cards(child, counts, generated_ids, ids_by_name)
        return

    if isinstance(value, list):
        for child in value:
            collect_trace_cards(child, counts, generated_ids, ids_by_name)
        return


def collect_card_container(
    value: Any,
    counts: Counter[int],
    generated_ids: set[int],
    ids_by_name: dict[str, int],
    *,
    allow_scalar: bool,
) -> None:
    if isinstance(value, dict):
        collect_cardish_value(value.get("id"), counts, generated_ids, ids_by_name)
        collect_cardish_value(value.get("card_id"), counts, generated_ids, ids_by_name)
        collect_cardish_value(value.get("cardId"), counts, generated_ids, ids_by_name)
        collect_cardish_value(value.get("def_id"), counts, generated_ids, ids_by_name)
        collect_cardish_value(value.get("defId"), counts, generated_ids, ids_by_name)
        collect_cardish_value(value.get("name"), counts, generated_ids, ids_by_name)
        for key, child in value.items():
            if key in CARD_CONTEXT_KEYS or "card" in key.lower():
                collect_card_container(
                    child,
                    counts,
                    generated_ids,
                    ids_by_name,
                    allow_scalar=key in SCALAR_CARD_LIST_KEYS,
                )
        return

    if isinstance(value, list):
        for child in value:
            collect_card_container(
                child,
                counts,
                generated_ids,
                ids_by_name,
                allow_scalar=allow_scalar,
            )
        return

    if allow_scalar:
        collect_cardish_value(value, counts, generated_ids, ids_by_name)


def collect_cardish_value(
    value: Any,
    counts: Counter[int],
    generated_ids: set[int],
    ids_by_name: dict[str, int],
) -> None:
    if isinstance(value, int) and value in generated_ids:
        counts[value] += 1
        return
    if isinstance(value, str):
        if value.isdigit() and int(value) in generated_ids:
            counts[int(value)] += 1
            return
        card_id = ids_by_name.get(normalize_name(value))
        if card_id is not None:
            counts[card_id] += 1


def card_tags(card: CardDef) -> tuple[str, ...]:
    tags: list[str] = [card.type.lower()]
    if card.base_damage > 0:
        tags.append("damage")
    if card.base_block > 0:
        tags.append("block")
    if card.cost == 0:
        tags.append("zero-cost")
    if card.cost < 0:
        tags.append("unplayable")
    if "Exhaust: true" in card.flags:
        tags.append("exhaust")
    if "Ethereal: true" in card.flags:
        tags.append("ethereal")
    if card.upgrade_damage or card.upgrade_block:
        tags.append("upgrades-value")
    return tuple(tags)


def fallback_covers(card: CardDef) -> bool:
    if "Unplayable: true" in card.flags:
        return True
    if card.type == "Attack":
        return card.base_damage > 0 and card.base_block == 0
    if card.type == "Skill":
        return (
            card.base_damage == 0
            and card.base_block > 0
            and "Exhaust: true" not in card.flags
        )
    return False


def build_candidates(
    cards: dict[int, CardDef],
    native_case_ids: set[int],
    run_pool_ids: set[int],
    trace_counts: Counter[int],
) -> list[Candidate]:
    candidates: list[Candidate] = []
    for card_id, card in cards.items():
        if card_id in native_case_ids:
            continue
        if fallback_covers(card):
            continue
        score = trace_counts[card_id] * 100 + (10 if card_id in run_pool_ids else 0)
        if score == 0:
            continue
        candidates.append(
            Candidate(
                id=card.id,
                name=card.name,
                type=card.type,
                cost=card.cost,
                score=score,
                trace_count=trace_counts[card_id],
                run_pool=card_id in run_pool_ids,
                tags=card_tags(card),
            ),
        )
    return sorted(candidates, key=lambda item: (-item.score, item.type, item.name))


def print_markdown_report(
    cards: dict[int, CardDef],
    native_case_ids: set[int],
    run_pool_ids: set[int],
    trace_counts: Counter[int],
    candidates: list[Candidate],
    limit: int,
) -> None:
    print("# Card Coverage Report")
    print()
    print(f"- Generated cards: {len(cards)}")
    print(f"- Explicit native CardEffects cases: {len(native_case_ids & set(cards))}")
    print(f"- Run reward/shop/deck card references: {len(run_pool_ids)}")
    print(f"- Trace-observed generated cards: {len(trace_counts)}")
    print(
        f"- Trace/run-pool cards likely needing explicit native cases: {len(candidates)}",
    )
    print()

    print("## Highest-priority likely native gaps")
    print()
    print("| score | id | name | type | cost | trace count | run pool | tags |")
    print("| ---: | ---: | --- | --- | ---: | ---: | --- | --- |")
    for candidate in candidates[:limit]:
        print(
            f"| {candidate.score} | {candidate.id} | {candidate.name} | "
            f"{candidate.type} | {candidate.cost} | {candidate.trace_count} | "
            f"{'yes' if candidate.run_pool else 'no'} | {', '.join(candidate.tags)} |",
        )

    print()
    print("## Suggested mechanic batches")
    print()
    grouped: dict[tuple[str, ...], list[Candidate]] = defaultdict(list)
    for candidate in candidates:
        grouped[candidate.tags].append(candidate)
    for tags, group in sorted(
        grouped.items(),
        key=lambda item: (-sum(candidate.score for candidate in item[1]), item[0]),
    )[:5]:
        names = ", ".join(
            f"{candidate.name} ({candidate.id})" for candidate in group[:10]
        )
        print(f"- **{', '.join(tags)}**: {names}")


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Report generated-card coverage signals for Ralph batch planning.",
    )
    parser.add_argument("--limit", type=int, default=25, help="maximum rows to print")
    parser.add_argument(
        "--json",
        action="store_true",
        help="emit machine-readable JSON",
    )
    args = parser.parse_args()

    cards = parse_generated_cards(GENERATED_CARDS)
    native_case_ids = parse_native_case_ids(CARD_EFFECTS)
    run_pool_ids = parse_run_pool_card_ids(RUN_REWARD_GENERATOR, set(cards))
    trace_counts = trace_card_counts(TRACE_DIR, cards)
    candidates = build_candidates(cards, native_case_ids, run_pool_ids, trace_counts)

    if args.json:
        payload = {
            "generated_cards": len(cards),
            "native_case_count": len(native_case_ids & set(cards)),
            "run_pool_card_count": len(run_pool_ids),
            "trace_observed_card_count": len(trace_counts),
            "candidate_count": len(candidates),
            "candidates": [asdict(candidate) for candidate in candidates[: args.limit]],
        }
        print(json.dumps(payload, indent=2, sort_keys=True))
    else:
        print_markdown_report(
            cards,
            native_case_ids,
            run_pool_ids,
            trace_counts,
            candidates,
            args.limit,
        )

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
