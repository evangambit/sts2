using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 2-cost Attack, CardMultiplayerConstraint.MultiplayerOnly.
// MegaCrit.Sts2.Core.Models.Cards/TagTeam.cs: DamageVar(11m) then TagTeamPower(1) on the
// target; OnUpgrade raises the damage by 4.
//
// TagTeamPower needs an ally to mean anything and is not modelled, so the damage is what
// these pin — it comes off the generic damage path rather than a case of its own.
public class TagTeamTests
{
    [Fact]
    public void DealsEleven()
    {
        var fight = Fight.Hand(Card(CL.TagTeam)).Energy(2).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(49, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradedDealsFifteen()
    {
        var fight = Fight.Hand(Card(CL.TagTeam, upgraded: true)).Energy(2).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(45, fight.Enemy0.Hp);
    }

    [Fact]
    public void AppliesNoDebuffInSingleplayer()
    {
        var fight = Fight.Hand(Card(CL.TagTeam)).Energy(2).Enemy(hp: 60);

        fight.Play();

        Assert.Empty(fight.Enemy0.Buffs);
    }
}
