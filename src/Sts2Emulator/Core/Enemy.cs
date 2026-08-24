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
    public int AnnouncedDamage(List<BuffState> attackerBuffs, List<BuffState> defenderBuffs) =>
        Type == IntentType.Attack || CarriesDamage
            ? BuffSystem.IncomingDamage(Magnitude, attackerBuffs, defenderBuffs) * Hits
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
    public int StolenGold;
    public int HeistGold;

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
