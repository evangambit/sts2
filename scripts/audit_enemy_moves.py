#!/usr/bin/env python3
"""Compare EnemyAI's hand-written move behaviour against the CURRENT decompiled source.

The emulator's data tables are generated, so a dev change to a card's damage or an
enemy's HP is re-extracted and reported by `diff_patch.py`. Monster BEHAVIOUR is not:
318 `new Intent(...)` constructions live in `EnemyAI.cs`, transcribed by hand from the
monster classes, and nothing links them back to what the game now says.

That matters more than it looks, because **a test written from the decompiled source is
a snapshot of the source at the time of writing**. If the devs change a move's damage,
its order or the intents it declares, the emulator keeps the old value, the test keeps
asserting the old value, and the suite stays green. Only a live capture or a check like
this one notices -- and a capture only covers the encounters that have one.

Three checks, each a WORKLIST rather than a verdict:

  hits   every MultiAttackIntent(damage, repeat) the game declares should have a
         matching `Hits:` in the emulator, or the hits are folded into the damage --
         which is a wrong number AND silently under-triggers every per-instance hook
         (E10, E83, E91, E98, E100, E106).

  types  a MoveState declares a LIST of intents and the readout follows the FIRST one.
         Announcing the type of a later one tells a policy the wrong thing about the
         turn (E12, E97, E105, E108).

  shape  the game gives each monster a MonsterMoveStateMachine -- follow-up pointers,
         conditional and random branches, repeat caps, cooldowns. The emulator walks it
         with `MoveIndex % n` arithmetic, which can only express a plain cycle. Of 117
         machines, 16 are plain chains (E93, E95, E100, E106, E107).

    uv run python scripts/audit_enemy_moves.py
    uv run python scripts/audit_enemy_moves.py --check hits
    uv run python scripts/audit_enemy_moves.py --monster Tunneler
"""

from __future__ import annotations

import argparse
import hashlib
import re
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
MONSTERS = REPO / "decompiled" / "MegaCrit.Sts2.Core.Models.Monsters"
ENEMY_AI = REPO / "src" / "Sts2Emulator" / "Core" / "EnemyAI.cs"

MULTI_ATTACK = re.compile(r"new MultiAttackIntent\(\s*([^,]+?)\s*,\s*([^)]+?)\s*\)")
MOVE_STATE = re.compile(r'new MoveState\(\s*"([^"]+)"\s*,\s*\w+\s*,\s*(new [^;]+?)\)\s*(?:\{|;|\))')
INTENT_CALL = re.compile(r"new (\w+)Intent\(")

# What the game's intent classes announce as, in the emulator's IntentType vocabulary.
ANNOUNCED_AS = {
    "SingleAttack": "Attack",
    "MultiAttack": "Attack",
    "DeathBlow": "Attack",
    "Defend": "Defend",
    "Buff": "Buff",
    "Summon": "Buff",
    "Heal": "Buff",
    "Debuff": "Debuff",
    "CardDebuff": "Debuff",
    "Status": "Debuff",
    "DebuffStrong": "Debuff",
    "Sleep": "Unknown",
    "Stun": "Unknown",
    "Hidden": "Unknown",
    "Escape": "Unknown",
}

BRANCHING = ("RandomBranchState", "ConditionalBranchState")

# Monster classes the game ships that no run can meet: mocks, dummies and the
# deprecated base. Listed rather than pattern-matched so a real monster cannot join
# them by having a plausible name.
NOT_A_MONSTER = {
    "BigDummy",
    "DeprecatedMonster",
    "MultiAttackMoveMonster",
    "OneHpMonster",
    "SingleAttackMoveMonster",
    "TenHpMonster",
}

# Where the emulator's KE constant is not the class name.
ALIASES = {"FakeMerchantMonster": "FakeMerchant"}


MACHINE = re.compile(
    r"protected override MonsterMoveStateMachine GenerateMoveStateMachine\(\).*?\n\t\}",
    re.S,
)

COMMENT = re.compile(r"//[^\n]*")


