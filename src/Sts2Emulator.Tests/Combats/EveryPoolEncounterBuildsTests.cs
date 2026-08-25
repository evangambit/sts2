using System.Collections.Generic;
using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Every encounter an act can deal must at least BUILD.
/// </summary>
/// <remarks>
/// A weaker bar than the per-encounter suites — it says nothing about rosters, HP or
/// intents — but it is the bar below which nothing else can be true, and two encounters
/// were under it. `ExoskeletonsNormal` sits in Hive's normal pool with no case in the
/// roster switch at all, so an act-2 run that drew it did not fight it wrongly: it threw
/// `ArgumentOutOfRangeException` out of `CombatFactory`. `TestSubject` is a Glory boss
/// whose monster was on `extract_data.py`'s exclusion list next to `BigDummy` and the
/// battle-friends, so `KE.TestSubject` named a row in `Enemies.g.cs` that did not exist.
///
/// Neither was reachable until the pools became real (E82) — which is the point: a
/// coverage list built from what the code HAS cannot see what the game deals that the
/// code has not got. This one is built from the pools.
/// </remarks>
public class EveryPoolEncounterBuildsTests
{
    private static readonly int[] Acts =
    [
        RunConstants.ActOvergrowth,
        RunConstants.ActUnderdocks,
        RunConstants.ActHive,
        RunConstants.ActGlory,
    ];

    private static readonly string[] Kinds = ["Weak", "Normal", "Elite", "Boss"];

    [Fact]
    public void EveryEncounterInEveryActsPoolsCanBeBuilt()
    {
        var broken = new List<string>();
        foreach (int act in Acts)
        {
            foreach (string kind in Kinds)
            {
                foreach (int id in GeneratedData.EncounterTags.Pool(act, kind))
                {
                    var failure = Record.Exception(() => Fight.Encounter(id));
                    if (failure is not null)
                    {
                        broken.Add(
                            $"act {act} {kind} {(CombatFactory.ActOneEncounter)id}: "
                                + failure.GetType().Name
                        );
                    }
                }
            }
        }

        Assert.Equal([], broken.Distinct().Order().ToList());
    }

    [Fact]
    public void EveryEncounterInAPoolFieldsAtLeastOneLivingEnemy()
    {
        var empty = new List<string>();
        foreach (int act in Acts)
        {
            foreach (string kind in Kinds)
            {
                foreach (int id in GeneratedData.EncounterTags.Pool(act, kind))
                {
                    var fight = Fight.Encounter(id);
                    if (!fight.State.Enemies.Any(enemy => enemy.Hp > 0))
                    {
                        empty.Add($"act {act} {kind} {(CombatFactory.ActOneEncounter)id}");
                    }
                }
            }
        }

        Assert.Equal([], empty.Distinct().Order().ToList());
    }
}
