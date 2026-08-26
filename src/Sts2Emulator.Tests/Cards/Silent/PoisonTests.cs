using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// Silent's signature mechanic. `PoisonPower.AfterSideTurnStart` triggers
// `min(Amount, 1 + Accelerant)` times, dealing the CURRENT amount each time and
// decrementing after each — so without Accelerant it is one tick and a decrement, which is
// what the emulator already did, and with Accelerant it is a different card entirely.
//
// Three of these were marked `approximation` in the source and all three turned out to be
// a different card rather than a rough one.

public class DeadlyPoisonTests
{
    // PowerVar<PoisonPower>(5m), OnUpgrade +2. TargetType.AnyEnemy.
    [Theory]
    [InlineData(false, 5)]
    [InlineData(true, 7)]
    public void PoisonsTheTarget(bool upgraded, int poison)
    {
        var fight = Fight.Hand(Card(SI.DeadlyPoison, upgraded)).Energy(1).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(poison, fight.EnemyBuffAmount(BuffId.Poison));
    }

    /// <summary>
    /// One trigger a turn without Accelerant: the enemy takes the whole stack and the
    /// stack falls by one.
    /// </summary>
    [Fact]
    public void ItTicksOnceAndFallsByOne()
    {
        var fight = Fight.Hand(Card(SI.DeadlyPoison)).Energy(1).Enemy(hp: 60);
        fight.Play();
        int before = fight.Enemy0.Hp;
        fight.State.PlayerHp = 999;

        fight.EndTurn();

        Assert.Equal(before - 5, fight.Enemy0.Hp);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Poison));
    }
}

public class PoisonedStabTests
{
    // DamageVar(6m) +2, PowerVar<PoisonPower>(3m) +1.
    [Theory]
    [InlineData(false, 6, 3)]
    [InlineData(true, 8, 4)]
    public void HitsAndPoisons(bool upgraded, int damage, int poison)
    {
        var fight = Fight.Hand(Card(SI.PoisonedStab, upgraded)).Energy(1).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(60 - damage, fight.Enemy0.Hp);
        Assert.Equal(poison, fight.EnemyBuffAmount(BuffId.Poison));
    }
}

public class NoxiousFumesTests
{
    // DynamicVar("PoisonPerTurn", 2m), OnUpgrade +1. NoxiousFumesPower poisons every
    // hittable enemy at the start of its owner's side turn.
    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 3)]
    public void PoisonsEveryEnemyEachTurn(bool upgraded, int perTurn)
    {
        var fight = Fight.Hand(Card(SI.NoxiousFumes, upgraded)).Energy(1);
        fight.State.PlayerHp = 999;

        fight.Play();
        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Poison));

        fight.EndTurn();

        // Applied at the start of the player's turn, so the stack is there and has not
        // ticked yet -- the enemies' own turn start is when they take it.
        Assert.All(
            fight.State.Enemies.Where(e => e.Hp > 0),
            e => Assert.True(BuffSystem.Get(e.Buffs, BuffId.Poison) > 0)
        );
        Assert.Equal(perTurn, BuffSystem.Get(fight.State.Enemies[0].Buffs, BuffId.Poison));
    }
}

public class AccelerantTests
{
    /// <summary>
    /// `AccelerantPower` does nothing on its own — `PoisonPower` reads it and re-triggers.
    /// The emulator modelled it as Envenom stacks, which is a different card doing a
    /// different thing: Envenom poisons on an attack, Accelerant makes poison already
    /// applied tick again.
    /// </summary>
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void ItGrantsExtraPoisonTriggers(bool upgraded, int stacks)
    {
        var fight = Fight.Hand(Card(SI.Accelerant, upgraded)).Energy(1);

        fight.Play();

        Assert.Equal(stacks, fight.PlayerBuffAmount(BuffId.Accelerant));
        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.Envenom));
    }

    /// <summary>
    /// With Accelerant 1, a poison of 5 deals 5 and then 4, and the stack falls by two.
    /// The amounts descend because the power decrements between triggers and the damage
    /// re-reads the current amount.
    /// </summary>
    [Fact]
    public void PoisonTicksTwiceForDescendingAmounts()
    {
        var fight = Fight.Hand(Card(SI.Accelerant), Card(SI.DeadlyPoison)).Energy(3).Enemy(hp: 60);
        fight.Play(0);
        fight.Play(0);
        int before = fight.Enemy0.Hp;
        fight.State.PlayerHp = 999;

        fight.EndTurn();

        Assert.Equal(before - 5 - 4, fight.Enemy0.Hp);
        Assert.Equal(3, fight.EnemyBuffAmount(BuffId.Poison));
    }

    /// <summary>
    /// `TriggerCount` is `min(Amount, 1 + Accelerant)` — it can never trigger more often
    /// than there is poison to spend, which is the clamp the game's own comment calls out.
    /// </summary>
    [Fact]
    public void ItNeverTriggersMoreOftenThanThereIsPoison()
    {
        var fight = Fight.Hand(Card(SI.Accelerant, upgraded: true)).Energy(3).Enemy(hp: 60);
        fight.Play();
        BuffSystem.Apply(fight.Enemy0.Buffs, BuffId.Poison, 1);
        int before = fight.Enemy0.Hp;
        fight.State.PlayerHp = 999;

        fight.EndTurn();

        // Accelerant 2 would be three triggers, but one point of poison is one tick.
        Assert.Equal(before - 1, fight.Enemy0.Hp);
        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Poison));
    }
}

