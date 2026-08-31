using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

/// <summary>
/// Nightmare: THREE copies of a card you CHOOSE, arriving at the start of the next turn.
/// </summary>
/// <remarks>
/// The emulator duplicated the leftmost card in hand, three times, immediately. Both
/// halves matter and each is the card: choosing is the whole point of a Nightmare, and
/// paying three energy for cards that only arrive next turn is what the three energy buys.
/// The upgrade takes a point off the cost and changes nothing else — the by-name fallback
/// that used to catch this card read it as 2 copies rising to 3.
/// </remarks>
public class NightmareTests
{
    // The copies are counted in the hand at the start of the next turn, so the setup has
    // to make sure nothing ELSE puts that card there. Copy a card the starter deck does
    // not contain, and stock the draw pile deep enough that the discard -- which holds the
    // original -- is never reshuffled back.
    private static Fight WithNightmare(params CardInstance[] rest)
    {
        var fight = Fight.Hand([Card(SI.Nightmare), .. rest]).Energy(9);
        fight.State.PlayerHp = 999;
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 30; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.StrikeSilent, false));
        }

        return fight;
    }

    /// <summary>Every card in hand is offered: `CardSelectCmd.FromHand` is given a null filter.</summary>
    [Fact]
    public void ItAsksWhichCardToCopyAndOffersTheWholeHand()
    {
        var fight = WithNightmare(Card(SI.Backstab), Card(SI.DeadlyPoison));

        fight.Play();

        Assert.NotNull(fight.Pending);
        Assert.Equal(CardSelectionKind.QueueHandCardCopies, fight.Pending!.Kind);
        Assert.Equal(SI.Nightmare, fight.Pending.SourceCardDefId);
        Assert.Equal(2, fight.Pending.Candidates.Count);
    }

    /// <summary>
    /// The pick is honoured, the chosen card is NOT consumed, and nothing arrives yet --
    /// the power holds the clones until <c>BeforeHandDraw</c>.
    /// </summary>
    [Fact]
    public void TheChosenCardStaysAndNothingArrivesThisTurn()
    {
        var fight = WithNightmare(Card(SI.Backstab), Card(SI.DeadlyPoison));
        fight.Play();

        fight.Choose(1); // the Deadly Poison, not the leftmost card

        Assert.Null(fight.Pending);
        Assert.Equal([SI.Backstab, SI.DeadlyPoison], fight.State.Hand.Select(c => c.DefId));
    }

    /// <summary>
    /// Three copies of the CHOSEN card, next turn. Picking the second candidate is what
    /// separates this from the old behaviour, which always took the leftmost.
    /// </summary>
    [Fact]
    public void ThreeCopiesOfTheChosenCardArriveNextTurn()
    {
        var fight = WithNightmare(Card(SI.Backstab), Card(SI.DeadlyPoison));
        fight.Play();
        fight.Choose(1);

        fight.EndTurn();

        Assert.Equal(3, fight.State.Hand.Count(c => c.DefId == SI.DeadlyPoison));
        Assert.DoesNotContain(fight.State.Hand, c => c.DefId == SI.Backstab);
    }

    /// <summary>
    /// The count is three whether or not it is upgraded -- `PowerCmd.Apply&lt;NightmarePower&gt;`
    /// is handed a literal 3, and `OnUpgrade` only does `EnergyCost.UpgradeBy(-1)`. The
    /// by-name fallback that used to catch this card read the upgrade as a fourth copy.
    /// </summary>
    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 2)]
    public void TheUpgradeChangesTheCostAndNotTheCount(bool upgraded, int cost)
    {
        var fight = Fight.Hand(Card(SI.Nightmare, upgraded), Card(SI.Backstab)).Energy(9);
        fight.State.PlayerHp = 999;
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 30; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.StrikeSilent, false));
        }

        int energyBefore = fight.State.Energy;

        fight.Play();
        fight.Choose(0);
        Assert.Equal(energyBefore - cost, fight.State.Energy);

        fight.EndTurn();

        Assert.Equal(3, fight.State.Hand.Count(c => c.DefId == SI.Backstab));
    }

    /// <summary>
    /// `PowerCmd.Remove(this)` runs right after the clones are handed over, so they come
    /// once. A Nightmare that paid out every turn would be the best card in the game.
    /// </summary>
    [Fact]
    public void TheCopiesArriveOnceAndNotEveryTurn()
    {
        var fight = WithNightmare(Card(SI.Backstab));
        fight.Play();
        fight.Choose(0);

        fight.EndTurn();
        Assert.Equal(3, fight.State.Hand.Count(c => c.DefId == SI.Backstab));

        fight.State.Hand.Clear();
        fight.EndTurn();

        Assert.DoesNotContain(fight.State.Hand, c => c.DefId == SI.Backstab);
    }

    /// <summary>
    /// `CreateClone` keeps the upgrade, so copying an upgraded card gives upgraded copies.
    /// It is a CLONE and not a DUPE, which is why they also keep Exhaust.
    /// </summary>
    [Fact]
    public void TheCopiesAreClonesAndKeepTheUpgrade()
    {
        var fight = WithNightmare(Card(SI.Backstab, upgraded: true));
        fight.Play();
        fight.Choose(0);

        fight.EndTurn();

        var copies = fight.State.Hand.Where(c => c.DefId == SI.Backstab).ToList();
        Assert.Equal(3, copies.Count);
        Assert.All(copies, c => Assert.True(c.Upgraded));
    }

    /// <summary>An empty hand raises no screen, and no clones are owed.</summary>
    [Fact]
    public void WithNothingInHandItAsksNothing()
    {
        var fight = WithNightmare();

        fight.Play();
        Assert.Null(fight.Pending);

        Assert.Empty(fight.State.CopiesToHandBeforeDraw);
    }
}

