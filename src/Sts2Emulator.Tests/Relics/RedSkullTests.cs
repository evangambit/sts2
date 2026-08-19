using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Relics/RedSkull.cs: PowerVar<StrengthPower>(3m) while
// CurrentHp is at or below DynamicVar("HpThreshold", 50m) percent of MaxHp, applied and
// removed as HP crosses the line rather than checked once.
public class RedSkullTests
{
    [Fact]
    public void GrantsNoStrengthAboveHalfHealth()
    {
        var fight = Fight.WithRelics(RelicEffects.RedSkull);

        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.Strength));
    }

    [Fact]
    public void GrantsThreeStrengthOnceHalfHealthIsCrossed()
    {
        var fight = Fight.WithRelics(RelicEffects.RedSkull);
        fight.State.PlayerHp = fight.State.PlayerMaxHp / 2;

        RelicEffects.ApplyAfterPlayerHpChanged(fight.State);

        Assert.Equal(3, fight.PlayerBuffAmount(BuffId.Strength));
    }

    [Fact]
    public void TakesTheStrengthBackWhenHealedAboveTheLine()
    {
        var fight = Fight.WithRelics(RelicEffects.RedSkull);
        fight.State.PlayerHp = fight.State.PlayerMaxHp / 2;
        RelicEffects.ApplyAfterPlayerHpChanged(fight.State);

        fight.State.PlayerHp = fight.State.PlayerMaxHp;
        RelicEffects.ApplyAfterPlayerHpChanged(fight.State);

        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.Strength));
    }

    [Fact]
    public void DoesNotStackWhileHpStaysLow()
    {
        var fight = Fight.WithRelics(RelicEffects.RedSkull);
        fight.State.PlayerHp = 10;

        RelicEffects.ApplyAfterPlayerHpChanged(fight.State);
        RelicEffects.ApplyAfterPlayerHpChanged(fight.State);

        Assert.Equal(3, fight.PlayerBuffAmount(BuffId.Strength));
    }
}
