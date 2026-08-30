using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The nine Regent cards the capture tool cannot reach. Every one of them raises a card
/// selection, ends the turn, or needs an ally the solo run does not have — so the play never
/// settles and no fixture is written. The Necrobinder pool ended the same way (E295): the
/// tool's silence is a category, not a coincidence.
/// </summary>
internal static class RegentSelection
{
    internal const int StrikeRegent = 474;
    internal const int DefendRegent = 133;
    internal const int MinionStrike = 310;
    internal const int MinionDiveBomb = 308;
    internal const int MinionSacrifice = 309;

    internal static Fight WithDrawPile(int count)
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Stars = 9;
        fight.State.DrawPile.Clear();
        for (int i = 0; i < count; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(StrikeRegent, false));
        }

        return fight;
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Begone.cs: a card CHOSEN from hand becomes a MINION
// STRIKE, upgraded if Begone was. The emulator transformed a random card into a random one.
public class BegoneTests
{
    private const int Begone = 37;

    [Fact]
    public void ItAsksWhichCardToReplace()
    {
        var fight = RegentSelection.WithDrawPile(3);
        fight.State.Hand.Add(new CardInstance(RegentSelection.DefendRegent, false));
        fight.State.Hand.Add(new CardInstance(Begone, false));

        fight.Play(1);

        Assert.Equal(CardSelectionKind.TransformHandToMinionStrike, fight.Pending!.Kind);
    }

    [Fact]
    public void TheChosenCardBecomesAMinionStrike()
    {
        var fight = RegentSelection.WithDrawPile(3);
        fight.State.Hand.Add(new CardInstance(RegentSelection.DefendRegent, false));
        fight.State.Hand.Add(new CardInstance(Begone, false));
        fight.Play(1);

        fight.Choose(0);

        Assert.Equal(RegentSelection.MinionStrike, fight.State.Hand[0].DefId);
        Assert.False(fight.State.Hand[0].Upgraded);
    }

