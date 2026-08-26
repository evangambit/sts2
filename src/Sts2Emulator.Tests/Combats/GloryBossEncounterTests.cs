using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// AeonglassBoss: one Aeonglass, holding ArtifactPower(3) from the moment it arrives.
/// </summary>
public class AeonglassTests
{
    private static Fight Glass(int ascension = 8) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.Aeonglass, ascension);

    /// <summary>
    /// EBB -> EYE_LASERS -> INCREASING_INTENSITY, cycling.
    ///
    /// INCREASING_INTENSITY declares StatusIntent before BuffIntent, so it announces as a
    /// Debuff whose number is WitherAmount — the Withers it adds — and not as the Buff of
    /// 2 the emulator reported. The Strength it takes is
    /// <c>IncreasingIntensityBaseStrength + AdditionalStrength</c>, and AdditionalStrength
    /// counts the times the move has already run, so the second helping is one larger than
    /// the first: the announcements climb by 4 then by 5 at A9, not by a flat 4 twice.
    /// </summary>
    [Theory]
    [InlineData(8, 26, 11, 1, 3)]
    [InlineData(9, 32, 12, 2, 4)]
    public void ItEbbsLasersThenIntensifies(
        int ascension,
        int ebb,
        int lasers,
        int wither,
        int strength
    )
    {
        var fight = Glass(ascension);
        var seen = GloryNormal.Cycle(fight, fight.State.Enemies[0], 6);

        Assert.Equal(
            [
                (IntentType.Attack, ebb, 1),
                // EYE_LASERS: MultiAttackIntent(EyeLasersDamage, 2), which had been folded.
                (IntentType.Attack, lasers * 2, 2),
                (IntentType.Debuff, wither, 1),
                (IntentType.Attack, ebb + strength, 1),
                (IntentType.Attack, (lasers + strength) * 2, 2),
                (IntentType.Debuff, wither, 1),
            ],
            seen
        );
    }

    /// <summary>
    /// EbbMove gains EbbBlock, a flat 33, and INCREASING_INTENSITY does not. The block
    /// used to sit in the buff branch, which meant the intensity move gained it and the
    /// EBB that owns it did not.
    /// </summary>
    [Fact]
    public void OnlyTheEbbBlocks()
    {
        var fight = Glass();
        var glass = fight.State.Enemies[0];
        fight.State.PlayerHp = 9999;

        fight.EndTurn(); // EBB
        Assert.Equal(33, glass.Block);

        fight.State.PlayerHp = 9999;
        fight.EndTurn(); // EYE_LASERS
        Assert.Equal(0, glass.Block);

        fight.State.PlayerHp = 9999;
        fight.EndTurn(); // INCREASING_INTENSITY
        Assert.Equal(0, glass.Block);
    }

    /// <summary>
    /// INCREASING_INTENSITY puts WitherAmount Withers in the discard and climbs its own
    /// Strength. The old handler dealt attack damage on this turn and applied an
    /// EbbPower(3) that nothing in the current build ever applies — and <c>BuffId.Ebb</c>
    /// was read nowhere, so it was a debuff the player carried and never paid.
    /// </summary>
    [Theory]
    [InlineData(8, 1, 3)]
    [InlineData(9, 2, 4)]
    public void TheIntensityMoveWithersAndClimbs(int ascension, int wither, int strength)
    {
        var fight = Glass(ascension);
        var glass = fight.State.Enemies[0];
        fight.State.PlayerHp = 9999;

        fight.Turns(3); // EBB, EYE_LASERS, INCREASING_INTENSITY

        Assert.Equal(wither, GloryNormal.Copies(fight, ST.Wither));
        Assert.Equal(strength, BuffSystem.Get(glass.Buffs, BuffId.Strength));

        for (int turn = 0; turn < 3; turn++)
        {
            fight.State.PlayerHp = 9999;
            glass.Hp = 9999;
            fight.EndTurn();
        }

        Assert.Equal(wither * 2, GloryNormal.Copies(fight, ST.Wither));
        Assert.Equal(strength * 2 + 1, BuffSystem.Get(glass.Buffs, BuffId.Strength));
    }
}
