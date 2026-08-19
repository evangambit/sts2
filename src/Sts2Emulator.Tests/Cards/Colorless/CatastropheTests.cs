using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 2-cost Skill. MegaCrit.Sts2.Core.Models.Cards/Catastrophe.cs auto-plays CardsVar(2)
// cards taken from the draw pile; OnUpgrade raises that by 1.
//
// The emulator approximates it as "draw that many, then discard that many" — the cards
// leave the draw pile and end up in the discard pile as they would after being played,
// but nothing they do happens. These tests pin the approximation, which is a long way
// from the card.
public class CatastropheTests
{
    [Fact]
    public void MovesTwoCardsFromDrawToDiscardWithoutPlayingThem()
    {
        var fight = Fight
            .Hand(Card(CL.Catastrophe))
            .Energy(2)
            .Draw(Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.Bash))
            .Enemy(hp: 60);

        fight.Play();

        // Both Strikes would have dealt 6 each had they actually been played.
        Assert.Equal(60, fight.Enemy0.Hp);
        Assert.Equal([IC.Bash], Fight.Ids(fight.State.DrawPile));
        Assert.Empty(fight.State.Hand);
    }

    [Fact]
    public void UpgradedMovesThree()
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

        Assert.Equal([IC.Bash], Fight.Ids(fight.State.DrawPile));
    }
}
