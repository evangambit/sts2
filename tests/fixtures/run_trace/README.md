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
| `25TS4F5T37-a8.json` | Overgrowth | 125 | **diverges** at step 122 — `player.block` 3 live, 5 here, which is a Defend the game taxed for Frail and the emulator did not. The Frail itself is not a compared field, so the miscount is invisible until it changes a number. Found and largely fixed E33-E35 first: the Fogmog fight it turns on used to end 35 steps early. |
| `XTLVVPKFBF-a8.json` | Overgrowth | 126 | **diverges** at step 75 (`player.hp` 57 live, 61 here) and never finishes its last fight. Same Fogmog/Eye encounter as `25TS4F5T37`. |
| `L9R346P3YD-a8.json` | Overgrowth | 110 | **diverges** at step 75 (`player.hp` 21 live, 20 here), enemy HP at 91. A Skulking Colony elite, and no illusion involved — a separate cause. |
| `SAM9XS24LM-a8.json` | Overgrowth | 134 | clean — found E36 and E37, a Sewer Clam gaining its Plating block twice and then decaying it a turn early |
| `NXV45HW43K-a8.json` | Overgrowth | 149 | **diverges** at step 126 on a single point of `player.hp` (9 live, 8 here) and nothing else in 149 steps. The narrowest divergence in the set. |
| `J09SPL8Y3V-a8.json` | Overgrowth | 190 | **diverges** at step 1: the game opens on `rewards`, the emulator on `event`. Neow's Bones, whose pickup puts something on a reward screen the emulator does not raise. The longest trace here, and blocked at its first step. |

Eleven of these replay clean and five do not, which is the point rather than a problem:
the six newest were captured after the set had gone green, and every one of them found
something.

`1UL0BRX8WC` is the pattern. It was taken specifically because the nine before it held
only nine of Neow's relics between them; it drew a tenth, Phial Holster, and diverged on
the very first combat reward. Four defects came out of one run, and three of them had
nothing to do with the relic — they were simply on paths no earlier capture had walked.
The batch of six after it did the same again: five defects closed (E33-E35 from the first
capture, E36-E37 from another) and five divergences still open across the rest.

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
