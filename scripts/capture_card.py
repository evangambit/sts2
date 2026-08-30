#!/usr/bin/env python3
r"""Capture what the *game* does when one card is played, as a committed fixture.

Card expectations in the C# suite are read off `decompiled\\`, which is the game's real
shipped logic but not the game running it. That is a decent source for "10 damage, +4
upgraded" and a poor one for the things cards actually get wrong: effect ordering,
rounding, splash and overkill, what a power sees when a target dies mid-effect. This
script closes that gap by staging one card in a live combat, playing it, and committing
the before/after the game reported.

    python scripts/capture_card.py --card MoltenFist
    python scripts/capture_card.py --card MoltenFist --upgraded --encounter Chompers
    python scripts/capture_card.py --card Cleave --energy 3 --target 1

The fixture is self-contained: it records the state the card was played into as well as
the state it produced, so `scripts/generate_card_capture_tests.py` can rebuild that exact
situation in the emulator rather than having to reproduce a whole run. Expectations
therefore come from the game and survive a re-capture, the same property
`scripts/generate_capture_tests.py` documents for run generation.

Needs the game running with STS2MCP (any OS -- see AGENTS.md), and a mod new enough to
have `debug_add_card` / `debug_set_energy`.
"""

from __future__ import annotations

import argparse
import importlib.util
import json
import sys
import time
from pathlib import Path
from types import ModuleType
from typing import Any

REPO = Path(__file__).resolve().parent.parent

# The piles a capture records. The game also has a PlayPile, which the mod does not
# report and which a card sits in while it resolves -- see wait_for_play_to_settle.
PILE_KEYS = (
    "hand_ordered",
    "draw_pile_ordered",
    "discard_pile_ordered",
    "exhaust_pile_ordered",
)
sys.path.insert(0, str(REPO / "scripts"))

import start_real_game_run  # noqa: E402
import trace_real_game  # noqa: E402

FIXTURES = REPO / "tests" / "fixtures" / "cards"
DEFAULT_ENCOUNTER = "CorpseSlugsWeak"
DEFAULT_SEED = "ABCDEF"


def _load(name: str) -> ModuleType:
    spec = importlib.util.spec_from_file_location(name, REPO / "scripts" / f"{name}.py")
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Could not load {name}.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


capture_sweep = _load("capture_sweep")
validate = _load("validate_real_game_trace")


def game_version() -> dict[str, Any]:
    path = REPO / "data" / "game_version.json"
    return json.loads(path.read_text(encoding="utf-8")) if path.exists() else {}


def find_in_hand(state: dict[str, Any], card: str, upgraded: bool) -> int | None:
    """Hand index of the staged card, matching the mod's id or its display name."""
    wanted = card.replace("_", "").casefold()
    for entry in (state.get("player") or {}).get("hand") or []:
        ids = (str(entry.get("id") or ""), str(entry.get("name") or ""))
        matches = any(
            i.replace("_", "").replace(" ", "").casefold() == wanted for i in ids
        )
        if matches and bool(entry.get("is_upgraded")) == upgraded:
            return int(entry["index"])
    return None


