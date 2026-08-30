using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// Defect's thirty-six uncommons, read against MegaCrit.Sts2.Core.Models.Cards/*.cs.
// Ten were wrong.

public class BootSequenceTests
{
    private const int BootSequence = 55;

    [Theory]
    [InlineData(false, 10)]
    [InlineData(true, 13)]
    public void BlocksForFree(bool upgraded, int block)
    {
        var fight = DefectFight.Hand(Card(BootSequence, upgraded)).Energy(0);
        fight.Play();
        Assert.Equal(block, fight.State.PlayerBlock);
    }
}

/// <summary>
/// Bulk Up trades ONE orb slot for Strength and Dexterity.
/// </summary>
/// <remarks>
/// `DynamicVar("OrbSlots", 1m)`, and `OnUpgrade` names Strength and Dexterity — so the
/// slot count is 1 at both levels and the emulator was taking two. `RemoveCapacity` drops
/// orbs off the END as the ring shrinks.
/// </remarks>
public class BulkUpTests
{
    private const int BulkUp = 64;

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 3)]
    public void TakesOneSlotAndGivesBoth(bool upgraded, int amount)
    {
        var fight = DefectFight.Hand(Card(BulkUp, upgraded)).Energy(2);

        fight.Play();

        Assert.Equal(2, fight.State.OrbCapacity);
        Assert.Equal(amount, fight.PlayerBuffAmount(BuffId.Strength));
        Assert.Equal(amount, fight.PlayerBuffAmount(BuffId.Dexterity));
    }

    /// <summary>A full ring loses its LAST orb when the slot goes.</summary>
    [Fact]
    public void AFullRingDropsItsNewestOrb()
    {
        var fight = DefectFight.Hand(Card(BulkUp)).Energy(2);
        CardEffects.ChannelOrb(fight.State, OrbType.Lightning);
        CardEffects.ChannelOrb(fight.State, OrbType.Frost);
        CardEffects.ChannelOrb(fight.State, OrbType.Dark);

        fight.Play();

        Assert.Equal([OrbType.Lightning, OrbType.Frost], fight.State.Orbs.Select(o => o.Type));
    }
}

public class CapacitorTests
{
    private const int Capacitor = 78;

    [Theory]
    [InlineData(false, 5)]
    [InlineData(true, 6)]
    public void AddsSlots(bool upgraded, int capacity)
    {
        var fight = DefectFight.Hand(Card(Capacitor, upgraded)).Energy(1);
        fight.Play();
        Assert.Equal(capacity, fight.State.OrbCapacity);
    }
}

/// <summary>
/// Chaos rolls over ALL FIVE orb types, on the orb-generation stream.
/// </summary>
/// <remarks>
/// `OrbModel.GetRandomOrb(Rng.CombatOrbGeneration)` picks from `_validOrbs`, which is five
/// entries including Glass. The emulator rolled `rng.Next(4)` — it could never produce a
/// Glass orb, and it drew from the combat rng rather than the orb stream.
/// </remarks>
public class ChaosTests
{
    private const int Chaos = 82;

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void ChannelsThatMany(bool upgraded, int count)
    {
        var fight = DefectFight.Hand(Card(Chaos, upgraded)).Energy(1).Enemy(hp: 200);
        fight.Play();
        Assert.Equal(count, fight.State.Orbs.Count);
    }

    [Fact]
    public void GlassIsInTheRoll()
    {
        var seen = new HashSet<OrbType>();
        for (int seed = 0; seed < 40; seed++)
        {
            var fight = DefectFight.Hand(Card(Chaos)).Energy(1).Enemy(hp: 200);
            fight.State.OrbGenerationRng = new CountingRandom(seed);
            fight.Play();
            seen.UnionWith(fight.State.Orbs.Select(o => o.Type));
        }

        Assert.Contains(OrbType.Glass, seen);
        Assert.Equal(5, seen.Count);
    }

