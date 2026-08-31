using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// `Fuel` and `BrightestFlame` shared a body giving 2/3 energy and 2/3 cards. They are
/// different cards, and the shared numbers were Brightest Flame's — Fuel had been handed
/// twice what it gives.
/// </summary>
public class FuelTests
{
    private static int Id => GeneratedData.Cards.FindId("Fuel")!.Value;

    /// <summary>`EnergyVar(1)` flat, `CardsVar(1)` upgrading to 2.</summary>
    [Theory]
    [InlineData(false, 1, 1)]
    [InlineData(true, 1, 2)]
    public void OneEnergyAndOneCard(bool upgraded, int energy, int cards)
    {
        var fight = Fight.Hand(new CardInstance(Id, upgraded)).Energy(3).Enemy(hp: 300);
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 5; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(IC.StrikeIronclad, false));
        }

        int before = fight.State.Energy;
        fight.Play(0);

        // Costs 0, so the energy delta is the gain.
        Assert.Equal(before + energy, fight.State.Energy);
        Assert.Equal(cards, fight.State.Hand.Count);
    }
}

public class BrightestFlameTests
{
    private static int Id => GeneratedData.Cards.FindId("BrightestFlame")!.Value;

    /// <summary>
    /// `EnergyVar(2)` upgrading to 3, `CardsVar(2)` which does NOT upgrade, and
    /// `LoseMaxHp(1)` — the price the card is built around, which was missing entirely.
    /// </summary>
    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 3)]
    public void EnergyUpgradesButTheDrawDoesNot(bool upgraded, int energy)
    {
        var fight = Fight.Hand(new CardInstance(Id, upgraded)).Energy(3).Enemy(hp: 300);
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 5; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(IC.StrikeIronclad, false));
        }

        int before = fight.State.Energy;
        fight.Play(0);

        Assert.Equal(before + energy, fight.State.Energy);
        Assert.Equal(2, fight.State.Hand.Count);
    }

    [Fact]
    public void ItCostsAPointOfMaxHp()
    {
        var fight = Fight.Hand(new CardInstance(Id, false)).Energy(3).Enemy(hp: 300);
        int max = fight.State.PlayerMaxHp;

        fight.Play(0);

        Assert.Equal(max - 1, fight.State.PlayerMaxHp);
    }

    /// <summary>
    /// `LoseMaxHp` deals the excess as Unblockable damage rather than clamping, so a
    /// player at full health loses a point of current HP with the cap.
    /// </summary>
    [Fact]
    public void AtFullHealthItCostsCurrentHpToo()
    {
        var fight = Fight.Hand(new CardInstance(Id, false)).Energy(3).Enemy(hp: 300);
        fight.State.PlayerHp = fight.State.PlayerMaxHp;
        int hp = fight.State.PlayerHp;

        fight.Play(0);

        Assert.Equal(hp - 1, fight.State.PlayerHp);
    }
}

/// <summary>
/// `Entrench` doubles the block you have — `GainBlock(Block, Unpowered | Move)`. UNPOWERED,
/// so Dexterity does not apply to the doubling; it was going through the powered path and
/// paying Dexterity twice on a card whose whole point is the block already there.
/// </summary>
public class EntrenchTests
{
    private static int Id => GeneratedData.Cards.FindId("Entrench")!.Value;

    [Fact]
    public void ItDoublesTheBlockYouHave()
    {
        var fight = Fight.Hand(new CardInstance(Id, false)).Energy(9).Enemy(hp: 300);
        fight.State.PlayerBlock = 12;

        fight.Play(0);

        Assert.Equal(24, fight.State.PlayerBlock);
    }

    [Fact]
    public void DexterityDoesNotRideTheDoubling()
    {
        var plain = Fight.Hand(new CardInstance(Id, false)).Energy(9).Enemy(hp: 300);
        var dexterous = Fight.Hand(new CardInstance(Id, false)).Energy(9).Enemy(hp: 300);
        plain.State.PlayerBlock = 12;
        dexterous.State.PlayerBlock = 12;
        BuffSystem.Apply(dexterous.State.PlayerBuffs, BuffId.Dexterity, 5);

        plain.Play(0);
        dexterous.Play(0);

        Assert.Equal(plain.State.PlayerBlock, dexterous.State.PlayerBlock);
    }
}

/// <summary>
/// `DualWield`: the player picks an ATTACK or POWER in hand, and gets `CardsVar(1)` clones
/// of it — one, or two upgraded. The emulator duplicated the FIRST card in hand whatever
/// it was, two or three times: no choice, no filter, and the wrong count both ways.
/// </summary>
public class DualWieldTests
{
    private static int Id => GeneratedData.Cards.FindId("DualWield")!.Value;

    private static Fight WithHand(params CardInstance[] rest)
    {
        var cards = new[] { new CardInstance(Id, false) }.Concat(rest).ToArray();
        return Fight.Hand(cards).Energy(9).Enemy(hp: 300);
    }

    [Fact]
    public void ItAsksWhichCardToCopy()
    {
        var fight = WithHand(
            new CardInstance(IC.DefendIronclad, false),
            new CardInstance(IC.Bash, false)
        );

        fight.Play(0);

        Assert.NotNull(fight.State.PendingSelection);
        Assert.Equal(CardSelectionKind.DualWield, fight.State.PendingSelection!.Kind);
    }

    /// <summary>Only Attacks and Powers are on the screen — a Defend is not a candidate.</summary>
    [Fact]
    public void OnlyAttacksAndPowersAreOffered()
    {
        var fight = WithHand(
            new CardInstance(IC.DefendIronclad, false),
            new CardInstance(IC.Bash, false)
        );

        fight.Play(0);

        var offered = fight
            .State.PendingSelection!.Candidates.Select(i =>
                GeneratedData.Cards.Get(fight.State.Hand[i].DefId).Type
            )
            .ToList();
        Assert.All(offered, type => Assert.True(type is CardType.Attack or CardType.Power));
        Assert.NotEmpty(offered);
    }

    /// <summary>ONE clone, not two — `CardsVar(1)`.</summary>
    [Fact]
    public void ItMakesOneCloneOfTheChosenCard()
    {
        var fight = WithHand(new CardInstance(IC.Bash, false));
        fight.Play(0);

        int index = fight.State.PendingSelection!.Candidates.IndexOf(
            fight.State.Hand.FindIndex(card => card.DefId == IC.Bash)
        );
        fight.State.PendingSelection = fight.State.PendingSelection;
        CombatEngine.Step(fight.State, index, new System.Random(0));

        Assert.Equal(2, fight.State.Hand.Count(card => card.DefId == IC.Bash));
    }

    /// <summary>Two upgraded.</summary>
    [Fact]
    public void UpgradedItMakesTwo()
    {
        var fight = Fight
            .Hand(new CardInstance(Id, true), new CardInstance(IC.Bash, false))
            .Energy(9)
            .Enemy(hp: 300);
        fight.Play(0);

        CombatEngine.Step(fight.State, 0, new System.Random(0));

        Assert.Equal(3, fight.State.Hand.Count(card => card.DefId == IC.Bash));
    }

    /// <summary>A hand with nothing copyable simply does nothing.</summary>
    [Fact]
    public void AHandOfSkillsOffersNothing()
    {
        var fight = WithHand(new CardInstance(IC.DefendIronclad, false));

        fight.Play(0);

        Assert.Null(fight.State.PendingSelection);
    }
}
