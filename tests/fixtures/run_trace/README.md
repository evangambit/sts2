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
| `DPUJR117FL-a8.json` | Overgrowth | 244 | **diverges** — Cubex Construct's attack lands 3 high (it carries Strength 2 and intends 9; the emulator deals 12). Replay also stops at step 117 on an unsupported `select_card` action, which is a harness gap rather than an engine one. |
| `4KJ7X2MQND-a8.json` | Underdocks | 100 | **diverges** — the Phantasmal Gardener's Skittish power is unmodelled: the first card that hits one each turn should give it 7 block (`SkittishAmount` at A8), and it gets none. The event divergence this trace originally showed is fixed. |

`QS2GYXRKWN-a0.json` was removed: it is an ascension-0 run, and the emulator models
A8 only (`RunEngine.Reset` starts the player at 64/80), so it diverged from step 0
and could never pass.
