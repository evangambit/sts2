#!/usr/bin/env python3
"""Play a full run in the emulator, by hand, from a terminal.

    uv run python scripts/play.py                 # a random seed
    uv run python scripts/play.py --seed CLIPLAY  # the same run every time

The emulator has been driven by three things so far -- a replay reading a capture, a
random policy soaking act one, and a search. All three read the observation as numbers
and none of them has to make sense of it. This one does: it turns the same observation
into a screen, labels every action the mask allows, and asks a person to pick one.

That is worth having for two reasons beyond playing. It is the fastest way to reach a
state a script would take a fixture to build -- walk to the shop and look at it -- and it
is the only reader that puts a name to everything at once, so a wrong card, a wrong
intent or an option that should not be on a screen shows up as something a player
notices rather than as a number nobody reads.

**It shows the run exactly as the observation carries it, and no more.** Where the
emulator does not model something -- an event's option text, the order of the draw pile,
what a card actually does -- the screen says so rather than inventing it. A readout that
filled those gaps in from the real game would be a nicer toy and a worse instrument.

Type `help` at any prompt for the meta-commands (`deck`, `map`, `undo`, `log`, `quit`).
"""

from __future__ import annotations

import argparse
import os
import random
import sys
from dataclasses import dataclass, field
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "src"))

from sts2_gym import names, native
from sts2_gym import run_constants as rc
from sts2_gym.commands import living_enemy_indices
from sts2_gym.run_env import Sts2RunEnv

PHASE_NAMES = {
    rc.PHASE_COMBAT: "combat",
    rc.PHASE_CARD_REWARD: "card reward",
    rc.PHASE_MAP: "map",
    rc.PHASE_REST: "rest site",
    rc.PHASE_SHOP: "shop",
    rc.PHASE_RELIC_REWARD: "rewards",
    rc.PHASE_COMPLETE: "run over",
    rc.PHASE_EVENT: "event",
    rc.PHASE_ANCIENT: "ancient",
    rc.PHASE_TRANSFORM_SELECT: "choose a card",
    rc.PHASE_TREASURE: "treasure",
    rc.PHASE_CRYSTAL_SPHERE: "crystal sphere",
    rc.PHASE_BUNDLE_SELECT: "choose a bundle",
}

NODE_NAMES = {
    rc.NODE_NORMAL: "monster",
    rc.NODE_ELITE: "elite",
    rc.NODE_REST: "rest site",
    rc.NODE_SHOP: "shop",
    rc.NODE_RELIC: "treasure",
    rc.NODE_BOSS: "boss",
    rc.NODE_EVENT: "unknown",
    rc.NODE_ANCIENT: "ancient",
}

# One character per room, for the drawn map. Chosen so the shape of a path is readable at
# a glance rather than for prettiness: the two that end a run early -- the elite and the
# boss -- are the two capitals that stand out, and `?` is the game's own mark for the
# rooms it will not tell you about in advance.
NODE_GLYPHS = {
    rc.NODE_NORMAL: "m",
    rc.NODE_ELITE: "E",
    rc.NODE_REST: "r",
    rc.NODE_SHOP: "$",
    rc.NODE_RELIC: "t",
    rc.NODE_BOSS: "B",
    rc.NODE_EVENT: "?",
    rc.NODE_ANCIENT: "a",
}

# Characters per map column. Three hold a node cell -- `[m]` when it can be travelled to,
# ` m ` when it cannot -- and the fourth is the gap that gives a diagonal edge somewhere
# to be drawn.
MAP_COLUMN = 4

# The rest site's actions, which are sparse: 3 is the leave action every reward screen
# shares, so the site's own options straddle it.
REST_OPTIONS = {
    rc.REST_HEAL_ACTION: "rest — heal",
    rc.REST_UPGRADE_ACTION: "smith — upgrade a card",
    rc.REST_CLONE_ACTION: "clone a card (Pael's Growth)",
    rc.REST_LIFT_ACTION: "lift (Girya)",
    rc.REST_DIG_ACTION: "dig (Shovel)",
}

# The game's seed alphabet, which has no I and no O: `SeedHelper.CanonicalizeSeed` folds
# both into digits, so a random seed drawn with them in would print as one string and play
# as another.
SEED_ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"

# `CardSelectionKind`, said in the second person. The engine's own names are on the enum
# in PendingCardSelection.cs; these are what the screen is asking for.
SELECTION_KINDS = {
    1: "put a card from the discard pile on top of the draw pile",
    2: "exhaust a card from your hand",
    3: "exhaust a card from your hand, then draw",
    4: "take a card from the draw pile",
    5: "put a card from your hand on top of the draw pile",
    6: "exhaust a card from your hand",
    7: "choose a card",
    8: "choose a curse",
    9: "discard a card from your hand",
}


# ── reading the observation ───────────────────────────────────────────────────
#
# Every offset comes from `native`, which reads them off the emulator itself. The one
# exception is the secondary-intent slot size, derived below, and it is derived rather
# than written down for the same reason: a literal here would keep reading the old column
# after the layout moved instead of failing.

_POTION_OFFSET = (
    native.OBS_HAND_OFFSET + native.OBS_MAX_HAND * native.OBS_CARD_SLOT_SIZE
)
_POTION_SLOT_SIZE = (native.OBS_PLAYER_BUFF_OFFSET - _POTION_OFFSET) // 3
# Gold sits one slot before the selection block, and the secondary-intent block fills
# everything between it and its own offset -- so its slot size falls out of two published
# numbers without being published itself.
_SECONDARY_INTENT_SLOT_SIZE = (
    native.OBS_SELECTION_KIND_OFFSET - 1 - native.OBS_SECONDARY_INTENT_OFFSET
) // native.MAX_ENEMIES


