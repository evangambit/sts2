using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// `StranglePower` is a DEBUFF that taxes the player for playing cards: the enemy
/// carrying it takes its amount as `Unblockable | Unpowered` damage after every card the
/// player plays, until the enemy's own side turn ends.
///
/// Vulnerable 2 used to stand in for it, which is a different effect entirely — and the
/// stand-in did not scale on upgrade even though the source does.
/// </summary>
public class StrangleTests
{
    [Fact]
    public void ItAppliesTwoAndThreeUpgraded()
    {
        var plain = Fight.Hand(new CardInstance(SI.Strangle, false)).Energy(3).Enemy(hp: 90);
        plain.Play(0);
        Assert.Equal(2, BuffSystem.Get(plain.Enemy0.Buffs, BuffId.Strangle));

        var upgraded = Fight.Hand(new CardInstance(SI.Strangle, true)).Energy(3).Enemy(hp: 90);
        upgraded.Play(0);
        Assert.Equal(3, BuffSystem.Get(upgraded.Enemy0.Buffs, BuffId.Strangle));
    }

    /// <summary>
    /// The snapshot is the whole mechanism: `BeforeCardPlayed` records the amount and
    /// `AfterCardPlayed` pays it, so the card that APPLIES the Strangle does not make its
    /// target pay for it.
    /// </summary>
    [Fact]
    public void TheCardThatAppliesItDoesNotTriggerIt()
    {
        var fight = Fight.Hand(new CardInstance(SI.Strangle, false)).Energy(3).Enemy(hp: 90);

        fight.Play(0);

        // 8 damage from the card and nothing more; the Strangle has not been paid yet.
        Assert.Equal(82, fight.Enemy0.Hp);
    }

    [Fact]
    public void EveryLaterCardTaxesTheEnemy()
    {
        var fight = Fight
            .Hand(
                new CardInstance(SI.Strangle, false),
                new CardInstance(IC.DefendIronclad, false),
                new CardInstance(IC.DefendIronclad, false)
            )
            .Energy(9)
            .Enemy(hp: 90);
        fight.Play(0);
        int afterStrangle = fight.Enemy0.Hp;

        fight.Play(0);
        Assert.Equal(afterStrangle - 2, fight.Enemy0.Hp);

        fight.Play(0);
        Assert.Equal(afterStrangle - 4, fight.Enemy0.Hp);
    }

    /// <summary>Unblockable: the enemy's own block does not stop it.</summary>
    [Fact]
    public void EnemyBlockDoesNotStopIt()
    {
        var fight = Fight
            .Hand(new CardInstance(SI.Strangle, false), new CardInstance(IC.DefendIronclad, false))
            .Energy(9)
            .Enemy(hp: 90);
        fight.Play(0);
        fight.Enemy0.Block = 50;
        int before = fight.Enemy0.Hp;

        fight.Play(0);

        Assert.Equal(before - 2, fight.Enemy0.Hp);
        Assert.Equal(50, fight.Enemy0.Block);
    }

    /// <summary>
    /// `AfterSideTurnEnd` removes it when its OWNER's turn ends, so it taxes the turn it
    /// landed on and no longer.
    /// </summary>
    [Fact]
    public void ItIsGoneAfterTheEnemyTurn()
    {
        var fight = Fight.Hand(new CardInstance(SI.Strangle, false)).Energy(9).Enemy(hp: 90);
        fight.Play(0);
        Assert.Equal(2, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Strangle));

        fight.State.Hand.Clear();
        fight.EndTurn();

        Assert.Equal(0, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Strangle));
    }

    /// <summary>A second Strangle stacks, and pays the OLD amount on the play that stacks it.</summary>
    [Fact]
    public void StackingPaysTheOldAmountFirst()
    {
        var fight = Fight
            .Hand(new CardInstance(SI.Strangle, false), new CardInstance(SI.Strangle, false))
            .Energy(9)
            .Enemy(hp: 90);
        fight.Play(0);
        int afterFirst = fight.Enemy0.Hp;

        fight.Play(0);

        // The second Strangle deals its 8, plus the FIRST Strangle's 2 -- not 4.
        Assert.Equal(afterFirst - 10, fight.Enemy0.Hp);
        Assert.Equal(4, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Strangle));
    }
}
