using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class BashTests
{
    [Fact]
    public void DoesNotApplyVulnerableToNextEnemyWhenTargetDies()
    {
        var state = new CombatState
        {
            PlayerHp = 64,
            PlayerMaxHp = 80,
            Energy = 3,
            MaxEnergy = 3,
            Hand = [new CardInstance(IC.Bash, Upgraded: false)],
            Enemies =
            [
                new EnemyState
                {
                    DefId = KE.LeafSlimeS,
                    Hp = 8,
                    MaxHp = 8,
                },
                new EnemyState
                {
                    DefId = KE.TwigSlimeM,
                    Hp = 29,
                    MaxHp = 29,
                },
            ],
        };

        CombatEngine.Step(state, 0, new Random(0), targetEnemyIndex: 0);

        Assert.Equal(0, state.Enemies[0].Hp);
        Assert.Empty(state.Enemies[0].Buffs);
        Assert.Empty(state.Enemies[1].Buffs);
    }

    [Fact]
    public void AppliesVulnerableToEnemy()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.Bash, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng);

        Assert.True(BuffSystem.Get(state.Enemies[0].Buffs, BuffId.Vulnerable) > 0);
    }
}
