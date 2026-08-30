using Sts2Emulator.Core;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/Seance.cs: Ethereal, CardsVar(1), and the upgrade is
// `EnergyCost.UpgradeBy(-1)` — it gets cheaper, not bigger.
//
// `CardSelectCmd.FromCombatPile(PileType.Draw, ...)` then `CardCmd.TransformTo<Soul>` on
// each pick. The emulator transformed DrawPile[0] with no screen at all: which card you
// spend is the whole decision the card offers.
public class SeanceTests
{
    private const int Seance = 413;
    private const int Strike = 473;
    private const int Defend = 132;
    private const int Soul = 446;

    private static Fight WithDrawPile() =>
        Fight
            .Hand()
            .Energy(9)
            .Draw(new CardInstance(Strike, false), new CardInstance(Defend, false))
            .Enemy(hp: 200);

    [Fact]
    public void ItAsksWhichCardInTheDrawPileToSpend()
    {
        var fight = WithDrawPile();
        fight.State.Hand.Add(new CardInstance(Seance, false));

        fight.Play(0);

        Assert.NotNull(fight.Pending);
        Assert.Equal(CardSelectionKind.TransformDrawPileToSoul, fight.Pending!.Kind);
        Assert.Equal(2, fight.Pending.Candidates.Count);
    }

    /// <summary>The chosen card becomes a Soul where it sat — not the top one.</summary>
    [Fact]
    public void TheChosenCardBecomesASoulInPlace()
    {
        var fight = WithDrawPile();
        fight.State.Hand.Add(new CardInstance(Seance, false));

        fight.Play(0);
        fight.Choose(1);

        Assert.Equal(Strike, fight.State.DrawPile[0].DefId);
        Assert.Equal(Soul, fight.State.DrawPile[1].DefId);
        Assert.Null(fight.Pending);
    }

    [Fact]
    public void ItSpendsOnlyOne()
    {
        var fight = WithDrawPile();
        fight.State.Hand.Add(new CardInstance(Seance, false));

        fight.Play(0);
        fight.Choose(0);

        Assert.Single(fight.State.DrawPile.Where(card => card.DefId == Soul));
    }

    [Fact]
    public void AnEmptyDrawPileAsksNothing()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 200);
        fight.State.DrawPile.Clear();
        fight.State.Hand.Add(new CardInstance(Seance, false));

        fight.Play(0);

        Assert.Null(fight.Pending);
    }

    /// <summary>The upgrade is a discount, so an upgraded Seance still spends one card.</summary>
    [Fact]
    public void TheUpgradeIsADiscountNotAnExtraCard()
    {
        var fight = WithDrawPile();
        fight.State.Hand.Add(new CardInstance(Seance, true));

        fight.Play(0);
        fight.Choose(0);

        Assert.Single(fight.State.DrawPile.Where(card => card.DefId == Soul));
        Assert.Null(fight.Pending);
    }
}
