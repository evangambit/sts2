"""Verify combat *starts* against the live game, across many seeds and encounters.

Run generation gets swept dozens of seeds at a time; combat had exactly one committed
capture ("ABCDEF" into CorpseSlugsWeak), all of Underdocks was unobserved, and enemy HP
and opening intents rested on a single two-enemy sample. Same headless loop as
`capture_sweep.py`, pointed at the other half of the emulator.

    python scripts/combat_sweep.py --count 3                    # 3 seeds x default set
    python scripts/combat_sweep.py --encounters corpse-slugs seapunk --count 5
    python scripts/combat_sweep.py --act underdocks --count 4 --save-fixtures

Per (seed, encounter) it embarks a fresh A8 run, jumps straight into the encounter with
`debug_start_encounter`, and compares what the game deals against the emulator:

  deck   — the whole shuffled deck IN ORDER (hand + draw pile), the strongest signal
           there is: 11 cards in the right order is 1 in 13,860 by luck
  enemies— count, HP and max HP, which is the Niche stream and the unique-HP rule
  intent — each enemy's opening intent, which is the MonsterAi stream
  player — HP/max HP, a cheap guard that both sides describe the same A8 fight

**Jump immediately and touch nothing on the way.** The direct combat env assumes fresh
per-stream RNG (CallCount 0), which only holds for a run's first combat — so the sweep
never answers Neow, never enters a room, and embarks a new run per encounter rather than
reusing one. That is the only real constraint: **normal-pool encounters do NOT need
three easy fights behind them**, because the debug jump names the encounter model
directly, so `--pool normal` works on a fresh run.

Exit code 0 when every section of every capture matches.
"""

from __future__ import annotations

import argparse
import functools
import importlib.util
import json
import random
import re
import sys
import time
from pathlib import Path
from types import ModuleType
from typing import Any

sys.path.insert(0, str(Path(__file__).parent.parent / "src"))
sys.path.insert(0, str(Path(__file__).parent))


import game_version

from sts2_gym import Sts2CombatEnv, game_seed


