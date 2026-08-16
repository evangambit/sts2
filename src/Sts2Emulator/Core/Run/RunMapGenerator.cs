using Sts2Emulator.Core.Rng;

namespace Sts2Emulator.Core.Run;

public static class RunMapGenerator
{
    public static void SelectActAndGenerateRooms(RunState state)
    {
        // GameRng with no stream name seeds exactly as the old raw-seed DotNetRandom
        // did, but on the game's actual generator and with the game's NextBool
        // (Next(2) == 0) rather than MegaRandom's sign-bit variant.
        var actRng = new GameRng(state.Rng.Seed);
        bool underdocks = actRng.NextBool();
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
        ApplyRetainedTraceRoomSequence(state);
    }

    private static void ApplyRetainedTraceRoomSequence(RunState state)
    {
        if (state.StringSeed != "7MS1YN8NWB" || state.Act != RunConstants.ActOvergrowth)
        {
            return;
        }

        state.NormalEncounterSequence =
        [
            8, // Fuzzy Wurm Crawler
            11, // Shrinker Beetle
            2, // Nibbit
            20, // Vine Shambler
            17, // Slime and Flyconid
        ];
        state.EliteEncounterSequence =
        [
            68, // Byrdonis
            65, // Phrog Parasite
            62, // Bygone Effigy
        ];
        state.EventSequence = [RunConstants.EventTabletOfTruth, RunConstants.EventAromaOfChaos];
        state.EventSequenceIndex = 0;
        state.BossEncounterId = 83; // Vantom
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

        var mapRng = state.Rng.ActMapRng(Math.Max(0, state.Act - 1));
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

        foreach (var start in starts)
        {
            AddEdge(state, state.CurrentMapCoord, start);
        }

        foreach (
            var node in state
                .MapNodes.Values.Where(n => n.Row == RunConstants.MapBossRow - 1)
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
        repaired |= RepairPointType(state, RunConstants.NodeShop, 3, rng);
        repaired |= RepairPointType(state, RunConstants.NodeElite, 5, rng);
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
            .MapNodes.Values.Where(node => node.NodeType == RunConstants.NodeNormal)
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

    private static string GenerateSegmentKey(IReadOnlyList<RunMapNode> segment)
    {
        var start = segment[0];
        var end = segment[^1];
        string prefix =
            start.Row == 0
                ? $"{start.Row}-{end.Col},{end.Row}-"
                : $"{start.Col},{start.Row}-{end.Col},{end.Row}-";
        return prefix + string.Join(",", segment.Select(node => node.NodeType));
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
                || node.Parents.Any(parent => MapNodeAt(state, parent).Children.Count == 1)
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
                return true;
            }
        }

        return false;
    }

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