    /// <summary>It draws from the ORB stream, which is kept apart from the combat rng.</summary>
    [Fact]
    public void ItDrawsFromTheOrbStream()
    {
        var fight = DefectFight.Hand(Card(Chaos)).Energy(1).Enemy(hp: 200);
        int before = fight.State.OrbGenerationRng!.CallCount;

        fight.Play();

        Assert.True(fight.State.OrbGenerationRng.CallCount > before);
    }
}

public class ChillTests
{
    private const int Chill = 86;

    [Fact]
    public void OneFrostPerLivingEnemy()
    {
        var fight = DefectFight.Hand(Card(Chill)).Energy(0).Enemy(hp: 60);
        fight.Enemy(hp: 60);
        fight.State.OrbCapacity = 5;

        fight.Play();

        Assert.Equal([OrbType.Frost, OrbType.Frost], fight.State.Orbs.Select(o => o.Type));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void TheUpgradeRemovesExhaust(bool upgraded, bool exhausts)
    {
        var fight = DefectFight.Hand(Card(Chill, upgraded)).Energy(0).Enemy(hp: 60);
        fight.Play();
        Assert.Equal(exhausts, fight.State.ExhaustPile.Any(c => c.DefId == Chill));
    }
}

public class CompactTests
{
    private const int Compact = 97;

    /// <summary>`BlockVar(6m)` upgrades by ONE, not the usual three.</summary>
    [Theory]
    [InlineData(false, 6)]
    [InlineData(true, 7)]
    public void BlocksAndTransformsStatuses(bool upgraded, int block)
    {
        var fight = DefectFight.Hand(Card(Compact, upgraded), Card(ST.Dazed)).Energy(1);

        fight.Play();

        Assert.Equal(block, fight.State.PlayerBlock);
        Assert.DoesNotContain(fight.State.Hand, c => c.DefId == ST.Dazed);
    }
}

public class DarknessTests
{
    private const int Darkness = 120;

    /// <summary>
    /// Channels a Dark orb and then fires EVERY Dark orb's passive — twice each when
    /// upgraded — so the new one banks immediately.
    /// </summary>
    [Theory]
    [InlineData(false, 12)]
    [InlineData(true, 18)]
    public void ChannelsThenTriggersEveryDarkOrb(bool upgraded, int banked)
    {
        var fight = DefectFight.Hand(Card(Darkness, upgraded)).Energy(1).Enemy(hp: 200);

        fight.Play();

        Assert.Equal(banked, fight.State.Orbs[0].EvokeValue);
    }
}

public class DoubleEnergyTests
{
    private const int DoubleEnergy = 151;

    [Fact]
    public void ItDoublesWhatIsLeft()
    {
        var fight = DefectFight.Hand(Card(DoubleEnergy)).Energy(5);
        fight.Play();
        // Five, minus the one it cost, doubled.
        Assert.Equal(8, fight.State.Energy);
    }
}

public class EnergySurgeTests
{
    private const int EnergySurge = 163;

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 3)]
    public void GivesEnergy(bool upgraded, int energy)
    {
        var fight = DefectFight.Hand(Card(EnergySurge, upgraded)).Energy(1);
        fight.Play();
        Assert.Equal(energy, fight.State.Energy);
    }
}

public class FeralTests
{
    private const int Feral = 186;

    /// <summary>
    /// `FeralPower` returns a 0-cost ATTACK to hand instead of the discard, up to Amount
    /// times a turn.
    /// </summary>
    [Fact]
    public void AZeroCostAttackComesBack()
    {
        var fight = DefectFight.Hand(Card(Feral), Card(SI.Slice), Card(SI.Slice)).Energy(3).Enemy(hp: 200);
        fight.Play();

        fight.Play(fight.State.Hand.FindIndex(c => c.DefId == SI.Slice));
        Assert.Equal(2, fight.State.Hand.Count(c => c.DefId == SI.Slice));

        // Once per turn: the second one goes to the discard.
        fight.Play(fight.State.Hand.FindIndex(c => c.DefId == SI.Slice));
        Assert.Contains(fight.State.DiscardPile, c => c.DefId == SI.Slice);
    }
}

public class FightThroughTests
{
    private const int FightThrough = 190;

