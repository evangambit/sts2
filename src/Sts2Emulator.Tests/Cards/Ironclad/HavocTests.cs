using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class HavocTests
{
    [Fact]
    public void PlaysAndExhaustsTopDrawPileCard()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Havoc, false)];
        state.DrawPile = [new CardInstance(IC.DefendIronclad, false)];
        state.Energy = 1;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(5, state.PlayerBlock);
        Assert.Contains(state.DiscardPile, card => card.DefId == IC.Havoc);
        Assert.Contains(state.ExhaustPile, card => card.DefId == IC.DefendIronclad);
    }
}
