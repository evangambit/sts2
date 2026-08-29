"""Print a card's decompiled source next to the emulator's `case` for it.

The tested-but-unread burn-down (see HANDOFF) is ~180 cards, and reading each one by
opening two files is slow enough that it does not get done. This dumps both sides,
stripped of vfx/anim/sfx boilerplate, so a batch of eight can be reviewed in one pass.

    uv run python scripts/card_pair.py --list            # what is left, by pool
    uv run python scripts/card_pair.py --list Ironclad   # the names in one pool
    uv run python scripts/card_pair.py Anger Armaments   # source vs emulator

It reads `audit_cards.READ`, so a card drops off `--list` the moment it is recorded.
"""

import sys, re, pathlib, collections
sys.path.insert(0, "scripts")
import audit_cards as A

gen, pend, impl = set(A.generated_names()), set(A.pending_names()), set(A.implemented_names())
unread = sorted(n for n in gen if n in impl and n not in pend and n not in A.READ)
pools = {}
for f in pathlib.Path("decompiled/MegaCrit.Sts2.Core.Models.CardPools").glob("*.cs"):
    for c in re.findall(r"Card<(\w+)>\(\)", f.read_text()):
        pools.setdefault(c, f.stem.replace("CardPool", ""))

if sys.argv[1:] and sys.argv[1] == "--list":
    want = sys.argv[2] if len(sys.argv) > 2 else None
    c = collections.Counter(pools.get(n, "<none>") for n in unread)
    if not want:
        print(f"tested but unread: {len(unread)}")
        for k, v in c.most_common(): print(f"  {k:12} {v}")
    else:
        sel = [n for n in unread if pools.get(n) == want]
        print(f"{want} ({len(sel)}):"); print("\n".join(sel))
    raise SystemExit

CE = pathlib.Path("src/Sts2Emulator/Core/Effects/CardEffects.cs").read_text().split("\n")
for name in sys.argv[1:]:
    src = pathlib.Path(f"decompiled/MegaCrit.Sts2.Core.Models.Cards/{name}.cs")
    print("=" * 78); print(f"### {name}")
    SKIP = ("using", "namespace", "///", "public sealed class", "ArgumentNullException",
            "HoverTip", "TriggerAnim", "WithHitFx", "Flash()", "SfxCmd", "PreviewCardPileAdd(")
    body = []
    for l in src.read_text().split("\n"):
        s = l.strip()
        if not s or s in ("{", "}") or any(k in l for k in SKIP):
            continue
        body.append(s)
    print("--- SOURCE"); print("\n".join(body))
    print("--- EMULATOR")
    hits = [i for i, l in enumerate(CE)
            if re.search(rf'case (IC|SI|CL|ST|NB|RG)\.{name}:|case "{name}":', l)]
    for h in hits:
        j = h
        while j < len(CE) and j < h + 30:
            print(f"{j+1}: {CE[j]}")
            if re.match(r"\s*(break|return)\b", CE[j]) and j > h: break
            j += 1
        print("    ---")
    if not hits: print("    (no case found)")
