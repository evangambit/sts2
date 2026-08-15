# Ralph Agent Instructions

You are an autonomous coding agent working on a software project.

## Your Task

1. Read the PRD at `scripts\ralph\prd.json`
2. Read the progress log at `scripts\ralph\progress.txt` (check Codebase Patterns section first)
3. Read the emulator completion plan at `plan.md`
4. Check you're on the correct branch from PRD `branchName`. If not, check it out or create from main.
5. If any PRD user story has `passes: false`, pick the **highest priority** failing story.
6. If every PRD story has `passes: true`, continue emulator-completion work from `plan.md` instead:
   - Start by running `uv run python scripts\ralph\card_coverage.py` when the next task is trace/card related, and run `uv run python scripts\replay_full_run_trace.py traces\full-run\<trace>.json` when a fresh STS2MCP replay-derived trace should drive the batch.
   - Pick the highest-priority incomplete milestone item, priority gap, or replay mismatch cluster.
   - Prefer trace-driven, testable parity work over broad rewrites.
   - Treat the selected plan item or coherent batch as this iteration's story and use an ID like `PLAN-M2-CARD-BATCH` or `PLAN-M3-REWARD-RNG`.
7. Implement one coherent parity batch per iteration:
   - For native cards, batch up to 10 related cards that share mechanics, trace relevance, or reward/shop-pool priority.
   - For run-level work, batch one subsystem or one deterministic replay mismatch cluster.
   - Keep each batch reviewable and avoid unrelated rewrites.
8. Run focused checks while developing, then run full quality checks once before committing (e.g., typecheck, lint, test - use whatever your project requires)
9. Update AGENTS.md files if you discover reusable patterns (see below)
10. If checks pass, commit ALL changes with message: `feat: [Story ID] - [Story Title]`
11. If you completed a PRD story, update the PRD to set `passes: true` for that story. If you completed a plan item, update `plan.md` to record the progress and any remaining work.
12. Append your progress to `scripts\ralph\progress.txt`

## Progress Report Format

APPEND to progress.txt (never replace, always append):

```
## [Date/Time] - [Story ID]
- What was implemented
- Files changed
- **Learnings for future iterations:**
  - Patterns discovered (e.g., "this codebase uses X for Y")
  - Gotchas encountered (e.g., "don't forget to update Z when changing W")
  - Useful context (e.g., "the evaluation panel is in component X")
---
```

The learnings section is critical - it helps future iterations avoid repeating mistakes and understand the codebase better.

## Consolidate Patterns

If you discover a **reusable pattern** that future iterations should know, add it to the `## Codebase Patterns` section at the TOP of progress.txt (create it if it doesn't exist). This section should consolidate the most important learnings:

```
## Codebase Patterns
- Example: Use `sql<number>` template for aggregations
- Example: Always use `IF NOT EXISTS` for migrations
- Example: Export types from actions.ts for UI components
```

Only add patterns that are **general and reusable**, not story-specific details.

## Update AGENTS.md Files

Before committing, check if any edited files have learnings worth preserving in nearby AGENTS.md files:

1. **Identify directories with edited files** - Look at which directories you modified
2. **Check for existing AGENTS.md** - Look for AGENTS.md in those directories or parent directories
3. **Add valuable learnings** - If you discovered something future developers/agents should know:
   - API patterns or conventions specific to that module
   - Gotchas or non-obvious requirements
   - Dependencies between files
   - Testing approaches for that area
   - Configuration or environment requirements

**Examples of good AGENTS.md additions:**

- "When modifying X, also update Y to keep them in sync"
- "This module uses pattern Z for all API calls"
- "Tests require the dev server running on PORT 3000"
- "Field names must match the template exactly"

**Do NOT add:**

- Story-specific implementation details
- Temporary debugging notes
- Information already in progress.txt

Only update AGENTS.md if you have **genuinely reusable knowledge** that would help future work in that directory.

## Quality Requirements

- ALL commits must pass your project's quality checks (typecheck, lint, test)
- Use targeted tests during development when a batch touches one area, but run the full required project check once before committing.
- Do NOT commit broken code
- Keep changes focused and minimal
- Follow existing code patterns

## Browser Testing (If Available)

For any story that changes UI, verify it works in the browser if you have browser testing tools configured (e.g., via MCP):

1. Navigate to the relevant page
2. Verify the UI changes work as expected
3. Take a screenshot if helpful for the progress log

If no browser tools are available, note in your progress report that manual browser verification is needed.

## Stop Condition

After completing an iteration, check both the PRD and `plan.md`.

Only reply with the completion signal if ALL PRD stories have `passes: true` AND `plan.md` no longer lists any incomplete emulator parity gaps, milestones, or future work:
<promise>COMPLETE</promise>

If there are still PRD stories with `passes: false` OR any remaining emulator completion work in `plan.md`, end your response normally without the completion signal. Ralph will start another Copilot iteration.

## Important

- Work on one coherent batch per iteration; do not limit emulator parity work to one card when several related cards can be implemented and validated together.
- Do not treat an all-passing PRD as completion while `plan.md` still describes incomplete emulator work.
- Commit frequently
- Keep CI green
- Read the Codebase Patterns section in progress.txt before starting
