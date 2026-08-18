using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class DarkShacklesTests
{
    [Fact]
    public void AppliesTemporaryStrengthLossUntilEnemyTurnEnds()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerHp = 100;
        state.PlayerMaxHp = 100;
        state.Hand = [new CardInstance(CL.DarkShackles, false)];
        state.DrawPile.Clear();
        state.Energy = 0;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 100,
                MaxHp = 100,
                CurrentIntent = new Intent(IntentType.Attack, 20),
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(-9, BuffSystem.Get(state.Enemies[0].Buffs, BuffId.Strength));
        Assert.Equal(9, BuffSystem.Get(state.Enemies[0].Buffs, BuffId.TemporaryStrength));
        Assert.Contains(state.ExhaustPile, card => card.DefId == CL.DarkShackles);

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(89, state.PlayerHp);
        Assert.Equal(0, BuffSystem.Get(state.Enemies[0].Buffs, BuffId.Strength));
        Assert.Equal(0, BuffSystem.Get(state.Enemies[0].Buffs, BuffId.TemporaryStrength));
    }

    [Fact]
    public void IsPreventedByArtifact()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(CL.DarkShackles, true)];
        state.Energy = 0;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 100,
                MaxHp = 100,
                Buffs = [new BuffState(BuffId.Artifact, 1)],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(0, BuffSystem.Get(state.Enemies[0].Buffs, BuffId.Artifact));
        Assert.Equal(0, BuffSystem.Get(state.Enemies[0].Buffs, BuffId.Strength));
        Assert.Equal(0, BuffSystem.Get(state.Enemies[0].Buffs, BuffId.TemporaryStrength));
    }
}
