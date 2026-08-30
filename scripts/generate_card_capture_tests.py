#!/usr/bin/env python3
r"""Turn live card captures into C# tests.

Same contract as ``scripts/generate_capture_tests.py``: every expected value comes from
the game, so re-capturing a fixture and regenerating propagates fresh ground truth into
the suite without ever letting the emulator grade its own homework. What this adds is
per-card granularity -- ``capture_card.py`` stages one card, plays it, and records the
state on both sides of that play, which is the one thing ``decompiled\\`` cannot show:
what the effect actually did, in order, at runtime.

    python scripts/capture_card.py --card MoltenFist
    python scripts/generate_card_capture_tests.py

Generation is deliberately brittle. A fixture holding anything the emulator cannot
express -- a relic, an unmapped power, a card missing from the id map -- fails the run
with the reason rather than quietly emitting a weaker test, because a capture that
silently drops the interesting half is worse than no capture.
"""

from __future__ import annotations

import json
import re
from pathlib import Path
from typing import Any

REPO = Path(__file__).resolve().parent.parent
FIXTURES = REPO / "tests" / "fixtures" / "cards"
OUT = REPO / "src" / "Sts2Emulator.Tests" / "Cards" / "CardCaptures.g.cs"
ID_MAP = REPO / "data" / "id_map.json"
ID_CLASSES = REPO / "data" / "card_id_classes.json"
BUFF_STATE = REPO / "src" / "Sts2Emulator" / "Core" / "BuffState.cs"

# Relics change what a card does, and the emulator applies them from its own relic list
# rather than from the fixture, so a capture taken with any relic in play cannot be
# rebuilt faithfully. The starter relic is inert during a card play.
ALLOWED_RELICS = {"BURNING_BLOOD"}


class UnsupportedCaptureError(Exception):
    """A fixture the generator refuses to turn into a test, with the reason."""


def normalize(name: str) -> str:
    return name.replace("_", "").replace(" ", "").casefold()


def card_constants() -> dict[str, str]:
    """Emulator card name -> the C# constant that names it, e.g. ``IC.MoltenFist``."""
    classes = json.loads(ID_CLASSES.read_text(encoding="utf-8"))["classes"]
    return {
        normalize(card): f"{cls}.{card}"
        for cls, cards in classes.items()
        for card in cards
    }


def card_ids() -> dict[str, int]:
    cards = json.loads(ID_MAP.read_text(encoding="utf-8"))["cards"]
    return {normalize(name): id_ for name, id_ in cards.items()}


def enemy_ids() -> dict[str, int]:
    enemies = json.loads(ID_MAP.read_text(encoding="utf-8"))["enemies"]
    return {normalize(name): id_ for name, id_ in enemies.items()}


def enemy_def_id(entity_id: str, enemies: dict[str, int]) -> int:
    """``CORPSE_SLUG_1`` -> the emulator's def id for CorpseSlug.

    Substituting a stand-in enemy would be quietly wrong: def id selects the enemy's
    own hooks, so a capture rebuilt against the wrong monster can pass while the
    emulator models the real one incorrectly.

    Raises:
        UnsupportedCaptureError: if the enemy is absent from ``data/id_map.json``.

    """
    base = re.sub(r"_\d+$", "", entity_id)
    key = normalize(base)
    if key not in enemies:
        raise UnsupportedCaptureError(f"enemy {entity_id!r} is not in data/id_map.json")
    return enemies[key]


def buff_ids() -> set[str]:
    """Read the BuffId enum's member names from the enum itself, so they cannot drift."""
    source = BUFF_STATE.read_text(encoding="utf-8")
    body = source.split("public enum BuffId", 1)[1].split("}", 1)[0]
    return set(re.findall(r"^\s*(\w+),", body, re.MULTILINE))


# Powers whose reported amount is a `DisplayAmount` override rather than `Amount`, so the
# number in the capture is not the number the name suggests.
#
# `OutbreakPower.DisplayAmount => timesPoisoned` -- the mod reports the progress towards
# the next burst, which is 0 the moment the card is played, while the power's own Amount is
# the 11 damage it will deal. The emulator keeps those two apart as `Outbreak` and
# `OutbreakCounter`, and it is the COUNTER the capture is looking at.
DISPLAY_AMOUNT_BUFFS = {"OUTBREAK_POWER": "OutbreakCounter"}

