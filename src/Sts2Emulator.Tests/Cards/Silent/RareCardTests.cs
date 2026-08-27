using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// Silent's nine unpinned rares, read against
// MegaCrit.Sts2.Core.Models.Cards/*.cs. Four were wrong, and two of those were a
// different card entirely.

public class AbrasiveTests
{
    // PowerVar<ThornsPower>(4m) +2 and PowerVar<DexterityPower>(1m). `OnUpgrade` names the
    // THORNS var only, so the Dexterity is 1 at both levels.
    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, 6)]
    public void GrantsThornsAndOneDexterity(bool upgraded, int thorns)
    {
        var fight = Fight.Hand(Card(SI.Abrasive, upgraded)).Energy(3);

        fight.Play();

        Assert.Equal(thorns, fight.PlayerBuffAmount(BuffId.Thorns));
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Dexterity));
    }

    [Fact]
    public void ItIsSly()
    {
        Assert.True(GeneratedData.Cards.Get(SI.Abrasive).Sly);
    }
}

public class AdrenalineTests
{
    // EnergyVar(1) +1 and CardsVar(2). `OnUpgrade` names the ENERGY var, so the draw is 2
    // at both levels.
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void GivesEnergyAndDrawsTwo(bool upgraded, int energy)
    {
        var fight = Fight.Hand(Card(SI.Adrenaline, upgraded)).Energy(0);
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 6; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.Backstab, false));
        }

        fight.Play();

        Assert.Equal(energy, fight.State.Energy);
        Assert.Equal(2, fight.State.Hand.Count);
        Assert.Contains(fight.State.ExhaustPile, c => c.DefId == SI.Adrenaline);
    }
}

/// <summary>
/// Afterimage blocks for what the power was worth when the play STARTED.
/// </summary>
/// <remarks>
/// `AfterimagePower.BeforeCardPlayed` records its amount for the card about to be played
/// and `AfterCardPlayed` spends that. The power's own Data comment says why: "avoid
/// triggering on cards that started play before it was applied, and avoid gaining extra
/// block on multiple plays of After Image". The emulator read the amount AFTER the card
/// resolved, so an Afterimage paid out on its own play — the same defect Burst had.
///
/// And the block is `ValueProp.Unpowered`, so Dexterity does not touch it.
/// </remarks>
public class AfterimageTests
{
    [Fact]
    public void ItDoesNotPayOutOnItsOwnPlay()
    {
        var fight = Fight.Hand(Card(SI.Afterimage), Card(SI.Slice)).Energy(3).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Afterimage));
        Assert.Equal(0, fight.State.PlayerBlock);
    }

    [Fact]
    public void EveryLaterCardBlocks()
    {
        var fight = Fight
            .Hand(Card(SI.Afterimage), Card(SI.Slice), Card(SI.Slice))
            .Energy(3)
            .Enemy(hp: 60);
        fight.Play();

        fight.Play();
        Assert.Equal(1, fight.State.PlayerBlock);

        fight.Play();
        Assert.Equal(2, fight.State.PlayerBlock);
    }

    /// <summary>A second Afterimage pays 1 for its own play, not 2 — the amount it had beforehand.</summary>
    [Fact]
    public void ASecondCopyPaysTheOldAmount()
    {
        var fight = Fight.Hand(Card(SI.Afterimage), Card(SI.Afterimage)).Energy(3);
        fight.Play();

        fight.Play();

        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.Afterimage));
        Assert.Equal(1, fight.State.PlayerBlock);
    }

    /// <summary>The block is unpowered, so Dexterity does not raise it.</summary>
    [Fact]
    public void DexterityDoesNotRaiseIt()
    {
        var fight = Fight.Hand(Card(SI.Afterimage), Card(SI.Slice)).Energy(3).Enemy(hp: 60);
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Dexterity, 5);
        fight.Play();

        fight.Play();

        Assert.Equal(1, fight.State.PlayerBlock);
    }

    [Fact]
    public void TheUpgradeMakesItInnate()
    {
        Assert.True(GeneratedData.Cards.Get(SI.Afterimage).InnateWhenUpgraded);
    }
}

