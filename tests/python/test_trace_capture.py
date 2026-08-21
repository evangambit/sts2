"""The capture loop's rule for when an action is finished.

Getting this wrong is expensive and quiet: the trace still looks plausible, and every
divergence it produces gets blamed on the emulator. Two bugs have lived here -- taking
the snapshot before the action was applied at all, and taking it from the middle of the
action's resolution -- so both are pinned.
"""

import importlib
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "scripts"))

start_real_game_run = importlib.import_module("start_real_game_run")
trace_real_game_run = importlib.import_module("trace_real_game_run")


def combat(hp: int, enemy_hp: int, hand: int = 3) -> dict:
    """Build a state the capture calls actionable, distinguished by its hp values."""
    return {
        "state_type": "monster",
        "battle": {
            "turn": "player",
            "is_play_phase": True,
            "round": 1,
            "enemies": [{"entity_id": "E_0", "hp": enemy_hp, "max_hp": 50}],
        },
        "run": {"act": 1, "floor": 3, "ascension": 8},
        "player": {
            "hp": hp,
            "max_hp": 80,
            "hand": [{"id": "STRIKE_IRONCLAD"} for _ in range(hand)],
        },
    }


class WaitForStateToChangeTests(unittest.TestCase):
    @staticmethod
    def drive(states: list[dict], **kwargs: object) -> dict:
        """Run the wait against a scripted sequence of poll results."""
        remaining = list(states)
        served: list[dict] = []

        def fake_get_state(_base_url: str) -> dict:
            state = remaining.pop(0) if remaining else served[-1]
            served.append(state)
            return state

        original = start_real_game_run.get_state
        start_real_game_run.get_state = fake_get_state
        try:
            return trace_real_game_run.wait_for_state_to_change(
                "http://unused",
                trace_real_game_run.compact_state(states[0]),
                delay=0.0,
                timeout=3.0,
                **kwargs,
            )
        finally:
            start_real_game_run.get_state = original

    def test_waits_for_the_action_to_be_applied_at_all(self):
        before = combat(hp=60, enemy_hp=50)
        after = combat(hp=60, enemy_hp=44)
        result = self.drive([before, before, after, after, after, after])

        self.assertEqual(44, result["battle"]["enemies"][0]["hp"])

    def test_ignores_a_frame_from_the_middle_of_the_resolution(self):
        # The Artifact is spent, then the damage lands. Both are changes; only the
        # second is the result of the action that was posted.
        before = combat(hp=60, enemy_hp=50)
        mid = combat(hp=60, enemy_hp=50, hand=2)
        settled = combat(hp=60, enemy_hp=44, hand=2)
        result = self.drive([before, mid, settled, settled, settled, settled])

        self.assertEqual(44, result["battle"]["enemies"][0]["hp"])

    def test_a_state_that_never_settles_falls_through_rather_than_hanging(self):
        # Snapshot changing on every poll: report whatever is current when time runs
        # out, so the capture records a stuck step instead of blocking forever.
        churn = [combat(hp=60, enemy_hp=50 - i) for i in range(200)]
        result = self.drive(churn, settle_polls=3)

        self.assertIn("battle", result)


class ScriptedActionTests(unittest.TestCase):
    def test_recorded_actions_keeps_order_and_drops_the_opening_snapshot(self):
        fixture = (
            Path(__file__).resolve().parents[1]
            / "fixtures"
            / "run_trace"
            / "QS2GYXRKWN-a8.json"
        )
        actions = trace_real_game_run.recorded_actions(fixture)

        self.assertTrue(all(action is not None for action in actions))
        self.assertEqual({"action": "choose_event_option", "index": 0}, actions[0])

    def test_the_script_runs_out_rather_than_wrapping(self):
        script = [{"action": "proceed"}, {"action": "end_turn"}]

        self.assertEqual(script[0], trace_real_game_run.next_scripted_action(script, 1))
        self.assertEqual(script[1], trace_real_game_run.next_scripted_action(script, 2))
        self.assertIsNone(trace_real_game_run.next_scripted_action(script, 3))


if __name__ == "__main__":
    unittest.main()
