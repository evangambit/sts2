# Captures the generator cannot rebuild yet

Ground truth from the live game that `generate_card_capture_tests.py` refuses, for a
reason that is about the EMULATOR rather than about the capture. They live here so the
refusal does not block every other capture from generating, and so the recording is not
thrown away — each one becomes a test the day the gap it names is closed.

The generator globs `../*.json` and never looks in here.

**It is currently empty, and that is the point of it.** Every capture that was set aside
here has been rebuilt, and each one named something real:

- `Nightmare` — no `BuffId` for `NIGHTMARE_POWER`. The card's outcome was right and its
  board was blank; the refusal was a divergence filed as housekeeping (E425).
- `Eradicate` — no `BuffId` for `WITHERING_PRESENCE_POWER`, because the Aeonglass did not
  have the power at all. A 535-HP boss whose whole gimmick is handing out a Wither every
  six cards was handing out none (E426).
- `Voltaic` and `Supermassive` — cards that read the combat's HISTORY rather than its
  board. The mod now reports `battle.history` and the generator stages the counts, which
  is the only way a pile snapshot can be replayed into "orbs channelled this combat".
- `Chaos` — a card that ROLLS. The mod now reports every named stream's `seed` and
  `counter` and the generator stages `CountingRandom(seed, counter)`, so a rebuilt fight
  draws the number the live run drew. This is the one the old note said "nobody has
  built", and it applies to every capture, not only the ones that were blocked.
- `VoidForm` — a card that ENDS THE TURN, whose snapshot the old note said did not exist.
  It does; it is just on the far side. The capture now waits for the next player turn and
  the rebuild runs the enemy's turn to meet it, which needs the enemy's announced INTENT
  staged as well.

Add a capture back here when the generator refuses one, with a note saying what is
missing. A fixture in this directory is a to-do, not a quarantine.
