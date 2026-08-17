// AUTO-GENERATED — do not edit. Re-run scripts/generate_capture_tests.py.
//
// Expected values come from live game captures in tests/fixtures/run_generation/,
// never from the emulator. Re-capture a fixture, re-run the generator, and these
// follow automatically. The full row/column/type map comparison lives in
// tests/python/test_live_fixtures.py against the same fixtures.
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

public class RunGenerationCaptureTests
{
    /// <summary>
    /// Live capture: seed "AAB" at ascension 8,
    /// ACT.OVERGROWTH, game v0.107.1 (build 23811903).
    /// Source: tests/fixtures/run_generation/AAB.json
    /// </summary>
    [Fact]
    public void RunGeneration_MatchesCapture_Aab()
    {
        var engine = new RunEngine();
        engine.Reset("AAB");
        var s = engine.State;

        Assert.Equal(RunConstants.ActOvergrowth, s.Act);
        Assert.Equal(new[] { 8, 3, 2, 15, 29, 16, 21, 14, 20, 5, 17, 27, 18, 19, 28 }, s.NormalEncounterSequence);
        Assert.Equal(new[] { 62, 68, 65, 68, 65, 62, 65, 68, 62, 68, 65, 62, 68, 65, 62 }, s.EliteEncounterSequence);
        Assert.Equal(74, s.BossEncounterId);
        Assert.Equal(61, s.MapNodes.Count);
        Assert.Equal(
            new[] { 1, 3, 5, 4, 3, 2, 3, 5, 6, 5, 5, 3, 5, 4, 3, 3, 1 },
            Enumerable
                .Range(0, 17)
                .Select(row => s.MapNodes.Values.Count(n => n.Row == row))
                .ToArray()
        );
    }

    /// <summary>
    /// Live capture: seed "UNS55LCMKP" at ascension 8,
    /// ACT.UNDERDOCKS, game v0.107.1 (build 23811903).
    /// Source: tests/fixtures/run_generation/UNS55LCMKP.json
    /// </summary>
    [Fact]
    public void RunGeneration_MatchesCapture_Uns55Lcmkp()
    {
        var engine = new RunEngine();
        engine.Reset("UNS55LCMKP");
        var s = engine.State;

        Assert.Equal(RunConstants.ActUnderdocks, s.Act);
        Assert.Equal(new[] { 10, 12, 9, 12, 24, 30, 0, 9, 7, 23, 26, 25, 6, 23, 24 }, s.NormalEncounterSequence);
        Assert.Equal(new[] { 86, 72, 67, 72, 86, 67, 86, 67, 72, 67, 86, 72, 86, 67, 72 }, s.EliteEncounterSequence);
        Assert.Equal(84, s.BossEncounterId);
        Assert.Equal(64, s.MapNodes.Count);
        Assert.Equal(
            new[] { 1, 3, 4, 4, 4, 4, 3, 4, 5, 6, 5, 4, 4, 4, 5, 3, 1 },
            Enumerable
                .Range(0, 17)
                .Select(row => s.MapNodes.Values.Count(n => n.Row == row))
                .ToArray()
        );
    }
}