def machine_digest(source: str) -> str:
    """A short fingerprint of this monster's move machine, as the GAME declares it.

    Whitespace-collapsed so a reformat does not count as a change, and taken over
    `GenerateMoveStateMachine` alone -- the states, their intents, the follow-ups and the
    branch arms. That is exactly the thing a `VERIFIED` note is a claim about.
    """
    match = MACHINE.search(source)
    body = match.group(0) if match else source
    return hashlib.sha256(" ".join(body.split()).encode()).hexdigest()[:12]


# Monsters whose flags have been READ against the source and found faithful, with the
# digest of the machine that was read. Each entry is a claim that the emulator expresses
# this machine correctly by other means -- usually by seeding MoveIndex per creature, or
# by rolling through `PickBranch`, neither of which the checks above can see.
#
# The digest is what keeps this from becoming a place where flags go to die: if MegaCrit
# changes the machine, the fingerprint stops matching and the audit says so LOUDLY rather
# than staying quiet on the strength of a reading of the old source. That is the same
# failure this whole script exists to catch, so it must not have it itself.
#
# Add an entry only alongside a test that pins the behaviour. `--digests` prints the
# current fingerprint for every flagged monster, so writing one down is mechanical.
VERIFIED: dict[str, tuple[str, str]] = {
    "CorpseSlug": ("2300db6815f5", "StarterMoveIdx numbers the moves in the same order the cycle walks them, so the seeded MoveIndex IS the cycle"),
    "LagavulinMatriarch": ("94f8def82c8d", "the branch is AsleepPower; the four-cycle below it is the follow-up chain, and MoveIndex parks at 0 while she sleeps"),
    "Myte": ("52652a5c729e", "slot-keyed opening, seeded as a MoveIndex offset by CombatFactory: the second Myte starts two ahead"),
    "Nibbit": ("e603c9d5ab2f", "slot-keyed opening, seeded the same way; alone/front/back pick the starting phase of one three-cycle"),
    "Ovicopter": ("1ebba9f1bb71", "the conditional only chooses what fills the first slot of a three-cycle, which MytesTests' sibling suite pins"),
    "PaelsLegion": ("f7dd883708a7", "one move whose follow-up is itself -- a cycle of one"),
    "PhantasmalGardener": ("4ffd742b8206", "slot-keyed opening plus a conditional, both pinned by PhantasmalGardenersTests"),
    "DecimillipedeSegment": ("5dbbac17e4ab", "the branch is reached only from REATTACH_MOVE, and a reattached segment rolls it through PickBranch (E121)"),
    "SlitheringStrangler": ("9ecb2ba264ee", "CONSTRICT alternates with a ROLLED attack, and the roll goes through PickBranch"),
    "Stabbot": ("", "one move whose follow-up is itself"),
    "TestSubject": ("fe6a9c1b5914", "the conditional is on Respawns, which the emulator keys off the powers each respawn leaves behind; RESPAWN_MOVE's Buff intent is set by CombatEngine, not by the case block, which is why the types check cannot see it"),
    "Toadpole": ("3898f71ee8ea", "slot-keyed opening into a three-cycle, seeded per creature"),
    "TwoTailedRat": ("3b8a03ff4628", "every move returns to a weighted branch, rolled through PickWeightedBranch with the game's weights"),
    "Wriggler": ("d96a5fa02389", "slot-keyed opening into a two-cycle, plus SPAWNED_MOVE for the ones that arrive stunned"),
}


def strip_comments(source: str) -> str:
    """C# with its line comments blanked out.

    Every check here asks what the CODE does, and a comment saying what the code used to
    do reads identically to a regex. The Mawler was reported as walking `MoveIndex % 3`
    for three batches after it stopped doing so, on the strength of the comment explaining
    that it no longer does -- so a fix that documents what it replaced re-flagged itself,
    and the better the comment the longer the false report survived.
    """
    return COMMENT.sub("", source)


