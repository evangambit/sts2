using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Sts2Emulator.GeneratedData;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Builds the one-card combat that almost every card test wants: a fresh combat, a
/// known hand, enough energy, and a punching bag to hit. Card tests are worth reading
/// for their expected values, and those used to be buried under fifteen lines of
/// identical setup.
///
/// The builder deliberately stops at "common setup" — <see cref="State"/> is public,
/// so a card needing something unusual (an intent, a relic, a second turn) reaches in
/// and sets it rather than growing a method here that one card calls.
/// </summary>
internal sealed class Fight
{
    /// <summary>Chomper: no moves, plenty of HP, already the de facto dummy in the suite.</summary>
    private const int DummyEnemyDefId = 16;

    private Random _rng = new(0);
    private bool _replacedEncounter;

    private Fight(CombatState state) => State = state;

    public CombatState State { get; }

    public EnemyState Enemy0 => State.Enemies[0];

    public EnemyState Enemy1 => State.Enemies[1];

    /// <summary>
    /// A fresh highest-difficulty combat (seed 0) holding exactly <paramref name="hand"/>.
    /// The draw pile is the shuffled starter deck unless <see cref="Draw"/> replaces it,
    /// and the enemies are the generated encounter unless <see cref="Enemy"/> replaces them.
    /// </summary>
    public static Fight Hand(params CardInstance[] hand)
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [.. hand];
        // Real entry points set this from the run's combat_targets stream. Setting it
        // here too means a card test exercises the same target-picking path they do,
        // rather than the bare fallback.
        state.TargetRng = new CountingRandom(0);
        state.CardSelectionRng = new CountingRandom(0);
        state.CardGenerationRng = new CountingRandom(0);
        state.PotionGenerationRng = new CountingRandom(0);
        state.OrbGenerationRng = new CountingRandom(0);
        return new Fight(state);
    }

    /// <summary>
    /// A fresh combat that already holds these relics. Relics fire during setup — Anchor
    /// grants block before the first turn, Bag of Marbles debuffs as it starts — so they
    /// have to be present when the combat is built rather than added afterwards.
    /// </summary>
    public static Fight WithRelics(params int[] relicIds) => Encounter(1, relicIds);

    /// <summary>
    /// A combat against a named encounter, for tests about the encounter itself rather
    /// than about a card. Ascension is an input the roster and its damage read, so it is
    /// explicit here: the suite's default is the highest difficulty.
    /// </summary>
    public static Fight Encounter(
        CombatFactory.ActOneEncounter encounter,
        int ascension = Ascension.DefaultLevel,
        int seed = 0,
        params int[] relicIds
    )
    {
        var state = new CombatState
        {
            AscensionLevel = ascension,
            // The stream the game rolls monster HP on. Real entry points set it, and it
            // is what makes SetUniqueMonsterHpValue's "no two creatures on a side share
            // an HP" rule apply — without it a test roster rolls flat and duplicates.
            NicheHpRng = new CountingRandom(seed),
        };
        CombatFactory.Reset(
            state,
            new Random(seed),
            TestDeck.StarterDeckIds,
            (int)encounter,
            relicIds
        );
        state.TargetRng = new CountingRandom(seed);
        state.CardSelectionRng = new CountingRandom(seed);
        state.CardGenerationRng = new CountingRandom(seed);
        state.PotionGenerationRng = new CountingRandom(seed);
        state.OrbGenerationRng = new CountingRandom(seed);
        return new Fight(state);
    }

    /// <summary>The enemies' def ids in position order — the roster, as a list to assert on.</summary>
    public IEnumerable<int> EnemyDefIds => State.Enemies.Select(enemy => enemy.DefId);

    /// <summary>
    /// Each enemy's intent as the game announces it: type, and the damage the player would
    /// read — per-hit damage times hits, with the attacker's Strength already in it.
    /// </summary>
    public IEnumerable<(IntentType Type, int Magnitude)> Intents =>
        State.Enemies.Select(enemy =>
            (
                enemy.CurrentIntent.Type,
                enemy.CurrentIntent.AnnouncedDamage(enemy.Buffs, State.PlayerBuffs)
            )
        );

    /// <summary>Ends the turn without playing anything, the way an encounter test watches a fight.</summary>
    public Fight Turns(int count)
    {
        for (int i = 0; i < count; i++)
        {
            EndTurn();
        }

        return this;
    }

    /// <summary>
    /// The same, against a chosen encounter. Encounter 1's two enemies both hold Artifact,
    /// which swallows a debuff whole — a test about applying one has to pick an encounter
    /// that can actually receive it (3 is three enemies, none protected).
    /// </summary>
    /// <summary>
    /// A fight built with an explicit ENCOUNTER stream seed, for the encounters that roll
    /// their own composition. Varying it and watching the roster is the only check that
    /// tells a builder reading the encounter stream from one quietly reading the combat
    /// rng, which is what E90 turned on.
    /// </summary>
    public static Fight EncounterWithStream(int encounterId, int encounterRngSeed)
    {
        var state = new CombatState { NicheHpRng = new CountingRandom(0) };
        CombatFactory.Reset(
            state,
            new Random(0),
            TestDeck.StarterDeckIds,
            encounterId,
            completedCombatRoomsBeforeCurrent: -1,
            encounterRngSeed: encounterRngSeed
        );
        state.TargetRng = new CountingRandom(0);
        state.CardSelectionRng = new CountingRandom(0);
        state.CardGenerationRng = new CountingRandom(0);
        state.PotionGenerationRng = new CountingRandom(0);
        state.OrbGenerationRng = new CountingRandom(0);
        return new Fight(state);
    }

    public static Fight Encounter(int encounterId, params int[] relicIds)
    {
        var state = new CombatState { NicheHpRng = new CountingRandom(0) };
        CombatFactory.Reset(state, new Random(0), TestDeck.StarterDeckIds, encounterId, relicIds);
        state.TargetRng = new CountingRandom(0);
        state.CardSelectionRng = new CountingRandom(0);
        state.CardGenerationRng = new CountingRandom(0);
        state.PotionGenerationRng = new CountingRandom(0);
        state.OrbGenerationRng = new CountingRandom(0);
        return new Fight(state);
    }

    /// <summary>
    /// Reseeds the RNG that every action on this fight shares. One RNG per fight rather
    /// than one per step, so a multi-step test draws the same sequence the engine would.
    /// </summary>
    public Fight Seed(int seed)
    {
        _rng = new Random(seed);
        State.TargetRng = new CountingRandom(seed);
        State.CardSelectionRng = new CountingRandom(seed);
        State.CardGenerationRng = new CountingRandom(seed);
        State.PotionGenerationRng = new CountingRandom(seed);
        return this;
    }

    public Fight Energy(int amount)
    {
        State.Energy = amount;
        return this;
    }

    public Fight Draw(params CardInstance[] cards)
    {
        State.DrawPile = [.. cards];
        return this;
    }

    public Fight Discard(params CardInstance[] cards)
    {
        State.DiscardPile = [.. cards];
        return this;
    }

    public Fight Exhausted(params CardInstance[] cards)
    {
        State.ExhaustPile = [.. cards];
        return this;
    }

    public Fight PlayerHp(int hp, int? maxHp = null)
    {
        State.PlayerHp = hp;
        State.PlayerMaxHp = maxHp ?? State.PlayerMaxHp;
        return this;
    }

    public Fight PlayerBuff(BuffId id, int magnitude)
    {
        State.PlayerBuffs.Add(new BuffState(id, magnitude));
        return this;
    }

    /// <summary>
    /// Adds a dummy enemy. The first call drops the generated encounter, so a test that
    /// wants two enemies calls this twice and one that wants the real encounter never
    /// calls it at all.
    /// </summary>
    public Fight Enemy(
        int hp = 100,
        int block = 0,
        int defId = DummyEnemyDefId,
        int? maxHp = null,
        params BuffState[] buffs
    )
    {
        if (!_replacedEncounter)
        {
            State.Enemies = [];
            _replacedEncounter = true;
        }

        State.Enemies.Add(
            new EnemyState
            {
                DefId = defId,
                Hp = hp,
                MaxHp = maxHp ?? hp,
                Block = block,
                Buffs = [.. buffs],
            }
        );
        return this;
    }

    /// <summary>Plays the card at <paramref name="index"/>; -1 targets the first living enemy.</summary>
    public StepResult Play(int index = 0, int target = -1) =>
        CombatEngine.Step(State, index, _rng, target);

    /// <summary>
    /// Answers an open card-selection screen with the candidate at
    /// <paramref name="candidate" />. A test makes this choice explicitly — that is the
    /// point of modelling it — so there is no "pick something sensible" helper.
    /// </summary>
    public StepResult Choose(int candidate) => CombatEngine.Step(State, candidate, _rng);

    public PendingCardSelection? Pending => State.PendingSelection;

    public StepResult EndTurn() => CombatEngine.Step(State, State.Hand.Count, _rng);

    public StepResult Potion(int slot) =>
        CombatEngine.Step(State, State.Hand.Count + 1 + slot, _rng);

    public int PlayerBuffAmount(BuffId id) => BuffSystem.Get(State.PlayerBuffs, id);

    /// <summary>
    /// Asserts the player carries exactly these POWERS and no others.
    /// </summary>
    /// <remarks>
    /// A capture asserts the powers the game reported, which says nothing about the ones it
    /// did NOT report — so an emulator that invents a power passes. That is not a
    /// hypothetical: Venerate gains STARS, the emulator granted Strength and Dexterity, and
    /// a live capture of it passed clean because the game's empty status list generated no
    /// assertions at all.
    ///
    /// "Power" means a <c>BuffId</c> with a matching <c>&lt;Name&gt;Power</c> in the
    /// generated power data. The twenty or so ids without one are the emulator's own
    /// counters — ShivDamage, OutbreakCounter, NextTurnEnergy — which the game does not
    /// show and which must not be asserted against a readout that could never contain them.
    /// </remarks>
    public void PlayerPowersAre(params BuffId[] expected)
    {
        var unexpected = State
            .PlayerBuffs.Where(buff => buff.Magnitude != 0)
            .Select(buff => buff.Id)
            .Where(id => GeneratedData.Powers.FindId($"{id}Power") is not null)
            .Where(id => !expected.Contains(id))
            .ToList();

        Assert.True(
            unexpected.Count == 0,
            $"the game reported no such power, and the emulator has: {string.Join(", ", unexpected)}"
        );
    }

    public int EnemyBuffAmount(BuffId id, int index = 0) =>
        BuffSystem.Get(State.Enemies[index].Buffs, id);

    /// <summary>The def IDs in a pile, for asserting on order rather than membership.</summary>
    public static IEnumerable<int> Ids(IEnumerable<CardInstance> cards) =>
        cards.Select(card => card.DefId);
}

/// <summary>Card literals for test setup, kept short because tests are mostly card lists.</summary>
internal static class TestDeck
{
    /// <summary>The highest-difficulty starter deck, as CombatFactory deals it.</summary>
    public static ReadOnlySpan<int> StarterDeckIds =>
        [
            IC.StrikeIronclad,
            IC.StrikeIronclad,
            IC.StrikeIronclad,
            IC.StrikeIronclad,
            IC.StrikeIronclad,
            IC.DefendIronclad,
            IC.DefendIronclad,
            IC.DefendIronclad,
            IC.DefendIronclad,
            IC.Bash,
            IC.AscendersBane,
        ];

    public static CardInstance Card(int defId, bool upgraded = false) => new(defId, upgraded);

    public static List<CardInstance> Pile(params int[] defIds) =>
        defIds.Select(id => new CardInstance(id, false)).ToList();
}
