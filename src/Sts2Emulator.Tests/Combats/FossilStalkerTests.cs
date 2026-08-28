using Sts2Emulator.Core;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// FossilStalkerNormal: one Fossil Stalker. Read off
/// MegaCrit.Sts2.Core.Models.Monsters/FossilStalker: HP 54-56 at A8 (51-53 below),
/// LatchDamage 12 (14 at A9), TackleDamage 9 (11), LashDamage 3 (4) as two hits, and a
/// SuckPower(3) applied to itself in AfterAddedToRoom.
///
/// <para>
/// Its two riders are both things this suite has had wrong: TACKLE_MOVE is an attack plus
/// a DebuffIntent (Frail 1), announced as an attack; and SuckPower is an
/// <c>AfterAttack</c> hook, so it fires ONCE per attack with the Strength multiplied by
/// the number of hits that landed, rather than per hit as the attack resolves.
/// </para>
/// </summary>
public class FossilStalkerTests
{
    private static Fight Encounter(int ascension = Ascension.DefaultLevel) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.FossilStalker, ascension).PlayerHp(999, 999);

    private const int Latch = 0;
    private const int Tackle = 1;
    private const int Lash = 2;

    [Fact]
    public void RosterIsOneStalker()
    {
        var fight = Encounter();

        Assert.Equal([KE.FossilStalker], fight.EnemyDefIds);
    }

    [Fact]
    public void HpIsRolledInsideTheDeclaredBand()
    {
        var fight = Encounter();

        Assert.InRange(fight.State.Enemies[0].MaxHp, 54, 56);
    }

    [Fact]
    public void HpIsLowerBelowAscensionEight()
    {
        var fight = Encounter(ascension: 7);

        Assert.InRange(fight.State.Enemies[0].MaxHp, 51, 53);
    }

    /// <summary>The machine's initialState is LATCH, and the encounter builds it there.</summary>
    [Fact]
    public void ItOpensOnLatch()
    {
        var fight = Encounter();

        Assert.Equal([(IntentType.Attack, 12)], fight.Intents);
    }

    [Fact]
    public void ItOpensHarderAtAscensionNine()
    {
        var fight = Encounter(ascension: 9);

        Assert.Equal([(IntentType.Attack, 14)], fight.Intents);
    }

    [Fact]
    public void ItCarriesSuckFromTheStart()
    {
        var fight = Encounter();

        Assert.Equal(3, fight.EnemyBuffAmount(BuffId.Suck));
    }

    /// <summary>
    /// Every branch is <c>AddBranch(state, 2)</c>: uniform, and barred only once it has
    /// come up twice running. So no move ever appears three times in a row.
    /// </summary>
    [Fact]
    public void NoMoveComesUpThreeTimesRunning()
    {
        var fight = Encounter();
        var moves = new List<int>();

        for (int turn = 0; turn < 20; turn++)
        {
            moves.Add(fight.State.Enemies[0].LastMove);
            fight.EndTurn();
        }

        for (int i = 2; i < moves.Count; i++)
        {
            Assert.False(
                moves[i] == moves[i - 1] && moves[i] == moves[i - 2],
                $"{moves[i]} came up three times running at turn {i}"
            );
        }
    }

    /// <summary>
    /// SuckPower.AfterAttack grants <c>Amount x</c> the hits that dealt unblocked damage,
    /// once, after the attack -- so a two-hit Lash gives six and both of its hits swing at
    /// the Strength the stalker started the turn with. Applying it per hit fed the first
    /// hit's Strength into the second, which is three damage the game does not deal.
    /// </summary>
    [Fact]
    public void LashGivesThreeStrengthPerHitAndNoneOfItToItself()
    {
        var fight = Encounter();
        var stalker = fight.State.Enemies[0];
        stalker.CurrentIntent = new Intent(IntentType.Attack, 3, Hits: 2);
        stalker.LastMove = Lash;
        int hpBefore = fight.State.PlayerHp;

        EnemyAI.ExecuteIntent(stalker, fight.State, new Random(0));

        Assert.Equal(6, BuffSystem.Get(stalker.Buffs, BuffId.Strength));
        Assert.Equal(hpBefore - 6, fight.State.PlayerHp);
    }

    /// <summary>A hit the player blocks outright is not one of the hits Suck counts.</summary>
    [Fact]
    public void SuckCountsOnlyTheHitsThatLandUnblocked()
    {
        var fight = Encounter();
        var stalker = fight.State.Enemies[0];
        stalker.CurrentIntent = new Intent(IntentType.Attack, 3, Hits: 2);
        stalker.LastMove = Lash;
        fight.State.PlayerBlock = 4;

        EnemyAI.ExecuteIntent(stalker, fight.State, new Random(0));

        Assert.Equal(3, BuffSystem.Get(stalker.Buffs, BuffId.Strength));
    }

    [Fact]
    public void TackleAppliesFrail()
    {
        var fight = Encounter();
        var stalker = fight.State.Enemies[0];
        stalker.CurrentIntent = new Intent(IntentType.Attack, 9);
        stalker.LastMove = Tackle;

        EnemyAI.ExecuteIntent(stalker, fight.State, new Random(0));

        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Frail));
    }

    [Fact]
    public void LatchIsAPlainAttack()
    {
        var fight = Encounter();
        var stalker = fight.State.Enemies[0];
        stalker.CurrentIntent = new Intent(IntentType.Attack, 12);
        stalker.LastMove = Latch;
        int hpBefore = fight.State.PlayerHp;

        EnemyAI.ExecuteIntent(stalker, fight.State, new Random(0));

        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.Frail));
        Assert.Equal(hpBefore - 12, fight.State.PlayerHp);
        Assert.Equal(3, BuffSystem.Get(stalker.Buffs, BuffId.Strength));
    }
}
