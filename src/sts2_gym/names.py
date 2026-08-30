"""What the ids in the observation are called, read off the emulator's own source.

Every id the run and combat observations carry is a bare integer: card 472, relic 36,
buff 3, enemy 91. That is the right shape for a network and the wrong shape for a person,
and the mapping back to names already exists -- in ``Generated/*.g.cs`` for the content
tables and in the C# enums for everything else.

So this parses those files rather than restating them. A hand-kept table here would be a
claim about the emulator that nothing rechecks, and the generated files are regenerated
from the game by ``scripts/extract_data.py`` whenever the game moves. The same reasoning
is already in ``commands.py``, which reads ``Cards.g.cs`` to resolve a replay's card ids;
this is that idea with the rest of the tables added and the results cached.

Nothing here is on a hot path -- it is for people reading a state, not for training.
"""

from __future__ import annotations

import re
from dataclasses import dataclass
from functools import cache
from pathlib import Path

_CORE = Path(__file__).resolve().parents[2] / "src" / "Sts2Emulator" / "Core"
_GENERATED = Path(__file__).resolve().parents[2] / "src" / "Sts2Emulator" / "Generated"

# `Key: value` inside a `new SomethingDef(...)` literal. Values are ints, quoted strings,
# `Enum.Member` or bools; none of them contain a comma or a bracket, which is what lets
# this read the argument list without parsing C#.
_ARGUMENT = re.compile(r'(\w+):\s*("(?:[^"\\]|\\.)*"|[^,()]+)')


@dataclass(frozen=True)
class CardInfo:
    """A card's printed face: what it costs and what its numbers are.

    Not what it DOES -- the emulator has no card text, because the game's text lives in a
    localisation table the extractor does not read. A reader gets the name, the cost, the
    type and the damage or block, which is what the numbers on a card are; the rules are
    the player's own knowledge of the game.
    """

    card_id: int
    name: str
    cost: int
    base_damage: int
    base_block: int
    upgrade_damage: int
    upgrade_block: int
    upgrade_cost: int
    card_type: str
    rarity: str
    target: str
    ethereal: bool
    exhaust: bool
    unplayable: bool
    retain: bool
    innate: bool
    innate_when_upgraded: bool
    has_energy_cost_x: bool
    star_cost: int
    has_star_cost_x: bool

    def cost_for(self, upgraded: bool) -> int:
        """`CombatEngine.EffectiveCost`'s printed half: UpgradeCost is a DELTA."""
        return self.cost + (self.upgrade_cost if upgraded else 0)

    def damage_for(self, upgraded: bool) -> int:
        """`CardEffects.Dmg` before any buff -- the number printed on the card."""
        return self.base_damage + (self.upgrade_damage if upgraded else 0)

    def block_for(self, upgraded: bool) -> int:
        """`CardEffects.Blk` before any buff -- the number printed on the card."""
        return self.base_block + (self.upgrade_block if upgraded else 0)

    def targets_an_enemy(self) -> bool:
        """Whether playing this card needs the player to pick which enemy.

        `AllEnemies` and `RandomEnemy` hit without being aimed, so only `AnyEnemy` is a
        question to ask. `TargetedNoCreature` is aimed at a spot rather than a creature
        and the engine takes no index for it.
        """
        return self.target == "AnyEnemy"


def spaced(name: str) -> str:
    """Split a C# class name into words: ``SeekerStrike`` -> ``Seeker Strike``.

    Runs of capitals are kept together so ``AOEAttack`` does not come apart, and a digit
    starts its own word the way the game's own names read.
    """
    return " ".join(re.findall(r"[A-Z]+(?![a-z])|[A-Z]?[a-z]+|\d+", name)) or name


def _definitions(path: Path, literal: str) -> dict[int, dict[str, str]]:
    """Read every ``new <literal>(...)`` in a generated file, keyed by its Id."""
    result: dict[int, dict[str, str]] = {}
    for match in re.finditer(
        rf"new {literal}\((?P<args>[^;]*?)\)(?=,?\s*$)",
        path.read_text(encoding="utf-8"),
        re.MULTILINE,
    ):
        fields = {
            key: value.strip() for key, value in _ARGUMENT.findall(match.group("args"))
        }
        if "Id" in fields:
            result[int(fields["Id"])] = fields
    return result


def _enum(path: Path, name: str) -> dict[int, str]:
    """Read a C# enum's members in order, honouring any explicit ``= n``.

    The observation carries these as ordinals, so reading them positionally is the point.
    An enum whose members were renumbered by hand would break here loudly rather than
    quietly renaming every buff in a readout, which is the failure worth having.

    Raises:
        RuntimeError: If the enum is not in the file, which means the engine moved it.

    """
    body = re.search(
        rf"^public enum {name}\s*$\s*^\{{(?P<body>.*?)^\}}",
        path.read_text(encoding="utf-8"),
        re.MULTILINE | re.DOTALL,
    )
    if body is None:
        raise RuntimeError(f"enum {name} not found in {path}")

    text = re.sub(r"//[^\n]*|/\*.*?\*/", "", body.group("body"), flags=re.DOTALL)
    members: dict[int, str] = {}
    value = 0
    for entry in text.split(","):
        entry = entry.strip()
        if not entry:
            continue
        member, _, explicit = entry.partition("=")
        if explicit.strip():
            value = int(explicit.strip())
        members[value] = member.strip()
        value += 1
    return members