public class AssassinateTests
{
    // DamageVar(10m) +3 and PowerVar<VulnerablePower>(1m) +1 -- both vars upgrade.
    // Innate and Exhaust, and it costs nothing.
    [Theory]
    [InlineData(false, 10, 1)]
    [InlineData(true, 13, 2)]
    public void HitsAndMakesVulnerable(bool upgraded, int damage, int vulnerable)
    {
        var fight = Fight.Hand(Card(SI.Assassinate, upgraded)).Energy(0).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(60 - damage, fight.Enemy0.Hp);
        Assert.Equal(vulnerable, fight.EnemyBuffAmount(BuffId.Vulnerable));
        Assert.Contains(fight.State.ExhaustPile, c => c.DefId == SI.Assassinate);
    }

    [Fact]
    public void ItIsInnate()
    {
        Assert.True(GeneratedData.Cards.Get(SI.Assassinate).Innate);
    }
}

/// <summary>
/// Envenom poisons on UNBLOCKED attack damage.
/// </summary>
/// <remarks>
/// `EnvenomPower.AfterDamageGiven` needs `props.IsPoweredAttack()` and
/// `result.UnblockedDamage > 0`. An attack the enemy blocks entirely poisons nobody, which
/// is the half of the rule a flat "attacks apply poison" reading loses.
/// </remarks>
public class EnvenomTests
{
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void AttacksPoison(bool upgraded, int poison)
    {
        var fight = Fight.Hand(Card(SI.Envenom, upgraded), Card(SI.Slice)).Energy(3).Enemy(hp: 60);
        fight.Play();

        fight.Play();

        Assert.Equal(poison, fight.EnemyBuffAmount(BuffId.Poison));
    }

    [Fact]
    public void AFullyBlockedAttackPoisonsNobody()
    {
        var fight = Fight.Hand(Card(SI.Envenom), Card(SI.Slice)).Energy(3).Enemy(hp: 60, block: 50);
        fight.Play();

        fight.Play();

        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Poison));
    }
}

/// <summary>
/// Shadow Step doubles your damage NEXT turn. It was Intangible.
/// </summary>
/// <remarks>
/// `ShadowStepPower.AfterSideTurnStart` applies `DoubleDamagePower` and removes itself, so
/// the payload lands a turn late and lasts that turn. The emulator gave Intangible 1 — a
/// defensive buff where the card is an offensive one, and immediate where the card is
/// delayed. Discarding your hand to take less damage is a different plan from discarding
/// it to hit twice as hard next turn.
/// </remarks>
public class ShadowStepTests
{
    [Fact]
    public void ItDiscardsTheHandAndGrantsNothingYet()
    {
        var fight = Fight
            .Hand(Card(SI.ShadowStep), Card(SI.Backstab), Card(SI.Slice))
            .Energy(1)
            .Enemy(hp: 200);

        fight.Play();

        Assert.Empty(fight.State.Hand);
        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.Intangible));
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.ShadowStep));
        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.DoubleDamage));
    }

    [Fact]
    public void NextTurnTheDamageIsDoubled()
    {
        var fight = Fight.Hand(Card(SI.ShadowStep)).Energy(1).Enemy(hp: 200);
        fight.State.PlayerHp = 999;
        fight.Play();

        fight.EndTurn();
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.DoubleDamage));
        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.ShadowStep));

        fight.State.Hand.Add(Card(SI.Slice));
        fight.State.Energy = 3;
        int before = fight.Enemy0.Hp;
        fight.Play(fight.State.Hand.FindIndex(c => c.DefId == SI.Slice));

        Assert.Equal(before - 12, fight.Enemy0.Hp); // 6 doubled
    }

    /// <summary>
    /// `DoubleDamagePower.AfterSideTurnEnd` decrements, so one Shadow Step buys exactly
    /// the turn it arrived for and the turn after is ordinary again.
    /// </summary>
    [Fact]
    public void TheDoublingLastsOneTurn()
    {
        var fight = Fight.Hand(Card(SI.ShadowStep)).Energy(1).Enemy(hp: 200);
        fight.State.PlayerHp = 999;
        fight.Play();
        fight.EndTurn();

        fight.EndTurn();
        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.DoubleDamage));

        fight.State.Hand.Add(Card(SI.Slice));
        fight.State.Energy = 3;
        int before = fight.Enemy0.Hp;
        fight.Play(fight.State.Hand.FindIndex(c => c.DefId == SI.Slice));

        Assert.Equal(before - 6, fight.Enemy0.Hp);
    }

    /// <summary>The discard is a `CardCmd.Discard`, so Sly cards in the hand play.</summary>
    [Fact]
    public void SlyCardsInTheHandArePlayed()
    {
        var fight = Fight.Hand(Card(SI.ShadowStep), Card(SI.Tactician)).Energy(3);
        int before = fight.State.Energy;

        fight.Play();

        Assert.Equal(before - 1 + 1, fight.State.Energy);
    }
}

