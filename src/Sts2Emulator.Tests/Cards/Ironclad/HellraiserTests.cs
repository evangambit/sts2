using System.Linq;
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
        //
        // The two do NOT necessarily land on the same creature. `HellraiserPower` calls
        // `CardCmd.AutoPlay(context, card, null)`, and a null target makes the command roll
        // `Rng.CombatTargets.NextItem(HittableEnemies)` -- so the auto-played Strike hits a
        // ROLLED enemy while the Pommel Strike hits the one that was aimed at. This used to
        // assert all 15 on enemy[0], which held only because the emulator rolled the target
        // and then threw the result away.
        int[] before = [.. state.Enemies.Select(e => e.Hp)];
        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(9, before[0] - enemy.Hp);
        Assert.Equal(15, before.Zip(state.Enemies, (hp, e) => hp - e.Hp).Sum());
        // Hand should be empty (Pommel Strike played, StrikeIronclad auto-played).
        Assert.Empty(state.Hand);
        Assert.Contains(state.DiscardPile, c => c.DefId == IC.StrikeIronclad);
    }
}
