using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Attack, CardTag.Strike. MegaCrit.Sts2.Core.Models.Cards/SeekerStrike.cs:
// DamageVar(9m), then CardsVar(3) draw-pile cards are offered and the chosen one goes to
// hand; OnUpgrade raises the damage by 3.
//
// The choice is real, and it is over a SAMPLE: the game shuffles the draw pile and offers
// the first CardsVar(3), whatever their type. Offering every Attack instead was both the
// wrong set and the wrong size -- a pile of four attacks offered four, and a pile of
// three skills offered nothing.
public class SeekerStrikeTests
{
    [Fact]
    public void DealsNine()
    {
        var fight = Fight.Hand(Card(CL.SeekerStrike)).Energy(1).Draw(Card(IC.Bash)).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(51, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradedDealsTwelve()
    {
        var fight = Fight
            .Hand(Card(CL.SeekerStrike, upgraded: true))
            .Energy(1)
            .Draw(Card(IC.Bash))
            .Enemy(hp: 60);

        fight.Play();

        Assert.Equal(48, fight.Enemy0.Hp);
    }

    [Fact]
    public void OffersThreeDrawPileCardsAndTakesTheChosenOne()
    {
        var fight = Fight
            .Hand(Card(CL.SeekerStrike))
            .Energy(1)
            .Draw(
                Card(IC.DefendIronclad),
                Card(IC.Bash),
                Card(IC.StrikeIronclad),
                Card(IC.StrikeIronclad)
            )
            .Enemy(hp: 60);
        fight.Play();

        Assert.Equal(3, fight.Pending?.Candidates.Count);
        fight.Choose(1);

        Assert.Equal([IC.Bash], Fight.Ids(fight.State.Hand));
        Assert.DoesNotContain(IC.Bash, Fight.Ids(fight.State.DrawPile));
    }

    /// <summary>
    /// Type does not narrow the offer: a draw pile of Skills still puts three in front of
    /// the player.
    /// </summary>
    [Fact]
    public void OffersSkillsToo()
    {
        var fight = Fight
            .Hand(Card(CL.SeekerStrike))
            .Energy(1)
            .Draw(Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.DefendIronclad))
            .Enemy(hp: 60);

        fight.Play();

        Assert.Equal(3, fight.Pending?.Candidates.Count);
    }

    [Fact]
    public void OffersWhatItCanFromAShortPile()
    {
        var fight = Fight
            .Hand(Card(CL.SeekerStrike))
            .Energy(1)
            .Draw(Card(IC.DefendIronclad))
            .Enemy(hp: 60);

        fight.Play();

        Assert.Equal(1, fight.Pending?.Candidates.Count);
    }

    [Fact]
    public void AsksNothingWithAnEmptyDrawPile()
    {
        var fight = Fight.Hand(Card(CL.SeekerStrike)).Energy(1).Draw().Enemy(hp: 60);

        fight.Play();

        Assert.Null(fight.Pending);
    }

    /// <summary>
    /// The three offered are `drawPile.StableShuffle(Rng.CombatCardSelection).Take(3)` — a
    /// random sample, not the top three. It used to take the first three indices, which is
    /// neither random nor a draw from the stream.
    /// </summary>
    [Fact]
    public void TheThreeOfferedVaryWithTheSelectionStream()
    {
        var seen = new HashSet<string>();
        for (int seed = 0; seed < 24; seed++)
        {
            var fight = Fight.Hand(Card(CL.SeekerStrike)).Energy(3).Enemy(hp: 200);
            fight.State.CardSelectionRng = new CountingRandom(seed);
            fight.State.DrawPile =
            [
                Card(IC.StrikeIronclad),
                Card(IC.Bludgeon),
                Card(IC.Cinder),
                Card(IC.Anger),
                Card(IC.Barricade),
                Card(IC.Corruption),
            ];

            fight.Play();

            seen.Add(string.Join(",", fight.State.PendingSelection!.Candidates));
        }

        Assert.True(seen.Count > 1, $"the sample never varied: {string.Join(" | ", seen)}");
    }

    [Fact]
    public void ItRollsOnTheCardSelectionStream()
    {
        var fight = Fight.Hand(Card(CL.SeekerStrike)).Energy(3).Enemy(hp: 200);
        var stream = new CountingRandom(5);
        fight.State.CardSelectionRng = stream;
        fight.State.DrawPile =
        [
            Card(IC.StrikeIronclad),
            Card(IC.Bludgeon),
            Card(IC.Cinder),
            Card(IC.Anger),
        ];

        fight.Play();

        Assert.True(stream.CallCount > 0, "the selection stream should have been drawn from");
    }

    /// <summary>Three offered when the pile has more, and all of them when it has fewer.</summary>
    [Fact]
    public void ItOffersAtMostThree()
    {
        var many = Fight.Hand(Card(CL.SeekerStrike)).Energy(3).Enemy(hp: 200);
        many.State.DrawPile =
        [
            Card(IC.StrikeIronclad),
            Card(IC.Bludgeon),
            Card(IC.Cinder),
            Card(IC.Anger),
            Card(IC.Barricade),
        ];
        many.Play();
        Assert.Equal(3, many.State.PendingSelection!.Candidates.Count);

        var few = Fight.Hand(Card(CL.SeekerStrike)).Energy(3).Enemy(hp: 200);
        few.State.DrawPile = [Card(IC.StrikeIronclad), Card(IC.Bludgeon)];
        few.Play();
        Assert.Equal(2, few.State.PendingSelection!.Candidates.Count);
    }
}