/// <summary>
/// Shadowmeld doubles the block you gain this turn. It was whole-hand Retain.
/// </summary>
/// <remarks>
/// `ShadowmeldPower.ModifyBlockMultiplicative` returns `2^Amount` for its owner, and
/// `AfterSideTurnEnd` removes it. The emulator applied `BuffId.RetainHand` — a different
/// card doing a different thing, and the second Silent rare in this sweep standing in for
/// RetainHand after Well-Laid Plans.
/// </remarks>
public class ShadowmeldTests
{
    [Fact]
    public void BlockGainedThisTurnIsDoubled()
    {
        var fight = Fight.Hand(Card(SI.Shadowmeld), Card(SI.Deflect)).Energy(3);

        fight.Play();
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Shadowmeld));
        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.RetainHand));

        fight.Play();

        Assert.Equal(8, fight.State.PlayerBlock); // Deflect's 4, doubled
    }

    /// <summary>
    /// It multiplies AFTER Dexterity, as a multiplicative hook does: (4 + 2) x 2.
    /// </summary>
    [Fact]
    public void ItMultipliesAfterDexterity()
    {
        var fight = Fight.Hand(Card(SI.Shadowmeld), Card(SI.Deflect)).Energy(3);
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Dexterity, 2);
        fight.Play();

        fight.Play();

        Assert.Equal(12, fight.State.PlayerBlock);
    }

    /// <summary>And it is gone at the end of the turn, so the next one blocks normally.</summary>
    [Fact]
    public void ItLastsOnlyTheTurnItWasPlayed()
    {
        var fight = Fight.Hand(Card(SI.Shadowmeld)).Energy(3);
        fight.State.PlayerHp = 999;
        fight.Play();

        fight.EndTurn();

        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.Shadowmeld));
        fight.State.Hand.Add(Card(SI.Deflect));
        fight.State.Energy = 3;
        int before = fight.State.PlayerBlock;
        fight.Play(fight.State.Hand.FindIndex(c => c.DefId == SI.Deflect));

        Assert.Equal(before + 4, fight.State.PlayerBlock);
    }

    /// <summary>
    /// The hook does not look at `props`, so UNPOWERED block is doubled too — Afterimage's,
    /// which is the pairing that makes the distinction visible.
    /// </summary>
    [Fact]
    public void ItDoublesUnpoweredBlockAsWell()
    {
        var fight = Fight
            .Hand(Card(SI.Afterimage), Card(SI.Shadowmeld), Card(SI.Slice))
            .Energy(9)
            .Enemy(hp: 60);
        fight.Play(); // Afterimage
        fight.Play(); // Shadowmeld -- pays 1 Afterimage block, doubled to 2

        int before = fight.State.PlayerBlock;
        fight.Play(); // Slice

        Assert.Equal(before + 2, fight.State.PlayerBlock);
    }
}