    [Fact]
    public void AnUpgradedBegoneMakesAnUpgradedOne()
    {
        var fight = RegentSelection.WithDrawPile(3);
        fight.State.Hand.Add(new CardInstance(RegentSelection.DefendRegent, false));
        fight.State.Hand.Add(new CardInstance(Begone, true));
        fight.Play(1);

        fight.Choose(0);

        Assert.True(fight.State.Hand[0].Upgraded);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Charge.cs: CardsVar 2 cards CHOSEN from the DRAW pile
// become MINION DIVE BOMBS, in place.
public class ChargeTests
{
    private const int Charge = 83;

    [Fact]
    public void ItAsksTwiceAndTransformsBoth()
    {
        var fight = RegentSelection.WithDrawPile(4);
        fight.State.Hand.Add(new CardInstance(Charge, false));
        fight.Play(0);

        Assert.Equal(CardSelectionKind.TransformDrawToMinionDiveBomb, fight.Pending!.Kind);
        fight.Choose(0);
        Assert.NotNull(fight.Pending);
        fight.Choose(1);
        Assert.Null(fight.Pending);


        Assert.Equal(
            2,
            fight.State.DrawPile.Count(c => c.DefId == RegentSelection.MinionDiveBomb)
        );
    }

    /// <summary>In place — the pile order the rest of the turn draws from is unchanged.</summary>
    [Fact]
    public void TheyReplaceTheCardsWhereTheySat()
    {
        var fight = RegentSelection.WithDrawPile(4);
        fight.State.Hand.Add(new CardInstance(Charge, false));
        fight.Play(0);
        fight.Choose(2);
        fight.Choose(0);

        Assert.Equal(4, fight.State.DrawPile.Count);
        Assert.Equal(RegentSelection.MinionDiveBomb, fight.State.DrawPile[2].DefId);
        Assert.Equal(RegentSelection.MinionDiveBomb, fight.State.DrawPile[0].DefId);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Guards.cs: `CardSelectorPrefs(prompt, 0, 999999999)` — ANY
// NUMBER of hand cards become MINION SACRIFICES, and keeping none is a legal answer.
public class GuardsTests
{
    private const int Guards = 229;

    private static Fight WithHand(int count)
    {
        var fight = RegentSelection.WithDrawPile(3);
        for (int i = 0; i < count; i++)
        {
            fight.State.Hand.Add(new CardInstance(RegentSelection.DefendRegent, false));
        }

        fight.State.Hand.Add(new CardInstance(Guards, false));
        return fight;
    }

    [Fact]
    public void ItKeepsAskingUntilTheHandIsSpent()
    {
        var fight = WithHand(2);
        fight.Play(fight.State.Hand.Count - 1);

        Assert.Equal(CardSelectionKind.TransformHandToMinionSacrifice, fight.Pending!.Kind);
        // `Choose` takes a CANDIDATE index, not a hand index -- the reopened screen offers
        // only what is left, so the second answer is 0 again.
        fight.Choose(0);
        Assert.NotNull(fight.Pending);
        fight.Choose(0);
        Assert.Null(fight.Pending);

        Assert.Equal(
            2,
            fight.State.Hand.Count(c => c.DefId == RegentSelection.MinionSacrifice)
        );
    }

    /// <summary>A minimum of zero: the screen can be declined.</summary>
    [Fact]
    public void TheScreenIsSkippable()
    {
        var fight = WithHand(2);

        fight.Play(fight.State.Hand.Count - 1);

        Assert.True(fight.Pending!.Skippable);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Glimmer.cs: draw CardsVar 3 (upgrading by 1), then put ONE
// card CHOSEN from hand back on TOP of the draw pile. The emulator drew 1/2 and put nothing
// back.
public class GlimmerTests
{
    private const int Glimmer = 220;

    [Fact]
    public void ItDrawsThreeThenAsksForOneBack()
    {
        var fight = RegentSelection.WithDrawPile(6);
        fight.State.Hand.Add(new CardInstance(Glimmer, false));

        fight.Play(0);

        Assert.Equal(3, fight.State.Hand.Count);
        Assert.Equal(CardSelectionKind.HandToDrawPileTop, fight.Pending!.Kind);
    }

    [Fact]
    public void TheChosenCardGoesBackOnTop()
    {
        var fight = RegentSelection.WithDrawPile(6);
        fight.State.Hand.Add(new CardInstance(Glimmer, false));
        fight.Play(0);
        int drawBefore = fight.State.DrawPile.Count;

        fight.Choose(0);

        Assert.Equal(2, fight.State.Hand.Count);
        Assert.Equal(drawBefore + 1, fight.State.DrawPile.Count);
    }

    [Fact]
    public void TheUpgradeDrawsFour()
    {
        var fight = RegentSelection.WithDrawPile(6);
        fight.State.Hand.Add(new CardInstance(Glimmer, true));

        fight.Play(0);

        Assert.Equal(4, fight.State.Hand.Count);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/PhotonCut.cs: 10/13 damage, draw 1/2, then put ONE card
// CHOSEN from hand on top of the draw pile. The emulator moved the leftmost card, which is
// the whole decision the card offers.
public class PhotonCutTests
{
    private const int PhotonCut = 351;

    [Fact]
    public void ItHitsDrawsAndAsks()
    {
        var fight = RegentSelection.WithDrawPile(6);
        fight.State.Hand.Add(new CardInstance(PhotonCut, false));

        fight.Play(0, target: 0);

        Assert.Equal(490, fight.Enemy0.Hp);
        Assert.Single(fight.State.Hand);
        Assert.Equal(CardSelectionKind.HandToDrawPileTop, fight.Pending!.Kind);
    }

    [Fact]
    public void TheUpgradeHitsThirteenAndDrawsTwo()
    {
        var fight = RegentSelection.WithDrawPile(6);
        fight.State.Hand.Add(new CardInstance(PhotonCut, true));

        fight.Play(0, target: 0);

        Assert.Equal(487, fight.Enemy0.Hp);
        Assert.Equal(2, fight.State.Hand.Count);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/DecisionsDecisions.cs: six stars, Exhaust. Draw CardsVar 3
// (upgrading by 2), then AUTO-PLAY a playable SKILL chosen from hand three times — RepeatVar
// 3, which does not upgrade. The emulator drew and stopped.
public class DecisionsDecisionsTests
{
    private const int DecisionsDecisions = 129;

    [Fact]
    public void ItDrawsThreeAndAsksForASkill()
    {
        var fight = RegentSelection.WithDrawPile(8);
        fight.State.Hand.Add(new CardInstance(RegentSelection.DefendRegent, false));
        fight.State.Hand.Add(new CardInstance(DecisionsDecisions, false));

        fight.Play(1);

        // Nine staged, six spent.
        Assert.Equal(3, fight.State.Stars);
        // Three drawn on top of the Defend that was already there.
        Assert.Equal(4, fight.State.Hand.Count);
        Assert.Equal(CardSelectionKind.AutoPlaySkillThrice, fight.Pending!.Kind);
    }

    /// <summary>A hand with no playable Skill asks nothing at all.</summary>
    [Fact]
    public void WithNoSkillInHandItAsksNothing()
    {
        var fight = RegentSelection.WithDrawPile(8);
        fight.State.Hand.Add(new CardInstance(DecisionsDecisions, false));

        fight.Play(0);

        Assert.Null(fight.Pending);
    }

    /// <summary>Only playable SKILLS are offered — the drawn Strikes are not.</summary>
    [Fact]
    public void OnlySkillsAreOffered()
    {
        var fight = RegentSelection.WithDrawPile(8);
        fight.State.Hand.Add(new CardInstance(RegentSelection.DefendRegent, false));
        fight.State.Hand.Add(new CardInstance(DecisionsDecisions, false));

        fight.Play(1);

        // Eight Strikes in the pile, three drawn, plus the one Defend: one candidate.
        Assert.Single(fight.Pending!.Candidates);
    }

    [Fact]
    public void TheChosenSkillPlaysThreeTimes()
    {
        var fight = RegentSelection.WithDrawPile(8);
        fight.State.Hand.Add(new CardInstance(RegentSelection.DefendRegent, false));
        fight.State.Hand.Add(new CardInstance(DecisionsDecisions, false));
        fight.Play(1);

        fight.Choose(fight.Pending!.Candidates[0]);

        // A Regent Defend blocks 5; three plays is fifteen.
        Assert.Equal(15, fight.State.PlayerBlock);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/HeavenlyDrill.cs: `HasEnergyCostX` — 8/10 damage once per
// energy spent, and the whole hit count DOUBLED when that is four or more. The emulator
// dealt one hit and traded stars for energy, which is not on the card at all.
public class HeavenlyDrillTests
{
    private const int HeavenlyDrill = 241;

    private static Fight WithEnergy(int energy)
    {
        var fight = Fight.Hand().Energy(energy).Enemy(hp: 500);
        fight.State.Stars = 9;
        return fight;
    }

    [Fact]
    public void BelowFourItHitsOncePerEnergy()
    {
        var fight = WithEnergy(3);
        fight.State.Hand.Add(new CardInstance(HeavenlyDrill, false));

        fight.Play(0, target: 0);

        Assert.Equal(500 - 24, fight.Enemy0.Hp);
        // Its X is ENERGY, not stars -- the counter is untouched.
        Assert.Equal(9, fight.State.Stars);
    }

    /// <summary>At four the whole COUNT doubles — eight hits, not four at double damage.</summary>
    [Fact]
    public void AtFourTheCountDoubles()
    {
        var fight = WithEnergy(4);
        fight.State.Hand.Add(new CardInstance(HeavenlyDrill, false));

        fight.Play(0, target: 0);

        Assert.Equal(500 - 64, fight.Enemy0.Hp);
    }

    [Fact]
    public void TheUpgradeHitsForTen()
    {
        var fight = WithEnergy(2);
        fight.State.Hand.Add(new CardInstance(HeavenlyDrill, true));

        fight.Play(0, target: 0);

        Assert.Equal(500 - 20, fight.Enemy0.Hp);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Quasar.cs: two stars for three DISTINCT colourless cards
// offered, one of which joins the hand — and the screen is SKIPPABLE. The emulator took a
// random class card.
public class QuasarTests
{
    private const int Quasar = 376;

    [Fact]
    public void ItOffersThreeColourlessCards()
    {
        var fight = RegentSelection.WithDrawPile(3);
        fight.State.Stars = 2;
        fight.State.Hand.Add(new CardInstance(Quasar, false));

        fight.Play(0);

        Assert.Equal(CardSelectionKind.GeneratedCardToHand, fight.Pending!.Kind);
        Assert.Equal(3, fight.Pending.GeneratedCandidates.Count);
        Assert.All(
            fight.Pending.GeneratedCandidates,
            id => Assert.True(GeneratedData.CardPools.Colorless.Contains(id))
        );
        Assert.True(fight.Pending.Skippable);
        Assert.Equal(0, fight.State.Stars);
    }

    [Fact]
    public void TheChosenCardJoinsTheHand()
    {
        var fight = RegentSelection.WithDrawPile(3);
        fight.State.Stars = 2;
        fight.State.Hand.Add(new CardInstance(Quasar, false));
        fight.Play(0);
        int offered = fight.Pending!.GeneratedCandidates[1];

        fight.Choose(1);

        Assert.Single(fight.State.Hand);
        Assert.Equal(offered, fight.State.Hand[0].DefId);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Largesse.cs: MultiplayerOnly and `TargetType.AnyAlly`, so
// a solo run can never play it — `CanPlay` refuses on NoLivingAllies, which is what the live
// capture reported when it tried. Its body gives one distinct colourless card to the ALLY.
public class LargesseTests
{
    private const int Largesse = 280;

    /// <summary>
    /// The fact worth pinning is that it is unreachable solo. The emulator has no ally-target
    /// gate, so this asserts what the data says rather than what a play would do.
    /// </summary>
    [Fact]
    public void ItIsMultiplayerOnlyAndTargetsAnAlly()
    {
        var def = GeneratedData.Cards.Get(Largesse);

        Assert.True(def.MultiplayerOnly);
        Assert.Equal(CardTarget.AnyAlly, def.Target);
    }
}
