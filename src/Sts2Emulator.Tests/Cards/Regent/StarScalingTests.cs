using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/CrescentSpear.cs: one star, and its damage is
// CalculationBase 8 plus ExtraDamage 2 (upgrading by 1) for each card with a STAR COST the
// player holds — `AllCards.Count(c => c.CanonicalStarCost >= 0 || c.HasStarCostX)`.
// AllCards spans every pile including Play, so the spear counts itself.
//
// The emulator had it in a flat 8/12 body with nine other cards.
public class CrescentSpearTests
{
    private const int CrescentSpear = 112;
    private const int FallingStar = 179; // 2 stars
    private const int StrikeRegent = 474; // none

    private static Fight Fresh() => RegentBoard.Fresh();

    [Fact]
    public void AloneItCountsOnlyItself()
    {
        var fight = Fresh();
        fight.State.Hand.Add(new CardInstance(CrescentSpear, false));

        fight.Play(0, target: 0);

        Assert.Equal(500 - 10, fight.Enemy0.Hp);
    }

    [Fact]
    public void EachStarCostCardHeldAddsTwo()
    {
        var fight = Fresh();
        fight.State.DrawPile.Add(new CardInstance(FallingStar, false));
        fight.State.DiscardPile.Add(new CardInstance(FallingStar, false));
        fight.State.Hand.Add(new CardInstance(CrescentSpear, false));

        fight.Play(fight.State.Hand.Count - 1, target: 0);

        Assert.Equal(500 - 14, fight.Enemy0.Hp);
    }

    [Fact]
    public void CardsWithoutAStarCostDoNotCount()
    {
        var fight = Fresh();
        for (int i = 0; i < 5; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(StrikeRegent, false));
        }

        fight.State.Hand.Add(new CardInstance(CrescentSpear, false));
        fight.Play(fight.State.Hand.Count - 1, target: 0);

        Assert.Equal(500 - 10, fight.Enemy0.Hp);
    }

    [Fact]
    public void TheUpgradeIsThreePerCard()
    {
        var fight = Fresh();
        fight.State.Hand.Add(new CardInstance(CrescentSpear, true));

        fight.Play(0, target: 0);

        Assert.Equal(500 - 11, fight.Enemy0.Hp);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/CrushUnder.cs: 7/8 at ALL enemies, then a StrengthLoss of
// 1/2 on all of them. The emulator hit one enemy and debuffed one.
public class CrushUnderTests
{
    private const int CrushUnder = 115;

    private static Fight TwoEnemies()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500).Enemy(hp: 500);
        fight.State.Stars = 9;
        return fight;
    }

    [Fact]
    public void ItHitsEveryoneAndTakesStrengthFromEveryone()
    {
        var fight = TwoEnemies();
        fight.State.Hand.Add(new CardInstance(CrushUnder, false));

        fight.Play(0, target: 0);

        Assert.Equal(493, fight.Enemy0.Hp);
        Assert.Equal(493, fight.State.Enemies[1].Hp);
        Assert.Equal(-1, fight.EnemyBuffAmount(BuffId.Strength, 0));
        Assert.Equal(-1, fight.EnemyBuffAmount(BuffId.Strength, 1));
    }

    [Fact]
    public void TheUpgradeRaisesBoth()
    {
        var fight = TwoEnemies();
        fight.State.Hand.Add(new CardInstance(CrushUnder, true));

        fight.Play(0, target: 0);

        Assert.Equal(492, fight.Enemy0.Hp);
        Assert.Equal(-2, fight.EnemyBuffAmount(BuffId.Strength, 1));
    }

    /// <summary>Temporary: they have it back once their turn is over.</summary>
    [Fact]
    public void TheStrengthComesBack()
    {
        var fight = TwoEnemies();
        fight.State.Hand.Add(new CardInstance(CrushUnder, false));
        fight.Play(0, target: 0);

        fight.EndTurn();

        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Strength, 0));
    }
}

// MegaCrit.Sts2.Core.Models.Cards/DyingStar.cs: three stars, Ethereal, 9/11 at ALL enemies
// and a StrengthLoss of 9/11 on each. The emulator hit one for the right damage and took
// 3/5 Strength off one — a number that appears nowhere on the card.
public class DyingStarTests
{
    private const int DyingStar = 158;

