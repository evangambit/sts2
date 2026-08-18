using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class AscendersBaneTests
{
    [Fact]
    public void IsNotPlayable()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.AscendersBane, false));

        var actions = CombatEngine.ValidActions(state);
        var result = CombatEngine.Step(state, 0, new Random(0));

        Assert.DoesNotContain(0, actions);
        Assert.Contains(1, actions);
        Assert.Equal(StepResult.Invalid, result);
    }
}