def emulator_blocks() -> dict[str, str]:
    """EnemyAI's text for each monster: its `case KE.X:` arms AND its helper methods.

    The case arms alone are not enough -- several monsters are implemented in a method
    named for them (`ExoskeletonIntent`, `FakeMerchantIntent`), and an audit that reads
    only the switch reports those as missing everything they have.
    """
    text = strip_comments(ENEMY_AI.read_text(encoding="utf-8"))
    blocks: dict[str, str] = {}

    marks = [(m.start(), m.group(1)) for m in re.finditer(r"case KE\.(\w+):", text)]
    for index, (start, name) in enumerate(marks):
        end = marks[index + 1][0] if index + 1 < len(marks) else len(text)
        blocks[name] = blocks.get(name, "") + text[start:end]

    for method in re.finditer(r"\n    private static \w+ (\w+?)Intent\(", text):
        name = method.group(1)
        start = method.start()
        nxt = text.find("\n    private static ", start + 1)
        blocks[name] = blocks.get(name, "") + text[start : nxt if nxt > 0 else len(text)]

    return blocks


def move_states(source: str) -> list[tuple[str, list[str]]]:
    """Each MoveState's id and the intents it declares, IN ORDER."""
    states = []
    for name, args in MOVE_STATE.findall(source):
        kinds = [ANNOUNCED_AS.get(k, k) for k in INTENT_CALL.findall(args)]
        if kinds:
            states.append((name, kinds))
    return states


def check_hits(monster: str, source: str, block: str) -> list[str]:
    rows = []
    for damage, repeat in MULTI_ATTACK.findall(source):
        if "Hits:" not in block:
            rows.append(f"    MultiAttackIntent({damage}, {repeat}) with no Hits: in the emulator")
    return rows


def check_types(monster: str, source: str, block: str) -> list[str]:
    rows = []
    for name, kinds in move_states(source):
        if len(kinds) < 2:
            continue
        announced = kinds[0]
        if announced == "Unknown":
            continue
        if f"IntentType.{announced}" not in block:
            rows.append(
                f"    {name} declares {' then '.join(kinds)}, so it announces as "
                f"{announced} -- which the emulator never says for this monster",
            )
    return rows


def is_reachable(source: str, kind: str) -> bool:
    """Whether a branch state of this kind can actually be ENTERED.

    A monster can DECLARE a branch, add its arms and put it in the state list without
    anything ever pointing at it -- the Phrog Parasite's RAND is exactly that, and its two
    moves follow up to each other instead. A declared-but-orphaned branch is not a shape
    the emulator is getting wrong; it is a shape the GAME does not use, and reporting it
    sends the next reader off to add a roll the live game never makes.

    Reachable means the variable holding it turns up in a `FollowUpState` assignment, is
    handed to the machine as its initial state, or is added to a conditional as an arm.
    The chained idiom counts: several monsters write
    `RandomBranchState x = (RandomBranchState)(a.FollowUpState = new RandomBranchState(..))`,
    so the declaration line is itself the assignment.
    """
    for match in re.finditer(rf"(\w+)\s*=\s*(?:\([^)]*\)\s*)?[^;]*new {kind}\(", source):
        name = match.group(1)
        for line in source.splitlines():
            if name not in line:
                continue
            if "FollowUpState" in line or "MonsterMoveStateMachine(" in line:
                return True
    return False


