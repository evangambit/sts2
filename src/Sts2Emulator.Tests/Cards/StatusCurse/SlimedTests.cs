using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class SlimedTests
{
    [Fact]
    public void DrawsOneAndExhaustsWhenPlayed()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand.Clear();
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.ExhaustPile.Clear();
        state.Hand.Add(new CardInstance(ST.Slimed, false));
        state.DrawPile.Add(new CardInstance(IC.StrikeIronclad, false));
        state.Energy = 1;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Contains(state.Hand, c => c.DefId == IC.StrikeIronclad);
        Assert.Contains(state.ExhaustPile, c => c.DefId == ST.Slimed);
    }
}
