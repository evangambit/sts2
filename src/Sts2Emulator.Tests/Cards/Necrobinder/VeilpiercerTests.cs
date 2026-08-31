using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/Veilpiercer.cs: DamageVar(10m) upgrading by 3, then
// PowerCmd.Apply<VeilpiercerPower> for 1 — a flat stack the upgrade does not touch.
//
// VeilpiercerPower is scoped by KEYWORD, not by card type: its
// TryModifyEnergyCostInCombatLate zeroes any Ethereal card in hand, and its
// BeforeCardPlayed spends a stack whenever an Ethereal card is played. The emulator dealt
// the damage and applied nothing at all — a live capture found the missing power.
public class VeilpiercerTests
{
    private const int Veilpiercer = 531;
    private const int Defile = 135; // Ethereal Attack, cost 1
    private const int Defy = 138; // Ethereal Skill, cost 1
    private const int Poke = 357; // not Ethereal, cost 0
    private const int StrikeNecrobinder = 473; // basic Strike, cost 1

    private static Fight Fresh() => Fight.Hand().Energy(9).Enemy(hp: 200);

    [Fact]
    public void ItAppliesOneStackRegardlessOfUpgrade()
    {
        var fight = Fresh();
        fight.State.Hand.Add(new CardInstance(Veilpiercer, true));

        fight.Play(0, target: 0);

        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Veilpiercer));
    }

    [Fact]
    public void ItStacksAcrossCopies()
    {
        var fight = Fresh();
        fight.State.Hand.Add(new CardInstance(Veilpiercer, false));
        fight.State.Hand.Add(new CardInstance(Veilpiercer, false));

        fight.Play(0, target: 0);
        fight.Play(0, target: 0);

        Assert.Equal(2, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Veilpiercer));
    }

    [Fact]
    public void EtherealCardsCostNothingWhileItIsUp()
    {
        var fight = Fresh().PlayerBuff(BuffId.Veilpiercer, 1);
        var ethereal = new CardInstance(Defy, false);
        var plain = new CardInstance(StrikeNecrobinder, false);

        Assert.Equal(0, CombatEngine.EffectiveCost(ethereal, fight.State));
        Assert.Equal(1, CombatEngine.EffectiveCost(plain, fight.State));
    }

    /// <summary>Without the power the same card is back to its printed cost.</summary>
    [Fact]
    public void WithoutThePowerEtherealCardsCostTheirPrintedCost()
    {
        var fight = Fresh();

        Assert.Equal(1, CombatEngine.EffectiveCost(new CardInstance(Defy, false), fight.State));
    }

    [Fact]
    public void PlayingAnEtherealCardSpendsAStack()
    {
        var fight = Fresh().PlayerBuff(BuffId.Veilpiercer, 2);
        fight.State.Hand.Add(new CardInstance(Defile, false));

        fight.Play(0, target: 0);

        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Veilpiercer));
    }

    [Fact]
    public void PlayingANonEtherealCardDoesNot()
    {
        var fight = Fresh().PlayerBuff(BuffId.Veilpiercer, 2);
        fight.State.Hand.Add(new CardInstance(Poke, false));

        fight.Play(0, target: 0);

        Assert.Equal(2, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Veilpiercer));
    }

    /// <summary>
    /// FreeAttackPower and VeilpiercerPower are separate powers, each with its own
    /// BeforeCardPlayed, so an Ethereal ATTACK spends one stack of each — a single
    /// if/else chain over the two would have kept one of them alive for a later card.
    /// </summary>
    [Fact]
    public void AnEtherealAttackSpendsAStackOfBothItAndFreeAttackPower()
    {
        var fight = Fresh().PlayerBuff(BuffId.Veilpiercer, 1).PlayerBuff(BuffId.FreeAttackPower, 1);
        fight.State.Hand.Add(new CardInstance(Defile, false));

        fight.Play(0, target: 0);

        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Veilpiercer));
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.FreeAttackPower));
    }

    /// <summary>
    /// The power reads `Card.Keywords`, and GhostSeed puts Ethereal ON basic Strikes and
    /// Defends when they enter combat — so those really are free here, even though the
    /// printed definition is not Ethereal.
    /// </summary>
    [Fact]
    public void GhostSeededBasicsCountAsEthereal()
    {
        var fight = Fight
            .WithRelics(RelicEffects.GhostSeed)
            .Energy(9)
            .Enemy(hp: 200)
            .PlayerBuff(BuffId.Veilpiercer, 1);
        var strike = new CardInstance(StrikeNecrobinder, false);

        Assert.Equal(0, CombatEngine.EffectiveCost(strike, fight.State));
    }
}