public class StormOfSteelTests
{
    // Discards the hand and makes one Shiv per card discarded. The count is read BEFORE
    // the discard, and the upgrade upgrades the SHIVS rather than making more of them.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OneShivPerCardDiscarded(bool upgraded)
    {
        var fight = Fight
            .Hand(
                Card(SI.StormOfSteel, upgraded),
                Card(SI.Backstab),
                Card(SI.Slice),
                Card(SI.Deflect)
            )
            .Energy(1);

        fight.Play();

        var shivs = fight.State.Hand.Where(c => c.DefId == SI.Shiv).ToList();
        Assert.Equal(3, shivs.Count);
        Assert.All(shivs, s => Assert.Equal(upgraded, s.Upgraded));
        Assert.Contains(fight.State.DiscardPile, c => c.DefId == SI.Backstab);
    }

    [Fact]
    public void AnEmptyHandMakesNoShivs()
    {
        var fight = Fight.Hand(Card(SI.StormOfSteel)).Energy(1);

        fight.Play();

        Assert.DoesNotContain(fight.State.Hand, c => c.DefId == SI.Shiv);
    }
}

/// <summary>
/// Tools of the Trade draws one more each turn and discards one you CHOOSE.
/// </summary>
/// <remarks>
/// `ModifyHandDraw` adds its amount to the hand draw, and `AfterPlayerTurnStart` raises a
/// discard selection for that many cards — `CardSelectorPrefs(prompt, Amount)`, whose
/// single-count constructor sets min and max alike, so the discard is compulsory but the
/// choice is the player's. The emulator threw away the leftmost card, which on a filtering
/// card is closer to a downside than an upside.
/// </remarks>
public class ToolsOfTheTradeTests
{
    [Fact]
    public void TheTurnStartsWithAnExtraCardAndAChoiceOfWhatToPitch()
    {
        var fight = Fight.Hand(Card(SI.ToolsOfTheTrade)).Energy(1);
        fight.State.PlayerHp = 999;
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 20; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.Backstab, false));
        }

        fight.Play();
        fight.EndTurn();

        // Five for the turn plus the one the power adds, and the screen is up.
        Assert.Equal(6, fight.State.Hand.Count);
        Assert.NotNull(fight.Pending);
        Assert.Equal(CardSelectionKind.DiscardFromHandRepeated, fight.Pending!.Kind);
        Assert.Equal(6, fight.Pending.Candidates.Count);
    }

    /// <summary>The pick is the player's — answering with the last card discards that one.</summary>
    [Fact]
    public void TheChosenCardIsTheOneDiscarded()
    {
        var fight = Fight.Hand(Card(SI.ToolsOfTheTrade)).Energy(1);
        fight.State.PlayerHp = 999;
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 5; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.Backstab, false));
        }

        fight.State.DrawPile.Add(new CardInstance(SI.Slice, false));
        fight.Play();
        fight.EndTurn();

        int slice = fight.State.Hand.FindIndex(c => c.DefId == SI.Slice);
        fight.Choose(fight.Pending!.Candidates.IndexOf(slice));

        Assert.Null(fight.Pending);
        Assert.DoesNotContain(fight.State.Hand, c => c.DefId == SI.Slice);
        Assert.Equal(5, fight.State.Hand.Count);
    }

    /// <summary>While the screen is up it owns the action space, as any selection does.</summary>
    [Fact]
    public void TheScreenOwnsTheActionSpace()
    {
        var fight = Fight.Hand(Card(SI.ToolsOfTheTrade)).Energy(1);
        fight.State.PlayerHp = 999;
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 20; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.Backstab, false));
        }

        fight.Play();
        fight.EndTurn();

        Assert.Equal(
            Enumerable.Range(0, fight.Pending!.Candidates.Count),
            CombatEngine.ValidActions(fight.State)
        );
    }
}
