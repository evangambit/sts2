"""Read each enemy's declared moves out of the decompiled monster classes.

Coverage needs a denominator. "The opening intent matches" says nothing about the state
machine behind it, and a fight test that ends after one turn can pass while every later
move is wrong — so the turn sweep needs to know how many distinct moves an enemy HAS.

The game declares them plainly:

    MoveState moveState = new MoveState("WHIP_SLAP_MOVE", WhipSlapMove,
                                        new MultiAttackIntent(WhipSlapDamage, WhipSlapRepeat));

Two MoveStates can share one intent (FuzzyWurmCrawler's FIRST_ACID_GOOP and ACID_GOOP are
the same attack from different entry points), so moves are counted by their *intent
expression*, not by MoveState count — otherwise coverage could never reach 100%.

    python scripts/enemy_moves.py            # every enemy with a move table
    python scripts/enemy_moves.py CorpseSlug Toadpole
"""

from __future__ import annotations

import argparse
import re
from pathlib import Path
from typing import NamedTuple

MONSTERS = (
    Path(__file__).parent.parent / "decompiled/MegaCrit.Sts2.Core.Models.Monsters"
)


class Move(NamedTuple):
    """One declared move: the game's state name and the intents it announces."""

    name: str
    intents: tuple[str, ...]


MOVE_STATE = re.compile(
    r'new MoveState\(\s*"(?P<name>[A-Z0-9_]+)"\s*,\s*\w+\s*,\s*(?P<intents>.*?)\)\s*;',
    re.DOTALL,
)
INTENT = re.compile(r"new (?P<kind>\w*Intent)\((?P<args>[^()]*(?:\([^()]*\)[^()]*)*)\)")


def moves_for(enemy: str) -> list[Move]:
    """Every declared move for an enemy, deduplicated by its intent expression.

    Raises:
        SystemExit: the enemy has no decompiled source to read.

    """
    path = MONSTERS / f"{enemy}.cs"
    if not path.exists():
        raise SystemExit(f"No decompiled monster at {path}. Run scripts/decompile.sh.")

    moves: list[Move] = []
    seen: set[str] = set()
    for match in MOVE_STATE.finditer(path.read_text()):
        intents = [
            f"{m.group('kind')}({m.group('args').strip()})"
            for m in INTENT.finditer(match.group("intents"))
        ]
        if not intents:
            continue
        key = " + ".join(intents)
        if key in seen:
            continue
        seen.add(key)
        moves.append(Move(match.group("name"), tuple(intents)))
    return moves


def moves_for_live_name(live_name: str) -> list[Move] | None:
    """Look a move table up by the name the live game reports, e.g. "Corpse Slug".

    The mod reports display names and the decompiled classes are PascalCase, and the
    two do not always collapse the same way ("Leaf Slime (S)" -> LeafSlimeS), so try the
    obvious squash and fall back to matching case-insensitively on letters only.
    """
    squashed = re.sub(r"[^A-Za-z0-9]", "", live_name)
    path = MONSTERS / f"{squashed}.cs"
    if path.exists():
        return moves_for(squashed)
    wanted = squashed.lower()
    for candidate in MONSTERS.glob("*.cs"):
        if candidate.stem.lower() == wanted:
            return moves_for(candidate.stem)
    return None


def all_enemies() -> list[str]:
    return sorted(p.stem for p in MONSTERS.glob("*.cs"))


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("enemies", nargs="*", default=None)
    args = parser.parse_args()

    for enemy in args.enemies or all_enemies():
        try:
            moves = moves_for(enemy)
        except SystemExit:
            continue
        if not moves:
            continue
        print(f"{enemy} ({len(moves)} distinct)")
        for move in moves:
            print(f"    {move.name:24} {' + '.join(move.intents)}")


if __name__ == "__main__":
    main()