def _quoted(value: str) -> str:
    return value.strip().strip('"')


def _flag(fields: dict[str, str], key: str) -> bool:
    return fields.get(key, "false").strip() == "true"


def _int(fields: dict[str, str], key: str, default: int = 0) -> int:
    return int(fields.get(key, default))


def _member(fields: dict[str, str], key: str, default: str = "") -> str:
    """``CardType.Attack`` -> ``Attack``."""
    return fields.get(key, default).rpartition(".")[2] or default


@cache
def cards() -> dict[int, CardInfo]:
    return {
        card_id: CardInfo(
            card_id=card_id,
            name=_quoted(fields["Name"]),
            cost=_int(fields, "Cost"),
            base_damage=_int(fields, "BaseDamage"),
            base_block=_int(fields, "BaseBlock"),
            upgrade_damage=_int(fields, "UpgradeDamage"),
            upgrade_block=_int(fields, "UpgradeBlock"),
            upgrade_cost=_int(fields, "UpgradeCost"),
            card_type=_member(fields, "Type", "Skill"),
            rarity=_member(fields, "Rarity", "Common"),
            target=_member(fields, "Target", "Self"),
            ethereal=_flag(fields, "Ethereal"),
            exhaust=_flag(fields, "Exhaust"),
            unplayable=_flag(fields, "Unplayable"),
            retain=_flag(fields, "Retain"),
            innate=_flag(fields, "Innate"),
            innate_when_upgraded=_flag(fields, "InnateWhenUpgraded"),
            has_energy_cost_x=_flag(fields, "HasEnergyCostX"),
            star_cost=_int(fields, "StarCost", -1),
            has_star_cost_x=_flag(fields, "HasStarCostX"),
        )
        for card_id, fields in _definitions(
            _GENERATED / "Cards.g.cs",
            "CardDef",
        ).items()
    }


@cache
def relics() -> dict[int, str]:
    return {
        relic_id: _quoted(fields["Name"])
        for relic_id, fields in _definitions(
            _GENERATED / "Relics.g.cs",
            "RelicDef",
        ).items()
    }


@cache
def potions() -> dict[int, str]:
    return {
        potion_id: _quoted(fields["Name"])
        for potion_id, fields in _definitions(
            _GENERATED / "Potions.g.cs",
            "PotionDef",
        ).items()
    }


@cache
def enemies() -> dict[int, str]:
    return {
        enemy_id: _quoted(fields["Name"])
        for enemy_id, fields in _definitions(
            _GENERATED / "Enemies.g.cs",
            "EnemyDef",
        ).items()
    }


@cache
def buffs() -> dict[int, str]:
    return _enum(_CORE / "BuffState.cs", "BuffId")


@cache
def intents() -> dict[int, str]:
    return _enum(_CORE / "Enemy.cs", "IntentType")


@cache
def enchantments() -> dict[int, str]:
    return _enum(_CORE / "Card.cs", "Enchantment")


@cache
def deck_selections() -> dict[int, str]:
    """`DeckSelection` -- what answering an open card-select screen does to the card."""
    return _enum(_CORE / "Run" / "RunState.cs", "DeckSelection")


@cache
def events() -> dict[int, str]:
    """Event ids, which live as ``public const int Event<Name>`` rather than an enum."""
    text = (_CORE / "Run" / "RunConstants.cs").read_text(encoding="utf-8")
    return {
        int(value): name
        for name, value in re.findall(
            r"public const int Event(\w+)\s*=\s*(-?\d+);",
            text,
        )
        # `EventResultPending = -1` and `EventSkipAction = 3` are not events; an id is
        # positive and the two action constants would otherwise shadow real events.
        if int(value) > 0 and name not in {"SkipAction", "ResultPending"}
    }


def card(card_id: int) -> CardInfo | None:
    return cards().get(card_id)


def card_name(card_id: int, upgraded: bool = False) -> str:
    info = card(card_id)
    name = spaced(info.name) if info is not None else f"card-{card_id}"
    return f"{name}+" if upgraded else name


def relic_name(relic_id: int) -> str:
    return spaced(relics().get(relic_id, f"relic-{relic_id}"))


def potion_name(potion_id: int) -> str:
    return spaced(potions().get(potion_id, f"potion-{potion_id}"))


def enemy_name(enemy_id: int) -> str:
    return spaced(enemies().get(enemy_id, f"enemy-{enemy_id}"))


def buff_name(buff_id: int) -> str:
    return spaced(buffs().get(buff_id, f"buff-{buff_id}"))


def intent_name(intent: int) -> str:
    return intents().get(intent, f"intent-{intent}")


def enchantment_name(enchantment: int) -> str:
    return spaced(enchantments().get(enchantment, f"enchantment-{enchantment}"))


def deck_selection_name(kind: int) -> str:
    return spaced(deck_selections().get(kind, f"selection-{kind}"))


def event_name(event_id: int) -> str:
    return spaced(events().get(event_id, f"event-{event_id}"))
