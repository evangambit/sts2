# AGENTS.md

## General

- Read the README.md to get an overview for what this project is.
- Whenever you edit a Markdown file, format it afterward with: `bunx --bun prettier --write foo.md`
- Whenever you edit a Python file, format it afterward with: `uv run black --target-version py314 foo.py`
- To validate code changes, run the combined lint/test script: `bash lint-and-test.sh`
- For `Sts2RunEnv` run-level reward logic, prefer decompiled source under `decompiled\` (for example `PotionRewardOdds`, `PotionFactory`, and merchant entry classes) over inferred deterministic shortcuts.
- Full-run replay diagnostics should report available boundary diffs before stopping on unsupported trace actions, and unsupported action errors should include the reference step, state type, and floor.
- Full-run replay may coalesce live reward/event substeps as no-ops when `Sts2RunEnv` has already applied those rewards or advanced to map, so retained traces continue to the next meaningful mismatch.

## Code Organization

- Native card effects that cause player HP loss should use `CardEffects.LoseHp` so Rupture and Inferno hooks stay consistent with other card-effect self-damage.
- Native card effects that exhaust another card from hand should select from `state.Hand` after the played card has already been removed, then call `CardEffects.ExhaustCard` so exhaust hooks stay consistent.
- Native card effects that upgrade hand cards should replace `CardInstance` values in `state.Hand` with upgraded copies; the played card has already been removed before `CardEffects.Apply` runs.
- Native card effects that conditionally draw multiple cards should draw one card at a time and respect the 10-card hand cap so the newly drawn card controls whether drawing continues.
- Native card powers that modify attack play count should live in `CombatEngine.PlayCard`, apply one extra `CardEffects.Apply` for affected Attack cards, decrement their counter per Attack, and expire at end of player turn.
- Native card powers that auto-play cards at the start of the player play phase should run in `CombatEngine` after the normal turn-start draw, bypass energy spending, and still route played Attack cards through normal attack play hooks and discard/exhaust cleanup.
- Native cards with dynamic per-turn costs should compute those costs in `CombatEngine.EffectiveCost` so play validation and energy spending stay aligned.
- Native generated cards that are free only for the current turn should use `CardInstance.FreeThisTurn`; clear it when cards leave hand for discard or exhaust piles.
- Native cards that return themselves before the next turn's draw should queue the played `CardInstance` from `CombatEngine` play lifecycle hooks, then move the matching card from discard/draw/exhaust to hand before normal draw.
- Native X-cost cards should spend current `state.Energy` inside `CardEffects.Apply` after the played card's printed cost has been handled; generated X-cost cards currently encode cost as 0.
- Native card effects that retain the remaining hand should apply a player `BuffId` and let `CombatEngine.EndTurn` skip normal discard for non-ethereal cards, then decrement/remove the retain counter at player side turn end.
- Native card effects that reapply or scale an enemy debuff after dealing damage should keep the pre-damage target reference, verify the target survived, and reuse the relevant debuff hooks.
- Native card effects with multiple actions should use explicit card cases when decompiled effect order matters; do not rely on fallback damage/block ordering.
- Native card effects that move cards from discard to hand should operate after the played card has left hand, clear `FreeThisTurn`, and respect the 10-card hand cap.
- Native card effects that splash based on the first hit should use the effective first-hit HP-loss plus overkill amount, then apply splash as unpowered damage unless decompiled value props say otherwise.
- Native cards that care whether the player lost HP this turn should use `CombatState.PlayerHpLostThisTurn`, reset it at the start of each player turn, and increment it from relevant unblocked player HP-loss paths.
- Native cards that care whether any card exhausted this turn should use `CombatState.CardsExhaustedThisTurn`, increment it only through `CardEffects.ExhaustCard`, and reset it at the start of each player turn.
- Native card effects that trigger when the card itself exhausts should put the hook in `CardEffects.ExhaustCard` so it works for normal self-exhaust and secondary exhaust effects.
- Native card effects that repeat block gain should call `CardEffects.GainBlock` once per decompiled gain so block hooks trigger per gain.
- Native cards that grant next-turn block should store a `BuffId.BlockNextTurn` amount, resolve it after the next player-turn block clear in `CombatEngine`, and grant it as unpowered block.
- Native cards that apply temporary enemy Strength loss should consume Artifact before applying paired `Strength` and `TemporaryStrength` buffs, then restore the enemy Strength in `EnemyAI.ExecuteIntent` at that enemy's turn end.
- Native card powers that modify a played card's destination pile should make that decision in `CombatEngine` after effects resolve but before adding the card to discard.
- Native card powers with extra dynamic variables can be represented with companion `BuffId` entries when `BuffState` needs to track both the visible counter and hidden per-power state.

## STS2MCP

- This project uses a fork of STS2MCP that is located here: `D:\Repositories\STS2MCP`
- Sometimes, we might need to update the mod in order to add/fix API functionality. If we make updates to the mod, we need to:
  - Recompile the mod.
  - Close the running Slay the Spire 2 instance, if it is running.
  - Copy over the DLL files to this directory: `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods`-
- If you need to make a trace from an in-game replay, you can use the `start_replay` API that we added to STS2MCP.
- The root STS2MCP URL (`http://localhost:15526/`) is only a health check. Use `http://localhost:15526/api/v1/singleplayer` for singleplayer state and actions, including replay control commands such as `start_replay`, `get_replay_status`, and `cancel_replay`.

## Slay the Spire 2 Launch Instructions

- **Always launch Slay the Spire 2 through Steam**, not by starting `SlayTheSpire2.exe` directly. Otherwise, the game will fail to initialize with the following error: Steam failed to initialize. Make sure you run the game from Steam.
- Correct CLI launch command on Windows: `Start-Process "steam://rungameid/2868840"`
- After launching through Steam, verify STS2MCP with: `Invoke-WebRequest -UseBasicParsing -Uri "http://localhost:15526/"`
