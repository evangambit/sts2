using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 0-cost Skill, MultiplayerOnly, TargetType.AnyAlly.
// MegaCrit.Sts2.Core.Models.Cards/BelieveInYou.cs gives EnergyVar(2) to the targeted
// ally; OnUpgrade raises it by 1. In singleplayer the only ally is you.
public class BelieveInYouTests
{
    [Fact]
    public void GivesTwoEnergy()
    {
        var fight = Fight.Hand(Card(CL.BelieveInYou)).Energy(0).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(2, fight.State.Energy);
    }

    [Fact]
    public void UpgradedGivesThree()
    {
        var fight = Fight.Hand(Card(CL.BelieveInYou, upgraded: true)).Energy(0).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(3, fight.State.Energy);
    }
}
