using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 2-cost Power. MegaCrit.Sts2.Core.Models.Cards/Juggernaut.cs applies
// PowerVar<JuggernautPower>(6m), OnUpgrade UpgradeValueBy(2m).
// MegaCrit.Sts2.Core.Models.Powers/JuggernautPower.cs, AfterBlockGained:
//   target = Owner.Player.RunState.Rng.CombatTargets.NextItem(CombatState.HittableEnemies)
//   CreatureCmd.Damage(target, base.Amount, ValueProp.Unpowered, ...)
public class JuggernautTests
{
    [Fact]
    public void AppliesSixToThePlayer()
    {
        var fight = Fight.Hand(Card(IC.Juggernaut)).Energy(2).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(6, fight.PlayerBuffAmount(BuffId.Juggernaut));
    }

    [Fact]
    public void UpgradedAppliesEight()
    {
        var fight = Fight.Hand(Card(IC.Juggernaut, upgraded: true)).Energy(2).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(8, fight.PlayerBuffAmount(BuffId.Juggernaut));
    }

    [Fact]
    public void DamagesAnEnemyWhenBlockIsGained()
    {
        var fight = Fight
            .Hand(Card(IC.DefendIronclad))
            .Energy(1)
            .PlayerBuff(BuffId.Juggernaut, 6)
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(5, fight.State.PlayerBlock);
        Assert.Equal(34, fight.Enemy0.Hp);
    }

    [Fact]
    public void DamageIsUnpoweredSoStrengthDoesNotScaleIt()
    {
        var fight = Fight
            .Hand(Card(IC.DefendIronclad))
            .Energy(1)
            .PlayerBuff(BuffId.Juggernaut, 6)
            .PlayerBuff(BuffId.Strength, 5)
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(34, fight.Enemy0.Hp);
    }

    [Fact]
    public void DamageIsAbsorbedByEnemyBlockFirst()
    {
        var fight = Fight
            .Hand(Card(IC.DefendIronclad))
            .Energy(1)
            .PlayerBuff(BuffId.Juggernaut, 6)
            .Enemy(hp: 40, block: 4);

        fight.Play();

        Assert.Equal(0, fight.Enemy0.Block);
        Assert.Equal(38, fight.Enemy0.Hp);
    }

    /// <summary>
    /// The target is drawn, not fixed. Asserting a specific index for a specific seed
    /// would only restate what the emulator already does, so this asserts the property
    /// the game's NextItem(HittableEnemies) actually guarantees: across seeds, the hit
    /// is not always the first enemy. Before the fix this method picked FirstEnemy and
    /// every seed landed on index 0.
    /// </summary>
    [Fact]
    public void TargetVariesWithTheTargetStream()
    {
        var hitCounts = new int[2];

        for (int seed = 0; seed < 32; seed++)
        {
            var fight = Fight
                .Hand(Card(IC.DefendIronclad))
                .Seed(seed)
                .Energy(1)
                .PlayerBuff(BuffId.Juggernaut, 6)
                .Enemy(hp: 40)
                .Enemy(hp: 40);

            fight.Play();

            hitCounts[0] += 40 - fight.Enemy0.Hp > 0 ? 1 : 0;
            hitCounts[1] += 40 - fight.Enemy1.Hp > 0 ? 1 : 0;

            // Exactly one enemy is hit per block gain, never both and never neither.
            Assert.Equal(6, 40 - fight.Enemy0.Hp + (40 - fight.Enemy1.Hp));
        }

        Assert.True(hitCounts[0] > 0, "the first enemy was never hit across 32 seeds");
        Assert.True(hitCounts[1] > 0, "the second enemy was never hit across 32 seeds");
    }

    /// <summary>
    /// The draw comes off the run's combat_targets stream, which is what keeps the
    /// emulator's sequence aligned with the game's; drawing from the combat RNG would
    /// desynchronise everything downstream of it.
    /// </summary>
    [Fact]
    public void DrawsFromTheTargetStreamNotTheCombatRng()
    {
        var fight = Fight
            .Hand(Card(IC.DefendIronclad))
            .Energy(1)
            .PlayerBuff(BuffId.Juggernaut, 6)
            .Enemy(hp: 40)
            .Enemy(hp: 40);
        var targetRng = new CountingRandom(7);
        fight.State.TargetRng = targetRng;

        fight.Play();

        Assert.Equal(1, targetRng.CallCount);
    }
}