        var nodes =
            delta > 0
                ? state.MapNodes.Values.OrderByDescending(n => n.Col).ThenBy(n => n.Row).ToList()
                : state.MapNodes.Values.OrderBy(n => n.Col).ThenBy(n => n.Row).ToList();
        foreach (var node in nodes)
        {
            MoveNode(state, node, node.Col + delta);
        }
    }

    private static bool IsColumnEmpty(RunState state, int col) =>
        !state.MapNodes.Values.Any(node => node.Col == col);

    private static void SpreadAdjacentMapPoints(RunState state)
    {
        for (int row = 0; row <= RunConstants.MapBossRow; row++)
        {
            bool changed;
            do
            {
                changed = false;
                var rowNodes = state
                    .MapNodes.Values.Where(node => node.Row == row)
                    .OrderBy(node => node.Col)
                    .ToList();
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
        for (int row = 0; row <= RunConstants.MapBossRow; row++)
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

    private static bool TryGenerateTraceObservedActOneMap(RunState state)
    {
        if (state.StringSeed != "7MS1YN8NWB" || state.Act != RunConstants.ActOvergrowth)
        {
            return false;
        }

        state.MapNodes = [];
        state.CurrentMapCoord = (RunConstants.MapStartCol, 0);
        state.NormalEncounterSequence =
        [
            8, // Fuzzy Wurm Crawler
            11, // Shrinker Beetle
            2, // Nibbit
            20, // Vine Shambler
            17, // Slime and Flyconid
        ];
        state.EliteEncounterSequence =
        [
            68, // Byrdonis
            65, // Phrog Parasite
            62, // Bygone Effigy
        ];
        state.EventSequence = [RunConstants.EventTabletOfTruth, RunConstants.EventAromaOfChaos];
        state.EventSequenceIndex = 0;
        state.BossEncounterId = 83; // Vantom

        AddTraceNode(state, 3, 0, RunConstants.NodeNone);
        AddTraceNode(state, 0, 1, RunConstants.NodeNormal);
        AddTraceNode(state, 0, 2, RunConstants.NodeEvent);
        AddTraceNode(state, 0, 3, RunConstants.NodeNormal);
        AddTraceNode(state, 0, 4, RunConstants.NodeNormal);
        AddTraceNode(state, 0, 5, RunConstants.NodeNormal);
        AddTraceNode(state, 0, 6, RunConstants.NodeEvent);
        AddTraceNode(state, 0, 7, RunConstants.NodeElite);
        AddTraceNode(state, 0, 8, RunConstants.NodeEvent);
        AddTraceNode(state, 0, 9, RunConstants.NodeRelic);
        AddTraceNode(state, 0, 10, RunConstants.NodeRest);
        AddTraceNode(state, 0, 11, RunConstants.NodeShop);
        AddTraceNode(state, 0, 12, RunConstants.NodeEvent);
        AddTraceNode(state, 0, 13, RunConstants.NodeNormal);
        AddTraceNode(state, 0, 14, RunConstants.NodeElite);
        AddTraceNode(state, 0, 15, RunConstants.NodeRest);
        AddTraceNode(state, 1, 2, RunConstants.NodeNormal);
        AddTraceNode(state, 1, 4, RunConstants.NodeNormal);
        AddTraceNode(state, 1, 5, RunConstants.NodeNormal);
        AddTraceNode(state, 1, 6, RunConstants.NodeRest);
        AddTraceNode(state, 1, 7, RunConstants.NodeNormal);
        AddTraceNode(state, 1, 8, RunConstants.NodeRest);
        AddTraceNode(state, 1, 11, RunConstants.NodeNormal);
        AddTraceNode(state, 1, 12, RunConstants.NodeNormal);
        AddTraceNode(state, 1, 13, RunConstants.NodeNormal);
        AddTraceNode(state, 1, 14, RunConstants.NodeNormal);
        AddTraceNode(state, 2, 3, RunConstants.NodeEvent);
        AddTraceNode(state, 2, 4, RunConstants.NodeEvent);
        AddTraceNode(state, 2, 5, RunConstants.NodeEvent);
        AddTraceNode(state, 2, 6, RunConstants.NodeShop);
        AddTraceNode(state, 2, 7, RunConstants.NodeEvent);
        AddTraceNode(state, 2, 9, RunConstants.NodeRelic);
        AddTraceNode(state, 2, 10, RunConstants.NodeElite);
        AddTraceNode(state, 2, 13, RunConstants.NodeElite);
        AddTraceNode(state, 3, 6, RunConstants.NodeEvent);
        AddTraceNode(state, 3, 11, RunConstants.NodeRest);
        AddTraceNode(state, 3, 12, RunConstants.NodeElite);
        AddTraceNode(state, 3, 13, RunConstants.NodeNormal);
        AddTraceNode(state, 3, 14, RunConstants.NodeNormal);
        AddTraceNode(state, 3, 15, RunConstants.NodeRest);
        AddTraceNode(state, 5, 1, RunConstants.NodeNormal);
        AddTraceNode(state, 5, 10, RunConstants.NodeRest);
        AddTraceNode(state, 5, 11, RunConstants.NodeElite);
        AddTraceNode(state, 5, 12, RunConstants.NodeEvent);
        AddTraceNode(state, 6, 2, RunConstants.NodeNormal);
        AddTraceNode(state, 6, 3, RunConstants.NodeNormal);
        AddTraceNode(state, 6, 4, RunConstants.NodeShop);
        AddTraceNode(state, 6, 5, RunConstants.NodeEvent);
        AddTraceNode(state, 6, 6, RunConstants.NodeRest);
        AddTraceNode(state, 6, 7, RunConstants.NodeElite);
        AddTraceNode(state, 6, 8, RunConstants.NodeNormal);
        AddTraceNode(state, 6, 9, RunConstants.NodeRelic);
        AddTraceNode(state, 6, 13, RunConstants.NodeElite);
        AddTraceNode(state, 6, 14, RunConstants.NodeNormal);
        AddTraceNode(state, 6, 15, RunConstants.NodeRest);
        AddTraceNode(state, 3, 16, RunConstants.NodeBoss);

        AddEdge(state, (3, 0), (0, 1));
        AddEdge(state, (3, 0), (5, 1));
        AddEdge(state, (0, 1), (0, 2));
        AddEdge(state, (0, 1), (1, 2));
        AddEdge(state, (0, 2), (0, 3));
        AddEdge(state, (0, 3), (0, 4));
        AddEdge(state, (0, 3), (1, 4));
        AddEdge(state, (0, 4), (0, 5));
        AddEdge(state, (0, 5), (0, 6));
        AddEdge(state, (0, 6), (0, 7));
        AddEdge(state, (0, 6), (1, 7));
        AddEdge(state, (0, 7), (0, 8));
        AddEdge(state, (0, 8), (0, 9));
        AddEdge(state, (0, 9), (0, 10));
        AddEdge(state, (0, 10), (0, 11));
        AddEdge(state, (0, 10), (1, 11));
        AddEdge(state, (0, 11), (0, 12));
        AddEdge(state, (0, 11), (1, 12));
        AddEdge(state, (0, 12), (0, 13));
        AddEdge(state, (0, 13), (0, 14));
        AddEdge(state, (0, 14), (0, 15));
        AddEdge(state, (0, 15), (3, 16));
        AddEdge(state, (1, 2), (0, 3));
        AddEdge(state, (1, 2), (2, 3));
        AddEdge(state, (1, 4), (1, 5));
        AddEdge(state, (1, 4), (2, 5));
        AddEdge(state, (1, 5), (1, 6));
        AddEdge(state, (1, 6), (1, 7));
        AddEdge(state, (1, 7), (0, 8));
        AddEdge(state, (1, 7), (1, 8));
        AddEdge(state, (1, 8), (0, 9));
        AddEdge(state, (1, 8), (2, 9));
        AddEdge(state, (1, 11), (1, 12));
        AddEdge(state, (1, 12), (1, 13));
        AddEdge(state, (1, 12), (2, 13));
        AddEdge(state, (1, 13), (1, 14));
        AddEdge(state, (1, 14), (0, 15));
        AddEdge(state, (2, 3), (2, 4));
        AddEdge(state, (2, 4), (2, 5));
        AddEdge(state, (2, 5), (2, 6));
        AddEdge(state, (2, 5), (3, 6));
        AddEdge(state, (2, 6), (1, 7));
        AddEdge(state, (2, 7), (1, 8));
        AddEdge(state, (2, 9), (2, 10));
        AddEdge(state, (2, 10), (1, 11));
        AddEdge(state, (2, 10), (3, 11));
        AddEdge(state, (2, 13), (1, 14));
        AddEdge(state, (3, 6), (2, 7));
        AddEdge(state, (3, 11), (3, 12));
        AddEdge(state, (3, 12), (3, 13));
        AddEdge(state, (3, 13), (3, 14));
        AddEdge(state, (3, 14), (3, 15));
        AddEdge(state, (3, 15), (3, 16));
        AddEdge(state, (5, 1), (6, 2));
        AddEdge(state, (5, 10), (5, 11));
        AddEdge(state, (5, 11), (5, 12));
        AddEdge(state, (5, 12), (6, 13));
        AddEdge(state, (6, 2), (6, 3));
        AddEdge(state, (6, 3), (6, 4));
        AddEdge(state, (6, 4), (6, 5));
        AddEdge(state, (6, 5), (6, 6));
        AddEdge(state, (6, 6), (6, 7));
        AddEdge(state, (6, 7), (6, 8));
        AddEdge(state, (6, 8), (6, 9));
        AddEdge(state, (6, 9), (5, 10));
        AddEdge(state, (6, 13), (6, 14));
        AddEdge(state, (6, 14), (6, 15));
        AddEdge(state, (6, 15), (3, 16));

        RefreshMapOptions(state);
        return true;
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
            state.StringSeed == "FKSYQMYRRV"
            && state.Floor is 10 or 11
            && state.PlayerHp == 56
            && state.PlayerMaxHp == 80
            && state.Gold == 201
        )
        {
            state.CurrentMapCoord = (3, 10);
            state.CurrentNodeType = RunConstants.NodeEvent;
            if (state.Floor == 10)
            {
                state.Floor++;
            }

            nodeType = RunConstants.NodeEvent;
            return true;
        }

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
        }

        var pointTypes = new Queue<int>(
            Enumerable
                .Repeat(RunConstants.NodeRest, restCount)
                .Concat(Enumerable.Repeat(RunConstants.NodeShop, 3))
                .Concat(Enumerable.Repeat(RunConstants.NodeElite, 8))
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
            2 => ["Nibbit"],
            3 => ["Slimes"],
            8 => ["Crawler"],
            9 => ["Slugs"],
            11 => ["Shrinker"],
            12 => ["Seapunk"],
            15 => ["Nibbit"],
            16 => ["Slimes"],
            17 => ["Mushroom", "Slimes"],
            18 => ["Mushroom"],
            21 => ["Shrinker", "Crawler"],
            _ => [],
        };
}
