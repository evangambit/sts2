using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Skill. MegaCrit.Sts2.Core.Models.Cards/Splash.cs: three Attacks drawn from every
// character pool EXCEPT the player's own, offered on a choose-a-card screen with
// canSkip, upgraded if the card is, and free for the turn once taken.
//
// The emulator used to add a plain Ironclad Strike and say so in a comment. That is the
// player's OWN character, it is not a choice, and it is not free — three of the four
// things the card does.
public class SplashTests
{
    [Fact]
    public void OffersThreeAttacksFromOtherCharacters()
    {
        var fight = Fight.Hand(Card(IC.Splash)).Energy(1);

        fight.Play();

        Assert.NotNull(fight.State.PendingSelection);
        var offered = fight.State.PendingSelection!.GeneratedCandidates;
        Assert.Equal(3, offered.Count);
        Assert.Equal(3, offered.Distinct().Count());

        var ironclad = GeneratedData.CardPools.Ironclad.ToArray().ToHashSet();
        Assert.All(
            offered,
            id =>
            {
                Assert.Equal(CardType.Attack, GeneratedData.Cards.Get(id).Type);
                Assert.DoesNotContain(id, ironclad);
            }
        );
    }

    [Fact]
    public void TheChosenCardJoinsTheHandFreeForTheTurn()
    {
        var fight = Fight.Hand(Card(IC.Splash)).Energy(1);
        fight.Play();
        int picked = fight.State.PendingSelection!.GeneratedCandidates[1];

        fight.Choose(1);

        Assert.Null(fight.State.PendingSelection);
        var added = Assert.Single(fight.State.Hand, c => c.DefId == picked);
        Assert.True(added.FreeThisTurn);
    }

    /// <summary>`canSkip: true` on the screen, so taking none is a legal answer.</summary>
    [Fact]
    public void TheScreenCanBeSkipped()
    {
        var fight = Fight.Hand(Card(IC.Splash)).Energy(1);
        fight.Play();

        Assert.True(fight.State.PendingSelection!.Skippable);
        int skip = fight.State.PendingSelection.Candidates.Count;
        CombatEngine.Step(fight.State, skip, new Random(0));

        Assert.Null(fight.State.PendingSelection);
        Assert.Empty(fight.State.Hand);
    }

    [Fact]
    public void UpgradedTheOfferedCardIsUpgraded()
    {
        var fight = Fight.Hand(Card(IC.Splash, upgraded: true)).Energy(1);
        fight.Play();

        fight.Choose(0);

        var added = Assert.Single(fight.State.Hand);
        Assert.True(added.Upgraded);
    }

    [Fact]
    public void WhichThreeAreOfferedVariesWithTheGenerationStream()
    {
        var seen = new HashSet<string>();
        for (int seed = 0; seed < 24; seed++)
        {
            var fight = Fight.Hand(Card(IC.Splash)).Energy(1);
            fight.State.CardGenerationRng = new CountingRandom(seed);
            fight.Play();
            seen.Add(string.Join(",", fight.State.PendingSelection!.GeneratedCandidates));
        }

        Assert.True(seen.Count > 1, "the three offered never varied");
    }
}
