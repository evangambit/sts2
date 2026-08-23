using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The Act 1 events that are one page and one effect per option.
///
/// Nothing structural is missing from these, so what is worth pinning is the part a
/// capture proves only at the state it was taken in: the amounts, and which option is
/// gated on what. Every one of these numbers comes from the event's own DynamicVars.
/// </summary>
[CoversEvent("ByrdonisNest")]
[CoversEvent("DenseVegetation")]
[CoversEvent("JungleMazeAdventure")]
[CoversEvent("SapphireSeed")]
[CoversEvent("SelfHelpBook")]
[CoversEvent("ThisOrThat")]
[CoversEvent("UnrestSite")]
public class SimpleActOneEventTests
{
    private static RunEngine At(int eventId, string seed = "ABCDEF")
    {
        var engine = new RunEngine();
        engine.Reset(seed);
        engine.State.EventId = eventId;
        engine.State.Phase = RunPhase.Event;
        return engine;
    }

    private static int[] Offered(RunEngine engine)
    {
        var mask = new int[RunConstants.MaxActions];
        engine.WriteActionMask(mask);
        return Enumerable
            .Range(0, RunConstants.EventSkipAction)
            .Where(index => mask[index] != 0)
            .ToArray();
    }

    private static int CountOf(RunState state, string card) =>
        state.Deck.Count(c => c.DefId == RunNonCombatEffects.NamedCard(card));

    // ── Byrdonis Nest ────────────────────────────────────────────────────────

    [Fact]
    public void EatingTheEggGainsSevenMaxHp()
    {
        var engine = At(RunConstants.EventByrdonisNest);

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(87, engine.State.PlayerMaxHp);
        Assert.Equal(71, engine.State.PlayerHp); // GainMaxHp carries current hp up with it
    }

    [Fact]
    public void TakingTheEggAddsTheQuestCardAndCostsNothing()
    {
        var engine = At(RunConstants.EventByrdonisNest);

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

        Assert.Equal(1, CountOf(engine.State, "ByrdonisEgg"));
        Assert.Equal(80, engine.State.PlayerMaxHp);
        Assert.Equal(64, engine.State.PlayerHp);
    }

    // ── Unrest Site ──────────────────────────────────────────────────────────

    /// <summary>
    /// Rest heals <c>MaxHp - CurrentHp</c> -- to full, whatever the gap -- and the Poor
    /// Sleep is the price. It is offered at full health too, where the heal is nothing and
    /// the curse still lands.
    /// </summary>
    [Theory]
    [InlineData(64)]
    [InlineData(1)]
    [InlineData(80)]
    public void RestingHealsToFullAndAlwaysCostsAPoorSleep(int hp)
    {
        var engine = At(RunConstants.EventUnrestSite);
        engine.State.PlayerHp = hp;

        Assert.Contains(0, Offered(engine));
        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(80, engine.State.PlayerHp);
        Assert.Equal(1, CountOf(engine.State, "PoorSleep"));
    }

    /// <summary>
    /// Killing the trees costs 8 MAX hp, and LoseMaxHp only damages by however far the new
    /// cap falls below current hp -- so a run at 64/80 keeps all 64.
    /// </summary>
    [Fact]
    public void KillingTheTreesCostsMaxHpAndBuysARelic()
    {
        var engine = At(RunConstants.EventUnrestSite);

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

        Assert.Equal(72, engine.State.PlayerMaxHp);
        Assert.Equal(64, engine.State.PlayerHp);
        Assert.Equal(2, engine.State.Relics.Count);
    }

    [Fact]
    public void KillingTheTreesDragsCurrentHpDownWithTheCap()
    {
        var engine = At(RunConstants.EventUnrestSite);
        engine.State.PlayerHp = 80;

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

        Assert.Equal(72, engine.State.PlayerMaxHp);
        Assert.Equal(72, engine.State.PlayerHp);
    }

    // ── This or That ─────────────────────────────────────────────────────────

    [Fact]
    public void ThePlainChestCostsSixHpAndPaysItsRolledGold()
    {
        var engine = At(RunConstants.EventThisOrThat);
        int gold = RunNonCombatEffects.ThisOrThatGold(engine.State);
        Assert.InRange(gold, 41, 68);

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(58, engine.State.PlayerHp);
        Assert.Equal(99 + gold, engine.State.Gold);
    }

    [Fact]
    public void TheOrnateChestCostsAClumsyAndBuysARelic()
    {
        var engine = At(RunConstants.EventThisOrThat);

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

        Assert.Equal(1, CountOf(engine.State, "Clumsy"));
        Assert.Equal(2, engine.State.Relics.Count);
        Assert.Equal(64, engine.State.PlayerHp);
        Assert.Equal(99, engine.State.Gold);
    }

    // ── Jungle Maze Adventure ────────────────────────────────────────────────

