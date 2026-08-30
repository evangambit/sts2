using Sts2Emulator.Core;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/Hang.cs: DamageVar(10) upgrading by 3, then
// `PowerCmd.Apply<HangPower>(target, Math.Max(2, existing))` — AFTER the damage.
// HangPower.ModifyDamageMultiplicative multiplies damage aimed at its owner by its own
// Amount, and only when `cardSource is Hang`.
//
// So the counter doubles — 2, 4, 8 — and each Hang lands at the multiple the PREVIOUS one
// left behind. The emulator applied Constrict 2 and never scaled anything.
public class HangTests
{
    private const int Hang = 236;
    private const int Strike = 473;

    private static Fight Fresh() => Fight.Hand().Energy(9).Enemy(hp: 500);

    [Fact]
    public void TheFirstOneDealsItsPrintedDamageAndAppliesTwo()
    {
        var fight = Fresh();
        fight.State.Hand.Add(new CardInstance(Hang, false));

        fight.Play(0, target: 0);

        Assert.Equal(490, fight.Enemy0.Hp);
        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Hang));
        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Constrict));
    }

    [Fact]
    public void TheUpgradeRaisesTheDamageNotTheStack()
    {
        var fight = Fresh();
        fight.State.Hand.Add(new CardInstance(Hang, true));

        fight.Play(0, target: 0);

        Assert.Equal(487, fight.Enemy0.Hp);
        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Hang));
    }

    /// <summary>Each one lands at the multiple the last left behind, and doubles it.</summary>
    [Fact]
    public void ItDoublesOnEveryPlay()
    {
        var fight = Fresh();
        fight.State.Hand.Add(new CardInstance(Hang, false));
        fight.State.Hand.Add(new CardInstance(Hang, false));
        fight.State.Hand.Add(new CardInstance(Hang, false));

        fight.Play(0, target: 0);
        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Hang));

        fight.Play(0, target: 0); // 10 x 2
        Assert.Equal(470, fight.Enemy0.Hp);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Hang));

        fight.Play(0, target: 0); // 10 x 4
        Assert.Equal(430, fight.Enemy0.Hp);
        Assert.Equal(8, fight.EnemyBuffAmount(BuffId.Hang));
    }

    /// <summary>
    /// `cardSource is Hang`: nothing else is multiplied by the stacks. Measured against a
    /// board with no Hang on it rather than against a number, so the Strike's own damage
    /// is not restated here.
    /// </summary>
    [Fact]
    public void ItDoesNotScaleOtherAttacks()
    {
        var control = Fresh();
        control.State.Hand.Add(new CardInstance(Strike, false));
        control.Play(0, target: 0);
        int plainStrike = 500 - control.Enemy0.Hp;

        var fight = Fresh();
        fight.State.Hand.Add(new CardInstance(Hang, false));
        fight.Play(0, target: 0);
        int afterHang = fight.Enemy0.Hp;
        fight.State.Hand.Add(new CardInstance(Strike, false));
        fight.Play(0, target: 0);

        Assert.True(plainStrike > 0, "the control Strike must actually hit");
        Assert.Equal(afterHang - plainStrike, fight.Enemy0.Hp);
    }

    /// <summary>The multiplier is read before the top-up, so Hang never scales itself.</summary>
    [Fact]
    public void ItDoesNotScaleItsOwnHit()
    {
        var fight = Fresh();
        fight.State.Hand.Add(new CardInstance(Hang, false));

        fight.Play(0, target: 0);

        Assert.Equal(490, fight.Enemy0.Hp);
    }
}
