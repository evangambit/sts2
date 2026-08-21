using Sts2Emulator.Core.Rng;

namespace Sts2Emulator.Core.Run;

public static class RunMapGenerator
{
    public static void SelectActAndGenerateRooms(RunState state)
    {
        // Act 1 is picked by StartRunLobby.BeginRunLocally from the *unlocked* acts for
        // index 0, via rng.NextItem on a dedicated "act_selection" stream — not a coin
        // flip on the raw seed, which is what this used to do.
        //
        // Caveat: like boss discovery, this is not a pure function of the seed. The
        // candidate list is whatever the profile has unlocked, and the game
        // force-selects an unlocked-but-undiscovered alt act instead of rolling. We
        // model the mature-profile case (both Act-1 options unlocked and discovered),
        // which is what the differential captures are taken on. A profile without
        // Underdocks unlocked would always get Overgrowth.
        var actRng = new GameRng(state.Rng.Seed, "act_selection");
        bool underdocks = actRng.NextInt(0, 2) == 1;
        state.Act = underdocks ? RunConstants.ActUnderdocks : RunConstants.ActOvergrowth;
        state.EventSequence = GenerateEventSequence(state, underdocks);
        state.EventSequenceIndex = 0;

        var upFront = state.Rng.UpFront;
        for (int i = 0; i < 202; i++)
        {
            upFront.NextDouble();
        }

        for (int i = 0; i < (underdocks ? 57 : 60); i++)
        {
            upFront.NextInt((underdocks ? 57 : 60) + 1);
        }

        int[] weakPool = (
            underdocks
                ? RunConstants.UnderdocksWeakEncounters
                : RunConstants.OvergrowthWeakEncounters
        ).ToArray();
        int[] normalPool = (
            underdocks
                ? RunConstants.UnderdocksNormalEncounters
                : RunConstants.OvergrowthNormalEncounters
        ).ToArray();
        int[] elitePool = (
            underdocks
                ? RunConstants.UnderdocksEliteEncounters
                : RunConstants.OvergrowthEliteEncounters
        ).ToArray();
        int[] bossPool = (
            underdocks
                ? RunConstants.UnderdocksBossEncounters
                : RunConstants.OvergrowthBossEncounters
        ).ToArray();

        var normalSequence = new List<int>();
        var weakBag = weakPool.ToList();
        int? last = null;
        for (int i = 0; i < 3; i++)
        {
            int enc = GrabWithoutRepeatingTags(weakBag, last, upFront);
            normalSequence.Add(enc);
            last = enc;
        }

        var normalBag = new List<int>();
        for (int i = 0; i < 12; i++)
        {
            if (normalBag.Count == 0)
            {
                normalBag = normalPool.ToList();
            }

            int enc = GrabWithoutRepeatingTags(normalBag, last, upFront);
            normalSequence.Add(enc);
            last = enc;
        }
        state.NormalEncounterSequence = normalSequence.ToArray();

        var eliteSequence = new List<int>();
        var eliteBag = new List<int>();
        // Elites go through the same AddWithoutRepeatingTags path as normals, and
        // track their own "last" — the game passes _rooms.eliteEncounters, so the
        // no-repeat rule looks at the previous *elite*, not the previous normal.
        int? lastElite = null;
        for (int i = 0; i < 15; i++)
        {
            if (eliteBag.Count == 0)
            {
                eliteBag = elitePool.ToList();
            }

            int enc = GrabWithoutRepeatingTags(eliteBag, lastElite, upFront);
            eliteSequence.Add(enc);
            lastElite = enc;
        }
        state.EliteEncounterSequence = eliteSequence.ToArray();
        state.BossEncounterId = bossPool[(int)(upFront.NextDouble() * bossPool.Length)];
    }

