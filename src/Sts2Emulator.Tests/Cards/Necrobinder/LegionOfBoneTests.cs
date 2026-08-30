using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/LegionOfBone.cs: SummonVar(6) upgrading by 2, summoned
// once per LIVING player creature. It is MultiplayerOnly, so a solo run only meets it
// through a debug grant — but the summon is not gated on the party, and a live capture
// grew Osty by six.
//
// The emulator had it sharing an `ApplyBaseDamageAndBlock` arm, and the card has neither
// damage nor block, so playing it did nothing whatsoever.
public class LegionOfBoneTests
{
    private const int LegionOfBone = 283;

    [Fact]
    public void ItSummonsForTheSoloPlayer()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 200);
        CardEffects.SummonOsty(fight.State, 1);
        fight.State.Hand.Add(new CardInstance(LegionOfBone, false));

        fight.Play(0);

        Assert.Equal(7, fight.State.OstyMaxHp);
    }

    [Fact]
    public void TheUpgradeSummonsForEight()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 200);
        CardEffects.SummonOsty(fight.State, 1);
        fight.State.Hand.Add(new CardInstance(LegionOfBone, true));

        fight.Play(0);

        Assert.Equal(9, fight.State.OstyMaxHp);
    }
}
