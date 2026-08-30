using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/CallOfTheVoid.cs: CardsVar 1, and the upgrade adds
// Innate — not a second card. `CallOfTheVoidPower.BeforeHandDraw` puts that many cards
// from the character's OWN pool into hand every turn, each granted ETHEREAL. The emulator
// was granting a one-shot extra draw next turn.
public class CallOfTheVoidTests
{
    private const int CallOfTheVoid = 76;

    private static Fight Played(bool upgraded = false)
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(CallOfTheVoid, upgraded));
        fight.Play(0);
        return fight;
    }

    [Fact]
    public void ItAppliesOneStackWhetherUpgradedOrNot()
    {
        Assert.Equal(1, BuffSystem.Get(Played().State.PlayerBuffs, BuffId.CallOfTheVoid));
        Assert.Equal(1, BuffSystem.Get(Played(true).State.PlayerBuffs, BuffId.CallOfTheVoid));
    }

    [Fact]
    public void ItAddsAnEtherealCardToHandEachTurn()
    {
        var fight = Played();
        int before = fight.State.Hand.Count;

        fight.EndTurn();

        Assert.Contains(fight.State.Hand, card => card.EtherealForCombat);
        Assert.True(fight.State.Hand.Count > before);
    }

    /// <summary>The grant is per COPY, so the card really is Ethereal to every power.</summary>
    [Fact]
    public void TheGrantedCardCountsAsEthereal()
    {
        var fight = Played();
        fight.EndTurn();

        var granted = fight.State.Hand.First(card => card.EtherealForCombat);

        Assert.True(granted.IsEthereal());
    }

    [Fact]
    public void ItGrantsNoOneShotDraw()
    {
        Assert.Equal(0, BuffSystem.Get(Played().State.PlayerBuffs, BuffId.NextTurnDraw));
    }
}

// MegaCrit.Sts2.Core.Models.Cards/DanseMacabre.cs: PowerVar 4 upgrading by 2, and an
// EnergyVar of 2. `DanseMacabrePower.BeforeCardPlayed` gains the power's amount in
// Unpowered block when the card played has a RESOLVED cost of 2 or more. The emulator read
// that 2 as energy next turn.
public class DanseMacabreTests
{
    private const int DanseMacabre = 118;
    private const int Uppercut = 529; // costs 2
    private const int Strike = 473; // costs 1

    private static Fight Armed(bool upgraded = false)
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(DanseMacabre, upgraded));
        fight.Play(0);
        return fight;
    }

    [Fact]
    public void ATwoCostCardGainsFourBlock()
    {
        var fight = Armed();
        fight.State.Hand.Add(new CardInstance(Uppercut, false));

        fight.Play(0, target: 0);

        Assert.Equal(4, fight.State.PlayerBlock);
    }

    [Fact]
    public void TheUpgradeGainsSix()
    {
        var fight = Armed(upgraded: true);
        fight.State.Hand.Add(new CardInstance(Uppercut, false));

        fight.Play(0, target: 0);

        Assert.Equal(6, fight.State.PlayerBlock);
    }

    [Fact]
    public void ACheaperCardGainsNothing()
    {
        var fight = Armed();
        fight.State.Hand.Add(new CardInstance(Strike, false));

        fight.Play(0, target: 0);

        Assert.Equal(0, fight.State.PlayerBlock);
    }

    /// <summary>RESOLVED, so a card the player got for free pays nothing.</summary>
    [Fact]
    public void AFreeTwoCostCardGainsNothing()
    {
        var fight = Armed();
        fight.State.Hand.Add(new CardInstance(Uppercut, false) { FreeThisTurn = true });

        fight.Play(0, target: 0);

        Assert.Equal(0, fight.State.PlayerBlock);
    }

    [Fact]
    public void ItGrantsNoEnergyNextTurn()
    {
        Assert.Equal(0, BuffSystem.Get(Armed().State.PlayerBuffs, BuffId.NextTurnEnergy));
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Haunt.cs: HpLossVar 6 upgrading by 2.
// `HauntPower.AfterCardPlayed` deals that much Unblockable, Unpowered damage to one random
// enemy when a SOUL is played. The card has no damage and no block of its own, so the
// ApplyBaseDamageAndBlock arm it shared did nothing at all.
public class HauntTests
{
    private const int Haunt = 237;
    private const int Soul = 446;
    private const int Strike = 473;

    private static Fight Armed(bool upgraded = false)
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Haunt, upgraded));
        fight.Play(0);
        return fight;
    }

    [Fact]
    public void PlayingASoulHitsForSix()
    {
        var fight = Armed();
        fight.State.Hand.Add(new CardInstance(Soul, false));

        fight.Play(0);

        Assert.Equal(494, fight.Enemy0.Hp);
    }

    [Fact]
    public void TheUpgradeHitsForEight()
    {
        var fight = Armed(upgraded: true);
        fight.State.Hand.Add(new CardInstance(Soul, false));

        fight.Play(0);

        Assert.Equal(492, fight.Enemy0.Hp);
    }

    /// <summary>Unblockable: a shield does not stop it.</summary>
    [Fact]
    public void BlockDoesNotStopIt()
    {
        var fight = Armed();
        fight.Enemy0.Block = 100;
        fight.State.Hand.Add(new CardInstance(Soul, false));

        fight.Play(0);

        Assert.Equal(494, fight.Enemy0.Hp);
        Assert.Equal(100, fight.Enemy0.Block);
    }

    [Fact]
    public void AnyOtherCardHitsNobodyExtra()
    {
        var fight = Armed();
        fight.State.Hand.Add(new CardInstance(Strike, false));
        int before = fight.Enemy0.Hp;

        fight.Play(0, target: 0);

        Assert.True(before - fight.Enemy0.Hp < 6 + 6);
    }
}
