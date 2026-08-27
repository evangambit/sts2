using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// Silent's fifteen unpinned commons, read against
// MegaCrit.Sts2.Core.Models.Cards/*.cs. Commons are the cards an agent actually plays --
// they arrive early, they arrive often, and a wrong one costs more total accuracy over a
// run than a wrong rare does. Four of the fifteen were wrong.

public class BackflipTests
{
    // BlockVar(5m) +3, CardsVar(2). OnUpgrade raises the BLOCK only, so the draw is 2 at
    // both levels.
    [Theory]
    [InlineData(false, 5)]
    [InlineData(true, 8)]
    public void BlocksThenDrawsTwo(bool upgraded, int block)
    {
        var fight = Fight.Hand(Card(SI.Backflip, upgraded)).Energy(1);
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 5; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.StrikeSilent, false));
        }

        fight.Play();

        Assert.Equal(block, fight.State.PlayerBlock);
        Assert.Equal(2, fight.State.Hand.Count);
    }
}

public class BladeDanceTests
{
    // CardsVar(3) +1, Exhaust.
    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 4)]
    public void MakesShivsAndExhausts(bool upgraded, int shivs)
    {
        var fight = Fight.Hand(Card(SI.BladeDance, upgraded)).Energy(1);

        fight.Play();

        Assert.Equal(shivs, fight.State.Hand.Count(c => c.DefId == SI.Shiv));
        Assert.Contains(fight.State.ExhaustPile, c => c.DefId == SI.BladeDance);
    }
}

public class CloakAndDaggerTests
{
    /// <summary>
    /// `OnUpgrade` raises the CARDS, not the block — so an upgraded Cloak and Dagger is
    /// still six block and one more Shiv. The generated table agrees (UpgradeBlock 0),
    /// which is what makes this worth pinning rather than assuming.
    /// </summary>
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void SixBlockAtBothLevelsAndOneMoreShivWhenUpgraded(bool upgraded, int shivs)
    {
        var fight = Fight.Hand(Card(SI.CloakAndDagger, upgraded)).Energy(1);

        fight.Play();

        Assert.Equal(6, fight.State.PlayerBlock);
        Assert.Equal(shivs, fight.State.Hand.Count(c => c.DefId == SI.Shiv));
    }
}

public class DaggerSprayTests
{
    // DamageVar(4m) +2, WithHitCount(2), TargetingAllOpponents.
    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, 6)]
    public void HitsEveryEnemyTwice(bool upgraded, int damage)
    {
        var fight = Fight.Hand(Card(SI.DaggerSpray, upgraded)).Energy(1).Enemy(hp: 60);
        fight.Enemy(hp: 60);

        fight.Play();

        Assert.Equal(60 - damage * 2, fight.Enemy0.Hp);
        Assert.Equal(60 - damage * 2, fight.Enemy1.Hp);
    }
}

public class DeflectTests
{
    // BlockVar(4m) +3, 0 cost.
    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, 7)]
    public void BlocksForFree(bool upgraded, int block)
    {
        var fight = Fight.Hand(Card(SI.Deflect, upgraded)).Energy(0);

        fight.Play();

        Assert.Equal(block, fight.State.PlayerBlock);
    }
}

/// <summary>
/// Dodge and Roll grants next turn's block from what it ACTUALLY gained.
/// </summary>
/// <remarks>
/// `decimal amount = await CreatureCmd.GainBlock(...)` and the power is applied at that
/// amount. `CreatureCmd.GainBlock` returns `modifiedAmount` — Dexterity and Frail already
/// in it, clamped at zero — so the card pays out from the result, not from its printed
/// number. The emulator applied the printed number, which is right only when nothing is
/// modifying block.
/// </remarks>
public class DodgeAndRollTests
{
    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, 6)]
    public void BlocksNowAndAgainNextTurn(bool upgraded, int block)
    {
        var fight = Fight.Hand(Card(SI.DodgeAndRoll, upgraded)).Energy(1);
        fight.State.PlayerHp = 999;

        fight.Play();
        Assert.Equal(block, fight.State.PlayerBlock);

        fight.EndTurn();

        Assert.Equal(block, fight.State.PlayerBlock);
    }

    /// <summary>
    /// Under Dexterity the card gains more, and owes what it gained. The printed 4 is not
    /// the number the power is applied at.
    /// </summary>
    [Fact]
    public void DexterityRaisesBothHalves()
    {
        var fight = Fight.Hand(Card(SI.DodgeAndRoll)).Energy(1);
        fight.State.PlayerHp = 999;
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Dexterity, 3);

        fight.Play();
        Assert.Equal(7, fight.State.PlayerBlock);

        fight.EndTurn();

        Assert.Equal(7, fight.State.PlayerBlock);
    }

    /// <summary>
    /// And under Frail it owes less. Frail is 0.75x on block gained, so 4 becomes 3 both
    /// now and next turn.
    /// </summary>
    [Fact]
    public void FrailLowersBothHalves()
    {
        var fight = Fight.Hand(Card(SI.DodgeAndRoll)).Energy(1);
        fight.State.PlayerHp = 999;
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Frail, 5);

        fight.Play();
        int gained = fight.State.PlayerBlock;
        Assert.Equal(3, gained);

        fight.EndTurn();

        Assert.Equal(gained, fight.State.PlayerBlock);
    }
}