def _load(name: str) -> ModuleType:
    path = Path(__file__).with_name(f"{name}.py")
    spec = importlib.util.spec_from_file_location(f"_combat_{name}", path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Could not load {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


capture_sweep = _load("capture_sweep")
enemy_moves = _load("enemy_moves")
compare_draw_pile = _load("compare_draw_pile")
validate = _load("validate_real_game_trace")
start_real_game_run = _load("start_real_game_run")
trace_real_game = _load("trace_real_game")

# BOTH pools are reachable here, which is worth understanding before adding encounters.
#
# `completed_combat_rooms in [0,3)` picks the weak variant, but that rule only governs
# what the MAP hands you: `debug_start_encounter` looks the encounter up by class name
# (ModelDb.AllEncounters) and enters a CombatRoom for it directly, so naming
# "NibbitsNormal" gets the normal pool on floor 1 with no combats behind it. The
# emulator matches by passing completed_combat_rooms = -1 for those, which
# validate_real_game_trace.emulator_completed_combat_rooms derives from the name.
#
# What DOES have to hold is stream freshness: the direct combat env assumes every named
# RNG stream is at CallCount 0, which is true of a run's FIRST combat whichever variant
# it is. So: embark, jump straight in, never answer Neow, one run per capture.
WEAK_BY_ACT = {
    "overgrowth": ["nibbit", "slimes", "shrinker-beetle", "fuzzy-wurm-crawler"],
    "underdocks": ["corpse-slugs", "seapunk", "sludge-spinner", "toadpoles"],
}
# Every normal-pool encounter either act-1 act declares, per
# decompiled/MegaCrit.Sts2.Core.Models.Acts/{Overgrowth,Underdocks}.cs. The list was eight
# for a long time; the other thirteen were reachable the whole while — the debug jump names
# the encounter model directly — and every one of them was unobserved.
NORMAL_BY_ACT = {
    "overgrowth": [
        "nibbits",
        "large-slimes",
        "mawler",
        "vine-shambler",
        "inklets",
        "cubex-construct",
        "slime-and-flyconid",
        "jaxfruit-and-flyconid",
        "shrinker-and-fuzzy",
        "ruby-raiders",
        "fogmog",
        "slithering-strangler",
    ],
    "underdocks": [
        "sewer-clam",
        "punch-construct",
        "fossil-stalker",
        "haunted-ship",
        "cultists",
        "corpse-slugs-normal",
        "two-tailed-rats",
        "gremlin-merc",
        "cultist-and-seapunk",
        "living-fog",
    ],
}
# Act-1 elites and bosses. `debug_start_encounter` reaches these the same way it reaches
# any other encounter — they were simply never listed, so nothing checked the fights an
# agent has to survive to finish the act.
ELITE_BY_ACT = {
    "overgrowth": ["bygone-effigy", "byrdonis", "phrog-parasite"],
    "underdocks": ["phantasmal-gardeners", "skulking-colony", "terror-eel"],
}
BOSS_BY_ACT = {
    "overgrowth": ["kin", "vantom", "ceremonial-beast"],
    "underdocks": ["lagavulin-matriarch", "soul-fysh", "waterfall-giant"],
}
ENCOUNTERS_BY_ACT = {
    act: [
        *WEAK_BY_ACT[act],
        *NORMAL_BY_ACT[act],
        *ELITE_BY_ACT[act],
        *BOSS_BY_ACT[act],
    ]
    for act in WEAK_BY_ACT
}
DEFAULT_ENCOUNTERS = [
    *ENCOUNTERS_BY_ACT["overgrowth"],
    *ENCOUNTERS_BY_ACT["underdocks"],
]


def emulator_summary(seed: str, encounter: str, ascension: int) -> dict[str, Any]:
    """Build the emulator's opening state for this fight, from the derived gen seed."""
    return validate.emulator_initial_summary(
        game_seed(seed),
        encounter,
        ascension=ascension,
    )


def compare_deck(
    seed: str,
    encounter: str,
    live_state: dict[str, Any],
    ascension: int,
) -> tuple[bool, str]:
    """Hand and draw pile, in order — the shuffled deck the game dealt."""
    completed = validate.emulator_completed_combat_rooms(encounter)
    gen_seed = game_seed(seed)
    for pile in ("hand", "draw"):
        emu = compare_draw_pile.emulator_pile(
            gen_seed,
            encounter,
            completed,
            pile,
            ascension=ascension,
        )
        live = compare_draw_pile.live_pile(live_state, pile)
        norm = compare_draw_pile.normalize
        if [(norm(n), up) for n, up in emu] != [(norm(n), up) for n, up in live]:
            return False, f"{pile} pile differs ({len(emu)} emu vs {len(live)} live)"
    return True, ""


def compare_enemies(
    live_summary: dict[str, Any],
    emu_summary: dict[str, Any],
) -> tuple[bool, bool, str]:
    """Compare enemy roster/HP and opening intents, reported separately.

    Two different generators: HP comes off the Niche stream (with the unique-HP rule),
    intents off MonsterAi. Collapsing them into one verdict would hide which is wrong.
    """
    live = live_summary.get("enemies") or []
    emu = emu_summary.get("enemies") or []
    if len(live) != len(emu):
        return False, False, f"enemy count {len(emu)} emu vs {len(live)} live"

    hp_ok, intent_ok, notes = True, True, []
    for index, (live_enemy, emu_enemy) in enumerate(zip(live, emu)):
        if (live_enemy.get("hp"), live_enemy.get("max_hp")) != (
            emu_enemy.get("hp"),
            emu_enemy.get("max_hp"),
        ):
            hp_ok = False
            notes.append(
                f"enemy {index} hp {emu_enemy.get('hp')}/{emu_enemy.get('max_hp')} emu "
                f"vs {live_enemy.get('hp')}/{live_enemy.get('max_hp')} live",
            )

        live_intent = validate.live_enemy_intent(live_enemy)
        if live_intent is None:
            continue
        emu_intent = (emu_enemy.get("intent_type"), emu_enemy.get("intent_magnitude"))
        if live_intent[0] != emu_intent[0] or (
            live_intent[1] is not None and live_intent[1] != emu_intent[1]
        ):
            intent_ok = False
            notes.append(f"enemy {index} intent {emu_intent} emu vs {live_intent} live")

    return hp_ok, intent_ok, "; ".join(notes)


def end_turn_action(live_state: dict[str, Any]) -> int:
    """Give the integer action both sides read as "end turn".

    `trace_real_game.action_payload_from_index` maps hand-size to end_turn, and the
    emulator's action space is laid out the same way, so one integer drives both.
    """
    hand = (live_state.get("player") or {}).get("hand") or []
    return len(hand)


def answer_combat_screen(base_url: str) -> None:
    """Answer a card-selection screen an ENEMY raised, LIVE only.

    Some monsters stop the fight to ask the player something. The Knowledge Demon's
    CURSE_OF_KNOWLEDGE is the first: two curses, and whichever is chosen applies its
    power. A sweep that treats the screen as "combat ended" cannot capture that fight at
    all, which is why the demon had no fixture while the other two Hive bosses did.

    Always the FIRST candidate. The choice has to be deterministic and identical on both
    sides or the two fights diverge by construction; which one it is does not matter to
    the comparison, only that both make it.

    **The emulator is answered elsewhere, and the order is the whole point.** The live
    game ends its turn when `end_turn` is posted; the emulator ends its when `env.step`
    is called, which is AFTER this wait returns. Answering the emulator here reaches it
    while it is still in the player's turn with no screen open -- where `step(0)` means
    "play card 0", so it quietly loses a card per screen and the demon's own selection is
    then left unanswered, putting it a move behind for the rest of the fight.
    """
    trace_real_game.post_action(base_url, {"action": "select_card", "index": 0})

    # Wait for the live screen to close, so the caller's next poll does not answer twice.
    deadline = time.monotonic() + 10.0
    while time.monotonic() < deadline:
        if start_real_game_run.get_state(base_url).get("state_type") != "card_select":
            return
        time.sleep(0.25)


def buff_both_players(base_url: str, env: Any, amount: int) -> list[str]:
    """Raise max HP and heal by it on BOTH sides, before turn one.

    A boss capture is worth what it survives. The Kaiser Crab kills a starter deck on
    turn four -- two moves short of walking either half's table -- and a capture that
    never reaches a move cannot put that move under test, so `coverage` fails and the
    fixture is not committable. Buffing is not cheating here: the comparison is of what
    each side DOES, and both sides get the same player.
    """
    notes: list[str] = []
    result = trace_real_game.post_action(
        base_url,
        {"action": "debug_gain_max_hp", "amount": amount},
    )
    if result.get("status") != "ok":
        notes.append(f"live refused the max-hp buff: {result}")
        return notes

    env.unwrapped.debug_gain_max_hp(amount)
    return notes


def add_cards_to_both_hands(base_url: str, env: Any, cards: list[str]) -> list[str]:
    """Put the same cards on top of both hands, live and emulated.

    Deck-stacking is how a capture reaches a state a starter deck never will: the Phrog
    Parasite's Wrigglers only spawn when it dies, and Terror Eel's second phase only when
    an unblocked hit drops it to its threshold — neither happens before a passive player
    is dead. The HAND is the place to do it, because no shuffle has to agree: the mod
    adds at CardPilePosition.Top and so does the emulator.

    Returns any notes worth reporting; the caller decides whether they are fatal.
    """
    notes: list[str] = []
    slugs = card_slug_to_id()
    # The game holds at most ten cards; asking for more silently drops the overflow and
    # the two sides stop agreeing about the hand.
    room = MAX_HAND_SIZE - len(
        (
            (start_real_game_run.get_state(base_url).get("player") or {}).get("hand")
            or []
        )
    )
    if len(cards) > room:
        notes.append(
            f"only {room} of {len(cards)} cards fit in the hand; the rest were skipped"
        )
        cards = cards[:room]

    for card in cards:
        entry, _, flag = card.partition(":")
        entry = entry.upper()
        upgraded = flag.lower() in {"u", "upgraded", "+"}
        card_id = slugs.get(entry)
        if card_id is None:
            notes.append(
                f"unknown card {entry}; the harness knows it by its model entry"
            )
            continue

        result = trace_real_game.post_action(
            base_url,
            {
                "action": "debug_add_card",
                "card": entry,
                "upgraded": upgraded,
                "pile": "hand",
            },
        )
        if result.get("status") != "ok":
            notes.append(f"live refused {entry}: {result}")
            continue
        env.unwrapped.debug_add_card_to_hand(card_id, upgraded)
    return notes


def wait_for_hand_to_reach(
    base_url: str, size: int, timeout: float = 20.0
) -> dict[str, Any]:
    """Wait for the live hand to grow to `size` — debug_add_card is fire-and-forget."""
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        state = start_real_game_run.get_state(base_url)
        if len(((state.get("player") or {}).get("hand") or [])) >= size:
            return state
        time.sleep(0.2)
    raise RuntimeError("live game never added the debug cards")


def play_action(live_state: dict[str, Any]) -> int:
    """The first card the live game says is playable, or end turn if none is.

    Deliberately the dumbest policy that still fights: the point is not to play well but
    to keep the player alive long enough for an enemy to reach the far end of its move
    table. A no-cards capture of the Waterfall Giant sees five of its seven moves because
    the player is dead by turn six, and no amount of re-seeding fixes that — three seeds
    of two-tailed-rats all end on turn 5 with three of four moves seen.

    Reading `can_play` off the live state rather than deciding ourselves keeps the
    emulator honest: it is told which card to play, not asked which it would allow.
    """
    hand = (live_state.get("player") or {}).get("hand") or []
    for index, card in enumerate(hand):
        if card.get("can_play"):
            return index
    return len(hand)


def action_is_legal_for_emulator(env: Any, action: int) -> bool:
    """Does the emulator agree this action is available?

    The play policy reads `can_play` off the LIVE state and sends the same index to
    both sides, so a disagreement about what is playable silently desynchronises the
    hands and every later row is noise. Asking the mask turns that into a stated
    failure at the turn it happens.
    """
    try:
        mask = env.action_masks()
    except Exception:  # noqa: BLE001 - masks are an optional convenience here
        return True
    return bool(action < len(mask) and mask[action])


def wait_for_card_to_leave_hand(
    base_url: str, hand_size_before: int, timeout: float = 20.0
):
    """Wait until the live game has actually PLAYED the card that was just posted.

    Posting an action and reading the state straight back reads it before the game has
    acted: the card is still in hand and the energy is unspent. Every later action then
    indexes into a hand the live game has already moved on from, and the two sides drift
    apart within one turn — which reads as the emulator playing cards the game would not
    allow. Same trap as `wait_for_next_round`, one level down.
    """
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        state = start_real_game_run.get_state(base_url)
        if state.get("state_type") not in COMBAT_STATE_TYPES:
            return state
        hand = (state.get("player") or {}).get("hand") or []
        if len(hand) < hand_size_before:
            return state
        time.sleep(0.2)
    raise RuntimeError("live game never played the card")


def apply_action(base_url: str, env: Any, live_state: dict[str, Any], action: int):
    """Send one integer action to both sides, and hand back the new live state."""
    hand_size = len((live_state.get("player") or {}).get("hand") or [])
    trace_real_game.post_action(
        base_url, trace_real_game.action_payload_from_index(live_state, action)
    )
    _obs, _reward, terminated, truncated, _info = env.step(action)
    return wait_for_card_to_leave_hand(base_url, hand_size), terminated, truncated


def living_emu_enemies(summary: dict[str, Any]) -> list[dict[str, Any]]:
    """The emulator's enemies that are still alive.

    The emulator keeps a dead enemy in the roster at 0 HP so an agent's observation has
    stable slots; the game removes the creature outright. Comparing the raw lists makes
    every fight where something dies look like an emulator hallucinating an extra
    attacker — which is exactly how it read the first time cards were played.
    """
    return [enemy for enemy in (summary.get("enemies") or []) if enemy.get("hp", 0) > 0]


def enemy_intents(summary: dict[str, Any], live: bool) -> list[tuple[Any, Any]]:
    if live:
        return [
            validate.live_enemy_intent(e) or (None, None)
            for e in summary.get("enemies") or []
        ]
    return [
        (e.get("intent_type"), e.get("intent_magnitude"))
        for e in living_emu_enemies(summary)
    ]


def intents_agree(live: tuple[Any, Any], emu: tuple[Any, Any]) -> bool:
    """Compare two intents, ignoring a magnitude the live side does not report.

    A bare Debuff has no number live, so only the type is meaningful there.
    """
    if live[0] is None:
        return True
    if live[0] != emu[0]:
        return False
    return live[1] is None or live[1] == emu[1]


@functools.cache
def card_slug_to_id() -> dict[str, int]:
    """The game's ModelId.Entry for each card, mapped to our numeric id."""
    text = (
        Path(__file__).parent.parent
        / "src"
        / "Sts2Emulator"
        / "Generated"
        / "Cards.g.cs"
    ).read_text()
    return {
        m.group(2): int(m.group(1))
        for m in re.finditer(r'Id: (\d+), Name: "[^"]*", Entry: "([A-Z0-9_]*)"', text)
    }


def hands_agree(live_player: dict[str, Any], emu_player: dict[str, Any]) -> bool:
    """Same cards, same order — which is the mid-combat reshuffle under test.

    Pile counts can agree turn after turn while the order coming off the top is
    wrong: the game sorts the pile by ModelId before Fisher-Yates, so the shuffle
    starts from the slugified card name and not from any id of ours.
    """
    slugs = card_slug_to_id()
    live = [
        slugs.get(c.get("id"), c.get("id")) for c in (live_player.get("hand") or [])
    ]
    emu = [c.get("id") for c in (emu_player.get("hand") or [])]
    return live == emu


COMBAT_STATE_TYPES = {"monster", "elite", "boss"}

# A turn can play at most this many cards before the sweep gives up and ends it.
MAX_PLAYS_PER_TURN = 12

# The game's hand limit; debug-added cards past it are dropped.
MAX_HAND_SIZE = 10


def live_round(state: dict[str, Any]) -> int:
    return int((state.get("battle") or {}).get("round") or 0)


def wait_for_next_round(base_url: str, previous_round: int, timeout: float = 30.0):
    """Wait until the live game has actually BEGUN the next round.

    `wait_for_combat_ready` only asks for a combat state holding a full hand, and that
    is already true the instant end_turn is posted — before the game has acted on it.
    When it returns early the whole rest of the capture is off by one: every row
    compares the emulator's turn N against the live turn N-1, which reads as an
    emulator running a turn ahead and landing its damage early. Two encounters were
    investigated as damage bugs on the strength of exactly that.
    """
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        state = start_real_game_run.get_state(base_url)
        # A monster can stop its own turn to ask the player something, and the game will
        # not reach a play phase until it is answered. There is no single moment to check
        # for it -- the screen goes up partway through the enemy turn, after end_turn is
        # posted and long before the round advances -- so the watching has to happen in
        # the same poll that waits for the round.
        if state.get("state_type") == "card_select":
            answer_combat_screen(base_url)
            continue
        if state.get("state_type") not in COMBAT_STATE_TYPES:
            return state
        battle = state.get("battle") or {}
        if battle.get("is_play_phase") and live_round(state) > previous_round:
            return state
        time.sleep(0.25)
    raise RuntimeError(f"live game never started round {previous_round + 1}")


def drive_turns(
    base_url: str,
    env: Any,
    turns: int,
    play: bool = False,
    add_cards: list[str] | None = None,
    buff_max_hp: int = 0,
) -> tuple[list[dict[str, Any]], list[str]]:
    """End turn on both sides in lockstep, comparing what the enemies announce.

    Ending the turn without playing anything is the cheapest way to walk an enemy
    through its move table: the intent for turn N+1 is announced as the enemy acts on
    turn N, so T turns show T+1 intents per enemy. It also puts enemy *damage* under
    test — the player's HP only stays in sync if every attack lands for the same amount.

    Stops early when the player dies or the emulator says the combat ended; a fight that
    ends before an enemy has shown every move is reported as missing coverage rather
    than passed over.
    """
    rows: list[dict[str, Any]] = []
    notes: list[str] = []
    if buff_max_hp:
        notes += buff_both_players(base_url, env, buff_max_hp)
    if add_cards:
        opening = start_real_game_run.get_state(base_url)
        before = len(((opening.get("player") or {}).get("hand") or []))
        notes += add_cards_to_both_hands(base_url, env, add_cards)
        wait_for_hand_to_reach(base_url, before + len(add_cards))

    for turn in range(1, turns + 1):
        live_state = start_real_game_run.get_state(base_url)
        if live_state.get("state_type") not in {"monster", "elite", "boss"}:
            notes.append(f"combat ended live before turn {turn}")
            break

        actions: list[int] = []
        terminated = truncated = False
        illegal = False
        if play:
            # Spend the turn first, then end it. Bounded because a card that fails to
            # leave the hand on one side would otherwise loop until the sweep hangs.
            for _ in range(MAX_PLAYS_PER_TURN):
                choice = play_action(live_state)
                if choice >= len(((live_state.get("player") or {}).get("hand") or [])):
                    break
                if not action_is_legal_for_emulator(env, choice):
                    hand = (live_state.get("player") or {}).get("hand") or []
                    name = hand[choice].get("id") if choice < len(hand) else "?"
                    notes.append(
                        f"turn {turn}: live can play {name} at index {choice}, "
                        "the emulator says it cannot",
                    )
                    illegal = True
                    break

                actions.append(choice)
                live_state, terminated, truncated = apply_action(
                    base_url,
                    env,
                    live_state,
                    choice,
                )
                if live_state.get("state_type") not in COMBAT_STATE_TYPES or terminated:
                    break

            if illegal:
                break

            if live_state.get("state_type") not in COMBAT_STATE_TYPES or terminated:
                notes.append(f"combat ended during turn {turn}")
                break

        action = end_turn_action(live_state)
        actions.append(action)
        round_before = live_round(live_state)
        trace_real_game.post_action(base_url, {"action": "end_turn"})
        try:
            # The ROUND wait first: it is the one that answers any screen the enemy turn
            # raised, and `wait_for_combat_ready` asks for a combat state with a full
            # hand -- which a curse screen is neither, so it would time out first.
            wait_for_next_round(base_url, round_before)
            start_real_game_run.wait_for_combat_ready(base_url, timeout=30.0)
        except RuntimeError:
            notes.append(f"live combat did not return to play phase after turn {turn}")
            break

        _obs, _reward, terminated, truncated, _info = env.step(action)
        # The emulator raises its own screen during ITS enemy turn, which is the step
        # just taken -- so its answer belongs here, after it, and matches the live one.
        # ASKED, not counted: a live poll can see one screen twice, and an extra step
        # with nothing open means "play card 0" -- which cost the emulator a card and
        # then left its own selection unanswered, putting the demon a move behind.
        while env.unwrapped.pending_selection_kind():
            env.step(0)
            # RECORDED, so the offline replay steps exactly what the capture stepped
            # rather than re-deriving when a screen was open. Re-deriving is what left
            # the demon a move behind at the far end of its fight.
            actions.append(0)
        live_summary = trace_real_game.summarize_state(
            start_real_game_run.get_state(base_url),
        )
        emu_summary = validate.emulator_trace.summarize_observation(
            env.unwrapped._obs(),
        )

        live_intents = enemy_intents(live_summary, live=True)
        emu_intents = enemy_intents(emu_summary, live=False)
        live_player = live_summary.get("player") or {}
        emu_player = emu_summary.get("player") or {}
        rows.append(
            {
                "turn": turn + 1,
                "action": action,
                # Every action the turn took, in order, ending with end turn. The offline
                # replay walks this; `action` alone only describes a turn that played
                # nothing.
                "actions": actions,
                "live_enemies": [
                    {
                        "name": e.get("name"),
                        "hp": e.get("hp"),
                        "intent": validate.live_enemy_intent(e),
                    }
                    for e in live_summary.get("enemies") or []
                ],
                "emu_enemies": [
                    {
                        "hp": e.get("hp"),
                        "intent": (e.get("intent_type"), e.get("intent_magnitude")),
                    }
                    for e in living_emu_enemies(emu_summary)
                ],
                "intents_match": len(live_intents) == len(emu_intents)
                and all(
                    map(intents_agree, live_intents, emu_intents),
                ),
                "player_match": (live_player.get("hp"), live_player.get("max_hp"))
                == (emu_player.get("hp"), emu_player.get("max_hp")),
                "hand_match": hands_agree(live_player, emu_player),
                "live_player_hp": live_player.get("hp"),
                "live_player_max_hp": live_player.get("max_hp"),
                "emu_player_hp": emu_player.get("hp"),
                # The hand IN ORDER, by model slug. This is what puts the mid-combat
                # RESHUFFLE under test: the pile counts can agree turn after turn while
                # the order coming off the top is wrong, and a status card drawn a turn
                # early is a turn of damage that never shows up anywhere else.
                "live_hand": [
                    card.get("id") for card in (live_player.get("hand") or [])
                ],
                "emu_hand": [card.get("id") for card in (emu_player.get("hand") or [])],
            },
        )
        if terminated or truncated:
            notes.append(f"emulator ended the combat after turn {turn}")
            break
        if not live_player.get("hp"):
            notes.append(f"player died on turn {turn}")
            break
    return rows, notes


def coverage_for(rows: list[dict[str, Any]], opening: dict[str, Any]) -> dict[str, Any]:
    """Distinct intents each enemy actually showed, against what it declares.

    The point of driving turns at all: an opening-only check can pass while every
    later move in the table is wrong.
    """
    # Count distinct (type, magnitude) pairs, not types: WhipSlap and Glomp are both
    # "Attack", so counting types alone caps a three-move slug at 2/3 forever. The count
    # can also EXCEED the table, because a monster that buffs itself announces one move
    # at several magnitudes — so treat a shortfall as the signal and a surplus as normal.
    seen: dict[str, set[Any]] = {}
    for enemy in opening.get("enemies") or []:
        intent = validate.live_enemy_intent(enemy)
        if intent is not None:
            seen.setdefault(str(enemy.get("name")), set()).add(intent)
    for row in rows:
        for enemy in row["live_enemies"]:
            if enemy["intent"] is not None:
                seen.setdefault(str(enemy["name"]), set()).add(tuple(enemy["intent"]))

    report = {}
    for name, intents in seen.items():
        declared = enemy_moves.moves_for_live_name(name)
        report[name] = {
            "seen": len(intents),
            "declared": len(declared) if declared is not None else None,
        }
    return report


def capture_one(
    base_url: str,
    seed: str,
    encounter: str,
    ascension: int,
    turns: int = 0,
    play: bool = False,
    add_cards: list[str] | None = None,
    buff_max_hp: int = 0,
) -> dict[str, Any]:
    live_encounter = validate.LIVE_ENCOUNTER_BY_EMULATOR.get(encounter)
    if live_encounter is None:
        raise RuntimeError(f"No live encounter mapped for {encounter!r}")

    capture_sweep.abandon_any_run(base_url)
    start_real_game_run.start_seeded_run(
        base_url,
        seed,
        "IRONCLAD",
        abandon_existing=False,
        ascension=ascension,
    )
    validate.jump_to_encounter(base_url, live_encounter)

    live_state = start_real_game_run.get_state(base_url)
    live_summary = trace_real_game.summarize_state(live_state)

    # One env, kept open: the opening comparison and the turn-by-turn one have to come
    # from the same combat, or the emulator would silently restart between turns.
    env = Sts2CombatEnv(
        seed=game_seed(seed),
        encounter=encounter,
        completed_combat_rooms=validate.emulator_completed_combat_rooms(encounter),
        total_floor=validate.NEOW_JUMP_TOTAL_FLOOR,
        ascension=ascension,
    )
    try:
        obs, _info = env.reset()
        emu_summary = validate.emulator_trace.summarize_observation(obs)
        turn_rows, turn_notes = (
            drive_turns(base_url, env, turns, play, add_cards, buff_max_hp)
            if turns
            else ([], [])
        )
    finally:
        env.close()

    deck_ok, deck_note = compare_deck(seed, encounter, live_state, ascension)
    hp_ok, intent_ok, enemy_note = compare_enemies(live_summary, emu_summary)
    live_player = live_summary.get("player") or {}
    emu_player = emu_summary.get("player") or {}
    player_ok = (live_player.get("hp"), live_player.get("max_hp")) == (
        emu_player.get("hp"),
        emu_player.get("max_hp"),
    )

    sections = {
        "deck": deck_ok,
        "enemies": hp_ok,
        "intent": intent_ok,
        "player": player_ok,
    }
    if turns:
        sections["turns"] = all(
            row["intents_match"] and row["player_match"] and row["hand_match"]
            for row in turn_rows
        )

    coverage = coverage_for(turn_rows, live_summary) if turns else {}
    missing = [
        f"{name} {c['seen']}/{c['declared']}"
        for name, c in coverage.items()
        if c["declared"] and c["seen"] < c["declared"]
    ]
    if turns:
        sections["coverage"] = not missing

    notes = [n for n in (deck_note, enemy_note) if n]
    notes += turn_notes
    if missing:
        notes.append("intents never seen: " + ", ".join(missing))
    if validate.UNMAPPED_INTENT_TYPES:
        notes.append(
            "live intent types the harness does not map: "
            + ", ".join(sorted(validate.UNMAPPED_INTENT_TYPES)),
        )
    for row in turn_rows:
        if not row["intents_match"]:
            notes.append(
                f"turn {row['turn']} intents: emu {[e['intent'] for e in row['emu_enemies']]} "
                f"vs live {[e['intent'] for e in row['live_enemies']]}",
            )
        if not row["player_match"]:
            notes.append(
                f"turn {row['turn']} player hp: emu {row['emu_player_hp']} "
                f"vs live {row['live_player_hp']}",
            )
        if not row["hand_match"]:
            notes.append(
                f"turn {row['turn']} hand: emu {row['emu_hand']} "
                f"vs live {row['live_hand']}",
            )

    return {
        "seed": seed,
        "encounter": encounter,
        "live_encounter": live_encounter,
        "sections": sections,
        "coverage": coverage,
        "turns": turn_rows,
        "notes": "; ".join(notes),
        "live_state": live_state,
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--base-url", default=capture_sweep.DEFAULT_BASE_URL)
    parser.add_argument("--seeds", nargs="*", default=None)
    parser.add_argument("--count", type=int, default=2, help="random seeds to use")
    parser.add_argument(
        "--encounters",
        nargs="*",
        default=None,
        help=f"default: {' '.join(DEFAULT_ENCOUNTERS)}",
    )
    parser.add_argument("--act", choices=sorted(ENCOUNTERS_BY_ACT), default=None)
    parser.add_argument(
        "--pool",
        choices=["weak", "normal", "both"],
        default="both",
        help="which encounter pool to sweep (default: both)",
    )
    parser.add_argument("--ascension", type=int, default=8)
    parser.add_argument(
        "--turns",
        type=int,
        default=0,
        help="end this many turns per fight and compare intents each turn; enough turns "
        "walk every enemy through its whole move table (0 = opening state only)",
    )
    parser.add_argument(
        "--play",
        action="store_true",
        help=(
            "play the first playable card each turn instead of passing. A capture that "
            "fights back survives long enough to reach the far end of a move table, "
            "which is the only way the coverage-only encounters can be closed."
        ),
    )
    parser.add_argument(
        "--buff-max-hp",
        type=int,
        default=0,
        help=(
            "raise the player's max HP (and heal by it) on BOTH sides before turn one. "
            "A boss that kills a starter deck before its move table runs out leaves a "
            "capture that cannot satisfy the coverage check"
        ),
    )
    parser.add_argument(
        "--add-card",
        action="append",
        default=[],
        metavar="ENTRY[:u]",
        help=(
            "put this card on top of BOTH hands before turn one, by model entry "
            "(DEVASTATE, DEVASTATE:u). Repeatable. How a capture reaches a state the "
            "starter deck cannot, such as a Phrog Parasite dead early enough for its "
            "Wrigglers to spawn."
        ),
    )
    parser.add_argument("--random-seed", type=int, default=0)
    parser.add_argument(
        "--save-fixtures",
        action="store_true",
        help="write each capture to tests/fixtures/combat/<SEED>-<encounter>.json",
    )
    args = parser.parse_args()

    by_act = {"weak": WEAK_BY_ACT, "normal": NORMAL_BY_ACT, "both": ENCOUNTERS_BY_ACT}[
        args.pool
    ]
    encounters = args.encounters or (
        by_act[args.act] if args.act else [e for acts in by_act.values() for e in acts]
    )
    seeds = args.seeds or capture_sweep.pick_seeds(
        args.count,
        None,
        random.Random(args.random_seed),  # noqa: S311 - picking test seeds, not crypto
    )

    print(f"game       : {game_version.describe(game_version.detect())}")
    print(f"seeds      : {' '.join(seeds)}")
    print(f"encounters : {' '.join(encounters)}")
    capture_sweep.ensure_game(args.base_url)

    fixtures = Path(__file__).parent.parent / "tests/fixtures/combat"
    results: list[dict[str, Any]] = []
    jobs = [(seed, enc) for seed in seeds for enc in encounters]
    for index, (seed, encounter) in enumerate(jobs, start=1):
        print(f"\n[{index}/{len(jobs)}] {seed} -> {encounter}", flush=True)
        try:
            result = capture_one(
                args.base_url,
                seed,
                encounter,
                args.ascension,
                turns=args.turns,
                play=args.play,
                add_cards=args.add_card,
                buff_max_hp=args.buff_max_hp,
            )
        except Exception as exc:  # noqa: BLE001 - one bad job must not end the sweep
            print(f"  CAPTURE FAILED: {exc}", flush=True)
            results.append({"seed": seed, "encounter": encounter, "error": str(exc)})
            capture_sweep.recover_to_menu(args.base_url)
            continue

        marks = " ".join(
            f"{name}:{'ok' if ok else 'FAIL'}"
            for name, ok in result["sections"].items()
        )
        print(f"  {marks}", flush=True)
        if result["notes"]:
            print(f"  {result['notes']}", flush=True)

        if args.save_fixtures:
            # Ascension goes in the name: the same seed and encounter at A8 and A10 are
            # different fights (every DeadlyEnemies value flips), and both are worth
            # pinning — A8 is what most captures use, A10 is the only thing that
            # exercises the other branch of every Ascension.Value pair.
            # A capture that fights back is a different fight from a passive one and
            # both are worth keeping: the passive one walks the enemy's move table, the
            # playing one reaches what only happens when the player is winning.
            variant = "-play" if args.play else ""
            path = fixtures / f"{seed}-{encounter}-a{args.ascension}{variant}.json"
            path.parent.mkdir(parents=True, exist_ok=True)
            # The live state verbatim (it is already the shape compare_draw_pile and
            # trace_real_game read), plus the inputs needed to rebuild the emulator side
            # offline. Recording them beats re-deriving from the filename: the floor and
            # the weak/normal context are what make an encounter reproducible at all.
            stamped = {
                **result["live_state"],
                "game": game_version.detect(),
                "capture": {
                    "seed": seed,
                    "encounter": encounter,
                    "live_encounter": result["live_encounter"],
                    "completed_combat_rooms": validate.emulator_completed_combat_rooms(
                        encounter,
                    ),
                    "total_floor": validate.NEOW_JUMP_TOTAL_FLOOR,
                    "ascension": args.ascension,
                    "turns": args.turns,
                    "play": args.play,
                    # The cards stacked on top of the hand before turn one. Recorded so
                    # the offline replay can put the same ones in the same slots; without
                    # them the fight it replays is a different fight.
                    "add_cards": list(args.add_card),
                    # Max HP the capture granted before turn one, for the same reason:
                    # a buffed player fights longer, and replaying the trace against an
                    # unbuffed one replays a fight that ends early.
                    "buff_max_hp": args.buff_max_hp,
                },
                # The turn-by-turn live readout, when turns were driven: enough to
                # replay the fight offline and check every intent an enemy showed, not
                # just its first. `coverage` records how much of each enemy's declared
                # move table those turns actually reached.
                "turn_trace": [
                    {
                        "turn": row["turn"],
                        "action": row["action"],
                        # Every action the turn took, in order, ending with end turn.
                        # `action` alone describes only a turn that played nothing.
                        "actions": row["actions"],
                        "player_hp": row["live_player_hp"],
                        "player_max_hp": row["live_player_max_hp"],
                        "enemies": row["live_enemies"],
                        # The hand IN ORDER, by model slug — what pins the mid-combat
                        # reshuffle offline. Pile counts can agree every turn while the
                        # order coming off the top is wrong.
                        "live_hand": row["live_hand"],
                    }
                    for row in result["turns"]
                ],
                "coverage": result["coverage"],
            }
            path.write_text(json.dumps(stamped, indent=2) + "\n")
            print(f"  wrote {path}", flush=True)
        results.append(result)

    print("\n" + "=" * 60)
    failed = []
    for result in results:
        label = f"{result['seed']:12} {result['encounter']:20}"
        if "error" in result:
            failed.append(result)
            print(f"  {label} ERROR  {result['error']}")
            continue
        bad = [name for name, ok in result["sections"].items() if not ok]
        if bad:
            failed.append(result)
        print(f"  {label} {'ALL MATCH' if not bad else 'FAIL: ' + ', '.join(bad)}")
    print(f"\n  {len(results) - len(failed)}/{len(results)} captures match everywhere")
    raise SystemExit(1 if failed else 0)


if __name__ == "__main__":
    main()
