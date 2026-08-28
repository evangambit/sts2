using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The two Act 1 events that end in a fight, and the curse Neow's Bones rolls.
///
/// Both fights are reachable in Act 1 and neither is in an act's encounter pool, so the
/// 42-of-42 encounter coverage said nothing about them: they are entered from an event,
/// not from the map. An encounter that no map node can reach is exactly the kind a
/// coverage count over map pools misses.
/// </summary>
public class EventCombatTests
{
    private static int[] Offered(RunEngine engine)
    {
        var mask = new int[RunConstants.MaxActions];
        engine.WriteActionMask(mask);
        return Enumerable
            .Range(0, RunConstants.EventSkipAction)
            .Where(index => mask[index] != 0)
            .ToArray();
    }

    private static RunEngine At(int eventId, string seed = "ABCDEF")
    {
        var engine = new RunEngine();
        engine.Reset(seed);
        engine.State.EventId = eventId;
        engine.State.Phase = RunPhase.Event;
        return engine;
    }

    // ── Punch Off ────────────────────────────────────────────────────────────

    [Fact]
    public void PunchOffsSecondPageStartsARealFight()
    {
        var engine = At(RunConstants.EventPunchOff);

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));
        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(RunPhase.Combat, engine.State.Phase);
        Assert.NotNull(engine.State.ActiveCombat);
        Assert.NotEmpty(engine.State.ActiveCombat!.Enemies);
        Assert.All(engine.State.ActiveCombat.Enemies, enemy => Assert.True(enemy.MaxHp > 0));
    }

    /// <summary>
    /// The fight carries the run in with it: the deck the player built, the HP they have
    /// left, their relics and their gold. A fight that starts from a fresh slate would be
    /// a different game.
    /// </summary>
    [Fact]
    public void TheFightInheritsTheRun()
    {
        var engine = At(RunConstants.EventPunchOff);
        engine.State.PlayerHp = 41;
        engine.State.Gold = 222;
        int deck = engine.State.Deck.Count;

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));
        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        var combat = engine.State.ActiveCombat!;
        Assert.Equal(41, combat.PlayerHp);
        Assert.Equal(222, combat.PlayerGold);
        Assert.Equal(deck, combat.DrawPile.Count + combat.Hand.Count);
    }

    [Fact]
    public void TheFightIsPlayable()
    {
        var engine = At(RunConstants.EventPunchOff);
        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));
        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        var mask = new int[RunConstants.MaxActions];
        engine.WriteActionMask(mask);

        Assert.Contains(mask, slot => slot != 0);
    }

    // ── Dense Vegetation ─────────────────────────────────────────────────────

    /// <summary>
    /// Resting in the vegetation heals and then wakes something up. The heal and the fight
    /// are two steps, not one: Rest answers with a page whose only option is the fight, so
    /// the player sees the healed total before committing to it.
    /// </summary>
    [Fact]
    public void RestingInTheVegetationHealsAndThenOffersTheFight()
    {
        var engine = At(RunConstants.EventDenseVegetation);
        engine.State.PlayerHp = 30;

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

        Assert.True(engine.State.PlayerHp > 30, "resting should heal first");
        Assert.Equal(RunPhase.Event, engine.State.Phase);
        Assert.Equal(new[] { 0 }, Offered(engine));

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(RunPhase.Combat, engine.State.Phase);
        Assert.NotNull(engine.State.ActiveCombat);
        Assert.NotEmpty(engine.State.ActiveCombat!.Enemies);
    }

    [Fact]
    public void TheVegetationFightInheritsTheHealedHp()
    {
        var engine = At(RunConstants.EventDenseVegetation);
        engine.State.PlayerHp = 30;

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));
        int healed = engine.State.PlayerHp;
        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(healed, engine.State.ActiveCombat!.PlayerHp);
    }

    // ── Neow's Bones ─────────────────────────────────────────────────────────

    /// <summary>
    /// The curse is ROLLED, from the curse pool, filtered to the ten that may be
    /// generated. Ascender's Bane stood here for every such roll -- and it is one of the
    /// eight the game will never generate, so it was not even a plausible stand-in.
    /// </summary>
    [Fact]
    public void NeowsBonesRollsACurseTheGameWouldActuallyGenerate()
    {
        var generatable = GeneratedData
            .CardPools.Curse.ToArray()
            .Where(id => GeneratedData.Cards.Get(id).CanBeGeneratedByModifiers)
            .ToHashSet();
        Assert.Equal(10, generatable.Count);

        foreach (string seed in new[] { "ABCDEF", "AAB", "UNS55LCMKP", "HEADLESS1" })
        {
            var engine = new RunEngine();
            engine.Reset(seed);

            int curse = RunNonCombatEffects.RollGeneratableCurse(engine.State);

            Assert.Contains(curse, generatable);
        }
    }

    [Fact]
    public void ItNeverRollsAscendersBane()
    {
        var engine = new RunEngine();
        engine.Reset("ABCDEF");

        for (int i = 0; i < 60; i++)
        {
            Assert.NotEqual(
                RunConstants.CursePlaceholderCard,
                RunNonCombatEffects.RollGeneratableCurse(engine.State)
            );
        }
    }

    /// <summary>
    /// Taking the relic actually puts one of those curses in the deck -- the roll being
    /// right is no use if the pickup still adds the placeholder.
    /// </summary>
    [Fact]
    public void TakingNeowsBonesAddsAGeneratableCurse()
    {
        var engine = new RunEngine();
        engine.Reset("ABCDEF");
        int deck = engine.State.Deck.Count;

        RunNonCombatEffects.ApplyRelicPickup(engine.State, RunConstants.RelicNeowsBones);
        // The curse comes AFTER the two relics are claimed, not with the pickup:
        // AfterObtained awaits the RewardsSet's Offer() and adds it on the line below.
        // This used to assert it landed immediately, which is a turn of the run earlier
        // than the game does it. NeowsBonesTests walks the screen properly; here it is
        // enough to drain it.
        Assert.True(engine.State.PendingNeowsBonesCurse);
        Assert.Equal(deck, engine.State.Deck.Count);
        while (RunRewardGenerator.HasPendingRewards(engine.State))
        {
            Assert.True(RunRewardGenerator.ClaimNextReward(engine.State));
        }

        var added = engine
            .State.Deck.Skip(deck)
            .Select(card => GeneratedData.Cards.Get(card.DefId))
            .Where(def => def.Type == CardType.Curse)
            .ToList();
        Assert.Single(added);
        Assert.True(added[0].CanBeGeneratedByModifiers, $"{added[0].Name} is never generated");
    }

    /// <summary>The roll varies with the seed, so it is a roll and not a constant.</summary>
    [Fact]
    public void TheCurseDependsOnTheSeed()
    {
        var seen = new HashSet<int>();
        foreach (string seed in new[] { "ABCDEF", "AAB", "UNS55LCMKP", "HEADLESS1", "SOAK00001" })
        {
            var engine = new RunEngine();
            engine.Reset(seed);
            seen.Add(RunNonCombatEffects.RollGeneratableCurse(engine.State));
        }

        Assert.True(seen.Count > 1, "every seed rolled the same curse");
    }
}