    [Theory]
    [InlineData(false, 13)]
    [InlineData(true, 17)]
    public void BlocksAndAddsTwoWounds(bool upgraded, int block)
    {
        var fight = DefectFight.Hand(Card(FightThrough, upgraded)).Energy(1);
        fight.Play();
        Assert.Equal(block, fight.State.PlayerBlock);
        Assert.Equal(2, fight.State.DiscardPile.Count(c => c.DefId == ST.Wound));
    }
}

public class FtlTests
{
    private const int Ftl = 208;

    /// <summary>
    /// Draws only while FEWER than PlayMax cards have already been played this turn. The
    /// count excludes Ftl itself, which has not finished when the check runs.
    /// </summary>
    [Theory]
    [InlineData(false, 5)]
    [InlineData(true, 6)]
    public void HitsAndDrawsWhileTheTurnIsYoung(bool upgraded, int damage)
    {
        var fight = DefectFight.Hand(Card(Ftl, upgraded)).Energy(0).Enemy(hp: 200);
        fight.State.DrawPile.Clear();
        fight.State.DrawPile.Add(new CardInstance(SI.Backstab, false));

        fight.Play();

        Assert.Equal(200 - damage, fight.Enemy0.Hp);
        Assert.Single(fight.State.Hand);
    }

    [Fact]
    public void PastTheLimitItJustHits()
    {
        var fight = DefectFight.Hand(Card(Ftl)).Energy(0).Enemy(hp: 200);
        fight.State.CardPlaysThisTurn = 3;
        fight.State.DrawPile.Clear();
        fight.State.DrawPile.Add(new CardInstance(SI.Backstab, false));

        fight.Play();

        Assert.Empty(fight.State.Hand);
    }
}

public class FusionTests
{
    private const int Fusion = 211;

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void ChannelsPlasmaAndTheUpgradeRemovesExhaust(bool upgraded, bool exhausts)
    {
        var fight = DefectFight.Hand(Card(Fusion, upgraded)).Energy(1);
        fight.Play();
        Assert.Equal([OrbType.Plasma], fight.State.Orbs.Select(o => o.Type));
        Assert.Equal(exhausts, fight.State.ExhaustPile.Any(c => c.DefId == Fusion));
    }
}

public class GlacierTests
{
    private const int Glacier = 218;

    [Theory]
    [InlineData(false, 6)]
    [InlineData(true, 9)]
    public void BlocksAndChannelsTwoFrost(bool upgraded, int block)
    {
        var fight = DefectFight.Hand(Card(Glacier, upgraded)).Energy(2);
        fight.Play();
        Assert.Equal(block, fight.State.PlayerBlock);
        Assert.Equal([OrbType.Frost, OrbType.Frost], fight.State.Orbs.Select(o => o.Type));
    }
}

public class GlassworkTests
{
    private const int Glasswork = 219;

    [Theory]
    [InlineData(false, 5)]
    [InlineData(true, 8)]
    public void BlocksAndChannelsGlass(bool upgraded, int block)
    {
        var fight = DefectFight.Hand(Card(Glasswork, upgraded)).Energy(1);
        fight.Play();
        Assert.Equal(block, fight.State.PlayerBlock);
        Assert.Equal([OrbType.Glass], fight.State.Orbs.Select(o => o.Type));
    }
}

public class HailstormTests
{
    private const int Hailstorm = 232;

    /// <summary>
    /// `BeforeSideTurnEnd`, and gated on holding at least one FROST orb — the condition is
    /// the card, and without it Hailstorm is free damage every turn.
    /// </summary>
    [Theory]
    [InlineData(false, 6)]
    [InlineData(true, 8)]
    public void HitsEveryEnemyWhileAFrostOrbIsHeld(bool upgraded, int damage)
    {
        var fight = DefectFight.Hand(Card(Hailstorm, upgraded)).Energy(1).Enemy(hp: 200);
        fight.State.PlayerHp = 999;
        fight.Play();
        CardEffects.ChannelOrb(fight.State, OrbType.Frost);

        fight.EndTurn();

        // The Frost passive also fires at turn end, but only the damage lands on enemies.
        Assert.Equal(200 - damage, fight.Enemy0.Hp);
    }