    /// <summary>
    /// Both purses are rolled with NextFloat rather than fixed: 150 and 50, each shifted
    /// by NextFloat(-15, 15), and DynamicVar.IntValue truncates.
    /// </summary>
    [Fact]
    public void TheSoloQuestPaysMoreAndCostsEighteen()
    {
        var engine = At(RunConstants.EventJungleMazeAdventure);
        int solo = RunNonCombatEffects.JungleMazeSoloGold(engine.State);
        int joint = RunNonCombatEffects.JungleMazeJoinForcesGold(engine.State);
        Assert.InRange(solo, 135, 165);
        Assert.InRange(joint, 35, 65);
        Assert.True(solo > joint);

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(46, engine.State.PlayerHp);
        Assert.Equal(99 + solo, engine.State.Gold);
    }

    [Fact]
    public void JoiningForcesCostsNothing()
    {
        var engine = At(RunConstants.EventJungleMazeAdventure);
        int joint = RunNonCombatEffects.JungleMazeJoinForcesGold(engine.State);

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

        Assert.Equal(64, engine.State.PlayerHp);
        Assert.Equal(99 + joint, engine.State.Gold);
    }

    // ── Dense Vegetation ─────────────────────────────────────────────────────

    [Fact]
    public void TrudgingOnCostsEightHpAndPaysItsRolledGold()
    {
        var engine = At(RunConstants.EventDenseVegetation);
        int gold = RunNonCombatEffects.DenseVegetationGold(engine.State);
        Assert.InRange(gold, 61, 99);

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(56, engine.State.PlayerHp);
        Assert.Equal(99 + gold, engine.State.Gold);
    }

    // ── Sapphire Seed ────────────────────────────────────────────────────────

    [Fact]
    public void EatingTheSeedHealsNineThenAsksWhatToUpgrade()
    {
        var engine = At(RunConstants.EventSapphireSeed);
        engine.State.PlayerHp = 40;

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(49, engine.State.PlayerHp);
        Assert.Equal(RunPhase.TransformSelect, engine.State.Phase);
        Assert.Equal(DeckSelection.Upgrade, engine.State.PendingSelectionKind);
    }

    [Fact]
    public void PlantingTheSeedEnchantsWithSown()
    {
        var engine = At(RunConstants.EventSapphireSeed);

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));
        Assert.Equal((int)Enchantment.Sown, engine.State.PendingSelectionArg);

        int index = Enumerable
            .Range(0, engine.State.Deck.Count)
            .First(i => RunNonCombatEffects.CanSelectCard(engine.State, i));
        Assert.Equal(0, engine.Step(index, -1, out _, out _, out _));

        Assert.Equal(Enchantment.Sown, engine.State.Deck[index].Enchantment);
        Assert.Equal(64, engine.State.PlayerHp); // planting costs nothing
    }

    // ── Self-Help Book ───────────────────────────────────────────────────────

    /// <summary>
    /// Three pages, one per card type: Sharp for Attacks, Nimble for Skills, Swift for
    /// Powers, each at 2. A page is offered only when the deck holds a card its
    /// enchantment would take -- the starter deck has no Power, so the third is locked.
    /// </summary>
    [Fact]
    public void OnlyThePagesTheDeckCanUseAreOffered()
    {
        var engine = At(RunConstants.EventSelfHelpBook);

        Assert.Equal(new[] { 0, 1 }, Offered(engine));
    }

    [Theory]
    [InlineData(0, Enchantment.Sharp, CardType.Attack)]
    [InlineData(1, Enchantment.Nimble, CardType.Skill)]
    public void EachPageEnchantsItsOwnCardType(int option, Enchantment enchantment, CardType type)
    {
        var engine = At(RunConstants.EventSelfHelpBook);

        Assert.Equal(0, engine.Step(option, -1, out _, out _, out _));
        Assert.Equal((int)enchantment, engine.State.PendingSelectionArg);

        int index = Enumerable
            .Range(0, engine.State.Deck.Count)
            .First(i => RunNonCombatEffects.CanSelectCard(engine.State, i));
        Assert.Equal(type, GeneratedData.Cards.Get(engine.State.Deck[index].DefId).Type);

        Assert.Equal(0, engine.Step(index, -1, out _, out _, out _));

        Assert.Equal(enchantment, engine.State.Deck[index].Enchantment);
        Assert.Equal(2, engine.State.Deck[index].EnchantAmount);
    }

    [Fact]
    public void ThePowerPageOpensOnceTheDeckHasAPower()
    {
        var engine = At(RunConstants.EventSelfHelpBook);
        int power = GeneratedData
            .CardPools.Ironclad.ToArray()
            .First(id => GeneratedData.Cards.Get(id).Type == CardType.Power);
        engine.State.Deck.Add(new CardInstance(power, Upgraded: false));

        Assert.Equal(new[] { 0, 1, 2 }, Offered(engine));

        Assert.Equal(0, engine.Step(2, -1, out _, out _, out _));
        Assert.Equal((int)Enchantment.Swift, engine.State.PendingSelectionArg);
    }
}
