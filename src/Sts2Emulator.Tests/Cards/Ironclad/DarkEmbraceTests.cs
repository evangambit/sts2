using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class DarkEmbraceTests
{
    [Fact]
    public void DrawsCardOnImmediateExhaust()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.DarkEmbrace, false),
            new CardInstance(IC.TrueGrit, false),
        ];
        state.DrawPile =
        [
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
            new CardInstance(IC.IronWave, false),
        ];
        state.Energy = 3;

        // Play Dark Embrace.
        CombatEngine.Step(state, 0, new Random(0));
        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.DarkEmbrace));
        Assert.Single(state.Hand); // True Grit remains after the Power leaves hand.

        // Play True Grit after adding another card for it to exhaust.
        state.Hand.Add(new CardInstance(IC.Bash, false));
        // Hand now has: True Grit, Bash.
        // Action 0 is True Grit.
        CombatEngine.Step(state, 0, new Random(0));

        // True Grit played, exhausts a card, Dark Embrace triggers again.
        // True Grit itself does not exhaust, so it shouldn't trigger another draw.
        Assert.Single(state.Hand);
    }

    [Fact]
    public void DrawsCardAfterTurnEndForEtherealExhaust()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.DarkEmbrace, false),
            new CardInstance(IC.AscendersBane, false),
        ];
        state.DrawPile =
        [
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
            new CardInstance(IC.IronWave, false),
            new CardInstance(IC.Bash, false),
            new CardInstance(IC.Anger, false),
            new CardInstance(IC.BodySlam, false),
            new CardInstance(IC.Break, false),
        ];
        state.Energy = 3;

        // Play Dark Embrace.
        CombatEngine.Step(state, 0, new Random(0));
        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.DarkEmbrace));
        Assert.Single(state.Hand); // Ascender's Bane remains after the Power leaves hand.

        // End turn. Ascender's Bane is Ethereal and should exhaust.
        // Dark Embrace should trigger but the draw should be deferred.
        CombatEngine.Step(state, 1, new Random(0)); // action 1 is End Turn when hand has 1 card

        // After end turn, we should have drawn 5 cards for next turn + 1 card from Dark Embrace.
        Assert.Equal(6, state.Hand.Count);
        Assert.Equal(1, state.ExhaustPile.Count(c => c.DefId == IC.AscendersBane));
    }
}