    [Fact]
    public void WithNoFrostOrbItDoesNothing()
    {
        var fight = DefectFight.Hand(Card(Hailstorm)).Energy(1).Enemy(hp: 200);
        fight.State.PlayerHp = 999;
        fight.Play();
        CardEffects.ChannelOrb(fight.State, OrbType.Dark);

        fight.EndTurn();

        Assert.Equal(200, fight.Enemy0.Hp);
    }
}

/// <summary>
/// Iteration draws when the FIRST Status card of a turn is drawn.
/// </summary>
/// <remarks>
/// `IterationPower.AfterCardDrawn`, guarded on the turn's Status draws being at most one.
/// The emulator gave a flat next-turn draw — a different card, which pays out on a turn
/// Iteration would not and pays nothing at all in the deck Iteration is built for.
/// </remarks>
public class IterationTests
{
    private const int Iteration = 269;

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 3)]
    public void TheFirstStatusDrawnDrawsMore(bool upgraded, int extra)
    {
        var fight = DefectFight.Hand(Card(Iteration, upgraded)).Energy(1);
        fight.Play();
        fight.State.DrawPile.Clear();
        fight.State.DrawPile.Add(new CardInstance(ST.Dazed, false));
        for (int i = 0; i < 6; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.Backstab, false));
        }

        CardEffects.DrawCards(fight.State, 1, new Random(0));

        // The Dazed, plus the cards it bought.
        Assert.Equal(1 + extra, fight.State.Hand.Count);
    }

    [Fact]
    public void OnlyTheFirstStatusOfTheTurnPaysOut()
    {
        var fight = DefectFight.Hand(Card(Iteration)).Energy(1);
        fight.Play();
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 3; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(ST.Dazed, false));
        }

        for (int i = 0; i < 6; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.Backstab, false));
        }

        CardEffects.DrawCards(fight.State, 1, new Random(0));
        int afterFirst = fight.State.Hand.Count;

        CardEffects.DrawCards(fight.State, 1, new Random(0));

        Assert.Equal(afterFirst + 1, fight.State.Hand.Count);
    }

    [Fact]
    public void ANonStatusDrawPaysNothing()
    {
        var fight = DefectFight.Hand(Card(Iteration)).Energy(1);
        fight.Play();
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 6; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.Backstab, false));
        }

        CardEffects.DrawCards(fight.State, 1, new Random(0));

        Assert.Single(fight.State.Hand);
    }
}

public class LoopTests
{
    private const int Loop = 288;

    /// <summary>Fires the FRONT orb's passive at the start of each turn, Amount times.</summary>
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void TheFrontOrbFiresAtTurnStart(bool upgraded, int times)
    {
        var fight = DefectFight.Hand(Card(Loop, upgraded)).Energy(1).Enemy(hp: 400);
        fight.State.PlayerHp = 999;
        fight.Play();
        CardEffects.ChannelOrb(fight.State, OrbType.Lightning);

        int before = fight.Enemy0.Hp;
        fight.EndTurn();

        // The end-of-turn passive, plus Loop's extra triggers at the next turn's start.
        Assert.Equal(before - 3 * (1 + times), fight.Enemy0.Hp);
    }
}

public class NullTests
{
    private const int Null = 330;

    [Theory]
    [InlineData(false, 10, 2)]
    [InlineData(true, 13, 3)]
    public void HitsWeakensAndChannelsDark(bool upgraded, int damage, int weak)
    {
        var fight = DefectFight.Hand(Card(Null, upgraded)).Energy(2).Enemy(hp: 200);
        fight.Play();
        Assert.Equal(200 - damage, fight.Enemy0.Hp);
        Assert.Equal(weak, fight.EnemyBuffAmount(BuffId.Weak));
        Assert.Equal([OrbType.Dark], fight.State.Orbs.Select(o => o.Type));
    }
}