def stage_card(
    base_url: str,
    card: str,
    upgraded: bool,
    energy: int,
    stars: int | None = None,
    timeout: float = 15.0,
    enchantment: str | None = None,
    enchant_amount: float = 1.0,
) -> tuple[dict[str, Any], int]:
    """Put the card in hand with enough energy to play it, and return (state, index).

    Both actions are fire-and-forget on the mod side -- the game resolves them on its
    own loop -- so this polls for the card rather than trusting the acknowledgement.

    ``enchantment`` is the reason this script exists in its current form. What
    ``decompiled/`` states plainly is that an enchantment is a damage modifier; what it
    does NOT settle is whether one reaches a card whose damage is a CALCULATION rather
    than a printed number, and whether a multi-hit attack pays the bonus once or per
    hit. Seventeen cards in the emulator depend on the answer. Staging a Sharp Body Slam
    and reading what the game reports is the only way to know.

    Raises:
        RuntimeError: if the mod rejects any action, or the card never appears
            (most often an STS2MCP build predating ``debug_add_card`` /
            ``debug_enchant_card``).

    """
    result = trace_real_game.post_action(
        base_url,
        {
            "action": "debug_add_card",
            "card": card,
            "upgraded": upgraded,
            "pile": "hand",
        },
    )
    if result.get("status") != "ok":
        raise RuntimeError(f"debug_add_card failed: {result}")

    result = trace_real_game.post_action(
        base_url,
        {"action": "debug_set_energy", "amount": energy},
    )
    if result.get("status") != "ok":
        raise RuntimeError(f"debug_set_energy failed: {result}")

    # Stars gate a play exactly as energy does, so a card costing more than Divine Right's
    # opening three cannot be staged without setting them -- Seven Stars wants seven. Left
    # alone by default, because for every other character the counter is not part of the
    # board and a capture should not invent one.
    if stars is not None:
        result = trace_real_game.post_action(
            base_url,
            {"action": "debug_set_stars", "amount": stars},
        )
        if result.get("status") != "ok":
            raise RuntimeError(f"debug_set_stars failed: {result}")

    if enchantment is not None:
        # debug_add_card puts the card on TOP of the hand, so index 0 is the one just
        # staged. The mod refuses rather than throws when the enchantment does not fit
        # the card, so a bad pairing surfaces here instead of taking the game down.
        result = trace_real_game.post_action(
            base_url,
            {
                "action": "debug_enchant_card",
                "enchantment": enchantment,
                "pile": "hand",
                "index": 0,
                "amount": enchant_amount,
            },
        )
        if result.get("status") != "ok":
            raise RuntimeError(f"debug_enchant_card failed: {result}")

    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        state = start_real_game_run.get_state(base_url)
        index = find_in_hand(state, card, upgraded)
        if index is not None and (state.get("player") or {}).get("energy") == energy:
            return state, index
        time.sleep(0.25)

    raise RuntimeError(
        f"{card} (upgraded={upgraded}) never reached hand with {energy} energy. "
        "An older STS2MCP build has no debug_add_card; rebuild and redeploy the mod.",
    )


def wait_for_menu_options(base_url: str, timeout: float = 30.0) -> None:
    """Wait until a menu actually advertises its options.

    Straight after the game launches the main menu reports an empty option list for a
    beat. ``abandon_any_run`` reads that list once and returns early when it lacks
    ``abandon_run``, so embarking then fails with "no singleplayer option" — the run it
    meant to clear is still there.

    Raises:
        RuntimeError: if the menu never populates.

    """
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        state = start_real_game_run.get_state(base_url)
        if state.get("state_type") != "menu" or state.get("options"):
            return
        time.sleep(0.5)

    raise RuntimeError(f"Menu never advertised any options within {timeout:.0f}s")


def apply_powers(
    base_url: str,
    powers: list[str],
    state: dict[str, Any],
) -> None:
    """Stage ``POWER=AMOUNT[@target]`` entries before the card is played.

    Target defaults to the first living enemy; ``@player`` targets the player and
    ``@1`` the second living enemy. This is what makes the conditional half of a card
    reachable -- Molten Fist only reapplies Vulnerable to an already-Vulnerable target.

    Raises:
        RuntimeError: on a malformed spec or a rejected application.

    """
    enemies = [
        e
        for e in (state.get("battle") or {}).get("enemies") or []
        if (e.get("hp") or 0) > 0
    ]
    for spec in powers:
        name, _, amount_and_target = spec.partition("=")
        amount_text, _, target_text = amount_and_target.partition("@")
        if not name or not amount_text.strip().lstrip("-").isdigit():
            raise RuntimeError(
                f"Malformed --power {spec!r}; expected POWER=AMOUNT[@target]",
            )

        target = target_text or "0"
        if target.isdigit():
            if int(target) >= len(enemies):
                raise RuntimeError(
                    f"--power {spec!r} targets enemy {target} but only {len(enemies)} are alive",
                )
            target = str(enemies[int(target)].get("entity_id"))

        result = trace_real_game.post_action(
            base_url,
            {
                "action": "debug_add_power",
                "power": name,
                "amount": int(amount_text),
                "target": target,
            },
        )
        if result.get("status") != "ok":
            raise RuntimeError(f"debug_add_power failed for {spec!r}: {result}")


