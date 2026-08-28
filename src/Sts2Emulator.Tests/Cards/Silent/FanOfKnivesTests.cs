using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

/// <summary>
/// 2-cost Power: `FanOfKnivesPower`, and `CardsVar("Shivs", 4)` Shivs into hand (5 upgraded).
/// </summary>
/// <remarks>
/// The power does nothing on its own — the SHIV card reads it and returns
/// `TargetType.AllEnemies` instead of `AnyEnemy`. So the retarget lives on the Shiv, not
/// in anything Fan of Knives does when played, which is the same shape as Master Planner
/// (E140) and Accelerant (E136): a power whose entire behaviour is that something else
/// checks for it.
///
/// The emulator applied `InfiniteBlades` at the SHIV COUNT — a different card, one Shiv
/// every turn forever, wearing this one's number — and the retarget was absent.
/// </remarks>
public class FanOfKnivesTests
{
    private static Fight Crowd(bool upgraded = false)
    {
        var fight = Fight.Encounter(CombatFactory.ActOneEncounter.Bowlbugs);
        fight.State.Hand = [Card(SI.FanOfKnives, upgraded)];
        fight.State.Energy = 9;
        return fight;
    }

    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, 5)]
    public void ItAddsItsShivsAndThePower(bool upgraded, int shivs)
    {
        var fight = Crowd(upgraded);

        fight.Play();

        Assert.Equal(shivs, fight.State.Hand.Count(c => c.DefId == SI.Shiv));
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.FanOfKnives));
        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.InfiniteBlades));
    }

    /// <summary>
    /// The point of the card: every Shiv now hits everything. Four damage to each of the
    /// living enemies rather than four to one.
    /// </summary>
    [Fact]
    public void EveryShivThenHitsEveryEnemy()
    {
        var fight = Crowd();
        fight.Play();
        var living = fight.State.Enemies.Where(e => e.Hp > 0).ToList();
        Assert.True(living.Count > 1, "this test needs more than one enemy");
        var before = living.Select(e => e.Hp).ToList();
        int shiv = GeneratedData.Cards.Get(SI.Shiv).BaseDamage;

        fight.Play(fight.State.Hand.FindIndex(c => c.DefId == SI.Shiv));

        for (int i = 0; i < living.Count; i++)
        {
            Assert.Equal(before[i] - shiv, living[i].Hp);
        }
    }

    /// <summary>Without the power a Shiv hits one enemy, as it always did.</summary>
    [Fact]
    public void WithoutThePowerAShivHitsOne()
    {
        var fight = Fight.Encounter(CombatFactory.ActOneEncounter.Bowlbugs);
        fight.State.Hand = [Card(SI.Shiv)];
        fight.State.Energy = 9;
        var living = fight.State.Enemies.Where(e => e.Hp > 0).ToList();
        var before = living.Select(e => e.Hp).ToList();

        fight.Play();

        int hurt = living.Where((e, i) => e.Hp < before[i]).Count();
        Assert.Equal(1, hurt);
    }

    /// <summary>
    /// `PowerStackType.Single`: a second Fan of Knives brings more Shivs and not a second
    /// stack, so nothing about the retarget doubles.
    /// </summary>
    [Fact]
    public void ASecondCopyDoesNotStackThePower()
    {
        var fight = Crowd();
        fight.State.Hand.Add(Card(SI.FanOfKnives));

        fight.Play();
        fight.Play(fight.State.Hand.FindIndex(c => c.DefId == SI.FanOfKnives));

        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.FanOfKnives));
        Assert.Equal(8, fight.State.Hand.Count(c => c.DefId == SI.Shiv));
    }

    /// <summary>
    /// The retarget composes with Phantom Blades: the first Shiv of the turn carries that
    /// bonus, and with Fan of Knives up it carries it to everything.
    /// </summary>
    [Fact]
    public void ItComposesWithPhantomBladesFirstShivBonus()
    {
        var fight = Crowd();
        fight.State.Hand.Add(Card(SI.PhantomBlades));
        fight.Play(fight.State.Hand.FindIndex(c => c.DefId == SI.PhantomBlades));
        fight.Play(fight.State.Hand.FindIndex(c => c.DefId == SI.FanOfKnives));

        var living = fight.State.Enemies.Where(e => e.Hp > 0).ToList();
        var before = living.Select(e => e.Hp).ToList();
        int shiv = GeneratedData.Cards.Get(SI.Shiv).BaseDamage;

        fight.Play(fight.State.Hand.FindIndex(c => c.DefId == SI.Shiv));

        for (int i = 0; i < living.Count; i++)
        {
            Assert.Equal(before[i] - (shiv + 9), living[i].Hp);
        }
    }
}
