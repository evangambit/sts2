using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class ViciousTests
{
    [Fact]
    public void DrawsWhenPlayerAppliesVulnerable()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Vicious, false), new CardInstance(IC.Taunt, false)];
        state.DrawPile =
        [
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
        ];
        state.Energy = 2;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 100,
                MaxHp = 100,
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.Vicious));
        Assert.Equal(1, BuffSystem.Get(state.Enemies[0].Buffs, BuffId.Vulnerable));
        Assert.Contains(state.Hand, card => card.DefId == IC.StrikeIronclad);
    }
}