/// <summary>
/// Echoing Slash: 10 to all enemies, thrown again once for every enemy that volley KILLED.
/// </summary>
/// <remarks>
/// `while (attackCount > 0) { attackCount--; ...; attackCount += hits.Count(r =>
/// r.WasTargetKilled); }`. The emulator threw it once, under a comment claiming the real
/// card "gains damage per kill" — a different card. The damage never changes; the number
/// of volleys does, and the repeats can kill in turn.
/// </remarks>
public class EchoingSlashTests
{
    [Theory]
    [InlineData(false, 10)]
    [InlineData(true, 13)]
    public void ItHitsEveryEnemy(bool upgraded, int damage)
    {
        var fight = Fight.Hand(Card(SI.EchoingSlash, upgraded)).Energy(1).Enemy(hp: 100);
        fight.Enemy(hp: 100);

        fight.Play();

        Assert.Equal(100 - damage, fight.Enemy0.Hp);
        Assert.Equal(100 - damage, fight.Enemy1.Hp);
    }

    /// <summary>
    /// One kill buys one more volley. The survivor is hit twice for a card that reads as
    /// one attack, which is the whole card.
    /// </summary>
    [Fact]
    public void AKillBuysAnotherVolley()
    {
        var fight = Fight.Hand(Card(SI.EchoingSlash)).Energy(1).Enemy(hp: 6);
        fight.Enemy(hp: 100);

        fight.Play();

        Assert.True(fight.Enemy0.Hp <= 0);
        Assert.Equal(100 - 20, fight.Enemy1.Hp);
    }

    /// <summary>
    /// And the volleys a kill buys can kill again: three enemies each dying in turn is
    /// four volleys, so the last one standing eats all four.
    /// </summary>
    [Fact]
    public void TheRepeatsCascade()
    {
        var fight = Fight.Hand(Card(SI.EchoingSlash)).Energy(1).Enemy(hp: 6);
        fight.Enemy(hp: 16);
        fight.Enemy(hp: 26);
        fight.Enemy(hp: 100);

        fight.Play();

        // Volley 1 kills the 6 and leaves 6/16/90. Volley 2 kills the 16 and leaves
        // 0/6/80. Volley 3 kills the 26. Volley 4 is the last one bought.
        Assert.All(fight.State.Enemies.Take(3), e => Assert.True(e.Hp <= 0));
        Assert.Equal(100 - 40, fight.State.Enemies[3].Hp);
    }

