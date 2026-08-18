"""Capture and compare a live STS2MCP combat trace against the emulator."""

from __future__ import annotations

import argparse
import importlib.util
import json
import sys
from pathlib import Path
from types import ModuleType
from typing import Any

sys.path.insert(0, str(Path(__file__).parent.parent / "src"))

import compare_traces
import start_real_game_run
import trace_real_game

from sts2_gym import Sts2CombatEnv


def load_emulator_trace_module() -> ModuleType:
    path = Path(__file__).with_name("trace.py")
    spec = importlib.util.spec_from_file_location("emulator_trace", path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Could not load emulator trace module from {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


emulator_trace = load_emulator_trace_module()

CARD_IDS = {
    "ASCENDERS_BANE": 10001,
    "BASH": 30,
    "DEFEND_IRONCLAD": 131,
    "STRIKE_IRONCLAD": 472,
}

ENCOUNTER_BY_ENEMY = {
    ("Corpse Slug", "Corpse Slug"): "corpse-slugs",
    ("Fuzzy Wurm Crawler",): "fuzzy-wurm-crawler",
    ("Nibbit",): "nibbit",
    ("Seapunk",): "seapunk",
    ("Shrinker Beetle",): "shrinker-beetle",
    ("Sludge Spinner",): "sludge-spinner",
    ("Toadpole", "Toadpole"): "toadpoles",
}

LIVE_ENCOUNTER_BY_EMULATOR = {
    "cultists": "CultistsNormal",
    "chompers": "ChompersNormal",
    "nibbit": "NibbitsWeak",
    "slimes": "SlimesWeak",
    "exoskeletons": "ExoskeletonsNormal",
    "inklets": "InkletsNormal",
    "two-tailed-rats": "TwoTailedRatsNormal",
    "gremlin-merc": "GremlinMercNormal",
    "fuzzy-wurm-crawler": "FuzzyWurmCrawlerWeak",
    "corpse-slugs": "CorpseSlugsWeak",
    "sludge-spinner": "SludgeSpinnerWeak",
    "shrinker-beetle": "ShrinkerBeetleWeak",
    "seapunk": "SeapunkWeak",
    "toadpoles": "ToadpolesWeak",
    "mawler": "MawlerNormal",
    "nibbits": "NibbitsNormal",
    "large-slimes": "SlimesNormal",
    "slime-and-flyconid": "FlyconidNormal",
    "jaxfruit-and-flyconid": "SnappingJaxfruitNormal",
    "cubex-construct": "CubexConstructNormal",
    "vine-shambler": "VineShamblerNormal",
    "shrinker-and-fuzzy": "OvergrowthCrawlers",
    "cultist-and-seapunk": "SeapunkNormal",
    "fossil-stalker": "FossilStalkerNormal",
    "punch-construct": "PunchConstructNormal",
    "sewer-clam": "SewerClamNormal",
    "haunted-ship": "HauntedShipNormal",
    "slithering-strangler": "SlitheringStranglerNormal",
    "ruby-raiders": "RubyRaidersNormal",
    "fogmog": "FogmogNormal",
    "living-fog": "LivingFogNormal",
    "bowlbugs-weak": "BowlbugsWeak",
    "bowlbugs": "BowlbugsNormal",
    "tunneler": "TunnelerWeak",
    "tunneler-and-chomper": "TunnelerNormal",
    "thieving-hopper": "ThievingHopperWeak",
    "mytes": "MytesNormal",
    "slumbering-beetle": "SlumberingBeetleNormal",
    "spiny-toad": "SpinyToadNormal",
    "ovicopter": "OvicopterNormal",
    "louse-progenitor": "LouseProgenitorNormal",
    "hunter-killer": "HunterKillerNormal",
    "axebot": "AxebotsNormal",
    "devoted-sculptor": "DevotedSculptorWeak",
    "fabricator": "FabricatorNormal",
    "frog-knight": "FrogKnightNormal",
    "globe-head": "GlobeHeadNormal",
    "turret-operator": "TurretOperatorWeak",
    "owl-magistrate": "OwlMagistrateNormal",
    "scrolls-weak": "ScrollsOfBitingWeak",
    "scrolls": "ScrollsOfBitingNormal",
    "slimed-berserker": "SlimedBerserkerNormal",
    "lost-and-forgotten": "TheLostAndForgottenNormal",
    "obscura": "TheObscuraNormal",
    "construct-menagerie": "ConstructMenagerieNormal",
    "dense-vegetation": "DenseVegetationEventEncounter",
    "punch-off": "PunchOffEventEncounter",
    "fake-merchant": "FakeMerchantEventEncounter",
    "mysterious-knight": "MysteriousKnightEventEncounter",
    "battleworn-dummy-1": "BattlewornDummyEventEncounter",
    "battleworn-dummy-2": "BattlewornDummyEventEncounter",
    "battleworn-dummy-3": "BattlewornDummyEventEncounter",
    "bygone-effigy": "BygoneEffigyElite",
    "entomancer": "EntomancerElite",
    "infested-prisms": "InfestedPrismsElite",
    "phrog-parasite": "PhrogParasiteElite",
    "soul-nexus": "SoulNexusElite",
    "terror-eel": "TerrorEelElite",
    "byrdonis": "ByrdonisElite",
    "decimillipede": "DecimillipedeElite",
    "knights": "KnightsElite",
    "mecha-knight": "MechaKnightElite",
    "phantasmal-gardeners": "PhantasmalGardenersElite",
    "aeonglass": "AeonglassBoss",
    "ceremonial-beast": "CeremonialBeastBoss",
    "kaiser-crab": "KaiserCrabBoss",
    "knowledge-demon": "KnowledgeDemonBoss",
    "lagavulin-matriarch": "LagavulinMatriarchBoss",
    "queen": "QueenBoss",
    "soul-fysh": "SoulFyshBoss",
    "test-subject": "TestSubjectBoss",
    "insatiable": "TheInsatiableBoss",
    "kin": "TheKinBoss",
    "vantom": "VantomBoss",
    "waterfall-giant": "WaterfallGiantBoss",
    "architect": "TheArchitectEventEncounter",
}

DEBUG_START_OPTIONS_BY_EMULATOR = {
    "battleworn-dummy-1": {"setting": "Setting1"},
    "battleworn-dummy-2": {"setting": "Setting2"},
    "battleworn-dummy-3": {"setting": "Setting3"},
}


def enemy_names(summary: dict[str, Any]) -> tuple[str, ...]:
    return tuple(enemy.get("name") or "" for enemy in summary.get("enemies") or [])


def detect_encounter(summary: dict[str, Any]) -> str:
    names = enemy_names(summary)
    if names in ENCOUNTER_BY_ENEMY:
        return ENCOUNTER_BY_ENEMY[names]
    if names and all("Slime" in name for name in names):
        return "slimes"
    raise ValueError(f"Could not map live enemies to an encounter: {names!r}")


def live_hand_ids(summary: dict[str, Any]) -> list[int]:
    hand = []
    for card in (summary.get("player") or {}).get("hand") or []:
        card_id = CARD_IDS.get(str(card.get("id") or ""))
        if card_id is None:
            raise ValueError(
                f"Live hand contains unsupported card {card.get('id')!r}; "
                "choose a Neow option that preserves the starter deck.",
            )
        hand.append(card_id)
    return hand


def validate_starter_player(summary: dict[str, Any]) -> None:
    player = summary.get("player") or {}
    hp = player.get("hp")
    max_hp = player.get("max_hp")
    if (hp, max_hp) != (64, 80):
        raise ValueError(
            f"Live run has {hp}/{max_hp} HP, expected max-ascension starter "
            "64/80. Choose a Neow option that does not change HP.",
        )
    piles = (
        player.get("draw_pile_count"),
        player.get("discard_pile_count"),
        player.get("exhaust_pile_count"),
    )
    if piles != (6, 0, 0):
        raise ValueError(
            f"Live run has pile counts draw/discard/exhaust={piles}, expected "
            "starter deck counts (6, 0, 0). Choose a Neow option that does not "
            "add, remove, transform, or create cards.",
        )


def emulator_completed_combat_rooms(encounter: str) -> int:
    """Combat-count context for the emulator's encounter setup.

    First-floor "weak" encounter variants (e.g. CorpseSlugsWeak = 2 slugs vs Normal
    = 3) are selected when completedCombatRoomsBeforeCurrent is in [0,3); -1 gives the
    normal variant. Encounters mapped to a "*Weak" live encounter need the weak
    context, or the emulator would set up the normal variant and mismatch enemy count.
    """
    live = LIVE_ENCOUNTER_BY_EMULATOR.get(encounter, "")
    return 0 if live.endswith("Weak") else -1


# A run jumped straight into its first combat is at TotalFloor 1: the ancient (Neow) is
# the one map point in its history. Measured, not assumed — floors 0 and 2 give the wrong
# slime roster for seeds where floor 1 reproduces the live one exactly.
NEOW_JUMP_TOTAL_FLOOR = 1


def emulator_initial_summary(
    seed: int,
    encounter: str,
    total_floor: int | None = NEOW_JUMP_TOTAL_FLOOR,
    ascension: int = 8,
) -> dict[str, Any]:
    env = Sts2CombatEnv(
        seed=seed,
        encounter=encounter,
        completed_combat_rooms=emulator_completed_combat_rooms(encounter),
        total_floor=total_floor,
        ascension=ascension,
    )
    try:
        obs, _ = env.reset()
        return emulator_trace.summarize_observation(obs)
    finally:
        env.close()


def initial_matches(
    live_summary: dict[str, Any],
    emulator_summary: dict[str, Any],
    match_hand: bool,
    ignore_hand_order: bool,
) -> bool:
    if match_hand:
        live_hand = live_hand_ids(live_summary)
        emulator_hand = [
            int(card["id"])
            for card in (emulator_summary.get("player") or {}).get("hand") or []
        ]
        if ignore_hand_order:
            live_hand = sorted(live_hand)
            emulator_hand = sorted(emulator_hand)
        if live_hand != emulator_hand:
            return False

    live_enemies = live_summary.get("enemies") or []
    emulator_enemies = emulator_summary.get("enemies") or []
    if len(live_enemies) != len(emulator_enemies):
        return False

    for live_enemy, emulator_enemy in zip(live_enemies, emulator_enemies):
        if live_enemy.get("hp") != emulator_enemy.get("hp"):
            return False
        if live_enemy.get("max_hp") != emulator_enemy.get("max_hp"):
            return False
        live_intent = live_enemy_intent(live_enemy)
        if live_intent is not None:
            emulator_intent = (
                emulator_enemy.get("intent_type"),
                emulator_enemy.get("intent_magnitude"),
            )
            if live_intent[0] != emulator_intent[0]:
                return False
            if live_intent[1] is not None and live_intent[1] != emulator_intent[1]:
                return False

    return True


def live_enemy_intent(enemy: dict[str, Any]) -> tuple[int, int | None] | None:
    intents = enemy.get("intents") or []
    if not intents:
        return None
    if (
        enemy.get("name") == "Sludge Spinner"
        and any(intent.get("type") == "Attack" for intent in intents)
        and any(intent.get("type") == "Debuff" for intent in intents)
    ):
        attack = next(intent for intent in intents if intent.get("type") == "Attack")
        return 3, live_intent_magnitude(attack)
    if (
        enemy.get("name") == "Living Fog"
        and any(intent.get("type") == "Attack" for intent in intents)
        and any(intent.get("type") == "CardDebuff" for intent in intents)
    ):
        attack = next(intent for intent in intents if intent.get("type") == "Attack")
        return 3, live_intent_magnitude(attack)

    intent = intents[0]
    intent_type = intent.get("type")
    intent_by_type = {
        "Attack": 0,
        "Block": 1,
        "Defend": 1,
        "Buff": 2,
        "Debuff": 3,
        "CardDebuff": 3,
        "StatusCard": 3,
    }
    if intent_type not in intent_by_type:
        return None
    return intent_by_type[intent_type], live_intent_magnitude(intent)


def live_intent_magnitude(intent: dict[str, Any]) -> int | None:
    raw_label = intent.get("label")
    if raw_label in (None, ""):
        return None
    label = str(raw_label)
    if "x" in label:
        damage, repeats = label.split("x", 1)
        return int(damage) * int(repeats)
    return int(label)


def format_opening_summary(summary: dict[str, Any]) -> str:
    player = summary.get("player") or {}
    enemies = summary.get("enemies") or []
    hand = [
        str(card.get("id") or card.get("name") or "?")
        for card in player.get("hand") or []
    ]
    enemy_parts = [
        f"{enemy.get('name')} {enemy.get('hp')}/{enemy.get('max_hp')} "
        f"{enemy.get('intents') or ''}"
        for enemy in enemies
    ]
    return (
        f"hand={hand}; enemies={enemy_parts}; "
        f"player={player.get('hp')}/{player.get('max_hp')}"
    )


def find_matching_seed(
    live_summary: dict[str, Any],
    encounter: str,
    limit: int,
    match_hand: bool,
    ignore_hand_order: bool,
    live_trace: dict[str, Any] | None = None,
    actions: list[int] | None = None,
) -> int:
    first_opening_match: int | None = None
    actions = actions or []
    for seed in range(limit):
        if initial_matches(
            live_summary,
            emulator_initial_summary(seed, encounter),
            match_hand,
            ignore_hand_order,
        ):
            if first_opening_match is None:
                first_opening_match = seed
            if live_trace is None or not actions:
                return seed
            emulator_trace_payload = capture_emulator_trace(seed, encounter, actions)
            diffs = compare_traces.compare(
                compare_traces.load_trace_from_payload(emulator_trace_payload),
                compare_traces.load_trace_from_payload(live_trace),
                compare_traces.DEFAULT_FIELDS,
            )
            if not diffs:
                return seed
    if first_opening_match is not None:
        raise ValueError(
            f"No emulator seed below {limit} matched live {encounter} full trace; "
            f"first opening-only match was seed {first_opening_match}",
        )
    raise ValueError(
        f"No emulator seed below {limit} matched live {encounter} opening state: "
        f"{format_opening_summary(live_summary)}",
    )


def capture_emulator_trace(
    seed: int,
    encounter: str,
    actions: list[int],
) -> dict[str, Any]:
    env = Sts2CombatEnv(
        seed=seed,
        encounter=encounter,
        completed_combat_rooms=emulator_completed_combat_rooms(encounter),
    )
    try:
        obs, info = env.reset()
        trace = [
            {
                "step": 0,
                "action": None,
                "reward": 0.0,
                "terminated": False,
                "truncated": False,
                "valid_actions": emulator_trace.valid_actions(env),
                "observation": obs.tolist(),
                "summary": emulator_trace.summarize_observation(obs),
                "info": info,
            },
        ]
        for step, action in enumerate(actions, start=1):
            obs, reward, terminated, truncated, info = env.step(action)
            trace.append(
                {
                    "step": step,
                    "action": action,
                    "reward": reward,
                    "terminated": terminated,
                    "truncated": truncated,
                    "valid_actions": (
                        emulator_trace.valid_actions(env)
                        if not (terminated or truncated)
                        else []
                    ),
                    "observation": obs.tolist(),
                    "summary": emulator_trace.summarize_observation(obs),
                    "info": info,
                },
            )
            if terminated or truncated:
                break
        return {"seed": seed, "encounter": encounter, "trace": trace}
    finally:
        env.close()


def capture_live_trace(
    base_url: str,
    actions: list[int],
    delay: float,
    card_select_index: int | None,
) -> dict[str, Any]:
    state = trace_real_game.get_state(base_url)
    trace = [
        {
            "step": 0,
            "action": None,
            "post": None,
            "summary": trace_real_game.summarize_state(state),
            "raw_state": state,
        },
    ]
    for step, action in enumerate(actions, start=1):
        payload = trace_real_game.action_payload_from_index(state, action)
        post_result = trace_real_game.post_action(base_url, payload)
        state = wait_for_live_action_result(
            base_url,
            delay,
            wait_for_play_phase=payload.get("action") == "end_turn",
            card_select_index=card_select_index,
        )
        trace.append(
            {
                "step": step,
                "action": action,
                "post": payload,
                "post_result": post_result,
                "summary": trace_real_game.summarize_state(state),
                "raw_state": state,
            },
        )
    return {"source": "sts2mcp", "base_url": base_url, "trace": trace}


def wait_for_live_action_result(
    base_url: str,
    delay: float,
    wait_for_play_phase: bool,
    card_select_index: int | None,
) -> dict[str, Any]:
    state = trace_real_game.wait_for_state(
        base_url,
        delay,
        wait_for_play_phase=wait_for_play_phase,
    )
    while card_select_index is not None and state.get("state_type") == "card_select":
        trace_real_game.post_action(
            base_url,
            {"action": "select_card", "index": card_select_index},
        )
        state = trace_real_game.wait_for_state(
            base_url,
            delay,
            wait_for_play_phase=wait_for_play_phase,
        )
    return state


def start_debug_encounter(
    base_url: str,
    seed: str,
    character: str,
    live_encounter: str,
    debug_options: dict[str, str] | None = None,
    ascension: int | None = None,
    abandon_existing: bool = True,
) -> None:
    start_real_game_run.start_seeded_run(
        base_url,
        seed,
        character,
        abandon_existing=abandon_existing,
        ascension=ascension,
    )
    jump_to_encounter(base_url, live_encounter, debug_options)


def jump_to_encounter(
    base_url: str,
    live_encounter: str,
    debug_options: dict[str, str] | None = None,
) -> None:
    """Drop the run already in progress straight into an encounter.

    Split out of start_debug_encounter so it can be used against a run that was
    embarked by hand or restored from a save — embarking through the lobby is the
    fragile part, this half is reliable.

    Raises:
        RuntimeError: if debug_force_play_phase is rejected by the mod.

    """
    payload = {"action": "debug_start_encounter", "encounter": live_encounter}
    if debug_options is not None:
        payload.update(debug_options)
    start_real_game_run.post_action(base_url, payload)
    state = start_real_game_run.wait_for_combat_ready(base_url, timeout=30.0)
    battle = state.get("battle") or {}
    if battle.get("turn") == "player" and battle.get("is_play_phase") is not True:
        force_result = trace_real_game.post_action(
            base_url,
            {"action": "debug_force_play_phase"},
        )
        if force_result.get("status") != "ok":
            raise RuntimeError(f"debug_force_play_phase failed: {force_result}")
        trace_real_game.wait_for_state(base_url, 0.25, wait_for_play_phase=True)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--base-url", default=trace_real_game.DEFAULT_BASE_URL)
    parser.add_argument("--actions", type=int, nargs="*", default=[])
    parser.add_argument("--delay", type=float, default=0.25)
    parser.add_argument(
        "--card-select-index",
        type=int,
        default=0,
        help="Automatically choose this card index when an enemy move opens a choose-card screen; use -1 to disable.",
    )
    parser.add_argument("--seed-search-limit", type=int, default=500000)
    parser.add_argument(
        "--ignore-hand",
        action="store_true",
        help="Match only encounter/enemy HP; useful for end-turn-only enemy behavior checks",
    )
    parser.add_argument(
        "--ignore-hand-order",
        action="store_true",
        help="Match opening hand as an unordered multiset",
    )
    parser.add_argument("--output-dir", type=Path, default=None)
    parser.add_argument("--start-seed", default=None)
    parser.add_argument("--character", default="IRONCLAD")
    parser.add_argument("--map-index", type=int, default=0)
    parser.add_argument("--neow-option", type=int, default=-1)
    parser.add_argument(
        "--encounter",
        default=None,
        help="Emulator encounter id/name. When set with --start-seed, starts the matching live encounter directly through STS2MCP debug_start_encounter.",
    )
    parser.add_argument(
        "--live-encounter",
        default=None,
        help="Override live STS2 encounter model name for --encounter, e.g. ChompersNormal.",
    )
    parser.add_argument("--max-diffs", type=int, default=40)
    args = parser.parse_args()

    if args.start_seed is not None:
        if args.encounter is None:
            start_real_game_run.start_seeded_run(
                args.base_url,
                args.start_seed,
                args.character,
                abandon_existing=True,
            )
            start_real_game_run.enter_first_combat(
                args.base_url,
                args.neow_option,
                args.map_index,
            )
        else:
            live_encounter = args.live_encounter or LIVE_ENCOUNTER_BY_EMULATOR.get(
                args.encounter,
            )
            if live_encounter is None:
                raise ValueError(
                    f"No live encounter mapping for {args.encounter!r}; pass --live-encounter.",
                )
            start_debug_encounter(
                args.base_url,
                args.start_seed,
                args.character,
                live_encounter,
                DEBUG_START_OPTIONS_BY_EMULATOR.get(args.encounter),
            )

    card_select_index = None if args.card_select_index < 0 else args.card_select_index
    live = capture_live_trace(
        args.base_url,
        args.actions,
        args.delay,
        card_select_index,
    )
    live_summary = compare_traces.summary(live["trace"][0])
    validate_starter_player(live_summary)
    encounter = args.encounter or detect_encounter(live_summary)
    if args.output_dir is not None:
        args.output_dir.mkdir(parents=True, exist_ok=True)
        prefix = f"{args.start_seed or 'current'}-{encounter}"
        (args.output_dir / f"{prefix}-real.json").write_text(
            json.dumps(live, indent=2),
            encoding="utf-8",
        )
    emulator_seed = find_matching_seed(
        live_summary,
        encounter,
        args.seed_search_limit,
        match_hand=not args.ignore_hand,
        ignore_hand_order=args.ignore_hand_order,
        live_trace=live,
        actions=args.actions,
    )
    emulator = capture_emulator_trace(emulator_seed, encounter, args.actions)
    diffs = compare_traces.compare(
        compare_traces.load_trace_from_payload(emulator),
        compare_traces.load_trace_from_payload(live),
        compare_traces.DEFAULT_FIELDS,
    )

    if args.output_dir is not None:
        (args.output_dir / f"{prefix}-emulator.json").write_text(
            json.dumps(emulator, indent=2),
            encoding="utf-8",
        )

    print(
        json.dumps(
            {
                "encounter": encounter,
                "emulator_seed": emulator_seed,
                "actions": args.actions,
                "diffs": diffs[: args.max_diffs],
                "diff_count": len(diffs),
            },
            indent=2,
        ),
    )
    if diffs:
        raise SystemExit(1)


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print(f"validate_real_game_trace.py: {exc}", file=sys.stderr)
        raise SystemExit(1) from exc