@dataclass(frozen=True)
class HandCard:
    """One card in hand, as the observation carries it."""

    index: int
    card_id: int
    upgraded: bool
    enchantment: int
    enchant_amount: int

    @property
    def info(self):
        return names.card(self.card_id)


@dataclass(frozen=True)
class Enemy:
    """One creature on the field. `slot` is the emulator's own index, dead kept in place."""

    slot: int
    def_id: int
    hp: int
    max_hp: int
    block: int
    intent: int
    magnitude: int
    buffs: tuple[tuple[int, int], ...]
    secondary: tuple[int, int] | None


def card_at(obs, offset: int, index: int) -> tuple[int, bool, int, int]:
    at = offset + index * native.OBS_CARD_SLOT_SIZE
    return int(obs[at]), bool(obs[at + 1]), int(obs[at + 2]), int(obs[at + 3])


def read_hand(obs) -> list[HandCard]:
    """Read the hand in play order; a zero card id is an empty slot and ends it."""
    hand: list[HandCard] = []
    for index in range(native.OBS_MAX_HAND):
        card_id, upgraded, enchantment, amount = card_at(
            obs,
            native.OBS_HAND_OFFSET,
            index,
        )
        if card_id == 0:
            break
        hand.append(HandCard(index, card_id, upgraded, enchantment, amount))
    return hand


def read_enemies(obs, info) -> list[Enemy]:
    """Every enemy slot the engine holds, including its dead.

    The def ids come from the run rather than the observation, which carries an enemy's
    hp and intent and never says what it is. They are in the same order, dead included,
    so slot i names slot i.
    """
    def_ids = tuple(info.get("enemy_def_ids", ()))
    enemies: list[Enemy] = []
    for slot in range(native.MAX_ENEMIES):
        base = native.OBS_ENEMY_OFFSET + slot * native.OBS_ENEMY_SLOT_SIZE
        max_hp = int(obs[base + 1])
        if max_hp == 0:
            continue
        buffs = tuple(
            (int(obs[base + 5 + i * 2]), int(obs[base + 5 + i * 2 + 1]))
            for i in range(native.OBS_MAX_ENEMY_BUFFS)
            if int(obs[base + 5 + i * 2 + 1]) != 0
        )
        secondary_at = (
            native.OBS_SECONDARY_INTENT_OFFSET + slot * _SECONDARY_INTENT_SLOT_SIZE
        )
        # The block stores type + 1 so an empty slot can be told from intent type 0.
        raw_secondary = int(obs[secondary_at])
        enemies.append(
            Enemy(
                slot=slot,
                def_id=def_ids[slot] if slot < len(def_ids) else 0,
                hp=int(obs[base]),
                max_hp=max_hp,
                block=int(obs[base + 2]),
                intent=int(obs[base + 3]),
                magnitude=int(obs[base + 4]),
                buffs=buffs,
                secondary=(
                    (raw_secondary - 1, int(obs[secondary_at + 1]))
                    if raw_secondary != 0
                    else None
                ),
            ),
        )
    return enemies


def read_player_buffs(obs) -> list[tuple[int, int]]:
    return [
        (int(obs[native.OBS_PLAYER_BUFF_OFFSET + i * 2]), magnitude)
        for i in range(native.OBS_MAX_PLAYER_BUFFS)
        if (magnitude := int(obs[native.OBS_PLAYER_BUFF_OFFSET + i * 2 + 1])) != 0
    ]


def read_combat_potions(obs) -> list[int]:
    """Read the potion belt as COMBAT holds it.

    `info["potions"]` is the run's copy, and a potion drunk mid-fight leaves the combat's
    belt without leaving the run's until the fight ends -- so during combat the run's copy
    still lists a potion that is gone.
    """
    return [int(obs[_POTION_OFFSET + slot * _POTION_SLOT_SIZE]) for slot in range(3)]


def read_orbs(obs) -> tuple[int, list[tuple[int, int, int]]]:
    capacity = int(obs[native.OBS_ORB_CAPACITY_OFFSET])
    orbs = []
    for i in range(native.OBS_MAX_ORBS):
        at = native.OBS_ORB_OFFSET + i * native.OBS_ORB_SLOT_SIZE
        if int(obs[at]) == 0:
            break
        orbs.append((int(obs[at]) - 1, int(obs[at + 1]), int(obs[at + 2])))
    return capacity, orbs


def read_selection(obs) -> tuple[int, int, list[tuple[int, bool]]]:
    """Read an open card-selection screen: its kind, how many cards, and which.

    Kind 0 is `CardSelectionKind.None`, so a zero here means no screen is up.
    """
    kind = int(obs[native.OBS_SELECTION_KIND_OFFSET])
    count = int(obs[native.OBS_SELECTION_COUNT_OFFSET])
    candidates = []
    for i in range(min(count, native.OBS_MAX_SELECTION_CANDIDATES)):
        card_id, upgraded, _enchantment, _amount = card_at(
            obs,
            native.OBS_SELECTION_OFFSET,
            i,
        )
        candidates.append((card_id, upgraded))
    return kind, count, candidates


# ── writing it down ───────────────────────────────────────────────────────────


