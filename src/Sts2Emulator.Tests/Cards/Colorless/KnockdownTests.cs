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
// KnockdownPower is a damage MULTIPLIER on the target, and ModifyDamageMultiplicative
// returns 1 when `dealer == base.Applier` — so it only ever amplifies another player's
// attacks and does nothing at all alone. The damage is the whole card in singleplayer.
//
// Stunned used to stand in for it at 2 and 3. That is a real debuff costing an enemy
// turns, handed out by a card that should do nothing beyond its damage — the same shape
// as Intercept's Intangible, and documented in the same honest way that made it look
// settled.
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

    /// <summary>No stun, and no other debuff: the damage is the entire card alone.</summary>
    [Fact]
    public void AppliesNoDebuff()
    {
        var fight = Fight.Hand(Card(CL.Knockdown)).Energy(3).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Stunned));
        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Vulnerable));
        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Weak));
    }

    /// <summary>
    /// Whistle shares this case body and DOES stun — it calls `CreatureCmd.Stun` outright,
    /// which is why that branch stays while Knockdown's went.
    /// </summary>
    [Fact]
    public void WhistleStillStuns()
    {
        const int whistle = 539;
        var fight = Fight.Hand(new CardInstance(whistle, false)).Energy(3).Enemy(hp: 90);

        fight.Play();

        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Stunned));
    }
}
