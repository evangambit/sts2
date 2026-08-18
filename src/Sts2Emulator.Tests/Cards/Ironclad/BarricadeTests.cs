using Sts2Emulator.Core;
using Xunit;

namespace Sts2Emulator.Tests;

public class BarricadeTests
{
    [Fact]
    public void BlockPersistsAcrossTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);

        // Give player Barricade and some block
        BuffSystem.Apply(state.PlayerBuffs, BuffId.Barricade, 1);
        state.PlayerBlock = 15;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 56,
                Hp = 1,
                MaxHp = 1,
                CurrentIntent = new Intent(IntentType.Defend, 0),
            },
        ];

        // End turn (don't play anything)
        state.Hand.Clear();
        CombatEngine.Step(state, 0, rng); // 0 = end turn when hand is empty

        // Block should NOT have been reset to 0
        Assert.True(state.PlayerBlock > 0);
    }
}
