using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 0-cost Skill, CardKeyword.Exhaust. MegaCrit.Sts2.Core.Models.Cards/ThinkingAhead.cs
// draws CardsVar(2) and then puts a CHOSEN card from hand back on top of the draw pile;
// OnUpgrade removes the Exhaust rather than changing the draw.
//
// The emulator puts the first card in hand back instead of asking.
public class ThinkingAheadTests
{
    [Fact]
    public void DrawsTwoAndPutsACardBackOnTop()
    {
        var fight = Fight
            .Hand(Card(CL.ThinkingAhead))
            .Energy(1)
            .Draw(Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.Anger))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Single(fight.State.Hand);
        Assert.Equal(2, fight.State.DrawPile.Count);
    }

    [Fact]
    public void UpgradedDoesNotExhaust()
    {
        var fight = Fight
            .Hand(Card(CL.ThinkingAhead, upgraded: true))
            .Energy(1)
            .Draw(Card(IC.Bash), Card(IC.StrikeIronclad))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Empty(fight.State.ExhaustPile);
        Assert.Contains(fight.State.DiscardPile, card => card.DefId == CL.ThinkingAhead);
    }

    [Fact]
    public void UnupgradedExhaustsItself()
    {
        var fight = Fight
            .Hand(Card(CL.ThinkingAhead))
            .Energy(1)
            .Draw(Card(IC.Bash), Card(IC.StrikeIronclad))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Contains(fight.State.ExhaustPile, card => card.DefId == CL.ThinkingAhead);
    }
}
