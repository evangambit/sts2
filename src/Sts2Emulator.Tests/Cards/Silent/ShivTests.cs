using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

/// <summary>
/// The token every Silent deck is really built on: 0-cost Attack, `DamageVar(4m)` with +2
/// on upgrade, Exhaust, and `CardTag.Shiv` — which is what Accuracy, Phantom Blades and
/// Fan of Knives all key off.
/// </summary>
/// <remarks>
/// Its damage is dealt from a by-NAME arm, and there was a second Shiv branch in the
/// shared base-damage path that had drifted from it (E144). One `ShivDamage` helper now
/// answers what a Shiv hits for, and this suite is what pins the answer.
/// </remarks>
public class ShivTests
{
    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, 6)]
    public void ItHitsForItsPrintedDamageAndExhausts(bool upgraded, int damage)
    {
        var fight = Fight.Hand(Card(SI.Shiv, upgraded)).Energy(3).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(60 - damage, fight.Enemy0.Hp);
        Assert.Contains(fight.State.ExhaustPile, c => c.DefId == SI.Shiv);
    }

    /// <summary>Accuracy's bonus rides on every Shiv, upgraded or not.</summary>
    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, 6)]
    public void AccuracyRaisesIt(bool accuracyUpgraded, int bonus)
    {
        var fight = Fight
            .Hand(Card(SI.Accuracy, accuracyUpgraded), Card(SI.Shiv))
            .Energy(9)
            .Enemy(hp: 60);

        fight.Play();
        fight.Play();

        Assert.Equal(60 - (4 + bonus), fight.Enemy0.Hp);
    }
}
