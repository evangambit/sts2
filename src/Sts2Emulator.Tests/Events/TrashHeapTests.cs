using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The Trash Heap: HP for a relic, or nothing for gold and a card.
///
/// The emulator had the two options' costs and prizes crossed -- Dive In paid the HP AND
/// took the gold, and Grab handed over a relic rolled from the reward pool. Both prizes
/// actually come off the event's own literal tables: five relics and ten cards, picked
/// with <c>Rng.NextItem</c> on the event's own stream.
/// </summary>
public class TrashHeapTests
{
    private static readonly string[] Seeds = ["ABCDEF", "AAB", "UNS55LCMKP", "HEADLESS1"];

    private static RunEngine AtTheHeap(string seed = "ABCDEF")
    {
        var engine = new RunEngine();
        engine.Reset(seed);
        engine.State.EventId = RunConstants.EventTrashHeap;
        engine.State.Phase = RunPhase.Event;
        return engine;
    }

    [Fact]
    public void DivingInCostsEightHpAndPaysARelic()
    {
        var engine = AtTheHeap();
        int hp = engine.State.PlayerHp;
        int gold = engine.State.Gold;
        int deck = engine.State.Deck.Count;

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(hp - 8, engine.State.PlayerHp);
        Assert.Equal(gold, engine.State.Gold);
        Assert.Equal(deck, engine.State.Deck.Count);
        Assert.Equal(2, engine.State.Relics.Count);
    }

    [Fact]
    public void GrabbingPaysAHundredGoldAndACardAndCostsNothing()
    {
        var engine = AtTheHeap();
        int hp = engine.State.PlayerHp;
        int gold = engine.State.Gold;
        int deck = engine.State.Deck.Count;

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

        Assert.Equal(hp, engine.State.PlayerHp);
        Assert.Equal(gold + 100, engine.State.Gold);
        Assert.Equal(deck + 1, engine.State.Deck.Count);
        Assert.Single(engine.State.Relics);
    }

    /// <summary>
    /// Both prizes come off the event's own tables, so whatever the seed, the relic is
    /// one of five and the card is one of ten. A prize from outside these lists means the
    /// reward pool has crept back in.
    /// </summary>
    [Fact]
    public void ThePrizesOnlyEverComeFromTheEventsOwnTables()
    {
        var relics = new[]
        {
            "DarkstonePeriapt",
            "DreamCatcher",
            "HandDrill",
            "MawBank",
            "TheBoot",
        }
            .Select(RunNonCombatEffects.NamedRelic)
            .ToHashSet();
        var cards = new[]
        {
            "Caltrops",
            "Clash",
            "Distraction",
            "DualWield",
            "Entrench",
            "HelloWorld",
            "Outmaneuver",
            "Rebound",
            "RipAndTear",
            "Stack",
        }
            .Select(name => GeneratedData.Cards.FindId(name)!.Value)
            .ToHashSet();

        foreach (string seed in Seeds)
        {
            var dive = AtTheHeap(seed);
            Assert.Equal(0, dive.Step(0, -1, out _, out _, out _));
            Assert.Contains(dive.State.Relics[^1].DefId, relics);

            var grab = AtTheHeap(seed);
            Assert.Equal(0, grab.Step(1, -1, out _, out _, out _));
            Assert.Contains(grab.State.Deck[^1].DefId, cards);
        }
    }

    /// <summary>
    /// Each option draws once, from the event's own stream, so the prize is a function of
    /// the seed alone -- and both options draw the SAME index, because neither is taken
    /// before the other.
    /// </summary>
    [Fact]
    public void ThePrizeIsAFunctionOfTheSeed()
    {
        foreach (string seed in Seeds)
        {
            int first = RunNonCombatEffects.TrashHeapRelic(AtTheHeap(seed).State);
            Assert.Equal(first, RunNonCombatEffects.TrashHeapRelic(AtTheHeap(seed).State));
        }

        Assert.NotEqual(
            RunNonCombatEffects.TrashHeapCard(AtTheHeap("ABCDEF").State),
            RunNonCombatEffects.TrashHeapCard(AtTheHeap("AAB").State)
        );
    }
}