# Powers the emulator names the other way round. Only reorderings and rewordings of the
# same concept belong here -- a live power with no emulator equivalent must still refuse,
# because that is how a missing power gets noticed at all.
BUFF_ALIASES = {
    "ENERGY_NEXT_TURN_POWER": "NextTurnEnergy",
    # `EnfeeblingTouchPower : TemporaryStrengthPower` with `IsPositive => false` -- a
    # subclass that adds nothing but a sign, and the emulator models it as the base power.
    "ENFEEBLING_TOUCH_POWER": "TemporaryStrength",
}


def buff_constant(live_id: str, buffs: set[str]) -> str:
    """Map a live power entry id (``VULNERABLE_POWER``) onto ``BuffId.Vulnerable``.

    Raises:
        UnsupportedCaptureError: if no BuffId corresponds to the live power.

    """
    for table in (DISPLAY_AMOUNT_BUFFS, BUFF_ALIASES):
        if (override := table.get(live_id)) is not None:
            if override not in buffs:
                raise UnsupportedCaptureError(
                    f"BuffId.{override} is gone; check {live_id}"
                )
            return f"BuffId.{override}"

    wanted = normalize(live_id)
    by_norm = {normalize(name): name for name in buffs}
    for candidate in (wanted, wanted.removesuffix("power")):
        if candidate in by_norm:
            return f"BuffId.{by_norm[candidate]}"
    raise UnsupportedCaptureError(f"no BuffId for live power {live_id!r}")


def card_literal(
    entry: dict[str, Any],
    constants: dict[str, str],
    ids: dict[str, int],
) -> str:
    key = normalize(str(entry.get("id") or ""))
    if key in constants:
        name = constants[key]
    elif key in ids:
        name = str(ids[key])
    else:
        raise UnsupportedCaptureError(
            f"card {entry.get('id')!r} is not in data/id_map.json",
        )
    return (
        f"Card({name}, upgraded: true)" if entry.get("is_upgraded") else f"Card({name})"
    )


def pile_literal(
    pile: list[dict[str, Any]],
    constants: dict[str, str],
    ids: dict[str, int],
    enchant_index: int | None = None,
    enchantment: str | None = None,
    enchant_amount: float | None = None,
) -> str:
    """Render a pile, decorating one card with a staged enchantment if there was one.

    The mod's pile readout does NOT report enchantments -- a Sharp Sword Boomerang and a
    plain one read identically -- so the enchantment cannot be recovered from the pile.
    It comes from the fixture's own record of what was staged, applied at the index the
    card was staged into. Anything that enchanted a card by other means would be
    invisible here, which is why `check_supported` refuses a fixture claiming an
    enchantment it cannot place.
    """
    out = []
    for i, entry in enumerate(pile):
        literal = card_literal(entry, constants, ids)
        if enchantment is not None and i == enchant_index:
            amount = int(enchant_amount or 0)
            literal = (
                f"{literal} with {{ Enchantment = Enchantment.{enchantment}, "
                f"EnchantAmount = {amount} }}"
            )
        out.append(literal)
    return ", ".join(out)


def count_assert(expected: int, collection: str) -> str:
    """Assert a pile's size in the form xUnit's analyzers accept (xUnit2013)."""
    if expected == 0:
        return f"        Assert.Empty({collection});"
    if expected == 1:
        return f"        Assert.Single({collection});"
    return f"        Assert.Equal({expected}, {collection}.Count);"


def buff_literal(statuses: list[dict[str, Any]], buffs: set[str]) -> str:
    parts = [
        f"new BuffState({buff_constant(str(s['id']), buffs)}, {int(s['amount'])})"
        for s in statuses
        if s.get("amount") is not None
    ]
    return f"[{', '.join(parts)}]" if parts else ""


