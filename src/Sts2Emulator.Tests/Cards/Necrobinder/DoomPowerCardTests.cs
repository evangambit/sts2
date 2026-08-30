using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Five Necrobinder cards that were labels on High Five's Osty-attack body — which does
/// nothing without a pet, so all five did nothing at all. One class each, because the
/// coverage gate looks for a <c>&lt;Name&gt;Tests</c> class per card, and they share this
/// board.
/// </summary>
internal static class NecrobinderPowerBoard
{
    internal const int SweepingGaze = 485;
    internal const int Defile = 135; // Ethereal
    internal const int Strike = 473;
    internal const int Defend = 132;

    internal static Fight Fresh() => Fight.Hand().Energy(9).Enemy(hp: 500);

    internal static Fight Playing(int defId, bool upgraded = false)
    {
        var fight = Fresh();
        fight.State.Hand.Add(new CardInstance(defId, upgraded));
        fight.Play(0);
        return fight;
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Pagestorm.cs: CardsVar(1), and the upgrade is a
// discount. `PagestormPower.AfterCardDrawn` draws Amount more when the card drawn is
// ETHEREAL.
public class PagestormTests
{
    private const int Pagestorm = 340;

    [Fact]
    public void ItDrawsOneMoreForAnEtherealCardDrawn()
    {
        var fight = Fight
            .Hand()
            .Energy(9)
            .Draw(
                new CardInstance(NecrobinderPowerBoard.Defile, false),
                new CardInstance(NecrobinderPowerBoard.Strike, false)
            )
            .Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Pagestorm, false));
        fight.Play(0);

        CardEffects.DrawCards(fight.State, 1, new Random(1));

        // The Ethereal card, and the extra draw it bought.
        Assert.Equal(2, fight.State.Hand.Count);
    }

    [Fact]
    public void ItIgnoresOrdinaryCards()
    {
        var fight = Fight
            .Hand()
            .Energy(9)
            .Draw(
                new CardInstance(NecrobinderPowerBoard.Strike, false),
                new CardInstance(NecrobinderPowerBoard.Defend, false)
            )
            .Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Pagestorm, false));
        fight.Play(0);

        CardEffects.DrawCards(fight.State, 1, new Random(1));

        Assert.Single(fight.State.Hand);
    }

    [Fact]
    public void TheUpgradeIsADiscountAndStillOneStack()
    {
        var fight = NecrobinderPowerBoard.Playing(Pagestorm, upgraded: true);

        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Pagestorm));
    }
}

// MegaCrit.Sts2.Core.Models.Cards/ReaperForm.cs: `PowerCmd.Apply<ReaperFormPower>(1)`,
// and the upgrade only adds Retain. `AfterDamageGiven` Dooms the target for
// `result.TotalDamage` on any powered attack by the player or their pet — and TotalDamage
// is blocked plus unblocked.
public class ReaperFormTests
{
    private const int ReaperForm = 385;

    [Fact]
    public void ItDoomsForTheDamageDealt()
    {
        var fight = NecrobinderPowerBoard.Playing(ReaperForm);
        fight.State.Hand.Add(new CardInstance(NecrobinderPowerBoard.Strike, false));
        int before = fight.Enemy0.Hp;

        fight.Play(0, target: 0);

        Assert.Equal(before - fight.Enemy0.Hp, fight.EnemyBuffAmount(BuffId.Doom));
    }

    /// <summary>`TotalDamage` is blocked plus unblocked, so a shield does not stop it.</summary>
    [Fact]
    public void ItDoomsThroughBlock()
    {
        var fight = NecrobinderPowerBoard.Playing(ReaperForm);
        fight.Enemy0.Block = 100;
        fight.State.Hand.Add(new CardInstance(NecrobinderPowerBoard.Strike, false));

        fight.Play(0, target: 0);

        Assert.Equal(500, fight.Enemy0.Hp);
        Assert.True(fight.EnemyBuffAmount(BuffId.Doom) > 0);
    }

    [Fact]
    public void TheUpgradeIsOneStackToo()
    {
        var fight = NecrobinderPowerBoard.Playing(ReaperForm, upgraded: true);

        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.ReaperForm));
    }
}

// MegaCrit.Sts2.Core.Models.Cards/SentryMode.cs: PowerVar(1), upgrade is a discount.
// `SentryModePower.BeforeHandDraw` puts Amount Sweeping Gazes into HAND, before the draw.
public class SentryModeTests
{
    private const int SentryMode = 419;

    [Fact]
    public void ItPutsASweepingGazeInHandEachTurn()
    {
        var fight = NecrobinderPowerBoard.Playing(SentryMode);

        fight.EndTurn();
        Assert.Single(fight.State.Hand.Where(c => c.DefId == NecrobinderPowerBoard.SweepingGaze));

        fight.EndTurn();
        Assert.Single(fight.State.Hand.Where(c => c.DefId == NecrobinderPowerBoard.SweepingGaze));
    }

