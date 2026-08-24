using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Winged Boots' free travel, read off MegaCrit.Sts2.Core.Models.Relics/WingedBoots.cs and
/// MegaCrit.Sts2.Core.Map/MapTravel.cs.
///
/// <para>
/// <c>MapTravel.GetTravelablePointsFrom</c> answers with the WHOLE of the next row while
/// <c>Hook.ShouldAllowFreeTravel</c> holds, and the boots hold it until three charges are
/// spent. A charge goes only on a move the map does not already draw an edge for, which is
/// why a run can carry the relic for an act without moving its counter.
/// </para>
/// </summary>
public class WingedBootsTests
{
    /// <summary>
    /// Three nodes on row 1, and the run stands on the only one of them the current node
    /// connects to. Without the boots that is the whole of the offer.
    /// </summary>
    private static RunState RunOnARowOfThree()
    {
        var state = new RunState { CurrentMapCoord = (3, 0) };
        AddNode(state, 3, 0, RunConstants.NodeNone, children: [(3, 1)]);
        AddNode(state, 1, 1, RunConstants.NodeShop);
        AddNode(state, 3, 1, RunConstants.NodeNormal);
        AddNode(state, 5, 1, RunConstants.NodeEvent);
        state.NormalEncounterSequence = [RunConstants.CorpseSlugsEncounterId];
        state.EliteEncounterSequence = [RunConstants.OvergrowthEliteEncounters[0]];
        return state;
    }

    private static void AddNode(
        RunState state,
        int col,
        int row,
        int nodeType,
        (int Col, int Row)[]? children = null
    )
    {
        var node = new RunMapNode(col, row) { NodeType = nodeType };
        foreach (var child in children ?? [])
        {
            node.Children.Add(child);
        }

        state.MapNodes[(col, row)] = node;
    }

    private static (int Col, int Row)?[] OptionsOf(RunState state)
    {
        RunMapGenerator.RefreshMapOptions(state);
        return state.MapOptionCoords;
    }

    [Fact]
    public void WithoutTheBootsOnlyTheChildrenAreOffered()
    {
        var state = RunOnARowOfThree();

        Assert.Equal<(int, int)?>((3, 1), OptionsOf(state)[0]);
        Assert.Null(state.MapOptionCoords[1]);
    }

    [Fact]
    public void TheBootsOfferTheWholeRowInColumnOrder()
    {
        var state = RunOnARowOfThree();
        state.Relics.Add(new RelicInstance(RunConstants.RelicWingedBoots));

        var options = OptionsOf(state);

        Assert.Equal<(int, int)?>((1, 1), options[0]);
        Assert.Equal<(int, int)?>((3, 1), options[1]);
        Assert.Equal<(int, int)?>((5, 1), options[2]);
        Assert.Null(options[3]);
    }

    /// <summary>
    /// A row can be as wide as the map, which is what the four-slot choice arrays could
    /// not hold: the run stalled rather than offering the options past the fourth.
    /// </summary>
    [Fact]
    public void AFullRowOfSevenFitsInTheOffer()
    {
        var state = new RunState { CurrentMapCoord = (3, 4) };
        AddNode(state, 3, 4, RunConstants.NodeNormal, children: [(3, 5)]);
        for (int col = 0; col < RunConstants.MapWidth; col++)
        {
            AddNode(state, col, 5, RunConstants.NodeNormal);
        }

        state.NormalEncounterSequence = [RunConstants.CorpseSlugsEncounterId];
        state.Relics.Add(new RelicInstance(RunConstants.RelicWingedBoots));

        var options = OptionsOf(state);

        Assert.Equal(
            Enumerable.Range(0, RunConstants.MapWidth).Select(col => ((int, int)?)(col, 5)),
            options
        );
    }

    [Fact]
    public void MovingAlongAnEdgeSpendsNoCharge()
    {
        var state = RunOnARowOfThree();
        state.Relics.Add(new RelicInstance(RunConstants.RelicWingedBoots));
        RunMapGenerator.RefreshMapOptions(state);

        Assert.True(RunMapGenerator.ChooseMapNode(state, 1, out _, out _));

        Assert.Equal((3, 1), state.CurrentMapCoord);
        Assert.Equal(0, state.Relics[0].Counter);
    }

    [Fact]
    public void MovingToANonChildSpendsACharge()
    {
        var state = RunOnARowOfThree();
        state.Relics.Add(new RelicInstance(RunConstants.RelicWingedBoots));
        RunMapGenerator.RefreshMapOptions(state);

        Assert.True(RunMapGenerator.ChooseMapNode(state, 0, out int nodeType, out _));

        Assert.Equal(RunConstants.NodeShop, nodeType);
        Assert.Equal((1, 1), state.CurrentMapCoord);
        Assert.Equal(1, state.Relics[0].Counter);
    }

    [Fact]
    public void TheThirdChargeIsTheLast()
    {
        var state = RunOnARowOfThree();
        state.Relics.Add(
            new RelicInstance(RunConstants.RelicWingedBoots, RunConstants.WingedBootsTravels)
        );

        Assert.False(RunMapGenerator.AllowsFreeTravel(state));

        var options = OptionsOf(state);
        Assert.Equal<(int, int)?>((3, 1), options[0]);
        Assert.Null(options[1]);
    }

    /// <summary>
    /// The boss is not in the game's <c>Grid</c>, so <c>GetPointsInRow</c> never returns
    /// it: from the last grid row <c>NMapScreen</c> makes the boss travelable outright
    /// instead. Offering the row there would offer nothing at all.
    /// </summary>
    [Fact]
    public void TheBossIsStillReachableFromTheFinalRestRow()
    {
        var state = new RunState { CurrentMapCoord = (3, RunConstants.MapFinalRestRow) };
        AddNode(
            state,
            3,
            RunConstants.MapFinalRestRow,
            RunConstants.NodeRest,
            children: [(3, RunConstants.MapBossRow)]
        );
        AddNode(state, 3, RunConstants.MapBossRow, RunConstants.NodeBoss);
        state.BossEncounterId = RunConstants.OvergrowthBossEncounters[0];
        state.Relics.Add(new RelicInstance(RunConstants.RelicWingedBoots));

        var options = OptionsOf(state);

        Assert.Equal<(int, int)?>((3, RunConstants.MapBossRow), options[0]);
        Assert.Equal(RunConstants.NodeBoss, state.MapNodeTypes[0]);
        Assert.Equal(state.BossEncounterId, state.MapChoices[0]);
    }
}
