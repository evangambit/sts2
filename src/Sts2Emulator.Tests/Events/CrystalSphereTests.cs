using System.Text.Json;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The Crystal Sphere's minigame, checked against boards divined in the game itself.
///
/// Each fixture is one run of the sphere: the board as it opened, the item footprints the
/// game exposed after every divination, and the reward screen the last one led to. All of
/// it comes off the event's own stream, so a board that agrees cell-for-cell is evidence
/// that the fifteen placement draws landed in the right order -- and a reward screen that
/// agrees is evidence the two population passes did too.
///
/// Captured by <c>scripts/capture_crystal_sphere.py</c>; expected values are the game's.
/// </summary>
public class CrystalSphereTests
{
    private static readonly string FixtureDir = Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        "..",
        "tests",
        "fixtures",
        "events"
    );

    public static TheoryData<string> Fixtures()
    {
        var data = new TheoryData<string>();
        foreach (
            string path in Directory.GetFiles(FixtureDir, "CrystalSphere-sphere-*.json").Order()
        )
        {
            data.Add(Path.GetFileName(path));
        }

        return data;
    }

    /// <summary>The mod's item class names, as the emulator's kinds.</summary>
    private static CrystalSphereItemKind KindFor(string itemType) =>
        itemType switch
        {
            "CrystalSphereCardReward" => CrystalSphereItemKind.CardReward,
            "CrystalSphereCurse" => CrystalSphereItemKind.Curse,
            "CrystalSphereGold" => CrystalSphereItemKind.Gold,
            "CrystalSpherePotion" => CrystalSphereItemKind.Potion,
            "CrystalSphereRelic" => CrystalSphereItemKind.Relic,
            _ => throw new ArgumentOutOfRangeException(nameof(itemType), itemType, "Unknown item"),
        };

    /// <summary>
    /// The items the game would list: every one with at least one cell out of the fog,
    /// whether or not it has been won, described the way the mod describes it.
    /// </summary>
    private static List<string> TouchedItems(CrystalSphereGame game) =>
        game
            .Items.Where(item =>
                Enumerable
                    .Range(0, item.Width)
                    .Any(dx =>
                        Enumerable
                            .Range(0, item.Height)
                            .Any(dy => !game.IsHidden(item.X + dx, item.Y + dy))
                    )
            )
            .Select(item => $"{item.Kind} {item.X},{item.Y} {item.Width}x{item.Height}")
            .Order(StringComparer.Ordinal)
            .ToList();

    private static List<string> ExpectedItems(JsonElement board) =>
        board
            .GetProperty("items")
            .EnumerateArray()
            .Select(item =>
                $"{KindFor(item.GetProperty("item_type").GetString()!)} "
                + $"{item.GetProperty("x").GetInt32()},{item.GetProperty("y").GetInt32()} "
                + $"{item.GetProperty("width").GetInt32()}x{item.GetProperty("height").GetInt32()}"
            )
            .Order(StringComparer.Ordinal)
            .ToList();

    private static List<string> ClearedCells(CrystalSphereGame game) =>
        Enumerable
            .Range(0, CrystalSphereGame.CellCount)
            .Where(cell => !game.Hidden[cell])
            .Select(cell => $"{cell / CrystalSphereGame.Size},{cell % CrystalSphereGame.Size}")
            .Order(StringComparer.Ordinal)
            .ToList();

    private static List<string> ExpectedCleared(JsonElement board) =>
        board
            .GetProperty("cleared")
            .EnumerateArray()
            .Select(cell => $"{cell[0].GetInt32()},{cell[1].GetInt32()}")
            .Order(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// A name reduced to letters and digits, so the game's "Fairy in a Bottle" and the
    /// emulator's "FairyInABottle" compare equal without either side having to guess where
    /// the other puts its word breaks.
    /// </summary>
    private static string NameKey(string name) =>
        string.Concat(name.Where(char.IsLetterOrDigit)).ToUpperInvariant();

    /// <summary>The reward screen as the mod lists it: type first, then what it is worth.</summary>
    private static List<string> RewardScreen(RunState state)
    {
        var items = new List<string>();
        foreach (
            int gold in (state.RewardGold != 0 ? new[] { state.RewardGold } : []).Concat(
                state.PendingGoldRewards
            )
        )
        {
            items.Add($"gold {gold}");
        }

        foreach (
            int potion in (state.RewardPotion != 0 ? new[] { state.RewardPotion } : []).Concat(
                state.PendingPotionRewards
            )
        )
        {
            items.Add($"potion {NameKey(GeneratedData.Potions.Get(potion).Name)}");
        }

        if (state.RelicReward != 0)
        {
            items.Add($"relic {NameKey(GeneratedData.Relics.Get(state.RelicReward).Name)}");
        }

        for (int i = 0; i < (state.RewardCardPending ? 1 : 0) + state.PendingCardOffers.Count; i++)
        {
            items.Add("card");
        }

        return items;
    }

    /// <summary>
    /// The card offers waiting on the screen, in order: the one showing, then the queue.
    /// </summary>
    private static List<List<string>> OfferedCards(RunState state)
    {
        var offers = new List<List<string>>();
        if (state.RewardCardPending)
        {
            offers.Add(
                state
                    .RewardCards.Where(id => id != 0)
                    .Select(id => GeneratedData.Cards.Get(id).Entry)
                    .ToList()
            );
        }

        offers.AddRange(
            state.PendingCardOffers.Select(offer =>
                offer.Select(id => GeneratedData.Cards.Get(id).Entry).ToList()
            )
        );
        return offers;
    }

    private static List<string> ExpectedRewards(JsonElement rewards) =>
        rewards
            .EnumerateArray()
            .Select(item =>
                item.GetProperty("type").GetString() switch
                {
                    "gold" => $"gold {item.GetProperty("gold_amount").GetInt32()}",
                    "potion" => $"potion {NameKey(item.GetProperty("description").GetString()!)}",
                    "relic" => $"relic {NameKey(item.GetProperty("description").GetString()!)}",
                    _ => "card",
                }
            )
            .ToList();

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void DiviningTheBoardMatchesTheGame(string fixtureName)
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(FixtureDir, fixtureName))
        );
        var root = document.RootElement;

        var engine = new RunEngine();
        engine.Reset(root.GetProperty("seed").GetString()!);
        engine.State.EventId = RunConstants.EventCrystalSphere;
        engine.State.Phase = RunPhase.Event;

        var before = root.GetProperty("before").GetProperty("player");
        Assert.Equal(before.GetProperty("gold").GetInt32(), engine.State.Gold);

        int option = root.GetProperty("option").GetInt32();
        Assert.Equal(0, engine.Step(option, -1, out _, out _, out _));
        Assert.Equal(RunPhase.CrystalSphere, engine.State.Phase);

        var game = engine.State.CrystalSphere!;
        Assert.True(game.PlacedAllItems);
        Assert.Equal(ExpectedCleared(root.GetProperty("opening_board")), ClearedCells(game));

        foreach (var step in root.GetProperty("clicks").EnumerateArray())
        {
            int x = step.GetProperty("x").GetInt32();
            int y = step.GetProperty("y").GetInt32();
            bool small = step.GetProperty("tool").GetString() == "small";
            int action = (small ? RunConstants.CrystalSphereSmallToolAction : 0) + x * 11 + y;
            Assert.Equal(0, engine.Step(action, -1, out _, out _, out _));

            if (step.GetProperty("board").ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            var board = step.GetProperty("board");
            Assert.Equal(ExpectedCleared(board), ClearedCells(game));
            Assert.Equal(ExpectedItems(board), TouchedItems(game));
        }

        Assert.Equal("rewards", root.GetProperty("after_state_type").GetString());
        Assert.Equal(RunPhase.RelicReward, engine.State.Phase);
        Assert.Equal(ExpectedRewards(root.GetProperty("rewards")), RewardScreen(engine.State));
        // The screen only says "add a card", so the three behind each offer are what pins
        // where the card rolls landed in the stream -- the reward list alone cannot.
        Assert.Equal(
            root.GetProperty("card_offers")
                .EnumerateArray()
                .Select(offer => offer.EnumerateArray().Select(card => card.GetString()!).ToList())
                .ToList(),
            OfferedCards(engine.State)
        );

        var after = root.GetProperty("after").GetProperty("player");
        Assert.Equal(after.GetProperty("gold").GetInt32(), engine.State.Gold);
        Assert.Equal(after.GetProperty("deck").GetArrayLength(), engine.State.Deck.Count);
    }

    private static RunEngine OpenedSphere(string seed = "ABCDEF", int option = 1)
    {
        var engine = new RunEngine();
        engine.Reset(seed);
        engine.State.EventId = RunConstants.EventCrystalSphere;
        engine.State.Phase = RunPhase.Event;
        Assert.Equal(0, engine.Step(option, -1, out _, out _, out _));
        return engine;
    }

    /// <summary>
    /// <c>CrystalSphere.IsAllowed</c> wants <c>CurrentActIndex > 0</c> as well as 100 gold,
    /// so the sphere is an Act 2 sight however rich an Act 1 run gets.
    /// </summary>
    [Fact]
    public void TheSphereNeverTurnsUpInActOne()
    {
        var engine = new RunEngine();
        engine.Reset("ABCDEF");
        engine.State.Gold = 500;

        engine.State.Act = RunConstants.ActOvergrowth;
        Assert.False(
            RunNonCombatEffects.IsEventAllowed(engine.State, RunConstants.EventCrystalSphere)
        );

        engine.State.Act = RunConstants.ActUnderdocks;
        Assert.True(
            RunNonCombatEffects.IsEventAllowed(engine.State, RunConstants.EventCrystalSphere)
        );
    }

    /// <summary>
    /// The price is rolled, not flat: 50 plus <c>NextInt(1, 50)</c> off the event's own
    /// stream. A flat 100 stood here, which is both the wrong number and the wrong draw --
    /// the board is laid out from the same stream immediately after.
    /// </summary>
    [Fact]
    public void TheCostIsRolledOffTheEventStream()
    {
        var costs = new HashSet<int>();
        foreach (string seed in new[] { "ABCDEF", "QS2GYXRKWN", "SPIRE", "ZZZZZZ" })
        {
            var engine = new RunEngine();
            engine.Reset(seed);
            int cost = RunNonCombatEffects.CrystalSphereCost(engine.State);
            Assert.InRange(cost, 51, 99);
            costs.Add(cost);
        }

        Assert.True(costs.Count > 1, "The cost is the same on every seed, so it is not rolled");
    }

    /// <summary>
    /// Both options open the same board. Neither draws anything between CalculateVars and
    /// the constructor -- one spends gold, the other adds a Debt -- so the layout is a
    /// property of the seed, not of the choice.
    /// </summary>
    [Fact]
    public void BothOptionsOpenTheSameBoard()
    {
        var uncover = OpenedSphere(option: 0).State.CrystalSphere!;
        var paymentPlan = OpenedSphere(option: 1).State.CrystalSphere!;

        Assert.Equal(3, uncover.Divinations);
        Assert.Equal(6, paymentPlan.Divinations);
        Assert.Equal(
            uncover.Items.Select(item => (item.Kind, item.X, item.Y)),
            paymentPlan.Items.Select(item => (item.Kind, item.X, item.Y))
        );
    }

    /// <summary>
    /// The mask carries a cell twice, once per tool, and leaves out ground a divination
    /// would waste itself on. The corners open clear out to two steps, so a small tool
    /// aimed inside one is an action the mask must not offer -- while the big tool at the
    /// same cell still reaches fog and is offered.
    /// </summary>
    [Fact]
    public void TheMaskCoversBothToolsAndSkipsClearGround()
    {
        var engine = OpenedSphere();
        var mask = new int[RunConstants.MaxActions];
        engine.WriteActionMask(mask);

        // The tip of the top-left corner's diamond: clear itself, but with fog beside it.
        int edge = 2 * 11 + 0;
        Assert.Equal(0, mask[RunConstants.CrystalSphereSmallToolAction + edge]);
        Assert.Equal(1, mask[edge]);

        // Deep inside the diamond, where even the big tool would lift nothing.
        int inside = 0 * 11 + 0;
        Assert.Equal(0, mask[inside]);
        Assert.Equal(0, mask[RunConstants.CrystalSphereSmallToolAction + inside]);

        int middle = 5 * 11 + 5;
        Assert.Equal(1, mask[middle]);
        Assert.Equal(1, mask[RunConstants.CrystalSphereSmallToolAction + middle]);

        Assert.Equal(
            -1,
            engine.Step(RunConstants.CrystalSphereSmallToolAction + edge, -1, out _, out _, out _)
        );
        Assert.Equal(6, engine.State.CrystalSphere!.Divinations);
    }

    /// <summary>A small divination clears one cell; a big one clears its nine.</summary>
    [Fact]
    public void TheToolDecidesHowMuchFogLifts()
    {
        var small = OpenedSphere();
        Assert.Equal(
            0,
            small.Step(
                RunConstants.CrystalSphereSmallToolAction + 5 * 11 + 5,
                -1,
                out _,
                out _,
                out _
            )
        );
        Assert.Equal(24 + 1, ClearedCells(small.State.CrystalSphere!).Count);

        var big = OpenedSphere();
        Assert.Equal(0, big.Step(5 * 11 + 5, -1, out _, out _, out _));
        Assert.Equal(24 + 9, ClearedCells(big.State.CrystalSphere!).Count);
    }

    /// <summary>
    /// A forked run gets its own board. Without this a search that divines in one branch
    /// lifts the fog in every other.
    /// </summary>
    [Fact]
    public void CloningForksTheBoard()
    {
        var engine = OpenedSphere();
        var copy = engine.Clone();

        Assert.Equal(0, engine.Step(5 * 11 + 5, -1, out _, out _, out _));

        Assert.Equal(6, copy.State.CrystalSphere!.Divinations);
        Assert.Equal(24, ClearedCells(copy.State.CrystalSphere).Count);
        Assert.Equal(5, engine.State.CrystalSphere!.Divinations);
    }

    /// <summary>
    /// The curse is the one thing that lands the moment it is uncovered rather than on the
    /// reward screen: <c>CrystalSphereCurse.RevealItem</c> puts a Doubt straight in the deck.
    /// </summary>
    [Fact]
    public void UncoveringTheCursePutsADoubtInTheDeckAtOnce()
    {
        var engine = OpenedSphere();
        int before = engine.State.Deck.Count;
        // The curse sits at (9,3) on this seed and a big divination at (9,4) covers it.
        Assert.Equal(0, engine.Step(9 * 11 + 4, -1, out _, out _, out _));

        Assert.Equal(RunPhase.CrystalSphere, engine.State.Phase);
        Assert.Equal(before + 1, engine.State.Deck.Count);
        Assert.Contains(
            engine.State.Deck,
            card => GeneratedData.Cards.Get(card.DefId).Entry == "DOUBT"
        );
    }

    /// <summary>
    /// A resampled fork gets a different board under the fog, but the same one above it: a
    /// search must not be able to read where the relic is, and must not forget what the
    /// player has already been shown.
    /// </summary>
    [Fact]
    public void ResamplingMovesOnlyWhatIsStillHidden()
    {
        var engine = OpenedSphere();
        // One divination, so something is on show and most is not.
        Assert.Equal(0, engine.Step(4 * 11 + 2, -1, out _, out _, out _));
        var original = engine.State.CrystalSphere!;
        var shown = TouchedItems(original);

        var copy = engine.Clone(resampleSeed: 12345).State.CrystalSphere!;

        Assert.Equal(ClearedCells(original), ClearedCells(copy));
        Assert.Equal(shown, TouchedItems(copy));
        Assert.NotEqual(
            original.Items.Select(item => (item.Kind, item.X, item.Y)).ToList(),
            copy.Items.Select(item => (item.Kind, item.X, item.Y)).ToList()
        );

        // Nothing landed on ground the player can already see, and nothing overlaps.
        var covered = new HashSet<int>();
        foreach (var item in copy.Items)
        {
            for (int dx = 0; dx < item.Width; dx++)
            {
                for (int dy = 0; dy < item.Height; dy++)
                {
                    Assert.True(covered.Add(CrystalSphereGame.Index(item.X + dx, item.Y + dy)));
                }
            }
        }
    }
}
