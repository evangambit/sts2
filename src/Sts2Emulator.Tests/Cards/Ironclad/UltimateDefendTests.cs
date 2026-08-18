using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class UltimateDefendTests
{
    [Fact]
    public void GainsBlock()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.UltimateDefend, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng);
        Assert.Equal(11, state.PlayerBlock);
    }
}
