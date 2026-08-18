using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Attack, CardTag.Strike. MegaCrit.Sts2.Core.Models.Cards/PommelStrike.cs:
// DamageVar(9m) and CardsVar(1); OnUpgrade raises damage by 1 AND cards by 1.
public class PommelStrikeTests
{
    [Fact]
    public void DealsNineAndDrawsOne()
    {
        var fight = Fight
            .Hand(Card(IC.PommelStrike))
            .Energy(1)
            .Draw(Card(IC.Bash), Card(IC.DefendIronclad))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(31, fight.Enemy0.Hp);
        Assert.Equal([IC.Bash], Fight.Ids(fight.State.Hand));
    }

    [Fact]
    public void UpgradedDealsTenAndDrawsTwo()
    {
        var fight = Fight
            .Hand(Card(IC.PommelStrike, upgraded: true))
            .Energy(1)
            .Draw(Card(IC.Bash), Card(IC.DefendIronclad))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(30, fight.Enemy0.Hp);
        Assert.Equal([IC.Bash, IC.DefendIronclad], Fight.Ids(fight.State.Hand));
    }

    [Fact]
    public void DrawsNothingFromAnEmptyDrawPile()
    {
        var fight = Fight.Hand(Card(IC.PommelStrike)).Energy(1).Draw().Enemy(hp: 40);

        fight.Play();

        Assert.Equal(31, fight.Enemy0.Hp);
        Assert.Empty(fight.State.Hand);
    }
}
