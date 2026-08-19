namespace Sts2Emulator.Core;

public sealed class CombatState
{
    /// <summary>
    /// The run's ascension level, which is an INPUT to enemy data rather than a
    /// difficulty label: the game picks monster damage with
    /// <c>GetValueIfAscension(level, high, low)</c>, so A8 and A10 are different
    /// numbers for the same enemy. Kept on the state so captures at different levels
    /// can be compared in one process — see Core/Ascension.cs.
    /// </summary>
    public int AscensionLevel = Ascension.DefaultLevel;

    // Player
    public int PlayerHp;
    public int PlayerMaxHp;
    public int PlayerBlock;
    public int Energy;
    public int MaxEnergy;
    public int PlayerGold;

    // Cards
    public List<CardInstance> Hand = [];
    public List<CardInstance> DrawPile = [];
    public List<CardInstance> DiscardPile = [];
    public List<CardInstance> ExhaustPile = [];
    public List<CardInstance> ReturnToHandBeforeDraw = [];
    public List<CardInstance> AutoPlayQueue = [];

    // Defect-style orb queue.
    public List<OrbState> Orbs = [];
    public int OrbCapacity = 3;

    // Necrobinder pet state.
    public int OstyHp;
    public int OstyMaxHp;

    // Regent star resource.
    public int Stars;

    // Potions: slot index → potion def ID, 0 = empty
    public int[] PotionSlots = new int[3];
    public int MaxPotionSlots = 3;

    // Relics
    public List<RelicInstance> Relics = [];

    // Buffs/debuffs on the player
    public List<BuffState> PlayerBuffs = [];

    // Enemies
    public List<EnemyState> Enemies = [];
    public int EncounterId;
    public bool IsEliteCombat;

    // Shuffle RNG (RunRngSet.shuffle subsystem) — used for mid-combat discard reshuffles.
    // Null falls back to the combat RNG (only valid when no pre-shuffle was done).
    // CountingRandom tracks total Next() calls so RunEngine can sync its shuffle RNG.
    public CountingRandom? ShuffleRng;

    // Target RNG (RunRngSet.combat_targets subsystem) — used whenever an effect picks
    // which enemy to hit (Juggernaut, Volley, Sword Boomerang). Null falls back to the
    // combat RNG (only valid in single-combat tests).
    public CountingRandom? TargetRng;

    // Card-selection RNG (RunRngSet.combat_card_selection subsystem) — used whenever an
    // effect picks WHICH existing card to exhaust, transform or otherwise act on (Cinder,
    // Thrash, unupgraded True Grit, Entropy). Distinct from combat_card_generation, which
    // rolls up a NEW card (Infernal Blade, Splash, Stoke). Null falls back to the combat
    // RNG (only valid in single-combat tests).
    public CountingRandom? CardSelectionRng;

    // Card-generation RNG (RunRngSet.combat_card_generation subsystem) — used when an
    // effect rolls up a NEW card (Stoke, Splash, Infernal Blade, Discovery). Distinct
    // from combat_card_selection, which picks among cards that already exist. Null falls
    // back to the combat RNG (only valid in single-combat tests).
    public CountingRandom? CardGenerationRng;

    // Potion-generation RNG (RunRngSet.combat_potion_generation subsystem) — used when a
    // card rolls up a potion in combat (Alchemize). Null falls back to the combat RNG
    // (only valid in single-combat tests).
    public CountingRandom? PotionGenerationRng;

    // AI RNG (RunRngSet.monster_ai subsystem) — used for enemy intent selection.
    // Null falls back to the combat RNG (used in single-combat tests).
    public Random? AiRng;

    // Niche HP RNG — used ONLY for SetUniqueMonsterHpValue (CreateEnemy HP calls).
    // When non-null, CreateEnemy uses this instead of the main combat RNG for HP.
    // CountingRandom.CallCount tracks how many HP values were drawn (= enemy count).
    public CountingRandom? NicheHpRng;

    // A card-selection screen the play is waiting on. Non-null blocks every other
    // action until it is answered — see PendingCardSelection.
    public PendingCardSelection? PendingSelection;

    // True while the engine is playing a card the player did not choose to play
    // (Havoc, Hellraiser, Stampede, Mayhem). The game still prompts for choices there,
    // but the emulator has no way to hand an auto-play back to the caller mid-queue, so
    // those resolve with the old automatic pick.
    public bool AutoPlaying;

    // Damage to add to the copy currently being played, before it lands in a pile. Set by
    // CardEffects during Apply and consumed by CombatEngine.PlayCard, because Apply takes
    // the CardInstance by value and cannot hand a mutation back any other way.
    public int PlayedCardBonusDamage;

    // Turn tracking
    public int Turn;
    public bool PlayerTurn = true;
    public bool SkillPlayedWhileSmoggy;
    public int AttackCardsPlayedThisTurn;
    public int AttackOrSkillCardsPlayedThisTurn;
    public int CardPlaysThisTurn;
    public int CardsPlayedThisCombat;
    public int DrawnCardsSinceAutomationProc;
    public int CardsPlayedSincePanacheProc;
    public int BlockGainsThisTurn;
    public int PlayerHpLostThisTurn;
    public int CardsExhaustedThisTurn;
    public int LightningOrbsChanneledThisCombat;
    public int EtherealExhaustCount; // number of cards exhausted by Ethereal this turn (Dark Embrace)
    public int UnblockedDamageHitCount; // times player took unblocked damage this combat (TearAsunder)
    public int TargetEnemyIndex = -1; // -1 = auto (first living enemy), >=0 = specific index
}
