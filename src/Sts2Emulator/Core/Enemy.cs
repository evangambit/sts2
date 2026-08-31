namespace Sts2Emulator.Core;

public enum IntentType
{
    Attack,
    Defend,
    Buff,
    Debuff,
    Unknown,
}

public readonly record struct EnemyDef(
    int Id,
    string Name,
    int MinHp,
    int MaxHp,
    int[] Moves, // flat [damage, repeats, damage, repeats, ...] pairs
    // MinInitialHp/MaxInitialHp are usually
    // GetValueIfAscension(ToughEnemies, high, low); these are the low branch, which the
    // game rolls below A8. They equal MinHp/MaxHp for a monster whose HP is a plain int.
    int MinHpBelowToughEnemies = 0,
    int MaxHpBelowToughEnemies = 0
)
{
    /// <summary>The HP band this monster rolls in at <paramref name="ascension"/>.</summary>
    public (int Min, int Max) HpBand(int ascension) =>
        Ascension.Has(ascension, Ascension.ToughEnemies) || MinHpBelowToughEnemies == 0
            ? (MinHp, MaxHp)
            : (MinHpBelowToughEnemies, MaxHpBelowToughEnemies);
}

/// <summary>
/// A monster's declared move. <paramref name="Magnitude"/> is damage PER HIT for an
/// attack, and <paramref name="Hits"/> is how many land — the game's MultiAttackIntent
/// keeps them apart the same way, and derives both its label and its damage from them.
/// Pre-multiplying into a total loses the distinction twice over: the display cannot add
/// Strength per hit, and the execution cannot let block absorb per hit.
/// </summary>
public readonly record struct Intent(
    IntentType Type,
    int Magnitude,
    int Hits = 1,
    // Some moves attack AND do something else, and the live readout does not always call
    // them attacks: Sludge Spinner's OIL_SPRAY reports as a Debuff whose number is still
    // damage, while Vine Shambler's GRASPING_VINES reports as an Attack. Either way the
    // number is damage and grows with Strength, which the Type alone cannot say.
    bool CarriesDamage = false
)
{
    /// <summary>
    /// What the game shows, which AttackIntent.GetTotalDamage builds from the modified
    /// per-hit damage — so a two-hit attack from a +2 Strength monster reads four higher,
    /// not two.
    /// </summary>
    public int AnnouncedDamage(
        List<BuffState> attackerBuffs,
        List<BuffState> defenderBuffs,
        float weakDelta = 0f
    ) =>
        Type == IntentType.Attack || CarriesDamage
            ? BuffSystem.IncomingDamage(
                Magnitude,
                attackerBuffs,
                defenderBuffs,
                weakDelta: weakDelta
            ) * Hits
            : Magnitude;
}

public sealed class EnemyState
{
    public int DefId;
    public int Hp;
    public int MaxHp;
    public int Block;
    public Intent CurrentIntent;
    public Intent? SecondaryIntent;
    public List<BuffState> Buffs = [];

    /// <summary>
    /// `DamageReceivedEntry`s against this enemy from the player, this turn, from POWERED
    /// attacks. Beat Into Shape counts them to size its Forge, and it counts damage
    /// INSTANCES rather than cards -- a multi-hit attack raises it once per hit.
    /// </summary>
    public int PoweredHitsThisTurn;

    public int MoveIndex;
    public int LastMove = -1; // ID of the last move chosen (to avoid repetition)

    /// <summary>
    /// How many times running the last move has been chosen. A RandomBranchState branch
    /// added with maxRepeats stops being eligible once it has come up that many times in
    /// a row (Fossil Stalker's three moves each cap at two).
    /// </summary>
    public int LastMoveRepeats;

    /// <summary>
    /// A UseOnlyOnce branch, spent for the rest of the combat once taken — Mawler's ROAR
    /// and a Two-Tailed Rat's CALL_FOR_BACKUP. Per creature, not per encounter: three rats
    /// have three of these between them.
    /// </summary>
    public bool OnceOnlyMoveUsed;

    /// <summary>
    /// Moves taken this combat, most recent last. A RandomBranchState branch added with a
    /// cooldown is ineligible while it appears in the last N moves — Flyconid's spores are
    /// on cooldowns of 3 and 2 — so a single LastMove cannot answer the question.
    /// </summary>
    public List<int> MoveHistory = [];

    /// <summary>
    /// True when the machine's initialState is a RandomBranchState rather than a move, so
    /// the very first selection rolls. A summoned Two-Tailed Rat starts this way
    /// (StarterMoveIndex == -1) and one that began the fight does not.
    /// </summary>
    public bool StartsOnBranch;