    [Fact]
    public void ItGivesNoneOnTheTurnItIsPlayed()
    {
        var fight = NecrobinderPowerBoard.Playing(SentryMode);

        Assert.Empty(fight.State.Hand.Where(c => c.DefId == NecrobinderPowerBoard.SweepingGaze));
    }

    [Fact]
    public void TheUpgradeIsADiscountAndStillOneStack()
    {
        var fight = NecrobinderPowerBoard.Playing(SentryMode, upgraded: true);

        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.SentryMode));
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Shroud.cs: BlockVar(2, Unpowered) upgrading by 1.
// `ShroudPower.AfterPowerAmountChanged` gains that much block whenever its owner applies
// DOOM — not block next turn, which is what the emulator granted.
public class ShroudTests
{
    private const int Shroud = 432;
    private const int NoEscape = 326;
    private const int ReaperForm = 385;

    [Fact]
    public void ItGainsBlockWhenTheEnemyIsDoomed()
    {
        var fight = NecrobinderPowerBoard.Playing(Shroud);
        Assert.Equal(0, fight.State.PlayerBlock);

        fight.State.Hand.Add(new CardInstance(NoEscape, false));
        fight.Play(0, target: 0);

        Assert.Equal(2, fight.State.PlayerBlock);
    }

    [Fact]
    public void TheUpgradeGainsThree()
    {
        var fight = NecrobinderPowerBoard.Playing(Shroud, upgraded: true);
        fight.State.Hand.Add(new CardInstance(NoEscape, false));

        fight.Play(0, target: 0);

        Assert.Equal(3, fight.State.PlayerBlock);
    }

    [Fact]
    public void ItGainsNothingForOrdinaryDamage()
    {
        var fight = NecrobinderPowerBoard.Playing(Shroud);

        CardEffects.DealDamage(fight.State, 1);

        Assert.Equal(0, fight.State.PlayerBlock);
    }

    [Fact]
    public void ItGrantsNoBlockNextTurn()
    {
        var fight = NecrobinderPowerBoard.Playing(Shroud);

        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.BlockNextTurn));
    }

    /// <summary>Reaper Form turns every attack into a Doom, which Shroud pays for.</summary>
    [Fact]
    public void ItAndReaperFormPayEachOther()
    {
        var fight = NecrobinderPowerBoard.Playing(Shroud, upgraded: true);
        fight.State.Hand.Add(new CardInstance(ReaperForm, false));
        fight.Play(0);

        fight.State.Hand.Add(new CardInstance(NecrobinderPowerBoard.Strike, false));
        fight.Play(0, target: 0);

        Assert.Equal(3, fight.State.PlayerBlock);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/NoEscape.cs: CalculationBase 10 upgrading by 5, plus
// CalculationExtra 5 for each FULL DoomThreshold of 10 already on the target —
// `Math.Floor(doom / 10)`.
public class NoEscapeTests
{
    private const int NoEscape = 326;

    [Fact]
    public void ItDoomsForTenAndFifteenUpgraded()
    {
        var fight = NecrobinderPowerBoard.Fresh();
        fight.State.Hand.Add(new CardInstance(NoEscape, false));
        fight.Play(0, target: 0);
        Assert.Equal(10, fight.EnemyBuffAmount(BuffId.Doom));

        var upgraded = NecrobinderPowerBoard.Fresh();
        upgraded.State.Hand.Add(new CardInstance(NoEscape, true));
        upgraded.Play(0, target: 0);
        Assert.Equal(15, upgraded.EnemyBuffAmount(BuffId.Doom));
    }

    /// <summary>Five more per FULL ten — 19 Doom is one threshold, not two.</summary>
    [Fact]
    public void ItScalesWithTheDoomAlreadyOnTheTarget()
    {
        var fight = NecrobinderPowerBoard.Fresh();
        BuffSystem.Apply(fight.Enemy0.Buffs, BuffId.Doom, 19);
        fight.State.Hand.Add(new CardInstance(NoEscape, false));

        fight.Play(0, target: 0);

        Assert.Equal(19 + 15, fight.EnemyBuffAmount(BuffId.Doom));
    }

    [Fact]
    public void ItCountsTwoFullThresholds()
    {
        var fight = NecrobinderPowerBoard.Fresh();
        BuffSystem.Apply(fight.Enemy0.Buffs, BuffId.Doom, 20);
        fight.State.Hand.Add(new CardInstance(NoEscape, false));

        fight.Play(0, target: 0);

        Assert.Equal(20 + 20, fight.EnemyBuffAmount(BuffId.Doom));
    }
}
