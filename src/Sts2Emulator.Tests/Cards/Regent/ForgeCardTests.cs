using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The ten cards that FORGE and the six around them, one class each because the coverage
/// gate looks for a <c>&lt;Name&gt;Tests</c> class per card. <see cref="ForgeTests" /> holds
/// the mechanic itself; these hold each card's own numbers.
/// </summary>
internal static class RegentBoard
{
    internal const int SovereignBlade = 448;
    internal const int StrikeRegent = 474;

    internal static Fight Fresh(int stars = 9) =>
        WithStars(Fight.Hand().Energy(9).Enemy(hp: 500), stars);

    internal static Fight WithStars(Fight fight, int stars)
    {
        fight.State.Stars = stars;
        return fight;
    }

    /// <summary>
    /// Plays the card from the end of hand and returns the fight. Named PlayCard rather
    /// than Play because an extension method never wins against an instance method of the
    /// same name -- `fight.Play(someDefId)` binds to `Fight.Play(index)` and plays whatever
    /// happens to be at that index.
    /// </summary>
    internal static Fight PlayCard(this Fight fight, int defId, bool upgraded = false, int target = 0)
    {
        fight.State.Hand.Add(new CardInstance(defId, upgraded));
        fight.Play(fight.State.Hand.Count - 1, target: target);
        return fight;
    }

    internal static int ForgedDamage(this Fight fight) =>
        fight.State.Hand.Concat(fight.State.DrawPile).Concat(fight.State.DiscardPile)
            .Concat(fight.State.ExhaustPile)
            .Where(c => c.DefId == SovereignBlade)
            .Select(c => c.BonusDamage)
            .DefaultIfEmpty(-1)
            .Max();
}

// MegaCrit.Sts2.Core.Models.Cards/WroughtInWar.cs: DamageVar 7 and ForgeVar 7, both
// upgrading by 2. The emulator dealt the damage and forged nothing.
public class WroughtInWarTests
{
    private const int WroughtInWar = 544;

    [Fact]
    public void ItHitsForSevenAndForgesSeven()
    {
        var fight = RegentBoard.Fresh().PlayCard(WroughtInWar);

        Assert.Equal(493, fight.Enemy0.Hp);
        Assert.Equal(7, fight.ForgedDamage());
    }

    [Fact]
    public void TheUpgradeRaisesBoth()
    {
        var fight = RegentBoard.Fresh().PlayCard(WroughtInWar, upgraded: true);

        Assert.Equal(491, fight.Enemy0.Hp);
        Assert.Equal(9, fight.ForgedDamage());
    }
}

// MegaCrit.Sts2.Core.Models.Cards/RefineBlade.cs: ForgeVar 9 upgrading by 4, and an energy
// next turn. The emulator upgraded a card in hand.
public class RefineBladeTests
{
    private const int RefineBlade = 389;

    [Fact]
    public void ItForgesNineAndPromisesAnEnergy()
    {
        var fight = RegentBoard.Fresh().PlayCard(RefineBlade);

        Assert.Equal(9, fight.ForgedDamage());
        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.NextTurnEnergy));
    }

    [Fact]
    public void TheUpgradeForgesThirteen()
    {
        Assert.Equal(13, RegentBoard.Fresh().PlayCard(RefineBlade, upgraded: true).ForgedDamage());
    }
}

// MegaCrit.Sts2.Core.Models.Cards/TheSmith.cs: four stars for a ForgeVar of 30, upgrading
// by 10 — the biggest single forge in the pool. The emulator drew a card.
public class TheSmithTests
{
    private const int TheSmith = 503;

    [Fact]
    public void ItForgesThirtyForFourStars()
    {
        var fight = RegentBoard.Fresh(stars: 4).PlayCard(TheSmith);

        Assert.Equal(30, fight.ForgedDamage());
        Assert.Equal(0, fight.State.Stars);
    }

    [Fact]
    public void TheUpgradeForgesForty()
    {
        Assert.Equal(40, RegentBoard.Fresh().PlayCard(TheSmith, upgraded: true).ForgedDamage());
    }
}

// MegaCrit.Sts2.Core.Models.Cards/SummonForth.cs: every Sovereign Blade NOT already in hand
// is pulled there, then ForgeVar 8 upgrading by 3. The emulator added a random class card.
public class SummonForthTests
{
    private const int SummonForth = 478;

    [Fact]
    public void ItForgesEight()
    {
        Assert.Equal(8, RegentBoard.Fresh().PlayCard(SummonForth).ForgedDamage());
    }