def check_supported(fixture: dict[str, Any]) -> None:
    before = fixture["before"]
    relics = [
        r.get("id")
        for r in (before.get("player") or {}).get("relics") or []
        if r.get("id") not in ALLOWED_RELICS
    ]
    if relics:
        raise UnsupportedCaptureError(
            f"capture has relics the rebuild cannot apply: {relics}",
        )
    if "hand_ordered" not in fixture.get("before_piles", {}):
        raise UnsupportedCaptureError(
            "capture predates hand_ordered; re-capture with a current mod",
        )


def test_name(fixture: dict[str, Any]) -> str:
    """Name a capture by what it proves, including any staged powers.

    Card plus upgrade state alone collides the moment the same card is captured twice
    with different setup -- a plain Molten Fist and one against a Vulnerable target are
    different facts about the same card.
    """
    parts = [fixture["card"], "Upgraded" if fixture["upgraded"] else "Base"]
    for spec in fixture.get("powers") or []:
        power = spec.split("=")[0].removesuffix("_POWER")
        parts.append("".join(word.capitalize() for word in power.split("_")))
    if enchantment := fixture.get("enchantment"):
        parts.append(enchantment.capitalize())
    # The encounter is part of what a capture proves too -- the same card against a Corpse
    # Slug and against Byrdonis are different boards, and a card that can kill the one but
    # not the other has to be captured against both. Without this they collide.
    if encounter := fixture.get("encounter"):
        parts.append(encounter)
    # And so is the CHARACTER: the same card played by the Ironclad and by the Necrobinder
    # are different boards, and Prolong has been captured as both. Read it off the state
    # the game reported rather than the capture's own `character` field, which older
    # fixtures do not carry at all.
    who = (fixture["before"].get("player") or {}).get("character")
    if who:
        parts.append(who.removeprefix("The ").replace(" ", ""))
    return "_".join(parts) + "_MatchesLiveCapture"


