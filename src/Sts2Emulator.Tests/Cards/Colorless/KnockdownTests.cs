using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 3-cost Attack, CardMultiplayerConstraint.MultiplayerOnly.
// MegaCrit.Sts2.Core.Models.Cards/Knockdown.cs: DamageVar(10m) then
// PowerVar<KnockdownPower>(2m) on the target; OnUpgrade raises the damage by 4 and the
// power by 1.
//
// KnockdownPower itself is a multiplayer mechanic; the emulator stands Stunned in for it
// at the same amount (2, and 3 upgraded), so these pin the stand-in as well as the damage.
public class KnockdownTests
{
    [Fact]
    public void DealsTen()
    {
        var fight = Fight.Hand(Card(CL.Knockdown)).Energy(3).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(50, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradedDealsFourteen()
    {
        var fight = Fight.Hand(Card(CL.Knockdown, upgraded: true)).Energy(3).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(46, fight.Enemy0.Hp);
    }

    [Fact]
    public void AppliesTwoOfTheStandInDebuff()
    {
        var fight = Fight.Hand(Card(CL.Knockdown)).Energy(3).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Stunned));
    }

    [Fact]
    public void UpgradedAppliesThree()
    {
        var fight = Fight.Hand(Card(CL.Knockdown, upgraded: true)).Energy(3).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(3, fight.EnemyBuffAmount(BuffId.Stunned));
    }
}
