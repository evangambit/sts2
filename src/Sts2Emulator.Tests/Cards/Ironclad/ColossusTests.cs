using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class ColossusTests
{
    [Fact]
    public void GainsBlockAndHalvesVulnerableEnemyAttackDamage()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerHp = 100;
        state.PlayerMaxHp = 100;
        state.Hand = [new CardInstance(IC.Colossus, false)];
        state.DrawPile.Clear();
        state.Energy = 1;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 100,
                MaxHp = 100,
                CurrentIntent = new Intent(IntentType.Attack, 20),
                Buffs = [new BuffState(BuffId.Vulnerable, 2)],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(5, state.PlayerBlock);
        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.Colossus));

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(95, state.PlayerHp);
        Assert.Equal(0, BuffSystem.Get(state.PlayerBuffs, BuffId.Colossus));
    }
}