    private static Fight TwoEnemies()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500).Enemy(hp: 500);
        fight.State.Stars = 9;
        return fight;
    }

    [Fact]
    public void ItHitsEveryoneForNineAndTakesNine()
    {
        var fight = TwoEnemies();
        fight.State.Hand.Add(new CardInstance(DyingStar, false));

        fight.Play(0, target: 0);

        Assert.Equal(491, fight.Enemy0.Hp);
        Assert.Equal(491, fight.State.Enemies[1].Hp);
        Assert.Equal(-9, fight.EnemyBuffAmount(BuffId.Strength, 0));
        Assert.Equal(-9, fight.EnemyBuffAmount(BuffId.Strength, 1));
    }

    [Fact]
    public void TheUpgradeIsElevenAndEleven()
    {
        var fight = TwoEnemies();
        fight.State.Hand.Add(new CardInstance(DyingStar, true));

        fight.Play(0, target: 0);

        Assert.Equal(489, fight.Enemy0.Hp);
        Assert.Equal(-11, fight.EnemyBuffAmount(BuffId.Strength, 0));
    }

    [Fact]
    public void ItCostsThreeStars()
    {
        var fight = TwoEnemies();
        fight.State.Stars = 3;
        fight.State.Hand.Add(new CardInstance(DyingStar, false));

        fight.Play(0, target: 0);

        Assert.Equal(0, fight.State.Stars);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/ForegoneConclusion.cs: ForegoneConclusionPower at CardsVar
// 2 upgrading by 1. BEFORE the next hand draw, that many cards CHOSEN from the draw pile go
// to hand, and the power removes itself. It had been sharing a body that drew one card.
public class ForegoneConclusionTests
{
    private const int ForegoneConclusion = 204;

    private static Fight Played(bool upgraded = false)
    {
        // An explicit draw pile: the screen offers what is in it, and a pile that runs dry
        // stops reopening rather than asking for a card that is not there.
        var fight = Fight
            .Hand()
            .Energy(9)
            .Draw(
                new CardInstance(RegentBoard.StrikeRegent, false),
                new CardInstance(RegentBoard.StrikeRegent, false),
                new CardInstance(RegentBoard.StrikeRegent, false),
                new CardInstance(RegentBoard.StrikeRegent, false),
                new CardInstance(RegentBoard.StrikeRegent, false),
                new CardInstance(RegentBoard.StrikeRegent, false),
                new CardInstance(RegentBoard.StrikeRegent, false),
                new CardInstance(RegentBoard.StrikeRegent, false)
            )
            .Enemy(hp: 500);
        fight.State.Stars = 9;
        fight.State.Hand.Add(new CardInstance(ForegoneConclusion, upgraded));
        fight.Play(0);
        return fight;
    }

    [Fact]
    public void ItPromisesTwoPicks()
    {
        Assert.Equal(2, BuffSystem.Get(Played().State.PlayerBuffs, BuffId.ForegoneConclusion));
        Assert.Null(Played().Pending);
    }

    [Fact]
    public void TheScreenComesUpNextTurn()
    {
        var fight = Played();

        fight.EndTurn();

        Assert.Equal(CardSelectionKind.DrawPileToHand, fight.Pending!.Kind);
    }

    /// <summary>Two picks, so the screen reopens once.</summary>
    [Fact]
    public void ItAsksTwiceAndThenStops()
    {
        var fight = Played();
        fight.EndTurn();

        fight.Choose(0);
        Assert.NotNull(fight.Pending);

        fight.Choose(0);
        Assert.Null(fight.Pending);
    }

    [Fact]
    public void ThePowerIsSpentAfterOneTurn()
    {
        var fight = Played();
        fight.EndTurn();
        fight.Choose(0);
        fight.Choose(0);

        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.ForegoneConclusion));
    }

    [Fact]
    public void TheUpgradePromisesThree()
    {
        Assert.Equal(3, BuffSystem.Get(Played(true).State.PlayerBuffs, BuffId.ForegoneConclusion));
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Furnace.cs: FurnacePower at ForgeVar 5, upgrading by 2 —
// a Forge of that much at the start of EVERY player turn. It had been one of thirty labels
// on a flat Strength body.
public class FurnaceTests
{
    private const int Furnace = 210;

    private static Fight Played(bool upgraded = false)
    {
        var fight = RegentBoard.Fresh();
        fight.State.Hand.Add(new CardInstance(Furnace, upgraded));
        fight.Play(0);
        return fight;
    }

    [Fact]
    public void ItForgesNothingOnTheTurnItIsPlayed()
    {
        var fight = Played();

        Assert.Equal(5, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Furnace));
        Assert.Empty(fight.State.Hand.Where(c => c.DefId == RegentBoard.SovereignBlade));
    }

    [Fact]
    public void ItForgesFiveEveryTurn()
    {
        var fight = Played();

        fight.EndTurn();
        Assert.Equal(5, fight.ForgedDamage());

        fight.EndTurn();
        Assert.Equal(10, fight.ForgedDamage());
    }

    [Fact]
    public void TheUpgradeForgesSeven()
    {
        var fight = Played(upgraded: true);

        fight.EndTurn();

        Assert.Equal(7, fight.ForgedDamage());
    }
}