    [Fact]
    public void ItPullsABladeOutOfTheDiscardPile()
    {
        var fight = RegentBoard.Fresh();
        fight.State.DiscardPile.Add(new CardInstance(RegentBoard.SovereignBlade, false));

        fight.PlayCard(SummonForth);

        Assert.Empty(fight.State.DiscardPile.Where(c => c.DefId == RegentBoard.SovereignBlade));
        Assert.Single(fight.State.Hand.Where(c => c.DefId == RegentBoard.SovereignBlade));
    }

    [Fact]
    public void TheUpgradeForgesEleven()
    {
        Assert.Equal(11, RegentBoard.Fresh().PlayCard(SummonForth, upgraded: true).ForgedDamage());
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Bulwark.cs: BlockVar 12 upgrading by 3, and ForgeVar 10
// upgrading by 3. The shared block body it sat in had the block and none of the forge.
public class BulwarkTests
{
    private const int Bulwark = 67;

    [Fact]
    public void ItBlocksTwelveAndForgesTen()
    {
        var fight = RegentBoard.Fresh().PlayCard(Bulwark);

        Assert.Equal(12, fight.State.PlayerBlock);
        Assert.Equal(10, fight.ForgedDamage());
    }

    [Fact]
    public void TheUpgradeRaisesBoth()
    {
        var fight = RegentBoard.Fresh().PlayCard(Bulwark, upgraded: true);

        Assert.Equal(15, fight.State.PlayerBlock);
        Assert.Equal(13, fight.ForgedDamage());
    }
}

// MegaCrit.Sts2.Core.Models.Cards/BigBang.cs: draw 1, a star, an energy and a Forge of 5.
// The upgrade only adds Innate. The emulator gave 2/3 energy and 2/3 cards.
public class BigBangTests
{
    private const int BigBang = 40;

    [Fact]
    public void ItGivesOneOfEverythingAndForgesFive()
    {
        var fight = RegentBoard.Fresh(stars: 0);
        fight.State.Energy = 0;
        int handBefore = fight.State.Hand.Count;

        fight.PlayCard(BigBang);

        Assert.Equal(1, fight.State.Stars);
        Assert.Equal(1, fight.State.Energy);
        Assert.Equal(5, fight.ForgedDamage());
        // The card played leaves hand, the draw and the blade arrive.
        Assert.Equal(handBefore + 2, fight.State.Hand.Count);
    }

    [Fact]
    public void TheUpgradeChangesNoneOfTheNumbers()
    {
        var fight = RegentBoard.Fresh(stars: 0);
        fight.State.Energy = 0;

        fight.PlayCard(BigBang, upgraded: true);

        Assert.Equal(1, fight.State.Stars);
        Assert.Equal(5, fight.ForgedDamage());
    }
}

// MegaCrit.Sts2.Core.Models.Cards/SpoilsOfBattle.cs: ForgeVar 5 upgrading by 3, then a
// CardsVar 2 draw that does NOT upgrade. The emulator drew 1/2 and forged nothing.
public class SpoilsOfBattleTests
{
    private const int SpoilsOfBattle = 456;

    [Fact]
    public void ItForgesFiveAndDrawsTwo()
    {
        var fight = RegentBoard.Fresh();
        int handBefore = fight.State.Hand.Count;

        fight.PlayCard(SpoilsOfBattle);

        Assert.Equal(5, fight.ForgedDamage());
        // Two drawn plus the blade, less the card played.
        Assert.Equal(handBefore + 3, fight.State.Hand.Count);
    }

    [Fact]
    public void TheUpgradeForgesEightAndStillDrawsTwo()
    {
        var fight = RegentBoard.Fresh();
        int handBefore = fight.State.Hand.Count;

        fight.PlayCard(SpoilsOfBattle, upgraded: true);

        Assert.Equal(8, fight.ForgedDamage());
        Assert.Equal(handBefore + 3, fight.State.Hand.Count);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/BeatIntoShape.cs: 5/7 damage, then a Forge of
// CalculationBase 5/7 plus CalculationExtra 5/7 per powered hit landed ON THIS TARGET this
// turn — less the hits of its own attack, so it never counts itself. The emulator scaled
// its DAMAGE by the player's block and forged nothing.
public class BeatIntoShapeTests
{
    private const int BeatIntoShape = 35;

    [Fact]
    public void AloneItForgesTheBaseFive()
    {
        var fight = RegentBoard.Fresh().PlayCard(BeatIntoShape);

        Assert.Equal(495, fight.Enemy0.Hp);
        Assert.Equal(5, fight.ForgedDamage());
    }

    [Fact]
    public void EachEarlierHitOnThatTargetAddsFive()
    {
        var fight = RegentBoard.Fresh().PlayCard(RegentBoard.StrikeRegent);
        int afterStrike = fight.Enemy0.Hp;

        fight.PlayCard(BeatIntoShape);

        Assert.Equal(afterStrike - 5, fight.Enemy0.Hp);
        Assert.Equal(10, fight.ForgedDamage());
    }

    [Fact]
    public void TheUpgradeIsSevenAndSeven()
    {
        var fight = RegentBoard.Fresh().PlayCard(BeatIntoShape, upgraded: true);

        Assert.Equal(493, fight.Enemy0.Hp);
        Assert.Equal(7, fight.ForgedDamage());
    }
}

// MegaCrit.Sts2.Core.Models.Cards/SeekingEdge.cs: an inert power the Sovereign Blade reads,
// plus a Forge of 7 upgrading by 4.
public class SeekingEdgeTests
{
    private const int SeekingEdge = 418;

    [Fact]
    public void ItGrantsThePowerAndForgesSeven()
    {
        var fight = RegentBoard.Fresh().PlayCard(SeekingEdge);

        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.SeekingEdge));
        Assert.Equal(7, fight.ForgedDamage());
    }

    [Fact]
    public void TheUpgradeForgesEleven()
    {
        Assert.Equal(11, RegentBoard.Fresh().PlayCard(SeekingEdge, upgraded: true).ForgedDamage());
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Parry.cs: `PowerVar<ParryPower>(10)` upgrading by 4. The
// power does nothing by itself — the Sovereign Blade reads it.
public class ParryTests
{
    private const int Parry = 344;

    [Fact]
    public void ItAppliesTenAndNoBlockNow()
    {
        var fight = RegentBoard.Fresh().PlayCard(Parry);

        Assert.Equal(10, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Parry));
        Assert.Equal(0, fight.State.PlayerBlock);
    }

    [Fact]
    public void TheUpgradeAppliesFourteen()
    {
        var fight = RegentBoard.Fresh().PlayCard(Parry, upgraded: true);

        Assert.Equal(14, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Parry));
    }
}

// MegaCrit.Sts2.Core.Models.Cards/NeutronAegis.cs: five stars for `PowerVar<PlatingPower>(8)`
// upgrading by 3. The emulator gave a flat Strength from a shared body.
public class NeutronAegisTests
{
    private const int NeutronAegis = 324;

    [Fact]
    public void ItPlatesEightForFiveStars()
    {
        var fight = RegentBoard.Fresh(stars: 5).PlayCard(NeutronAegis);

        Assert.Equal(8, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Plating));
        Assert.Equal(0, fight.State.Stars);
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));
    }

    [Fact]
    public void TheUpgradePlatesEleven()
    {
        var fight = RegentBoard.Fresh().PlayCard(NeutronAegis, upgraded: true);

        Assert.Equal(11, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Plating));
    }
}