def assert_playable(state: dict[str, Any], index: int, card: str) -> None:
    """Refuse to capture a card the game will not let us play.

    A rejected play still returns a state, and its before/after would be identical --
    a fixture that asserts the card does nothing, which is worse than no fixture.

    Raises:
        RuntimeError: if the card left hand, or the game reports it as unplayable.

    """
    hand = (state.get("player") or {}).get("hand") or []
    entry = next((c for c in hand if c.get("index") == index), None)
    if entry is None:
        raise RuntimeError(f"{card} vanished from hand before it could be played")
    if entry.get("can_play") is False:
        raise RuntimeError(
            f"Game says {card} is unplayable here: {entry.get('unplayable_reason')}",
        )


def play_card(
    base_url: str,
    index: int,
    target_index: int,
    state: dict[str, Any],
) -> dict[str, Any]:
    payload: dict[str, Any] = {"action": "play_card", "card_index": index}
    enemies = (state.get("battle") or {}).get("enemies") or []
    living = [e for e in enemies if (e.get("hp") or 0) > 0]
    if living:
        target = living[min(target_index, len(living) - 1)]
        payload["target"] = target.get("entity_id")

    # A Power card is CONSUMED by its own play -- it becomes a power on the creature and
    # lands in no pile at all -- so the pile count it leaves behind is one short for good.
    hand = (state.get("player") or {}).get("hand") or []
    played = next((c for c in hand if c.get("index") == index), None)
    consumed = (played or {}).get("type") == "Power"

    result = trace_real_game.post_action(base_url, payload)
    if result.get("status") != "ok":
        raise RuntimeError(f"play_card failed: {result}")

    return wait_for_play_to_settle(
        base_url,
        card_count_before=_card_count(state),
        consumed=consumed,
    )


def _settle_key(state: dict[str, Any]) -> str:
    """What has to stop moving before a consumed card counts as settled.

    Everything a capture asserts on, so a state that matches its predecessor here is one
    the fixture can be written from.
    """
    player = state.get("player") or {}
    return json.dumps(
        {
            "player": {
                k: player.get(k)
                for k in (
                    "hp",
                    "block",
                    "energy",
                    "draw_pile_count",
                    "discard_pile_count",
                    "exhaust_pile_count",
                    "status",
                )
            },
            "hand": [c.get("id") for c in (player.get("hand") or [])],
            "enemies": [
                (e.get("hp"), e.get("block"), e.get("status"))
                for e in (state.get("enemies") or [])
            ],
            "allies": [
                (a.get("hp"), a.get("max_hp"), a.get("status"))
                for a in (state.get("allies") or [])
            ],
        },
        sort_keys=True,
    )


def _card_count(state: dict[str, Any]) -> int:
    """Cards in every combat pile, counted from the SUMMARY rather than the ordered lists.

    The ordered lists are a fork addition and a pile can be missing from them -- the
    exhaust one was, for a while, so a Second Wind that exhausted four cards looked like
    four cards vanishing and the settle wait never finished. The summary counts come from
    the game's own piles and are the thing to trust for "has everything landed yet".
    """
    player = state.get("player") or {}
    return (
        len(player.get("hand") or [])
        + int(player.get("draw_pile_count") or 0)
        + int(player.get("discard_pile_count") or 0)
        + int(player.get("exhaust_pile_count") or 0)
    )