def check_shape(monster: str, source: str, block: str) -> list[str]:
    """Machines whose shape `MoveIndex %` arithmetic cannot express.

    Ranked, because the shapes are not equally suspicious. A **RandomBranchState** cannot
    be a cycle at all: if the emulator's block never rolls, the monster is walking a fixed
    order where the game rolls. A ConditionalBranchState or a slot-keyed opening, on the
    other hand, is often modelled correctly by seeding MoveIndex per creature -- which is
    what the Myte and the Decimillipede do -- so those are a read-the-source prompt rather
    than a finding.

    Two things this check got wrong before, both of which made it over-report:

    - **A declared branch is not a reachable one** (see `is_reachable`).
    - **"Touches rng" was a regex for `rng.`**, and the emulator rolls through
      `PickBranch(eligible, rng)` and `PickWeightedBranch(...)` far more often than it
      calls a method on the stream. Four monsters were reported as walking a fixed order
      while rolling on the line below.
    """
    shapes = [s for s in BRANCHING if s in source and is_reachable(source, s)]
    if re.search(r"(\w+)\.FollowUpState = \1\b", source):
        shapes.append("a move that follows up to ITSELF")
    if "StarterMoveIdx" in source or "SlotName ==" in source:
        shapes.append("an opening keyed to slot or starter index")
    if not shapes:
        return []
    # Only interesting where the emulator walks it with plain modular arithmetic.
    if not re.search(r"MoveIndex\s*%", block):
        return []

    # Any use of the stream, not just a method call on it: the emulator rolls through
    # `PickBranch(eligible, rng)` and `PickWeightedBranch([...], rng)` much more often
    # than it calls `rng.NextDouble()` directly.
    rolls = re.search(r"\brng\b", block) is not None
    if "RandomBranchState" in shapes and not rolls:
        joined = ", ".join(shapes)
        strong = (
            "    ** the game ROLLS (RandomBranchState) and the emulator never touches "
            f"rng -- it walks a fixed order. Also has: {joined}"
        )
        return [strong]
    return [
        f"    walked with `MoveIndex %`; the machine has: {', '.join(shapes)}",
    ]


CHECKS = {"hits": check_hits, "types": check_types, "shape": check_shape}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", choices=[*CHECKS, "all"], default="all")
    parser.add_argument("--monster", default=None, help="audit just this one")
    parser.add_argument(
        "--digests",
        action="store_true",
        help="print each flagged monster's machine digest, for writing into VERIFIED",
    )
    parser.add_argument(
        "--all",
        action="store_true",
        help="report VERIFIED monsters too, instead of counting them",
    )
    args = parser.parse_args()

    blocks = emulator_blocks()
    wanted = list(CHECKS) if args.check == "all" else [args.check]
    flagged = 0
    verified = 0
    modelled = 0
    stale: list[tuple[str, str, str]] = []
    unmapped: list[str] = []

    for path in sorted(MONSTERS.glob("*.cs")):
        monster = path.stem
        if args.monster and monster != args.monster:
            continue
        source = strip_comments(path.read_text(encoding="utf-8"))
        if "GenerateMoveStateMachine" not in source:
            continue
        if monster in NOT_A_MONSTER:
            continue

        block = blocks.get(ALIASES.get(monster, monster))
        if block is None:
            # LOUD, not skipped. A monster the audit cannot find is a monster it silently
            # reports as clean, which is the one failure mode a staleness check must not
            # have -- and a patch that renames a class is exactly when it would happen.
            unmapped.append(monster)
            continue
        modelled += 1

        rows: list[str] = []
        for name in wanted:
            found = CHECKS[name](monster, source, block)
            rows += [f"[{name}]{row}" for row in found]
        if not rows:
            continue

        digest = machine_digest(source)
        if args.digests:
            print(f"{monster}: {digest}")
            continue

        note = VERIFIED.get(monster)
        if note and not args.all:
            if note[0] and note[0] != digest:
                stale.append((monster, note[0], digest))
            else:
                verified += 1
                continue

        flagged += len(rows)
        print(monster)
        print("\n".join(rows))

    if args.digests:
        return

    print(f"\n{flagged} flag(s) across {modelled} modelled monsters")
    if verified:
        print(
            f"{verified} more flagged and VERIFIED -- read against the source, faithful by "
            "other means, machine unchanged since. `--all` reports them anyway.",
        )

    if stale:
        print(
            "\nVERIFIED notes whose MACHINE HAS CHANGED since it was read -- re-read these "
            "before trusting anything about them:",
        )
        for monster, was, now in stale:
            print(f"  {monster}: verified against {was}, the source now digests to {now}")
        raise SystemExit(1)

    if unmapped:
        print(
            f"\n{len(unmapped)} monster(s) have a move machine and no emulator block: "
            + ", ".join(unmapped),
        )
        print(
            "Either the emulator does not implement them, or the class was renamed and "
            "ALIASES needs the new name. Until then this audit says nothing about them.",
        )
        raise SystemExit(1)


if __name__ == "__main__":
    main()
