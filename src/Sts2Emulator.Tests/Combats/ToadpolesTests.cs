using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// ToadpolesWeak: two Toadpoles, the first marked IsFront. Read off
/// MegaCrit.Sts2.Core.Models.Monsters/Toadpole: HP 22-26 at A8 (21-25 below), and a
/// three-move ring — SPIKEN (buff), SPIKE_SPIT (MultiAttack 3x3, 3x4 at A9), WHIRL
/// (7, 8 at A9) — entered at SPIKEN by the front one and at WHIRL by the back one.
/// </summary>
public class ToadpolesTests
{
    private static Fight Encounter(int ascension = Ascension.DefaultLevel) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.Toadpoles, ascension);

    [Fact]
    public void RosterIsTwoToadpoles()
    {
        var fight = Encounter();

        Assert.Equal([KE.Toadpole, KE.Toadpole], fight.EnemyDefIds);
    }

    [Fact]
    public void HpIsRolledInsideTheDeclaredBand()
    {
        var fight = Encounter();

        Assert.All(
            fight.State.Enemies,
            enemy =>
            {
                Assert.InRange(enemy.MaxHp, 22, 26);
                Assert.Equal(enemy.MaxHp, enemy.Hp);
            }
        );
    }

    /// <summary>MinInitialHp/MaxInitialHp drop by one below A8.</summary>
    [Fact]
    public void HpBandIsLowerBelowAscensionEight()
    {
        var fight = Encounter(ascension: 7);

        Assert.All(fight.State.Enemies, enemy => Assert.InRange(enemy.MaxHp, 21, 25));
    }

    /// <summary>
    /// The front Toadpole opens on SPIKEN and the back one on WHIRL — the conditional
    /// branch state is the whole reason the two announce different things on turn one.
    /// </summary>
    [Fact]
    public void FrontBuffsWhileBackAttacksOnTurnOne()
    {
        var fight = Encounter();

        Assert.Equal([(IntentType.Buff, 0), (IntentType.Attack, 7)], fight.Intents);
    }

    [Fact]
    public void WhirlHitsHarderAtAscensionNine()
    {
        var fight = Encounter(ascension: 9);

        Assert.Equal((IntentType.Attack, 8), fight.Intents.Last());
    }

    /// <summary>
    /// SPIKEN -> SPIKE_SPIT -> WHIRL -> SPIKEN for the front one, and the back one runs
    /// the same ring one step ahead. Four turns is enough to see it come round.
    /// </summary>
    [Fact]
    public void MovesRunTheirRingInOrder()
    {
        var fight = Encounter();
        var front = new List<(IntentType, int)>();
        var back = new List<(IntentType, int)>();

        for (int turn = 0; turn < 4; turn++)
        {
            front.Add(fight.Intents.First());
            back.Add(fight.Intents.Last());
            fight.EndTurn();
        }

        // SpikeSpit announces its total (3 hits x 3), not its per-hit damage.
        Assert.Equal(
            [(IntentType.Attack, 7), (IntentType.Buff, 0), (IntentType.Attack, 9)],
            back.Take(3)
        );
        Assert.Equal(
            [(IntentType.Buff, 0), (IntentType.Attack, 9), (IntentType.Attack, 7)],
            front.Take(3)
        );
        Assert.Equal(front[0], front[3]);
    }

    /// <summary>
    /// SpikeSpitMove applies ThornsPower(-SpikenAmount) before it swings, so the Spiken
    /// buff it spent is gone by the time the hits land.
    /// </summary>
    [Fact]
    public void SpikeSpitSpendsTheSpikenThorns()
    {
        var fight = Encounter();
        fight.EndTurn();

        int thornsAfterSpiken = BuffSystem.Get(fight.State.Enemies[0].Buffs, BuffId.Thorns);
        fight.EndTurn();

        Assert.Equal(2, thornsAfterSpiken);
        Assert.Equal(0, BuffSystem.Get(fight.State.Enemies[0].Buffs, BuffId.Thorns));
    }
}