public class OverclockTests
{
    private const int Overclock = 338;

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 3)]
    public void DrawsAndBurns(bool upgraded, int cards)
    {
        var fight = DefectFight.Hand(Card(Overclock, upgraded)).Energy(0);
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 6; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.Backstab, false));
        }

        fight.Play();

        Assert.Equal(cards, fight.State.Hand.Count);
        Assert.Contains(fight.State.DiscardPile, c => c.DefId == ST.Burn);
    }
}

/// <summary>Refract hits TWICE. `.WithHitCount(2)`, which the emulator did not have.</summary>
public class RefractTests
{
    private const int Refract = 392;

    [Theory]
    [InlineData(false, 9)]
    [InlineData(true, 12)]
    public void HitsTwiceThenChannelsTwoGlass(bool upgraded, int damage)
    {
        var fight = DefectFight.Hand(Card(Refract, upgraded)).Energy(3).Enemy(hp: 200);

        fight.Play();

        Assert.Equal(200 - damage * 2, fight.Enemy0.Hp);
        Assert.Equal([OrbType.Glass, OrbType.Glass], fight.State.Orbs.Select(o => o.Type));
    }
}

/// <summary>
/// Rocket Punch costs nothing UNTIL PLAYED once its owner generates a Status card.
/// </summary>
/// <remarks>
/// `AfterCardGeneratedForCombat` calls `EnergyCost.SetUntilPlayed(0)` on itself. Unlike a
/// free-this-turn grant this survives the turn boundary and is spent by the play. The
/// emulator had none of it.
/// </remarks>
public class RocketPunchTests
{
    private const int RocketPunch = 400;
    private const int GunkUp = 231;

    [Theory]
    [InlineData(false, 13, 1)]
    [InlineData(true, 14, 2)]
    public void HitsAndDraws(bool upgraded, int damage, int cards)
    {
        var fight = DefectFight.Hand(Card(RocketPunch, upgraded)).Energy(2).Enemy(hp: 200);
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 4; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.Backstab, false));
        }

        fight.Play();

        Assert.Equal(200 - damage, fight.Enemy0.Hp);
        Assert.Equal(cards, fight.State.Hand.Count);
    }

    [Fact]
    public void AGeneratedStatusMakesItFree()
    {
        var fight = DefectFight.Hand(Card(GunkUp), Card(RocketPunch)).Energy(9).Enemy(hp: 400);
        fight.State.DrawPile.Clear();
        fight.State.DrawPile.Add(new CardInstance(SI.Backstab, false));

        fight.Play(); // Gunk Up generates a Slimed
        Assert.True(fight.State.Hand.Single(c => c.DefId == RocketPunch).FreeUntilPlayed);

        int before = fight.State.Energy;
        fight.Play(fight.State.Hand.FindIndex(c => c.DefId == RocketPunch));

        Assert.Equal(before, fight.State.Energy);
        // Spent by the play, so the copy in the discard is ordinary again.
        Assert.False(fight.State.DiscardPile.Single(c => c.DefId == RocketPunch).FreeUntilPlayed);
    }
}

/// <summary>Scavenge exhausts a CHOSEN card, not the leftmost one.</summary>
public class ScavengeTests
{
    private const int Scavenge = 408;

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 3)]
    public void AsksWhatToBurnAndOwesEnergyNextTurn(bool upgraded, int energy)
    {
        var fight = Fight
            .Hand(Card(Scavenge, upgraded), Card(SI.Backstab), Card(SI.Slice))
            .Energy(1);

        fight.Play();

        Assert.Equal(energy, fight.PlayerBuffAmount(BuffId.NextTurnEnergy));
        Assert.Equal(CardSelectionKind.ExhaustFromHand, fight.Pending!.Kind);
        Assert.Equal(2, fight.Pending.Candidates.Count);
    }

    [Fact]
    public void TheChosenCardIsTheOneExhausted()
    {
        var fight = DefectFight.Hand(Card(Scavenge), Card(SI.Backstab), Card(SI.Slice)).Energy(1);
        fight.Play();

        fight.Choose(1); // the Slice, not the leftmost card

        Assert.Contains(fight.State.ExhaustPile, c => c.DefId == SI.Slice);
        Assert.Contains(fight.State.Hand, c => c.DefId == SI.Backstab);
    }
}

