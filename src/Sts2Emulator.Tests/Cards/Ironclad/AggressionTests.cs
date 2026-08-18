using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class AggressionTests
{
    [Fact]
    public void AddsUpgradedCardAtStartOfTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Aggression, false)];
        state.DrawPile = [];
        state.DiscardPile = [];
        state.Energy = 3;

        // Play Aggression.
        CombatEngine.Step(state, 0, new Random(0));
        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.Aggression));
        Assert.Empty(state.Hand);

        // End turn.
        CombatEngine.Step(state, 0, new Random(0));

        // Start of next turn. Should have 5 cards (from draw) + 1 card from Aggression.
        // Wait, draw pile was empty. So it should only have cards from Aggression?
        // No, EndTurn draws 5 cards. If draw/discard empty, it draws 0.
        // So hand should have exactly 1 card.
        Assert.Single(state.Hand);
        Assert.True(state.Hand[0].Upgraded);
    }
}