def describe_card(
    card_id: int,
    upgraded: bool,
    enchantment: int = 0,
    amount: int = 0,
) -> str:
    """Write out a card's printed face: name, cost, type, and the number it prints."""
    info = names.card(card_id)
    label = names.card_name(card_id, upgraded)
    if info is None:
        return label

    parts = [f"{label:<24}"]
    cost = info.cost_for(upgraded)
    parts.extend(
        (
            "X" if info.has_energy_cost_x else str(max(0, cost)),
            f"{info.card_type.lower():<7}",
        ),
    )

    numbers = []
    if info.damage_for(upgraded) > 0:
        numbers.append(f"{info.damage_for(upgraded)} dmg")
    if info.block_for(upgraded) > 0:
        numbers.append(f"{info.block_for(upgraded)} blk")
    if info.star_cost > 0:
        numbers.append(f"{info.star_cost}★")
    parts.append(", ".join(numbers) if numbers else "")

    keywords = [
        keyword
        for keyword, present in (
            ("exhaust", info.exhaust),
            ("ethereal", info.ethereal),
            ("retain", info.retain),
            ("innate", info.innate or (info.innate_when_upgraded and upgraded)),
            ("unplayable", info.unplayable),
        )
        if present
    ]
    if enchantment:
        keywords.append(f"{names.enchantment_name(enchantment).lower()} {amount}")
    if keywords:
        parts.append(f"[{', '.join(keywords)}]")
    return "  ".join(part for part in parts if part).rstrip()


def describe_buffs(buffs) -> str:
    return ", ".join(
        f"{names.buff_name(buff)} {magnitude}" for buff, magnitude in buffs
    )


def describe_intent(enemy: Enemy) -> str:
    """Say what the enemy has announced, as the game shows it.

    The magnitude means different things per intent -- damage for an attack, a count for
    anything else -- which is what `AnnouncedMagnitude` writes, so it is printed the same
    way rather than labelled as damage everywhere.
    """

    def one(intent: int, magnitude: int) -> str:
        detail = f" {magnitude}" if magnitude else ""
        return f"{names.intent_name(intent).lower()}{detail}"

    announced = one(enemy.intent, enemy.magnitude)
    if enemy.secondary is not None:
        # A SECOND declared intent, not a hit count: the Sludge Spinner announces its
        # spray and the Weak it comes with, and both are what the game shows.
        announced += f" + {one(*enemy.secondary)}"
    return announced


def bar(current: int, maximum: int, width: int = 10) -> str:
    if maximum <= 0:
        return " " * width
    filled = max(0, min(width, round(width * current / maximum)))
    return "█" * filled + "·" * (width - filled)


# ── the screen ────────────────────────────────────────────────────────────────


@dataclass
class Row:
    """One line of a screen: an action, what it does, and whether it can be taken."""

    action: int
    label: str
    takeable: bool


@dataclass
class Screen:
    """One rendered screen: what to show, and what the player may do about it.

    The rows are kept in ONE list rather than split into takeable and not, because the
    order is the information: an unplayable card belongs where it sits in your hand, not
    in a footnote under the end-turn line.
    """

    title: str
    lines: list[str] = field(default_factory=list)
    rows: list[Row] = field(default_factory=list)
    # Actions that need an enemy picked as well. Only combat has any.
    targeted: set[int] = field(default_factory=set)
    # Actions that may be aimed but do not have to be. A potion's target is not on the
    # potion -- the engine hands the aimed-at enemy to whichever one wants it -- so
    # demanding an aim for a Block Potion would be inventing a question.
    aimable: set[int] = field(default_factory=set)

    def offer(self, action: int, label: str) -> None:
        self.rows.append(Row(action, label, takeable=True))

    def show(self, action: int, label: str) -> None:
        """Add a row the player can read but not take, because its absence is information.

        A card you cannot afford is still in your hand, and a relic priced past your purse
        is still what the merchant has.
        """
        self.rows.append(Row(action, label, takeable=False))

    @property
    def choices(self) -> dict[int, str]:
        return {row.action: row.label for row in self.rows if row.takeable}

    @property
    def disabled(self) -> dict[int, str]:
        return {row.action: row.label for row in self.rows if not row.takeable}


def header(info: dict, seed: str) -> list[str]:
    relics = " · ".join(
        names.relic_name(relic["relic_id"]) + (" (spent)" if relic["used_up"] else "")
        for relic in info["relic_slots"]
    )
    potions = "  ".join(
        f"[{slot + 1}] {names.potion_name(potion) if potion else '—'}"
        for slot, potion in enumerate(info["potions"])
    )
    return [
        (
            f"seed {seed} · act {info['act']} · floor {info['floor']} · "
            f"HP {info['player_hp']}/{info['player_max_hp']} · "
            f"gold {info['gold']} · deck {info['deck_size']}"
        ),
        f"relics:  {relics or '—'}",
        f"potions: {potions}",
    ]


