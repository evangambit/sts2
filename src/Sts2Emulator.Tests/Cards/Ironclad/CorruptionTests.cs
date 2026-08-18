using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class CorruptionTests
{
    [Fact]
    public void MakesSkillsFree()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);

        BuffSystem.Apply(state.PlayerBuffs, BuffId.Corruption, 1);
        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.DefendIronclad, false)); // Skill, cost 1
        state.Energy = 0; // not enough energy normally

        var actions = CombatEngine.ValidActions(state);
        Assert.Contains(0, actions); // card 0 should be playable despite 0 energy
    }
}
