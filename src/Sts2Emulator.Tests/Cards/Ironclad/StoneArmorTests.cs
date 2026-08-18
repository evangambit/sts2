using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class StoneArmorTests
{
    [Fact]
    public void AppliesPlatingAndDecaysEachTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.StoneArmor, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng); // play StoneArmor (base: 4 Plating)
        Assert.Equal(4, BuffSystem.Get(state.PlayerBuffs, BuffId.Plating));
        Assert.Equal(0, state.PlayerBlock); // block not yet gained (end of turn pending)

        // end_turn → end-of-turn: gain 4 block → enemy turn → start-of-turn: plating decrements to 3
        CombatEngine.Step(state, state.Hand.Count, rng);
        Assert.Equal(3, BuffSystem.Get(state.PlayerBuffs, BuffId.Plating));
    }

    [Fact]
    public void Upgraded_Applies6Plating()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.StoneArmor, true));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng); // play upgraded StoneArmor
        Assert.Equal(6, BuffSystem.Get(state.PlayerBuffs, BuffId.Plating));
    }
}
