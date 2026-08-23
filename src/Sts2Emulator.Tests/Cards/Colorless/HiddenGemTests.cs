using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Skill. MegaCrit.Sts2.Core.Models.Cards/HiddenGem.cs picks a card in the DRAW pile
// off Rng.CombatCardSelection and raises its BaseReplayCount by IntVar("Replay", 2m) --
// that card then plays itself twice more when drawn; OnUpgrade raises the replay by 1.
//
// This used to draw a card and make the hand free for the turn, which is a different card
// and a much better one. These tests asserted that.
public class HiddenGemTests
{
    [Fact]
    public void GrantsReplayToADrawPileCard()
    {
        var fight = Fight
            .Hand(Card(CL.HiddenGem))
            .Energy(1)
            .Draw(Card(IC.StrikeIronclad), Card(IC.StrikeIronclad))
            .Enemy(hp: 60);

        fight.Play();

        Assert.Equal(2, fight.State.DrawPile.Sum(card => card.ReplayCount));
        Assert.Equal(2, fight.State.DrawPile.Count);
    }

    [Fact]
    public void UpgradedGrantsThree()
    {
        var fight = Fight
            .Hand(Card(CL.HiddenGem, upgraded: true))
            .Energy(1)
            .Draw(Card(IC.StrikeIronclad))
            .Enemy(hp: 60);

        fight.Play();

        Assert.Equal(3, fight.State.DrawPile.Sum(card => card.ReplayCount));
    }

    /// <summary>It draws nothing -- the card stays in the pile it was in.</summary>
    [Fact]
    public void DrawsNothingAndLeavesTheHandAlone()
    {
        var fight = Fight
            .Hand(Card(CL.HiddenGem))
            .Energy(1)
            .Draw(Card(IC.StrikeIronclad), Card(IC.StrikeIronclad))
            .Enemy(hp: 60);

        fight.Play();

        Assert.Empty(fight.State.Hand);
    }

    /// <summary>The replay is what the card is for: that copy plays three times.</summary>
    [Fact]
    public void TheReplayedCardPlaysThreeTimes()
    {
        var fight = Fight
            .Hand(Card(IC.StrikeIronclad) with { ReplayCount = 2 })
            .Energy(1)
            .Enemy(hp: 60);

        fight.Play();

        Assert.Equal(42, fight.Enemy0.Hp);
    }

    /// <summary>A Status or Curse in the pile is never the one chosen.</summary>
    [Fact]
    public void NeverChoosesAStatusOrCurse()
    {
        var fight = Fight
            .Hand(Card(CL.HiddenGem))
            .Energy(1)
            .Draw(Card(ST.Dazed), Card(IC.StrikeIronclad))
            .Enemy(hp: 60);

        fight.Play();

        var dazed = fight.State.DrawPile.First(card => card.DefId == ST.Dazed);
        Assert.Equal(0, dazed.ReplayCount);
        Assert.Equal(2, fight.State.DrawPile.Sum(card => card.ReplayCount));
    }
}
