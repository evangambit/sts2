using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class DazedTests
{
    [Fact]
    public void ExhaustsAtEndOfTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand.Clear();
        state.Hand.Add(new CardInstance(ST.Dazed, false));

        CombatEngine.Step(state, 1, new Random(0));

        Assert.DoesNotContain(state.Hand, c => c.DefId == ST.Dazed);
        Assert.Contains(state.ExhaustPile, c => c.DefId == ST.Dazed);
        Assert.DoesNotContain(state.DiscardPile, c => c.DefId == ST.Dazed);
    }
}
