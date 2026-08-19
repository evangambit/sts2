using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 0-cost Skill. MegaCrit.Sts2.Core.Models.Cards/Rage.cs applies RagePower at
// DynamicVar("Power", 3m) — block whenever you play an Attack this turn; OnUpgrade
// raises it by 2.
public class RageTests
{
    [Fact]
    public void AppliesThree()
    {
        var fight = Fight.Hand(Card(IC.Rage)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(3, fight.PlayerBuffAmount(BuffId.Rage));
    }

    [Fact]
    public void UpgradedAppliesFive()
    {
        var fight = Fight.Hand(Card(IC.Rage, upgraded: true)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(5, fight.PlayerBuffAmount(BuffId.Rage));
    }

    [Fact]
    public void GivesBlockForEachAttackPlayed()
    {
        var fight = Fight
            .Hand(Card(IC.Rage), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad))
            .Energy(9)
            .Enemy(hp: 60);
        fight.Play(index: 0);

        fight.Play(index: 0);
        fight.Play(index: 0);

        Assert.Equal(6, fight.State.PlayerBlock);
    }

    [Fact]
    public void GivesNoBlockForASkill()
    {
        var fight = Fight
            .Hand(Card(IC.Rage), Card(IC.ShrugItOff))
            .Energy(9)
            .Draw(Card(IC.Bash))
            .Enemy(hp: 40);
        fight.Play(index: 0);

        fight.Play(index: 0);

        // Shrug It Off's own 8, with nothing added by Rage.
        Assert.Equal(8, fight.State.PlayerBlock);
    }
}
