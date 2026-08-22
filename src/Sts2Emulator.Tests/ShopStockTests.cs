using System.Text.Json;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// What the merchant has in stock, checked against what the merchant had in stock.
///
/// A shop is fourteen slots -- seven cards, three relics, three potions and the removal
/// service -- and every one is rolled: which card, at which rarity, which relic, what
/// each costs, and which single slot is discounted. That makes it the densest single
/// readout in the run layer, and it had no fixture at all: the only shop assertion in the
/// suite was that entering one advanced the Shops stream.
///
/// The fixture is a live capture taken through the mod's debug_start_shop, so every
/// expected value here is the game's own.
/// </summary>
public class ShopStockTests
{
    private static JsonElement Fixture()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "tests",
            "fixtures",
            "shop",
            "ABCDEF-a8-floor1.json"
        );
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
    }

    private static RunState ShopAtFloorOne(JsonElement fixture)
    {
        var engine = new RunEngine();
        engine.Reset(fixture.GetProperty("seed").GetString()!);
        Assert.Equal(fixture.GetProperty("floor").GetInt32(), engine.State.Floor);
        RunRewardGenerator.EnterShop(engine.State);
        return engine.State;
    }

    /// <summary>
    /// The game names things in SCREAMING_SNAKE (its ModelId.Entry); relic and potion
    /// definitions here carry the PascalCase class name instead. Cards already carry
    /// Entry, so only these two need converting.
    /// </summary>
    private static string Slug(string pascalCase)
    {
        var slug = new System.Text.StringBuilder();
        for (int i = 0; i < pascalCase.Length; i++)
        {
            if (i > 0 && char.IsUpper(pascalCase[i]) && !char.IsUpper(pascalCase[i - 1]))
            {
                slug.Append('_');
            }

            slug.Append(char.ToUpperInvariant(pascalCase[i]));
        }

        return slug.ToString();
    }

    private static string[] Ids(JsonElement fixture, string category) =>
        fixture
            .GetProperty("stock")
            .EnumerateArray()
            .Where(item => item.GetProperty("category").GetString() == category)
            .Select(item => item.GetProperty("id").GetString() ?? "")
            .ToArray();

    private static int[] Prices(JsonElement fixture, string category) =>
        fixture
            .GetProperty("stock")
            .EnumerateArray()
            .Where(item => item.GetProperty("category").GetString() == category)
            .Select(item => item.GetProperty("price").GetInt32())
            .ToArray();

    [Fact]
    public void TheCardsOnOfferAreTheCardsTheMerchantHad()
    {
        var fixture = Fixture();
        var state = ShopAtFloorOne(fixture);

        Assert.Equal(
            Ids(fixture, "card"),
            state.ShopCards.Select(id => GeneratedData.Cards.Get(id).Entry).ToArray()
        );
    }

    [Fact]
    public void TheRelicsOnOfferAreTheRelicsTheMerchantHad()
    {
        var fixture = Fixture();
        var state = ShopAtFloorOne(fixture);

        Assert.Equal(
            Ids(fixture, "relic"),
            state.ShopRelics.Select(id => Slug(GeneratedData.Relics.Get(id).Name)).ToArray()
        );
    }

    [Fact]
    public void ThePotionsOnOfferAreThePotionsTheMerchantHad()
    {
        var fixture = Fixture();
        var state = ShopAtFloorOne(fixture);

        Assert.Equal(
            Ids(fixture, "potion"),
            state.ShopPotions.Select(id => Slug(GeneratedData.Potions.Get(id).Name)).ToArray()
        );
    }

    [Fact]
    public void EverythingCostsWhatTheMerchantAskedForIt()
    {
        var fixture = Fixture();
        var state = ShopAtFloorOne(fixture);

        Assert.Equal(Prices(fixture, "card"), state.ShopCosts[..7]);
        Assert.Equal(Prices(fixture, "relic"), state.ShopCosts[7..10]);
        Assert.Equal(
            Prices(fixture, "card_removal").Single(),
            state.ShopCosts[RunConstants.ShopRemoveAction]
        );
    }

    [Fact]
    public void TheDiscountedSlotIsTheOneTheMerchantDiscounted()
    {
        // Exactly one card slot is on sale, and which one is its own roll.
        var fixture = Fixture();
        var state = ShopAtFloorOne(fixture);
        var sale = fixture
            .GetProperty("stock")
            .EnumerateArray()
            .Where(item => item.GetProperty("on_sale").ValueKind == JsonValueKind.True)
            .Select(item => item.GetProperty("index").GetInt32())
            .ToArray();

        int discounted = Assert.Single(sale);
        // A sale slot is half price, so it undercuts what that card would otherwise ask.
        int fullPrice = RunRewardGenerator.ShopCardCost(
            state.ShopCards[discounted],
            colorless: false,
            new Core.Rng.GameRng(0)
        );
        Assert.True(
            state.ShopCosts[discounted] < fullPrice,
            $"slot {discounted} was on sale in the game but is not discounted here"
        );
    }
}