def wait_for_play_to_settle(
    base_url: str,
    card_count_before: int,
    consumed: bool = False,
    timeout: float = 30.0,
) -> dict[str, Any]:
    """Wait until the played card has LEFT the play pile, then snapshot.

    A fixed settle delay is not enough for a card that resolves slowly. Sword Boomerang
    throws three separately-targeted hits, and a state read after the damage landed but
    before the card finished moving showed it in no pile at all -- not hand, not discard,
    not exhaust -- because it was still in the PlayPile, which the mod does not report.
    The generated test then asserted an empty discard and failed against an emulator that
    was right.

    The card played leaves the recorded piles one short until it arrives somewhere, so
    waiting for the count to come back is waiting for the play to finish.

    The timeout is generous because the slowest cards are exactly the ones that need
    this: Second Wind exhausts a whole hand one card at a time, and ten seconds was not
    enough for it. A refusal here is safe -- it declines to write a fixture rather than
    writing a wrong one -- but a refusal on a card that would have settled is a nuisance.
    """
    target = card_count_before - 1 if consumed else card_count_before
    deadline = time.monotonic() + timeout
    latest = trace_real_game.wait_for_state(base_url, 0.5)
    while time.monotonic() < deadline:
        if _card_count(latest) >= target:
            # The count coming back is necessary and not sufficient, for two reasons that
            # meet here. A CONSUMED card's count returns the instant it leaves hand, before
            # its effect has resolved. And a card that GENERATES another can have the
            # generated card restore the count while the played one is still in the Play
            # pile -- Collision Course made a Debris and was snapshotted in no pile at all,
            # which generated a test asserting an empty discard against an emulator that
            # was right. Waiting for the board to stop MOVING answers both.
            time.sleep(0.5)
            again = start_real_game_run.get_state(base_url)
            if _card_count(again) >= target and _settle_key(again) == _settle_key(
                latest
            ):
                return again
            latest = again
            continue
        time.sleep(0.25)
        latest = start_real_game_run.get_state(base_url)

    raise RuntimeError(
        "the played card never reached a recorded pile -- it is probably still resolving. "
        "Capturing here would record a state with the card in no pile at all.",
    )


def capture(
    base_url: str,
    card: str,
    upgraded: bool,
    encounter: str,
    seed: str,
    ascension: int,
    energy: int,
    stars: int | None,
    target_index: int,
    reuse_run: bool,
    powers: list[str],
    enchantment: str | None = None,
    enchant_amount: float = 1.0,
    character: str = "IRONCLAD",
) -> dict[str, Any]:
    if not reuse_run:
        wait_for_menu_options(base_url)
        capture_sweep.abandon_any_run(base_url)
        start_real_game_run.start_seeded_run(
            base_url,
            seed,
            character,
            abandon_existing=False,
            ascension=ascension,
        )
    else:
        # `--reuse-run` reuses whatever run is in progress, and that run has a CHARACTER.
        # Reusing a Regent's run to capture a Defect card produced four fixtures whose
        # filename said defect and whose player said Regent -- with no orb queue, which is
        # the whole of what a Defect card needs. Refuse rather than record it.
        running = ((start_real_game_run.get_state(base_url).get("player") or {})).get(
            "character"
        )
        wanted = character.replace("_", " ").lower()
        if running is not None and running.removeprefix("The ").lower() != wanted:
            raise RuntimeError(
                f"--reuse-run wants a {character} run and the one in progress is "
                f"{running!r}. Drop --reuse-run to start a fresh one.",
            )

    validate.jump_to_encounter(base_url, encounter)

    if powers:
        apply_powers(base_url, powers, start_real_game_run.get_state(base_url))
        trace_real_game.wait_for_state(base_url, 0.5)

    before_state, index = stage_card(
        base_url,
        card,
        upgraded,
        energy,
        stars=stars,
        enchantment=enchantment,
        enchant_amount=enchant_amount,
    )
    assert_playable(before_state, index, card)
    after_state = play_card(base_url, index, target_index, before_state)

    before = trace_real_game.summarize_state(before_state)
    after = trace_real_game.summarize_state(after_state)
    if before == after:
        raise RuntimeError(
            f"Playing {card} changed nothing the state exposes; refusing to commit a "
            "fixture that would assert the card is a no-op.",
        )

    return {
        "_comment": (
            "Captured from the live game by scripts/capture_card.py. Expected values "
            "here are the GAME's, never the emulator's; re-capturing re-reads ground "
            "truth and cannot rubber-stamp an emulator regression."
        ),
        "card": card,
        "upgraded": upgraded,
        "encounter": encounter,
        "seed": seed,
        "ascension": ascension,
        "target_index": target_index,
        # The mod adds to the top of the pile, but recording the index the card was
        # actually played from keeps the generated test correct if that ever changes.
        "hand_index": index,
        "energy": energy,
        "powers": powers,
        # The enchantment belongs in the FIXTURE, not just the filename. The first Sharp
        # capture recorded it in the name alone, which would have generated a test that
        # rebuilt a plain card and expected the enchanted number -- a fixture that fails
        # for a reason nothing in it explains.
        "enchantment": enchantment,
        "enchant_amount": enchant_amount if enchantment else None,
        "game": game_version(),
        "before": before,
        "after": after,
        # Ordered piles are not in the summary and some cards (draw, retain, put-on-top)
        # are only checkable against them.
        "before_piles": ordered_piles(before_state),
        "after_piles": ordered_piles(after_state),
    }


