using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The Sunken Statue: one named relic, or a pool of gold paid for in blood.
///
/// Both options were wrong in a way the outcome fixtures could not see on their own.
/// Grab the Sword rolled a relic off the reward pool where the game obtains one named
/// relic outright (<c>RelicCmd.Obtain&lt;SwordOfStone&gt;</c>), which the fixtures missed
/// because nothing compared relics until this suite's sibling assertion was added. Dive
/// into the Water paid a flat 111 gold and charged 12 HP, where the game rolls the gold
/// from the event's own stream and charges 7.
/// </summary>
public class SunkenStatueTests
{
    private static RunEngine AtTheStatue(string seed)
    {
        var engine = new RunEngine();
        engine.Reset(seed);
        engine.State.EventId = RunConstants.EventSunkenStatue;
        engine.State.Phase = RunPhase.Event;
        return engine;
    }

    [Fact]
    public void GrabbingTheSwordObtainsTheSwordOfStoneAndNothingElse()
    {
        var engine = AtTheStatue("ABCDEF");
        int hp = engine.State.PlayerHp;
        int gold = engine.State.Gold;

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(RunNonCombatEffects.NamedRelic("SwordOfStone"), engine.State.Relics[^1].DefId);
        Assert.Equal(hp, engine.State.PlayerHp);
        Assert.Equal(gold, engine.State.Gold);
    }

    [Fact]
    public void DivingPaysTheRolledGoldAndCostsSevenHp()
    {
        var engine = AtTheStatue("ABCDEF");
        int hp = engine.State.PlayerHp;
        int gold = engine.State.Gold;
        int rolled = RunNonCombatEffects.SunkenStatueGold(engine.State);

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

        Assert.Equal(gold + rolled, engine.State.Gold);
        Assert.Equal(hp - 7, engine.State.PlayerHp);
        Assert.Single(engine.State.Relics);
    }

    /// <summary>
    /// <c>CalculateVars</c> is <c>GoldVar(111) + Rng.NextInt(-10, 11)</c> on the event's
    /// own stream, so the amount is a fixed function of the seed and lands in 101..121.
    /// The live capture on "ABCDEF" paid 109.
    /// </summary>
    [Fact]
    public void TheGoldIsTheSeedsRollAroundOneHundredAndEleven()
    {
        Assert.Equal(109, RunNonCombatEffects.SunkenStatueGold(AtTheStatue("ABCDEF").State));

        foreach (string seed in new[] { "ABCDEF", "AAB", "UNS55LCMKP", "HEADLESS1" })
        {
            int gold = RunNonCombatEffects.SunkenStatueGold(AtTheStatue(seed).State);
            Assert.InRange(gold, 101, 121);
        }
    }

    [Fact]
    public void TheRollIsStableAcrossReadsOfTheSameRun()
    {
        var engine = AtTheStatue("AAB");
        int first = RunNonCombatEffects.SunkenStatueGold(engine.State);

        Assert.Equal(first, RunNonCombatEffects.SunkenStatueGold(engine.State));
        Assert.Equal(first, RunNonCombatEffects.SunkenStatueGold(engine.State));
    }
}
