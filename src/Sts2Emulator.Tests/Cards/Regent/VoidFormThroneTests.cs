using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// `CardPileCmd.Draw` STOPS at a full hand — `if (num <= 0) break`, and again on
/// `hand.Cards.Count >= MaxCardsInHand` — so the card that will not fit stays in the DRAW
/// PILE. The emulator used to draw it anyway and put the overflow in the discard.
/// </summary>
/// <remarks>
/// A live Prophesize found it: six cards asked for, five drawn into a hand with five slots,
/// and the sixth still on top of the pile where the emulator had moved it to the discard.
/// A different card in a different place, on every draw effect in the game.
/// </remarks>
public class DrawAtHandCapTests
{
    private const int StrikeRegent = 474;

    [Fact]
    public void ACardThatWillNotFitStaysInTheDrawPile()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        for (int i = 0; i < 9; i++)
        {
            fight.State.Hand.Add(new CardInstance(StrikeRegent, false));
        }

        fight.State.DrawPile.Clear();
        fight.State.DrawPile.Add(new CardInstance(StrikeRegent, false));
        fight.State.DrawPile.Add(new CardInstance(StrikeRegent, false));
        fight.State.DrawPile.Add(new CardInstance(StrikeRegent, false));

        CardEffects.DrawCards(fight.State, 3, new Random(0));

        Assert.Equal(10, fight.State.Hand.Count);
        Assert.Equal(2, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
    }

    [Fact]
    public void AFullHandDrawsNothingAtAll()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        for (int i = 0; i < 10; i++)
        {
            fight.State.Hand.Add(new CardInstance(StrikeRegent, false));
        }

        fight.State.DrawPile.Clear();
        fight.State.DrawPile.Add(new CardInstance(StrikeRegent, false));

        CardEffects.DrawCards(fight.State, 3, new Random(0));

        Assert.Single(fight.State.DrawPile);
        Assert.Empty(fight.State.DiscardPile);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Prophesize.cs: CardsVar 6, upgrading by 3. The emulator
// drew 1/2 — the shared body's number, not this card's.
public class ProphesizeTests
{
    private const int Prophesize = 367;
    private const int StrikeRegent = 474;

    private static Fight WithDrawPile(int count)
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.DrawPile.Clear();
        for (int i = 0; i < count; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(StrikeRegent, false));
        }

        return fight;
    }

    [Fact]
    public void ItDrawsSix()
    {
        var fight = WithDrawPile(10);
        fight.State.Hand.Add(new CardInstance(Prophesize, false));

        fight.Play(0);

        Assert.Equal(6, fight.State.Hand.Count);
        Assert.Equal(4, fight.State.DrawPile.Count);
    }

    [Fact]
    public void TheUpgradeDrawsNine()
    {
        var fight = WithDrawPile(12);
        fight.State.Hand.Add(new CardInstance(Prophesize, true));

        fight.Play(0);

        Assert.Equal(9, fight.State.Hand.Count);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/HiddenCache.cs: StarsVar 1 now, and
// `PowerVar<StarNextTurnPower>(3)` upgrading by 1 for next turn. The emulator drew a card
// and gave energy.
public class HiddenCacheTests
{
    private const int HiddenCache = 248;

    private static Fight Played(bool upgraded = false)
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(HiddenCache, upgraded));
        fight.Play(0);
        return fight;
    }

    [Fact]
    public void ItGivesAStarNowAndThreeNextTurn()
    {
        var fight = Played();

        Assert.Equal(1, fight.State.Stars);
        Assert.Equal(3, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.StarNextTurn));
        Assert.Empty(fight.State.Hand);
    }

    [Fact]
    public void TheStarsArriveNextTurn()
    {
        var fight = Played();

        fight.EndTurn();

        Assert.Equal(4, fight.State.Stars);
    }

    [Fact]
    public void TheUpgradePromisesFour()
    {
        Assert.Equal(4, BuffSystem.Get(Played(true).State.PlayerBuffs, BuffId.StarNextTurn));
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Glow.cs: StarsVar 1 (upgrading by 1) and CardsVar 1 spent
// TWICE — a card now and a card next turn. The emulator drew and did neither of the others.
public class GlowTests
{
    private const int Glow = 223;

    private static Fight Played(bool upgraded = false)
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Glow, upgraded));
        fight.Play(0);
        return fight;
    }

    [Fact]
    public void ItGivesAStarACardAndACardNextTurn()
    {
        var fight = Played();

        Assert.Equal(1, fight.State.Stars);
        Assert.Single(fight.State.Hand);
        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.NextTurnDraw));
    }

    /// <summary>Only the stars upgrade.</summary>
    [Fact]
    public void TheUpgradeGivesTwoStarsAndStillOneCard()
    {
        var fight = Played(upgraded: true);

        Assert.Equal(2, fight.State.Stars);
        Assert.Single(fight.State.Hand);
        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.NextTurnDraw));
    }
}

