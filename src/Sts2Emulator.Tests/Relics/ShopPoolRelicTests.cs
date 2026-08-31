using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Sts2Emulator.Core.Run;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// The shop pool's combat-side relics. Shop relics are only reachable through a merchant,
// which is why they sat behind the rest of the shared pool.

public class BreadTests
{
    /// <summary>
    /// Turn one is charged 2 energy; every turn after gains 1. A bad first turn bought
    /// with a better every turn after, and modelling only one half inverts the trade.
    /// </summary>
    [Fact]
    public void ItCostsTwoOnTurnOneAndPaysOneAfter()
    {
        var fight = Fight.WithRelics(RelicEffects.Bread);
        fight.State.PlayerHp = 999;
        int max = fight.State.MaxEnergy;

        fight.EndTurn();

        Assert.Equal(max + 1, fight.State.Energy);
    }
}

public class ChemicalXTests
{
    [Fact]
    public void EveryXCostCardResolvesTwoHigher()
    {
        var fight = Fight.WithRelics(RelicEffects.ChemicalX);
        fight.State.Hand = [Card(495)]; // Tempest, an X-cost card
        fight.State.Energy = 1;
        fight.State.OrbCapacity = 10;

        fight.Play();

        // One energy plus Chemical X's two.
        Assert.Equal(3, fight.State.Orbs.Count);
    }
}

public class MysticLighterTests
{
    /// <summary>Nine more damage from an attack played off an ENCHANTED card, any enchantment.</summary>
    [Fact]
    public void AnEnchantedAttackHitsForNineMore()
    {
        var fight = Fight.WithRelics(RelicEffects.MysticLighter);
        fight.State.Energy = 9;

        fight.State.Hand = [Card(SI.Slice)];
        int before = fight.Enemy0.Hp;
        fight.Play();
        Assert.Equal(before - 6, fight.Enemy0.Hp);

        fight.State.Hand =
        [
            Card(SI.Slice) with
            {
                Enchantment = Enchantment.Sharp,
                EnchantAmount = 2,
            },
        ];
        before = fight.Enemy0.Hp;
        fight.Play();

        // Six, plus Sharp's two, plus the Lighter's nine.
        Assert.Equal(before - 17, fight.Enemy0.Hp);
    }
}

public class TheAbacusTests
{
    [Fact]
    public void EveryShuffleIsSixUnpoweredBlock()
    {
        var fight = Fight.WithRelics(RelicEffects.TheAbacus);
        fight.State.DrawPile.Clear();
        fight.State.DiscardPile.Clear();
        fight.State.DiscardPile.Add(new CardInstance(SI.Slice, false));
        int before = fight.State.PlayerBlock;

        CardEffects.DrawCards(fight.State, 1, new Random(0));

        Assert.Equal(before + 6, fight.State.PlayerBlock);
    }
}

public class BurningSticksTests
{
    /// <summary>The first SKILL exhausted each combat comes back to hand. Once, and Skills only.</summary>
    [Fact]
    public void TheFirstSkillExhaustedComesBack()
    {
        var fight = Fight.WithRelics(RelicEffects.BurningSticks);
        fight.State.Hand = [];

        CardEffects.ExhaustCard(
            fight.State,
            new CardInstance(SI.DefendSilent, false),
            rng: new Random(0)
        );
        Assert.Contains(fight.State.Hand, c => c.DefId == SI.DefendSilent);

        fight.State.Hand.Clear();
        CardEffects.ExhaustCard(
            fight.State,
            new CardInstance(SI.DefendSilent, false),
            rng: new Random(0)
        );
        Assert.Empty(fight.State.Hand);
    }

    [Fact]
    public void AnExhaustedAttackDoesNotSpendIt()
    {
        var fight = Fight.WithRelics(RelicEffects.BurningSticks);
        fight.State.Hand = [];

        CardEffects.ExhaustCard(fight.State, new CardInstance(SI.Slice, false), rng: new Random(0));
        Assert.Empty(fight.State.Hand);

        CardEffects.ExhaustCard(
            fight.State,
            new CardInstance(SI.DefendSilent, false),
            rng: new Random(0)
        );
        Assert.Contains(fight.State.Hand, c => c.DefId == SI.DefendSilent);
    }
}

public class BeltBuckleTests
{
    /// <summary>
    /// Dexterity 2 while the belt is EMPTY, and it toggles — a once-at-combat-start reading
    /// would give it to a player who then drinks their way out of it.
    /// </summary>
    [Fact]
    public void ItTogglesWithThePotionBelt()
    {
        var fight = Fight.WithRelics(RelicEffects.BeltBuckle);
        Array.Clear(fight.State.PotionSlots);
        RelicEffects.ApplyCombatStart(fight.State, new Random(0));
        // The combat-start hook ran twice here; the flag stops it stacking.
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.Dexterity));

        fight.State.PotionSlots[0] = 1;
        RelicEffects.RefreshBeltBuckle(fight.State);
        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.Dexterity));

        fight.State.PotionSlots[0] = 0;
        RelicEffects.RefreshBeltBuckle(fight.State);
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.Dexterity));
    }

    [Fact]
    public void AFullBeltMeansNoDexterity()
    {
        var fight = Fight.WithRelics(RelicEffects.BeltBuckle);
        fight.State.PotionSlots[0] = 1;
        RelicEffects.RefreshBeltBuckle(fight.State);

        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.Dexterity));
    }
}

