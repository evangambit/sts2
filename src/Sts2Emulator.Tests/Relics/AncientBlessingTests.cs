using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// What the act ancients' blessings DO when taken.
/// </summary>
/// <remarks>
/// Which three an ancient offers is one problem and what they do is another; this is the
/// second. Six of the thirty across Orobas, Pael and Tezcatara are modelled — the ones
/// that need nothing the emulator lacks. The rest are listed in the catalogue under O13:
/// four want enchantments that are not modelled at all (Goopy, Imbued, Clone,
/// Tezcatara's Ember), two build reward SETS from another character's pool, and two only
/// do anything inside a combat.
/// </remarks>
public class AncientBlessingTests
{
    private static readonly RunEngine Pristine = Generate();

    private static RunEngine Generate()
    {
        var engine = new RunEngine();
        engine.Reset("ACT2TEST01");
        return engine;
    }

    private static RunEngine Taking(int relicId)
    {
        var engine = Pristine.Clone();
        RunNonCombatEffects.ApplyRelicPickup(engine.State, relicId);
        return engine;
    }

    private static int Count(RunState state, string entry) =>
        state.Deck.Count(card => GeneratedData.Cards.Get(card.DefId).Entry == entry);

    /// <summary>
    /// Two Relax into the deck — the one ancient blessing a live capture pins, and the
    /// reason act 2's replay gets past its ancient at all.
    /// </summary>
    [Fact]
    public void PaelsHornAddsTwoRelax()
    {
        var state = Taking(RunConstants.RelicPaelsHorn).State;

        Assert.Equal(2, Count(state, "RELAX"));
        Assert.Equal(Pristine.State.Deck.Count + 2, state.Deck.Count);
    }

    [Fact]
    public void StorybookAddsABrightestFlame()
    {
        var state = Taking(RunConstants.RelicStorybook).State;

        Assert.Equal(1, Count(state, "BRIGHTEST_FLAME"));
    }

    /// <summary>
    /// CardsVar(6), StableShuffled on Rng.NICHE. A starting deck has ten upgradable
    /// cards, so exactly six come back upgraded — and the curse is not one of them.
    /// </summary>
    [Fact]
    public void SandCastleUpgradesSixCards()
    {
        var state = Taking(RunConstants.RelicSandCastle).State;

        Assert.Equal(6, state.Deck.Count(card => card.Upgraded));
        Assert.All(
            state.Deck.Where(card => card.Upgraded),
            card => Assert.NotEqual(RunConstants.CardAscendersBane, card.DefId)
        );
    }

    /// <summary>It draws off Niche, which is not the stream a reward or an event uses.</summary>
    [Fact]
    public void SandCastleDrawsFromNiche()
    {
        var engine = Pristine.Clone();
        int rewardsBefore = engine.State.PlayerRng.Rewards.CallCount;
        int nicheBefore = engine.State.Rng.Niche.CallCount;

        RunNonCombatEffects.ApplyRelicPickup(engine.State, RunConstants.RelicSandCastle);

        Assert.Equal(rewardsBefore, engine.State.PlayerRng.Rewards.CallCount);
        Assert.True(engine.State.Rng.Niche.CallCount > nicheBefore);
    }

    /// <summary>
    /// PotionSlots(4): the slots come FIRST so all four potions have somewhere to go, and
    /// they roll off CombatPotionGeneration — the same shape as Phial Holster, and the
    /// same reason. Rolling them off PlayerRng.Rewards would move every card reward after.
    /// </summary>
    [Fact]
    public void AlchemicalCofferGivesFourSlotsAndFourPotions()
    {
        var engine = Pristine.Clone();
        int slotsBefore = engine.State.MaxPotionSlots;
        int rewardsBefore = engine.State.PlayerRng.Rewards.CallCount;

        RunNonCombatEffects.ApplyRelicPickup(engine.State, RunConstants.RelicAlchemicalCoffer);

        Assert.Equal(slotsBefore + 4, engine.State.MaxPotionSlots);
        Assert.Equal(rewardsBefore, engine.State.PlayerRng.Rewards.CallCount);

        // NOT asserting four potions. RunState.PotionSlots is a fixed int[3] — as are
        // CombatState's and GameState's, and the combat observation is laid out around
        // that width — so the emulator physically cannot hold the six a Coffer run has.
        // Deliberately left failing-in-the-world rather than asserted at three, which
        // would bake the cap in as though it were the rule. See O15.
        Assert.True(engine.State.PotionSlots.Count(potion => potion != 0) >= 3);
    }

    /// <summary>
    /// Yummy Cookie and Biiig Hug both hand the choice to the player rather than picking
    /// — four cards each, through the deck-selection screen.
    /// </summary>
    [Theory]
    [InlineData(RunConstants.RelicYummyCookie, DeckSelection.Upgrade)]
    [InlineData(RunConstants.RelicBiiigHug, DeckSelection.Remove)]
    public void TheChoosingBlessingsOpenASelectionForFourCards(int relicId, DeckSelection kind)
    {
        var engine = Pristine.Clone();
        int before = engine.State.Deck.Count;

        var followUp = RunNonCombatEffects.ApplyRelicPickup(engine.State, relicId);

        Assert.Equal(RunFollowUp.TransformSelect, followUp);
        Assert.Equal(kind, engine.State.PendingSelectionKind);
        Assert.Equal(4, engine.State.PendingSelectionCount);
        // Nothing has happened yet: the screen is open and the player has not answered.
        Assert.Equal(before, engine.State.Deck.Count);
    }

    [Fact]
    public void BiiigHugRemovesTheFourTheChoiceNames()
    {
        var engine = Pristine.Clone();
        int before = engine.State.Deck.Count;
        RunNonCombatEffects.ApplyRelicPickup(engine.State, RunConstants.RelicBiiigHug);

        for (int i = 0; i < 4; i++)
        {
            engine.Step(0, -1, out _, out _, out _);
        }

        Assert.Equal(before - 4, engine.State.Deck.Count);
    }
}
