using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class HellraiserTests
{
    [Fact]
    public void AutoPlaysDrawnStrike()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.Hellraiser, false),
            new CardInstance(IC.PommelStrike, false),
        ];
        state.DrawPile = [new CardInstance(IC.StrikeIronclad, false)];
        state.DiscardPile = [];
        state.Energy = 3;
        var enemy = state.Enemies[0];
        enemy.Hp = 100;

        // Play Hellraiser.
        CombatEngine.Step(state, 0, new Random(0));
        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.Hellraiser));

        // Play Pommel Strike (draws 1).
        // It should draw StrikeIronclad, which Hellraiser should automatically play.
        // StrikeIronclad deals 6 damage. Pommel Strike deals 9.
        // Total damage should be 15.
        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(85, enemy.Hp);
        // Hand should be empty (Pommel Strike played, StrikeIronclad auto-played).
        Assert.Empty(state.Hand);
        Assert.Contains(state.DiscardPile, c => c.DefId == IC.StrikeIronclad);
    }
}