    private static int[] GenerateEventSequence(RunState state, bool underdocks)
    {
        int[] eventPool = underdocks
            ?
            [
                RunConstants.EventAbyssalBaths,
                RunConstants.EventDrowningBeacon,
                RunConstants.EventEndlessConveyor,
                RunConstants.EventPunchOff,
                RunConstants.EventSpiralingWhirlpool,
                RunConstants.EventSunkenStatue,
                RunConstants.EventSunkenTreasury,
                RunConstants.EventDoorsOfLightAndDark,
                RunConstants.EventTrashHeap,
                RunConstants.EventWaterloggedScriptorium,
                RunConstants.EventCrystalSphere,
                RunConstants.EventDollRoom,
                RunConstants.EventFakeMerchant,
                RunConstants.EventPotionCourier,
                RunConstants.EventRanwidTheElder,
                RunConstants.EventRelicTrader,
                RunConstants.EventRoomFullOfCheese,
                RunConstants.EventSlipperyBridge,
                RunConstants.EventStoneOfAllTime,
                RunConstants.EventSymbiote,
                RunConstants.EventTeaMaster,
                RunConstants.EventTheFutureOfPotions,
                RunConstants.EventThisOrThat,
                RunConstants.EventWarHistorianRepy,
                RunConstants.EventWelcomeToWongos,
            ]
            :
            [
                RunConstants.EventAromaOfChaos,
                RunConstants.EventByrdonisNest,
                RunConstants.EventDenseVegetation,
                RunConstants.EventJungleMazeAdventure,
                RunConstants.EventLuminousChoir,
                RunConstants.EventSelfHelpBook,
                RunConstants.EventSapphireSeed,
                RunConstants.EventSunkenStatue,
                RunConstants.EventTabletOfTruth,
                RunConstants.EventUnrestSite,
                RunConstants.EventWellspring,
                RunConstants.EventWhisperingHollow,
                RunConstants.EventWoodCarvings,
                RunConstants.EventCrystalSphere,
                RunConstants.EventDollRoom,
                RunConstants.EventFakeMerchant,
                RunConstants.EventPotionCourier,
                RunConstants.EventRanwidTheElder,
                RunConstants.EventRelicTrader,
                RunConstants.EventRoomFullOfCheese,
                RunConstants.EventSlipperyBridge,
                RunConstants.EventStoneOfAllTime,
                RunConstants.EventSymbiote,
                RunConstants.EventTeaMaster,
                RunConstants.EventTheFutureOfPotions,
                RunConstants.EventThisOrThat,
                RunConstants.EventWarHistorianRepy,
                RunConstants.EventWelcomeToWongos,
            ];

        var rng = new GameRng(state.Rng.Seed, "up_front");
        rng.Shuffle(eventPool);
        return eventPool.Where(eventId => eventId != 0).ToArray();
    }

    public static void GenerateActMap(RunState state)
    {
        state.MapNodes = [];
        state.CurrentMapCoord = (RunConstants.MapStartCol, 0);
        Array.Clear(state.MapOptionCoords);
        GetOrCreate(state, RunConstants.MapStartCol, 0).NodeType = RunConstants.NodeNone;
        GetOrCreate(state, RunConstants.MapStartCol, RunConstants.MapBossRow).NodeType =
            RunConstants.NodeBoss;

        // The game keys this stream on the act *index* — `act_{CurrentActIndex + 1}_map`
        // — not on which act was rolled. This used to pass `state.Act - 1`, conflating
        // the two: an Underdocks act 1 would have read "act_2_map" instead of
        // "act_1_map" and desynced the entire map. The emulator only models act 1, so
        // the index is always 0.
        var mapRng = state.Rng.ActMapRng(0);
        int restCount = mapRng.NextGaussianInt(7, 1, 6, 7);
        int unknownCount = mapRng.NextGaussianInt(12, 1, 10, 14);

        var starts = new List<(int Col, int Row)>();
        for (int path = 0; path < RunConstants.MapPathIterations; path++)
        {
            int startCol = mapRng.NextInt(RunConstants.MapWidth);
            if (path == 1)
            {
                while (starts.Contains((startCol, 1)))
                {
                    startCol = mapRng.NextInt(RunConstants.MapWidth);
                }
            }

            var current = GetOrCreate(state, startCol, 1);
            if (!starts.Contains((startCol, 1)))
            {
                starts.Add((startCol, 1));
            }

            GeneratePath(state, mapRng, current);
        }

        // Order matters. The game wires these with ForEachInRow, which walks the grid
        // columns 0..6 in order, and that insertion order becomes the child-enumeration
        // order used by FindAllPaths. Segments then land in their duplicate group in
        // that order, and PrunePaths shuffles the group before keeping one — so a
        // different initial order prunes a different node. Adding them in path-draw
        // order (the order the 7 starts were rolled) is not the same thing.
        foreach (var start in starts.OrderBy(s => s.Col))
        {
            AddEdge(state, state.CurrentMapCoord, start);
        }

        foreach (
            var node in state
                .MapNodes.Values.Where(n => n.Row == RunConstants.MapBossRow - 1)
                .OrderBy(n => n.Col)
                .ToArray()
        )
        {
            AddEdge(
                state,
                (node.Col, node.Row),
                (RunConstants.MapStartCol, RunConstants.MapBossRow)
            );
        }

        AssignPointTypes(state, mapRng, restCount, unknownCount);
        PruneAndRepair(state, mapRng, restCount, unknownCount);
        CenterGrid(state);
        SpreadAdjacentMapPoints(state);
        StraightenPaths(state);
        AssignEncounterIds(state);
        RefreshMapOptions(state);
    }

    private static void PruneAndRepair(RunState state, GameRng rng, int restCount, int unknownCount)
    {
        for (int i = 0; i < 3; i++)
        {
            PruneDuplicateSegments(state, rng);
            if (!RepairPrunedPointTypes(state, rng, restCount, unknownCount))
            {
                break;
            }
        }
    }

