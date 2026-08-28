using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// <c>CardModel.IsUpgradable</c> is <c>CurrentUpgradeLevel &lt; MaxUpgradeLevel</c>, and the
/// 37 cards that override <c>MaxUpgradeLevel</c> to zero are the curses and statuses.
/// </summary>
/// <remarks>
/// This used to be a list of fourteen ids kept by hand, and the twenty-three it missed
/// were quietly eligible for every upgrade in the game. That is invisible while an upgrade
/// is CHOSEN and loud the moment one is random: Doors of Light and Dark shuffles the
/// upgradable cards and takes two, so one extra name in the candidate list is a different
/// shuffle and a different pick.
/// </remarks>
public class UpgradableCardTests
{
    private static bool Upgradable(int cardId) =>
        RunConstants.IsRunCardUpgradable(new CardInstance(cardId, Upgraded: false));

    [Fact]
    public void NoCurseOrStatusIsUpgradable()
    {
        foreach (var card in GeneratedData.Cards.All.ToArray())
        {
            if (card.Type is CardType.Curse or CardType.Status)
            {
                Assert.False(Upgradable(card.Id), $"{card.Name} should not be upgradable");
            }
        }
    }

    /// <summary>
    /// Greed is the one the live capture caught: a Cursed Pearl run carries it from floor
    /// one, and it sat in Light Door's candidate list for the whole run.
    /// </summary>
    [Fact]
    public void GreedIsNotUpgradable()
    {
        Assert.False(Upgradable(10029));
    }

    [Fact]
    public void OrdinaryCardsStillAre()
    {
        Assert.True(Upgradable(472)); // Strike
        Assert.True(Upgradable(131)); // Defend
        Assert.True(Upgradable(30)); // Bash
    }

    [Fact]
    public void AnAlreadyUpgradedCardIsNot()
    {
        Assert.False(RunConstants.IsRunCardUpgradable(new CardInstance(472, Upgraded: true)));
    }

    /// <summary>
    /// The `NXV45HW43K` capture's own Light Door: a deck of Bash, Breakthrough, four
    /// Defends, Perfected Strike, Setup Strike, five Strikes, plus Ascender's Bane and
    /// Greed. The game upgraded two Strikes; counting Greed among the thirteen candidates
    /// made it a Strike and a Defend.
    /// </summary>
    [Fact]
    public void LightDoorUpgradesTheTwoCardsTheCaptureUpgraded()
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        engine.State.Deck.Clear();
        foreach (
            int id in new[]
            {
                10001,
                30,
                60,
                131,
                131,
                131,
                131,
                10029,
                349,
                421,
                472,
                472,
                472,
                472,
                472,
            }
        )
        {
            engine.State.Deck.Add(new CardInstance(id, false));
        }

        RunNonCombatEffects.UpgradeTwoRandomCardsForLightDoor(engine.State);

        Assert.Equal(
            ["STRIKE_IRONCLAD", "STRIKE_IRONCLAD"],
            engine
                .State.Deck.Where(card => card.Upgraded)
                .Select(card => GeneratedData.Cards.Get(card.DefId).Entry)
        );
    }
}
