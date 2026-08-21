using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// EncounterModel seeds its own stream from the run seed, TotalFloor and a hash of the
/// encounter's entry id, and every encounter that rolls its own roster reads it. The run
/// engine used to hand back a seed for Slimes alone and a constant for everything else,
/// so Slithering Strangler's secondary enemy -- a whole extra monster, or two -- came
/// out of a fixed stream that had nothing to do with the run.
/// </summary>
public class EncounterRngWiringTests
{
    private static List<(int Hp, int MaxHp)> RosterAtFloor(int floor)
    {
        var engine = new RunEngine();
        engine.Reset("QS2GYXRKWN");
        engine.State.Floor = floor;
        engine.State.CurrentNodeType = RunConstants.NodeNormal;
        engine.StartCombat(
            RunConstants.StarterDeckIds,
            RunConstants.SlitheringStranglerEncounterId,
            [],
            playerHp: 64,
            playerMaxHp: 80,
            potionIds: [],
            playerGold: 99
        );
        return engine.State.ActiveCombat!.Enemies.Select(enemy => (enemy.Hp, enemy.MaxHp)).ToList();
    }

    [Fact]
    public void TheFloorDecidesTheRoster()
    {
        // The roster is a NextItem over three secondary-enemy types on the encounter's
        // own stream, so a different floor is a different seed is a different roster.
        var rosters = Enumerable.Range(1, 16).Select(RosterAtFloor).ToList();

        Assert.True(
            rosters.Select(roster => string.Join(",", roster)).Distinct().Count() > 1,
            "every floor produced the same roster, so the encounter stream is not seeded from it"
        );
    }

    [Fact]
    public void TheStranglerAlwaysBringsCompanyAndItself()
    {
        foreach (int floor in Enumerable.Range(1, 16))
        {
            var roster = RosterAtFloor(floor);

            // One or two secondary enemies, then the Strangler.
            Assert.InRange(roster.Count, 2, 3);
            Assert.Equal(54, roster[^1].MaxHp);
        }
    }
}
