using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// CardFactory.FilterForPlayerCount drops every MultiplayerOnly card from a pool before
/// anything is rolled from it, and IRunState.CardMultiplayerConstraint reports
/// SingleplayerOnly whenever Players.Count is one. The emulator only ever runs solo, so
/// the 21 cards flagged in CardDef.MultiplayerOnly can never be offered — the pools
/// themselves are copies of the character's full card pool and still list them.
/// </summary>
public class MultiplayerCardFilterTests
{
    /// <summary>Read off the card data rather than a list written out here.</summary>
    private static int[] MultiplayerCards =>
        [
            .. GeneratedData
                .Cards.All.ToArray()
                .Where(def => def.MultiplayerOnly)
                .Select(def => def.Id),
        ];

    private static bool IsMultiplayerOnly(int cardId) => MultiplayerCards.Contains(cardId);

    [Fact]
    public void TheDataStillCarriesTheMultiplayerCards()
    {
        // 21 in the decompiled source; if this moves, the extractor's regex is the suspect.
        Assert.Equal(21, MultiplayerCards.Length);
    }

    /// <summary>
    /// Both reward pools list a multiplayer card — Tank and Demonic Shield for the
    /// Ironclad — which is exactly why the filter cannot live in the pool literals.
    /// </summary>
    [Fact]
    public void ThePoolsThemselvesStillListThem()
    {
        Assert.Contains(RunRewardGenerator.IroncladRewardPool.ToArray(), IsMultiplayerOnly);
        Assert.Contains(RunRewardGenerator.ColorlessRewardPool.ToArray(), IsMultiplayerOnly);
    }

    [Fact]
    public void NoCardRewardEverOffersAMultiplayerCard()
    {
        var offered = new HashSet<int>();
        for (int seed = 0; seed < 40; seed++)
        {
            var engine = new RunEngine();
            engine.Reset(seed.ToString());
            engine.State.CurrentNodeType = RunConstants.NodeNormal;
            for (int floor = 0; floor < 8; floor++)
            {
                engine.State.Floor = floor;
                RunRewardGenerator.EnterCardReward(engine.State);
                offered.UnionWith(engine.State.RewardCards);
            }
        }

        Assert.NotEmpty(offered);
        Assert.DoesNotContain(offered, IsMultiplayerOnly);
    }

    [Fact]
    public void TransformingNeverProducesAMultiplayerCard()
    {
        var produced = new HashSet<int>();
        for (int seed = 0; seed < 40; seed++)
        {
            var engine = new RunEngine();
            engine.Reset(seed.ToString());
            engine.State.Deck = [new CardInstance(472, false)];
            RunNonCombatEffects.TransformCardAt(engine.State, 0, engine.State.PlayerRng.Rewards);
            produced.UnionWith(engine.State.Deck.Select(card => card.DefId));
        }

        Assert.NotEmpty(produced);
        Assert.DoesNotContain(produced, IsMultiplayerOnly);
    }
}
