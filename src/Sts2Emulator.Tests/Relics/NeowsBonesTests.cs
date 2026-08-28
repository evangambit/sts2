using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Neow's Bones: "gain 2 random Neow Relics, add 1 random Curse to your Deck", read off
/// MegaCrit.Sts2.Core.Models.Relics/NeowsBones.cs.
///
/// <para>
/// <c>AfterObtained</c> shuffles <c>GetValidRelics</c> on <c>PlayerRng.Rewards</c>, takes
/// two, and OFFERS them — a <c>RewardsSet</c> with <c>WithSkippingDisallowed</c>, so both
/// are claimed — and only then adds the curse. The emulator used to take two independent
/// <c>Rng.UpFront.NextItem</c> draws and apply them on the spot: the wrong stream, a draw
/// that can hand out the same relic twice where a shuffle-and-take cannot, no screen at
/// all, and a candidate list of only the positives.
/// </para>
/// </summary>
public class NeowsBonesTests
{
    private static RunEngine AtNeowsBones(string seed)
    {
        var engine = new RunEngine();
        engine.Reset(seed);
        engine.State.Phase = RunPhase.Ancient;
        engine.State.NeowOptions[0] = RunConstants.RelicNeowsBones;
        return engine;
    }

    private static int Claim(RunEngine engine) => engine.Step(0, -1, out _, out _, out _);

    [Fact]
    public void ItOffersTwoRelicsOnAScreenRatherThanGrantingThem()
    {
        var engine = AtNeowsBones("J09SPL8Y3V");

        engine.Step(0, -1, out _, out _, out _);

        Assert.Equal(RunPhase.RelicReward, engine.State.Phase);
        Assert.NotEqual(0, engine.State.RelicReward);
        Assert.Single(engine.State.PendingBonusRelicRewards);
        // Only Burning Blood and the Bones themselves — neither offered relic is held yet.
        Assert.Equal(2, engine.State.Relics.Count);
    }

    /// <summary>
    /// A live capture of this seed answers the screen twice and comes away with Winged
    /// Boots and then Silken Tress, in that order (`J09SPL8Y3V`).
    /// </summary>
    [Fact]
    public void TheOfferMatchesTheCapturedRun()
    {
        var engine = AtNeowsBones("J09SPL8Y3V");

        engine.Step(0, -1, out _, out _, out _);

        Assert.Equal(RunConstants.RelicWingedBoots, engine.State.RelicReward);
        Assert.Equal(RunConstants.RelicSilkenTress, engine.State.PendingBonusRelicRewards[0]);
    }

    /// <summary>
    /// A relic claimed from a reward screen is obtained through <c>RelicCmd.Obtain</c>,
    /// which runs its <c>AfterObtained</c> — Silken Tress zeroed the captured run's gold
    /// the moment it was taken.
    /// </summary>
    [Fact]
    public void ClaimingARelicRunsItsPickupEffect()
    {
        var engine = AtNeowsBones("J09SPL8Y3V");
        engine.Step(0, -1, out _, out _, out _);
        Assert.Equal(99, engine.State.Gold);

        Claim(engine);
        Claim(engine);

        Assert.Contains(engine.State.Relics, relic => relic.DefId == RunConstants.RelicSilkenTress);
        Assert.Equal(0, engine.State.Gold);
    }

    /// <summary>
    /// The curse comes after the screen, not with the relics: `AfterObtained` awaits the
    /// Offer() and adds it on the line below.
    /// </summary>
    [Fact]
    public void TheCurseArrivesOnlyOnceBothRelicsAreClaimed()
    {
        var engine = AtNeowsBones("J09SPL8Y3V");
        int deckBefore = engine.State.Deck.Count;
        engine.Step(0, -1, out _, out _, out _);

        Claim(engine);
        Assert.Equal(deckBefore, engine.State.Deck.Count);
        Assert.True(engine.State.PendingNeowsBonesCurse);

        Claim(engine);
        Assert.Equal(deckBefore + 1, engine.State.Deck.Count);
        Assert.False(engine.State.PendingNeowsBonesCurse);
    }

    /// <summary>
    /// And the claim that empties the screen returns to Neow itself, which stays up for
    /// one more Proceed the way it does after every other blessing.
    /// </summary>
    [Fact]
    public void TheLastClaimReturnsToNeowRatherThanTheMap()
    {
        var engine = AtNeowsBones("J09SPL8Y3V");
        engine.Step(0, -1, out _, out _, out _);

        Claim(engine);
        Assert.Equal(RunPhase.RelicReward, engine.State.Phase);

        Claim(engine);
        Assert.Equal(RunPhase.Ancient, engine.State.Phase);

        engine.Step(0, -1, out _, out _, out _);
        Assert.Equal(RunPhase.Map, engine.State.Phase);
    }

    /// <summary>
    /// A shuffle-and-take cannot repeat itself, where two independent draws can. Swept
    /// over many seeds because a duplicate is a one-in-twenty-six coincidence per run.
    /// </summary>
    [Fact]
    public void ItNeverOffersTheSameRelicTwice()
    {
        foreach (
            string seed in new[] { "J09SPL8Y3V", "ABCDEF", "0", "AAB", "QS2GYXRKWN", "WK1DEGZD8P" }
        )
        {
            var engine = AtNeowsBones(seed);
            engine.Step(0, -1, out _, out _, out _);

            Assert.NotEqual(engine.State.RelicReward, engine.State.PendingBonusRelicRewards[0]);
            Assert.NotEqual(RunConstants.RelicNeowsBones, engine.State.RelicReward);
            Assert.NotEqual(RunConstants.RelicNeowsBones, engine.State.PendingBonusRelicRewards[0]);
        }
    }
}
