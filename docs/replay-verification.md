# Full-Run Replay Verification — Design

## Goal: replace "zero bugs" with a measurable fidelity metric

"Zero bugs across all characters" is not an achievable guarantee — the bug surface
is the **interaction space** (card × relic × power × enemy combinations), which is
combinatorial and can't be covered by per-effect unit tests. Reframe the target:

> **Fidelity = % of a diverse, per-character corpus of real runs that the emulator
> replays bit-exact.** Drive it toward 100%; every divergence is a localized bug
> with a precise repro.

This makes correctness a dashboard, not a vibe, and tells you when a character is
trustworthy enough to train on.

## What already exists (build on, don't reinvent)

- **Trace format** — `scripts/trace_real_game_run.py` captures
  `{"seed": ..., "steps": [{step, action_payload, result, state, note}]}` via
  `append_snapshot(...)`.
- **Replay + compare** — `scripts/replay_full_run_trace.py` replays a reference trace
  in the emulator and compares at **room-boundary transitions** (floor change,
  combat↔non-combat) using `compare_traces.summary()`/`get_path()`, with field-skips
  for known-benign diffs (`state_type` naming, gold at reward screens).
- **The gaps**: (1) capturing clean reference traces needs the **`start_replay` API**
  from Zamiell's RunReplays fork (not in our mod); (2) comparison is boundary-level,
  not strict per-step; (3) no corpus structure or fidelity metric; (4) v0.107.1 driver
  fixes (seeded embark, Neow timing) still pending.

## Architecture

```
 real game  ──STS2MCP──►  capture  ──►  corpus/<char>/<policy>/<seed>.trace.json
 (+RunReplays start_replay)                         │
                                                    ▼
 emulator  ◄── replay(seed, actions) ── replay_full_run_trace.py
                                                    │
                            per-step + boundary compare │
                                                    ▼
                            first divergence → full-state dump (the bug)
                                                    │
                                                    ▼
                            fidelity metric: replay-exact % per character  (CI gate)
```

### 1. Capture (real game → reference trace)

- **Policy diversity is the whole game.** A corpus only verifies what it plays:
  - *Human runs* — realistic lines.
  - *Random / fuzz agent* — bizarre card+relic combos humans avoid (best interaction
    coverage).
  - *Bot runs* — the states the agent actually visits (highest training value; add
    once MCTS exists). Fidelity where the agent goes is what matters; fidelity in
    never-visited states is academic.
- **Mechanism.** Drive the real game over STS2MCP recording `(seed, action, state)`
  per step. Live capture (what `trace_real_game_run.py` does) works but carries UI
  timing/nondeterminism (cf. the Neow-event race we hit). For clean per-step state,
  **`start_replay` re-runs a recorded trace deterministically** — adopt it for the
  canonical corpus; keep live capture for quick spot-checks.
- **`start_replay` is a self-contained mod action, NOT a RunReplays dependency**
  (investigated 2026-08-15). The game's built-in replay is combat-only (a multiplayer
  desync checksum, `MegaCrit.Sts2.Core.Multiplayer.Replay`) and unusable for full
  runs; the RunReplays mod does full-run replay but exposes no programmatic API. So
  `start_replay` is implemented in our STS2MCP fork like the debug actions: it takes a
  recorded harness trace `{seed, character, actions[]}`, embarks a fresh seeded run,
  and replays the action payloads **one per settled/actionable frame** through the
  existing `ExecuteAction` dispatch, ticked on the mod's `SceneTree.ProcessFrame`
  hook. Fire-and-forget + poll: `start_replay` / `get_replay_status` / `cancel_replay`.
  The hard part is the "settled + actionable" gate (mod-side port of the harness's
  `is_actionable_state` / `IsPlayPhase`) — a wrong gate fires an action before the
  game is ready. Register the three verbs *before* `ExecuteAction`'s
  run-in-progress guard, since `start_replay` begins at the main menu.
- **Store compact.** `(seed, actions[], per-step state-hash[])`. Full state only for a
  small sample and on-demand when a hash diverges (the RNG-hash trick).

### 2. Corpus (organized, versioned)

- Layout: `corpus/<character>/<policy>/<seed>.trace.json`
  (e.g. `corpus/ironclad/random/FORCE_00042.trace.json`).
- Per-trace metadata: **game version** (e.g. `v0.107.1`), character, policy, capture
  date, outcome (win / loss / floor reached).
- **Version-pinned.** A trace belongs to one game version. After a patch, a trace that
  now diverges is *either* a game change *or* an emulator bug — the diff tells you
  which. Re-capture or mark stale; never silently reuse across versions.
- Freeze a small curated golden set; bulk it out with automated agents.

### 3. Replay + compare (two granularities)

- **Boundary comparison** (exists) — compare emulator vs reference at room
  transitions. Robust against intra-turn UI-state timing. Good default; keep it.
- **Strict per-step hash** (add) — a canonical hash of the emulator's *semantic*
  state after each action, compared to the game's. Localizes a divergence to the exact
  action, not just the room. **The crux is a canonical state serialization** shared by
  capture and emulator that excludes cosmetic/nondeterministic fields (animation, UI,
  timestamps, ordering). Get this wrong and false divergences drown the real ones —
  so grow it from the boundary comparator's existing field-skip logic, incrementally.
- On first divergence: dump full state from both sides → that *is* the bug repro.

### 4. Fidelity metric (the dashboard / CI gate)

- Per character: **replay-exact %** over the corpus, at both boundary and per-step
  granularity.
- Track over time; **gate merges** — a card-logic change must not regress the metric.
- "Trustworthy for training" = metric above threshold on a large, diverse corpus.

## Key risks / decisions

- **Canonical state hashing is the hard part.** Normalize semantic state, exclude the
  cosmetic. Start boundary-level (already solved for the skipped fields), add per-step
  hashing field-by-field.
- **RNG completeness.** Full-run replay exercises *all* run-RNG subsystems — map gen,
  Neow, card rewards, shops, events, potions, combat — in order. We've only validated
  combat-setup RNG for seed 0. **Expect the first full-run replays to surface gaps in
  map/reward/shop/event RNG.** That is the point, not a setback.
- **Capture fidelity.** Prioritize the `start_replay` fork for deterministic
  re-capture; live capture is noisier.
- **Divergence triage.** Classify each: (a) real logic bug → fix the sim; (b) benign
  representation diff → fix the comparator; (c) game-patch drift → re-capture. Only (a)
  is a sim defect. Don't let (b)/(c) inflate the bug count or block training.

## Build order

1. **Implement `start_replay` (+ `get_replay_status` / `cancel_replay`) as a
   self-contained STS2MCP mod action** (no RunReplays dependency; see §1 Capture).
   The deterministic trace-replay driver is the clean-capture mechanism.
2. **Capture pipeline** → `corpus/<char>/<policy>/` with version-pinned metadata.
3. **Strict per-step canonical hash** on both sides, extending the boundary comparator.
4. **Fidelity metric + CI gate** (replay-exact % per character).
5. **Bug-fix loop**: replay corpus → localize first divergence → fix → re-run;
   loop-until-dry per character.
6. **Expand**: more seeds, more policies, more characters; add bot runs once MCTS lands.

## Relationship to the rest of the plan

This is the concrete form of §3's "large golden-replay suite" and the honest answer to
"how do we know a character is correct." It subsumes the encounter-level differential
testing (`validate_real_game_trace.py`) — that stays useful for fast targeted checks,
but full-run replay is the primary fidelity signal because it alone exercises the
interaction and run-level state that per-encounter tests can't reach.
