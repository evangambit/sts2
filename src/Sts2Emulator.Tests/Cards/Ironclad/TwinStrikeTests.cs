using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class TwinStrikeTests
{
    [Fact]
    public void HitsTwice()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 14,
                Hp = 30,
                MaxHp = 30,
                CurrentIntent = new Intent(IntentType.Attack, 9),
                Buffs = [],
            },
        ];
        int enemyHp = state.Enemies[0].Hp;

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.TwinStrike, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng);

        // TwinStrike deals 5×2 = 10 damage (no buffs)
        Assert.Equal(enemyHp - 10, state.Enemies[0].Hp);
    }
}
