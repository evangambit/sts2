"""Print a relic's decompiled source next to every line of the emulator that mentions it.

The card version of this (`card_pair.py`) is what made the card reading passes bearable,
and relics are the same job one layer out: 171 of 296 are modelled and 107 of those have
never been compared to anything.

Relics are harder to pair than cards, and the difference is the whole reason this exists.
A card has ONE `case` in a switch, so `card_pair.py` can print it. A relic has no home:
`RelicEffects.cs` is a set of per-HOOK functions, and a relic's behaviour is however many
`HasRelic(state, X)` tests are scattered across them -- plus anything in `RunEngine.cs` or
`CombatEngine.cs`. So this greps the whole engine for the id constant and prints each hit
with the FUNCTION it sits in, because which hook a test lives in is most of what a relic
reading has to check.

    uv run python scripts/relic_pair.py --list          # what is left, by pool
    uv run python scripts/relic_pair.py Akabeko Vajra   # source vs emulator

It reads `audit_relics.READ`, so a relic drops off `--list` the moment it is recorded.
"""

import collections
import pathlib
import re
import sys

sys.path.insert(0, "scripts")
import audit_relics as A

REPO = pathlib.Path(__file__).resolve().parent.parent
RELICS = REPO / "decompiled" / "MegaCrit.Sts2.Core.Models.Relics"
ENGINE = REPO / "src" / "Sts2Emulator"

# Cosmetic lines, the same categories `card_pair.py` drops -- with the standalone
# `CardCmd.PreviewCardPileAdd(x);` form, because the WRAPPING form hides a real effect and
# dropping it once cost a card (see E336).
SKIP = (
    "using",
    "namespace",
    "///",
    "public sealed class",
    "ArgumentNullException",
    "HoverTip",
    "TriggerAnim",
    "WithHitFx",
    "Flash()",
    "SfxCmd",
    "VfxCmd",
    "NCombatRoom",
)

# NOT stripped, though it is tempting: a relic that shows a counter carries thirty lines of
# display plumbing -- `IsActivating`, `DisplayAmount`, a `DoActivateVisuals` that waits a
# second -- and Kunai's real behaviour is four lines inside eighty. Dropping those lines
# individually leaves orphaned braces, which reads WORSE than the noise, and E336 is the
# standing reason not to hide source from a reader: a tool that shows part of a card
# produces confident wrong readings. Skim the noise; do not let the tool guess for you.
PREVIEW_ONLY = re.compile(
    r"^\s*CardCmd\.PreviewCardPileAdd\([\w.]+(,\s*[\d.f]+)?\);\s*$",
)
FUNC = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)?(?:public|private|internal|protected)[\w<>,\s\[\]?]*\s(\w+)\s*\(",
)


def source_of(name: str) -> list[str]:
    path = RELICS / f"{name}.cs"
    if not path.exists():
        return [f"    (no decompiled source at {path.relative_to(REPO)})"]

    out = []
    for line in path.read_text(encoding="utf-8").split("\n"):
        if not line.strip() or any(k in line for k in SKIP) or PREVIEW_ONLY.match(line):
            continue
        out.append(line.replace("\t", "    "))
    return out


def engine_hits(name: str) -> list[tuple[pathlib.Path, int, str, str]]:
    r"""Find every engine line naming the relic, with the function it sits in.

    BOTH aliases, because several relics have two constants for the same id --
    `RelicEffects.LizardTail` and `RunConstants.RelicLizardTail`. A bare `\bName\b` search
    misses the prefixed one (there is no word boundary inside `RelicLizardTail`), and the
    first thing this tool did with that regex was report Burning Blood as unmodelled when
    it is wired up under the other name. `audit_relics.id_constants` already matches on the
    name AND the value for the same reason.
    """
    pattern = re.compile(rf"(?<![A-Za-z0-9_])(?:Relic)?{name}(?![A-Za-z0-9_])")
    hits = []
    for path in sorted(ENGINE.rglob("*.cs")):
        if "bin" in path.parts or "obj" in path.parts or "Generated" in path.parts:
            continue
        lines = path.read_text(encoding="utf-8").split("\n")
        enclosing = "?"
        for i, line in enumerate(lines):
            if (m := FUNC.match(line)) and "=>" not in line[: m.end()]:
                enclosing = m.group(1)
            if pattern.search(line):
                hits.append((path, i + 1, enclosing, line.rstrip()))
    return hits


def main() -> None:
    args = sys.argv[1:]
    if args and args[0] == "--list":
        want = args[1] if len(args) > 1 else None
        unread = A.unread_names(reachable=False)
        pools = A.relic_pools()
        counted = collections.Counter(pools.get(n, "<none>") for n in unread)
        if not want:
            print(f"modelled but unread: {len(unread)}")
            for k, v in counted.most_common():
                print(f"  {k:14} {v}")
        else:
            names = [n for n in unread if pools.get(n) == want]
            print(f"{want} ({len(names)}):")
            print("\n".join(names))
        return

    for name in args:
        print("=" * 78)
        print(f"### {name}")
        print("--- SOURCE")
        print("\n".join(source_of(name)))
        print("--- EMULATOR")
        hits = engine_hits(name)
        if not hits:
            print("    (the constant is not named anywhere in the engine)")
        for path, line_no, func, text in hits:
            rel = path.relative_to(REPO)
            print(f"{rel}:{line_no}  [{func}]")
            print(f"    {text.strip()}")


if __name__ == "__main__":
    main()