public class ScrapeTests
{
    private const int Scrape = 410;

    /// <summary>Draws, then discards every drawn card that does not cost zero.</summary>
    [Theory]
    [InlineData(false, 7, 4)]
    [InlineData(true, 10, 5)]
    public void HitsThenKeepsOnlyTheFreeCards(bool upgraded, int damage, int drawn)
    {
        var fight = DefectFight.Hand(Card(Scrape, upgraded)).Energy(1).Enemy(hp: 200);
        fight.State.DrawPile.Clear();
        for (int i = 0; i < drawn; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(i % 2 == 0 ? SI.Slice : SI.Backstab, false));
        }

        fight.Play();

        Assert.Equal(200 - damage, fight.Enemy0.Hp);
        // Slice and Backstab both cost 0, so nothing is thrown back.
        Assert.Equal(drawn, fight.State.Hand.Count);
    }

    [Fact]
    public void ACostlyDrawIsDiscarded()
    {
        var fight = DefectFight.Hand(Card(Scrape)).Energy(1).Enemy(hp: 200);
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 4; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.StrikeSilent, false));
        }

        fight.Play();

        Assert.Empty(fight.State.Hand);
        Assert.Equal(4, fight.State.DiscardPile.Count(c => c.DefId == SI.StrikeSilent));
    }
}

public class ShadowShieldTests
{
    private const int ShadowShield = 425;

    [Theory]
    [InlineData(false, 11)]
    [InlineData(true, 15)]
    public void BlocksAndChannelsDark(bool upgraded, int block)
    {
        var fight = DefectFight.Hand(Card(ShadowShield, upgraded)).Energy(2);
        fight.Play();
        Assert.Equal(block, fight.State.PlayerBlock);
        Assert.Equal([OrbType.Dark], fight.State.Orbs.Select(o => o.Type));
    }
}

public class SkimTests
{
    private const int Skim = 437;

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 4)]
    public void Draws(bool upgraded, int cards)
    {
        var fight = DefectFight.Hand(Card(Skim, upgraded)).Energy(1);
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 6; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.Backstab, false));
        }

        fight.Play();
        Assert.Equal(cards, fight.State.Hand.Count);
    }
}

public class SmokestackTests
{
    private const int Smokestack = 441;
    private const int GunkUp = 231;

    /// <summary>Fires when a STATUS card is generated, hitting every enemy.</summary>
    [Theory]
    [InlineData(false, 5)]
    [InlineData(true, 7)]
    public void AGeneratedStatusHitsEveryEnemy(bool upgraded, int damage)
    {
        var fight = DefectFight.Hand(Card(Smokestack, upgraded), Card(GunkUp)).Energy(9).Enemy(hp: 200);
        fight.Enemy(hp: 200);
        fight.Play();

        fight.Play();

        // Gunk Up's three hits on the first enemy, then the Slimed's Smokestack damage.
        Assert.Equal(200 - 12 - damage, fight.Enemy0.Hp);
        Assert.Equal(200 - damage, fight.Enemy1.Hp);
    }
}

/// <summary>
/// Storm channels Lightning after each POWER card, and does not trigger on its own play.
/// </summary>
public class StormTests
{
    private const int Storm = 467;
    private const int Capacitor = 78;

    [Fact]
    public void ItDoesNotTriggerOnItself()
    {
        var fight = DefectFight.Hand(Card(Storm), Card(Capacitor)).Energy(3);
        fight.Play();
        Assert.Empty(fight.State.Orbs);

        fight.Play();

        Assert.Equal([OrbType.Lightning], fight.State.Orbs.Select(o => o.Type));
    }

    /// <summary>A second Storm pays the OLD amount for its own play — one orb, not two.</summary>
    [Fact]
    public void ASecondCopyPaysTheOldAmount()
    {
        var fight = DefectFight.Hand(Card(Storm), Card(Storm)).Energy(3);
        fight.Play();

        fight.Play();

        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.Storm));
        Assert.Single(fight.State.Orbs);
    }
}

