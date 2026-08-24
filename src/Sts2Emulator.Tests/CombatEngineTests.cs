using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Sts2Emulator.Interop;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

public class CombatEngineTests
{
    [Fact]
    public void NewCombat_StartsAtHighestDifficultyHp()
    {
        var state = CombatFactory.NewCombat(seed: 0);

        Assert.Equal(64, state.PlayerHp);
        Assert.Equal(80, state.PlayerMaxHp);
    }

    [Fact]
    public void NewCombat_StartsWithHighestDifficultyStarterDeck()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var cards = state
            .Hand.Concat(state.DrawPile)
            .Concat(state.DiscardPile)
            .Concat(state.ExhaustPile)
            .ToList();

        Assert.Equal(11, cards.Count);
        Assert.Equal(5, cards.Count(c => c.DefId == IC.StrikeIronclad));
        Assert.Equal(4, cards.Count(c => c.DefId == IC.DefendIronclad));
        Assert.Equal(1, cards.Count(c => c.DefId == IC.Bash));
        Assert.Equal(1, cards.Count(c => c.DefId == IC.AscendersBane));
    }

    // ── card selection ────────────────────────────────────────────────────────
    // A pending selection owns the action space until it is answered.

    [Fact]
    public void CardSelection_OffersOnlyItsCandidatesAsValidActions()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Headbutt, false)];
        state.DiscardPile =
        [
            new CardInstance(IC.Bash, false),
            new CardInstance(IC.StrikeIronclad, false),
        ];
        state.Energy = 3;
        state.PotionSlots[0] = 1;

        CombatEngine.Step(state, 0, new Random(0));

        // Not end turn, not the potion, not a card: just the two candidates.
        Assert.Equal(new[] { 0, 1 }, CombatEngine.ValidActions(state).ToList());
    }

    [Fact]
    public void CardSelection_RejectsAnOutOfRangeAnswer()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Headbutt, false)];
        state.DiscardPile = [new CardInstance(IC.Bash, false)];
        state.Energy = 3;
        CombatEngine.Step(state, 0, new Random(0));

        var result = CombatEngine.Step(state, 5, new Random(0));

        Assert.False(result.Terminal);
        Assert.NotNull(state.PendingSelection);
    }

    [Fact]
    public void CardSelection_BlocksEndingTheTurnUntilItIsAnswered()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Headbutt, false)];
        state.DiscardPile = [new CardInstance(IC.Bash, false)];
        state.Energy = 3;
        CombatEngine.Step(state, 0, new Random(0));
        int turnBefore = state.Turn;

        // The end-turn action for an empty hand is 0, which is also candidate 0 — so the
        // selection answers instead, which is exactly the point.
        CombatEngine.Step(state, state.Hand.Count, new Random(0));

        Assert.Equal(turnBefore, state.Turn);
        Assert.Null(state.PendingSelection);
    }

    [Fact]
    public void CardSelection_IsNotRaisedByAnAutoPlayedCard()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Havoc, false)];
        state.DrawPile = [new CardInstance(IC.Headbutt, false)];
        state.DiscardPile =
        [
            new CardInstance(IC.Bash, false),
            new CardInstance(IC.StrikeIronclad, false),
        ];
        state.Energy = 3;

        CombatEngine.Step(state, 0, new Random(0));

        // Havoc drains its play inline, so the choice resolves itself rather than
        // stranding the queue: the most recent discard goes on top of the draw pile.
        Assert.Null(state.PendingSelection);
        Assert.Equal(IC.StrikeIronclad, state.DrawPile[0].DefId);
    }

    [Fact]
    public void CardSelection_IsClearedByAReset()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Headbutt, false)];
        state.DiscardPile = [new CardInstance(IC.Bash, false)];
        state.Energy = 3;
        CombatEngine.Step(state, 0, new Random(0));

        CombatFactory.Reset(state, seed: 0);

        Assert.Null(state.PendingSelection);
    }

    // ── turn-1 draw-pile reorder ──────────────────────────────────────────────
    // Ports MegaCrit.Sts2.Core.Combat/CombatManager.cs ~line 658.

    [Fact]
    public void TurnOneReorder_LeavesPileAloneWhenNoCardIsInnate()
    {
        var pile = Pile(IC.StrikeIronclad, IC.DefendIronclad, IC.Bash);

        int draw = CombatFactory.ApplyTurnOneDrawPileReorder(pile, 5);

        Assert.Equal(5, draw);
        Assert.Equal([IC.StrikeIronclad, IC.DefendIronclad, IC.Bash], pile.Select(c => c.DefId));
    }

    [Fact]
    public void TurnOneReorder_MovesInnateCardToTop()
    {
        var pile = Pile(IC.StrikeIronclad, IC.DefendIronclad, CL.MindBlast, IC.Bash);

        int draw = CombatFactory.ApplyTurnOneDrawPileReorder(pile, 5);

        Assert.Equal(5, draw); // one innate card < the base draw of 5
        Assert.Equal(
            [CL.MindBlast, IC.StrikeIronclad, IC.DefendIronclad, IC.Bash],
            pile.Select(c => c.DefId)
        );
    }

    [Fact]
    public void TurnOneReorder_ReversesMultipleInnateCards()
    {
        // The game moves each innate card with MoveToTopInternal (Insert at 0), so
        // walking them in pile order leaves the innate block reversed.
        var pile = Pile(CL.MindBlast, IC.StrikeIronclad, SI.Backstab, SI.Suppress);

        CombatFactory.ApplyTurnOneDrawPileReorder(pile, 5);

        Assert.Equal(
            [SI.Suppress, SI.Backstab, CL.MindBlast, IC.StrikeIronclad],
            pile.Select(c => c.DefId)
        );
    }

    [Fact]
    public void TurnOneReorder_IgnoresUpgradeOnlyInnateWhenNotUpgraded()
    {
        // Aggression gains Innate from OnUpgrade, so unupgraded it must not move.
        var pile = new List<CardInstance>
        {
            new(IC.StrikeIronclad, false),
            new(IC.Aggression, false),
        };

        CombatFactory.ApplyTurnOneDrawPileReorder(pile, 5);

        Assert.Equal([IC.StrikeIronclad, IC.Aggression], pile.Select(c => c.DefId));
    }

    [Fact]
    public void TurnOneReorder_HonorsUpgradeOnlyInnateWhenUpgraded()
    {
        var pile = new List<CardInstance>
        {
            new(IC.StrikeIronclad, false),
            new(IC.Aggression, true),
        };

        CombatFactory.ApplyTurnOneDrawPileReorder(pile, 5);

        Assert.Equal([IC.Aggression, IC.StrikeIronclad], pile.Select(c => c.DefId));
    }

    [Fact]
    public void TurnOneReorder_RaisesDrawCountToCoverEveryInnateCard()
    {
        // Duplicates deliberately: identical CardInstances are value-equal, so this
        // also pins that every copy counts (the game's Except/Distinct on reference
        // types must not become a value-based dedup here).
        var pile = Pile(
            CL.MindBlast,
            CL.MindBlast,
            SI.Backstab,
            SI.Backstab,
            SI.Suppress,
            CL.DramaticEntrance,
            IC.StrikeIronclad
        );

        int draw = CombatFactory.ApplyTurnOneDrawPileReorder(pile, 5);

        Assert.Equal(6, draw); // 6 innate cards > the base draw of 5
        Assert.Equal(IC.StrikeIronclad, pile[^1].DefId); // sole non-innate sinks last
    }

    [Fact]
    public void TurnOneReorder_CapsDrawCountAtMaxHandSize()
    {
        // 12 innate cards would otherwise ask for a 12-card opening hand.
        var pile = Enumerable.Repeat(new CardInstance(CL.MindBlast, false), 12).ToList();

        int draw = CombatFactory.ApplyTurnOneDrawPileReorder(pile, 5);

        Assert.Equal(10, draw);
    }

    [Fact]
    public void NewCombat_DealsOpeningHandFromReorderedPile()
    {
        // End-to-end: the starter deck has no innate cards, so the opening hand is
        // still the top 5 of the shuffled pile.
        var state = CombatFactory.NewCombat(seed: 0);

        Assert.Equal(5, state.Hand.Count);
        Assert.DoesNotContain(state.Hand, c => c.IsInnate());
    }

    [Fact]
    public void NewCombat_IsDeterministicForSameSeed()
    {
        var first = CombatFactory.NewCombat(seed: 123);
        var second = CombatFactory.NewCombat(seed: 123);

        Assert.Equal(first.Enemies[0].Hp, second.Enemies[0].Hp);
        Assert.Equal(first.Hand.Select(c => c.DefId), second.Hand.Select(c => c.DefId));
        Assert.Equal(first.DrawPile.Select(c => c.DefId), second.DrawPile.Select(c => c.DefId));
    }

    [Fact]
    public void Reset_RestoresHighestDifficultyStartingState()
    {
        var state = CombatFactory.NewCombat(seed: 123);
        state.PlayerHp = 1;
        state.Energy = 0;
        state.Hand.Clear();

        CombatFactory.Reset(state, seed: 123);

        Assert.Equal(64, state.PlayerHp);
        Assert.Equal(80, state.PlayerMaxHp);
        Assert.Equal(3, state.Energy);
        Assert.Equal(3, state.MaxEnergy);
        Assert.Equal(5, state.Hand.Count);
        Assert.Equal(6, state.DrawPile.Count);
    }

    [Fact]
    public void DrawCards_DrawsFromTopOfDrawPile()
    {
        var state = new CombatState
        {
            DrawPile =
            [
                new CardInstance(IC.StrikeIronclad, false),
                new CardInstance(IC.Bash, false),
                new CardInstance(IC.DefendIronclad, false),
            ],
        };

        CardEffects.DrawCards(state, 2, new Random(0));

        Assert.Equal([IC.StrikeIronclad, IC.Bash], state.Hand.Select(card => card.DefId));
        Assert.Equal([IC.DefendIronclad], state.DrawPile.Select(card => card.DefId));
    }

    [Fact]
    public void ResetWithDeck_NegativeCardIdsEncodeUpgradedCards()
    {
        var state = new CombatState();

        CombatFactory.Reset(state, new Random(0), [-IC.Bash], encounterId: 1);

        Assert.Single(state.Hand.Concat(state.DrawPile));
        Assert.All(
            state.Hand.Concat(state.DrawPile),
            card =>
            {
                Assert.Equal(IC.Bash, card.DefId);
                Assert.True(card.Upgraded);
            }
        );
    }

    [Fact]
    public void CardsExhaustedThisTurn_ResetsAtStartOfNextPlayerTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.ForgottenRitual, false)];
        state.DrawPile.Clear();
        state.Energy = 1;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 100,
                MaxHp = 100,
                CurrentIntent = new Intent(IntentType.Defend, 0),
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(0, state.CardsExhaustedThisTurn);
    }

    [Fact]
    public void Weakness_FromSludgeSpinner_LastsThroughNextPlayerTurn()
    {
        // SludgeSpinner move 0 is OIL_SPRAY: an ATTACK that also applies 1 Weak.
        // OIL_SPRAY_MOVE declares SingleAttackIntent first and DebuffIntent second, and
        // a live capture reads Attack '8' then Debuff -- it used to be modelled as a
        // debuff carrying damage. The spinner picks its move at random, so which move
        // this is lives in LastMove, not in MoveIndex.
        var state = CombatFactory.NewCombat(seed: 0);
        state.Enemies =
        [
            CombatFactory.CreateEnemy(
                KE.SludgeSpinner,
                new Random(0),
                new Intent(IntentType.Attack, 9),
                0
            ),
        ];
        state.Enemies[0].LastMove = 0;
        state.Hand = [new CardInstance(IC.StrikeIronclad, false)];
        state.DrawPile = [];
        state.DiscardPile = [];
        var enemy = state.Enemies[0];
        enemy.Hp = 100;

        // Turn 1: End Turn.
        // SludgeSpinner should use Oil Spray, dealing 9 damage and applying 1 Weak.
        CombatEngine.Step(state, 1, new Random(0));

        // Turn 2 start. Player should be Weak 1.
        // If the bug exists, Weak 1 was ticked to 0 at the end of Turn 1 (start of Turn 2).
        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.Weak));

        // Play Strike. Should deal 4 damage.
        CombatEngine.Step(state, 0, new Random(0));
        Assert.Equal(96, enemy.Hp);
    }

    [Fact]
    public void PlayedPower_DoesNotTriggerFeelNoPainExhaustHooks()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.FeelNoPain, false), new CardInstance(IC.Inflame, false)];
        state.Energy = 2;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 100,
                MaxHp = 100,
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(0, state.PlayerBlock);
        Assert.Empty(state.ExhaustPile);
        Assert.Equal(3, BuffSystem.Get(state.PlayerBuffs, BuffId.FeelNoPain));
        Assert.Equal(2, BuffSystem.Get(state.PlayerBuffs, BuffId.Strength));
    }

    [Fact]
    public void ResetWithRunHp_PreservesCurrentRunHp()
    {
        var state = new CombatState();

        CombatFactory.Reset(
            state,
            new Random(0),
            StarterDeckIds,
            encounterId: 1,
            relicIds: [],
            playerHp: 37,
            playerMaxHp: 80
        );

        Assert.Equal(37, state.PlayerHp);
        Assert.Equal(80, state.PlayerMaxHp);
    }

    [Fact]
    public void ResetWithPotions_PreservesRunPotionSlots()
    {
        var state = new CombatState();

        CombatFactory.Reset(
            state,
            new Random(0),
            StarterDeckIds,
            encounterId: 1,
            relicIds: [],
            playerHp: 37,
            playerMaxHp: 80,
            potionIds: [1, 0, 2]
        );

        Assert.Equal(new[] { 1, 0, 2 }, state.PotionSlots);
    }

    [Fact]
    public void PlayerThorns_RetaliatesAgainstEnemyAttacks()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var enemy = new EnemyState
        {
            DefId = 16,
            Hp = 20,
            MaxHp = 20,
            CurrentIntent = new Intent(IntentType.Attack, 1),
            Buffs = [],
        };
        BuffSystem.Apply(state.PlayerBuffs, BuffId.Thorns, 3);

        EnemyAI.ExecuteIntent(enemy, state, new Random(0));

        Assert.Equal(17, enemy.Hp);
    }

    [Fact]
    public void NewCombat_SamplesActOneWeakEncounterPools()
    {
        var states = Enumerable.Range(0, 64).Select(seed => CombatFactory.NewCombat(seed)).ToList();
        var shapes = states
            .Select(state =>
                (
                    state.EncounterId,
                    Count: state.Enemies.Count,
                    Intents: string.Join(",", state.Enemies.Select(e => e.CurrentIntent.Type))
                )
            )
            .Distinct()
            .ToList();

        Assert.True(shapes.Count >= 6);
        Assert.DoesNotContain(states, s => s.Enemies.Any(e => e.DefId == 16)); // Chomper is not an opening easy encounter.
        Assert.Contains(states, s => s.Enemies.Any(e => e.DefId == 56)); // Nibbit
        Assert.Contains(states, s => s.Enemies.Any(e => e.DefId == 69)); // Seapunk
        Assert.Contains(states, s => s.Enemies.Any(e => e.DefId == 71)); // ShrinkerBeetle
        Assert.Contains(states, s => s.Enemies.Any(e => e.DefId == 93)); // Toadpole
    }

    [Fact]
    public void ChomperDebuff_AddsDazedToDiscard()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var enemy = new EnemyState
        {
            DefId = 16,
            Hp = 60,
            MaxHp = 60,
            CurrentIntent = new Intent(IntentType.Debuff, 3),
            Buffs = [],
        };

        EnemyAI.ExecuteIntent(enemy, state, new Random(0));

        Assert.Equal(3, state.DiscardPile.Count(c => c.DefId == ST.Dazed));
    }

    [Fact]
    public void ForcedChompers_MatchesDecompiledOpening()
    {
        var state = new CombatState();
        CombatFactory.Reset(state, new Random(0), StarterDeckIds, encounterId: 1);

        Assert.Equal(1, state.EncounterId);
        Assert.Equal(2, state.Enemies.Count);
        Assert.All(
            state.Enemies,
            enemy =>
            {
                Assert.Equal(16, enemy.DefId);
                Assert.Equal(2, BuffSystem.Get(enemy.Buffs, BuffId.Artifact));
            }
        );
        Assert.Equal(IntentType.Attack, state.Enemies[0].CurrentIntent.Type);
        Assert.Equal(18, state.Enemies[0].CurrentIntent.Magnitude);
        Assert.Equal(IntentType.Debuff, state.Enemies[1].CurrentIntent.Type);
        Assert.Equal(3, state.Enemies[1].CurrentIntent.Magnitude);
    }

    [Fact]
    public void SlimeDebuff_AddsSlimedToDiscard()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var enemy = new EnemyState
        {
            DefId = 47,
            Hp = 32,
            MaxHp = 32,
            CurrentIntent = new Intent(IntentType.Debuff, 2),
            Buffs = [],
        };

        EnemyAI.ExecuteIntent(enemy, state, new Random(0));

        Assert.Equal(2, state.DiscardPile.Count(c => c.DefId == ST.Slimed));
    }

    [Fact]
    public void Shrink_ReducesPoweredAttackDamageByThirtyPercent()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand.Clear();
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.ExhaustPile.Clear();
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 56,
                Hp = 20,
                MaxHp = 20,
                CurrentIntent = new Intent(IntentType.Attack, 0),
                Buffs = [],
            },
        ];
        state.Hand.Add(new CardInstance(IC.StrikeIronclad, false));
        state.Energy = 3;
        BuffSystem.Apply(state.PlayerBuffs, BuffId.Shrink, 1);

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(16, state.Enemies[0].Hp);
    }

    [Fact]
    public void Thorns_RetaliatesAgainstPoweredAttacks()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand.Clear();
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.ExhaustPile.Clear();
        state.PlayerHp = 64;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 93,
                Hp = 20,
                MaxHp = 20,
                CurrentIntent = new Intent(IntentType.Attack, 0),
                Buffs = [new BuffState(BuffId.Thorns, 2)],
            },
        ];
        state.Hand.Add(new CardInstance(IC.StrikeIronclad, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(62, state.PlayerHp);
        Assert.Equal(14, state.Enemies[0].Hp);
    }

    [Fact]
    public void Ravenous_StrengthensAndStunsCorpseSlugWhenAllyDies()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand.Clear();
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.ExhaustPile.Clear();
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 17,
                Hp = 1,
                MaxHp = 25,
                CurrentIntent = new Intent(IntentType.Attack, 6),
                Buffs = [new BuffState(BuffId.Ravenous, 5)],
            },
            new EnemyState
            {
                DefId = 17,
                Hp = 25,
                MaxHp = 25,
                CurrentIntent = new Intent(IntentType.Attack, 6),
                Buffs = [new BuffState(BuffId.Ravenous, 5)],
            },
        ];
        state.Hand.Add(new CardInstance(IC.StrikeIronclad, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(0, state.Enemies[0].Hp);
        Assert.Equal(5, BuffSystem.Get(state.Enemies[1].Buffs, BuffId.Strength));
        Assert.Equal(1, BuffSystem.Get(state.Enemies[1].Buffs, BuffId.Stunned));

        EnemyAI.ExecuteIntent(state.Enemies[1], state, new Random(0));

        Assert.Equal(0, BuffSystem.Get(state.Enemies[1].Buffs, BuffId.Stunned));
        Assert.Equal(64, state.PlayerHp);

        EnemyAI.ChooseIntents(state.Enemies, state.Turn, new Random(0));

        Assert.Equal(IntentType.Attack, state.Enemies[1].CurrentIntent.Type);
        // WHIP_SLAP is MultiAttackIntent(3, 2), not a single 6. Folded into one hit the
        // two are the same number only while the slug has no Strength -- and Ravenous,
        // which this very test just handed it, is how it gets some.
        Assert.Equal(3, state.Enemies[1].CurrentIntent.Magnitude);
        Assert.Equal(2, state.Enemies[1].CurrentIntent.Hits);
    }

    [Fact]
    public void Slippery_CapsOneUnblockedHitThenExpires()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand.Clear();
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.ExhaustPile.Clear();
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 42,
                Hp = 20,
                MaxHp = 20,
                CurrentIntent = new Intent(IntentType.Attack, 4),
                Buffs = [new BuffState(BuffId.Slippery, 1)],
            },
        ];
        state.Hand.Add(new CardInstance(IC.StrikeIronclad, false));
        state.Hand.Add(new CardInstance(IC.StrikeIronclad, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(13, state.Enemies[0].Hp);
        Assert.Equal(0, BuffSystem.Get(state.Enemies[0].Buffs, BuffId.Slippery));
    }

    [Fact]
    public void Surprise_SpawnsGremlinsWhenMercDies()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand.Clear();
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.ExhaustPile.Clear();
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 37,
                Hp = 1,
                MaxHp = 47,
                CurrentIntent = new Intent(IntentType.Attack, 16),
                Buffs = [new BuffState(BuffId.Surprise, 1)],
            },
        ];
        state.Hand.Add(new CardInstance(IC.StrikeIronclad, false));
        state.Energy = 3;

        var result = CombatEngine.Step(state, 0, new Random(0));

        Assert.False(result.Terminal);
        Assert.Contains(state.Enemies, e => e.DefId == 78 && e.Hp > 0);
        Assert.Contains(state.Enemies, e => e.DefId == 28 && e.Hp > 0);
        Assert.Contains(
            state.Enemies,
            e => e.DefId == 78 && BuffSystem.Get(e.Buffs, BuffId.Stunned) == 1
        );
        Assert.Contains(
            state.Enemies,
            e => e.DefId == 28 && BuffSystem.Get(e.Buffs, BuffId.Stunned) == 1
        );
    }

    [Fact]
    public void GremlinMerc_AttacksStealGold()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerGold = 99;
        state.PlayerBlock = 99;
        var merc = new EnemyState
        {
            DefId = 37,
            Hp = 53,
            MaxHp = 53,
            CurrentIntent = new Intent(IntentType.Attack, 16),
            Buffs = [],
        };

        EnemyAI.ExecuteIntent(merc, state, new Random(0));

        Assert.Equal(79, state.PlayerGold);
        Assert.Equal(20, merc.StolenGold);
    }

    [Fact]
    public void GremlinMerc_TransfersStolenGoldToFatGremlinHeist()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand.Clear();
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.ExhaustPile.Clear();
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 37,
                Hp = 1,
                MaxHp = 47,
                CurrentIntent = new Intent(IntentType.Attack, 16),
                Buffs = [new BuffState(BuffId.Surprise, 1)],
                StolenGold = 40,
            },
        ];
        state.Hand.Add(new CardInstance(IC.StrikeIronclad, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Contains(state.Enemies, e => e.DefId == 28 && e.HeistGold == 40);
    }

    [Fact]
    public void HeistGold_ReturnsWhenFatGremlinDies()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerGold = 59;
        state.Hand.Clear();
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.ExhaustPile.Clear();
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 28,
                Hp = 1,
                MaxHp = 13,
                CurrentIntent = new Intent(IntentType.Unknown, 0),
                Buffs = [],
                HeistGold = 40,
            },
        ];
        state.Hand.Add(new CardInstance(IC.StrikeIronclad, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(99, state.PlayerGold);
        Assert.Equal(0, state.Enemies[0].HeistGold);
    }

    /// <summary>A pack of rats in the slots the encounter places them in, Slots[2..4].</summary>
    private static CombatState RatPack(params (int Slot, int Hp)[] rats)
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Enemies =
        [
            .. rats.Select(rat => new EnemyState
            {
                DefId = 101,
                Hp = rat.Hp,
                MaxHp = 20,
                Slot = rat.Slot,
                CurrentIntent = new Intent(IntentType.Buff, 0),
                Buffs = [],
            }),
        ];
        return state;
    }

    [Fact]
    public void TwoTailedRat_CallForBackupSummonsRatAndTracksLimit()
    {
        var state = RatPack((2, 20), (3, 20), (4, 20));

        EnemyAI.ExecuteIntent(state.Enemies[0], state, new Random(0));

        Assert.Equal(4, state.Enemies.Count(e => e.DefId == 101));
        Assert.All(
            state.Enemies.Where(e => e.DefId == 101),
            rat => Assert.Equal(1, BuffSystem.Get(rat.Buffs, BuffId.BackupCount))
        );
        // NOT stunned. CallForBackup adds the rat with a plain CreatureCmd.Add — a
        // monster meant to sit out its arrival sets StartStunned, and this one does not —
        // and the enemy phase iterates a snapshot of the roster, so the newcomer misses
        // the phase that summoned it either way. In a live A8 capture the rat summoned on
        // turn 3 attacks for 6 on turn 4; with a stun it stood there instead.
        Assert.All(
            state.Enemies.Where(e => e.DefId == 101),
            rat => Assert.Equal(0, BuffSystem.Get(rat.Buffs, BuffId.Stunned))
        );
        // With the three starters standing, the last free slot is "second" -- so the
        // newcomer leads the pack.
        Assert.Equal(1, state.Enemies[0].Slot);
        Assert.Equal(IntentType.Unknown, state.Enemies[0].CurrentIntent.Type);
    }

    /// <summary>
    /// CallForBackup takes <c>Slots.LastOrDefault</c>, so where the newcomer stands
    /// depends on which rats are still alive rather than being the front every time.
    /// A dead rat's slot is free again: with "fifth" empty the summon joins the BACK,
    /// and a target index that named the survivor before the summon still names it after.
    /// </summary>
    [Fact]
    public void TwoTailedRat_BackupTakesTheLastSlotLeftEmpty()
    {
        var state = RatPack((2, 0), (3, 20), (4, 0));

        EnemyAI.ExecuteIntent(state.Enemies[1], state, new Random(0));

        Assert.Equal(4, state.Enemies.Count(e => e.DefId == 101));
        var summoned = state.Enemies[^1];
        Assert.Equal(4, summoned.Slot);
        Assert.Equal(20, state.Enemies[1].Hp);
    }

    [Fact]
    public void TwoTailedRat_CallForBackupRespectsTotalSlotLimit()
    {
        var state = RatPack((0, 20), (1, 20), (2, 20), (3, 20), (4, 20));

        EnemyAI.ExecuteIntent(state.Enemies[0], state, new Random(0));

        Assert.Equal(5, state.Enemies.Count(e => e.DefId == 101));
    }

    [Fact]
    public void FabricatorBots_UseDecompiledLoopingMoves()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerBlock = 0;
        state.PlayerHp = 64;
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        var fabricator = new EnemyState
        {
            DefId = KE.Fabricator,
            Hp = 50,
            MaxHp = 50,
            CurrentIntent = new Intent(IntentType.Attack, 0),
            Buffs = [],
        };
        state.Enemies =
        [
            fabricator,
            new EnemyState
            {
                DefId = KE.Guardbot,
                Hp = 20,
                MaxHp = 20,
                CurrentIntent = new Intent(IntentType.Defend, 15),
                Buffs = [],
            },
            new EnemyState
            {
                DefId = KE.Noisebot,
                Hp = 20,
                MaxHp = 20,
                CurrentIntent = new Intent(IntentType.Debuff, 2),
                Buffs = [],
            },
            new EnemyState
            {
                DefId = KE.Stabbot,
                Hp = 20,
                MaxHp = 20,
                CurrentIntent = new Intent(IntentType.Debuff, 12),
                Buffs = [],
            },
            new EnemyState
            {
                DefId = KE.Zapbot,
                Hp = 20,
                MaxHp = 20,
                CurrentIntent = new Intent(IntentType.Attack, 15),
                Buffs = [new BuffState(BuffId.HighVoltage, 2)],
            },
        ];

        EnemyAI.ExecuteIntent(state.Enemies[1], state, new Random(0));
        EnemyAI.ExecuteIntent(state.Enemies[2], state, new Random(0));
        EnemyAI.ExecuteIntent(state.Enemies[3], state, new Random(0));
        EnemyAI.ExecuteIntent(state.Enemies[4], state, new Random(0));

        Assert.Equal(15, fabricator.Block);
        Assert.Equal(
            2,
            state.DiscardPile.Count(c => c.DefId == ST.Dazed)
                + state.DrawPile.Count(c => c.DefId == ST.Dazed)
        );
        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.Frail));
        Assert.Equal(37, state.PlayerHp);
        Assert.Equal(2, BuffSystem.Get(state.Enemies[4].Buffs, BuffId.Strength));
    }

    [Fact]
    public void ToughEgg_HatchesThenLoopsNibble()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerHp = 64;
        var egg = new EnemyState
        {
            DefId = KE.ToughEgg,
            Hp = 16,
            MaxHp = 16,
            CurrentIntent = new Intent(IntentType.Buff, 0),
            Buffs = [new BuffState(BuffId.Hatch, 1), new BuffState(BuffId.Minion, 1)],
        };

        EnemyAI.ExecuteIntent(egg, state, new Random(0));
        EnemyAI.ChooseIntents([egg], 0, new Random(0));
        EnemyAI.ExecuteIntent(egg, state, new Random(0));

        Assert.Equal(0, BuffSystem.Get(egg.Buffs, BuffId.Hatch));
        Assert.Equal(1, BuffSystem.Get(egg.Buffs, BuffId.Minion));
        Assert.InRange(egg.MaxHp, 20, 23);
        Assert.Equal(IntentType.Attack, egg.CurrentIntent.Type);
        Assert.Equal(59, state.PlayerHp);
    }

    [Fact]
    public void AdversaryVariants_UseBarrageAttackAndStrengthMove()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerBlock = 999;
        var mkThree = new EnemyState
        {
            DefId = KE.TheAdversaryMkThree,
            Hp = 300,
            MaxHp = 300,
            CurrentIntent = new Intent(IntentType.Buff, 20),
            Buffs = [new BuffState(BuffId.Artifact, 2)],
            MoveIndex = 2,
        };

        EnemyAI.ExecuteIntent(mkThree, state, new Random(0));

        Assert.Equal(999 - 10 - 10, state.PlayerBlock);
        Assert.Equal(4, BuffSystem.Get(mkThree.Buffs, BuffId.Strength));
    }

    [Fact]
    public void Axebot_StockRespawnsBeforeCombatCanEnd()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.StrikeIronclad, false)];
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.Energy = 3;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = KE.Axebot,
                Hp = 1,
                MaxHp = 76,
                CurrentIntent = new Intent(IntentType.Attack, 14),
                Buffs = [new BuffState(BuffId.Stock, 2)],
                MoveIndex = 2,
            },
        ];

        var result = CombatEngine.Step(state, 0, new Random(0));

        Assert.False(result.Terminal);
        Assert.InRange(state.Enemies[0].Hp, 76, 86);
        Assert.Equal(1, BuffSystem.Get(state.Enemies[0].Buffs, BuffId.Stock));
        Assert.Equal(IntentType.Defend, state.Enemies[0].CurrentIntent.Type);
    }

    [Fact]
    public void PhrogParasite_InfestedSpawnsWrigglersBeforeCombatCanEnd()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.StrikeIronclad, false)];
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.Energy = 3;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = KE.PhrogParasite,
                Hp = 1,
                MaxHp = 66,
                CurrentIntent = new Intent(IntentType.Debuff, 3),
                Buffs = [new BuffState(BuffId.Infested, 4)],
            },
        ];

        var result = CombatEngine.Step(state, 0, new Random(0));

        Assert.False(result.Terminal);
        Assert.Equal(4, state.Enemies.Count(enemy => enemy.DefId == KE.Wriggler));
        Assert.All(
            state.Enemies.Where(enemy => enemy.DefId == KE.Wriggler),
            enemy => Assert.Equal(1, BuffSystem.Get(enemy.Buffs, BuffId.Stunned))
        );
    }

    [Fact]
    public void TestSubject_AdaptableRespawnsThroughSecondAndThirdForms()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.Enemies =
        [
            new EnemyState
            {
                DefId = KE.TestSubject,
                Hp = 1,
                MaxHp = 111,
                CurrentIntent = new Intent(IntentType.Attack, 22),
                Buffs = [new BuffState(BuffId.Adaptable, 1)],
            },
        ];

        state.Hand = [new CardInstance(IC.StrikeIronclad, false)];
        state.Energy = 3;
        var first = CombatEngine.Step(state, 0, new Random(0));

        Assert.False(first.Terminal);
        Assert.Equal(212, state.Enemies[0].Hp);
        Assert.Equal(1, BuffSystem.Get(state.Enemies[0].Buffs, BuffId.PainfulStabs));

        state.Enemies[0].Hp = 1;
        state.Hand = [new CardInstance(IC.StrikeIronclad, false)];
        state.Energy = 3;
        var second = CombatEngine.Step(state, 0, new Random(0));

        Assert.False(second.Terminal);
        Assert.Equal(313, state.Enemies[0].Hp);
        Assert.Equal(0, BuffSystem.Get(state.Enemies[0].Buffs, BuffId.Adaptable));
        Assert.Equal(0, BuffSystem.Get(state.Enemies[0].Buffs, BuffId.PainfulStabs));
        Assert.Equal(1, BuffSystem.Get(state.Enemies[0].Buffs, BuffId.Intangible));
    }

    [Fact]
    public void Slow_IncreasesPoweredAttackDamageAfterEachPlayedCard()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.DefendIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
        ];
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.Energy = 3;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = KE.BygoneEffigy,
                Hp = 132,
                MaxHp = 132,
                CurrentIntent = new Intent(IntentType.Unknown, 0),
                Buffs = [new BuffState(BuffId.Slow, 1)],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(126, state.Enemies[0].Hp);
        Assert.Equal(2, BuffSystem.Get(state.Enemies[0].Buffs, BuffId.SlowCount));
    }

    [Fact]
    public void TerritorialAndHighVoltage_GainStrengthAtEnemyTurnEnd()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var byrdonis = new EnemyState
        {
            DefId = KE.Byrdonis,
            Hp = 90,
            MaxHp = 90,
            CurrentIntent = new Intent(IntentType.Attack, 19),
            Buffs = [new BuffState(BuffId.Territorial, 1)],
        };
        state.Enemies = [byrdonis];

        EnemyAI.ExecuteIntent(byrdonis, state, new Random(0));

        Assert.Equal(1, BuffSystem.Get(byrdonis.Buffs, BuffId.Strength));
    }

    [Fact]
    public void Byrdonis_PeckDealsThreeStrengthModifiedHits()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var byrdonis = new EnemyState
        {
            DefId = KE.Byrdonis,
            Hp = 90,
            MaxHp = 90,
            MoveIndex = 1,
            // The intent SelectIntent builds for PECK: per-hit damage and a hit count,
            // not a pre-multiplied total. It used to be able to say anything, because a
            // hand-written Byrdonis branch dealt the hits from its own literals and
            // ignored the intent -- that branch was the generic multi-hit path written
            // out twice, and it is gone.
            CurrentIntent = new Intent(IntentType.Attack, 3, Hits: 3),
            Buffs = [new BuffState(BuffId.Strength, 1)],
        };
        state.Enemies = [byrdonis];
        state.PlayerHp = 66;
        state.PlayerBlock = 5;

        EnemyAI.ExecuteIntent(byrdonis, state, new Random(0));

        // PeckDamage is 3 at A8 (4 only at DeadlyEnemies), +1 Strength = 4 per hit,
        // three hits. 5 block absorbs all of hit one and 1 of hit two, so the player
        // takes 3 + 4: 66 -> 59.
        Assert.Equal(59, state.PlayerHp);
    }

    [Fact]
    public void BlockPotion_GainsTwelveBlock()
    {
        var state = CombatFactory.NewCombat(seed: 0);

        PotionEffects.Apply(5, state);

        Assert.Equal(12, state.PlayerBlock);
    }

    [Fact]
    public void ShacklingPotion_AppliesTemporaryStrengthLossToAllEnemies()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 30,
                MaxHp = 30,
                Buffs = [],
            },
            new EnemyState
            {
                DefId = 16,
                Hp = 30,
                MaxHp = 30,
                Buffs = [new BuffState(BuffId.Artifact, 1)],
            },
        ];

        PotionEffects.Apply(51, state);

        Assert.Equal(-7, BuffSystem.Get(state.Enemies[0].Buffs, BuffId.Strength));
        Assert.Equal(7, BuffSystem.Get(state.Enemies[0].Buffs, BuffId.TemporaryStrength));
        Assert.Equal(0, BuffSystem.Get(state.Enemies[1].Buffs, BuffId.Artifact));
        Assert.Equal(0, BuffSystem.Get(state.Enemies[1].Buffs, BuffId.Strength));
        Assert.Equal(0, BuffSystem.Get(state.Enemies[1].Buffs, BuffId.TemporaryStrength));
    }

    [Fact]
    public void PhrogParasite_LashDealsFourStrengthModifiedHits()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var phrog = new EnemyState
        {
            DefId = KE.PhrogParasite,
            Hp = 67,
            MaxHp = 67,
            MoveIndex = 1,
            // LashDamage x 4, as SelectIntent declares it: 4 per hit at A8.
            CurrentIntent = new Intent(IntentType.Attack, 4, Hits: 4),
            Buffs = [],
        };
        state.Enemies = [phrog];
        state.PlayerHp = 63;
        state.PlayerBlock = 0;

        PotionEffects.Apply(51, state);
        EnemyAI.ExecuteIntent(phrog, state, new Random(0));

        Assert.Equal(63, state.PlayerHp);
        Assert.Equal(0, BuffSystem.Get(phrog.Buffs, BuffId.Strength));
        Assert.Equal(0, BuffSystem.Get(phrog.Buffs, BuffId.TemporaryStrength));
    }

    [Fact]
    public void PersonalHive_AddsDazedWhenEntomancerTakesPoweredAttackDamage()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.StrikeIronclad, false)];
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.Energy = 3;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = KE.Entomancer,
                Hp = 155,
                MaxHp = 155,
                CurrentIntent = new Intent(IntentType.Attack, 24),
                Buffs = [new BuffState(BuffId.PersonalHive, 1)],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Single(state.DrawPile, card => card.DefId == ST.Dazed);
    }

    [Fact]
    public void Galvanic_DamagesPlayerWhenPowerCardIsPlayed()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Inflame, false)];
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.Energy = 3;
        state.PlayerHp = 64;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = KE.GlobeHead,
                Hp = 158,
                MaxHp = 158,
                CurrentIntent = new Intent(IntentType.Attack, 21),
                Buffs = [new BuffState(BuffId.Galvanic, 6)],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(58, state.PlayerHp);
    }

    [Fact]
    public void MixedEnemyIntents_ExposeSecondaryAttackMetadata()
    {
        var state = new CombatState();

        CombatFactory.Reset(state, new Random(0), StarterDeckIds, encounterId: 7);
        var merc = Assert.Single(state.Enemies);
        merc.MoveIndex = 1;
        merc.CurrentIntent = new Intent(IntentType.Debuff, 14);
        EnemyAI.UpdateSecondaryIntents(state.Enemies);

        Assert.Equal(IntentType.Attack, merc.SecondaryIntent?.Type);
        Assert.Equal(14, merc.SecondaryIntent?.Magnitude);
    }

    [Fact]
    public void ForcedNormalEncounters_CreateExpectedShapes()
    {
        var expectedEnemyIds = new Dictionary<int, int>
        {
            [14] = 53, // Mawler
            [15] = 56, // Nibbits
            [16] = 47, // Large slimes include LeafSlimeM
            [17] = 30, // Flyconid encounter
            [18] = 77, // Snapping Jaxfruit
            [19] = 20, // Cubex Construct
            [20] = 103, // Vine Shambler
            [21] = 71, // Shrinker Beetle + Fuzzy
            [22] = 14, // Calcified Cultist + Seapunk
            [23] = 32, // Fossil Stalker
            [24] = 65, // Punch Construct
            [25] = 70, // Sewer Clam
            [26] = 39, // Haunted Ship
            [27] = 74, // Slithering Strangler
            [28] = 96, // Ruby Raiders include TrackerRubyRaider
            [29] = 31, // Fogmog
            [30] = 49, // Living Fog
        };

        foreach (var (encounterId, enemyId) in expectedEnemyIds)
        {
            var state = CombatFactory.NewCombat(seed: encounterId);

            CombatFactory.Reset(state, new Random(encounterId), StarterDeckIds, encounterId);

            Assert.Equal(encounterId, state.EncounterId);
            Assert.Contains(state.Enemies, enemy => enemy.DefId == enemyId);
        }
    }

    /// <summary>
    /// PlatingPower decrements on <c>AfterSideTurnStart</c> and grants its block on
    /// <c>BeforeSideTurnEndEarly</c>, and it skips the decrement for enemies on round one.
    /// So the first turn ends on the full amount and the decay starts on the second.
    /// </summary>
    /// <remarks>
    /// This used to assert gain-then-decrement on the very first turn, which is a point of
    /// block ahead of the game for the rest of the fight. A live Sewer Clam holds 9 block
    /// at Plating 9 and then 8 at Plating 8 (`SAM9XS24LM`). CombatState.Turn counts from
    /// zero, so the first enemy phase is Turn 0.
    /// </remarks>
    [Fact]
    public void SewerClam_PlatingHoldsItsFullAmountOnRoundOneThenDecays()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var enemy = new EnemyState
        {
            DefId = 70,
            Hp = 45,
            MaxHp = 45,
            CurrentIntent = new Intent(IntentType.Attack, 11),
            Buffs = [new BuffState(BuffId.Plating, 9)],
        };

        EnemyAI.ExecuteIntent(enemy, state, new Random(0));

        Assert.Equal(9, enemy.Block);
        Assert.Equal(9, BuffSystem.Get(enemy.Buffs, BuffId.Plating));

        enemy.Block = 0;
        state.Turn++;
        EnemyAI.ExecuteIntent(enemy, state, new Random(0));

        Assert.Equal(8, enemy.Block);
        Assert.Equal(8, BuffSystem.Get(enemy.Buffs, BuffId.Plating));
    }

    /// <summary>
    /// PRESSURIZE_MOVE is <c>PowerCmd.Apply&lt;StrengthPower&gt;(4)</c> and nothing else.
    /// The block the clam's buff turn used to also gain was invented, and it gave the clam
    /// twice the block the game shows on the turn it pressurises.
    /// </summary>
    [Fact]
    public void SewerClam_PressurizeGivesStrengthAndNoExtraBlock()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var enemy = new EnemyState
        {
            DefId = 70,
            Hp = 45,
            MaxHp = 45,
            CurrentIntent = new Intent(IntentType.Buff, 0),
            Buffs = [new BuffState(BuffId.Plating, 9)],
        };
        state.Turn = 1;

        EnemyAI.ExecuteIntent(enemy, state, new Random(0));

        Assert.Equal(4, BuffSystem.Get(enemy.Buffs, BuffId.Strength));
        // Plating's own 8, once -- not twice.
        Assert.Equal(8, enemy.Block);
    }

    [Fact]
    public void HauntedShip_HauntAppliesWeakAndDazed()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var enemy = new EnemyState
        {
            DefId = 39,
            Hp = 67,
            MaxHp = 67,
            CurrentIntent = new Intent(IntentType.Debuff, 5),
            Buffs = [],
        };

        EnemyAI.ExecuteIntent(enemy, state, new Random(0));

        Assert.Equal(3, BuffSystem.Get(state.PlayerBuffs, BuffId.Weak));
        Assert.Equal(5, state.DiscardPile.Count(c => c.DefId == ST.Dazed));
    }

    [Fact]
    public void Fogmog_IllusionSummonsEyeWithTeeth()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 31,
                Hp = 78,
                MaxHp = 78,
                CurrentIntent = new Intent(IntentType.Buff, 0),
                Buffs = [],
            },
        ];

        EnemyAI.ExecuteIntent(state.Enemies[0], state, new Random(0));

        Assert.Contains(
            state.Enemies,
            e => e.DefId == 26 && BuffSystem.Get(e.Buffs, BuffId.Illusion) == 1
        );
        // NOT stunned. Wriggler is the only monster in the game that sets StartStunned,
        // and Fogmog adds the eye with a plain CreatureCmd.Add; the enemy phase iterates
        // a snapshot of the roster, so the eye misses the phase that summoned it without
        // needing a stun. With one, its three Dazed reached the player's draw pile a turn
        // late — visible as the wrong hand from turn 3 in a live A8 capture.
        Assert.All(
            state.Enemies.Where(e => e.DefId == 26),
            eye => Assert.Equal(0, BuffSystem.Get(eye.Buffs, BuffId.Stunned))
        );
    }

    [Fact]
    public void LivingFog_AdvancedGasAppliesSmoggy()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var enemy = new EnemyState
        {
            DefId = 49,
            Hp = 82,
            MaxHp = 82,
            CurrentIntent = new Intent(IntentType.Debuff, 9),
            Buffs = [],
        };

        EnemyAI.ExecuteIntent(enemy, state, new Random(0));

        Assert.Equal(55, state.PlayerHp);
        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.Smoggy));
    }

    [Fact]
    public void Tangled_IncreasesAttackEnergyCostUntilNextTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.StrikeIronclad, false));
        state.Energy = 1;
        BuffSystem.Apply(state.PlayerBuffs, BuffId.Tangled, 1);

        Assert.DoesNotContain(0, CombatEngine.ValidActions(state));
        Assert.Equal(StepResult.Invalid, CombatEngine.Step(state, 0, new Random(0)));
    }

    [Fact]
    public void Smoggy_BlocksAdditionalSkillsAfterSkillPlayed()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand.Clear();
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.ExhaustPile.Clear();
        state.Hand.Add(new CardInstance(IC.DefendIronclad, false));
        state.Hand.Add(new CardInstance(IC.DefendIronclad, false));
        BuffSystem.Apply(state.PlayerBuffs, BuffId.Smoggy, 1);

        CombatEngine.Step(state, 0, new Random(0));

        Assert.DoesNotContain(0, CombatEngine.ValidActions(state));
        Assert.Equal(StepResult.Invalid, CombatEngine.Step(state, 0, new Random(0)));
    }

    [Fact]
    public void Constrict_DamagesAtEndTurnAndExpiresWhenStranglerDies()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand.Clear();
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.ExhaustPile.Clear();
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 30,
                MaxHp = 30,
                CurrentIntent = new Intent(IntentType.Defend, 0),
                Buffs = [],
            },
        ];
        BuffSystem.Apply(state.PlayerBuffs, BuffId.Constrict, 3);

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(61, state.PlayerHp);

        state.Hand.Add(new CardInstance(IC.StrikeIronclad, false));
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 74,
                Hp = 1,
                MaxHp = 56,
                CurrentIntent = new Intent(IntentType.Debuff, 3),
                Buffs = [],
            },
        ];
        state.Energy = 3;
        BuffSystem.Apply(state.PlayerBuffs, BuffId.Constrict, 3);

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(0, BuffSystem.Get(state.PlayerBuffs, BuffId.Constrict));
    }

    private static ReadOnlySpan<int> StarterDeckIds =>
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

    [Fact]
    public void Artifact_PreventsEnemyDebuff()
    {
        var enemy = new EnemyState { Buffs = [] };
        BuffSystem.Apply(enemy.Buffs, BuffId.Artifact, 2);

        BuffSystem.Apply(enemy.Buffs, BuffId.Vulnerable, 2);

        Assert.Equal(1, BuffSystem.Get(enemy.Buffs, BuffId.Artifact));
        Assert.Equal(0, BuffSystem.Get(enemy.Buffs, BuffId.Vulnerable));
    }

    [Fact]
    public void NibbitSlice_GainsBlock()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var enemy = new EnemyState
        {
            DefId = 56,
            Hp = 44,
            MaxHp = 44,
            CurrentIntent = new Intent(IntentType.Attack, 7),
            Buffs = [],
            MoveIndex = 1,
        };

        EnemyAI.ExecuteIntent(enemy, state, new Random(0));

        Assert.Equal(6, enemy.Block);
    }

    [Fact]
    public void HardToKill_CapsDamagePerHit()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 25,
                Hp = 24,
                MaxHp = 24,
                CurrentIntent = new Intent(IntentType.Attack, 4),
                Buffs = [new BuffState(BuffId.HardToKill, 9)],
            },
        ];

        CardEffects.DealDamage(state, 50);

        Assert.Equal(15, state.Enemies[0].Hp);
    }

    [Fact]
    public void EndTurn_AdvancesTurnCounter()
    {
        var state = CombatFactory.NewCombat(seed: 42);
        int endTurn = state.Hand.Count;
        var rng = new Random(42);

        CombatEngine.Step(state, endTurn, rng);

        Assert.Equal(1, state.Turn);
    }

    [Fact]
    public void ValidActions_AlwaysIncludesEndTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        int endTurn = state.Hand.Count;

        var actions = CombatEngine.ValidActions(state);

        Assert.Contains(endTurn, actions);
    }

    [Fact]
    public void PlayCard_CostsEnergy()
    {
        var state = CombatFactory.NewCombat(seed: 1);
        var rng = new Random(1);
        int before = state.Energy;

        int action = CombatEngine.ValidActions(state).First(a => a < state.Hand.Count);
        CombatEngine.Step(state, action, rng);

        Assert.True(state.Energy < before);
    }

    [Fact]
    public void Strike_DealsDamageToEnemy()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 14,
                Hp = 30,
                MaxHp = 30,
                CurrentIntent = new Intent(IntentType.Attack, 9),
                Buffs = [],
            },
        ];
        int enemyHp = state.Enemies[0].Hp;

        // Force Strike into hand for determinism
        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.StrikeIronclad, false));

        CombatEngine.Step(state, 0, rng); // play Strike

        Assert.True(state.Enemies[0].Hp < enemyHp);
    }

    [Fact]
    public void Defend_GainsBlock()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.DefendIronclad, false));

        CombatEngine.Step(state, 0, rng); // play Defend

        Assert.True(state.PlayerBlock > 0);
    }

    [Fact]
    public void CalcifiedCultist_BuffsOnTurn1_AttacksAfter()
    {
        var state = new CombatState();
        var rng = new Random(0);
        CombatFactory.Reset(state, rng, StarterDeckIds, encounterId: 0);

        Assert.All(state.Enemies, enemy => Assert.Equal(IntentType.Buff, enemy.CurrentIntent.Type));

        CombatEngine.Step(state, state.Hand.Count, rng); // end turn

        Assert.All(
            state.Enemies,
            enemy => Assert.Equal(IntentType.Attack, enemy.CurrentIntent.Type)
        );
    }

    [Fact]
    public void FreeAttackPower_MakesNextAttackFree()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);

        BuffSystem.Apply(state.PlayerBuffs, BuffId.FreeAttackPower, 1);
        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.StrikeIronclad, false)); // normally costs 1
        state.Energy = 0; // no energy, but FreeAttackPower makes it free

        var actions = CombatEngine.ValidActions(state);
        Assert.Contains(0, actions); // playable despite 0 energy
    }

    [Fact]
    public void FreeAttackPower_DecrementsAfterAttack()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);

        BuffSystem.Apply(state.PlayerBuffs, BuffId.FreeAttackPower, 2);
        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.StrikeIronclad, false));
        state.Energy = 0;

        CombatEngine.Step(state, 0, rng); // play first free Attack
        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.FreeAttackPower));
    }
}
