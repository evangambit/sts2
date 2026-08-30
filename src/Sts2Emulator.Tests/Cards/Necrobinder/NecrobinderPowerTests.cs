using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/Countdown.cs: PowerVar 6 upgrading by 3.
// `CountdownPower.AfterSideTurnStart` Dooms ONE random hittable enemy for its amount at
// the start of every player turn, rolled on the CombatTargets stream. The emulator was
// granting The Bomb — a different card with a different payload.
public class CountdownTests
{
    private const int Countdown = 109;

    [Fact]
    public void ItAppliesSixAndNineUpgraded()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Countdown, false));
        fight.Play(0);
        Assert.Equal(6, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Countdown));

        var upgraded = Fight.Hand().Energy(9).Enemy(hp: 500);
        upgraded.State.Hand.Add(new CardInstance(Countdown, true));
        upgraded.Play(0);
        Assert.Equal(9, BuffSystem.Get(upgraded.State.PlayerBuffs, BuffId.Countdown));
    }

    [Fact]
    public void ItDoomsAnEnemyAtTheStartOfEachTurn()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Countdown, false));
        fight.Play(0);
        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Doom));

        fight.EndTurn();
        Assert.Equal(6, fight.EnemyBuffAmount(BuffId.Doom));

        fight.EndTurn();
        Assert.Equal(12, fight.EnemyBuffAmount(BuffId.Doom));
    }

    [Fact]
    public void ItGrantsNoBomb()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Countdown, false));

        fight.Play(0);

        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.TheBombPower));
    }
}

// MegaCrit.Sts2.Core.Models.Cards/DevourLife.cs: PowerVar 1 upgrading by 1.
// `DevourLifePower.AfterCardPlayed` summons Osty for its amount when the card played is a
// SOUL. The emulator was granting Noxious Fumes.
public class DevourLifeTests
{
    private const int DevourLife = 144;
    private const int Soul = 446;
    private const int Strike = 473;

