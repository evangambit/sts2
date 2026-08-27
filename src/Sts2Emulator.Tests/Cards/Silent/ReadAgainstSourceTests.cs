using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// Cards with no `approximation` comment on them, read against the decompiled source
// anyway. Every one of these was plainly implemented and plainly wrong, which is the
// point: the label was never what marked the risk.

public class HazeTests
{
    /// <summary>
    /// `PowerVar<PoisonPower>(4m)`, applied to every hittable enemy. The emulator gave
    /// Weak to ONE — the wrong debuff, on the wrong number of creatures.
    /// </summary>
    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, 6)]
    public void ItPoisonsEveryEnemy(bool upgraded, int poison)
    {
        var fight = Fight.Encounter(CombatFactory.ActOneEncounter.Bowlbugs);
        fight.State.Hand = [Card(SI.Haze, upgraded)];
        fight.State.Energy = 3;

        fight.Play();

        Assert.All(
            fight.State.Enemies.Where(e => e.Hp > 0),
            e => Assert.Equal(poison, BuffSystem.Get(e.Buffs, BuffId.Poison))
        );
        Assert.All(fight.State.Enemies, e => Assert.Equal(0, BuffSystem.Get(e.Buffs, BuffId.Weak)));
    }
}

public class BulletTimeTests
{
    /// <summary>
    /// `NoDrawPower`, not NoBlock. The comment beside the line said "prevent draw" and the
    /// line applied the wrong buff, so the card stopped the player BLOCKING and left them
    /// drawing freely — a card that reads as a combo enabler played as a self-debuff.
    /// </summary>
    [Fact]
    public void TheHandIsFreeAndDrawingStops()
    {
        var fight = Fight.Hand(Card(SI.BulletTime), Card(SI.StrikeSilent)).Energy(3);
        fight.State.DrawPile.Clear();
        fight.State.DrawPile.Add(new CardInstance(SI.DefendSilent, false));

        fight.Play();

        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.NoDraw));
        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.NoBlock));
        Assert.All(fight.State.Hand, c => Assert.True(c.FreeThisTurn));

        CardEffects.DrawCards(fight.State, 1, new Random(0));
        Assert.DoesNotContain(fight.State.Hand, c => c.DefId == SI.DefendSilent);
    }
}

public class AnticipateTests
{
    /// <summary>
    /// The var is a `PowerVar<DexterityPower>`, but the card applies `AnticipatePower` —
    /// a `TemporaryDexterityPower`, handed back at the end of the turn. Reading the var
    /// rather than the Apply gave the player permanent Dexterity from a 0-cost common.
    /// </summary>
    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 3)]
    public void TheDexterityLastsOneTurn(bool upgraded, int dexterity)
    {
        var fight = Fight.Hand(Card(SI.Anticipate, upgraded)).Energy(1);
        fight.State.PlayerHp = 999;

        fight.Play();
        Assert.Equal(dexterity, fight.PlayerBuffAmount(BuffId.Dexterity));

        fight.EndTurn();

        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.Dexterity));
    }
}

public class ExposeTests
{
    /// <summary>
    /// `PowerCmd.Remove&lt;ArtifactPower&gt;` takes the whole power off, not one stack — and
    /// it happens BEFORE the Vulnerable, so the debuff always lands. Consuming a single
    /// charge left a two-Artifact enemy holding one, which then swallowed the Vulnerable
    /// this card exists to apply.
    /// </summary>
    [Fact]
    public void ItStripsEveryArtifactStackSoTheVulnerableLands()
    {
        var fight = Fight.Hand(Card(SI.Expose)).Energy(1).Enemy(hp: 60);
        BuffSystem.Apply(fight.Enemy0.Buffs, BuffId.Artifact, 2);
        fight.Enemy0.Block = 12;

        fight.Play();

        Assert.Equal(0, fight.Enemy0.Block);
        Assert.Equal(0, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Artifact));
        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Vulnerable));
    }
}

public class MalaiseTests
{
    /// <summary>
    /// X, plus one more when upgraded — `if (base.IsUpgraded) powerAmount++`, which the
    /// emulator ignored entirely. And the Strength loss is a plain `StrengthPower(-X)`:
    /// PERMANENT, not the temporary one that is handed back at the end of the turn.
    /// </summary>
    [Theory]
    [InlineData(false, 3, 3)]
    [InlineData(true, 3, 4)]
    public void ItSpendsXAndTheLossIsPermanent(bool upgraded, int energy, int amount)
    {
        var fight = Fight.Hand(Card(SI.Malaise, upgraded)).Energy(energy).Enemy(hp: 60);
        fight.State.PlayerHp = 999;

        fight.Play();

        Assert.Equal(-amount, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Strength));
        Assert.Equal(amount, fight.EnemyBuffAmount(BuffId.Weak));
        Assert.Equal(0, fight.State.Energy);

        fight.EndTurn();

        // Still gone next turn: a temporary loss would have been handed back.
        Assert.Equal(-amount, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Strength));
    }
}

public class MirageTests
{
    /// <summary>
    /// `CalculatedBlockVar` with a multiplier of the total PoisonPower across living
    /// enemies, base 0 and extra 1 — so the block IS the poison on the board, and the
    /// upgrade is a cost cut rather than a bigger number. The emulator gave a flat 10/14.
    /// </summary>
    [Fact]
    public void TheBlockIsTheTotalPoisonOnTheBoard()
    {
        var fight = Fight.Encounter(CombatFactory.ActOneEncounter.Bowlbugs);
        fight.State.Hand = [Card(SI.Mirage)];
        fight.State.Energy = 3;
        var living = fight.State.Enemies.Where(e => e.Hp > 0).ToList();
        BuffSystem.Apply(living[0].Buffs, BuffId.Poison, 7);
        BuffSystem.Apply(living[1].Buffs, BuffId.Poison, 5);

        fight.Play();

        Assert.Equal(12, fight.State.PlayerBlock);
    }

    [Fact]
    public void WithNoPoisonItBlocksNothing()
    {
        var fight = Fight.Hand(Card(SI.Mirage)).Energy(3).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(0, fight.State.PlayerBlock);
    }
}

public class GrandFinaleTests
{
    /// <summary>
    /// `IsPlayable => draw pile is empty` is a PLAYABILITY rule, so the action mask has to
    /// carry it. The emulator checked it inside the effect and dealt nothing otherwise,
    /// which is a different game: the play was allowed, the card and the energy were
    /// spent, and an agent was offered an action the real game does not have.
    /// </summary>
    [Fact]
    public void ItIsNotOfferedWhileTheDrawPileHasCards()
    {
        var fight = Fight.Hand(Card(SI.GrandFinale)).Energy(3).Enemy(hp: 200);
        fight.State.DrawPile.Clear();
        fight.State.DrawPile.Add(new CardInstance(SI.StrikeSilent, false));

        Assert.DoesNotContain(0, CombatEngine.ValidActions(fight.State));
    }

    [Theory]
    [InlineData(false, 60)]
    [InlineData(true, 75)]
    public void WithAnEmptyDrawPileItIsOfferedAndHitsEveryone(bool upgraded, int damage)
    {
        var fight = Fight.Hand(Card(SI.GrandFinale, upgraded)).Energy(3).Enemy(hp: 200);
        fight.State.DrawPile.Clear();

        Assert.Contains(0, CombatEngine.ValidActions(fight.State));
        fight.Play();

        Assert.Equal(200 - damage, fight.Enemy0.Hp);
    }
}
