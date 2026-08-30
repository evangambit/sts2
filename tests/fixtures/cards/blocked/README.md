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