// MegaCrit.Sts2.Core.Models.Cards/TheSealedThrone.cs: three stars, Ancient, one stack, and
// the upgrade is a discount. The power gives a STAR for every card its owner plays.
public class TheSealedThroneTests
{
    private const int TheSealedThrone = 502;
    private const int StrikeRegent = 474;

    private static Fight Played()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Stars = 3;
        fight.State.Hand.Add(new CardInstance(TheSealedThrone, false));
        fight.Play(0);
        return fight;
    }

    [Fact]
    public void ItCostsThreeStarsAndPaysForItself()
    {
        var fight = Played();

        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.TheSealedThrone));
        // The power lands before its own play is counted, so the throne pays nothing for
        // itself: three spent, none back.
        Assert.Equal(0, fight.State.Stars);
    }

    [Fact]
    public void EveryLaterCardIsAStar()
    {
        var fight = Played();

        fight.State.Hand.Add(new CardInstance(StrikeRegent, false));
        fight.Play(0, target: 0);
        Assert.Equal(1, fight.State.Stars);

        fight.State.Hand.Add(new CardInstance(StrikeRegent, false));
        fight.Play(0, target: 0);
        Assert.Equal(2, fight.State.Stars);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/VoidForm.cs: an Ethereal 3-cost applying VoidFormPower 2;
// the upgrade only removes Ethereal. The power makes the first two cards each turn cost
// NOTHING — `TryModifyEnergyCostInCombatLate` AND `TryModifyStarCost`, so energy and stars
// both — and playing it ENDS THE TURN.
//
// Its live capture is parked: the tool snapshots after the play, and there is no moment in
// a turn that has ended but whose enemies have not acted for the emulator to match.
public class VoidFormTests
{
    private const int VoidForm = 534;
    private const int FallingStar = 179; // 0 energy, 2 stars
    private const int StrikeRegent = 474; // 1 energy

    [Fact]
    public void PlayingItEndsTheTurn()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(VoidForm, false));

        fight.Play(0);

        Assert.Equal(2, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.VoidForm));
        // The hand was flushed and a new one drawn, which only an ended turn does.
        Assert.DoesNotContain(fight.State.Hand, c => c.DefId == VoidForm);
    }

    [Fact]
    public void TheFirstTwoCardsOfATurnAreFree()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.VoidForm, 2);
        fight.State.Energy = 2;
        fight.State.Hand.Add(new CardInstance(StrikeRegent, false));
        fight.State.Hand.Add(new CardInstance(StrikeRegent, false));
        fight.State.Hand.Add(new CardInstance(StrikeRegent, false));

        fight.Play(0, target: 0);
        fight.Play(0, target: 0);
        Assert.Equal(2, fight.State.Energy);

        fight.Play(0, target: 0);
        Assert.Equal(1, fight.State.Energy);
    }

    /// <summary>Stars too — `TryModifyStarCost`, or the card is only half free.</summary>
    [Fact]
    public void TheFreeCardsCostNoStarsEither()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.VoidForm, 2);
        fight.State.Stars = 2;
        fight.State.Hand.Add(new CardInstance(FallingStar, false));

        fight.Play(0, target: 0);

        Assert.Equal(2, fight.State.Stars);
    }

    /// <summary>The count resets, so the next turn's first two are free again.</summary>
    [Fact]
    public void TheCountResetsEachTurn()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.VoidForm, 2);
        fight.State.Hand.Add(new CardInstance(StrikeRegent, false));
        fight.State.Hand.Add(new CardInstance(StrikeRegent, false));
        fight.Play(0, target: 0);
        fight.Play(0, target: 0);

        fight.EndTurn();
        fight.State.Energy = 3;
        fight.State.Hand.Add(new CardInstance(StrikeRegent, false));
        fight.Play(fight.State.Hand.Count - 1, target: 0);

        Assert.Equal(3, fight.State.Energy);
    }
}