// MegaCrit.Sts2.Core.Models.Cards/CosmicIndifference.cs: 6/9 block, then a card CHOSEN from
// the DISCARD pile goes on TOP of the draw pile — Headbutt's screen. The emulator gave 7
// block and no choice.
public class CosmicIndifferenceTests
{
    private const int CosmicIndifference = 108;
    private const int DefendRegent = 133;

    [Fact]
    public void ItBlocksSixAndAsksWhichDiscardToRecover()
    {
        var fight = RegentBoard.Fresh();
        fight.State.DiscardPile.Add(new CardInstance(RegentBoard.StrikeRegent, false));
        fight.State.DiscardPile.Add(new CardInstance(DefendRegent, false));

        fight.PlayCard(CosmicIndifference);

        Assert.Equal(6, fight.State.PlayerBlock);
        Assert.Equal(CardSelectionKind.DiscardToDrawPileTop, fight.Pending!.Kind);
    }

    [Fact]
    public void TheChosenCardGoesOnTop()
    {
        var fight = RegentBoard.Fresh();
        fight.State.DiscardPile.Add(new CardInstance(RegentBoard.StrikeRegent, false));
        fight.State.DiscardPile.Add(new CardInstance(DefendRegent, false));
        fight.PlayCard(CosmicIndifference);

        fight.Choose(1);

        Assert.Equal(DefendRegent, fight.State.DrawPile[0].DefId);
    }