def render_test(
    fixture: dict[str, Any],
    constants: dict[str, str],
    ids: dict[str, int],
    enemies: dict[str, int],
    buffs: set[str],
) -> str:
    check_supported(fixture)

    before, after = fixture["before"], fixture["after"]
    piles = fixture["before_piles"]
    player, after_player = before["player"], after["player"]

    lines = [
        "    [Fact]",
        f"    public void {test_name(fixture)}()",
        "    {",
        f"        // Captured from the live game ({fixture.get('game', {}).get('release', 'unknown build')}) by",
        f"        // scripts/capture_card.py --card {fixture['card']}"
        + (" --upgraded" if fixture["upgraded"] else "")
        + f" --encounter {fixture['encounter']} --seed {fixture['seed']}.",
        "        // Every number below is the game's, not the emulator's.",
        f"        var fight = Fight.Hand({pile_literal(
            piles['hand_ordered'],
            constants,
            ids,
            enchant_index=fixture.get('hand_index'),
            enchantment=fixture.get('enchantment'),
            enchant_amount=fixture.get('enchant_amount'),
        )})",
        f"            .PlayerHp({player['hp']}, {player['max_hp']})",
        f"            .Energy({player['energy']})",
    ]

    if piles.get("draw_pile_ordered"):
        lines.append(
            f"            .Draw({pile_literal(piles['draw_pile_ordered'], constants, ids)})",
        )
    if piles.get("discard_pile_ordered"):
        lines.append(
            f"            .Discard({pile_literal(piles['discard_pile_ordered'], constants, ids)})",
        )
    if piles.get("exhaust_pile_ordered"):
        lines.append(
            f"            .Exhausted({pile_literal(piles['exhaust_pile_ordered'], constants, ids)})",
        )

    lines.extend(
        f"            .PlayerBuff({buff_constant(str(status['id']), buffs)}, "
        f"{int(status['amount'])})"
        for status in player.get("status") or []
    )

    for enemy in before["enemies"]:
        def_id = enemy_def_id(str(enemy["entity_id"]), enemies)
        args = [f"defId: {def_id}", f"hp: {enemy['hp']}", f"maxHp: {enemy['max_hp']}"]
        if enemy.get("block"):
            args.append(f"block: {enemy['block']}")
        if literal := buff_literal(enemy.get("status") or [], buffs):
            args.append(f"buffs: {literal}")
        lines.append(f"            .Enemy({', '.join(args)})")

    lines[-1] += ";"

    # Osty, when the capture was taken by a Necrobinder. The pet is the character's whole
    # mechanic and the mod reported nothing about it until now, so a capture that has it
    # must rebuild it or the assertions below are checked against an empty board.
    before_allies = before.get("allies") or []
    if len(before_allies) > 1:
        raise UnsupportedCaptureError(
            "more than one ally in the capture; the emulator models Osty alone",
        )
    for ally in before_allies:
        if ally.get("name") != "Osty":
            raise UnsupportedCaptureError(f"unmodelled ally {ally.get('name')!r}")
        lines += [
            f"        fight.State.OstyHp = {ally['hp']};",
            f"        fight.State.OstyMaxHp = {ally['max_hp']};",
        ]

    # Stars, the Regent's resource. The mod reports the field only for a character whose
    # star counter is always shown, or when there are stars to show, so its absence means
    # zero and staging is skipped. Osty's comment applies here word for word: it is the
    # character's whole mechanic, and a capture that does not rebuild it checks the
    # assertions below against a board the game never had.
    if (before_stars := player.get("stars")) is not None:
        lines.append(f"        fight.State.Stars = {before_stars};")

    if "hand_index" not in fixture:
        raise UnsupportedCaptureError(
            "capture predates hand_index; re-capture so the played card is unambiguous",
        )
    if fixture.get("enchantment") and fixture.get("hand_index") is None:
        raise UnsupportedCaptureError(
            "capture claims an enchantment but records no hand_index to put it on",
        )
    hand_index = fixture["hand_index"]
    lines += [
        "",
        f"        fight.Play(index: {hand_index}, target: {fixture['target_index']});",
        "",
        f"        Assert.Equal({after_player['hp']}, fight.State.PlayerHp);",
        f"        Assert.Equal({after_player['block']}, fight.State.PlayerBlock);",
        f"        Assert.Equal({after_player['energy']}, fight.State.Energy);",
        count_assert(after_player["draw_pile_count"], "fight.State.DrawPile"),
        count_assert(after_player["discard_pile_count"], "fight.State.DiscardPile"),
        count_assert(after_player["exhaust_pile_count"], "fight.State.ExhaustPile"),
    ]

    # Asserted whenever EITHER side of the capture reports stars: a card that spends the
    # last one leaves the field out of the after state, and "the key is gone" has to read
    # as zero rather than as nothing to check.
    if player.get("stars") is not None or after_player.get("stars") is not None:
        lines.append(
            f"        Assert.Equal({after_player.get('stars') or 0}, fight.State.Stars);"
        )

    player_powers = [
        buff_constant(str(status["id"]), buffs)
        for status in after_player.get("status") or []
    ]
    # A power the emulator splits in two shows up ONCE in the game's readout, so the
    # natural id has to be allowed alongside the aliased one: Outbreak is the emulator's
    # damage and OutbreakCounter its progress, and the game reports a single OUTBREAK_POWER
    # whose displayed number is the counter.
    by_norm = {name.lower(): name for name in buffs}
    for status in after_player.get("status") or []:
        live_id = str(status["id"])
        if live_id not in DISPLAY_AMOUNT_BUFFS:
            continue
        if (
            natural := by_norm.get(normalize(live_id).removesuffix("power"))
        ) is not None:
            player_powers.append(f"BuffId.{natural}")
    lines.extend(
        f"        Assert.Equal({int(status['amount'])}, "
        f"fight.PlayerBuffAmount({buff_constant(str(status['id']), buffs)}));"
        for status in after_player.get("status") or []
        if status.get("amount") is not None
    )

    # And nothing ELSE. Asserting only the powers the game reported says nothing about the
    # ones it did not, so an emulator that invents a power passes -- which is how Venerate
    # granted Strength and Dexterity for a card that gains stars, through a clean capture.
    lines.append(f"        fight.PlayerPowersAre({', '.join(player_powers)});")

    # A capture where something DIED cannot be rebuilt positionally. The game drops the
    # dead enemy from its readout AND renumbers the survivors -- a Fiend Fire that killed
    # the front Corpse Slug reported the survivor as CORPSE_SLUG_0, the id the corpse had
    # held. The emulator instead leaves the corpse in place so indices stay put, which is
    # the more useful behaviour and the reason the two cannot be lined up after a kill:
    # neither position nor entity id survives it.
    if len(after["enemies"]) != len(before["enemies"]):
        raise UnsupportedCaptureError(
            "an enemy died during the capture; the game renumbers the survivors, so the "
            "after state cannot be matched to the before state. Re-capture against a "
            "board the card cannot kill.",
        )

    for index, enemy in enumerate(after["enemies"]):
        lines.append(
            f"        Assert.Equal({enemy['hp']}, fight.State.Enemies[{index}].Hp);",
        )
        lines.append(
            f"        Assert.Equal({enemy['block']}, fight.State.Enemies[{index}].Block);",
        )
        for status in enemy.get("status") or []:
            constant = buff_constant(str(status["id"]), buffs)
            lines.append(
                f"        Assert.Equal({int(status['amount'])}, "
                f"fight.EnemyBuffAmount({constant}, {index}));",
            )

    after_allies = after.get("allies") or []
    after_osty = next((a for a in after_allies if a.get("name") == "Osty"), None)
    if after_osty is not None:
        lines.append(f"        Assert.Equal({after_osty['hp']}, fight.State.OstyHp);")
        lines.append(
            f"        Assert.Equal({after_osty['max_hp']}, fight.State.OstyMaxHp);"
        )
    elif before_allies:
        # It was there and is not any more, which the emulator records as zero HP rather
        # than by removing anything.
        lines.append("        Assert.Equal(0, fight.State.OstyHp);")

    lines.append("    }")
    return "\n".join(lines)


