using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

public class DragonFruitTests
{
    private static RunState WithFruit(params int[] alsoHeld)
    {
        var state = new RunState
        {
            PlayerMaxHp = 80,
            PlayerHp = 50,
            Gold = 0,
        };
        state.Relics.Add(new RelicInstance(RelicEffects.DragonFruit));
        foreach (int id in alsoHeld)
        {
            state.Relics.Add(new RelicInstance(id));
        }

        return state;
    }

    /// <summary>
    /// `CreatureCmd.GainMaxHp` heals by what it grants, so the player is 1/1 better off
    /// rather than 1 further from full.
    /// </summary>
    [Fact]
    public void GainingGoldRaisesTheCapAndHeals()
    {
        var state = WithFruit();

        RunNonCombatEffects.GainGold(state, 15);

        Assert.Equal(15, state.Gold);
        Assert.Equal(81, state.PlayerMaxHp);
        Assert.Equal(51, state.PlayerHp);
    }

    /// <summary>Once per gain EVENT: a boss's hundred is worth the same one as a mushroom's fifteen.</summary>
    [Fact]
    public void ItIsPerGainNotPerGold()
    {
        var big = WithFruit();
        RunNonCombatEffects.GainGold(big, 100);

        var small = WithFruit();
        RunNonCombatEffects.GainGold(small, 15);

        Assert.Equal(81, big.PlayerMaxHp);
        Assert.Equal(81, small.PlayerMaxHp);
    }

    [Fact]
    public void ThreeSeparateGainsAreThreeMaxHp()
    {
        var state = WithFruit();

        RunNonCombatEffects.GainGold(state, 5);
        RunNonCombatEffects.GainGold(state, 5);
        RunNonCombatEffects.GainGold(state, 5);

        Assert.Equal(83, state.PlayerMaxHp);
    }

    /// <summary>
    /// `PlayerCmd.GainGold` returns on `!(amount > 0m)` BEFORE `AfterGoldGained`, and
    /// Ectoplasm's ModifyGoldGained returns 0 -- so it does not merely take the gold, it
    /// stops the relic firing at all.
    /// </summary>
    [Fact]
    public void EctoplasmSuppressesItEntirely()
    {
        var state = WithFruit(RelicEffects.Ectoplasm);

        RunNonCombatEffects.GainGold(state, 100);

        Assert.Equal(0, state.Gold);
        Assert.Equal(80, state.PlayerMaxHp);
        Assert.Equal(50, state.PlayerHp);
    }

    [Fact]
    public void AZeroGainFiresNothing()
    {
        var state = WithFruit();

        RunNonCombatEffects.GainGold(state, 0);

        Assert.Equal(80, state.PlayerMaxHp);
    }

    /// <summary>Bowler Hat raises the amount, so the gain is still one gain and still +1.</summary>
    [Fact]
    public void BowlerHatRaisesTheGoldButNotTheMaxHp()
    {
        var state = WithFruit(RelicEffects.BowlerHat);

        RunNonCombatEffects.GainGold(state, 20);

        Assert.Equal(25, state.Gold);
        Assert.Equal(81, state.PlayerMaxHp);
    }

    /// <summary>`LoseGold` is a different command and dispatches no hook.</summary>
    [Fact]
    public void SpendingGoldDoesNothing()
    {
        var state = WithFruit();
        RunNonCombatEffects.GainGold(state, 50);
        int maxHp = state.PlayerMaxHp;

        state.Gold -= 30;

        Assert.Equal(maxHp, state.PlayerMaxHp);
    }

    /// <summary>
    /// Gold gained mid-combat is the same hook. Wish is the reachable one: the combat
    /// chokepoint has to fire it too, or the relic works everywhere except the two places
    /// a card hands you gold.
    /// </summary>
    [Fact]
    public void GoldGainedInCombatCountsToo()
    {
        const int wish = 541;
        var fight = Fight.WithRelics(RelicEffects.DragonFruit);
        fight.State.Hand = [new CardInstance(wish, false)];
        fight.State.Energy = 3;
        fight.State.PlayerHp = 50;
        int maxHpBefore = fight.State.PlayerMaxHp;

        fight.Play(0);

        Assert.Equal(25, fight.State.PlayerGold);
        Assert.Equal(maxHpBefore + 1, fight.State.PlayerMaxHp);
        Assert.Equal(51, fight.State.PlayerHp);
    }
}
