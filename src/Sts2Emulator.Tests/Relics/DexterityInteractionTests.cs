using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Anchor, Horn Cleat and Captain's Wheel all declare
/// <c>BlockVar(..., ValueProp.Unpowered)</c>, so Dexterity must not raise what they give.
/// The emulator granted all three as powered block, which quietly paid the player extra
/// for every point of Dexterity. Grouped here because it is one rule across three relics.
///
/// Anchor is fixed too but has no test here on purpose: it grants its block during combat
/// setup, before any relic that could supply Dexterity has applied, so powered and
/// unpowered give the same answer and a test would pass either way. Horn Cleat and
/// Captain's Wheel fire on later turns, where the difference is real.
/// </summary>
public class RelicUnpoweredBlockTests
{
    [Fact]
    public void HornCleatsBlockIgnoresDexterity()
    {
        var plain = Fight.WithRelics(RelicEffects.HornCleat);
        var dexterous = Fight.WithRelics(RelicEffects.OddlySmoothStone, RelicEffects.HornCleat);
        plain.EndTurn();
        dexterous.EndTurn();

        Assert.Equal(14, plain.State.PlayerBlock);
        Assert.Equal(14, dexterous.State.PlayerBlock);
    }

    [Fact]
    public void CaptainsWheelsBlockIgnoresDexterity()
    {
        var plain = Fight.WithRelics(RelicEffects.CaptainsWheel);
        var dexterous = Fight.WithRelics(RelicEffects.OddlySmoothStone, RelicEffects.CaptainsWheel);
        plain.EndTurn();
        plain.EndTurn();
        dexterous.EndTurn();
        dexterous.EndTurn();

        Assert.Equal(18, plain.State.PlayerBlock);
        Assert.Equal(18, dexterous.State.PlayerBlock);
    }
}
