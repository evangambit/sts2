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
| `J09SPL8Y3V-a8.json` | Overgrowth | 190 | **diverges** at step 1: the game opens on `rewards`, the emulator on `event`. Neow's Bones, whose pickup puts something on a reward screen the emulator does not raise. The longest trace here, and blocked at its first step. |

Fifteen of these replay clean and one does not. The six newest were captured after the
set had gone green, and every one of them found something — nine defects between them.

`1UL0BRX8WC` is the pattern. It was taken specifically because the nine before it held
only nine of Neow's relics between them; it drew a tenth, Phial Holster, and diverged on
the very first combat reward. Four defects came out of one run, and three of them had
nothing to do with the relic — they were simply on paths no earlier capture had walked.
The batch of six after it did the same again, and then some: E33-E35, E36-E37, E38, E39,
E40 and E41 all came out of those six runs. Only Neow's Bones (O8) is still open.

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
mentions a choice, so the blessings with a pickup CHOICE never get picked up:
`DPUJR117FL` was offered Lead Paperweight and `KFMKQQA7MS` Hefty Tablet, and both runs
took the safe option beside it. That is not just a variety problem -- those two are the
relics whose stand-in Rewards draw counts (catalogue E30) have nothing to check them
against. `trace_real_game_run.py --neow-option N` takes the one you name.

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

`QS2GYXRKWN-a0.json` was removed: it is an ascension-0 run, and the emulator models
A8 only (`RunEngine.Reset` starts the player at 64/80), so it diverged from step 0
and could never pass.