    [Fact]
    public void TheUpgradeBlocksNine()
    {
        var fight = RegentBoard.Fresh().PlayCard(CosmicIndifference, upgraded: true);

        Assert.Equal(9, fight.State.PlayerBlock);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/RoyalGamble.cs: five stars for NINE, and it Exhausts. The
// upgrade adds Retain, not a bigger payout. The emulator gained nine ENERGY.
public class RoyalGambleTests
{
    private const int RoyalGamble = 402;

    [Fact]
    public void ItTradesFiveStarsForNine()
    {
        var fight = RegentBoard.Fresh(stars: 5);
        int energyBefore = fight.State.Energy;

        fight.PlayCard(RoyalGamble);

        Assert.Equal(9, fight.State.Stars);
        Assert.Equal(energyBefore, fight.State.Energy);
    }

    [Fact]
    public void ItExhausts()
    {
        var fight = RegentBoard.Fresh(stars: 5).PlayCard(RoyalGamble);

        Assert.Single(fight.State.ExhaustPile);
        Assert.Empty(fight.State.DiscardPile);
    }

    [Fact]
    public void TheUpgradePaysTheSameNine()
    {
        var fight = RegentBoard.Fresh(stars: 5).PlayCard(RoyalGamble, upgraded: true);

        Assert.Equal(9, fight.State.Stars);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/CollisionCourse.cs: 11 damage upgrading by 4, and a DEBRIS
// into hand. The emulator added a free Venerate — a different card, free when it should not
// be.
public class CollisionCourseTests
{
    private const int CollisionCourse = 94;

    [Fact]
    public void ItHitsForElevenAndMakesDebris()
    {
        var fight = RegentBoard.Fresh().PlayCard(CollisionCourse);

        Assert.Equal(489, fight.Enemy0.Hp);
        Assert.Single(fight.State.Hand.Where(c => c.DefId == ST.Debris));
        Assert.Empty(fight.State.Hand.Where(c => c.DefId == 532));
    }

    [Fact]
    public void TheUpgradeHitsForFifteen()
    {
        var fight = RegentBoard.Fresh().PlayCard(CollisionCourse, upgraded: true);

        Assert.Equal(485, fight.Enemy0.Hp);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Convergence.cs: RetainHand 1, an energy next turn, and a
// STAR next turn of 1 upgrading by 1 — three powers, of which the emulator applied one.
public class ConvergenceTests
{
    private const int Convergence = 102;

    [Fact]
    public void ItAppliesAllThree()
    {
        var fight = RegentBoard.Fresh().PlayCard(Convergence);

        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.RetainHand));
        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.NextTurnEnergy));
        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.StarNextTurn));
    }

    /// <summary>The star arrives at the turn's reset, and the power removes itself.</summary>
    [Fact]
    public void TheStarArrivesNextTurnAndOnlyOnce()
    {
        var fight = RegentBoard.Fresh(stars: 0).PlayCard(Convergence);

        fight.EndTurn();
        Assert.Equal(1, fight.State.Stars);

        fight.EndTurn();
        Assert.Equal(1, fight.State.Stars);
    }

    [Fact]
    public void TheUpgradePromisesTwoStars()
    {
        var fight = RegentBoard.Fresh().PlayCard(Convergence, upgraded: true);

        Assert.Equal(2, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.StarNextTurn));
    }
}

// The Forge token itself. Its own numbers live here; the mechanic is in ForgeTests.
public class SovereignBladeTests
{
    [Fact]
    public void AFreshBladeHitsForTen()
    {
        var fight = RegentBoard.Fresh();
        fight.State.Hand.Add(new CardInstance(RegentBoard.SovereignBlade, false));

        fight.Play(0, target: 0);

        Assert.Equal(490, fight.Enemy0.Hp);
    }

    /// <summary>It Retains, which is how it survives to be forged again.</summary>
    [Fact]
    public void ItRetains()
    {
        Assert.True(GeneratedData.Cards.Get(RegentBoard.SovereignBlade).Retain);
    }

    /// <summary>The upgrade is a discount: `EnergyCost.UpgradeBy(-1)`.</summary>
    [Fact]
    public void TheUpgradeIsADiscount()
    {
        var fight = RegentBoard.Fresh();
        var plain = new CardInstance(RegentBoard.SovereignBlade, false);
        var upgraded = new CardInstance(RegentBoard.SovereignBlade, true);

        Assert.Equal(2, CombatEngine.EffectiveCost(plain, fight.State));
        Assert.Equal(1, CombatEngine.EffectiveCost(upgraded, fight.State));
    }
}
