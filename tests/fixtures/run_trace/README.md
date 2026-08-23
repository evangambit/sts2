# Full-run traces

Whole runs captured from the live game by `scripts/trace_real_game_run.py`, replayed
against the emulator by `scripts/replay_full_run_trace.py`. Nothing runs these
automatically — they are a manual fidelity check, and the strongest one there is:
a per-step comparison of a real run rather than a single option in isolation.

    uv run python scripts/replay_full_run_trace.py tests/fixtures/run_trace/<file>

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
| `KFMKQQA7MS-a8.json` | Overgrowth | 97 | **diverges** at step 23 — the run took Winged Boots from Neow, and its free travel is unmodelled. While `TimesUsed < 3` the relic lets the player move to ANY node on the next row, not just a child, and moving to a non-child spends a charge. The map itself is right: all 77 edges match. `RunConstants.MapChoices` is 4, so the choice arrays cannot even hold a full row of 7 — widening it moves the run observation layout, and `sts2_gym/run_constants.py` restates the 4 rather than reading it. |
| `WK1DEGZD8P-a8.json` | Underdocks | 216 | **diverges** at step 125 — the opening hand of a fight differs. The deck matches in contents AND order, and both sides shuffle it the same way (`UnstableShuffle` on the pile as it stands), so the run-level `Rng.Shuffle` stream is at a different position: some earlier combat drew from it a different number of times. Needs the call count instrumented across the run rather than guessed at. |
| `CF32ERF3DH-a8.json` | Underdocks | 99 | clean |
| `QD1DQCJU2K-a8.json` | Underdocks | 85 | clean |

`QS2GYXRKWN-a0.json` was removed: it is an ascension-0 run, and the emulator models
A8 only (`RunEngine.Reset` starts the player at 64/80), so it diverged from step 0
and could never pass.