/// <summary>Subroutine gives energy after each POWER, on the same before-play reading.</summary>
public class SubroutineTests
{
    private const int Subroutine = 476;
    private const int Capacitor = 78;

    [Fact]
    public void ItDoesNotTriggerOnItself()
    {
        var fight = DefectFight.Hand(Card(Subroutine), Card(Capacitor)).Energy(3);

        fight.Play();
        Assert.Equal(2, fight.State.Energy);

        fight.Play();

        // One for Capacitor's cost, plus the one Subroutine gives back.
        Assert.Equal(2, fight.State.Energy);
    }
}

public class SunderTests
{
    private const int Sunder = 479;

    [Theory]
    [InlineData(false, 24)]
    [InlineData(true, 32)]
    public void HitsAndRefundsOnAKill(bool upgraded, int damage)
    {
        var fight = DefectFight.Hand(Card(Sunder, upgraded)).Energy(3).Enemy(hp: 200);
        fight.Play();
        Assert.Equal(200 - damage, fight.Enemy0.Hp);
        Assert.Equal(0, fight.State.Energy);

        var kill = DefectFight.Hand(Card(Sunder, upgraded)).Energy(3).Enemy(hp: 5);
        kill.Play();
        Assert.Equal(3, kill.State.Energy);
    }
}

/// <summary>
/// Synchronize gives 2 Focus PER DISTINCT ORB TYPE, and it is temporary.
/// </summary>
/// <remarks>
/// `CalculatedVar("CalculatedFocus")` with base 0 and extra 2, multiplied by the distinct
/// orb count. The emulator gave a flat 2 — right only when exactly one type is held, and
/// worth nothing where the card is worth most.
/// </remarks>
public class SynchronizeTests
{
    private const int Synchronize = 488;

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 2)]
    [InlineData(3, 6)]
    public void TwoFocusPerDistinctOrbType(int types, int focus)
    {
        var fight = DefectFight.Hand(Card(Synchronize)).Energy(1);
        fight.State.OrbCapacity = 6;
        var kinds = new[] { OrbType.Lightning, OrbType.Frost, OrbType.Dark };
        for (int i = 0; i < types; i++)
        {
            CardEffects.ChannelOrb(fight.State, kinds[i]);
        }

        fight.Play();

        Assert.Equal(focus, fight.PlayerBuffAmount(BuffId.Focus));
    }

    /// <summary>Duplicates do not count twice — it is DISTINCT types.</summary>
    [Fact]
    public void ThreeOrbsOfOneTypeAreStillOneType()
    {
        var fight = DefectFight.Hand(Card(Synchronize)).Energy(1);
        for (int i = 0; i < 3; i++)
        {
            CardEffects.ChannelOrb(fight.State, OrbType.Frost);
        }

        fight.Play();

        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.Focus));
    }

    [Fact]
    public void TheFocusIsTemporary()
    {
        var fight = DefectFight.Hand(Card(Synchronize)).Energy(1).Enemy(hp: 60);
        fight.State.PlayerHp = 999;
        CardEffects.ChannelOrb(fight.State, OrbType.Frost);
        fight.Play();
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.Focus));

        fight.EndTurn();

        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.Focus));
    }
}

/// <summary>Synthesis makes the next POWER free. It was making the next ATTACK free.</summary>
public class SynthesisTests
{
    private const int Synthesis = 489;
    private const int Capacitor = 78;

    [Theory]
    [InlineData(false, 14)]
    [InlineData(true, 20)]
    public void HitsThenTheNextPowerIsFree(bool upgraded, int damage)
    {
        var fight = DefectFight.Hand(Card(Synthesis, upgraded), Card(Capacitor)).Energy(3).Enemy(hp: 200);

        fight.Play();
        Assert.Equal(200 - damage, fight.Enemy0.Hp);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.FreePowerPower));

        int before = fight.State.Energy;
        fight.Play();

        Assert.Equal(before, fight.State.Energy);
        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.FreePowerPower));
    }

    /// <summary>And not the next Attack — Synthesis is one, which is what made this wrong.</summary>
    [Fact]
    public void ItDoesNotDiscountAnAttack()
    {
        var fight = DefectFight.Hand(Card(Synthesis), Card(SI.StrikeSilent)).Energy(3).Enemy(hp: 200);
        fight.Play();

        int before = fight.State.Energy;
        fight.Play();

        Assert.Equal(before - 1, fight.State.Energy);
    }
}

