using Sts2Emulator.Core;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/Oblivion.cs: a 0-cost Skill applying OblivionPower to
// the TARGET at `PowerVar<DoomPower>(3)`, upgrading by 1. The power is not Doom.
//
// OblivionPower records its amount in BeforeCardPlayed and pays it out as Doom in
// AfterCardPlayed, which is how it avoids triggering on the card that applied it, and it
// removes itself when the PLAYER's side turn ends. The emulator gave the PLAYER Doom.
public class OblivionTests
{
    private const int Oblivion = 331;
    private const int Poke = 357;
    private const int Strike = 473;

    private static Fight Fresh() => Fight.Hand().Energy(9).Enemy(hp: 500);

    [Fact]
    public void ItGoesOnTheEnemyAndIsNotDoom()
    {
        var fight = Fresh();
        fight.State.Hand.Add(new CardInstance(Oblivion, false));

        fight.Play(0, target: 0);

        Assert.Equal(3, fight.EnemyBuffAmount(BuffId.Oblivion));
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Doom));
    }

    [Fact]
    public void TheUpgradeIsFour()
    {
        var fight = Fresh();
        fight.State.Hand.Add(new CardInstance(Oblivion, true));

        fight.Play(0, target: 0);

        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Oblivion));
    }

    /// <summary>Nothing is recorded for the play that applies it, so it does not pay itself.</summary>
    [Fact]
    public void ItDoesNotDoomOnItsOwnPlay()
    {
        var fight = Fresh();
        fight.State.Hand.Add(new CardInstance(Oblivion, false));

        fight.Play(0, target: 0);

        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Doom));
    }

    [Fact]
    public void EveryLaterCardDoomsThatEnemy()
    {
        var fight = Fresh();
        fight.State.Hand.Add(new CardInstance(Oblivion, false));
        fight.Play(0, target: 0);

        fight.State.Hand.Add(new CardInstance(Poke, false));
        fight.Play(0, target: 0);
        Assert.Equal(3, fight.EnemyBuffAmount(BuffId.Doom));

        fight.State.Hand.Add(new CardInstance(Strike, false));
        fight.Play(0, target: 0);
        Assert.Equal(6, fight.EnemyBuffAmount(BuffId.Doom));
    }

    /// <summary>`AfterSideTurnEnd` for the PLAYER's side, so it does not survive the turn.</summary>
    [Fact]
    public void ItIsGoneAfterTheTurn()
    {
        var fight = Fresh();
        fight.State.Hand.Add(new CardInstance(Oblivion, false));
        fight.Play(0, target: 0);

        fight.EndTurn();

        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Oblivion));
    }

    [Fact]
    public void WithoutItCardsDoomNobody()
    {
        var fight = Fresh();
        fight.State.Hand.Add(new CardInstance(Poke, false));

        fight.Play(0, target: 0);

        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Doom));
    }
}
