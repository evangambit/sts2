using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Forge and the Sovereign Blade — the Regent's other mechanic, and the emulator had
/// neither.
/// </summary>
/// <remarks>
/// `ForgeCmd.Forge(amount)` gives the player a SOVEREIGN BLADE if they hold no un-exhausted
/// one, then adds that much damage to EVERY blade they hold, exhausted ones included. Ten
/// cards forge. The blade is a 2-cost Retain Token that hits for ten plus everything forged
/// into it, once — `SetRepeats` has no callers anywhere in the game — and reads two inert
/// powers: SeekingEdge makes it hit every enemy, Parry gives it block after the attack.
///
/// The emulator's blade hit once for the printed ten and then DOUBLED the player's block,
/// which is not an effect this card has.
/// </remarks>
public class ForgeTests
{
    private const int SovereignBlade = 448;
    private const int RefineBlade = 389; // Forge 9
    private const int SeekingEdge = 418; // Forge 7 and the power
    private const int Parry = 344; // ParryPower 10

    private static Fight Fresh() => Fight.Hand().Energy(9).Enemy(hp: 500);

    private static Fight Forged(int times = 1)
    {
        var fight = Fresh();
        for (int i = 0; i < times; i++)
        {
            fight.State.Hand.Add(new CardInstance(RefineBlade, false));
            fight.Play(fight.State.Hand.Count - 1);
        }

        return fight;
    }

    [Fact]
    public void TheFirstForgeMakesABlade()
    {
        var fight = Forged();

        Assert.Single(fight.State.Hand.Where(c => c.DefId == SovereignBlade));
    }

    [Fact]
    public void ASecondForgeMakesNoSecondBlade()
    {
        var fight = Forged(times: 2);

        Assert.Single(fight.State.Hand.Where(c => c.DefId == SovereignBlade));
    }

    [Fact]
    public void EveryForgeAddsToTheBladeItAlreadyMade()
    {
        var fight = Forged(times: 2);

        var blade = fight.State.Hand.First(c => c.DefId == SovereignBlade);
        Assert.Equal(18, blade.BonusDamage);
    }

    [Fact]
    public void TheBladeHitsForTenPlusWhatWasForged()
    {
        var fight = Forged();
        int index = fight.State.Hand.FindIndex(c => c.DefId == SovereignBlade);

        fight.Play(index, target: 0);

        Assert.Equal(500 - 19, fight.Enemy0.Hp);
    }

    /// <summary>Seeking Edge forges 7 AND makes the blade hit everyone.</summary>
    [Fact]
    public void SeekingEdgeMakesTheBladeHitEveryEnemy()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(SeekingEdge, false));
        fight.Play(0);
        int index = fight.State.Hand.FindIndex(c => c.DefId == SovereignBlade);

        fight.Play(index, target: 0);

        Assert.Equal(500 - 17, fight.Enemy0.Hp);
        Assert.Equal(500 - 17, fight.State.Enemies[1].Hp);
    }

    /// <summary>Parry gives the blade block AFTER its attack — one per point.</summary>
    [Fact]
    public void ParryGivesTheBladeBlock()
    {
        var fight = Forged();
        fight.State.Hand.Add(new CardInstance(Parry, false));
        fight.Play(fight.State.Hand.Count - 1);
        int index = fight.State.Hand.FindIndex(c => c.DefId == SovereignBlade);

        fight.Play(index, target: 0);

        Assert.Equal(10, fight.State.PlayerBlock);
    }

    [Fact]
    public void WithoutParryTheBladeGainsNoBlock()
    {
        var fight = Forged();
        int index = fight.State.Hand.FindIndex(c => c.DefId == SovereignBlade);

        fight.Play(index, target: 0);

        Assert.Equal(0, fight.State.PlayerBlock);
    }

    /// <summary>An exhausted blade keeps growing, which is why Summon Forth can fetch it.</summary>
    [Fact]
    public void AnExhaustedBladeStillGrows()
    {
        var fight = Forged();
        var blade = fight.State.Hand.First(c => c.DefId == SovereignBlade);
        fight.State.Hand.Remove(blade);
        fight.State.ExhaustPile.Add(blade);

        fight.State.Hand.Add(new CardInstance(RefineBlade, false));
        fight.Play(fight.State.Hand.Count - 1);

        Assert.Equal(18, fight.State.ExhaustPile.First(c => c.DefId == SovereignBlade).BonusDamage);
        // And a fresh one was made, because the exhausted one does not count as held.
        Assert.Single(fight.State.Hand.Where(c => c.DefId == SovereignBlade));
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Conqueror.cs: Forge 3/5, then ConquerorPower 1 on the
// TARGET. That power doubles a SOVEREIGN BLADE's damage against it —
// `cardSource is SovereignBlade` and nothing else — and decrements when its owner's side
// turn ends.
public class ConquerorTests
{
    private const int Conqueror = 100;
    private const int SovereignBlade = 448;
    private const int StrikeRegent = 474;

    [Fact]
    public void ItForgesAndMarksTheTarget()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Conqueror, false));

        fight.Play(0, target: 0);

        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Conqueror));
        Assert.Single(fight.State.Hand.Where(c => c.DefId == SovereignBlade));
    }

    [Fact]
    public void TheBladeHitsAMarkedEnemyTwice()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Conqueror, false));
        fight.Play(0, target: 0);
        int index = fight.State.Hand.FindIndex(c => c.DefId == SovereignBlade);

        fight.Play(index, target: 0);

        // 10 forged to 13, doubled.
        Assert.Equal(500 - 26, fight.Enemy0.Hp);
    }

    /// <summary>`cardSource is SovereignBlade`: nothing else is doubled.</summary>
    [Fact]
    public void OtherAttacksAreNotDoubled()
    {
        var control = Fight.Hand().Energy(9).Enemy(hp: 500);
        control.State.Hand.Add(new CardInstance(StrikeRegent, false));
        control.Play(0, target: 0);
        int plain = 500 - control.Enemy0.Hp;

        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        BuffSystem.Apply(fight.Enemy0.Buffs, BuffId.Conqueror, 1);
        fight.State.Hand.Add(new CardInstance(StrikeRegent, false));
        fight.Play(0, target: 0);

        Assert.Equal(500 - plain, fight.Enemy0.Hp);
    }

    [Fact]
    public void ItIsGoneAfterTheEnemysTurn()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Conqueror, false));
        fight.Play(0, target: 0);

        fight.EndTurn();

        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Conqueror));
    }
}
