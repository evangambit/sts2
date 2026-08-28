using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

/// <summary>
/// 2-cost Skill, Rare, `TargetType.AnyEnemy`: it takes every Shiv-tagged card out of the
/// EXHAUST pile, upgrades each one if the trap is upgraded, and auto-plays them all at the
/// trap's own target.
/// </summary>
/// <remarks>
/// `CalculatedShivs` is a display var counting them. There is no damage and no block on
/// the card at all — the emulator gave Thorns 4/6, which is not a smaller version of that
/// but a different card.
///
/// The replays are given a target, so they must not roll for one: see
/// `CombatState.AutoPlayTargetIndex`. That distinction only exists because this card is
/// the one thing in the emulator that hands an auto-play an explicit target.
/// </remarks>
public class KnifeTrapTests
{
    private static Fight WithExhaustedShivs(int count, bool upgraded = false)
    {
        var fight = Fight.Hand(Card(SI.KnifeTrap, upgraded)).Energy(9).Enemy(hp: 200);
        for (int i = 0; i < count; i++)
        {
            fight.State.ExhaustPile.Add(new CardInstance(SI.Shiv, false));
        }

        return fight;
    }

    [Fact]
    public void ItReplaysEveryShivInTheExhaustPile()
    {
        var fight = WithExhaustedShivs(3);
        int shiv = GeneratedData.Cards.Get(SI.Shiv).BaseDamage;

        fight.Play();

        Assert.Equal(200 - (3 * shiv), fight.Enemy0.Hp);
    }

    /// <summary>
    /// Each replayed Shiv exhausts again as it resolves, so the pile is where it started
    /// and the trap cannot be looped on itself within one play.
    /// </summary>
    [Fact]
    public void TheShivsGoBackToTheExhaustPile()
    {
        var fight = WithExhaustedShivs(2);

        fight.Play();

        Assert.Equal(2, fight.State.ExhaustPile.Count(c => c.DefId == SI.Shiv));
    }

    /// <summary>
    /// The upgrade is on what it THROWS, not on how many: `CardCmd.Upgrade(item)` runs on
    /// each Shiv before the play, so an upgraded trap replays upgraded Shivs.
    /// </summary>
    [Fact]
    public void UpgradedItReplaysUpgradedShivs()
    {
        var fight = WithExhaustedShivs(2, upgraded: true);
        int upgradedShiv =
            GeneratedData.Cards.Get(SI.Shiv).BaseDamage
            + GeneratedData.Cards.Get(SI.Shiv).UpgradeDamage;

        fight.Play();

        Assert.Equal(200 - (2 * upgradedShiv), fight.Enemy0.Hp);
        Assert.All(
            fight.State.ExhaustPile.Where(c => c.DefId == SI.Shiv),
            c => Assert.True(c.Upgraded)
        );
    }

    /// <summary>An empty exhaust pile makes it a 2-cost card that does nothing.</summary>
    [Fact]
    public void WithNoExhaustedShivsItDoesNothing()
    {
        var fight = Fight.Hand(Card(SI.KnifeTrap)).Energy(9).Enemy(hp: 200);

        fight.Play();

        Assert.Equal(200, fight.Enemy0.Hp);
        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.Thorns));
    }

    /// <summary>
    /// The replays hit the trap's OWN target rather than rolling for one apiece —
    /// `CardCmd.AutoPlay` is handed `cardPlay.Target`, and a given target does not roll.
    /// </summary>
    [Fact]
    public void EveryReplayHitsTheTargetTheTrapWasAimedAt()
    {
        var fight = Fight.Encounter(CombatFactory.ActOneEncounter.Bowlbugs);
        fight.State.Hand = [Card(SI.KnifeTrap)];
        fight.State.Energy = 9;
        for (int i = 0; i < 4; i++)
        {
            fight.State.ExhaustPile.Add(new CardInstance(SI.Shiv, false));
        }

        var living = fight.State.Enemies.Where(e => e.Hp > 0).ToList();
        Assert.True(living.Count > 1, "this test needs more than one enemy");
        int aimed = fight.State.Enemies.IndexOf(living[1]);
        var before = living.Select(e => e.Hp).ToList();
        int shiv = GeneratedData.Cards.Get(SI.Shiv).BaseDamage;

        fight.Play(0, target: aimed);

        for (int i = 0; i < living.Count; i++)
        {
            int expected = living[i] == fight.State.Enemies[aimed] ? 4 * shiv : 0;
            Assert.Equal(before[i] - expected, living[i].Hp);
        }
    }
}
