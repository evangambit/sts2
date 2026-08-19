using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Relics/Orichalcum.cs: BeforeSideTurnEnd grants
// BlockVar(6m, ValueProp.Unpowered) when the player ends the turn holding no block.
//
// The block is granted at the end of the player turn and is gone by the time control
// comes back — spent on the enemy attack, then cleared. So these measure what it
// absorbed rather than looking for it afterwards.
public class OrichalcumTests
{
    private static Fight Facing(int attackDamage, params int[] relicIds)
    {
        var fight = Fight.WithRelics(relicIds);
        fight.State.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 100,
                MaxHp = 100,
                CurrentIntent = new Intent(IntentType.Attack, attackDamage),
            },
        ];
        return fight;
    }

    [Fact]
    public void AbsorbsSixDamageWhenTheTurnEndsWithNoBlock()
    {
        var plain = Facing(10);
        var withRelic = Facing(10, RelicEffects.Orichalcum);
        int hpBefore = plain.State.PlayerHp;

        plain.EndTurn();
        withRelic.EndTurn();

        Assert.Equal(hpBefore - 10, plain.State.PlayerHp);
        Assert.Equal(hpBefore - 4, withRelic.State.PlayerHp);
    }

    [Fact]
    public void GivesNothingWhenBlockIsAlreadyHeld()
    {
        var withRelic = Facing(10, RelicEffects.Orichalcum);
        withRelic.State.PlayerBlock = 4;
        int hpBefore = withRelic.State.PlayerHp;

        withRelic.EndTurn();

        // Only the 4 already held absorbs anything; the relic stays out of it.
        Assert.Equal(hpBefore - 6, withRelic.State.PlayerHp);
    }

    /// <summary>
    /// The block is Unpowered, so Dexterity leaves it alone. Unlike Anchor, this fires at
    /// the end of a turn, by which point a Dexterity relic has long since applied — so the
    /// distinction is observable here.
    /// </summary>
    [Fact]
    public void TheBlockIgnoresDexterity()
    {
        var plain = Facing(10, RelicEffects.Orichalcum);
        var dexterous = Facing(10, RelicEffects.OddlySmoothStone, RelicEffects.Orichalcum);
        int hpBefore = plain.State.PlayerHp;

        plain.EndTurn();
        dexterous.EndTurn();

        Assert.Equal(1, dexterous.PlayerBuffAmount(BuffId.Dexterity));
        Assert.Equal(hpBefore - 4, plain.State.PlayerHp);
        Assert.Equal(hpBefore - 4, dexterous.State.PlayerHp);
    }
}
