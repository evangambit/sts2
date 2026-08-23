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
| `4KJ7X2MQND-a8.json` | Underdocks | 100 | **diverges** — the Sludge Spinner announces its OIL_SPRAY as a Debuff carrying damage where the game announces an Attack. `MoveState("OIL_SPRAY_MOVE", …, new SingleAttackIntent(…), new DebuffIntent())` lists the attack first, and the capture reads `Attack '8'` then `Debuff`. Several enemies model the attack as the SECONDARY intent this way, so it likely needs one pass rather than a one-line fix. |
| `DPUJR117FL-a8.json` | Overgrowth | 244 | **diverges** — the game skips Sunken Statue, the first entry in its own event sequence, which has no `IsAllowed` of its own and had not been visited. Something consumes the sequence that we have not found; the `select_card` stop a step later is downstream of it, not a separate gap. |

`QS2GYXRKWN-a0.json` was removed: it is an ascension-0 run, and the emulator models
A8 only (`RunEngine.Reset` starts the player at 64/80), so it diverged from step 0
and could never pass.
