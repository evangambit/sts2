using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class ImperviousTests
{
    [Fact]
    public void Gains30BlockAndExhausts()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.Impervious, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng);
        Assert.Equal(30, state.PlayerBlock);
        var exhaustedCard = Assert.Single(state.ExhaustPile);
        Assert.Equal(IC.Impervious, exhaustedCard.DefId);
    }
}
