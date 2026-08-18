using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class SplashTests
{
    [Fact]
    public void AddsGeneratedAttackToHand()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Splash, false)];
        state.Energy = 1;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Single(state.Hand);
        Assert.Equal(IC.StrikeIronclad, state.Hand[0].DefId);
    }
}
