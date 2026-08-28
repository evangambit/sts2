using System;
using System.Collections.Generic;
using System.Diagnostics;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Where a step's time and allocation go. Skipped by default; run it on demand.
/// </summary>
/// <remarks>
/// <code>dotnet test src/Sts2Emulator.Tests --filter StepCostProbe -c Release</code>
/// then read /tmp/sts2-step-cost.txt.
///
/// This lives here rather than in `scripts/` because the interesting number is
/// <c>GC.GetAllocatedBytesForCurrentThread</c>, which only C# can see — and allocation is
/// the thing worth watching. A step that allocates kilobytes is paying a GC bill that no
/// amount of algorithmic tidying will refund.
///
/// It is skipped rather than deleted because a performance claim goes stale silently.
/// The numbers in docs/agent-interface.md came from here and should be re-taken from here.
/// </remarks>
public class StepCostProbe
{
    private const string OutputPath = "/tmp/sts2-step-cost.txt";
    private const int Iterations = 500;
    private const int Warmup = 30;

    private static void Measure(List<string> rows, string label, Action<Fight> setup)
    {
        for (int i = 0; i < Warmup; i++)
        {
            var warm = Fight.Encounter(CombatFactory.ActOneEncounter.Chompers);
            setup(warm);
            warm.EndTurn();
        }

        long bytes = 0;
        double micros = 0;
        for (int i = 0; i < Iterations; i++)
        {
            var fight = Fight.Encounter(CombatFactory.ActOneEncounter.Chompers);
            fight.State.PlayerHp = 9999;
            setup(fight);
            long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
            long beforeTicks = Stopwatch.GetTimestamp();
            fight.EndTurn();
            long afterTicks = Stopwatch.GetTimestamp();
            bytes += GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
            micros += (afterTicks - beforeTicks) * 1e6 / Stopwatch.Frequency;
        }

        rows.Add(
            $"{label, -46}{micros / Iterations, 9:F1} us{bytes / (double)Iterations / 1024, 9:F1} KB"
        );
    }

    [Fact(Skip = "Performance probe; run explicitly with --filter StepCostProbe -c Release")]
    public void WhereDoesAStepGo()
    {
        var rows = new List<string> { $"{"scenario", -46}{"time", 12}{"alloc", 12}" };

        // The whole run-level step, for scale: a combat step is ~60% of a run's time.
        var obs = new int[RunConstants.RunObsSize];
        var engine = new RunEngine();
        engine.Reset("PROBE00001");
        var obsWatch = Stopwatch.StartNew();
        for (int i = 0; i < 10_000; i++)
        {
            engine.WriteObservation(obs);
        }

        obsWatch.Stop();
        rows.Add(
            $"{"WriteObservation (not the problem)", -46}"
                + $"{obsWatch.ElapsedTicks * 1e6 / Stopwatch.Frequency / 10_000, 9:F1} us{0.0, 9:F1} KB"
        );

        Measure(rows, "end turn, baseline", _ => { });
        Measure(
            rows,
            "end turn, combat already over",
            fight =>
            {
                foreach (var enemy in fight.State.Enemies)
                {
                    enemy.Hp = 0;
                }
            }
        );
        Measure(
            rows,
            "end turn, one enemy instead of two",
            fight => fight.State.Enemies[^1].Hp = 0
        );
        Measure(
            rows,
            "end turn, draw pile stocked (no reshuffle)",
            fight =>
            {
                for (int i = 0; i < 30; i++)
                {
                    fight.State.DrawPile.Add(new CardInstance(472, false));
                }
            }
        );

        rows.Add("");
        rows.Add("Read it this way: 'combat already over' skips the START OF THE NEXT");
        rows.Add("PLAYER TURN, and that single difference is essentially the whole cost.");
        rows.Add("It is not the enemy phase (one enemy costs more than two), not the");
        rows.Add("reshuffle (same allocation either way), and not the observation.");

        System.IO.File.WriteAllLines(OutputPath, rows);
    }
}