    private static bool RepairPrunedPointTypes(
        RunState state,
        GameRng rng,
        int restCount,
        int unknownCount
    )
    {
        bool repaired = false;
        repaired |= RepairPointType(state, RunConstants.NodeShop, RunConstants.MapShopCount, rng);
        repaired |= RepairPointType(state, RunConstants.NodeElite, RunConstants.MapEliteCount, rng);
        repaired |= RepairPointType(state, RunConstants.NodeRest, restCount, rng);
        repaired |= RepairPointType(state, RunConstants.NodeEvent, unknownCount, rng);
        return repaired;
    }

    private static bool RepairPointType(RunState state, int nodeType, int targetCount, GameRng rng)
    {
        int missing = targetCount - state.MapNodes.Values.Count(node => node.NodeType == nodeType);
        if (missing <= 0)
        {
            return false;
        }

        var candidates = state
            .MapNodes.Values.Where(node =>
                node.NodeType == RunConstants.NodeNormal && node.CanBeModified
            )
            .ToList();
        rng.StableShuffle(candidates, CompareNodesByColThenRow);
        bool repaired = false;
        foreach (var node in candidates)
        {
            if (missing == 0)
            {
                break;
            }

            if (IsValidPointType(state, nodeType, node))
            {
                node.NodeType = nodeType;
                missing--;
                repaired = true;
            }
        }

        return repaired;
    }

    private static void PruneDuplicateSegments(RunState state, GameRng rng)
    {
        int count = 0;
        var matchingSegments = FindMatchingSegments(state);
        while (PrunePaths(state, matchingSegments, rng))
        {
            count++;
            if (count > 50)
            {
                throw new InvalidOperationException("Unable to prune matching map segments");
            }

            matchingSegments = FindMatchingSegments(state);
        }
    }

    private static List<List<RunMapNode[]>> FindMatchingSegments(RunState state)
    {
        var paths = FindAllPaths(state, MapNodeAt(state, state.CurrentMapCoord));
        var segments = new SortedDictionary<string, List<RunMapNode[]>>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            AddSegmentsToDictionary(path, segments);
        }