public class TempestTests
{
    private const int Tempest = 495;

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 4)]
    public void ChannelsOnePerEnergySpent(bool upgraded, int orbs)
    {
        var fight = DefectFight.Hand(Card(Tempest, upgraded)).Energy(3);
        fight.State.OrbCapacity = 6;

        fight.Play();

        Assert.Equal(orbs, fight.State.Orbs.Count);
        Assert.All(fight.State.Orbs, o => Assert.Equal(OrbType.Lightning, o.Type));
        Assert.Equal(0, fight.State.Energy);
    }
}

/// <summary>
/// Tesla Coil fires every Lightning orb it holds AT ITS TARGET. It was a plain Strike.
/// </summary>
public class TeslaCoilTests
{
    private const int TeslaCoil = 497;

    [Theory]
    [InlineData(false, 3, 1)]
    [InlineData(true, 4, 2)]
    public void HitsThenFiresEveryLightningOrb(bool upgraded, int damage, int times)
    {
        var fight = DefectFight.Hand(Card(TeslaCoil, upgraded)).Energy(0).Enemy(hp: 400);
        CardEffects.ChannelOrb(fight.State, OrbType.Lightning);
        CardEffects.ChannelOrb(fight.State, OrbType.Lightning);
        CardEffects.ChannelOrb(fight.State, OrbType.Frost);

        fight.Play();

        // The card's own damage, plus 3 per Lightning orb per trigger.
        Assert.Equal(400 - damage - 2 * times * 3, fight.Enemy0.Hp);
    }

    /// <summary>The orbs stay — this is the passive, not an evoke.</summary>
    [Fact]
    public void TheOrbsAreNotSpent()
    {
        var fight = DefectFight.Hand(Card(TeslaCoil)).Energy(0).Enemy(hp: 400);
        CardEffects.ChannelOrb(fight.State, OrbType.Lightning);

        fight.Play();

        Assert.Single(fight.State.Orbs);
    }

    /// <summary>With no Lightning orb it is just a small attack.</summary>
    [Fact]
    public void WithNoLightningOrbItIsJustTheDamage()
    {
        var fight = DefectFight.Hand(Card(TeslaCoil)).Energy(0).Enemy(hp: 400);
        CardEffects.ChannelOrb(fight.State, OrbType.Frost);

        fight.Play();

        Assert.Equal(400 - 3, fight.Enemy0.Hp);
    }
}

public class ThunderTests
{
    private const int Thunder = 507;

    /// <summary>`AfterOrbEvoked` on a LIGHTNING orb adds its amount at the orb's target.</summary>
    [Theory]
    [InlineData(false, 6)]
    [InlineData(true, 8)]
    public void ALightningEvokeHitsHarder(bool upgraded, int extra)
    {
        var fight = DefectFight.Hand(Card(Thunder, upgraded)).Energy(1).Enemy(hp: 400);
        fight.Play();
        CardEffects.ChannelOrb(fight.State, OrbType.Lightning);

        CardEffects.EvokeNextOrb(fight.State, new Random(0));

        Assert.Equal(400 - 8 - extra, fight.Enemy0.Hp);
    }
}

public class WhiteNoiseTests
{
    private const int WhiteNoise = 540;

    [Fact]
    public void AddsAFreePowerCardToHand()
    {
        var fight = DefectFight.Hand(Card(WhiteNoise)).Energy(1);

        fight.Play();

        var added = Assert.Single(fight.State.Hand);
        Assert.Equal(CardType.Power, GeneratedData.Cards.Get(added.DefId).Type);
        Assert.True(added.FreeThisTurn);
    }
}