public class FlickFlackTests
{
    // DamageVar(6m) +2, TargetType.AllEnemies, and the Sly keyword.
    [Theory]
    [InlineData(false, 6)]
    [InlineData(true, 8)]
    public void HitsEveryEnemy(bool upgraded, int damage)
    {
        var fight = Fight.Hand(Card(SI.FlickFlack, upgraded)).Energy(1).Enemy(hp: 60);
        fight.Enemy(hp: 60);

        fight.Play();

        Assert.Equal(60 - damage, fight.Enemy0.Hp);
        Assert.Equal(60 - damage, fight.Enemy1.Hp);
    }

    [Fact]
    public void ItIsSly()
    {
        Assert.True(GeneratedData.Cards.Get(SI.FlickFlack).Sly);
    }
}

public class LeadingStrikeTests
{
    /// <summary>
    /// `CardsVar("Shivs", 2)` and `DamageVar(3m)`, and `OnUpgrade` raises the DAMAGE — so
    /// the Shiv count is 2 at both levels. Named vars are the trap here: a card's vars
    /// name its numbers, and reading the upgrade off the wrong one is how Hidden Daggers
    /// grew a third Shiv it does not have.
    /// </summary>
    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 6)]
    public void HitsThenMakesTwoShivsAtBothLevels(bool upgraded, int damage)
    {
        var fight = Fight.Hand(Card(SI.LeadingStrike, upgraded)).Energy(1).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(60 - damage, fight.Enemy0.Hp);
        Assert.Equal(2, fight.State.Hand.Count(c => c.DefId == SI.Shiv));
    }
}

/// <summary>
/// Piercing Wail takes Strength off EVERY enemy, and hands it back.
/// </summary>
/// <remarks>
/// `PiercingWailPower : TemporaryStrengthPower` with `IsPositive => false`. It is a
/// Debuff, so Artifact swallows it whole — which is why this goes through the shared
/// per-target helper rather than repeating the Artifact check inline, as it used to.
/// </remarks>
public class PiercingWailTests
{
    [Theory]
    [InlineData(false, 6)]
    [InlineData(true, 8)]
    public void EveryEnemyLosesStrength(bool upgraded, int loss)
    {
        var fight = Fight.Hand(Card(SI.PiercingWail, upgraded)).Energy(1).Enemy(hp: 60);
        fight.Enemy(hp: 60);

        fight.Play();

        Assert.Equal(-loss, fight.EnemyBuffAmount(BuffId.Strength));
        Assert.Equal(-loss, fight.EnemyBuffAmount(BuffId.Strength, 1));
        Assert.Equal(loss, fight.EnemyBuffAmount(BuffId.TemporaryStrength));
    }

    /// <summary>The loss is temporary: it is given back once the enemies have had their turn.</summary>
    [Fact]
    public void TheStrengthComesBack()
    {
        var fight = Fight.Hand(Card(SI.PiercingWail)).Energy(1).Enemy(hp: 60);
        fight.State.PlayerHp = 999;
        fight.Play();
        Assert.Equal(-6, fight.EnemyBuffAmount(BuffId.Strength));

        fight.EndTurn();

        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Strength));
        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.TemporaryStrength));
    }

    /// <summary>Artifact eats the whole application, and spends one charge doing it.</summary>
    [Fact]
    public void ArtifactSwallowsIt()
    {
        var fight = Fight.Hand(Card(SI.PiercingWail)).Energy(1).Enemy(hp: 60);
        BuffSystem.Apply(fight.Enemy0.Buffs, BuffId.Artifact, 2);

        fight.Play();

        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Strength));
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Artifact));
    }
}

/// <summary>
/// Predator draws two next turn — two at BOTH levels.
/// </summary>
/// <remarks>
/// `PowerCmd.Apply&lt;DrawCardsNextTurnPower&gt;(..., 2m, ...)` is a literal, and
/// `OnUpgrade` raises the damage and nothing else. The emulator read the upgrade as a
/// third card as well, which is the same mistake as reading Hidden Daggers' upgrade onto
/// its Shiv count: the upgrade belongs to whichever var `OnUpgrade` names.
/// </remarks>
public class PredatorTests
{
    [Theory]
    [InlineData(false, 15)]
    [InlineData(true, 20)]
    public void HitsAndDrawsTwoNextTurnAtBothLevels(bool upgraded, int damage)
    {
        var fight = Fight.Hand(Card(SI.Predator, upgraded)).Energy(2).Enemy(hp: 60);
        fight.State.PlayerHp = 999;
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 30; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.StrikeSilent, false));
        }

        fight.Play();
        Assert.Equal(60 - damage, fight.Enemy0.Hp);
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.NextTurnDraw));

        fight.EndTurn();

        // Five for the turn plus the two Predator owes.
        Assert.Equal(7, fight.State.Hand.Count);
    }
}

