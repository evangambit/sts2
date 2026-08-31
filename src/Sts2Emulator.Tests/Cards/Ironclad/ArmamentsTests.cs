using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class ArmamentsTests
{
    /// <summary>
    /// The block lands immediately; the upgrade waits on the player. This used to assert
    /// that the FIRST card in hand was upgraded, which is what the emulator did and not
    /// what `CardSelectCmd.FromHandForUpgrade` does.
    /// </summary>
    [Fact]
    public void GainsBlockAndThenAsksWhichCardToUpgrade()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.Armaments, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
        ];
        state.Energy = 1;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(5, state.PlayerBlock);
        Assert.NotNull(state.PendingSelection);
        Assert.Equal(CardSelectionKind.UpgradeInHand, state.PendingSelection!.Kind);
        Assert.All(state.Hand, card => Assert.False(card.Upgraded));
    }

    [Fact]
    public void UpgradedUpgradesAllCardsInHand()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.Armaments, true),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
            new CardInstance(ST.Slimed, false),
        ];
        state.Energy = 1;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(5, state.PlayerBlock);
        Assert.Contains(state.Hand, card => card.DefId == IC.StrikeIronclad && card.Upgraded);
        Assert.Contains(state.Hand, card => card.DefId == IC.DefendIronclad && card.Upgraded);
        Assert.Contains(state.Hand, card => card.DefId == ST.Slimed && !card.Upgraded);
    }

    /// <summary>
    /// `CardSelectCmd.FromHandForUpgrade` ASKS. Upgrading the leftmost upgradable card is
    /// not a simplification — which card gets the upgrade is the entire decision.
    /// </summary>
    [Fact]
    public void UnupgradedItAsksWhichCardToUpgrade()
    {
        var fight = Fight
            .Hand(
                new CardInstance(IC.Armaments, false),
                new CardInstance(IC.StrikeIronclad, false),
                new CardInstance(IC.Bludgeon, false)
            )
            .Energy(3);

        fight.Play(0);

        Assert.NotNull(fight.Pending);
        Assert.Equal(CardSelectionKind.UpgradeInHand, fight.Pending!.Kind);
        Assert.Equal(2, fight.Pending.Candidates.Count);
    }

    [Fact]
    public void TheChosenCardIsTheOneUpgraded()
    {
        var fight = Fight
            .Hand(
                new CardInstance(IC.Armaments, false),
                new CardInstance(IC.StrikeIronclad, false),
                new CardInstance(IC.Bludgeon, false)
            )
            .Energy(3);
        fight.Play(0);

        // Take the SECOND candidate, which the leftmost-card reading could never pick.
        int pick = fight.Pending!.Candidates[1];
        int chosenDefId = fight.State.Hand[pick].DefId;
        fight.Choose(1);

        Assert.Null(fight.Pending);
        Assert.True(fight.State.Hand.Single(c => c.DefId == chosenDefId).Upgraded);
        Assert.False(fight.State.Hand.Single(c => c.DefId != chosenDefId).Upgraded);
    }

    /// <summary>
    /// The candidate list is filtered to UPGRADABLE cards, so an already-upgraded hand
    /// offers nothing and the game's `list.Count &lt;= 1` auto-pick needs no screen.
    /// </summary>
    [Fact]
    public void AnAlreadyUpgradedHandRaisesNoScreen()
    {
        var fight = Fight
            .Hand(new CardInstance(IC.Armaments, false), new CardInstance(IC.StrikeIronclad, true))
            .Energy(3);

        fight.Play(0);

        Assert.Null(fight.Pending);
    }

    /// <summary>Upgraded, it takes the whole hand and asks nothing.</summary>
    [Fact]
    public void UpgradedItTakesEveryUpgradableCardWithNoChoice()
    {
        var fight = Fight
            .Hand(
                new CardInstance(IC.Armaments, true),
                new CardInstance(IC.StrikeIronclad, false),
                new CardInstance(IC.Bludgeon, false)
            )
            .Energy(3);

        fight.Play(0);

        Assert.Null(fight.Pending);
        Assert.All(fight.State.Hand, c => Assert.True(c.Upgraded));
    }
}
