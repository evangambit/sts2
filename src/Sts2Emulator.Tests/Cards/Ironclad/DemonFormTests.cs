using Sts2Emulator.Core;
using Xunit;

namespace Sts2Emulator.Tests;

public class DemonFormTests
{
    [Fact]
    public void GrantsStrengthEachTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);

        // Apply DemonForm (2 Strength per turn)
        BuffSystem.Apply(state.PlayerBuffs, BuffId.DemonForm, 2);

        // End turn
        state.Hand.Clear();
        CombatEngine.Step(state, 0, rng);

        // Player should have gained 2 Strength
        Assert.Equal(2, BuffSystem.Get(state.PlayerBuffs, BuffId.Strength));
    }
}