    /// <summary>
    /// The machine has just been forced through a state whose FollowUpState is a
    /// RandomBranchState, so the NEXT selection is a roll rather than the next step of a
    /// cycle. A reattached Decimillipede segment is the case: DEAD_MOVE -> REATTACH_MOVE
    /// -> RAND, so a segment that comes back does not resume where it fell.
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="StartsOnBranch"/>, which is about the machine's INITIAL
    /// state and is answered once at creation. Conflating the two would make a summoned
    /// creature and a revived one the same thing, and they are not.
    /// </remarks>
    public bool RollsNextMove;

    /// <summary>
    /// <c>NemesisPower</c>'s private flip bool: false, then true, then false, once per
    /// enemy side turn. True means the power applied Intangible on this flip.
    /// </summary>
    /// <remarks>
    /// Kept as its own state rather than read back off the Intangible stack, which is the
    /// obvious shortcut and is wrong: Intangible decrements itself at the same moment, so
    /// by the time Nemesis looks the stack is already gone and "is it on?" answers no
    /// every round. The power alternates because the BOOL alternates.
    /// </remarks>
    public bool NemesisIntangibleOn;

    /// <summary>
    /// The game's <c>StarterMoveIdx</c>: which move this creature's machine opens on.
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="MoveIndex"/> because the two mean different things and
    /// conflating them has bitten twice. The starter is an index into the monster's own
    /// numbering of its moves, which need not match the order the machine walks them —
    /// the Decimillipede's is 0/1/2 = WRITHE/BULK/CONSTRICT against a cycle of
    /// WRITHE -> CONSTRICT -> BULK (E95), and the Scroll of Biting's is CHOMP/CHEW/
    /// MORE_TEETH against a chain of CHOMP -> MORE_TEETH -> CHEW.
    /// </remarks>
    public int StarterMove;
    public int StolenGold;
    public int HeistGold;

    /// <summary>This creature left the fight rather than dying; see CombatState.FatGremlinEscaped.</summary>
    public bool Escaped;

    /// <summary>
    /// A Bowlbug Rock whose headbutt was fully blocked, and which owes a turn for it.
    /// </summary>
    /// <remarks>
    /// <c>ImbalancedPower.AfterDamageGiven</c> fires on `result.WasFullyBlocked` and sets
    /// the Rock's `IsOffBalance`; the same HEADBUTT_MOVE then stuns it, so the next turn
    /// is DIZZY_MOVE, which clears the flag. Only the Bowlbug Rock carries the power —
    /// on anything else it would stun outright — so this is a field rather than a buff.
    ///
    /// Without it the Rock alternated headbutt and dizzy unconditionally, which is half
    /// its damage against a player who never fully blocks, and announces a stun that is
    /// not coming.
    /// </remarks>
    public bool OffBalance;

    /// <summary>
    /// Which arm of a <c>RandomBranchState</c> this creature last took, and how many
    /// turns running it has taken it.
    /// </summary>
    /// <remarks>
    /// <c>RandomBranchState</c> zeroes a branch's weight once the recent state log shows
    /// it too many times: <c>CannotRepeat</c> is a cap of one, <c>CanRepeatXTimes(n)</c>
    /// a cap of n. The obvious implementation — compare the new intent with
    /// <c>CurrentIntent</c> — is WRONG for anything that buffs itself: the Obscura's WAIL
    /// grants it Strength, so what it announces climbs and a base-damage branch never
    /// equals the stored intent again. The branch's identity has to be remembered, not
    /// inferred from what it announced.
    /// </remarks>
    public int LastBranch = -1;

    /// <summary>Turns running on <see cref="LastBranch"/>.</summary>
    public int RepeatStreak;

    /// <summary>
    /// Moves left before a branch on a COOLDOWN can be taken again.
    /// </summary>
    /// <remarks>
    /// <c>RandomBranchState</c> gives a branch weight zero while it appears in the last
    /// <c>cooldown</c> logged MOVES — a different rule from the repeat cap, and one that
    /// outlasts it. The Fake Merchant's ENRAGE is the only branch that uses it, at three.
    /// </remarks>
    public int BranchCooldown;

    /// <summary>
    /// Which of the encounter's <c>Slots</c> this creature stands in, or -1 when the
    /// encounter does not place by slot.
    /// </summary>
    /// <remarks>
    /// The game's summons ask the ENCOUNTER for a slot rather than the roster for a
    /// position: a Two-Tailed Rat's <c>CallForBackup</c> takes
    /// <c>Slots.LastOrDefault(s => no living creature holds s)</c>. Which end of the
    /// roster that lands on depends on which rats are still alive, so "the newcomer goes
    /// to the front" is only the answer while the three starters are untouched.
    /// </remarks>
    public int Slot = -1;
}
