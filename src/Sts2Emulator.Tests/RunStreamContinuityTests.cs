using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Streams the run owns but combat consumes. Monster move selection reads the run's
/// "monster_ai" stream by name -- FlutterPower reaches for Monster.RunRng.MonsterAi --
/// so it has to span the whole run. Handing each combat a stream restarted at zero put
/// every branching monster on the wrong draw: Mawler's third choice came up Rip and
/// Tear where the game rolled Claw.
/// </summary>
public class RunStreamContinuityTests
{
    private static RunEngine StartedRun()
    {
        var engine = new RunEngine();
        engine.Reset("QS2GYXRKWN");
        return engine;
    }

    private static void EnterCombat(RunEngine engine, int encounterId)
    {
        engine.StartCombat(
            RunConstants.StarterDeckIds,
            encounterId,
            [],
            playerHp: 64,
            playerMaxHp: 80,
            potionIds: [],
            playerGold: 99
        );
    }

    [Fact]
    public void CombatPicksUpTheMonsterAiStreamWhereTheRunLeftIt()
    {
        var engine = StartedRun();
        engine.State.Rng.MonsterAi.AdvanceToCallCount(7);

        EnterCombat(engine, RunConstants.SlitheringStranglerEncounterId);

        var aiRng = Assert.IsType<CountingRandom>(engine.State.ActiveCombat!.AiRng);
        Assert.Equal(7, aiRng.CallCount);
    }

    [Fact]
    public void AFreshRunStartsTheMonsterAiStreamAtZero()
    {
        var engine = StartedRun();

        EnterCombat(engine, RunConstants.SlitheringStranglerEncounterId);

        var aiRng = Assert.IsType<CountingRandom>(engine.State.ActiveCombat!.AiRng);
        Assert.Equal(0, aiRng.CallCount);
    }
}
