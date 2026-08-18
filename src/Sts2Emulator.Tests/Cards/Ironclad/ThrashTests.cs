using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class ThrashTests
{
    [Fact]
    public void TwoHitsAndExhaustsRandomAttack()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);
        var enemy = state.Enemies[0];
        int hpBefore = enemy.Hp;

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.Thrash, false));
        state.Hand.Add(new CardInstance(IC.StrikeIronclad, false)); // Attack to exhaust
        state.Hand.Add(new CardInstance(IC.DefendIronclad, false)); // Skill, should not be exhausted
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng);
        // Thrash deals 4×2=8 damage
        Assert.Equal(hpBefore - 8, enemy.Hp);
        // The Attack (Strike) should be exhausted, Skill (Defend) should remain
        Assert.Equal(0, state.ExhaustPile.Count(c => c.DefId == IC.Thrash)); // Thrash does not exhaust itself
        Assert.DoesNotContain(state.Hand, c => c.DefId == IC.StrikeIronclad);
    }
}