def combat_screen(obs, info, legal: set[int]) -> Screen:
    enemies = read_enemies(obs, info)
    living = [enemy for enemy in enemies if enemy.hp > 0]
    screen = Screen(title=f"COMBAT — {info['encounter']}")

    screen.lines.append("  enemies")
    for ordinal, enemy in enumerate(living, start=1):
        buffs = describe_buffs(enemy.buffs)
        screen.lines.append(
            f"   {ordinal}  {names.enemy_name(enemy.def_id):<22}"
            f"{enemy.hp:>4}/{enemy.max_hp:<4} {bar(enemy.hp, enemy.max_hp)}"
            f"{'  block ' + str(enemy.block) if enemy.block else '':<10}"
            f"  {describe_intent(enemy)}" + (f"   ({buffs})" if buffs else ""),
        )
    if not living:
        screen.lines.append("   (none standing)")

    player_buffs = describe_buffs(read_player_buffs(obs))
    screen.lines.append("")
    screen.lines.append(
        f"  you  HP {int(obs[0])}/{int(obs[1])} {bar(int(obs[0]), int(obs[1]))}"
        f"   block {int(obs[2])}   energy {int(obs[3])}/{int(obs[4])}",
    )
    if player_buffs:
        screen.lines.append(f"       {player_buffs}")
    capacity, orbs = read_orbs(obs)
    if capacity:
        ring = ", ".join(
            f"orb{orb} ({passive}/{evoke})" for orb, passive, evoke in orbs
        )
        screen.lines.append(f"       orbs {len(orbs)}/{capacity}: {ring or '—'}")
    screen.lines.append(
        f"       draw {int(obs[5])} · discard {int(obs[6])} · exhaust {int(obs[7])}"
        "   (piles are counts; the emulator does not expose their order)",
    )

    kind, count, candidates = read_selection(obs)
    if kind != 0:
        screen.title += " — " + SELECTION_KINDS.get(kind, f"card selection {kind}")
        screen.lines.append("")
        screen.lines.append("  choose:")
        for index in range(count):
            if index < len(candidates):
                card_id, upgraded = candidates[index]
                label = describe_card(card_id, upgraded)
            else:
                # The observation carries ten candidates; a wider screen is truncated
                # rather than dropped, and saying so beats printing a blank row.
                label = f"candidate {index} (past the observation's ten)"
            (screen.offer if index in legal else screen.show)(index, label)
        if count in legal:
            screen.offer(count, "decline")
        return screen

    screen.lines.append("")
    screen.lines.append("  hand")
    hand = read_hand(obs)
    potions = read_combat_potions(obs)
    for card in hand:
        label = describe_card(
            card.card_id,
            card.upgraded,
            card.enchantment,
            card.enchant_amount,
        )
        if card.index in legal:
            screen.offer(card.index, label)
            if (info_ := card.info) is not None and info_.targets_an_enemy():
                screen.targeted.add(card.index)
        else:
            screen.show(card.index, label)

    end_turn = len(hand)
    screen.offer(end_turn, "end turn")
    for slot, potion in enumerate(potions):
        action = end_turn + 1 + slot
        if potion and action in legal:
            screen.offer(action, f"drink {names.potion_name(potion)}")
            screen.aimable.add(action)
    return screen


def card_reward_screen(info, legal: set[int]) -> Screen:
    """Build the card-reward screen from the cards that are actually there.

    The card-reward mask is unconditional -- `WriteActionMask` sets 0, 1, 2 and skip
    without asking whether `State.RewardCards` holds anything -- while `StepCardReward`
    refuses a slot whose card id is zero. The two disagree on a screen that is re-entered
    after its cards have been taken, which a run reaches by claiming a queued reward:
    three actions are offered and all three are refused. Showing a slot the engine will
    refuse would be handing the player a move that does nothing, so an empty slot is
    listed as empty rather than offered.
    """
    screen = Screen(title="CARD REWARD")
    for index, card_id in enumerate(info["card_rewards"]):
        if index not in legal:
            continue
        if card_id:
            screen.offer(
                index,
                describe_card(card_id, info["card_reward_upgraded"][index]),
            )
        else:
            screen.show(
                index,
                "(empty — the mask offers this slot, the engine refuses it)",
            )
    if rc.REWARD_SKIP_ACTION in legal:
        screen.offer(
            rc.REWARD_SKIP_ACTION,
            "skip" if screen.choices else "leave the empty screen",
        )
    return screen


