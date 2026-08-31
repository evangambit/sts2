using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Sts2Emulator.Core.Run;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// The last of the shop pool: five relics that hook the REWARD list, plus Toolbox, whose
// screen is a combat one.

public class LavaLampTests
{
    /// <summary>
    /// Every upgradable option on the card-reward screen is upgraded after a combat that
    /// landed no unblocked damage — the same shape as Silver Crucible's, read once for the
    /// whole screen.
    /// </summary>
    [Fact]
    public void ACleanCombatUpgradesTheWholeScreen()
    {
        var clean = new RunEngine();
        clean.Reset("LAVA");
        clean.State.Relics.Add(new RelicInstance(RelicEffects.LavaLamp));
        clean.State.TookUnblockedDamageThisCombat = false;
        RunRewardGenerator.PopulateCardReward(clean.State);

        var upgradable = Enumerable
            .Range(0, clean.State.RewardCards.Length)
            .Where(i =>
                RunConstants.IsRunCardUpgradable(
                    new CardInstance(clean.State.RewardCards[i], false)
                )
            )
            .ToList();

        Assert.NotEmpty(upgradable);
        Assert.All(upgradable, i => Assert.True(clean.State.RewardUpgraded[i]));
    }

    /// <summary>
    /// `AfterDamageReceived` ignores Unblockable damage, so only damage the player could
    /// have blocked spoils it — which is why the flag is set from the blockable path.
    /// </summary>
    [Fact]
    public void UnblockableDamageDoesNotSpoilIt()
    {
        var fight = Fight.Hand().Energy(3).Enemy(hp: 200);
        fight.State.PlayerHp = 200;

        CardEffects.LoseHp(fight.State, 10);
        Assert.False(fight.State.TookUnblockedDamage);

        fight.State.PlayerBlock = 0;
        CardEffects.DealDamageToPlayer(fight.State, 10);
        Assert.True(fight.State.TookUnblockedDamage);
    }
}

public class DingyRugTests
{
    /// <summary>
    /// The colourless pool is ADDED to the reward pool, not swapped for it — so the
    /// character's own cards are still on offer alongside.
    /// </summary>
    [Fact]
    public void ColourlessCardsBecomeReachable()
    {
        var colourless = GeneratedData.CardPools.Colorless.ToArray().ToHashSet();
        bool sawColourless = false;

        for (int seed = 0; seed < 25 && !sawColourless; seed++)
        {
            var engine = new RunEngine();
            engine.Reset($"RUG{seed}");
            engine.State.Relics.Add(new RelicInstance(RelicEffects.DingyRug));
            RunRewardGenerator.PopulateCardReward(engine.State);
            sawColourless = engine.State.RewardCards.Any(colourless.Contains);
        }

        Assert.True(sawColourless, "no seed offered a colourless card with Dingy Rug");
    }

    [Fact]
    public void WithoutItTheyAreNot()
    {
        var colourless = GeneratedData.CardPools.Colorless.ToArray().ToHashSet();

        for (int seed = 0; seed < 25; seed++)
        {
            var engine = new RunEngine();
            engine.Reset($"RUG{seed}");
            RunRewardGenerator.PopulateCardReward(engine.State);
            Assert.DoesNotContain(engine.State.RewardCards, colourless.Contains);
        }
    }
}

public class WingCharmTests
{
    /// <summary>
    /// Finds a seed whose reward screen holds at least one card Swift can go on. Swift is
    /// POWERS ONLY, and a three-card reward often has none — which is not a failure of the
    /// relic but the `if (list.Count == 0) return false` in its own body.
    /// </summary>
    private static RunEngine ScreenWithAPower(bool withCharm)
    {
        for (int seed = 0; seed < 60; seed++)
        {
            var engine = new RunEngine();
            engine.Reset($"WING{seed}");
            if (withCharm)
            {
                engine.State.Relics.Add(new RelicInstance(RelicEffects.WingCharm));
            }

            RunRewardGenerator.PopulateCardReward(engine.State);
            bool hasPower = engine.State.RewardCards.Any(id =>
                id != 0 && GeneratedData.Cards.Get(id).Type == CardType.Power
            );
            if (hasPower)
            {
                return engine;
            }
        }

        Assert.Fail("no seed in 60 produced a reward screen holding a Power");
        return null!;
    }

    [Fact]
    public void OneRewardOptionCarriesSwift()
    {
        var engine = ScreenWithAPower(withCharm: true);

        Assert.InRange(engine.State.RewardEnchantIndex, 0, engine.State.RewardCards.Length - 1);
        Assert.Equal(Enchantment.Swift, engine.State.RewardEnchantment);
    }

