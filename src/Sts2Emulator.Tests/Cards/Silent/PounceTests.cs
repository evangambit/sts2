using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 2-cost Attack. MegaCrit.Sts2.Core.Models.Cards/Pounce.cs: DamageVar(14m) then
// PowerCmd.Apply<FreeSkillPower>(1) — the next Skill costs nothing; OnUpgrade raises the
// damage by 6.
public class PounceTests
{
    [Fact]
    public void DealsFourteenAndMakesTheNextSkillFree()
    {
        var fight = Fight
            .Hand(Card(SI.Pounce), Card(IC.ShrugItOff))
            .Energy(2)
            .Draw(Card(IC.Bash))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(26, fight.Enemy0.Hp);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.FreeSkillPower));
    }

    [Fact]
    public void UpgradedDealsTwenty()
    {
        var fight = Fight.Hand(Card(SI.Pounce, upgraded: true)).Energy(2).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(20, fight.Enemy0.Hp);
    }

    [Fact]
    public void TheFreeSkillCostsNoEnergyAndIsSpentOnce()
    {
        var fight = Fight
            .Hand(Card(SI.Pounce), Card(IC.ShrugItOff), Card(IC.ShrugItOff))
            .Energy(2)
            .Draw(Card(IC.Bash), Card(IC.Bash))
            .Enemy(hp: 40);
        fight.Play(index: 0);
        int energyAfterPounce = fight.State.Energy;

        fight.Play(index: 0);

        Assert.Equal(energyAfterPounce, fight.State.Energy);
        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.FreeSkillPower));
    }

    [Fact]
    public void DoesNotMakeTheNextAttackFree()
    {
        var fight = Fight.Hand(Card(SI.Pounce), Card(IC.StrikeIronclad)).Energy(3).Enemy(hp: 60);
        fight.Play(index: 0);
        int energyAfterPounce = fight.State.Energy;

        fight.Play(index: 0);

        Assert.Equal(energyAfterPounce - 1, fight.State.Energy);
    }
}
