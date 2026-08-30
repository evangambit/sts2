using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Skill, CardMultiplayerConstraint.MultiplayerOnly, TargetType.AnyAlly.
// MegaCrit.Sts2.Core.Models.Cards/Intercept.cs: BlockVar(9m) to yourself, then
// CoveredPower(1) on the targeted ally; OnUpgrade raises the block by 4.
//
// CoveredPower marks the targeted ally and gives the APPLIER an InterceptPower that soaks
// the hits aimed at them. Alone, the target is the player, so covering yourself redirects
// your own damage to yourself: nothing happens, and the card is block and no more.
//
// Intangible used to stand in for it. The header here called that "a generous stand-in"
// and pinned it so the substitution would be visible — which is exactly right as far as
// it goes, and still left a 1-cost common granting the strongest defensive effect in the
// game. A stand-in being DOCUMENTED does not make it proportionate.
public class InterceptTests
{
    [Fact]
    public void GainsNineBlock()
    {
        var fight = Fight.Hand(Card(CL.Intercept)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(9, fight.State.PlayerBlock);
    }

    [Fact]
    public void UpgradedGainsThirteen()
    {
        var fight = Fight.Hand(Card(CL.Intercept, upgraded: true)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(13, fight.State.PlayerBlock);
    }

    /// <summary>
    /// No Intangible, and nothing else either: CoveredPower has nobody to cover.
    /// </summary>
    [Fact]
    public void GrantsNoIntangible()
    {
        var fight = Fight.Hand(Card(CL.Intercept)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.Intangible));
    }

    /// <summary>
    /// The point of the above, made concrete: with Intangible the enemy's attack landed
    /// for 1 whatever it swung, which is the difference between a common and a rare.
    /// </summary>
    [Fact]
    public void IncomingDamageIsNotCappedAtOne()
    {
        var fight = Fight.Hand(Card(CL.Intercept)).Energy(1).Enemy(hp: 40);
        fight.Play();
        fight.State.PlayerBlock = 0;
        int before = fight.State.PlayerHp;

        CardEffects.DealDamageToPlayer(fight.State, 12);

        Assert.Equal(before - 12, fight.State.PlayerHp);
    }
}