def render(tests: list[str]) -> str:
    header = [
        "// AUTO-GENERATED — do not edit. Re-run scripts/generate_card_capture_tests.py to update.",
        "// Expected values come from the live game via scripts/capture_card.py, never from the",
        "// emulator: re-capturing a fixture re-reads ground truth, so regenerating cannot mask a",
        "// regression. Hand-written per-card tests live in Cards/<Class>/<Card>Tests.cs.",
        "using Sts2Emulator.Core;",
        "using Sts2Emulator.Core.Effects;",
        "using Xunit;",
        "using static Sts2Emulator.Tests.TestDeck;",
        "",
        "namespace Sts2Emulator.Tests;",
        "",
        "public class CardCaptureTests",
        "{",
    ]
    return "\n".join(header) + "\n" + "\n\n".join(tests) + "\n}\n"


def main() -> None:
    fixtures = sorted(FIXTURES.glob("*.json")) if FIXTURES.exists() else []
    if not fixtures:
        print(
            f"No card captures in {FIXTURES.relative_to(REPO)}; nothing to generate.\n"
            "Capture one with: python scripts/capture_card.py --card MoltenFist",
        )
        return

    constants, ids, enemies, buffs = (
        card_constants(),
        card_ids(),
        enemy_ids(),
        buff_ids(),
    )
    tests, skipped = [], []
    for path in fixtures:
        fixture = json.loads(path.read_text(encoding="utf-8"))
        try:
            tests.append(render_test(fixture, constants, ids, enemies, buffs))
        except UnsupportedCaptureError as exc:
            skipped.append(f"{path.name}: {exc}")

    names = [
        match.group(1)
        for match in (re.search(r"public void (\w+)\(", test) for test in tests)
        if match is not None
    ]
    duplicates = sorted({name for name in names if names.count(name) > 1})
    if duplicates:
        raise SystemExit(
            "Two captures generated the same test name, so one would be lost: "
            + ", ".join(duplicates),
        )

    if skipped:
        raise SystemExit(
            "Refusing to generate; these captures cannot be rebuilt faithfully:\n  "
            + "\n  ".join(skipped),
        )

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(render(tests), encoding="utf-8")
    print(f"Wrote {len(tests)} capture tests to {OUT.relative_to(REPO)}")


if __name__ == "__main__":
    main()
