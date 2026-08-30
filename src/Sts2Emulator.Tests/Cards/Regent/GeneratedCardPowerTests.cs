using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/PillarOfCreation.cs: `BlockVar(3, Unpowered)` upgrading by
// 1. Its power gains that much block for every card its owner GENERATES — the same
// `AfterCardGeneratedForCombat` hook Arsenal pays Strength from.
//
// The emulator granted block NEXT TURN, once.
public class PillarOfCreationTests
{
    private const int PillarOfCreation = 354;
    private const int Dirge = 145; // makes one Soul per energy

    private static Fight Armed(bool upgraded = false)
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(PillarOfCreation, upgraded));
        fight.Play(0);
        return fight;
    }

    [Fact]
    public void PlayingItGainsNoBlockAndPromisesNoneNextTurn()
    {
        var fight = Armed();

        Assert.Equal(3, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.PillarOfCreation));
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.BlockNextTurn));
    }

    [Fact]
    public void EachGeneratedCardGainsThreeBlock()
    {
        var fight = Armed();
        fight.State.Energy = 3;
        fight.State.Hand.Add(new CardInstance(Dirge, false));

        fight.Play(0);

        // Three energy through Dirge is three Souls, so nine block.
        Assert.Equal(9, fight.State.PlayerBlock);
    }

    [Fact]
    public void TheUpgradeGainsFourPerCard()
    {
        var fight = Armed(upgraded: true);
        fight.State.Energy = 2;
        fight.State.Hand.Add(new CardInstance(Dirge, false));

        fight.Play(0);

        Assert.Equal(8, fight.State.PlayerBlock);
    }

    /// <summary>It and Arsenal read the same hook, so a generated card pays both.</summary>
    [Fact]
    public void ItAndArsenalBothPay()
    {
        var fight = Armed();
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Arsenal, 1);
        fight.State.Energy = 2;
        fight.State.Hand.Add(new CardInstance(Dirge, false));

        fight.Play(0);

        Assert.Equal(6, fight.State.PlayerBlock);
        Assert.Equal(2, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));
    }
}

// MegaCrit.Sts2.Core.Models.Cards/PaleBlueDot.cs: CardsVar 1 upgrading by 1, behind a
// threshold of five. The power draws that many more, but only when the player finished at
// least five card plays LAST turn — a threshold on the previous turn, not a running total.
public class PaleBlueDotTests
{
    private const int PaleBlueDot = 341;
    private const int DefendRegent = 133;

    private static Fight Armed(bool upgraded = false)
    {
        var fight = Fight.Hand().Energy(20).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(PaleBlueDot, upgraded));
        fight.Play(0);
        return fight;
    }

    private static void PlayCards(Fight fight, int count)
    {
        for (int i = 0; i < count; i++)
        {
            fight.State.Hand.Add(new CardInstance(DefendRegent, false));
            fight.Play(fight.State.Hand.Count - 1);
        }
    }

    [Fact]
    public void UnderTheThresholdItDrawsNothingExtra()
    {
        var control = Fight.Hand().Energy(20).Enemy(hp: 500);
        control.EndTurn();
        int plain = control.State.Hand.Count;

        var fight = Armed();
        // Pale Blue Dot itself is one play; three more is four, still under five.
        PlayCards(fight, 3);
        fight.EndTurn();

        Assert.Equal(plain, fight.State.Hand.Count);
    }

    [Fact]
    public void FivePlaysLastTurnDrawsOneMore()
    {
        var control = Fight.Hand().Energy(20).Enemy(hp: 500);
        control.EndTurn();
        int plain = control.State.Hand.Count;

        var fight = Armed();
        PlayCards(fight, 4); // five plays counting the Dot
        fight.EndTurn();

        Assert.Equal(plain + 1, fight.State.Hand.Count);
    }

    /// <summary>LAST turn, so a quiet turn switches it back off.</summary>
    [Fact]
    public void AQuietTurnTurnsItOffAgain()
    {
        var control = Fight.Hand().Energy(20).Enemy(hp: 500);
        control.EndTurn();
        control.EndTurn();
        int plain = control.State.Hand.Count;

        var fight = Armed();
        PlayCards(fight, 4);
        fight.EndTurn();
        fight.EndTurn();

        Assert.Equal(plain, fight.State.Hand.Count);
    }

    [Fact]
    public void TheUpgradeDrawsTwoMore()
    {
        var control = Fight.Hand().Energy(20).Enemy(hp: 500);
        control.EndTurn();
        int plain = control.State.Hand.Count;

        var fight = Armed(upgraded: true);
        PlayCards(fight, 4);
        fight.EndTurn();

        Assert.Equal(plain + 2, fight.State.Hand.Count);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/ParticleWall.cs: two stars for 9 block, and a
// `GetResultPileTypeForCardPlay` that turns the DISCARD result into HAND — the card comes
// back instead of being spent. The emulator discarded it.
public class ParticleWallTests
{
    private const int ParticleWall = 346;

    [Fact]
    public void ItBlocksNineAndComesBack()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Stars = 2;
        fight.State.Hand.Add(new CardInstance(ParticleWall, false));

        fight.Play(0);

        Assert.Equal(9, fight.State.PlayerBlock);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.Hand.Where(c => c.DefId == ParticleWall));
        Assert.Equal(0, fight.State.Stars);
    }

    /// <summary>Twice, if the stars are there — that is the whole point of it returning.</summary>
    [Fact]
    public void ItCanBePlayedAgainImmediately()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Stars = 4;
        fight.State.Hand.Add(new CardInstance(ParticleWall, false));

        fight.Play(0);
        fight.Play(0);

        Assert.Equal(18, fight.State.PlayerBlock);
        Assert.Equal(0, fight.State.Stars);
    }
}
