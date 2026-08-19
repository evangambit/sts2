using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Skill, MultiplayerOnly, TargetType.AnyAlly.
// MegaCrit.Sts2.Core.Models.Cards/Coordinate.cs applies CoordinatePower to the targeted
// ally at PowerVar<StrengthPower>(5m); OnUpgrade raises it by 3. CoordinatePower is a
// TemporaryStrengthPower, so the Strength lasts until the end of the side's turn.
//
// Singleplayer's only ally is the player.
public class CoordinateTests
{
    [Fact]
    public void GivesFiveTemporaryStrength()
    {
        var fight = Fight.Hand(Card(CL.Coordinate)).Energy(2).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(5, fight.PlayerBuffAmount(BuffId.Strength));
        Assert.Equal(5, fight.PlayerBuffAmount(BuffId.TemporaryStrength));
    }

    [Fact]
    public void UpgradedGivesEight()
    {
        var fight = Fight.Hand(Card(CL.Coordinate, upgraded: true)).Energy(2).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(8, fight.PlayerBuffAmount(BuffId.Strength));
    }

    [Fact]
    public void TheStrengthRaisesAnAttackPlayedThisTurn()
    {
        var fight = Fight
            .Hand(Card(CL.Coordinate), Card(IC.StrikeIronclad))
            .Energy(9)
            .Enemy(hp: 60);
        fight.Play(index: 0);

        fight.Play(index: 0);

        Assert.Equal(49, fight.Enemy0.Hp);
    }

    [Fact]
    public void TheStrengthIsGoneAfterTheTurnEnds()
    {
        var fight = Fight.Hand(Card(CL.Coordinate)).Energy(2).Enemy(hp: 40);
        fight.Play();

        fight.EndTurn();

        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.Strength));
    }
}
