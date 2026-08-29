using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Red Skull is the observable half of `AfterCurrentHpChanged`: it re-asks "am I under
/// half?" on every current-HP change and applies or REMOVES three Strength accordingly.
/// Which makes it the probe for whether each HP route dispatches at all.
/// </summary>
public class RedSkullTests
{
    private static Fight Wounded(int hp, int maxHp = 80)
    {
        var fight = Fight.WithRelics(RelicEffects.RedSkull);
        fight.State.PlayerMaxHp = maxHp;
        fight.State.PlayerHp = hp;
        return fight;
    }

    private static int Strength(Fight fight) =>
        BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength);

    /// <summary>
    /// Poison is the route with no card played, so a turn could pass entirely under half
    /// with the relic still unarmed.
    /// </summary>
    [Fact]
    public void PoisonTickingUnderHalfArmsIt()
    {
        var fight = Wounded(45);
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Poison, 10);
        fight.State.Hand.Clear();

        fight.EndTurn();

        Assert.True(fight.State.PlayerHp <= 40);
        Assert.Equal(3, Strength(fight));
    }

    /// <summary>
    /// The removal is the half a "grant Strength below 50%" reading loses: healing back
    /// over the line hands it straight back.
    /// </summary>
    [Fact]
    public void HealingBackOverHalfTakesItAway()
    {
        var fight = Wounded(30);
        CardEffects.LoseHp(fight.State, 1);
        Assert.Equal(3, Strength(fight));

        CardEffects.HealPlayer(fight.State, 20);

        Assert.Equal(49, fight.State.PlayerHp);
        Assert.Equal(0, Strength(fight));
    }

    /// <summary>Self-damage is a route too -- Offering can put you under the line.</summary>
    [Fact]
    public void SelfDamageArmsIt()
    {
        var fight = Wounded(43);

        CardEffects.LoseHp(fight.State, 6);

        Assert.Equal(37, fight.State.PlayerHp);
        Assert.Equal(3, Strength(fight));
    }

    [Fact]
    public void ThornsArmsIt()
    {
        var fight = Wounded(42);
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Thorns, 5);
        fight.State.Enemies[0].Hp = 40;

        // The enemy's attack takes the thorns back off the player.
        CardEffects.DealDamageToPlayer(fight.State, 0);
        CardEffects.LoseHp(fight.State, 3);

        Assert.Equal(3, Strength(fight));
    }

    /// <summary>
    /// `GainMaxHp` heals AND raises the ceiling, so it moves the player and the line at
    /// once -- and the line moves further, which is what disarms it.
    /// </summary>
    [Fact]
    public void GainingMaxHpCanDisarmIt()
    {
        var fight = Wounded(40);
        CardEffects.LoseHp(fight.State, 0);
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Strength, 0);
        CardEffects.HealPlayer(fight.State, 0);

        // 40 of 80 is exactly the line, so the relic is on.
        CardEffects.LoseHp(fight.State, 1);
        Assert.Equal(3, Strength(fight));

        // 43 of 84: the threshold moved to 42 and the player is above it.
        CardEffects.GainMaxHp(fight.State, 4);

        Assert.Equal(84, fight.State.PlayerMaxHp);
        Assert.Equal(43, fight.State.PlayerHp);
        Assert.Equal(0, Strength(fight));
    }

    /// <summary>
    /// `CreatureCmd.SetMaxHp` does NOT dispatch the hook, so a shrinking maximum does not
    /// re-ask the question even though it moves the threshold. The direction that matters
    /// is losing max HP while ARMED: a smaller ceiling makes the same HP a larger fraction
    /// of it, so the player crosses back over half without the relic noticing and keeps
    /// three Strength it would otherwise hand back.
    ///
    /// Both halves are asserted, because the point is that this is the game declining to
    /// ask rather than the emulator failing to notice: run the hook by hand and the
    /// Strength does go.
    /// </summary>
    [Fact]
    public void LosingMaxHpDoesNotDisarmIt()
    {
        var fight = Wounded(30);
        CardEffects.LoseHp(fight.State, 0);
        CardEffects.HealPlayer(fight.State, 0);
        RelicEffects.ApplyAfterPlayerHpChanged(fight.State);
        Assert.Equal(3, Strength(fight));

        // What ApplyPaperCuts does. 30 of 50 is above the line, but nothing asks.
        fight.State.PlayerMaxHp = 50;
        fight.State.PlayerHp = System.Math.Min(fight.State.PlayerHp, fight.State.PlayerMaxHp);

        Assert.Equal(3, Strength(fight));

        RelicEffects.ApplyAfterPlayerHpChanged(fight.State);
        Assert.Equal(0, Strength(fight));
    }
}