def gap_lines(
    edges: list[tuple[int, int]],
    width: int,
    centre,
) -> list[str]:
    r"""Draw the edges between one map row and the row above it.

    Two shapes, because the map has two. Ordinary rows step one column at most and are one
    line of `|`, `/` and `\\` sitting between the nodes they join. The two FAN rows -- the
    node below row one, which reaches every path start, and the row under the boss, which
    all reaches the boss -- cross up to three columns, and a slash at the midpoint of a
    three-column jump lands on a node it has nothing to do with. Those get the tree fan:
    risers off the many, a bar across, a stem into the one.
    """
    if not edges:
        return []

    if all(abs(child - parent) <= 1 for parent, child in edges):
        line = [" "] * (width * MAP_COLUMN)
        for parent, child in edges:
            if child == parent:
                line[centre(parent)] = "|"
            else:
                low, high = sorted((centre(parent), centre(child)))
                line[(low + high) // 2] = "/" if child > parent else "\\"
        return ["".join(line)]

    parents = {parent for parent, _child in edges}
    children = {child for _parent, child in edges}
    if len(parents) == 1:
        hub, leaves, hub_below = centre(next(iter(parents))), children, True
    elif len(children) == 1:
        hub, leaves, hub_below = centre(next(iter(children))), parents, False
    else:
        # Not a fan: several nodes each reaching several columns. Nothing in the generator
        # makes one, and drawing it wrong would be worse than saying so.
        return [f"{'':<{MAP_COLUMN}}(edges too tangled to draw)"]

    leaf_marks = sorted(centre(leaf) for leaf in leaves)
    risers = [" "] * (width * MAP_COLUMN)
    bar = [" "] * (width * MAP_COLUMN)
    stem = [" "] * (width * MAP_COLUMN)
    for at in range(min(*leaf_marks, hub), max(*leaf_marks, hub) + 1):
        bar[at] = "-"
    for at in leaf_marks:
        risers[at] = "|"
        bar[at] = "+"
    bar[hub] = "+"
    stem[hub] = "|"

    fan = ["".join(risers), "".join(bar), "".join(stem)]
    return fan if hub_below else fan[::-1]


def map_lines(
    graph: dict,
    floor: int,
    actions: dict[tuple[int, int], int],
) -> list[str]:
    r"""Draw the act map: every node, every edge, and where the run is standing.

    Rows descend down the page so the boss is at the top and the run's own position is at
    the bottom, next to the prompt -- which is the way the game draws it and, more to the
    point, puts the rows a player is choosing between closest to where they type.

    Almost every edge steps one column, and those are drawn as the single character that
    sits between the two nodes: `|`, `/` or `\\`. Two rows are not like that. The node
    below row 1 fans out to every path start and the whole of the row below the boss fans
    into it, so those edges cross up to three columns -- drawn as a dashed run rather than
    as one character, because a single slash placed at the midpoint of a three-column jump
    sits over a node it has nothing to do with and reads as an edge that is not there.
    """
    nodes = graph["nodes"]
    if not nodes:
        return ["   (no map — the act has not generated one)"]

    children: dict[tuple[int, int], list[tuple[int, int]]] = {}
    for parent, child in graph["edges"]:
        children.setdefault(parent, []).append(child)

    current = graph["current"]
    width = max(col for col, _row in nodes) + 1
    rows = sorted({row for _col, row in nodes}, reverse=True)
    # The floor a row sits on, derived from the row the run is actually standing on rather
    # than assumed. Nothing promises floor and row share an origin, and a gutter that
    # silently numbered every row wrong would be worse than no gutter at all.
    offset = floor - current[1] if current is not None else 0

    def centre(col: int) -> int:
        return col * MAP_COLUMN + 1

    lines: list[str] = []
    for index, row in enumerate(rows):
        cells = []
        for col in range(width):
            node_type = nodes.get((col, row), rc.NODE_NONE)
            glyph = NODE_GLYPHS.get(node_type, " ")
            if (col, row) == current:
                cells.append(" @ ")
            elif (col, row) in actions:
                cells.append(f"[{glyph}]")
            elif node_type == rc.NODE_NONE:
                cells.append("   ")
            else:
                cells.append(f" {glyph} ")
        lines.append(f"  {row + offset:>3}  " + " ".join(cells).rstrip())

        if index + 1 >= len(rows):
            break
        below = rows[index + 1]
        edges = [
            (col, child_col)
            for col in range(width)
            for child_col, child_row in children.get((col, below), ())
            if child_row == row
        ]
        lines += [
            "       " + line.rstrip()
            for line in gap_lines(edges, width, centre)
            if line.strip()
        ]

    lines.append("       " + " ".join(f" {col} " for col in range(width)))
    lines.append("")
    lines.append(
        "   "
        + " · ".join(
            f"{NODE_GLYPHS[node_type]} {NODE_NAMES[node_type]}"
            for node_type in (
                rc.NODE_NORMAL,
                rc.NODE_ELITE,
                rc.NODE_EVENT,
                rc.NODE_SHOP,
                rc.NODE_RELIC,
                rc.NODE_REST,
                rc.NODE_BOSS,
            )
        ),
    )
    lines.append("   @ where you are · [x] where you may go · the gutter is the floor")
    return lines


def map_screen(env: Sts2RunEnv, info, legal: set[int]) -> Screen:
    """Build the act map, with the row of nodes the run may travel to marked on it.

    `info["map_choices"]` drops the empty slots in ascending order and the mask sets the
    same slots, so the legal actions line up with the entries one for one -- and that
    pairing is also what puts an action number on the right node of the drawing.
    """
    screen = Screen(title="MAP — where to next")
    actions = {
        (choice["x"], choice["y"]): action
        for action, choice in zip(sorted(legal), info["map_choices"])
    }
    screen.lines += map_lines(env.map_graph(), int(info["floor"]), actions)
    screen.lines.append("")

    for (x, y), action in actions.items():
        choice = next(c for c in info["map_choices"] if (c["x"], c["y"]) == (x, y))
        node = NODE_NAMES.get(choice["node_type"], f"node-{choice['node_type']}")
        # The node's TYPE and nothing else. `info["map_choices"]` also carries the
        # encounter each node holds, and the game does not: you learn which monsters are
        # in a room by walking into it. Printing it turned every monster row into a
        # decision made with the answer already on the screen.
        screen.offer(action, f"{node:<10} in column {x}")
    return screen


def rest_screen(legal: set[int]) -> Screen:
    screen = Screen(title="REST SITE")
    for action, label in REST_OPTIONS.items():
        if action in legal:
            screen.offer(action, label)
    if rc.REWARD_SKIP_ACTION in legal:
        screen.offer(rc.REWARD_SKIP_ACTION, "leave without resting")
    return screen


def shop_screen(info, legal: set[int]) -> Screen:
    """Build the merchant's board, indexed by the action that buys each slot.

    Only what the run can AFFORD is legal, so a card priced past the purse is shown
    greyed out rather than hidden -- what is on sale is half of what a shop tells you.
    """
    screen = Screen(title=f"SHOP — {info['gold']} gold")
    costs = info["shop_costs"]

    def stock(action: int, description: str) -> None:
        row = f"{costs[action]:>4}g  {description}"
        (screen.offer if action in legal else screen.show)(action, row)

    for action, item in enumerate(info["shop_cards"]):
        if item:
            stock(action, describe_card(item, False))
    for offset, item in enumerate(info["shop_relics"]):
        if item:
            stock(7 + offset, f"relic: {names.relic_name(item)}")
    for offset, item in enumerate(info["shop_potions"]):
        if item:
            stock(10 + offset, f"potion: {names.potion_name(item)}")
    stock(rc.SHOP_REMOVE_ACTION, "remove a card from your deck")
    if rc.SHOP_SKIP_ACTION in legal:
        screen.offer(rc.SHOP_SKIP_ACTION, "leave the shop")
    return screen


def reward_screen(info, legal: set[int]) -> Screen:
    """Build the post-combat screen, whose actions are COMPACTED rather than fixed.

    `WriteRewardActionMask` hands out 0, 1, 2 … to whichever slots are non-empty, in the
    order gold, potion, relic, cards -- so an index means nothing until the same walk is
    repeated here.

    The walk is repeated against the MASK rather than against the reward list, because the
    two do not always agree: the mask offers a card action for `PendingCardOffers` as well
    as for `RewardCardPending`, and `Sts2Run_GetStateList`'s reward list counts only the
    second. A screen built from the list alone therefore came up an option short on the
    events that queue an offer -- five runs in a hundred. Laying the labels along the mask
    keeps the screen right whichever of the two is the source, and leaves an unlabelled
    action reading as what it is rather than as nothing.

    The last reward on a FULL screen is unreachable, and that is the engine's: action 3 is
    the leave action, and `StepRelicReward` answers it as leave before it ever reaches the
    claim. Four rewards at once are rare enough that no capture has hit it.
    """
    screen = Screen(title="REWARDS")
    pending = info["pending_rewards"]
    labels = []
    if pending[0]:
        labels.append(f"take {pending[0]} gold")
    if pending[1]:
        labels.append(f"take potion: {names.potion_name(pending[1])}")
    if pending[2]:
        labels.append(f"take relic: {names.relic_name(pending[2])}")
    labels += ["open the card reward"] * pending[3]

    for index, action in enumerate(
        sorted(action for action in legal if action != rc.REWARD_SKIP_ACTION),
    ):
        screen.offer(
            action,
            labels[index] if index < len(labels) else "open the card reward",
        )
    if rc.REWARD_SKIP_ACTION in legal:
        screen.offer(
            rc.REWARD_SKIP_ACTION,
            "leave (abandons anything left on the screen)",
        )
    return screen


def event_screen(info, legal: set[int]) -> Screen:
    """List an event's options by number, which is all the emulator knows of them.

    The game's option text lives in a localisation table `extract_data.py` does not read,
    so the emulator knows how many options an event offers and which are takeable, and
    nothing about what they say. Numbering them without pretending to name them is the
    honest readout; the event's own name is the clue a player has.
    """
    screen = Screen(title=f"EVENT — {names.event_name(info['event_id'])}")
    screen.lines.append(
        "  (the emulator carries no option text — option order is the game's)",
    )
    for action in sorted(legal):
        if action == rc.EVENT_SKIP_ACTION:
            continue
        screen.offer(action, f"option {action}")
    if rc.EVENT_SKIP_ACTION in legal:
        screen.offer(rc.EVENT_SKIP_ACTION, "leave")
    return screen


def ancient_screen(info, legal: set[int]) -> Screen:
    """Neow, and every act's ancient after it: two blessings and a cursed one.

    Every option is a RELIC -- `GenerateNeowOptions` picks two positives and one cursed
    from the relic pool -- so the third row is a bargain rather than a gift.
    """
    options = info["neow_options"]
    if not any(options):
        screen = Screen(title="ANCIENT")
        if 0 in legal:
            screen.offer(0, "proceed")
        return screen

    screen = Screen(title="ANCIENT — choose a blessing")
    for action, relic_id in enumerate(options):
        if relic_id and action in legal:
            screen.offer(action, names.relic_name(relic_id))
    return screen


def deck_selection_purpose(info) -> str:
    """Say what answering the open deck screen will do to the card.

    One screen answers four different questions -- remove it, upgrade it, transform it,
    copy it -- and the list of cards is identical in all four. `DeckSelection` is what
    tells them apart, and a player picking blind between them is picking blind.
    """
    # Padded, because a build whose state list 20 is missing returns an empty tuple
    # rather than a short one, and an unpacking that throws would take the screen with it.
    padded = (*info.get("deck_selection", ()), 0, 0, 0)
    kind, argument, rest_upgrade = padded[:3]
    if rest_upgrade:
        return "upgrade one at the fire"
    if kind == 0:
        return "choose a card"
    purpose = names.deck_selection_name(kind).lower()
    if names.deck_selections().get(kind) == "TransformTo" and argument:
        return f"{purpose} {names.card_name(argument)}"
    if names.deck_selections().get(kind) == "Enchant" and argument:
        return f"enchant one with {names.enchantment_name(argument)}"
    return purpose


def transform_screen(env: Sts2RunEnv, info, legal: set[int]) -> Screen:
    """Build a card-select screen, which is two screens wearing one phase.

    With an offer grid open the action indexes the OFFER; otherwise it indexes the DECK.
    Asking the run which is open is the same question a replay has to ask -- guessing from
    the deck's contents gets it wrong the moment an offer holds a card the deck also has.
    """
    offer = info["offer_cards"]
    if offer:
        screen = Screen(title="CHOOSE A CARD")
        for action in sorted(legal):
            if action < len(offer):
                screen.offer(action, describe_card(offer[action], False))
        return screen

    screen = Screen(title=f"YOUR DECK — {deck_selection_purpose(info)}")
    deck = info["deck"]
    for action in sorted(legal):
        if action < len(deck):
            card = deck[action]
            screen.offer(
                action,
                describe_card(
                    card["card_id"],
                    card["upgraded"],
                    card["enchantment"],
                    card["enchant_amount"],
                ),
            )
    return screen


def bundle_screen(info, legal: set[int]) -> Screen:
    """Scroll Boxes: two bundles of three, highlighted then confirmed."""
    screen = Screen(title="CHOOSE A BUNDLE")
    offer = info["bundle_offer"]
    for bundle in range(2):
        cards = offer[bundle * 3 : bundle * 3 + 3]
        if bundle in legal:
            screen.offer(
                bundle,
                ", ".join(names.card_name(card_id) for card_id in cards if card_id),
            )
    if rc.BUNDLE_CONFIRM_ACTION in legal:
        screen.offer(rc.BUNDLE_CONFIRM_ACTION, "confirm the highlighted bundle")
    return screen


def crystal_sphere_screen(legal: set[int]) -> Screen:
    """Offer the cells worth divining; the sphere's board is hidden from any player.

    See docs/agent-interface.md: the phase is modelled and the mask is right, and the
    fifteen items under the fog are not exposed to anything that plays. The cells offered
    are the ones that would uncover something.
    """
    size = 11
    screen = Screen(
        title="CRYSTAL SPHERE — divine (the board is hidden from any player)",
    )
    for action in sorted(legal):
        cell = action % (size * size)
        tool = "small" if action >= size * size else "big"
        screen.offer(action, f"{tool:<5} tool at x={cell // size}, y={cell % size}")
    return screen


def build_screen(env: Sts2RunEnv, obs, info, legal: set[int]) -> Screen:
    phase = int(info["phase"])
    if phase == rc.PHASE_COMBAT:
        return combat_screen(obs, info, legal)
    if phase == rc.PHASE_CARD_REWARD:
        return card_reward_screen(info, legal)
    if phase == rc.PHASE_MAP:
        return map_screen(env, info, legal)
    if phase == rc.PHASE_REST:
        return rest_screen(legal)
    if phase == rc.PHASE_SHOP:
        return shop_screen(info, legal)
    if phase == rc.PHASE_RELIC_REWARD:
        return reward_screen(info, legal)
    if phase == rc.PHASE_EVENT:
        return event_screen(info, legal)
    if phase == rc.PHASE_ANCIENT:
        return ancient_screen(info, legal)
    if phase == rc.PHASE_TRANSFORM_SELECT:
        return transform_screen(env, info, legal)
    if phase == rc.PHASE_BUNDLE_SELECT:
        return bundle_screen(info, legal)
    if phase == rc.PHASE_CRYSTAL_SPHERE:
        return crystal_sphere_screen(legal)
    if phase == rc.PHASE_COMPLETE:
        # Nothing to offer; the loop says how it ended. A title beats the fallback's
        # "no screen written", which reads as a missing renderer rather than a finished run.
        return Screen(title="RUN OVER")
    if phase == rc.PHASE_TREASURE:
        screen = Screen(title="TREASURE")
        if rc.REWARD_SKIP_ACTION in legal:
            screen.offer(rc.REWARD_SKIP_ACTION, "open the chest")
        return screen

    # A phase with no renderer still has a mask, and a bare list of legal numbers is
    # playable where a crash is not. Nothing should reach this; if something does, the
    # missing screen is the bug and this says which phase it was.
    screen = Screen(
        title=f"{PHASE_NAMES.get(phase, f'phase {phase}').upper()} (no screen written)",
    )
    for action in sorted(legal):
        screen.offer(action, f"action {action}")
    return screen


# ── the session ───────────────────────────────────────────────────────────────

HELP = """
  <n>          take action n
  <n> <t>      take action n aimed at enemy t (combat only)
  deck         list your deck
  relics       list your relics
  map          the nodes on offer, when a map is up
  log          what has happened so far
  undo         take back the last action
  state        the raw info dict — a debugging hatch, and it SPOILS: the dict
               carries things the game hides from a player, the encounter
               waiting on each map node among them
  help         this
  quit         leave
"""


class Session:
    """One run, and the terminal in front of it."""

    def __init__(self, seed: str, undo_depth: int) -> None:
        self.seed = seed
        self.env = Sts2RunEnv(seed=seed, max_episode_steps=100_000, max_floors=64)
        self.obs, self.info = self.env.reset()
        self.log: list[str] = []
        self.undo_depth = undo_depth
        # A faithful clone per snapshot, and every clone holds one of the engine's 256
        # handles -- so the stack is bounded and the evicted end is closed rather than
        # dropped. An undo that leaked handles would die of a full pool a hundred moves in.
        self.snapshots: list[tuple[Sts2RunEnv, str]] = []
        self.done = False

    def close(self) -> None:
        for snapshot, _ in self.snapshots:
            snapshot.close()
        self.snapshots.clear()
        self.env.close()

    def legal(self) -> set[int]:
        return {int(action) for action in np.flatnonzero(self.env.action_masks())}

    def snapshot(self, label: str) -> None:
        if self.undo_depth <= 0:
            return
        self.snapshots.append((self.env.clone(resample_hidden=False), label))
        while len(self.snapshots) > self.undo_depth:
            evicted, _ = self.snapshots.pop(0)
            evicted.close()

    def undo(self) -> str:
        if not self.snapshots:
            return "nothing to undo"
        restored, label = self.snapshots.pop()
        self.env.close()
        self.env = restored
        self.obs = self.env._obs()
        self.info = self.env._info()
        self.done = False
        if self.log:
            self.log.pop()
        return f"took back: {label}"

    def take(self, action: int, target: int, label: str) -> None:
        self.snapshot(label)
        self.obs, reward, terminated, truncated, self.info = self.env.step(
            action,
            target=target,
        )
        self.log.append(label)
        if reward < 0 and not terminated:
            # `_invalid_action` is how the engine refuses a step, and it refuses without
            # moving -- so this is worth saying rather than leaving as a screen that did
            # not change.
            self.log[-1] = f"{label}  (refused by the engine)"
        self.done = terminated or truncated


def render(info: dict, seed: str, screen: Screen, colour: bool) -> str:
    rule = "─" * 78
    out = [rule]
    out += [" " + line for line in header(info, seed)]
    out.append(rule)
    out.append("")
    out.append(f" {screen.title}")
    out.append("")
    # The body owns its own spacing: a heading like "hand" wants the rows immediately
    # under it, and a blank inserted here would divorce them.
    out += screen.lines
    for row in screen.rows:
        if row.takeable:
            aim = " ⟵ pick a target" if row.action in screen.targeted else ""
            out.append(f"   [{row.action:>2}] {row.label}{aim}")
        else:
            # No brackets on a row that cannot be typed: the numbers a player may enter
            # should be the only ones that look like numbers to enter.
            out.append(
                dim(f"    {row.action:>2}  {row.label}   (not available)", colour),
            )
    return "\n".join(out)


def dim(text: str, colour: bool) -> str:
    return f"\033[2m{text}\033[0m" if colour else text


def deck_listing(info) -> str:
    lines = [f" deck — {info['deck_size']} cards"]
    for index, card in enumerate(info["deck"]):
        lines.append(
            f"   {index:>3}  "
            + describe_card(
                card["card_id"],
                card["upgraded"],
                card["enchantment"],
                card["enchant_amount"],
            ),
        )
    return "\n".join(lines)


def relic_listing(info) -> str:
    lines = [" relics"]
    for relic in info["relic_slots"]:
        counter = f"  counter {relic['counter']}" if relic["counter"] else ""
        spent = "  (spent)" if relic["used_up"] else ""
        lines.append(f"   {names.relic_name(relic['relic_id'])}{counter}{spent}")
    return "\n".join(lines) if len(lines) > 1 else " relics: none"


def prompt(interactive: bool) -> str | None:
    """Read a line, echoing it when the input is a pipe so a transcript reads back.

    A piped stdin echoes nothing of its own, so a recorded session would otherwise be a
    list of screens with no sign of what was typed between them.
    """
    try:
        line = input("\n> ") if interactive else input()
    except EOFError:
        return None
    if not interactive:
        print(f"\n> {line}")
    return line


def resolve_target(
    session: Session,
    screen: Screen,
    action: int,
    given: int | None,
) -> int:
    """Turn a displayed enemy number into the engine's own enemy index.

    The screen numbers LIVING enemies from one; the engine indexes a list that keeps its
    dead. The two agree until something dies, which is exactly when a mistake here would
    start hitting the wrong creature.
    """
    if action not in screen.targeted and action not in screen.aimable:
        return -1
    living = living_enemy_indices(session.obs)
    if not living:
        return -1
    if given is not None and 1 <= given <= len(living):
        return living[given - 1]
    if len(living) == 1:
        return living[0]
    if action in screen.aimable:
        return -1
    print(f"   aim at which enemy? 1..{len(living)} — e.g. `{action} 1`")
    return -2


def random_seed() -> str:
    """Draw a seed the way the game's own lobby does, so a run can be typed back in."""
    # SystemRandom rather than the module's own: a run seed wants no reproducibility of
    # its own, and this is the draw that does not have to explain itself to a linter.
    picker = random.SystemRandom()
    return "".join(picker.choice(SEED_ALPHABET) for _ in range(8))


def main() -> int:
    parser = argparse.ArgumentParser(description=(__doc__ or "").splitlines()[0])
    parser.add_argument(
        "--seed",
        default=None,
        help="the run seed, as the game takes it; random when omitted",
    )
    parser.add_argument(
        "--undo-depth",
        type=int,
        default=20,
        help="how many moves can be taken back; 0 disables undo (default: 20)",
    )
    parser.add_argument("--no-color", action="store_true", help="plain output")
    args = parser.parse_args()

    seed = args.seed or random_seed()
    interactive = sys.stdin.isatty()
    colour = (
        not args.no_color and sys.stdout.isatty() and not os.environ.get("NO_COLOR")
    )

    session = Session(seed, max(0, args.undo_depth))
    print(f"\nSlay the Spire 2 — emulator, seed {seed}.  `help` for commands.")
    try:
        return loop(session, interactive, colour)
    finally:
        session.close()


def loop(session: Session, interactive: bool, colour: bool) -> int:
    show = True
    while True:
        legal = session.legal()
        screen = build_screen(session.env, session.obs, session.info, legal)
        if show:
            print()
            print(render(session.info, session.seed, screen, colour))
        show = True

        if session.done or int(session.info["phase"]) == rc.PHASE_COMPLETE or not legal:
            won = session.info["player_won"]
            print(
                f"\n  run over on floor {session.info['floor']} — "
                + ("the run was won." if won else "the player is dead."),
            )
            return 0

        line = prompt(interactive)
        if line is None:
            print("\n  (end of input)")
            return 0
        line = line.strip().lower()

        if line in {"", "look"}:
            continue
        if line in {"q", "quit", "exit"}:
            return 0
        if line in {"h", "help", "?"}:
            print(HELP)
            show = False
            continue
        if line == "deck":
            print(deck_listing(session.info))
            show = False
            continue
        if line == "relics":
            print(relic_listing(session.info))
            show = False
            continue
        if line == "map":
            actions = {
                (choice["x"], choice["y"]): action
                for action, choice in zip(sorted(legal), session.info["map_choices"])
            }
            print()
            print(
                "\n".join(
                    map_lines(
                        session.env.map_graph(),
                        int(session.info["floor"]),
                        actions,
                    ),
                ),
            )
            show = False
            continue
        if line == "log":
            for index, entry in enumerate(session.log, start=1):
                print(f"   {index:>3}  {entry}")
            show = False
            continue
        if line == "state":
            for key, value in session.info.items():
                print(f"   {key}: {value}")
            show = False
            continue
        if line == "undo":
            print(f"\n  {session.undo()}")
            continue

        parts = line.split()
        if not parts[0].lstrip("-").isdigit():
            print("   didn't understand that — `help` lists the commands")
            show = False
            continue

        action = int(parts[0])
        given = int(parts[1]) if len(parts) > 1 and parts[1].isdigit() else None
        if action not in legal:
            print(f"   {action} is not a legal action here")
            show = False
            continue

        target = resolve_target(session, screen, action, given)
        if target == -2:
            show = False
            continue

        label = f"floor {session.info['floor']}, {PHASE_NAMES.get(int(session.info['phase']), '?')}: {screen.choices[action]}"
        session.take(action, target, label)


if __name__ == "__main__":
    raise SystemExit(main())