def ordered_piles(state: dict[str, Any]) -> dict[str, Any]:
    player = state.get("player") or {}
    return {
        name: player.get(name) for name in PILE_KEYS if player.get(name) is not None
    }


def default_out(
    card: str,
    upgraded: bool,
    encounter: str,
    powers: list[str],
    enchantment: str | None = None,
    character: str = "IRONCLAD",
) -> Path:
    suffix = "-upgraded" if upgraded else ""
    # Staged powers change what the capture proves, so they belong in the filename --
    # otherwise a Vulnerable capture silently overwrites the plain one. An enchantment
    # is the same kind of claim and gets the same treatment.
    staged = (
        "-" + "-".join(p.split("=")[0].removesuffix("_POWER").lower() for p in powers)
        if powers
        else ""
    )
    enchanted = f"-{enchantment.lower()}" if enchantment else ""
    # The character is part of what a capture proves: the same card played by a
    # Necrobinder has Osty on the board and by an Ironclad does not.
    who = "" if character.upper() == "IRONCLAD" else f"-{character.lower()}"
    return FIXTURES / f"{card}{suffix}{staged}{enchanted}{who}-{encounter}.json"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--card",
        required=True,
        help="card entry id or class name, e.g. MoltenFist",
    )
    parser.add_argument("--upgraded", action="store_true")
    parser.add_argument(
        "--enchantment",
        help="enchant the staged card first, e.g. Sharp -- needs a mod with debug_enchant_card",
    )
    parser.add_argument(
        "--enchant-amount",
        type=float,
        default=1.0,
        help="amount for --enchantment (ignored by enchantments that read their own vars)",
    )
    parser.add_argument("--encounter", default=DEFAULT_ENCOUNTER)
    parser.add_argument(
        "--character",
        default="IRONCLAD",
        help=(
            "character to start the run as. A card belonging to another character can be "
            "staged into an Ironclad run with debug_add_card, but it will not have that "
            "character's CONTEXT -- a Necrobinder card that reads Osty needs a Necrobinder "
            "run, because Osty comes from the starter relic."
        ),
    )
    parser.add_argument("--seed", default=DEFAULT_SEED)
    parser.add_argument("--ascension", type=int, default=8)
    parser.add_argument(
        "--energy",
        type=int,
        default=9,
        help="energy to set before playing, so cost never decides the capture",
    )
    parser.add_argument(
        "--stars",
        type=int,
        default=None,
        help=(
            "stars to set before playing, for a Regent card whose STAR cost is more than "
            "the three Divine Right opens with. Left alone by default"
        ),
    )
    parser.add_argument(
        "--target",
        type=int,
        default=0,
        help="index among living enemies",
    )
    parser.add_argument(
        "--reuse-run",
        action="store_true",
        help="skip embarking and use the run already in progress",
    )
    parser.add_argument(
        "--power",
        action="append",
        default=[],
        metavar="POWER=AMOUNT[@target]",
        help="stage a power first, e.g. VULNERABLE_POWER=2 or STRENGTH_POWER=3@player",
    )
    parser.add_argument("--out", type=Path)
    parser.add_argument("--base-url", default=trace_real_game.DEFAULT_BASE_URL)
    args = parser.parse_args()

    fixture = capture(
        args.base_url,
        args.card,
        args.upgraded,
        args.encounter,
        args.seed,
        args.ascension,
        args.energy,
        args.stars,
        args.target,
        args.reuse_run,
        args.power,
        enchantment=args.enchantment,
        enchant_amount=args.enchant_amount,
        character=args.character,
    )

    out = args.out or default_out(
        args.card,
        args.upgraded,
        args.encounter,
        args.power,
        args.enchantment,
        args.character,
    )
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(fixture, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {out.relative_to(REPO)}")
    print("Now run: python scripts/generate_card_capture_tests.py")


if __name__ == "__main__":
    main()
