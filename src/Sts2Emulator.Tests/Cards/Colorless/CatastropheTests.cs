using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 2-cost Skill. MegaCrit.Sts2.Core.Models.Cards/Catastrophe.cs auto-plays CardsVar(2)
// cards taken from the draw pile; OnUpgrade raises that by 1.
//
// This used to be approximated as "draw that many, then discard that many" -- the cards
// ended up where a played card ends up, so nothing looked wrong, and nothing they do
// happened. These tests asserted that, which is how a stand-in survives having a test.
public class CatastropheTests
{
    [Fact]
    public void PlaysTwoCardsOffTheDrawPile()
    {
        var fight = Fight
            .Hand(Card(CL.Catastrophe))
            .Energy(2)
            .Draw(Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.Bash))
            .Enemy(hp: 60);

        fight.Play();

        // Two Strikes at 6 each, actually played.
        Assert.Equal(48, fight.Enemy0.Hp);
        Assert.Equal([IC.Bash], Fight.Ids(fight.State.DrawPile));
        Assert.Empty(fight.State.Hand);
    }

    [Fact]
    public void UpgradedPlaysThree()
    {
        var fight = Fight
            .Hand(Card(CL.Catastrophe, upgraded: true))
            .Energy(2)
            .Draw(
                Card(IC.StrikeIronclad),
                Card(IC.StrikeIronclad),
                Card(IC.StrikeIronclad),
                Card(IC.Bash)
            )
            .Enemy(hp: 60);

        fight.Play();

        Assert.Equal(42, fight.Enemy0.Hp);
        Assert.Equal([IC.Bash], Fight.Ids(fight.State.DrawPile));
    }

    /// <summary>
    /// The card prefers a playable card and only reaches for an Unplayable one when every
    /// card left is Unplayable -- which is the fallback the card itself carries.
    /// </summary>
    [Fact]
    public void PrefersAPlayableCard()
    {
        var fight = Fight
            .Hand(Card(CL.Catastrophe))
            .Energy(2)
            .Draw(Card(ST.Dazed), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad))
            .Enemy(hp: 60);

        fight.Play();

        Assert.Equal(48, fight.Enemy0.Hp);
        Assert.Equal([ST.Dazed], Fight.Ids(fight.State.DrawPile));
    }

    [Fact]
    public void DoesNothingWithAnEmptyDrawPile()
    {
        // Draw() with nothing empties the pile; the harness seeds a starter deck otherwise.
        var fight = Fight.Hand(Card(CL.Catastrophe)).Energy(2).Draw().Enemy(hp: 60);

        fight.Play();

        Assert.Equal(60, fight.Enemy0.Hp);
    }
}