    /// <summary>Killing nothing means one volley, and the loop terminates.</summary>
    [Fact]
    public void NoKillsMeansOneVolley()
    {
        var fight = Fight.Hand(Card(SI.EchoingSlash)).Energy(1).Enemy(hp: 100);

        fight.Play();

        Assert.Equal(90, fight.Enemy0.Hp);
    }
}

/// <summary>
/// Blade of Ink: two Shivs, and every one of them is Inky.
/// </summary>
/// <remarks>
/// The enchantment IS the card. Without it Blade of Ink is a strictly worse Blade Dance —
/// one fewer Shiv at the same cost — which is exactly what the emulator played. Worse, the
/// by-name fallback underneath the switch had it granting Focus, a Defect mechanic.
///
/// Inky's two numbers are its own vars, a `DamageVar(1m)` and a `PowerVar&lt;WeakPower&gt;(1m)`,
/// not the amount it was applied at. That is why it declares `ShowAmount => false`.
/// </remarks>
public class BladeOfInkTests
{
    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 3)]
    public void ItMakesInkyShivs(bool upgraded, int count)
    {
        var fight = Fight.Hand(Card(SI.BladeOfInk, upgraded)).Energy(1);

        fight.Play();

        var shivs = fight.State.Hand.Where(c => c.DefId == SI.Shiv).ToList();
        Assert.Equal(count, shivs.Count);
        Assert.All(shivs, s => Assert.Equal(Enchantment.Inky, s.Enchantment));
    }

    /// <summary>An Inky Shiv hits for 5, not 4.</summary>
    [Fact]
    public void AnInkyShivHitsForOneMore()
    {
        var plain = Fight.Hand(Card(SI.Shiv)).Energy(3).Enemy(hp: 60);
        plain.Play();
        Assert.Equal(60 - 4, plain.Enemy0.Hp);

        var inky = Fight.Hand(Card(SI.BladeOfInk)).Energy(3).Enemy(hp: 60);
        inky.Play();
        inky.Play(inky.State.Hand.FindIndex(c => c.DefId == SI.Shiv));

        Assert.Equal(60 - 5, inky.Enemy0.Hp);
    }

    /// <summary>And it Weakens what it hit — `Inky.OnPlay`, which the plain Shiv does not do.</summary>
    [Fact]
    public void AnInkyShivWeakensItsTarget()
    {
        var fight = Fight.Hand(Card(SI.BladeOfInk)).Energy(3).Enemy(hp: 60);
        fight.Play();

        fight.Play(fight.State.Hand.FindIndex(c => c.DefId == SI.Shiv));

        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Weak));
    }

    /// <summary>
    /// The bonus is a flat 1 from the enchantment's own var rather than its amount, so it
    /// does not scale the way Sharp's does.
    /// </summary>
    [Fact]
    public void TheBonusIsTheEnchantmentsOwnNumberNotItsAmount()
    {
        var fight = Fight
            .Hand(Card(SI.Shiv) with { Enchantment = Enchantment.Inky, EnchantAmount = 5 })
            .Energy(3)
            .Enemy(hp: 60);

        fight.Play();

        Assert.Equal(60 - 5, fight.Enemy0.Hp);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Weak));
    }

    /// <summary>The Shivs still Exhaust and still cost nothing, as any Shiv does.</summary>
    [Fact]
    public void TheyAreStillOrdinaryShivsOtherwise()
    {
        var fight = Fight.Hand(Card(SI.BladeOfInk)).Energy(1).Enemy(hp: 60);
        fight.Play();

        fight.Play(fight.State.Hand.FindIndex(c => c.DefId == SI.Shiv));

        Assert.Contains(fight.State.ExhaustPile, c => c.DefId == SI.Shiv);
    }
}
