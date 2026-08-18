using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class FeedTests
{
    [Fact]
    public void KillsEnemyAndGrantsMaxHp()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);
        var enemy = state.Enemies[0];
        enemy.Hp = 1; // set enemy to 1 HP so Feed kills it
        int maxHpBefore = state.PlayerMaxHp;

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.Feed, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng);
        Assert.Equal(0, enemy.Hp);
        Assert.Equal(maxHpBefore + 3, state.PlayerMaxHp);
    }

    [Fact]
    public void NoKillNoMaxHpGain()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);
        var enemy = state.Enemies[0];
        enemy.Hp = 100; // enemy survives
        int maxHpBefore = state.PlayerMaxHp;

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.Feed, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng);
        Assert.Equal(maxHpBefore, state.PlayerMaxHp);
    }
}
