using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The two Common relics that pay out between combats, read off
/// MegaCrit.Sts2.Core.Models.Relics: Meal Ticket HealVar(15m) on AfterRoomEntered of a
/// MerchantRoom, Regal Pillow HealVar(15m) added by ModifyRestSiteHealAmount.
/// </summary>
public class RunSiteRelicTests
{
    [Fact]
    public void MealTicketHealsFifteenOnEnteringAShop()
    {
        var plain = ShopBoundRun();
        var withTicket = ShopBoundRun();
        withTicket.Relics.Add(new RelicInstance(RelicEffects.MealTicket));

        RunRewardGenerator.EnterShop(plain);
        RunRewardGenerator.EnterShop(withTicket);

        Assert.Equal(50, plain.PlayerHp);
        Assert.Equal(65, withTicket.PlayerHp);
    }

    [Fact]
    public void MealTicketCannotHealPastMaxHp()
    {
        var state = ShopBoundRun();
        state.PlayerHp = state.PlayerMaxHp - 2;
        state.Relics.Add(new RelicInstance(RelicEffects.MealTicket));

        RunRewardGenerator.EnterShop(state);

        Assert.Equal(state.PlayerMaxHp, state.PlayerHp);
    }

    /// <summary>The game skips the heal outright when the owner is dead.</summary>
    [Fact]
    public void MealTicketDoesNothingForADeadPlayer()
    {
        var state = ShopBoundRun();
        state.PlayerHp = 0;
        state.Relics.Add(new RelicInstance(RelicEffects.MealTicket));

        RunRewardGenerator.EnterShop(state);

        Assert.Equal(0, state.PlayerHp);
    }

    [Fact]
    public void RegalPillowAddsFifteenToARestSiteHeal()
    {
        int plain = RestAndReportHealing(withPillow: false);
        int withPillow = RestAndReportHealing(withPillow: true);

        Assert.Equal(plain + 15, withPillow);
    }

    private static RunState ShopBoundRun()
    {
        var engine = new RunEngine();
        engine.Reset("0");
        engine.State.PlayerHp = 50;
        return engine.State;
    }

    /// <summary>Rests at 1 HP so nothing is lost to the max-HP cap, and reports the heal.</summary>
    private static int RestAndReportHealing(bool withPillow)
    {
        var engine = new RunEngine();
        engine.Reset("0");
        if (withPillow)
        {
            engine.State.Relics.Add(new RelicInstance(RelicEffects.RegalPillow));
        }

        engine.State.Phase = RunPhase.Rest;
        engine.State.PlayerHp = 1;
        engine.Step(RunConstants.RestHealAction, -1, out _, out _, out _);

        return engine.State.PlayerHp - 1;
    }

    /// <summary>
    /// ChosenCheese.AfterCombatEnd is GainMaxHp(1), and gaining a maximum heals by the
    /// same amount — so a fight won at 2 HP ends at 3 before Burning Blood's six.
    /// </summary>
    /// <remarks>
    /// The emulator could already be given this relic by Room Full of Cheese and then did
    /// nothing with it, which is worth more than one point: it surfaced as a single HP of
    /// difference 126 steps into a capture (`NXV45HW43K`) with nothing else wrong in 149,
    /// and every fight after it would have compounded.
    /// </remarks>
    [Fact]
    public void ChosenCheeseGainsAMaxHpEveryTimeACombatEnds()
    {
        var state = ShopBoundRun();
        state.Relics.Add(new RelicInstance(RunConstants.RelicChosenCheese));
        int maxBefore = state.PlayerMaxHp;

        RunRewardGenerator.GenerateCombatRewards(state);

        Assert.Equal(maxBefore + 1, state.PlayerMaxHp);
        // 50, +1 for the maximum, +6 for Burning Blood.
        Assert.Equal(57, state.PlayerHp);
    }

    [Fact]
    public void WithoutTheCheeseTheMaximumDoesNotMove()
    {
        var state = ShopBoundRun();
        int maxBefore = state.PlayerMaxHp;

        RunRewardGenerator.GenerateCombatRewards(state);

        Assert.Equal(maxBefore, state.PlayerMaxHp);
        Assert.Equal(56, state.PlayerHp);
    }
}
