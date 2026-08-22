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


# The terminator has to allow `{` as well as `;`: a MoveState with an object initializer
# — `new MoveState("STUN_MOVE", StunnedMove, new StunIntent()) { MustPerformOnce... }` —
# is followed by a brace, and a pattern that insists on the semicolon runs on to the NEXT
# declaration and swallows it. That silently shrank the denominator for every monster
# with one (Ceremonial Beast's BEAST_CRY, Waterfall Giant's EXPLODE), which makes
# coverage easier to pass rather than harder.
MOVE_STATE = re.compile(
    r'new MoveState\(\s*"(?P<name>[A-Z0-9_]+)"\s*,\s*\w+\s*,\s*(?P<intents>.*?)\)\s*[;{]',
    re.DOTALL,
)
INTENT = re.compile(r"new (?P<kind>\w*Intent)\((?P<args>[^()]*(?:\([^()]*\)[^()]*)*)\)")

# The machine is declared in terms of local variables, so following its edges means
# knowing which variable holds which state.
STATE_LITERAL = re.compile(
    # Branch ids are not all shouty — Slithering Strangler's is "rand" — and missing one
    # silently drops every edge out of it, which reads as unreachable attacks.
    r'new (?P<kind>MoveState|RandomBranchState|ConditionalBranchState)\(\s*"(?P<id>\w+)"',
)
ASSIGNED_TO = re.compile(r"(?P<name>[A-Za-z_][\w.]*)\s*=(?!=)")
FOLLOW_UP_ASSIGN = re.compile(r"(?P<from>[A-Za-z_]\w*)\.FollowUpState\s*=(?!=)")
BRANCH_EDGE = re.compile(
    r"(?P<from>[A-Za-z_]\w*)\.(?:AddBranch|AddState)\(\s*(?P<to>[A-Za-z_]\w*)",
)
MACHINE = re.compile(
    r"new MonsterMoveStateMachine\(\s*\w+\s*,\s*(?P<initial>.+?)\)\s*;", re.DOTALL
)
IDENTIFIER = re.compile(r"[A-Za-z_]\w*")


def reachable_state_ids(text: str) -> set[str] | None:
    """The state ids a monster can actually walk to from its initial state.

    Some states are not in the machine's graph at all: nothing sets them as a
    FollowUpState or adds them to a branch, and the only way in is an outside trigger.
    Terror Eel's STUN_MOVE is entered by ShriekPower when an unblocked hit drops the eel
    to its threshold, and Waterfall Giant's ABOUT_TO_BLOW by TriggerAboutToBlowState —
    with TERROR_MOVE and EXPLODE_MOVE hanging off those, reachable only through them. A
    capture that neither kills nor nearly kills the monster can never see any of the
    four, so counting them as declared moves makes coverage unreachable by construction
    rather than saying anything about the emulator.

    Returns None when the machine cannot be parsed, which means "count everything" — a
    parser that quietly found no edges would drop every move and turn coverage green.
    """
    # States are declared and wired in the same statement often enough that the parse has
    # to be statement-wise: Lagavulin Matriarch's branch arrives as
    # `ConditionalBranchState cbs = (ConditionalBranchState)(sleep.FollowUpState = new
    # ConditionalBranchState("SLEEP_BRANCH"));`, which both names the branch and points
    # the sleep move at it.
    names: dict[str, str] = {}
    edges: dict[str, set[str]] = {}
    statements = [statement for statement in text.split(";")]

    for statement in statements:
        declared = STATE_LITERAL.search(statement)
        if declared is None:
            continue
        state_id = declared.group("id")
        for lhs in ASSIGNED_TO.finditer(statement[: declared.start()]):
            names[lhs.group("name")] = state_id
        for edge in FOLLOW_UP_ASSIGN.finditer(statement[: declared.start()]):
            edges.setdefault(edge.group("from"), set()).add(state_id)

    def resolve(expression: str) -> set[str]:
        return {
            names[identifier]
            for identifier in IDENTIFIER.findall(expression)
            if identifier in names
        }

    for statement in statements:
        if STATE_LITERAL.search(statement):
            continue
        follow = FOLLOW_UP_ASSIGN.search(statement)
        if follow is not None:
            for target in resolve(statement[follow.end() :]):
                edges.setdefault(follow.group("from"), set()).add(target)
        for branch in BRANCH_EDGE.finditer(statement):
            edges.setdefault(branch.group("from").strip(), set()).add(
                branch.group("to").strip()
            )

    # Edges are keyed by variable name; rewrite them to state ids now that every
    # declaration has been seen.
    by_state: dict[str, set[str]] = {}
    for source, targets in edges.items():
        state = names.get(source)
        if state is None:
            continue
        by_state.setdefault(state, set()).update(
            names.get(target, target) for target in targets
        )

    machine = MACHINE.search(text)
    if machine is None or not names:
        return None

    # The initial state can be a variable, a ternary (Wriggler) or a switch over a
    # starter index (TwoTailedRat), so every state named in the expression is a root, as
    # is anything the variables in it were themselves assigned from.
    initial = machine.group("initial")
    roots = resolve(initial)
    for identifier in IDENTIFIER.findall(initial):
        for assignment in re.finditer(
            rf"\b{re.escape(identifier)}\s*=\s*([^;]+);", text
        ):
            roots |= resolve(assignment.group(1))
    if not roots:
        return None

    reached = set(roots)
    frontier = list(roots)
    while frontier:
        for target in by_state.get(frontier.pop(), ()):
            if target not in reached:
                reached.add(target)
                frontier.append(target)
    return reached


def moves_for(enemy: str) -> list[Move]:
    """Every declared move for an enemy, deduplicated by its intent expression.

    Raises:
        SystemExit: the enemy has no decompiled source to read.

    """
    path = MONSTERS / f"{enemy}.cs"
    if not path.exists():
        raise SystemExit(f"No decompiled monster at {path}. Run scripts/decompile.sh.")

    text = path.read_text()
    reachable = reachable_state_ids(text)
    moves: list[Move] = []
    seen: set[str] = set()
    for match in MOVE_STATE.finditer(text):
        if reachable is not None and match.group("name") not in reachable:
            continue
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

    # Some display names drop a word the class keeps: every Ruby Raider shows as
    # "Crossbow Raider" against CrossbowRubyRaider. Match on the first and last word
    # instead, which pins both ends of the name and cannot collapse two raiders onto
    # each other.
    words = [w for w in re.split(r"[^A-Za-z0-9]+", live_name) if w]
    if len(words) >= 2:
        head, tail = words[0].lower(), words[-1].lower()
        matches = [
            candidate.stem
            for candidate in MONSTERS.glob("*.cs")
            if candidate.stem.lower().startswith(head)
            and candidate.stem.lower().endswith(tail)
        ]
        if len(matches) == 1:
            return moves_for(matches[0])
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
