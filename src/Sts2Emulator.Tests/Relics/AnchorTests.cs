using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Relics/Anchor.cs: BeforeCombatStart grants
// BlockVar(10m, ValueProp.Unpowered). The Unpowered is the part worth pinning — it means
// Dexterity leaves the amount alone, which the emulator used to get wrong.
public class AnchorTests
{
    [Fact]
    public void GrantsTenBlockAtCombatStart()
    {
        var fight = Fight.WithRelics(RelicEffects.Anchor);

        Assert.Equal(10, fight.State.PlayerBlock);
    }

    [Fact]
    public void GrantsNothingWithoutTheRelic()
    {
        var fight = Fight.WithRelics();

        Assert.Equal(0, fight.State.PlayerBlock);
    }
}
