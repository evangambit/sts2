using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Skill, CardTag.Defend. MegaCrit.Sts2.Core.Models.Cards/DefendIronclad.cs:
// BlockVar(5m); OnUpgrade raises it by 3. Like Strike, it has no case in
// CardEffects.Apply and runs on the generic damage-and-block path.
public class DefendIroncladTests
{
    [Fact]
    public void GainsFiveBlock()
    {
        var fight = Fight.Hand(Card(IC.DefendIronclad)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(5, fight.State.PlayerBlock);
    }

    [Fact]
    public void UpgradedGainsEight()
    {
        var fight = Fight.Hand(Card(IC.DefendIronclad, upgraded: true)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(8, fight.State.PlayerBlock);
    }

    [Fact]
    public void BlockAccumulatesAcrossPlays()
    {
        var fight = Fight
            .Hand(Card(IC.DefendIronclad), Card(IC.DefendIronclad))
            .Energy(9)
            .Enemy(hp: 40);

        fight.Play(index: 0);
        fight.Play(index: 0);

        Assert.Equal(10, fight.State.PlayerBlock);
    }

    [Fact]
    public void IsRaisedByDexterity()
    {
        var fight = Fight
            .Hand(Card(IC.DefendIronclad))
            .Energy(1)
            .PlayerBuff(BuffId.Dexterity, 3)
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(8, fight.State.PlayerBlock);
    }
}
