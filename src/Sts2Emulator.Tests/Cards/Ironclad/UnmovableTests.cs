using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 2-cost Power. MegaCrit.Sts2.Core.Models.Cards/Unmovable.cs applies UnmovablePower(1) —
// the first block gain each turn is doubled — and OnUpgrade only makes it cheaper.
public class UnmovableTests
{
    [Fact]
    public void AppliesOne()
    {
        var fight = Fight.Hand(Card(IC.Unmovable)).Energy(2).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.UnmovablePower));
    }

    [Fact]
    public void DoublesTheFirstBlockGainOfTheTurn()
    {
        var fight = Fight
            .Hand(Card(IC.Unmovable), Card(IC.DefendIronclad))
            .Energy(9)
            .Draw(Card(IC.Bash))
            .Enemy(hp: 40);
        fight.Play(index: 0);

        fight.Play(index: 0);

        Assert.Equal(10, fight.State.PlayerBlock);
    }

    [Fact]
    public void LeavesTheSecondBlockGainAlone()
    {
        var fight = Fight
            .Hand(Card(IC.Unmovable), Card(IC.DefendIronclad), Card(IC.DefendIronclad))
            .Energy(9)
            .Draw(Card(IC.Bash), Card(IC.Bash))
            .Enemy(hp: 40);
        fight.Play(index: 0);

        fight.Play(index: 0);
        fight.Play(index: 0);

        // The first Defend doubled to 10, the second worth its plain 5.
        Assert.Equal(15, fight.State.PlayerBlock);
    }

    [Fact]
    public void UpgradedCostsOneRatherThanDoublingMore()
    {
        var fight = Fight.Hand(Card(IC.Unmovable, upgraded: true)).Energy(2).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.UnmovablePower));
        Assert.Equal(1, fight.State.Energy);
    }
}