public class CorrosiveWaveTests
{
    /// <summary>
    /// `CorrosiveWavePower.AfterCardDrawn` poisons every hittable enemy each time its
    /// owner draws a card, and `AfterSideTurnEnd` removes the power — a one-TURN draw
    /// engine. The emulator played it as a one-shot poison AND a Weak that the card does
    /// not apply at all.
    /// </summary>
    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 3)]
    public void EveryCardDrawnPoisonsAllEnemies(bool upgraded, int perDraw)
    {
        var fight = Fight.Hand(Card(SI.CorrosiveWave, upgraded)).Energy(1).Enemy(hp: 60);
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 3; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.StrikeSilent, false));
        }

        fight.Play();
        // Playing it poisons nobody by itself.
        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Poison));

        CardEffects.DrawCards(fight.State, 3, new Random(0));

        Assert.Equal(perDraw * 3, fight.EnemyBuffAmount(BuffId.Poison));
        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Weak));
    }

    [Fact]
    public void ThePowerIsGoneAfterTheTurnItWasPlayed()
    {
        var fight = Fight.Hand(Card(SI.CorrosiveWave)).Energy(1);
        fight.State.PlayerHp = 999;

        fight.Play();
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.CorrosiveWave));

        fight.EndTurn();

        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.CorrosiveWave));
    }
}

public class OutbreakTests
{
    /// <summary>
    /// `OutbreakPower` counts every Poison its owner applies and every THIRD one deals its
    /// amount to all enemies as unpowered damage. The emulator modelled it as Noxious
    /// Fumes at a bigger number, which is a different card.
    /// </summary>
    [Theory]
    [InlineData(false, 11)]
    [InlineData(true, 15)]
    public void EveryThirdPoisonBurstsAllEnemies(bool upgraded, int burst)
    {
        var fight = Fight
            .Hand(
                Card(SI.Outbreak, upgraded),
                Card(SI.DeadlyPoison),
                Card(SI.DeadlyPoison),
                Card(SI.DeadlyPoison)
            )
            .Energy(9)
            .Enemy(hp: 200);

        fight.Play(); // the power itself
        int before = fight.Enemy0.Hp;

        fight.Play(); // poison 1
        fight.Play(); // poison 2
        Assert.Equal(before, fight.Enemy0.Hp);

        fight.Play(); // poison 3 -- the burst

        Assert.Equal(before - burst, fight.Enemy0.Hp);
        // And the count resets, so the next two do nothing again.
        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.OutbreakCounter));
    }

    /// <summary>
    /// It counts poison from any source, because the game hooks the power-applied event
    /// rather than the card — so Noxious Fumes feeds the count too.
    /// </summary>
    [Fact]
    public void PoisonFromAPowerCountsAsWell()
    {
        var fight = Fight.Hand(Card(SI.Outbreak), Card(SI.NoxiousFumes)).Energy(9);
        fight.State.PlayerHp = 999;
        fight.Play();
        fight.Play();

        // One enemy poisoned per turn start; with two enemies that is two applications a
        // turn, so the third arrives on the second turn.
        int applications = fight.State.Enemies.Count(e => e.Hp > 0);
        fight.EndTurn();

        Assert.Equal(applications % 3, fight.PlayerBuffAmount(BuffId.OutbreakCounter));
    }
}

public class BouncingFlaskTests
{
    /// <summary>
    /// `TargetType.RandomEnemy`, and the card rolls
    /// `Rng.CombatTargets.NextItem(HittableEnemies)` for every bounce. The emulator used
    /// the AIMED-AT enemy, so all the bounces landed on one creature and the target stream
    /// was never drawn from.
    /// </summary>
    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 4)]
    public void ItBouncesAndTheTotalPoisonIsThreePerBounce(bool upgraded, int bounces)
    {
        var fight = Fight.Encounter(CombatFactory.ActOneEncounter.Bowlbugs);
        fight.State.Hand = [Card(SI.BouncingFlask, upgraded)];
        fight.State.Energy = 2;

        fight.Play();

        int total = fight.State.Enemies.Sum(e => BuffSystem.Get(e.Buffs, BuffId.Poison));
        Assert.Equal(3 * bounces, total);
    }

    /// <summary>
    /// And it really is spread by a roll rather than piled on the first enemy: across
    /// seeds, more than one creature is hit.
    /// </summary>
    [Fact]
    public void TheBouncesAreRolledNotAimed()
    {
        bool everySeedHitOnlyOne = true;
        for (int seed = 0; seed < 8; seed++)
        {
            var fight = Fight.Encounter(CombatFactory.ActOneEncounter.Bowlbugs, seed: seed);
            fight.State.Hand = [Card(SI.BouncingFlask, upgraded: true)];
            fight.State.Energy = 2;
            fight.Play();

            if (fight.State.Enemies.Count(e => BuffSystem.Get(e.Buffs, BuffId.Poison) > 0) > 1)
            {
                everySeedHitOnlyOne = false;
                break;
            }
        }

        Assert.False(everySeedHitOnlyOne, "every bounce landed on the same enemy");
    }
}