    private static Fight Armed(bool upgraded = false)
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        CardEffects.SummonOsty(fight.State, 1);
        fight.State.Hand.Add(new CardInstance(DevourLife, upgraded));
        fight.Play(0);
        return fight;
    }

    [Fact]
    public void PlayingASoulSummons()
    {
        var fight = Armed();
        int before = fight.State.OstyMaxHp;
        fight.State.Hand.Add(new CardInstance(Soul, false));

        fight.Play(0);

        Assert.Equal(before + 1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void TheUpgradeSummonsForTwo()
    {
        var fight = Armed(upgraded: true);
        int before = fight.State.OstyMaxHp;
        fight.State.Hand.Add(new CardInstance(Soul, false));

        fight.Play(0);

        Assert.Equal(before + 2, fight.State.OstyMaxHp);
    }

    [Fact]
    public void AnyOtherCardSummonsNothing()
    {
        var fight = Armed();
        int before = fight.State.OstyMaxHp;
        fight.State.Hand.Add(new CardInstance(Strike, false));

        fight.Play(0, target: 0);

        Assert.Equal(before, fight.State.OstyMaxHp);
    }

    [Fact]
    public void ItGrantsNoNoxiousFumes()
    {
        Assert.Equal(0, BuffSystem.Get(Armed().State.PlayerBuffs, BuffId.NoxiousFumes));
    }
}

// MegaCrit.Sts2.Core.Models.Cards/ForbiddenGrimoire.cs: an Ancient, Eternal 2-cost Power
// applying one stack; the upgrade is a discount.
//
// `ForbiddenGrimoirePower.AfterCombatEnd` adds that many extra card-REMOVAL rewards to the
// fight. The emulator has no removal reward row, so the power is tracked and its payout is
// NOT modelled — a stated gap, not an oversight. It was granting Dark Embrace, which is a
// different card entirely.
public class ForbiddenGrimoireTests
{
    private const int ForbiddenGrimoire = 203;

    private static Fight Played(bool upgraded = false)
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(ForbiddenGrimoire, upgraded));
        fight.Play(0);
        return fight;
    }

    [Fact]
    public void ItAppliesOneStackWhetherUpgradedOrNot()
    {
        Assert.Equal(1, BuffSystem.Get(Played().State.PlayerBuffs, BuffId.ForbiddenGrimoire));
        Assert.Equal(1, BuffSystem.Get(Played(true).State.PlayerBuffs, BuffId.ForbiddenGrimoire));
    }

    [Fact]
    public void ItGrantsNoDarkEmbrace()
    {
        Assert.Equal(0, BuffSystem.Get(Played().State.PlayerBuffs, BuffId.DarkEmbrace));
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Lethality.cs: an Ethereal 1-cost Power, PowerVar 50
// upgrading by 25 — a PERCENTAGE. `LethalityPower.ModifyDamageMultiplicative` returns
// `1 + Amount/100`, but only while the turn's Attack-play count is still one, and never
// for a repeat of the same play.
public class LethalityTests
{
    private const int Lethality = 285;
    private const int Strike = 473;

    private static Fight Armed(bool upgraded = false)
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Lethality, upgraded));
        fight.Play(0);
        return fight;
    }

    private static int StrikeDamage(Fight fight)
    {
        fight.State.Hand.Add(new CardInstance(Strike, false));
        int before = fight.Enemy0.Hp;
        fight.Play(0, target: 0);
        return before - fight.Enemy0.Hp;
    }

    [Fact]
    public void TheFirstAttackOfTheTurnHitsHalfAgain()
    {
        var control = Fight.Hand().Energy(9).Enemy(hp: 500);
        int plain = StrikeDamage(control);

        Assert.Equal((int)(plain * 1.5f), StrikeDamage(Armed()));
    }

    [Fact]
    public void TheUpgradeIsSeventyFivePercent()
    {
        var control = Fight.Hand().Energy(9).Enemy(hp: 500);
        int plain = StrikeDamage(control);

        Assert.Equal((int)(plain * 1.75f), StrikeDamage(Armed(upgraded: true)));
    }

    /// <summary>Only the FIRST — the second Attack of the turn is plain.</summary>
    [Fact]
    public void TheSecondAttackGetsNothing()
    {
        var control = Fight.Hand().Energy(9).Enemy(hp: 500);
        int plain = StrikeDamage(control);

        var fight = Armed();
        StrikeDamage(fight);

        Assert.Equal(plain, StrikeDamage(fight));
    }

    /// <summary>The count is per TURN, so the next turn's first Attack pays again.</summary>
    [Fact]
    public void TheNextTurnsFirstAttackPaysAgain()
    {
        var control = Fight.Hand().Energy(9).Enemy(hp: 500);
        int plain = StrikeDamage(control);

        var fight = Armed();
        StrikeDamage(fight);
        fight.EndTurn();
        fight.State.Energy = 9;

        Assert.Equal((int)(plain * 1.5f), StrikeDamage(fight));
    }
}

// MegaCrit.Sts2.Core.Models.Cards/SpiritOfAsh.cs: a 1-cost Power whose var is called
// "BlockOnExhaust" at 4, upgrading by 1 — and whose hook is
// `SpiritOfAshPower.BeforeCardPlayed`, gaining that much Unpowered block when the card
// played is ETHEREAL. Nothing to do with exhausting.
public class SpiritOfAshTests
{
    private const int SpiritOfAsh = 453;
    private const int Defile = 135; // Ethereal
    private const int Strike = 473;

    private static Fight Armed(bool upgraded = false)
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(SpiritOfAsh, upgraded));
        fight.Play(0);
        return fight;
    }

    [Fact]
    public void AnEtherealCardPlayedGainsFourBlock()
    {
        var fight = Armed();
        fight.State.Hand.Add(new CardInstance(Defile, false));

        fight.Play(0, target: 0);

        Assert.Equal(4, fight.State.PlayerBlock);
    }

    [Fact]
    public void TheUpgradeGainsFive()
    {
        var fight = Armed(upgraded: true);
        fight.State.Hand.Add(new CardInstance(Defile, false));

        fight.Play(0, target: 0);

        Assert.Equal(5, fight.State.PlayerBlock);
    }

    [Fact]
    public void AnOrdinaryCardGainsNothing()
    {
        var fight = Armed();
        fight.State.Hand.Add(new CardInstance(Strike, false));

        fight.Play(0, target: 0);

        Assert.Equal(0, fight.State.PlayerBlock);
    }
}
