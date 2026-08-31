using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>`Soul`: `CardsVar(2)` upgrading by 1 — draw TWO, or three. It drew one.</summary>
public class SoulTests
{
    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 3)]
    public void ItDrawsTwoOrThree(bool upgraded, int drawn)
    {
        var fight = Fight.Hand(new CardInstance(446, upgraded)).Energy(9).Enemy();
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 5; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(IC.StrikeIronclad, false));
        }

        fight.Play(0);

        Assert.Equal(drawn, fight.State.Hand.Count);
    }
}

/// <summary>
/// `SporeMind` is a 1-cost Curse with Exhaust and NO `OnPlay` at all — the whole card is
/// paying one energy to be rid of it, Debris's shape as a curse. The emulator applied
/// `NoBlock`, which is invented and one of the harshest debuffs in the game.
/// </summary>
public class SporeMindTests
{
    private static int Id => GeneratedData.Cards.FindId("SporeMind")!.Value;

    [Fact]
    public void PlayingItDoesNothingButRemoveIt()
    {
        var fight = Fight
            .Hand(new CardInstance(Id, false), new CardInstance(IC.DefendIronclad, false))
            .Energy(9)
            .Enemy(hp: 300);

        fight.Play(0);

        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.NoBlock));
        Assert.Contains(fight.State.ExhaustPile, card => card.DefId == Id);
    }

    /// <summary>And the Defend beside it still blocks — the proof `NoBlock` is gone.</summary>
    [Fact]
    public void BlockStillWorksAfterwards()
    {
        var fight = Fight
            .Hand(new CardInstance(Id, false), new CardInstance(IC.DefendIronclad, false))
            .Energy(9)
            .Enemy(hp: 300);
        fight.State.PlayerBlock = 0;

        fight.Play(0);
        fight.Play(0);

        Assert.True(fight.State.PlayerBlock > 0);
    }
}

/// <summary>
/// `FranticEscape` raises the Sandpit on the enemy HOLDING it — `Enemies.FirstOrDefault(c
/// =&gt; c.HasPower&lt;SandpitPower&gt;())` — not on whichever enemy is in front.
/// </summary>
public class FranticEscapeTests
{
    [Fact]
    public void ItFindsTheSandpitEnemyRatherThanTheFirst()
    {
        var fight = Fight
            .Hand(new CardInstance(ST.FranticEscape, false))
            .Energy(9)
            .Enemy(hp: 200)
            .Enemy(hp: 200);
        // The sandpit is on the SECOND enemy.
        BuffSystem.Apply(fight.State.Enemies[1].Buffs, BuffId.Sandpit, 1);

        fight.Play(0);

        Assert.Equal(2, BuffSystem.Get(fight.State.Enemies[1].Buffs, BuffId.Sandpit));
        Assert.Equal(0, BuffSystem.Get(fight.State.Enemies[0].Buffs, BuffId.Sandpit));
    }

    /// <summary>Its own cost rises by one per play — `EnergyCost.AddThisCombat(1)`.</summary>
    [Fact]
    public void ItGetsMoreExpensiveEveryPlay()
    {
        var fight = Fight.Hand(new CardInstance(ST.FranticEscape, false)).Energy(9).Enemy(hp: 200);
        BuffSystem.Apply(fight.State.Enemies[0].Buffs, BuffId.Sandpit, 1);

        fight.Play(0);

        var played = fight.State.DiscardPile.First(card => card.DefId == ST.FranticEscape);
        Assert.Equal(1, played.CostBump);
    }
}

/// <summary>
/// The two Quest cards do nothing in Act 1, and that is the game's answer rather than a
/// gap: Lantern Key's hooks are gated on `CurrentActIndex == 2` and Spoils Map's on its
/// own `SpoilsActIndex` of 1. Both reshape a LATER act's map.
/// </summary>
public class QuestCardTests
{
    [Theory]
    [InlineData("LanternKey")]
    [InlineData("SpoilsMap")]
    public void TheyAreInertInCombat(string name)
    {
        int id = GeneratedData.Cards.FindId(name)!.Value;
        var plain = Fight.Hand().Enemy(hp: 300);
        var quest = Fight.Hand(new CardInstance(id, false)).Enemy(hp: 300);

        Assert.Equal(plain.State.PlayerBlock, quest.State.PlayerBlock);
        Assert.Equal(plain.State.Enemies[0].Hp, quest.State.Enemies[0].Hp);
        Assert.Equal(CardType.Quest, GeneratedData.Cards.Get(id).Type);
    }
}

/// <summary>
/// `Wish` is a TUTOR: `CardSelectCmd.FromCombatPile(PileType.Draw)` — the player picks one
/// card out of the draw pile and takes it into hand. The upgrade adds RETAIN. The emulator
/// paid GOLD, which is not something this card does and not something a combat card
/// generally does.
/// </summary>
public class WishTests
{
    private static int Id => GeneratedData.Cards.FindId("Wish")!.Value;

    private static Fight Ready()
    {
        var fight = Fight.Hand(new CardInstance(Id, false)).Energy(9).Enemy(hp: 300);
        fight.State.DrawPile.Clear();
        fight.State.DrawPile.Add(new CardInstance(IC.Bash, false));
        fight.State.DrawPile.Add(new CardInstance(IC.StrikeIronclad, false));
        return fight;
    }

    [Fact]
    public void ItAsksWhichCardToTakeFromTheDrawPile()
    {
        var fight = Ready();

        fight.Play(0);

        Assert.NotNull(fight.State.PendingSelection);
        Assert.Equal(CardSelectionKind.DrawPileToHand, fight.State.PendingSelection!.Kind);
        Assert.Equal(2, fight.State.PendingSelection.Candidates.Count);
    }

    [Fact]
    public void TheChosenCardMovesToHand()
    {
        var fight = Ready();
        fight.Play(0);

        CombatEngine.Step(fight.State, 0, new System.Random(0));

        Assert.Contains(fight.State.Hand, card => card.DefId == IC.Bash);
        Assert.DoesNotContain(fight.State.DrawPile, card => card.DefId == IC.Bash);
    }

    [Fact]
    public void ItPaysNoGold()
    {
        var fight = Ready();
        int gold = fight.State.PlayerGold;

        fight.Play(0);

        Assert.Equal(gold, fight.State.PlayerGold);
    }

    /// <summary>An empty draw pile has nothing to wish for.</summary>
    [Fact]
    public void AnEmptyDrawPileOffersNothing()
    {
        var fight = Fight.Hand(new CardInstance(Id, false)).Energy(9).Enemy(hp: 300);
        fight.State.DrawPile.Clear();

        fight.Play(0);

        Assert.Null(fight.State.PendingSelection);
    }

    /// <summary>The upgrade buys RETAIN, not a bigger effect.</summary>
    [Fact]
    public void TheUpgradeIsRetain()
    {
        Assert.True(GeneratedData.Cards.Get(Id).RetainWhenUpgraded);
    }
}