    [Fact]
    public void WithoutItNoOptionIsEnchanted()
    {
        var engine = ScreenWithAPower(withCharm: false);

        Assert.Equal(-1, engine.State.RewardEnchantIndex);
    }

    /// <summary>Swift is Powers-only, so the pick is filtered to a card that can take it.</summary>
    [Fact]
    public void ItPicksACardThatCanTakeSwift()
    {
        var engine = ScreenWithAPower(withCharm: true);

        int index = engine.State.RewardEnchantIndex;
        var chosen = new CardInstance(engine.State.RewardCards[index], false);
        Assert.True(Enchantments.CanEnchant(chosen, Enchantment.Swift));
    }

    /// <summary>A screen with no Power on it gets nothing — and that is the relic, not a gap.</summary>
    [Fact]
    public void AScreenWithNoPowerIsLeftAlone()
    {
        for (int seed = 0; seed < 60; seed++)
        {
            var engine = new RunEngine();
            engine.Reset($"NOPOWER{seed}");
            engine.State.Relics.Add(new RelicInstance(RelicEffects.WingCharm));
            RunRewardGenerator.PopulateCardReward(engine.State);

            bool hasPower = engine.State.RewardCards.Any(id =>
                id != 0 && GeneratedData.Cards.Get(id).Type == CardType.Power
            );
            if (!hasPower)
            {
                Assert.Equal(-1, engine.State.RewardEnchantIndex);
                return;
            }
        }

        Assert.Fail("every seed in 60 produced a Power; the no-Power path went untested");
    }
}

public class OrreryTests
{
    /// <summary>
    /// FIVE whole card-reward screens, not five cards on one — `RewardsCmd.OfferCustom`
    /// takes a list of five `CardReward`s.
    /// </summary>
    [Fact]
    public void ItOwesFiveCardRewards()
    {
        var state = new RunState();

        var followUp = RunNonCombatEffects.ApplyRelicPickup(state, RelicEffects.Orrery);

        Assert.Equal(RunFollowUp.PreRolledCardReward, followUp);
        Assert.Equal(5, state.ExtraCardRewardsOwed);
    }
}

public class CauldronTests
{
    [Fact]
    public void ItOwesFivePotionRewards()
    {
        var state = new RunState();

        var followUp = RunNonCombatEffects.ApplyRelicPickup(state, RelicEffects.Cauldron);

        Assert.Equal(RunFollowUp.BonusRelicRewards, followUp);
        Assert.Equal(5, state.PendingPotionRewards.Count);
    }
}

public class ToolboxTests
{
    /// <summary>Three DISTINCT colourless cards on turn one, one of which joins the hand.</summary>
    [Fact]
    public void ThreeColourlessCardsAreOffered()
    {
        var fight = Fight.WithRelics(RelicEffects.Toolbox);

        Assert.NotNull(fight.Pending);
        Assert.Equal(CardSelectionKind.GeneratedCardToHand, fight.Pending!.Kind);
        Assert.Equal(3, fight.Pending.GeneratedCandidates.Count);
        Assert.Equal(3, fight.Pending.GeneratedCandidates.Distinct().Count());

        var colourless = GeneratedData.CardPools.Colorless.ToArray().ToHashSet();
        Assert.All(fight.Pending.GeneratedCandidates, id => Assert.Contains(id, colourless));
    }

    [Fact]
    public void TheChosenCardJoinsTheHand()
    {
        var fight = Fight.WithRelics(RelicEffects.Toolbox);
        int picked = fight.Pending!.GeneratedCandidates[1];

        fight.Choose(1);

        Assert.Null(fight.Pending);
        Assert.Contains(fight.State.Hand, c => c.DefId == picked);
    }

    /// <summary>
    /// Only one screen can be up at a time, so a Gambling Chip held alongside is OWED and
    /// follows once the Toolbox pick is made rather than being lost.
    /// </summary>
    [Fact]
    public void AGamblingChipScreenFollowsIt()
    {
        var fight = Fight.WithRelics(RelicEffects.Toolbox, RelicEffects.GamblingChip);

        Assert.Equal(CardSelectionKind.GeneratedCardToHand, fight.Pending!.Kind);
        Assert.True(fight.State.GamblingChipOwed);

        fight.Choose(0);

        Assert.NotNull(fight.Pending);
        Assert.Equal(CardSelectionKind.DiscardAnyThenDraw, fight.Pending!.Kind);
    }
}