/// <summary>
/// Ricochet sprays its hits across the room, rolling a target for each one.
/// </summary>
/// <remarks>
/// `TargetingRandomOpponents`, and `AttackCommand.Execute` rolls
/// `Rng.CombatTargets.NextItem(validTargets)` INSIDE its per-hit loop with the living
/// targets recomputed each round. The emulator put every hit on the aimed-at creature and
/// never touched the target stream — the same defect Bouncing Flask had, and the stream
/// half of it desynchronises every roll downstream.
/// </remarks>
public class RicochetTests
{
    /// <summary>The damage never upgrades; `RepeatVar(4)` does.</summary>
    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, 5)]
    public void ThreeDamageFourOrFiveTimes(bool upgraded, int hits)
    {
        var fight = Fight.Hand(Card(SI.Ricochet, upgraded)).Energy(2).Enemy(hp: 200);

        fight.Play();

        // One enemy, so every roll lands on it: the total is the whole spray.
        Assert.Equal(200 - 3 * hits, fight.Enemy0.Hp);
    }

    [Fact]
    public void TheHitsAreRolledNotAimed()
    {
        bool everySeedHitOnlyOne = true;
        for (int seed = 0; seed < 8; seed++)
        {
            var fight = Fight.Encounter(CombatFactory.ActOneEncounter.Bowlbugs, seed: seed);
            fight.State.Hand = [Card(SI.Ricochet)];
            fight.State.Energy = 3;
            var hpBefore = fight.State.Enemies.Select(e => e.Hp).ToList();
            fight.Play();

            int hurt = fight.State.Enemies.Where((e, i) => e.Hp < hpBefore[i]).Count();
            if (hurt > 1)
            {
                everySeedHitOnlyOne = false;
                break;
            }
        }

        Assert.False(everySeedHitOnlyOne, "every hit landed on the same enemy");
    }
}

public class SliceTests
{
    // DamageVar(6m) +3, 0 cost.
    [Theory]
    [InlineData(false, 6)]
    [InlineData(true, 9)]
    public void HitsForFree(bool upgraded, int damage)
    {
        var fight = Fight.Hand(Card(SI.Slice, upgraded)).Energy(0).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(60 - damage, fight.Enemy0.Hp);
    }
}

public class SnakebiteTests
{
    // PowerVar<PoisonPower>(7m) +3, and the Retain keyword.
    [Theory]
    [InlineData(false, 7)]
    [InlineData(true, 10)]
    public void PoisonsTheTarget(bool upgraded, int poison)
    {
        var fight = Fight.Hand(Card(SI.Snakebite, upgraded)).Energy(2).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(poison, fight.EnemyBuffAmount(BuffId.Poison));
    }
}

public class SuckerPunchTests
{
    // DamageVar(8m) +2 and PowerVar<WeakPower>(1m) +1 -- BOTH vars upgrade here, which is
    // what makes reading OnUpgrade per var rather than per card the only safe rule.
    [Theory]
    [InlineData(false, 8, 1)]
    [InlineData(true, 10, 2)]
    public void HitsAndWeakens(bool upgraded, int damage, int weak)
    {
        var fight = Fight.Hand(Card(SI.SuckerPunch, upgraded)).Energy(1).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(60 - damage, fight.Enemy0.Hp);
        Assert.Equal(weak, fight.EnemyBuffAmount(BuffId.Weak));
    }
}

/// <summary>
/// Untouchable is six block. That is the entire card.
/// </summary>
/// <remarks>
/// `OnPlay` is one `CreatureCmd.GainBlock` and nothing else. The emulator added
/// `state.DrawPile.Count` on top — another ten block or more early in a combat, growing
/// with the deck for the whole run, on a 2-cost common. An invented scaling term is worse
/// than a wrong constant: it is wrong by a different amount in every fight.
/// </remarks>
public class UntouchableTests
{
    [Theory]
    [InlineData(false, 6)]
    [InlineData(true, 9)]
    public void BlocksAndNothingElse(bool upgraded, int block)
    {
        var fight = Fight.Hand(Card(SI.Untouchable, upgraded)).Energy(2);

        fight.Play();

        Assert.Equal(block, fight.State.PlayerBlock);
    }

    /// <summary>And the draw pile does not come into it, at any size.</summary>
    [Fact]
    public void TheDrawPileDoesNotChangeIt()
    {
        var fight = Fight.Hand(Card(SI.Untouchable)).Energy(2);
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 40; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.StrikeSilent, false));
        }

        fight.Play();

        Assert.Equal(6, fight.State.PlayerBlock);
    }

    [Fact]
    public void ItIsSly()
    {
        Assert.True(GeneratedData.Cards.Get(SI.Untouchable).Sly);
    }
}