public class RingingTriangleTests
{
    /// <summary>`ShouldFlush` is false on turn ONE, so the opening hand is kept whole.</summary>
    [Fact]
    public void TheOpeningHandIsNotDiscarded()
    {
        var fight = Fight.WithRelics(RelicEffects.RingingTriangle);
        fight.State.PlayerHp = 999;
        // Ethereal cards exhaust before the retain check, so an Ascender's Bane in the
        // opening hand goes whatever the relic says -- the relic stops the FLUSH, not the
        // Ethereal exhaust.
        var opening = fight.State.Hand.Where(c => !c.IsEthereal()).Select(c => c.DefId).ToList();

        fight.EndTurn();

        Assert.All(opening, id => Assert.Contains(fight.State.Hand, c => c.DefId == id));
    }

    [Fact]
    public void LaterHandsAreDiscardedAsUsual()
    {
        var fight = Fight.WithRelics(RelicEffects.RingingTriangle);
        fight.State.PlayerHp = 999;
        fight.EndTurn();

        // A deep draw pile so a card that IS discarded cannot be reshuffled and drawn
        // straight back, which would read as a retain.
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 30; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.StrikeSilent, false));
        }

        fight.State.Hand = [Card(SI.Backstab)];

        fight.EndTurn();

        Assert.DoesNotContain(fight.State.Hand, c => c.DefId == SI.Backstab);
    }
}

public class GhostSeedTests
{
    /// <summary>
    /// Basic Strikes and Defends gain Ethereal — so they vanish at the end of a turn they
    /// are not played on.
    /// </summary>
    [Fact]
    public void BasicStrikesAndDefendsVanish()
    {
        var fight = Fight.WithRelics(RelicEffects.GhostSeed);
        fight.State.PlayerHp = 999;
        fight.State.Hand = [Card(SI.StrikeSilent), Card(SI.DefendSilent), Card(SI.Backstab)];

        fight.EndTurn();

        Assert.Equal(
            2,
            fight.State.ExhaustPile.Count(c => c.DefId is SI.StrikeSilent or SI.DefendSilent)
        );
        Assert.Contains(fight.State.DiscardPile, c => c.DefId == SI.Backstab);
    }

    /// <summary>Only BASIC ones — a rare attack keeps its own keywords.</summary>
    [Fact]
    public void NonBasicCardsAreUntouched()
    {
        var fight = Fight.WithRelics(RelicEffects.GhostSeed);
        fight.State.PlayerHp = 999;
        fight.State.Hand = [Card(SI.Backstab)];

        fight.EndTurn();

        Assert.DoesNotContain(fight.State.ExhaustPile, c => c.DefId == SI.Backstab);
    }
}

public class SlingOfCourageTests
{
    [Fact]
    public void TwoStrengthInAnEliteRoom()
    {
        var elite = new CombatState { IsEliteRoom = true };
        CombatFactory.Reset(
            elite,
            new Random(0),
            TestDeck.StarterDeckIds,
            1,
            [RelicEffects.SlingOfCourage]
        );
        Assert.Equal(2, BuffSystem.Get(elite.PlayerBuffs, BuffId.Strength));
    }

    [Fact]
    public void AndNoneAnywhereElse()
    {
        var normal = new CombatState();
        CombatFactory.Reset(
            normal,
            new Random(0),
            TestDeck.StarterDeckIds,
            1,
            [RelicEffects.SlingOfCourage]
        );
        Assert.Equal(0, BuffSystem.Get(normal.PlayerBuffs, BuffId.Strength));
    }
}

public class MiniatureTentTests
{
    private static RunEngine AtARestSite(params int[] relicIds)
    {
        var engine = new RunEngine();
        engine.Reset("TENT");
        engine.State.Phase = RunPhase.Rest;
        engine.State.RestOptionsTaken = 0;
        foreach (int id in relicIds)
        {
            engine.State.Relics.Add(new RelicInstance(id));
        }

        return engine;
    }

    /// <summary>
    /// `ShouldDisableRemainingRestSiteOptions` returns FALSE, so taking one option leaves
    /// the others available — but the one taken is spent, so the Tent buys another
    /// DIFFERENT option rather than the same one twice.
    /// </summary>
    [Fact]
    public void TakingAnOptionLeavesTheOthersAvailable()
    {
        var engine = AtARestSite(RelicEffects.MiniatureTent);
        engine.Step(RunConstants.RestHealAction, -1, out _, out _, out _);

        Assert.Equal(RunPhase.Rest, engine.State.Phase);
        Assert.False(engine.State.RestResultPending);

        var mask = new int[RunConstants.MaxActions];
        engine.WriteActionMask(mask);
        Assert.Equal(0, mask[RunConstants.RestHealAction]);
        Assert.NotEqual(0, mask[RunConstants.RestUpgradeAction]);
    }

    [Fact]
    public void WithoutItTheFirstOptionEndsTheVisit()
    {
        var engine = AtARestSite();
        engine.Step(RunConstants.RestHealAction, -1, out _, out _, out _);

        Assert.True(engine.State.RestResultPending);
    }
}
