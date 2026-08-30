# Captures the generator cannot rebuild yet

Ground truth from the live game that `generate_card_capture_tests.py` refuses, for a
reason that is about the EMULATOR rather than about the capture. They live here so the
refusal does not block every other capture from generating, and so the recording is not
thrown away — each one becomes a test the day the gap it names is closed.

The generator globs `../*.json` and never looks in here.

- `Eradicate-necrobinder-AeonglassBoss.json` — the Aeonglass carries a
  `WITHERING_PRESENCE_POWER` the emulator has no BuffId for. Eradicate needed a boss
  because at nine energy it kills any act-one elite outright (E292), and this is the boss
  it got. The card itself is read and tested; what is missing is the enemy.
- `VoidForm-regent-ByrdonisElite.json` — the card ENDS THE TURN as part of playing it
  (`PlayerCmd.EndTurn(canBackOut: false)` inside its OnPlay), and the capture caught the
  board mid-transition: the player's turn had ended (`turn: enemy`, hand flushed) but the
  enemies had not acted. The emulator's EndTurn runs the enemies' whole turn atomically, so
  there is no moment in it that matches. **A card that ends the turn has no snapshot the
  capture tool can take**, and its behaviour lives in `VoidFormTests` instead.
- `Chaos-defect-ByrdonisElite.json` — the card rolls its orb on
  `Rng.CombatOrbGeneration`, and a rebuilt fight starts that stream at zero while the live
  run's had advanced. The game channelled Frost and the emulator Plasma from the same
  logic: `_validOrbs` is in the emulator's exact enum order and `NextItem` is the same draw,
  so nothing is wrong but the POSITION.

  This is the Sword Boomerang shape (see HANDOFF: "capture randomly-targeted cards against
  a single enemy or not at all"), and it has a way out nobody has built: the mod could
  report each named stream's raw seed and call count, and the generator could stage
  `CountingRandom` at that position — the emulator already models the streams that way. That
  would make every RNG-dependent card capturable across all four pools. Until then a card
  that rolls is read, not captured.