        return segments.Values.Where(segmentList => segmentList.Count > 1).ToList();
    }

    private static List<List<RunMapNode>> FindAllPaths(RunState state, RunMapNode current)
    {
        if (current.NodeType == RunConstants.NodeBoss)
        {
            return
            [
                [current],
            ];
        }

        var paths = new List<List<RunMapNode>>();
        foreach (var childCoord in current.Children)
        {
            var child = MapNodeAt(state, childCoord);
            foreach (var path in FindAllPaths(state, child))
            {
                path.Insert(0, current);
                paths.Add(path);
            }
        }

        return paths;
    }

    private static void AddSegmentsToDictionary(
        IReadOnlyList<RunMapNode> path,
        IDictionary<string, List<RunMapNode[]>> segments
    )
    {
        for (int i = 0; i < path.Count - 1; i++)
        {
            if (!IsValidSegmentStartMapPoint(path[i]))
            {
                continue;
            }

            for (int j = 2; j < path.Count - i; j++)
            {
                var end = path[i + j];
                if (!IsValidSegmentEndMapPoint(end))
                {
                    continue;
                }

                var segment = path.Skip(i).Take(j + 1).ToArray();
                string key = GenerateSegmentKey(segment);
                if (!segments.TryGetValue(key, out var existing))
                {
                    segments[key] = [segment];
                }
                else if (
                    !existing.Any(existingSegment => OverlappingSegment(existingSegment, segment))
                )
                {
                    existing.Add(segment);
                }
            }
        }
    }

    private static bool IsValidSegmentStartMapPoint(RunMapNode node) =>
        node.Children.Count <= 1 ? node.Row == 0 : true;

    private static bool IsValidSegmentEndMapPoint(RunMapNode node) => node.Parents.Count >= 2;

    /// <summary>
    /// Our node-type constants to the game's <c>MapPointType</c> enum values.
    ///
    /// This matters because segment keys embed the point types as integers and the
    /// segments live in a SortedDictionary ordered by that key string. Grouping
    /// survives any relabelling, but *iteration order* does not — and PrunePaths
    /// walks the groups in order, shuffling and pruning as it goes. Emitting our own
    /// numbering here sorts the groups differently from the game, which changes both
    /// the pruning decisions and the RNG draws they consume.
    ///
    /// NodeNone maps to Ancient because by the time pruning runs every other node has
    /// been assigned a type, so the start point is the only one left holding it.
    /// </summary>
    private static int GameMapPointType(int nodeType) =>
        nodeType switch
        {
            RunConstants.NodeEvent => 1, // Unknown
            RunConstants.NodeShop => 2, // Shop
            RunConstants.NodeRelic => 3, // Treasure
            RunConstants.NodeRest => 4, // RestSite
            RunConstants.NodeNormal => 5, // Monster
            RunConstants.NodeElite => 6, // Elite
            RunConstants.NodeBoss => 7, // Boss
            RunConstants.NodeNone => 8, // Ancient
            _ => 0, // Unassigned
        };

    private static string GenerateSegmentKey(IReadOnlyList<RunMapNode> segment)
    {
        var start = segment[0];
        var end = segment[^1];
        string prefix =
            start.Row == 0
                ? $"{start.Row}-{end.Col},{end.Row}-"
                : $"{start.Col},{start.Row}-{end.Col},{end.Row}-";
        return prefix + string.Join(",", segment.Select(node => GameMapPointType(node.NodeType)));
    }

    private static bool OverlappingSegment(
        IReadOnlyList<RunMapNode> left,
        IReadOnlyList<RunMapNode> right
    )
    {
        if (left.Count < 3 || right.Count < 3)
        {
            return false;
        }

        for (int i = 1; i <= left.Count - 2; i++)
        {
            if (ReferenceEquals(left[i], right[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PrunePaths(
        RunState state,
        IEnumerable<List<RunMapNode[]>> matchingSegments,
        GameRng rng
    )
    {
        foreach (var matchingSegment in matchingSegments)
        {
            rng.Shuffle(matchingSegment);
            if (PruneAllButLast(state, matchingSegment) != 0)
            {
                return true;
            }

            if (BreakAParentChildRelationshipInAnySegment(state, matchingSegment))
            {
                return true;
            }
        }

        return false;
    }

    private static int PruneAllButLast(RunState state, IReadOnlyList<RunMapNode[]> matches)
    {
        int pruned = 0;
        foreach (var match in matches)
        {
            if (pruned == matches.Count - 1)
            {
                return pruned;
            }

            if (PruneSegment(state, match))
            {
                pruned++;
            }
        }

        return pruned;
    }

    private static bool PruneSegment(RunState state, RunMapNode[] segment)
    {
        bool pruned = false;
        for (int i = 0; i < segment.Length - 1; i++)
        {
            var node = segment[i];
            if (!IsInMap(state, node))
            {
                return true;
            }

            if (
                node.Children.Count > 1
                || node.Parents.Count > 1
                // `&& !IsRemoved(...)`: the game tests the parent's *grid* cell, and the
                // ancient is never in the grid, so a row-1 node is never skipped on
                // account of the start point — even when pruning has left it one child.
                || node.Parents.Any(parent =>
                    MapNodeAt(state, parent).Children.Count == 1 && !IsRemovedFromGrid(parent)
                )
            )
            {
                continue;
            }

            var tail = segment.Skip(i).ToArray();
            if (tail.Any(n => n.Children.Count > 1 && n.Parents.Count == 1))
            {
                continue;
            }

            if (segment[^1].Parents.Count == 1)
            {
                return false;
            }

            if (
                !node
                    .Children.Where(child =>
                        !segment.Any(n => n.Col == child.Col && n.Row == child.Row)
                    )
                    .Any(child => MapNodeAt(state, child).Parents.Count == 1)
            )
            {
                RemovePoint(state, node);
                pruned = true;
            }
        }

        return pruned;
    }

    private static bool BreakAParentChildRelationshipInAnySegment(
        RunState state,
        IEnumerable<RunMapNode[]> matches
    )
    {
        foreach (var match in matches)
        {
            if (BreakAParentChildRelationshipInSegment(state, match))
            {
                return true;
            }
        }

        return false;
    }

    private static bool BreakAParentChildRelationshipInSegment(
        RunState state,
        IReadOnlyList<RunMapNode> segment
    )
    {
        // The game walks the WHOLE segment and breaks every qualifying link before
        // reporting back (MapPathPruning.BreakAParentChildRelationshipInSegment sets a
        // flag and keeps going). Returning on the first break, as this used to, leaves
        // later links intact — and the re-scan that follows sees a different graph, so
        // they may never be broken at all. Seed "L4CEF9U55L" kept one such edge, which
        // then pinned its row-11 node to the only column both its children allowed.
        bool broken = false;
        for (int i = 0; i < segment.Count - 1; i++)
        {
            var node = segment[i];
            if (node.Children.Count < 2)
            {
                continue;
            }

            var child = segment[i + 1];
            if (child.Parents.Count != 1)
            {
                RemoveEdge(state, (node.Col, node.Row), (child.Col, child.Row));
                broken = true;
            }
        }

        return broken;
    }

    // The game's grid holds only the path rows, so the ancient and boss points read as
    // "removed" to any grid lookup — see IsGridRow.
    private static bool IsRemovedFromGrid((int Col, int Row) coord) => !IsGridRow(coord.Row);

    private static bool IsInMap(RunState state, RunMapNode node) =>
        node.NodeType is RunConstants.NodeNone or RunConstants.NodeBoss
        || state.MapNodes.TryGetValue((node.Col, node.Row), out var current)
            && ReferenceEquals(current, node);

    private static void RemovePoint(RunState state, RunMapNode node)
    {
        state.MapNodes.Remove((node.Col, node.Row));
        foreach (var child in node.Children.ToArray())
        {
            RemoveEdge(state, (node.Col, node.Row), child);
        }

        foreach (var parent in node.Parents.ToArray())
        {
            RemoveEdge(state, parent, (node.Col, node.Row));
        }
    }

    private static void RemoveEdge(
        RunState state,
        (int Col, int Row) parentCoord,
        (int Col, int Row) childCoord
    )
    {
        if (state.MapNodes.TryGetValue(parentCoord, out var parent))
        {
            parent.Children.Remove(childCoord);
        }

        if (state.MapNodes.TryGetValue(childCoord, out var child))
        {
            child.Parents.Remove(parentCoord);
        }
    }

    // The game's post-processing takes `Grid`, which is [7, rooms + 1] — rows 1..15 of
    // path nodes. The ancient and boss points are NOT in it: StandardActMap holds them
    // as standalone `StartingMapPoint` / `BossMapPoint` at `GetColumnCount() / 2`, and
    // the boss's row is `GetRowCount()`, one past the grid's last row. So centering,
    // spreading and straightening never move them — they stay in the middle column
    // whatever the body does, which is also why the save writes `start` and `boss`
    // outside `points`. The emulator carries them as ordinary nodes, so every pass has
    // to skip them explicitly or a centered map drags them off-centre with it.
    private static bool IsGridRow(int row) => row > 0 && row < RunConstants.MapBossRow;

    private static void CenterGrid(RunState state)
    {
        bool leftEmpty = IsColumnEmpty(state, 0) && IsColumnEmpty(state, 1);
        bool rightEmpty =
            IsColumnEmpty(state, RunConstants.MapWidth - 1)
            && IsColumnEmpty(state, RunConstants.MapWidth - 2);
        int delta =
            leftEmpty && !rightEmpty ? -1
            : !leftEmpty && rightEmpty ? 1
            : 0;
        if (delta == 0)
        {
            return;
        }

        var gridNodes = state.MapNodes.Values.Where(n => IsGridRow(n.Row));
        var nodes =
            delta > 0
                ? gridNodes.OrderByDescending(n => n.Col).ThenBy(n => n.Row).ToList()
                : gridNodes.OrderBy(n => n.Col).ThenBy(n => n.Row).ToList();
        foreach (var node in nodes)
        {
            MoveNode(state, node, node.Col + delta);
        }
    }

    private static bool IsColumnEmpty(RunState state, int col) =>
        !state.MapNodes.Values.Any(node => IsGridRow(node.Row) && node.Col == col);

    private static void SpreadAdjacentMapPoints(RunState state)
    {
        for (int row = 1; row < RunConstants.MapBossRow; row++)
        {
            // Collect the row ONCE, in column order, and keep that list for every pass
            // of the loop below — the game walks its grid columns to build the list
            // before entering its do/while and never rebuilds it. Re-sorting each pass
            // (what this used to do) visits the nodes in a different order once one has
            // moved, and the order decides which node claims a free column: seed
            // "L4CEF9U55L" ended up with row 11's last node at c5 where the game put it
            // at c6, with the rest of the map identical.
            var rowNodes = state
                .MapNodes.Values.Where(node => node.Row == row)
                .OrderBy(node => node.Col)
                .ToList();
            bool changed;
            do
            {
                changed = false;
                foreach (var node in rowNodes)
                {
                    int currentCol = node.Col;
                    var allowed = GetAllowedPositions(state, node);
                    int bestCol = currentCol;
                    int bestGap = ComputeGap(currentCol, rowNodes, node);
                    foreach (int candidateCol in allowed)
                    {
                        if (
                            candidateCol == currentCol
                            || state.MapNodes.ContainsKey((candidateCol, row))
                        )
                        {
                            continue;
                        }

                        int gap = ComputeGap(candidateCol, rowNodes, node);
                        if (gap > bestGap)
                        {
                            bestCol = candidateCol;
                            bestGap = gap;
                        }
                    }

                    if (bestCol != currentCol)
                    {
                        MoveNode(state, node, bestCol);
                        changed = true;
                    }
                }
            } while (changed);
        }
    }

    private static HashSet<int> GetAllowedPositions(RunState state, RunMapNode node)
    {
        var allowed = Enumerable.Range(0, RunConstants.MapWidth).ToHashSet();
        foreach (var parent in node.Parents)
        {
            allowed.IntersectWith(GetNeighborAllowedPositions(parent.Col));
        }

        foreach (var child in node.Children)
        {
            allowed.IntersectWith(GetNeighborAllowedPositions(child.Col));
        }

        return allowed;
    }

    private static HashSet<int> GetNeighborAllowedPositions(int col) =>
        Enumerable
            .Range(
                Math.Max(0, col - 1),
                Math.Min(RunConstants.MapWidth - 1, col + 1) - Math.Max(0, col - 1) + 1
            )
            .ToHashSet();

    private static int ComputeGap(int candidateCol, List<RunMapNode> rowNodes, RunMapNode current)
    {
        int gap = int.MaxValue;
        foreach (var node in rowNodes)
        {
            if (!ReferenceEquals(node, current))
            {
                gap = Math.Min(gap, Math.Abs(candidateCol - node.Col));
            }
        }

        return gap;
    }

    private static void StraightenPaths(RunState state)
    {
        for (int row = 1; row < RunConstants.MapBossRow; row++)
        {
            for (int col = 0; col < RunConstants.MapWidth; col++)
            {
                if (!state.MapNodes.TryGetValue((col, row), out var node))
                {
                    continue;
                }

                if (node.Parents.Count != 1 || node.Children.Count != 1)
                {
                    continue;
                }

                var parent = node.Parents[0];
                var child = node.Children[0];
                bool leftKink = node.Col < child.Col && node.Col < parent.Col;
                bool rightKink = node.Col > child.Col && node.Col > parent.Col;
                if (
                    leftKink
                    && col < RunConstants.MapWidth - 1
                    && !state.MapNodes.ContainsKey((col + 1, row))
                )
                {
                    MoveNode(state, node, col + 1);
                }
                else if (rightKink && col > 0 && !state.MapNodes.ContainsKey((col - 1, row)))
                {
                    MoveNode(state, node, col - 1);
                }
            }
        }
    }

    private static void MoveNode(RunState state, RunMapNode node, int newCol)
    {
        if (newCol < 0 || newCol >= RunConstants.MapWidth || newCol == node.Col)
        {
            return;
        }

        var oldCoord = (node.Col, node.Row);
        var newCoord = (newCol, node.Row);
        if (state.MapNodes.ContainsKey(newCoord))
        {
            return;
        }

        state.MapNodes.Remove(oldCoord);
        node.Col = newCol;
        state.MapNodes[newCoord] = node;
        foreach (var parentCoord in node.Parents.ToArray())
        {
            if (state.MapNodes.TryGetValue(parentCoord, out var parent))
            {
                ReplaceCoord(parent.Children, oldCoord, newCoord);
            }
        }

        foreach (var childCoord in node.Children.ToArray())
        {
            if (state.MapNodes.TryGetValue(childCoord, out var child))
            {
                ReplaceCoord(child.Parents, oldCoord, newCoord);
            }
        }

        if (state.CurrentMapCoord == oldCoord)
        {
            state.CurrentMapCoord = newCoord;
        }
    }

    private static void ReplaceCoord(
        List<(int Col, int Row)> coords,
        (int Col, int Row) oldCoord,
        (int Col, int Row) newCoord
    )
    {
        int index = coords.IndexOf(oldCoord);
        if (index >= 0)
        {
            coords[index] = newCoord;
        }
    }

    private static void AddTraceNode(RunState state, int col, int row, int nodeType)
    {
        var node = GetOrCreate(state, col, row);
        node.NodeType = nodeType;
    }

    public static void RefreshMapOptions(RunState state)
    {
        Array.Clear(state.MapNodeTypes);
        Array.Clear(state.MapChoices);
        Array.Clear(state.MapOptionCoords);
        if (!state.MapNodes.TryGetValue(state.CurrentMapCoord, out var current))
        {
            return;
        }

        var options = current
            .Children.OrderBy(coord => coord.Row)
            .ThenBy(coord => coord.Col)
            .Take(RunConstants.MapChoices)
            .ToArray();
        for (int i = 0; i < options.Length; i++)
        {
            var node = state.MapNodes[options[i]];
            state.MapNodeTypes[i] = node.NodeType;
            state.MapChoices[i] = node.NodeType switch
            {
                RunConstants.NodeNormal => state.NormalEncounterSequence[
                    state.NormalEncountersVisited % state.NormalEncounterSequence.Length
                ],
                RunConstants.NodeElite => state.EliteEncounterSequence[
                    state.EliteEncountersVisited % state.EliteEncounterSequence.Length
                ],
                RunConstants.NodeBoss => state.BossEncounterId,
                _ => node.EncounterId,
            };
            state.MapOptionCoords[i] = options[i];
        }
    }

    public static bool ChooseMapNode(
        RunState state,
        int action,
        out int nodeType,
        out int encounterId
    )
    {
        nodeType = RunConstants.NodeNone;
        encounterId = 0;

        if (
            (uint)action >= RunConstants.MapChoices
            || state.MapNodeTypes[action] == RunConstants.NodeNone
        )
        {
            return false;
        }

        var coord = state.MapOptionCoords[action];
        if (coord is null)
        {
            return false;
        }

        nodeType = state.MapNodeTypes[action];
        encounterId = state.MapChoices[action];
        state.CurrentMapCoord = coord.Value;
        state.CurrentNodeType = nodeType;
        state.Floor++;
        if (nodeType == RunConstants.NodeNormal)
        {
            state.NormalEncountersVisited++;
        }
        else if (nodeType == RunConstants.NodeElite)
        {
            state.EliteEncountersVisited++;
        }

        Array.Clear(state.MapNodeTypes);
        Array.Clear(state.MapChoices);
        Array.Clear(state.MapOptionCoords);
        return true;
    }

    private static void GeneratePath(RunState state, GameRng rng, RunMapNode start)
    {
        var current = start;
        while (current.Row < RunConstants.MapBossRow - 1)
        {
            var child = GenerateNextCoord(state, rng, current);
            AddEdge(state, (current.Col, current.Row), child);
            current = state.MapNodes[child];
        }
    }

    private static (int Col, int Row) GenerateNextCoord(
        RunState state,
        GameRng rng,
        RunMapNode current
    )
    {
        var deltas = new List<int> { -1, 0, 1 };
        rng.StableShuffle(deltas, Comparer<int>.Default);
        foreach (int delta in deltas)
        {
            int target = Math.Clamp(current.Col + delta, 0, RunConstants.MapWidth - 1);
            if (!HasInvalidCrossover(state, current, target))
            {
                return (target, current.Row + 1);
            }
        }
        return (current.Col, current.Row + 1);
    }

    private static bool HasInvalidCrossover(RunState state, RunMapNode current, int targetCol)
    {
        int delta = targetCol - current.Col;
        if (delta == 0 || !state.MapNodes.TryGetValue((targetCol, current.Row), out var sibling))
        {
            return false;
        }

        return sibling.Children.Any(child => child.Col - sibling.Col == -delta);
    }

    private static void AssignPointTypes(
        RunState state,
        GameRng rng,
        int restCount,
        int unknownCount
    )
    {
        foreach (var node in state.MapNodes.Values)
        {
            node.NodeType = node.Row switch
            {
                0 => RunConstants.NodeNone,
                1 => RunConstants.NodeNormal,
                RunConstants.MapTreasureRow => RunConstants.NodeRelic,
                RunConstants.MapFinalRestRow => RunConstants.NodeRest,
                RunConstants.MapBossRow => RunConstants.NodeBoss,
                _ => RunConstants.NodeNone,
            };
            // The game sets CanBeModified = false on exactly the forced rows, which
            // keeps their points out of the repair candidate pool.
            node.CanBeModified =
                node.Row is not (1 or RunConstants.MapTreasureRow or RunConstants.MapFinalRestRow);
        }

        var pointTypes = new Queue<int>(
            Enumerable
                .Repeat(RunConstants.NodeRest, restCount)
                .Concat(Enumerable.Repeat(RunConstants.NodeShop, RunConstants.MapShopCount))
                .Concat(Enumerable.Repeat(RunConstants.NodeElite, RunConstants.MapEliteCount))
                .Concat(Enumerable.Repeat(RunConstants.NodeEvent, unknownCount))
        );
        for (int pass = 0; pass < 3 && pointTypes.Count > 0; pass++)
        {
            var candidates = state
                .MapNodes.Values.Where(n =>
                    n.NodeType == RunConstants.NodeNone
                    && n.Row is > 1 and < RunConstants.MapFinalRestRow
                    && n.Row != RunConstants.MapTreasureRow
                )
                .ToList();
            rng.StableShuffle(candidates, CompareNodesByColThenRow);
            foreach (var node in candidates)
            {
                if (pointTypes.Count == 0)
                {
                    break;
                }

                node.NodeType = GetNextValidPointType(state, pointTypes, node);
            }
        }

        foreach (
            var node in state.MapNodes.Values.Where(n =>
                n.NodeType == RunConstants.NodeNone && n.Row > 0
            )
        )
        {
            node.NodeType = RunConstants.NodeNormal;
        }
    }

    private static int GetNextValidPointType(RunState state, Queue<int> pointTypes, RunMapNode node)
    {
        for (int i = 0; i < pointTypes.Count; i++)
        {
            int nodeType = pointTypes.Dequeue();
            if (IsValidPointType(state, nodeType, node))
            {
                return nodeType;
            }

            pointTypes.Enqueue(nodeType);
        }
        return RunConstants.NodeNone;
    }

    private static bool IsValidPointType(RunState state, int nodeType, RunMapNode node) =>
        IsValidForLower(nodeType, node)
        && IsValidForUpper(nodeType, node)
        && IsValidWithParentsAndChildren(state, nodeType, node)
        && IsValidWithChildren(state, nodeType, node)
        && IsValidWithSiblings(state, nodeType, node);

    private static bool IsValidForLower(int nodeType, RunMapNode node) =>
        node.Row >= 6 || nodeType is not (RunConstants.NodeRest or RunConstants.NodeElite);

    private static bool IsValidForUpper(int nodeType, RunMapNode node) =>
        node.Row < RunConstants.MapBossRow - 3 || nodeType != RunConstants.NodeRest;

    private static bool IsValidWithParentsAndChildren(
        RunState state,
        int nodeType,
        RunMapNode node
    ) =>
        !HasParentChildRestriction(nodeType)
        || !node
            .Parents.Concat(node.Children)
            .Any(coord => MapNodeTypeAt(state, coord) == nodeType);

    private static bool IsValidWithChildren(RunState state, int nodeType, RunMapNode node) =>
        !HasParentChildRestriction(nodeType)
        || !node.Children.Any(coord => MapNodeTypeAt(state, coord) == nodeType);

    private static bool IsValidWithSiblings(RunState state, int nodeType, RunMapNode node) =>
        !HasSiblingRestriction(nodeType)
        || !node
            .Parents.SelectMany(parent => MapNodeAt(state, parent).Children)
            .Where(coord => coord != (node.Col, node.Row))
            .Any(coord => MapNodeTypeAt(state, coord) == nodeType);

    private static RunMapNode MapNodeAt(RunState state, (int Col, int Row) coord) =>
        state.MapNodes[coord];

    private static int MapNodeTypeAt(RunState state, (int Col, int Row) coord) =>
        MapNodeAt(state, coord).NodeType;

    private static bool HasParentChildRestriction(int nodeType) =>
        nodeType
            is RunConstants.NodeElite
                or RunConstants.NodeRest
                or RunConstants.NodeRelic
                or RunConstants.NodeShop;

    private static bool HasSiblingRestriction(int nodeType) =>
        nodeType
            is RunConstants.NodeRest
                or RunConstants.NodeNormal
                or RunConstants.NodeEvent
                or RunConstants.NodeElite
                or RunConstants.NodeShop;

    private static readonly Comparer<RunMapNode> CompareNodesByColThenRow =
        Comparer<RunMapNode>.Create(
            (a, b) => a.Col != b.Col ? a.Col.CompareTo(b.Col) : a.Row.CompareTo(b.Row)
        );

    private static void AssignEncounterIds(RunState state)
    {
        foreach (
            var group in state
                .MapNodes.Values.Where(n => n.NodeType == RunConstants.NodeNormal)
                .GroupBy(n => n.Row)
                .OrderBy(g => g.Key)
        )
        {
            int index = Math.Min(group.Key - 1, state.NormalEncounterSequence.Length - 1);
            foreach (var node in group)
            {
                node.EncounterId = state.NormalEncounterSequence[Math.Max(0, index)];
            }
        }
        foreach (
            var group in state
                .MapNodes.Values.Where(n => n.NodeType == RunConstants.NodeElite)
                .GroupBy(n => n.Row)
                .OrderBy(g => g.Key)
        )
        {
            int index = Math.Min(group.Key, state.EliteEncounterSequence.Length - 1);
            foreach (var node in group)
            {
                node.EncounterId = state.EliteEncounterSequence[Math.Max(0, index)];
            }
        }
        foreach (var node in state.MapNodes.Values.Where(n => n.NodeType == RunConstants.NodeBoss))
        {
            node.EncounterId = state.BossEncounterId;
        }
    }

    private static RunMapNode GetOrCreate(RunState state, int col, int row)
    {
        if (!state.MapNodes.TryGetValue((col, row), out var node))
        {
            node = new RunMapNode(col, row);
            state.MapNodes[(col, row)] = node;
        }
        return node;
    }

    private static void AddEdge(
        RunState state,
        (int Col, int Row) parentCoord,
        (int Col, int Row) childCoord
    )
    {
        var parent = GetOrCreate(state, parentCoord.Col, parentCoord.Row);
        var child = GetOrCreate(state, childCoord.Col, childCoord.Row);
        if (!parent.Children.Contains(childCoord))
        {
            parent.Children.Add(childCoord);
        }

        if (!child.Parents.Contains(parentCoord))
        {
            child.Parents.Add(parentCoord);
        }
    }

    private static int GrabWithoutRepeatingTags(List<int> bag, int? lastEncounter, GameRng rng)
    {
        var lastTags = lastEncounter.HasValue ? Tags(lastEncounter.Value) : [];
        bool anyValid = bag.Any(enc => enc != lastEncounter && !Tags(enc).Overlaps(lastTags));
        while (true)
        {
            int index = (int)(rng.NextDouble() * bag.Count);
            int enc = bag[index];
            if (!anyValid || (enc != lastEncounter && !Tags(enc).Overlaps(lastTags)))
            {
                bag.RemoveAt(index);
                return enc;
            }
        }
    }

    private static HashSet<string> Tags(int encounterId) =>
        encounterId switch
        {
            2 => ["Nibbit"], // NibbitsWeak
            3 => ["Slimes"], // SlimesWeak
            8 => ["Crawler"], // FuzzyWurmCrawlerWeak
            9 => ["Slugs"], // CorpseSlugs
            11 => ["Shrinker"], // ShrinkerBeetleWeak
            12 => ["Seapunk"],
            // 15 is NibbitsNormal, which declares NO Tags in the game — only
            // NibbitsWeak is tagged Nibbit. Tagging it here wrongly blocked the game's
            // legitimate NibbitsWeak -> NibbitsNormal run, shifting the whole
            // remaining sequence by one.
            16 => ["Slimes"], // SlimesNormal
            17 => ["Mushroom", "Slimes"],
            18 => ["Mushroom"],
            21 => ["Shrinker", "Crawler"],
            _ => [],
        };
}
