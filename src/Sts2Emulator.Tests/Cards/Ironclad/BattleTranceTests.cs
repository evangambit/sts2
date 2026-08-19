using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 0-cost Skill. MegaCrit.Sts2.Core.Models.Cards/BattleTrance.cs: CardsVar(3) drawn, then
// PowerCmd.Apply<NoDrawPower>(1) — no further drawing this turn. OnUpgrade raises the
// draw to 4.
//
// NoDrawPower is not modelled, so these tests cover the draw only.
public class BattleTranceTests
{
    [Fact]
    public void DrawsThree()
    {
        var fight = Fight
            .Hand(Card(IC.BattleTrance))
            .Energy(1)
            .Draw(Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.Anger))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal([IC.Bash, IC.StrikeIronclad, IC.DefendIronclad], Fight.Ids(fight.State.Hand));
    }

    [Fact]
    public void UpgradedDrawsFour()
    {
        var fight = Fight
            .Hand(Card(IC.BattleTrance, upgraded: true))
            .Energy(1)
            .Draw(Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.Anger))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(4, fight.State.Hand.Count);
    }

    [Fact]
    public void DrawsWhatItCanFromAShortDrawPile()
    {
        var fight = Fight.Hand(Card(IC.BattleTrance)).Energy(1).Draw(Card(IC.Bash)).Enemy(hp: 40);

        fight.Play();

        Assert.Equal([IC.Bash], Fight.Ids(fight.State.Hand));
    }
}
