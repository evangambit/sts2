# Full-run traces

Whole runs captured from the live game by `scripts/trace_real_game_run.py`, replayed
against the emulator by `scripts/replay_full_run_trace.py`. Nothing runs these
automatically — they are a manual fidelity check, and the strongest one there is:
a per-step comparison of a real run rather than a single option in isolation.

    uv run python scripts/replay_full_run_trace.py tests/fixtures/run_trace/<file>

Every divergence these have found, fixed or open, is catalogued in
`docs/divergence-catalog.md` with its cause and its seed.

Seeded capture requires custom mode (standard mode refuses a chosen seed), and the
run's FIRST act is seed-dependent — roughly half of all seeds open in Underdocks
rather than Overgrowth. "Act 1" in a trace means the run's first act, whichever it
is.

| trace | first act | steps | status |
| --- | --- | --- | --- |
| `QS2GYXRKWN-a8.json` | Overgrowth | 158 | clean |
| `Y75EFT6EDV-a8.json` | Overgrowth | 183 | clean |
| `4KJ7X2MQND-a8.json` | Underdocks | 100 | clean |
| `DPUJR117FL-a8.json` | Overgrowth | 244 | clean |
| `41TJ3T2Y0Q-a8.json` | Overgrowth | 96 | clean |
| `KFMKQQA7MS-a8.json` | Overgrowth | 97 | clean — the run that pinned Winged Boots' free travel (catalogue E22) |
| `WK1DEGZD8P-a8.json` | Underdocks | 216 | clean — the longest of these, and the one that found five defects at once (catalogue E23-E26, H7) |
| `CF32ERF3DH-a8.json` | Underdocks | 99 | clean |
| `QD1DQCJU2K-a8.json` | Underdocks | 85 | clean |
| `1UL0BRX8WC-a8.json` | Overgrowth | 121 | clean — the first capture taken after the set went green, and it found four defects (catalogue E29-E32) |
| `25TS4F5T37-a8.json` | Overgrowth | 125 | clean — found E33-E35 (a Fogmog fight that ended 35 steps early) and then E39, a Flyconid's spores that dealt their damage without their Frail |
| `XTLVVPKFBF-a8.json` | Overgrowth | 126 | clean — the same Fogmog/Eye encounter as `25TS4F5T37`, and closed by the same two fixes |
| `L9R346P3YD-a8.json` | Overgrowth | 110 | clean — found two on its own: E40 (Doors of Light and Dark upgrading off the wrong stream AND the wrong sort key) and E41 (a Skulking Colony that did not read as an elite, so Booming Conch never fired) |
| `SAM9XS24LM-a8.json` | Overgrowth | 134 | clean — found E36 and E37, a Sewer Clam gaining its Plating block twice and then decaying it a turn early |
| `NXV45HW43K-a8.json` | Overgrowth | 149 | clean — the narrowest divergence in the set, one point of HP at step 126, and it was E38: The Chosen Cheese did nothing at all |
| `J09SPL8Y3V-a8.json` | Overgrowth | 190 | clean — the longest trace here, and it was blocked at step 1 until Neow's Bones was modelled properly (E42) |
| `25TS4F5T37-a8-leadpaperweight.json` | Overgrowth | 100 | clean — the SAME seed as `25TS4F5T37-a8.json`, captured with `--neow-option 1` to take a blessing the auto-player will never pick. Settled half of E30's debt (E43) |
| `XTLVVPKFBF-a8-heftytablet.json` | Overgrowth | 148 | clean — likewise `--neow-option 2` on the seed of `XTLVVPKFBF-a8.json`. Settled the other half, and found H11 on the way |
| `SAM9XS24LM-a8-goldenpearl.json` | Overgrowth | 118 | clean on first contact — Golden Pearl's 150 gold was already right |
| `J09SPL8Y3V-a8-precisescissors.json` | Overgrowth | 112 | clean — found E44 and E45, and then E46: a Gremlin Merc stealing 20 gold from a player its own attack had just killed, which showed up only in the run's final snapshot |
| `SAM9XS24LM-a8-precariousshears.json` | Overgrowth | 25 | clean — **cut to its trustworthy prefix**: the game wedged mid-run and steps 26-28 record a broken game rather than ground truth. Found E44 and H12 |
| `25TS4F5T37-a8-leafypoultice.json` | Overgrowth | 101 | clean on first contact — Leafy Poultice needed no fix |
| `NXV45HW43K-a8-cursedpearl.json` | Overgrowth | 101 | clean — found E47. Cursed Pearl's Greed sat in the deck from floor one, and it was Doors of Light and Dark that made that visible: one extra name in a shuffled candidate list is a different pick |
| `RRRR6WR3C4-a8-silvercrucible.json` | Overgrowth | 113 | clean — found E48, a `||` that short-circuited the reward upgrade roll, so every card the rewards stream produced after the first was somebody else's |
| `RRRR6WR3C4-a8-pomander.json` | Overgrowth | 146 | clean — the same seed as `RRRR6WR3C4-a8-silvercrucible.json` on a different blessing. Found E49 at step 1 and then E53 at step 113, an event's reward screen that never gave the event its page back |
| `P14DQ9GNPW-a8-smallcapsule.json` | Overgrowth | 85 | clean — found E51: a relic granted where the game offers it on a screen |
| `N11HWGCNUN-a8-newleaf.json` | Overgrowth | 106 | clean — found E50, the last blessing still riding the pre-`BeginDeckSelection` transform path |
| `P5E6EWCMDW-a8-stonehumidifier.json` | Overgrowth | 91 | clean — the blessing itself (E52) was not modelled at all, and the trace's own divergence was H13: a Gas Bomb's DeathBlow intent, which the comparison had been declining to read |
| `9V9WN98106-a8-neowstalisman.json` | Overgrowth | 129 | clean — the blessing was fine and the run was not. Found E55 (the Gremlin Merc's fight paying no gold, and skipping the DRAW for it) and E56 (stolen gold handed back mid-combat instead of claimed from the screen) |
| `ZY1E5128P6-a8-scrollboxes.json` | Overgrowth | 112 | clean — the last of the twenty-five blessings, and the only one that needed a screen built from scratch (E57): `RunPhase.BundleSelect`, two bundles of three, answered with a select and then a confirm |
| `8QKMNR4T2W-a8-buff200.json` | Overgrowth | 216 | clean — the first BUFFED capture in the set (`--buff-max-hp 200 --upgrade-deck`), reaching floor 12. Found E59 at step 7: Gremlin Horn, handed over by Large Capsule on floor one, was not modelled at all |

Twenty of the twenty-one replay clean. Every capture taken after the set first went green
has found something — thirteen defects and six harness gaps between them.

`1UL0BRX8WC` is the pattern. It was taken specifically because the nine before it held
only nine of Neow's relics between them; it drew a tenth, Phial Holster, and diverged on
the very first combat reward. Four defects came out of one run, and three of them had
nothing to do with the relic — they were simply on paths no earlier capture had walked.
The batch of six after it did the same again, and then some: E33-E35, E36-E37, E38, E39,
E40, E41 and E42 all came out of those six runs.

So the cheapest next captures are the ones that walk somewhere new: a Neow relic not in
the list below, an event none of these hit, or an act 2 (all ten of these die on floors
5-17, so no committed trace covers one).

Which relics a seed CAN offer is knowable with no game running -- Neow's three options are
seed-deterministic and the emulator models the stream, checked against all ten of these:

```bash
uv run python scripts/screen_neow_seeds.py --count 6
uv run python scripts/screen_neow_seeds.py --want LeadPaperweight,HeftyTablet
```

**Offered is not taken.** The auto-player's Neow policy skips any option whose text
mentions a choice, so the blessings with a pickup CHOICE never get picked up on their own:
`DPUJR117FL` was offered Lead Paperweight and `KFMKQQA7MS` Hefty Tablet, and both runs
took the safe option beside it. `trace_real_game_run.py --neow-option N` takes the one you
name, and the last two rows above are what that bought: two blessings no run had ever
taken, three defects, and the end of E30's standing debt. **A seed can be captured more
than once** this way -- those two share their seeds with the plain captures above, and the
runs diverge from the first decision onward.

**Buffed captures.** `--buff-max-hp N --upgrade-deck` applies the same buff to the live
run and to the emulator at the same step, so the run gets deep enough to reach screens the
scripted player otherwise dies before seeing. It is honest differential evidence — the game
is still the reference for every step, and neither buff rolls anything. The first three
buffed runs found three defects after thirty unbuffed traces had gone green, including an
elite reward-ordering bug (E58) that no committed trace could have caught, because not one
of them had ever claimed a combat relic reward.

The table below is keyed by SEED and names the blessing that seed's plain capture took.
Most of the forced-option traces are not in it: `25TS4F5T37-leadpaperweight`,
`XTLVVPKFBF-heftytablet`, `SAM9XS24LM-goldenpearl`, `J09SPL8Y3V-precisescissors`,
`SAM9XS24LM-precariousshears`, `NXV45HW43K-cursedpearl`, `25TS4F5T37-leafypoultice` and
`RRRR6WR3C4-pomander` each take a different blessing on a seed that already appears here,
which is the point of them. Fourteen blessings covered that no auto-played run would ever
have reached. **All twenty-five the screener knows about are now captured and replay
clean**, so the screener finds nothing new and the next capture has to be chosen on some
other axis — an act 2, an unwalked event, a fight nobody has lost yet.

| trace | Neow relic |
| --- | --- |
| `41TJ3T2Y0Q` | Silken Tress |
| `4KJ7X2MQND` | Large Capsule |
| `CF32ERF3DH` | Lost Coffer |
| `DPUJR117FL` | Fishing Rod |
| `KFMKQQA7MS` | Winged Boots |
| `QD1DQCJU2K` | Arcane Scroll |
| `QS2GYXRKWN` | Kaleidoscope |
| `WK1DEGZD8P` | Nutritious Oyster |
| `Y75EFT6EDV` | Neow's Torment |
| `1UL0BRX8WC` | Phial Holster |
| `25TS4F5T37` | Lava Rock |
| `XTLVVPKFBF` | Nutritious Oyster |
| `L9R346P3YD` | Booming Conch |
| `SAM9XS24LM` | Nutritious Oyster |
| `NXV45HW43K` | Winged Boots |
| `J09SPL8Y3V` | Neow's Bones |
| `RRRR6WR3C4` | Silver Crucible |
| `P14DQ9GNPW` | Small Capsule |
| `N11HWGCNUN` | New Leaf |
| `P5E6EWCMDW` | Stone Humidifier |
| `9V9WN98106` | Neow's Talisman |
| `ZY1E5128P6` | Scroll Boxes |

`QS2GYXRKWN-a0.json` was removed: it is an ascension-0 run, and the emulator models
A8 only (`RunEngine.Reset` starts the player at 64/80), so it diverged from step 0
and could never pass.
