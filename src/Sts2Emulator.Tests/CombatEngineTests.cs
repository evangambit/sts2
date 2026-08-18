using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Sts2Emulator.Interop;
using Xunit;

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

    // ── turn-1 draw-pile reorder ──────────────────────────────────────────────
    // Ports MegaCrit.Sts2.Core.Combat/CombatManager.cs ~line 658.

    private static List<CardInstance> Pile(params int[] defIds) =>
        defIds.Select(id => new CardInstance(id, false)).ToList();

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
    public void AscendersBane_IsNotPlayable()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.AscendersBane, false));

        var actions = CombatEngine.ValidActions(state);
        var result = CombatEngine.Step(state, 0, new Random(0));

        Assert.DoesNotContain(0, actions);
        Assert.Contains(1, actions);
        Assert.Equal(StepResult.Invalid, result);
    }

    [Fact]
    public void Bash_DoesNotApplyVulnerableToNextEnemyWhenTargetDies()
    {
        var state = new CombatState
        {
            PlayerHp = 64,
            PlayerMaxHp = 80,
            Energy = 3,
            MaxEnergy = 3,
            Hand = [new CardInstance(IC.Bash, Upgraded: false)],
            Enemies =
            [
                new EnemyState
                {
                    DefId = KE.LeafSlimeS,
                    Hp = 8,
                    MaxHp = 8,
                },
                new EnemyState
                {
                    DefId = KE.TwigSlimeM,
                    Hp = 29,
                    MaxHp = 29,
                },
            ],
        };

        CombatEngine.Step(state, 0, new Random(0), targetEnemyIndex: 0);

        Assert.Equal(0, state.Enemies[0].Hp);
        Assert.Empty(state.Enemies[0].Buffs);
        Assert.Empty(state.Enemies[1].Buffs);
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
    public void ResetWithRelics_AppliesCombatStartRelics()
    {
        var state = new CombatState();

        CombatFactory.Reset(
            state,
            new Random(0),
            StarterDeckIds,
            encounterId: 1,
            relicIds:
            [
                RelicEffects.Anchor,
                RelicEffects.BagOfPreparation,
                RelicEffects.BloodVial,
                RelicEffects.BronzeScales,
                RelicEffects.OddlySmoothStone,
                RelicEffects.Vajra,
            ]
        );

        Assert.Equal(7, state.Hand.Count);
        Assert.Equal(66, state.PlayerHp);
        Assert.Equal(10, state.PlayerBlock);
        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.Strength));
        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.Dexterity));
        Assert.Equal(3, BuffSystem.Get(state.PlayerBuffs, BuffId.Thorns));
    }

    [Fact]
    public void BoomingConch_DrawsAndGivesEnergyInEliteCombatOnly()
    {
        var elite = new CombatState();
        CombatFactory.Reset(
            elite,
            new Random(0),
            StarterDeckIds,
            encounterId: 82,
            relicIds: [RelicEffects.BoomingConch],
            playerHp: 64,
            playerMaxHp: 80
        );

        Assert.True(elite.IsEliteCombat);
        Assert.Equal(7, elite.Hand.Count);
        Assert.Equal(4, elite.Energy);
        Assert.Equal(3, elite.MaxEnergy);

        var normal = new CombatState();
        CombatFactory.Reset(
            normal,
            new Random(0),
            StarterDeckIds,
            encounterId: 1,
            relicIds: [RelicEffects.BoomingConch],
            playerHp: 64,
            playerMaxHp: 80
        );

        Assert.False(normal.IsEliteCombat);
        Assert.Equal(5, normal.Hand.Count);
        Assert.Equal(3, normal.Energy);
    }

    [Fact]
    public void HappyFlower_GivesEnergyEveryThirdPlayerTurn()
    {
        var state = new CombatState();
        CombatFactory.Reset(
            state,
            new Random(0),
            StarterDeckIds,
            encounterId: 1,
            relicIds: [RelicEffects.HappyFlower],
            playerHp: 64,
            playerMaxHp: 80
        );

        Assert.Equal(1, state.Relics.Single().Counter);
        Assert.Equal(3, state.Energy);

        state.Energy = 3;
        RelicEffects.ApplyStartOfPlayerTurn(state);

        Assert.Equal(2, state.Relics.Single().Counter);
        Assert.Equal(3, state.Energy);

        RelicEffects.ApplyStartOfPlayerTurn(state);

        Assert.Equal(0, state.Relics.Single().Counter);
        Assert.Equal(4, state.Energy);
    }

    [Fact]
    public void FirstTurnRelics_ApplyLanternEnergyAndBagOfMarblesVulnerable()
    {
        var state = new CombatState();
        CombatFactory.Reset(
            state,
            new Random(0),
            StarterDeckIds,
            encounterId: 2,
            relicIds: [RelicEffects.Lantern, RelicEffects.BagOfMarbles],
            playerHp: 64,
            playerMaxHp: 80
        );

        Assert.Equal(4, state.Energy);
        Assert.All(
            state.Enemies.Where(enemy => enemy.Hp > 0),
            enemy => Assert.Equal(1, BuffSystem.Get(enemy.Buffs, BuffId.Vulnerable))
        );
    }

    [Fact]
    public void VenerableTeaSetActive_GivesTwoEnergyOnFirstTurn()
    {
        var state = new CombatState();
        CombatFactory.Reset(
            state,
            new Random(0),
            StarterDeckIds,
            encounterId: 2,
            relicIds: [RelicEffects.VenerableTeaSetActive],
            playerHp: 64,
            playerMaxHp: 80
        );

        Assert.Equal(5, state.Energy);
    }

    [Fact]
    public void Armaments_GainsBlockAndUpgradesFirstCardInHand()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.Armaments, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
        ];
        state.Energy = 1;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(5, state.PlayerBlock);
        Assert.DoesNotContain(
            state.Hand,
            card => card.DefId == IC.StrikeIronclad && !card.Upgraded
        );
        Assert.Contains(state.Hand, card => card.DefId == IC.StrikeIronclad && card.Upgraded);
        Assert.Contains(state.Hand, card => card.DefId == IC.DefendIronclad && !card.Upgraded);
    }

    [Fact]
    public void Armaments_UpgradedUpgradesAllCardsInHand()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.Armaments, true),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
            new CardInstance(ST.Slimed, false),
        ];
        state.Energy = 1;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(5, state.PlayerBlock);
        Assert.Contains(state.Hand, card => card.DefId == IC.StrikeIronclad && card.Upgraded);
        Assert.Contains(state.Hand, card => card.DefId == IC.DefendIronclad && card.Upgraded);
        Assert.Contains(state.Hand, card => card.DefId == ST.Slimed && !card.Upgraded);
    }

    [Fact]
    public void ExpectAFight_GainsEnergyForAttacksInHand()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.ExpectAFight, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
            new CardInstance(IC.SwordBoomerang, false),
        ];
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

        Assert.Equal(2, state.Energy);
    }

    [Fact]
    public void Juggling_CopiesThirdAttackPlayedEachTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.Juggling, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
        ];
        state.Energy = 4;
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
        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.Juggling));
        Assert.Single(state.Hand);
        Assert.Equal(IC.StrikeIronclad, state.Hand[0].DefId);
    }

    [Fact]
    public void Restlessness_DrawsAndGainsEnergyWhenOnlyCardInHand()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Restlessness, false)];
        state.DrawPile =
        [
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
        ];
        state.Energy = 0;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(2, state.Energy);
        Assert.Equal([IC.StrikeIronclad, IC.DefendIronclad], state.Hand.Select(card => card.DefId));
    }

    [Fact]
    public void DrumOfBattle_DrawsAndGainsEnergyWhenExhausted()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.DrumOfBattle, false)];
        state.DrawPile =
        [
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
        ];
        state.Energy = 1;

        CombatEngine.Step(state, 0, new Random(0));

        // Playing it only draws — DrumOfBattle declares no Exhaust keyword, and its
        // OnPlay just draws. The energy comes from AfterCardExhausted, which fires
        // when the card itself is exhausted by something else.
        Assert.Equal(0, state.Energy);
        Assert.Equal([IC.StrikeIronclad, IC.DefendIronclad], state.Hand.Select(card => card.DefId));
        Assert.DoesNotContain(state.ExhaustPile, card => card.DefId == IC.DrumOfBattle);
        Assert.Contains(state.DiscardPile, card => card.DefId == IC.DrumOfBattle);

        CardEffects.ExhaustCard(state, new CardInstance(IC.DrumOfBattle, false));

        Assert.Equal(2, state.Energy);
    }

    [Fact]
    public void DrumOfBattle_GainsEnergyWhenExhaustedByAnotherCard()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.TrueGrit, false),
            new CardInstance(IC.DrumOfBattle, true),
        ];
        state.Energy = 1;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(3, state.Energy);
        Assert.Equal(7, state.PlayerBlock);
        Assert.Contains(state.ExhaustPile, card => card.DefId == IC.DrumOfBattle);
        Assert.DoesNotContain(state.Hand, card => card.DefId == IC.DrumOfBattle);
    }

    [Fact]
    public void FightMe_HitsTwiceAndAppliesStrengthToBothSides()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.FightMe, false)];
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

        Assert.Equal(90, state.Enemies[0].Hp);
        Assert.Equal(3, BuffSystem.Get(state.PlayerBuffs, BuffId.Strength));
        Assert.Equal(1, BuffSystem.Get(state.Enemies[0].Buffs, BuffId.Strength));
    }

    [Fact]
    public void MoltenFist_DamagesAndDuplicatesTargetVulnerable()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.MoltenFist, false)];
        state.Energy = 1;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 100,
                MaxHp = 100,
                Buffs = [new BuffState(BuffId.Vulnerable, 2)],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(85, state.Enemies[0].Hp);
        Assert.Equal(4, BuffSystem.Get(state.Enemies[0].Buffs, BuffId.Vulnerable));
        Assert.Contains(state.ExhaustPile, card => card.DefId == IC.MoltenFist);
    }

    [Fact]
    public void MoltenFist_UpgradedUsesUpgradedDamageAndTriggersVicious()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.MoltenFist, true)];
        state.DrawPile = [new CardInstance(IC.StrikeIronclad, false)];
        state.Energy = 1;
        state.PlayerBuffs = [new BuffState(BuffId.Vicious, 1)];
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 100,
                MaxHp = 100,
                Buffs = [new BuffState(BuffId.Vulnerable, 1)],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(79, state.Enemies[0].Hp);
        Assert.Equal(2, BuffSystem.Get(state.Enemies[0].Buffs, BuffId.Vulnerable));
        Assert.Equal([IC.StrikeIronclad], state.Hand.Select(card => card.DefId));
    }

    [Fact]
    public void IronWave_GainsBlockBeforeDealingDamage()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.IronWave, false)];
        state.Energy = 1;
        state.PlayerBuffs = [new BuffState(BuffId.Juggernaut, 5)];
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 30,
                MaxHp = 30,
                Block = 5,
                Buffs = [new BuffState(BuffId.Vulnerable, 1)],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(5, state.PlayerBlock);
        Assert.Equal(23, state.Enemies[0].Hp);
        Assert.Equal(0, state.Enemies[0].Block);
    }

    [Fact]
    public void IronWave_UpgradedUsesUpgradedBlockAndDamage()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.IronWave, true)];
        state.Energy = 1;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 30,
                MaxHp = 30,
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(7, state.PlayerBlock);
        Assert.Equal(23, state.Enemies[0].Hp);
    }

    [Fact]
    public void Pillage_DamagesAndDrawsUntilNonAttack()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Pillage, false)];
        state.DrawPile =
        [
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.Bash, false),
            new CardInstance(IC.DefendIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
        ];
        state.Energy = 1;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 50,
                MaxHp = 50,
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(44, state.Enemies[0].Hp);
        Assert.Equal(
            [IC.StrikeIronclad, IC.Bash, IC.DefendIronclad],
            state.Hand.Select(card => card.DefId)
        );
        Assert.Equal([IC.StrikeIronclad], state.DrawPile.Select(card => card.DefId));
    }

    [Fact]
    public void Pillage_UpgradedUsesUpgradedDamageAndStopsAtFullHand()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.Pillage, true),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
        ];
        state.DrawPile =
        [
            new CardInstance(IC.Bash, false),
            new CardInstance(IC.DefendIronclad, false),
        ];
        state.Energy = 1;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 50,
                MaxHp = 50,
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(41, state.Enemies[0].Hp);
        Assert.Equal(10, state.Hand.Count);
        Assert.Equal([IC.DefendIronclad], state.DrawPile.Select(card => card.DefId));
    }

    [Fact]
    public void Breakthrough_LosesHpAndDamagesAllEnemies()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerHp = 50;
        state.Hand = [new CardInstance(IC.Breakthrough, false)];
        state.Energy = 1;
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
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(49, state.PlayerHp);
        Assert.Equal([21, 21], state.Enemies.Select(enemy => enemy.Hp));
    }

    [Fact]
    public void Breakthrough_UpgradedUsesUpgradedAllEnemyDamage()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerHp = 50;
        state.Hand = [new CardInstance(IC.Breakthrough, true)];
        state.Energy = 1;
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
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(49, state.PlayerHp);
        Assert.Equal([17, 17], state.Enemies.Select(enemy => enemy.Hp));
    }

    [Fact]
    public void DramaticEntrance_DamagesAllEnemiesAndExhausts()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(CL.DramaticEntrance, false)];
        state.Energy = 0;
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
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal([19, 19], state.Enemies.Select(enemy => enemy.Hp));
        Assert.Empty(state.Hand);
        Assert.Contains(state.ExhaustPile, card => card.DefId == CL.DramaticEntrance);
    }

    [Fact]
    public void DramaticEntrance_UpgradedUsesUpgradedAllEnemyDamage()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(CL.DramaticEntrance, true)];
        state.Energy = 0;
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
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal([15, 15], state.Enemies.Select(enemy => enemy.Hp));
    }

    [Fact]
    public void Omnislice_SplashesEffectiveFirstHitDamageToOtherEnemies()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(CL.Omnislice, false)];
        state.Energy = 0;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 5,
                MaxHp = 5,
                Buffs = [],
            },
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
                Block = 3,
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal([0, 22, 25], state.Enemies.Select(enemy => enemy.Hp));
        Assert.Empty(state.Hand);
        Assert.Contains(state.DiscardPile, card => card.DefId == CL.Omnislice);
    }

    [Fact]
    public void Omnislice_UpgradedSplashIsUnpowered()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(CL.Omnislice, true)];
        state.Energy = 0;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 50,
                MaxHp = 50,
                Buffs = [new BuffState(BuffId.Vulnerable, 1)],
            },
            new EnemyState
            {
                DefId = 16,
                Hp = 50,
                MaxHp = 50,
                Buffs = [new BuffState(BuffId.Vulnerable, 1)],
            },
        ];
        BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, 2);

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal([31, 31], state.Enemies.Select(enemy => enemy.Hp));
    }

    [Fact]
    public void Volley_SpendsAllEnergyForRepeatedRandomEnemyHits()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(CL.Volley, false)];
        state.Energy = 3;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 40,
                MaxHp = 40,
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(0, state.Energy);
        Assert.Equal(10, state.Enemies[0].Hp);
    }

    [Fact]
    public void Volley_UpgradedUsesUpgradedDamagePerEnergy()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(CL.Volley, true)];
        state.Energy = 2;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 40,
                MaxHp = 40,
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(12, state.Enemies[0].Hp);
    }

    [Fact]
    public void Salvo_DamagesTargetAndRetainsRemainingHand()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(CL.Salvo, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
        ];
        state.DrawPile =
        [
            new CardInstance(IC.Bash, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
        ];
        state.DiscardPile.Clear();
        state.Energy = 1;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 40,
                MaxHp = 40,
                CurrentIntent = new Intent(IntentType.Defend, 0),
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(28, state.Enemies[0].Hp);
        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.RetainHand));
        Assert.Equal([IC.StrikeIronclad, IC.DefendIronclad], state.Hand.Select(card => card.DefId));
        Assert.Contains(state.DiscardPile, card => card.DefId == CL.Salvo);

        CombatEngine.Step(state, state.Hand.Count, new Random(0));

        Assert.Equal(0, BuffSystem.Get(state.PlayerBuffs, BuffId.RetainHand));
        Assert.Equal(
            [IC.StrikeIronclad, IC.DefendIronclad],
            state.Hand.Take(2).Select(card => card.DefId)
        );
        Assert.DoesNotContain(
            state.DiscardPile,
            card => card.DefId is IC.StrikeIronclad or IC.DefendIronclad
        );
    }

    [Fact]
    public void Salvo_UpgradedUsesUpgradedDamage()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(CL.Salvo, true)];
        state.Energy = 1;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 40,
                MaxHp = 40,
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(24, state.Enemies[0].Hp);
    }

    [Fact]
    public void NeowsFury_DamagesTargetMovesDiscardCardsToHandAndExhausts()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(AN.NeowsFury, false)];
        state.DiscardPile =
        [
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
            new CardInstance(IC.Bash, false),
        ];
        state.Energy = 1;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 40,
                MaxHp = 40,
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(30, state.Enemies[0].Hp);
        Assert.Equal([IC.StrikeIronclad, IC.DefendIronclad], state.Hand.Select(card => card.DefId));
        Assert.Equal([IC.Bash], state.DiscardPile.Select(card => card.DefId));
        Assert.Contains(state.ExhaustPile, card => card.DefId == AN.NeowsFury);
    }

    [Fact]
    public void NeowsFury_UpgradedMovesThreeCardsAndRespectsHandCap()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(AN.NeowsFury, true),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
        ];
        state.DiscardPile =
        [
            new CardInstance(IC.DefendIronclad, false),
            new CardInstance(IC.Bash, false),
        ];
        state.Energy = 1;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 40,
                MaxHp = 40,
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(26, state.Enemies[0].Hp);
        Assert.Equal(10, state.Hand.Count);
        Assert.Equal(
            [
                IC.StrikeIronclad,
                IC.StrikeIronclad,
                IC.StrikeIronclad,
                IC.StrikeIronclad,
                IC.StrikeIronclad,
                IC.StrikeIronclad,
                IC.StrikeIronclad,
                IC.StrikeIronclad,
                IC.StrikeIronclad,
                IC.DefendIronclad,
            ],
            state.Hand.Select(card => card.DefId)
        );
        Assert.Equal([IC.Bash], state.DiscardPile.Select(card => card.DefId));
    }

    [Fact]
    public void Bolas_DamagesTargetAndReturnsBeforeNextDraw()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(CL.Bolas, false)];
        state.DrawPile =
        [
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
        ];
        state.DiscardPile.Clear();
        state.Energy = 0;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 20,
                MaxHp = 20,
                CurrentIntent = new Intent(IntentType.Defend, 0),
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(17, state.Enemies[0].Hp);
        Assert.Contains(state.DiscardPile, card => card.DefId == CL.Bolas);
        Assert.Single(state.ReturnToHandBeforeDraw);

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(CL.Bolas, state.Hand[0].DefId);
        Assert.Equal(6, state.Hand.Count);
        Assert.DoesNotContain(state.DiscardPile, card => card.DefId == CL.Bolas);
        Assert.Empty(state.ReturnToHandBeforeDraw);
    }

    [Fact]
    public void Bolas_UpgradedUsesUpgradedDamageAndReturnsUpgraded()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(CL.Bolas, true)];
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.Energy = 0;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 20,
                MaxHp = 20,
                CurrentIntent = new Intent(IntentType.Defend, 0),
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(16, state.Enemies[0].Hp);
        Assert.Single(state.Hand);
        Assert.Equal(CL.Bolas, state.Hand[0].DefId);
        Assert.True(state.Hand[0].Upgraded);
    }

    [Fact]
    public void Bolas_ReturnsOnceWhenAttackEffectIsDuplicated()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.OneTwoPunch, false), new CardInstance(CL.Bolas, false)];
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.Energy = 1;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 20,
                MaxHp = 20,
                CurrentIntent = new Intent(IntentType.Defend, 0),
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(14, state.Enemies[0].Hp);
        Assert.Single(state.ReturnToHandBeforeDraw);

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(1, state.Hand.Count(card => card.DefId == CL.Bolas));
        Assert.Empty(state.ReturnToHandBeforeDraw);
    }

    [Fact]
    public void Cinder_DamagesTargetAndExhaustsRandomCardFromHand()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.Cinder, false),
            new CardInstance(IC.DefendIronclad, false),
        ];
        state.Energy = 2;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 50,
                MaxHp = 50,
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(32, state.Enemies[0].Hp);
        Assert.Empty(state.Hand);
        // Cinder exhausts a random card from hand, not itself — it declares no
        // Exhaust keyword, so it discards like any other attack.
        Assert.Contains(state.ExhaustPile, card => card.DefId == IC.DefendIronclad);
        Assert.DoesNotContain(state.ExhaustPile, card => card.DefId == IC.Cinder);
        Assert.Contains(state.DiscardPile, card => card.DefId == IC.Cinder);
    }

    [Fact]
    public void Cinder_UpgradedUsesUpgradedDamage()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Cinder, true)];
        state.Energy = 2;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 50,
                MaxHp = 50,
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(26, state.Enemies[0].Hp);
        Assert.Contains(state.DiscardPile, card => card.DefId == IC.Cinder);
    }

    [Fact]
    public void Stomp_DamagesAllEnemies()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Stomp, false)];
        state.Energy = 3;
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
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal([18, 18], state.Enemies.Select(enemy => enemy.Hp));
    }

    [Fact]
    public void Stomp_UpgradedUsesUpgradedAllEnemyDamage()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Stomp, true)];
        state.Energy = 3;
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
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal([15, 15], state.Enemies.Select(enemy => enemy.Hp));
    }

    [Fact]
    public void Stomp_CostIsReducedByAttacksPlayedThisTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.Stomp, false),
        ];
        state.Energy = 3;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 100,
                MaxHp = 100,
                Buffs = [],
            },
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

        Assert.Equal(1, state.Energy);
        Assert.Contains(0, CombatEngine.ValidActions(state));

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(76, state.Enemies[0].Hp);
        Assert.Equal(88, state.Enemies[1].Hp);
    }

    [Fact]
    public void Havoc_PlaysAndExhaustsTopDrawPileCard()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Havoc, false)];
        state.DrawPile = [new CardInstance(IC.DefendIronclad, false)];
        state.Energy = 1;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(5, state.PlayerBlock);
        Assert.Contains(state.DiscardPile, card => card.DefId == IC.Havoc);
        Assert.Contains(state.ExhaustPile, card => card.DefId == IC.DefendIronclad);
    }

    [Fact]
    public void Splash_AddsGeneratedAttackToHand()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Splash, false)];
        state.Energy = 1;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Single(state.Hand);
        Assert.Equal(IC.StrikeIronclad, state.Hand[0].DefId);
    }

    [Fact]
    public void InfernalBlade_AddsRandomAttackFreeThisTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.InfernalBlade, false)];
        state.Energy = 1;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Single(state.Hand);
        Assert.Equal(IC.AshenStrike, state.Hand[0].DefId);
        Assert.True(state.Hand[0].FreeThisTurn);
        Assert.Contains(state.ExhaustPile, card => card.DefId == IC.InfernalBlade);
    }

    [Fact]
    public void InfernalBlade_GeneratedAttackCanBePlayedWithoutEnergy()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.InfernalBlade, false)];
        state.Energy = 1;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 50,
                MaxHp = 50,
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));
        state.Energy = 0;

        Assert.Contains(0, CombatEngine.ValidActions(state));

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(41, state.Enemies[0].Hp);
        // AshenStrike mentions Exhaust only in a hover tip; it declares no Exhaust
        // keyword and its OnPlay just deals damage, so it discards.
        Assert.Contains(
            state.DiscardPile,
            card => card.DefId == IC.AshenStrike && !card.FreeThisTurn
        );
    }

    [Fact]
    public void InfernalBlade_UpgradedCostsZero()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.InfernalBlade, true)];
        state.Energy = 0;

        Assert.Contains(0, CombatEngine.ValidActions(state));

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Single(state.Hand);
        Assert.Equal(IC.AshenStrike, state.Hand[0].DefId);
    }

    [Fact]
    public void Stampede_AppliesTrackedPower()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Stampede, false)];
        state.Energy = 2;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.Stampede));
    }

    [Fact]
    public void Stampede_AutoPlaysAttackFromRemainingHandAtEndOfTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.Stampede, false),
            new CardInstance(IC.HowlFromBeyond, false),
            new CardInstance(IC.DefendIronclad, false),
        ];
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.Energy = 2;
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

        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 2, new Random(0));

        Assert.Equal(14, state.Enemies[0].Hp);
    }

    [Fact]
    public void Stampede_RepeatsForStackCountAndSkipsUnplayableCards()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.AscendersBane, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
        ];
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.PlayerBuffs = [new BuffState(BuffId.Stampede, 2)];
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 50,
                MaxHp = 50,
                CurrentIntent = new Intent(IntentType.Defend, 0),
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, state.Hand.Count, new Random(0));

        Assert.Equal(38, state.Enemies[0].Hp);
    }

    [Fact]
    public void Vicious_DrawsWhenPlayerAppliesVulnerable()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Vicious, false), new CardInstance(IC.Taunt, false)];
        state.DrawPile =
        [
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
        ];
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

        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.Vicious));
        Assert.Equal(1, BuffSystem.Get(state.Enemies[0].Buffs, BuffId.Vulnerable));
        Assert.Contains(state.Hand, card => card.DefId == IC.StrikeIronclad);
    }

    [Fact]
    public void Nostalgia_PutsFirstAttackOrSkillEachTurnOnTopOfDrawPile()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.Nostalgia, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
        ];
        state.DrawPile = [new CardInstance(IC.Bash, false)];
        state.DiscardPile.Clear();
        state.Energy = 3;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 50,
                MaxHp = 50,
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.Nostalgia));
        Assert.Equal(IC.StrikeIronclad, state.DrawPile[0].DefId);
        Assert.DoesNotContain(state.DiscardPile, card => card.DefId == IC.StrikeIronclad);
        Assert.Contains(state.DiscardPile, card => card.DefId == IC.DefendIronclad);
    }

    [Fact]
    public void Nostalgia_UpgradedCostsZeroAndResetsEachTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Nostalgia, true)];
        state.DrawPile = [];
        state.DiscardPile.Clear();
        state.Energy = 0;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 50,
                MaxHp = 50,
                Buffs = [],
            },
        ];

        Assert.Contains(0, CombatEngine.ValidActions(state));
        CombatEngine.Step(state, 0, new Random(0));
        state.Hand = [new CardInstance(IC.StrikeIronclad, false)];
        state.Energy = 1;
        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));
        state.Hand = [new CardInstance(IC.DefendIronclad, false)];
        state.Energy = 1;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(IC.DefendIronclad, state.DrawPile[0].DefId);
    }

    [Fact]
    public void ForgottenRitual_DoesNotGainEnergyWithoutPriorExhaust()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.ForgottenRitual, false)];
        state.Energy = 1;
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

        Assert.Equal(0, state.Energy);
        Assert.Equal(1, state.CardsExhaustedThisTurn);
        Assert.Contains(state.ExhaustPile, card => card.DefId == IC.ForgottenRitual);
    }

    [Fact]
    public void ForgottenRitual_GainsEnergyAfterCardExhaustedThisTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.ForgottenRitual, true)];
        state.Energy = 1;
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
        CardEffects.ExhaustCard(state, new CardInstance(IC.StrikeIronclad, false));

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(4, state.Energy);
        Assert.Equal(2, state.CardsExhaustedThisTurn);
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
    public void EvilEye_GainsBlockOnceWithoutPriorExhaust()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.EvilEye, false)];
        state.Energy = 1;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(8, state.PlayerBlock);
        // EvilEye reads whether a card was exhausted this turn; it does not exhaust
        // itself, so playing it alone leaves the count at zero.
        Assert.Equal(0, state.CardsExhaustedThisTurn);
        Assert.DoesNotContain(state.ExhaustPile, card => card.DefId == IC.EvilEye);
    }

    [Fact]
    public void EvilEye_GainsBlockTwiceAfterCardExhaustedThisTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.EvilEye, true)];
        state.Energy = 1;
        CardEffects.ExhaustCard(state, new CardInstance(IC.StrikeIronclad, false));

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(22, state.PlayerBlock);
        // Only the pre-exhausted Strike counts — EvilEye does not add itself.
        Assert.Equal(1, state.CardsExhaustedThisTurn);
    }

    [Fact]
    public void Prolong_GainsCurrentBlockAfterNextTurnBlockClear()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerBlock = 12;
        state.PlayerBuffs = [new BuffState(BuffId.Dexterity, 3)];
        state.Hand = [new CardInstance(CL.Prolong, false)];
        state.DrawPile.Clear();
        state.Energy = 0;
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

        Assert.Equal(12, BuffSystem.Get(state.PlayerBuffs, BuffId.BlockNextTurn));
        Assert.Contains(state.ExhaustPile, card => card.DefId == CL.Prolong);

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(12, state.PlayerBlock);
        Assert.Equal(0, BuffSystem.Get(state.PlayerBuffs, BuffId.BlockNextTurn));
    }

    [Fact]
    public void Prolong_UpgradedDoesNotExhaust()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerBlock = 4;
        state.Hand = [new CardInstance(CL.Prolong, true)];
        state.Energy = 0;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.DoesNotContain(state.ExhaustPile, card => card.DefId == CL.Prolong);
        Assert.Contains(state.DiscardPile, card => card.DefId == CL.Prolong && card.Upgraded);
        Assert.Equal(4, BuffSystem.Get(state.PlayerBuffs, BuffId.BlockNextTurn));
    }

    [Fact]
    public void OneTwoPunch_DuplicatesNextAttack()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.OneTwoPunch, false),
            new CardInstance(IC.StrikeIronclad, false),
        ];
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

        Assert.Equal(88, state.Enemies[0].Hp);
        Assert.Equal(0, BuffSystem.Get(state.PlayerBuffs, BuffId.OneTwoPunch));
    }

    [Fact]
    public void OneTwoPunch_UpgradedDuplicatesNextTwoAttacks()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.OneTwoPunch, true),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
        ];
        state.Energy = 3;
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
        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(76, state.Enemies[0].Hp);
        Assert.Equal(0, BuffSystem.Get(state.PlayerBuffs, BuffId.OneTwoPunch));
    }

    [Fact]
    public void OneTwoPunch_ExpiresAtEndOfPlayerTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.OneTwoPunch, false)];
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

        Assert.Equal(0, BuffSystem.Get(state.PlayerBuffs, BuffId.OneTwoPunch));
    }

    [Fact]
    public void Colossus_GainsBlockAndHalvesVulnerableEnemyAttackDamage()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerHp = 100;
        state.PlayerMaxHp = 100;
        state.Hand = [new CardInstance(IC.Colossus, false)];
        state.DrawPile.Clear();
        state.Energy = 1;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 100,
                MaxHp = 100,
                CurrentIntent = new Intent(IntentType.Attack, 20),
                Buffs = [new BuffState(BuffId.Vulnerable, 2)],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(5, state.PlayerBlock);
        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.Colossus));

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(95, state.PlayerHp);
        Assert.Equal(0, BuffSystem.Get(state.PlayerBuffs, BuffId.Colossus));
    }

    [Fact]
    public void DarkEmbrace_DrawsCardOnImmediateExhaust()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.DarkEmbrace, false),
            new CardInstance(IC.TrueGrit, false),
        ];
        state.DrawPile =
        [
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
            new CardInstance(IC.IronWave, false),
        ];
        state.Energy = 3;

        // Play Dark Embrace.
        CombatEngine.Step(state, 0, new Random(0));
        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.DarkEmbrace));
        Assert.Single(state.Hand); // True Grit remains after the Power leaves hand.

        // Play True Grit after adding another card for it to exhaust.
        state.Hand.Add(new CardInstance(IC.Bash, false));
        // Hand now has: True Grit, Bash.
        // Action 0 is True Grit.
        CombatEngine.Step(state, 0, new Random(0));

        // True Grit played, exhausts a card, Dark Embrace triggers again.
        // True Grit itself does not exhaust, so it shouldn't trigger another draw.
        Assert.Single(state.Hand);
    }

    [Fact]
    public void DarkEmbrace_DrawsCardAfterTurnEndForEtherealExhaust()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.DarkEmbrace, false),
            new CardInstance(IC.AscendersBane, false),
        ];
        state.DrawPile =
        [
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
            new CardInstance(IC.IronWave, false),
            new CardInstance(IC.Bash, false),
            new CardInstance(IC.Anger, false),
            new CardInstance(IC.BodySlam, false),
            new CardInstance(IC.Break, false),
        ];
        state.Energy = 3;

        // Play Dark Embrace.
        CombatEngine.Step(state, 0, new Random(0));
        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.DarkEmbrace));
        Assert.Single(state.Hand); // Ascender's Bane remains after the Power leaves hand.

        // End turn. Ascender's Bane is Ethereal and should exhaust.
        // Dark Embrace should trigger but the draw should be deferred.
        CombatEngine.Step(state, 1, new Random(0)); // action 1 is End Turn when hand has 1 card

        // After end turn, we should have drawn 5 cards for next turn + 1 card from Dark Embrace.
        Assert.Equal(6, state.Hand.Count);
        Assert.Equal(1, state.ExhaustPile.Count(c => c.DefId == IC.AscendersBane));
    }

    [Fact]
    public void Weakness_FromSludgeSpinner_LastsThroughNextPlayerTurn()
    {
        // SludgeSpinner Move 0 is Oil Spray (9 dmg + 1 Weak).
        var state = CombatFactory.NewCombat(seed: 0);
        state.Enemies =
        [
            CombatFactory.CreateEnemy(
                KE.SludgeSpinner,
                new Random(0),
                new Intent(IntentType.Debuff, 9),
                0
            ),
        ];
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
    public void Aggression_AddsUpgradedCardAtStartOfTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Aggression, false)];
        state.DrawPile = [];
        state.DiscardPile = [];
        state.Energy = 3;

        // Play Aggression.
        CombatEngine.Step(state, 0, new Random(0));
        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.Aggression));
        Assert.Empty(state.Hand);

        // End turn.
        CombatEngine.Step(state, 0, new Random(0));

        // Start of next turn. Should have 5 cards (from draw) + 1 card from Aggression.
        // Wait, draw pile was empty. So it should only have cards from Aggression?
        // No, EndTurn draws 5 cards. If draw/discard empty, it draws 0.
        // So hand should have exactly 1 card.
        Assert.Single(state.Hand);
        Assert.True(state.Hand[0].Upgraded);
    }

    [Fact]
    public void Hellraiser_AutoPlaysDrawnStrike()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.Hellraiser, false),
            new CardInstance(IC.PommelStrike, false),
        ];
        state.DrawPile = [new CardInstance(IC.StrikeIronclad, false)];
        state.DiscardPile = [];
        state.Energy = 3;
        var enemy = state.Enemies[0];
        enemy.Hp = 100;

        // Play Hellraiser.
        CombatEngine.Step(state, 0, new Random(0));
        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.Hellraiser));

        // Play Pommel Strike (draws 1).
        // It should draw StrikeIronclad, which Hellraiser should automatically play.
        // StrikeIronclad deals 6 damage. Pommel Strike deals 9.
        // Total damage should be 15.
        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(85, enemy.Hp);
        // Hand should be empty (Pommel Strike played, StrikeIronclad auto-played).
        Assert.Empty(state.Hand);
        Assert.Contains(state.DiscardPile, c => c.DefId == IC.StrikeIronclad);
    }

    [Fact]
    public void DarkShackles_AppliesTemporaryStrengthLossUntilEnemyTurnEnds()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerHp = 100;
        state.PlayerMaxHp = 100;
        state.Hand = [new CardInstance(CL.DarkShackles, false)];
        state.DrawPile.Clear();
        state.Energy = 0;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 100,
                MaxHp = 100,
                CurrentIntent = new Intent(IntentType.Attack, 20),
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(-9, BuffSystem.Get(state.Enemies[0].Buffs, BuffId.Strength));
        Assert.Equal(9, BuffSystem.Get(state.Enemies[0].Buffs, BuffId.TemporaryStrength));
        Assert.Contains(state.ExhaustPile, card => card.DefId == CL.DarkShackles);

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(89, state.PlayerHp);
        Assert.Equal(0, BuffSystem.Get(state.Enemies[0].Buffs, BuffId.Strength));
        Assert.Equal(0, BuffSystem.Get(state.Enemies[0].Buffs, BuffId.TemporaryStrength));
    }

    [Fact]
    public void DarkShackles_IsPreventedByArtifact()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(CL.DarkShackles, true)];
        state.Energy = 0;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 100,
                MaxHp = 100,
                Buffs = [new BuffState(BuffId.Artifact, 1)],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(0, BuffSystem.Get(state.Enemies[0].Buffs, BuffId.Artifact));
        Assert.Equal(0, BuffSystem.Get(state.Enemies[0].Buffs, BuffId.Strength));
        Assert.Equal(0, BuffSystem.Get(state.Enemies[0].Buffs, BuffId.TemporaryStrength));
    }

    [Fact]
    public void Inferno_TriggersWhenPlayerLosesHpOnPlayerTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerHp = 50;
        state.Hand = [new CardInstance(IC.Inferno, false), new CardInstance(IC.Hemokinesis, false)];
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

        Assert.Equal(48, state.PlayerHp);
        Assert.Equal(6, BuffSystem.Get(state.PlayerBuffs, BuffId.Inferno));
        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.InfernoSelfDamage));
        Assert.Equal(79, state.Enemies[0].Hp);
        Assert.Equal(94, state.Enemies[1].Hp);
    }

    [Fact]
    public void Inferno_DamagesPlayerAndEnemiesAtStartOfPlayerTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerHp = 50;
        state.Hand = [new CardInstance(IC.Inferno, true)];
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
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

        Assert.Equal(49, state.PlayerHp);
        Assert.Equal(9, BuffSystem.Get(state.PlayerBuffs, BuffId.Inferno));
        Assert.Equal(91, state.Enemies[0].Hp);
        Assert.Equal(91, state.Enemies[1].Hp);
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
    public void SetupStrike_AppliesTemporaryStrengthUntilEndOfTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.SetupStrike, false)];
        state.Energy = 1;
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

        Assert.Equal(93, state.Enemies[0].Hp);
        Assert.Equal(2, BuffSystem.Get(state.PlayerBuffs, BuffId.Strength));
        Assert.Equal(2, BuffSystem.Get(state.PlayerBuffs, BuffId.TemporaryStrength));

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(0, BuffSystem.Get(state.PlayerBuffs, BuffId.Strength));
        Assert.Equal(0, BuffSystem.Get(state.PlayerBuffs, BuffId.TemporaryStrength));
    }

    [Fact]
    public void Spite_HitsOnceBeforePlayerLosesHpThisTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Spite, false)];
        state.Energy = 0;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 50,
                MaxHp = 50,
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(45, state.Enemies[0].Hp);
    }

    [Fact]
    public void Spite_HitsTwiceAfterCardHpLossThisTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerHp = 50;
        state.Hand = [new CardInstance(IC.Bloodletting, false), new CardInstance(IC.Spite, false)];
        state.Energy = 0;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 50,
                MaxHp = 50,
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(47, state.PlayerHp);
        Assert.Equal(40, state.Enemies[0].Hp);
    }

    [Fact]
    public void Spite_UpgradedHitsThreeTimesAfterHpLossThisTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerHp = 50;
        state.Hand = [new CardInstance(IC.Breakthrough, false), new CardInstance(IC.Spite, true)];
        state.Energy = 1;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 50,
                MaxHp = 50,
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(49, state.PlayerHp);
        Assert.Equal(26, state.Enemies[0].Hp);
    }

    [Fact]
    public void Spite_HpLossConditionResetsOnNextPlayerTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerHp = 50;
        state.Hand = [new CardInstance(IC.Bloodletting, false)];
        state.DrawPile = [new CardInstance(IC.Spite, false)];
        state.DiscardPile.Clear();
        state.Energy = 0;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 50,
                MaxHp = 50,
                CurrentIntent = new Intent(IntentType.Defend, 0),
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(45, state.Enemies[0].Hp);
    }

    [Fact]
    public void TurnBlockRelics_ApplyHornCleatAndCaptainsWheel()
    {
        var state = new CombatState
        {
            Relics =
            [
                new RelicInstance(RelicEffects.HornCleat),
                new RelicInstance(RelicEffects.CaptainsWheel),
            ],
        };

        state.Turn = 1;
        RelicEffects.ApplyStartOfPlayerTurn(state);
        Assert.Equal(14, state.PlayerBlock);

        state.PlayerBlock = 0;
        state.Turn = 2;
        RelicEffects.ApplyStartOfPlayerTurn(state);
        Assert.Equal(18, state.PlayerBlock);
    }

    [Fact]
    public void RedSkull_TracksLowHpStrength()
    {
        var state = new CombatState();
        CombatFactory.Reset(
            state,
            new Random(0),
            StarterDeckIds,
            encounterId: 2,
            relicIds: [RelicEffects.RedSkull],
            playerHp: 40,
            playerMaxHp: 80
        );

        Assert.Equal(3, BuffSystem.Get(state.PlayerBuffs, BuffId.Strength));
        Assert.Equal(1, state.Relics.Single().Counter);

        state.PlayerHp = 41;
        RelicEffects.ApplyAfterPlayerHpChanged(state);

        Assert.Equal(0, BuffSystem.Get(state.PlayerBuffs, BuffId.Strength));
        Assert.Equal(0, state.Relics.Single().Counter);
    }

    [Fact]
    public void Orichalcum_GainsBlockWhenEndingTurnWithoutBlock()
    {
        var state = new CombatState();
        CombatFactory.Reset(
            state,
            new Random(0),
            StarterDeckIds,
            encounterId: 1,
            relicIds: [RelicEffects.Orichalcum],
            playerHp: 64,
            playerMaxHp: 80
        );
        state.Hand.Clear();
        state.DrawPile.Clear();
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 20,
                MaxHp = 20,
                CurrentIntent = new Intent(IntentType.Attack, 1),
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(64, state.PlayerHp);
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
    public void ForcedCultists_MatchesDecompiledOpeningAndRitual()
    {
        var state = new CombatState();
        CombatFactory.Reset(state, new Random(0), StarterDeckIds, encounterId: 0);

        Assert.Equal(0, state.EncounterId);
        Assert.Collection(
            state.Enemies,
            enemy =>
            {
                Assert.Equal(14, enemy.DefId);
                Assert.Equal(IntentType.Buff, enemy.CurrentIntent.Type);
                EnemyAI.ExecuteIntent(enemy, state, new Random(0));
                Assert.Equal(2, BuffSystem.Get(enemy.Buffs, BuffId.Ritual));
                Assert.Equal(0, BuffSystem.Get(enemy.Buffs, BuffId.Strength));
            },
            enemy =>
            {
                Assert.Equal(21, enemy.DefId);
                Assert.Equal(IntentType.Buff, enemy.CurrentIntent.Type);
                EnemyAI.ExecuteIntent(enemy, state, new Random(0));
                Assert.Equal(6, BuffSystem.Get(enemy.Buffs, BuffId.Ritual));
                Assert.Equal(0, BuffSystem.Get(enemy.Buffs, BuffId.Strength));
            }
        );
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
    public void Toadpole_SpikeSpitConsumesThornsBeforeAttacking()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerBlock = 0;
        state.PlayerHp = 64;
        var enemy = new EnemyState
        {
            DefId = 93,
            Hp = 22,
            MaxHp = 22,
            CurrentIntent = new Intent(IntentType.Attack, 12),
            Buffs = [new BuffState(BuffId.Thorns, 2)],
            MoveIndex = 1,
        };

        EnemyAI.ExecuteIntent(enemy, state, new Random(0));

        Assert.Equal(0, BuffSystem.Get(enemy.Buffs, BuffId.Thorns));
        // SpikeSpitDamage is 3 at A8 (4 only at DeadlyEnemies) x 3 hits = 9. Confirmed
        // against a live 4-turn capture, where the emulator dealt 12 to the game's 9.
        Assert.Equal(55, state.PlayerHp);
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
        Assert.Equal(6, state.Enemies[1].CurrentIntent.Magnitude);
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

    [Fact]
    public void TwoTailedRat_CallForBackupSummonsRatAndTracksLimit()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 101,
                Hp = 20,
                MaxHp = 20,
                CurrentIntent = new Intent(IntentType.Buff, 0),
                Buffs = [],
            },
        ];

        EnemyAI.ExecuteIntent(state.Enemies[0], state, new Random(0));

        Assert.Equal(2, state.Enemies.Count(e => e.DefId == 101));
        Assert.All(
            state.Enemies.Where(e => e.DefId == 101),
            rat => Assert.Equal(1, BuffSystem.Get(rat.Buffs, BuffId.BackupCount))
        );
        Assert.Contains(
            state.Enemies,
            e => e.DefId == 101 && BuffSystem.Get(e.Buffs, BuffId.Stunned) == 1
        );
    }

    [Fact]
    public void TwoTailedRat_CallForBackupRespectsTotalSlotLimit()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Enemies = Enumerable
            .Range(0, 6)
            .Select(i => new EnemyState
            {
                DefId = 101,
                Hp = i == 0 ? 20 : 0,
                MaxHp = 20,
                CurrentIntent = new Intent(IntentType.Buff, 0),
                Buffs = [],
            })
            .ToList();

        EnemyAI.ExecuteIntent(state.Enemies[0], state, new Random(0));

        Assert.Equal(6, state.Enemies.Count(e => e.DefId == 101));
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
    public void SkulkingColony_MatchesDecompiledMoveCycle()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerHp = 100;
        state.PlayerBlock = 999;
        var colony = new EnemyState
        {
            DefId = KE.SkulkingColony,
            Hp = 80,
            MaxHp = 80,
            CurrentIntent = new Intent(IntentType.Attack, 16),
            Buffs = [new BuffState(BuffId.HardenedShell, 20)],
        };

        EnemyAI.ExecuteIntent(colony, state, new Random(0));
        EnemyAI.ChooseIntents([colony], 0, new Random(0));
        EnemyAI.ExecuteIntent(colony, state, new Random(0));
        EnemyAI.ChooseIntents([colony], 0, new Random(0));
        EnemyAI.ExecuteIntent(colony, state, new Random(0));
        EnemyAI.ChooseIntents([colony], 0, new Random(0));
        EnemyAI.ExecuteIntent(colony, state, new Random(0));

        // Was 934. PiercingStabs is 7 per hit at A8 (8 only at DeadlyEnemies) and the
        // cycle lands on it once, so two hits cost 1 less each.
        // NOTE: this enemy's Inertia is still pinned at its A9 value (11; A8 is 9), like
        // every elite the combat sweep does not reach yet — see HANDOFF.
        Assert.Equal(936, state.PlayerBlock);
        Assert.Equal(3, BuffSystem.Get(colony.Buffs, BuffId.Strength));
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
            CurrentIntent = new Intent(IntentType.Attack, 12),
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
            CurrentIntent = new Intent(IntentType.Attack, 20),
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
    public void StoneCracker_UpgradesTwoCardsBeforeOpeningDraw()
    {
        var state = new CombatState();
        var deck = new CardInstance[]
        {
            new(IC.StrikeIronclad, false),
            new(IC.DefendIronclad, false),
            new(IC.Bash, false),
            new(IC.AscendersBane, false),
        };

        CombatFactory.Reset(
            state,
            new Random(0),
            deck,
            encounterId: 2,
            relicIds: [RelicEffects.StoneCracker],
            playerHp: 80,
            playerMaxHp: 80,
            potionIds: [],
            playerGold: 0,
            deckPreShuffled: true
        );

        int upgradedCount = state.Hand.Concat(state.DrawPile).Count(card => card.Upgraded);
        Assert.Equal(2, upgradedCount);
        Assert.DoesNotContain(
            state.Hand.Concat(state.DrawPile),
            card => card.DefId == IC.AscendersBane && card.Upgraded
        );
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

    [Fact]
    public void SewerClam_PlatingAddsBlockAndDecays()
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
        Assert.Equal(8, BuffSystem.Get(enemy.Buffs, BuffId.Plating));
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
    public void VineShambler_GraspingVinesAppliesTangled()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerBlock = 0;
        var enemy = new EnemyState
        {
            DefId = 103,
            Hp = 65,
            MaxHp = 65,
            CurrentIntent = new Intent(IntentType.Debuff, 9),
            Buffs = [],
        };

        EnemyAI.ExecuteIntent(enemy, state, new Random(0));

        Assert.Equal(55, state.PlayerHp);
        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.Tangled));
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
        Assert.Contains(
            state.Enemies,
            e => e.DefId == 26 && BuffSystem.Get(e.Buffs, BuffId.Stunned) == 1
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
    public void LivingFog_BloatSummonsGasBombAndAttacks()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var enemy = new EnemyState
        {
            DefId = 49,
            Hp = 82,
            MaxHp = 82,
            CurrentIntent = new Intent(IntentType.Buff, 6),
            Buffs = [],
            MoveIndex = 1,
        };

        EnemyAI.ExecuteIntent(enemy, state, new Random(0));

        Assert.Equal(58, state.PlayerHp);
        Assert.Contains(
            state.Enemies,
            e => e.DefId == 35 && BuffSystem.Get(e.Buffs, BuffId.Minion) == 1
        );
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
    public void Dazed_ExhaustsAtEndOfTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand.Clear();
        state.Hand.Add(new CardInstance(ST.Dazed, false));

        CombatEngine.Step(state, 1, new Random(0));

        Assert.DoesNotContain(state.Hand, c => c.DefId == ST.Dazed);
        Assert.Contains(state.ExhaustPile, c => c.DefId == ST.Dazed);
        Assert.DoesNotContain(state.DiscardPile, c => c.DefId == ST.Dazed);
    }

    [Fact]
    public void Slimed_DrawsOneAndExhaustsWhenPlayed()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand.Clear();
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.ExhaustPile.Clear();
        state.Hand.Add(new CardInstance(ST.Slimed, false));
        state.DrawPile.Add(new CardInstance(IC.StrikeIronclad, false));
        state.Energy = 1;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Contains(state.Hand, c => c.DefId == IC.StrikeIronclad);
        Assert.Contains(state.ExhaustPile, c => c.DefId == ST.Slimed);
    }

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
    public void Bash_AppliesVulnerableToEnemy()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.Bash, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng);

        Assert.True(BuffSystem.Get(state.Enemies[0].Buffs, BuffId.Vulnerable) > 0);
    }

    [Fact]
    public void Inflame_GrantsStrengthToPlayer()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.Inflame, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng);

        Assert.Equal(2, BuffSystem.Get(state.PlayerBuffs, BuffId.Strength));
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
    public void Barricade_BlockPersistsAcrossTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);

        // Give player Barricade and some block
        BuffSystem.Apply(state.PlayerBuffs, BuffId.Barricade, 1);
        state.PlayerBlock = 15;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 56,
                Hp = 1,
                MaxHp = 1,
                CurrentIntent = new Intent(IntentType.Defend, 0),
            },
        ];

        // End turn (don't play anything)
        state.Hand.Clear();
        CombatEngine.Step(state, 0, rng); // 0 = end turn when hand is empty

        // Block should NOT have been reset to 0
        Assert.True(state.PlayerBlock > 0);
    }

    [Fact]
    public void DemonForm_GrantsStrengthEachTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);

        // Apply DemonForm (2 Strength per turn)
        BuffSystem.Apply(state.PlayerBuffs, BuffId.DemonForm, 2);

        // End turn
        state.Hand.Clear();
        CombatEngine.Step(state, 0, rng);

        // Player should have gained 2 Strength
        Assert.Equal(2, BuffSystem.Get(state.PlayerBuffs, BuffId.Strength));
    }

    [Fact]
    public void TwinStrike_HitsTwice()
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

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.TwinStrike, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng);

        // TwinStrike deals 5×2 = 10 damage (no buffs)
        Assert.Equal(enemyHp - 10, state.Enemies[0].Hp);
    }

    [Fact]
    public void Corruption_MakesSkillsFree()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);

        BuffSystem.Apply(state.PlayerBuffs, BuffId.Corruption, 1);
        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.DefendIronclad, false)); // Skill, cost 1
        state.Energy = 0; // not enough energy normally

        var actions = CombatEngine.ValidActions(state);
        Assert.Contains(0, actions); // card 0 should be playable despite 0 energy
    }

    [Fact]
    public void StoneArmor_AppliesPlatingAndDecaysEachTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.StoneArmor, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng); // play StoneArmor (base: 4 Plating)
        Assert.Equal(4, BuffSystem.Get(state.PlayerBuffs, BuffId.Plating));
        Assert.Equal(0, state.PlayerBlock); // block not yet gained (end of turn pending)

        // end_turn → end-of-turn: gain 4 block → enemy turn → start-of-turn: plating decrements to 3
        CombatEngine.Step(state, state.Hand.Count, rng);
        Assert.Equal(3, BuffSystem.Get(state.PlayerBuffs, BuffId.Plating));
    }

    [Fact]
    public void StoneArmor_Upgraded_Applies6Plating()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.StoneArmor, true));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng); // play upgraded StoneArmor
        Assert.Equal(6, BuffSystem.Get(state.PlayerBuffs, BuffId.Plating));
    }

    [Fact]
    public void Break_DealsBaseAndAppliesVulnerable5()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);
        var enemy = state.Enemies[0];
        int hpBefore = enemy.Hp;

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.Break, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng);

        Assert.True(enemy.Hp < hpBefore); // took damage
        Assert.Equal(5, BuffSystem.Get(enemy.Buffs, BuffId.Vulnerable));
    }

    [Fact]
    public void Break_Upgraded_AppliesVulnerable7()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);
        var enemy = state.Enemies[0];
        enemy.Hp = 100;

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.Break, true));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng);
        Assert.Equal(7, BuffSystem.Get(enemy.Buffs, BuffId.Vulnerable));
    }

    [Fact]
    public void Bludgeon_Deals32Damage()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);
        var enemy = state.Enemies[0];
        enemy.Hp = 100;
        int hpBefore = enemy.Hp;

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.Bludgeon, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng);
        Assert.Equal(hpBefore - 32, enemy.Hp);
    }

    [Fact]
    public void UltimateDefend_GainsBlock()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.UltimateDefend, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng);
        Assert.Equal(11, state.PlayerBlock);
    }

    [Fact]
    public void Impervious_Gains30BlockAndExhausts()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.Impervious, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng);
        Assert.Equal(30, state.PlayerBlock);
        var exhaustedCard = Assert.Single(state.ExhaustPile);
        Assert.Equal(IC.Impervious, exhaustedCard.DefId);
    }

    [Fact]
    public void Feed_KillsEnemyAndGrantsMaxHp()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);
        var enemy = state.Enemies[0];
        enemy.Hp = 1; // set enemy to 1 HP so Feed kills it
        int maxHpBefore = state.PlayerMaxHp;

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.Feed, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng);
        Assert.Equal(0, enemy.Hp);
        Assert.Equal(maxHpBefore + 3, state.PlayerMaxHp);
    }

    [Fact]
    public void Feed_NoKillNoMaxHpGain()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);
        var enemy = state.Enemies[0];
        enemy.Hp = 100; // enemy survives
        int maxHpBefore = state.PlayerMaxHp;

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.Feed, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng);
        Assert.Equal(maxHpBefore, state.PlayerMaxHp);
    }

    [Fact]
    public void Mangle_DamagesAndAppliesTempStrength()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);
        var enemy = state.Enemies[0];
        int hpBefore = enemy.Hp;

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.Mangle, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng);
        Assert.True(enemy.Hp < hpBefore);
        Assert.Equal(-10, BuffSystem.Get(enemy.Buffs, BuffId.Strength));
        Assert.Equal(10, BuffSystem.Get(enemy.Buffs, BuffId.TemporaryStrength));
    }

    [Fact]
    public void Unrelenting_DamagesAndGrantsFreeAttackPower()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);
        var enemy = state.Enemies[0];
        int hpBefore = enemy.Hp;

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.Unrelenting, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng);
        Assert.True(enemy.Hp < hpBefore);
        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.FreeAttackPower));
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

    [Fact]
    public void Thrash_TwoHitsAndExhaustsRandomAttack()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);
        var enemy = state.Enemies[0];
        int hpBefore = enemy.Hp;

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.Thrash, false));
        state.Hand.Add(new CardInstance(IC.StrikeIronclad, false)); // Attack to exhaust
        state.Hand.Add(new CardInstance(IC.DefendIronclad, false)); // Skill, should not be exhausted
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng);
        // Thrash deals 4×2=8 damage
        Assert.Equal(hpBefore - 8, enemy.Hp);
        // The Attack (Strike) should be exhausted, Skill (Defend) should remain
        Assert.Equal(0, state.ExhaustPile.Count(c => c.DefId == IC.Thrash)); // Thrash does not exhaust itself
        Assert.DoesNotContain(state.Hand, c => c.DefId == IC.StrikeIronclad);
    }

    [Fact]
    public void TearAsunder_HitsOnceWithNoUnblockedDamage()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);
        var enemy = state.Enemies[0];
        int hpBefore = enemy.Hp;

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.TearAsunder, false));
        state.Energy = 3;
        // UnblockedDamageHitCount = 0 → 1 hit

        CombatEngine.Step(state, 0, rng);
        Assert.Equal(hpBefore - 5, enemy.Hp); // 5 dmg × 1 hit
    }

    [Fact]
    public void TearAsunder_HitsMoreWithUnblockedDamage()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);
        var enemy = state.Enemies[0];
        int hpBefore = enemy.Hp;

        state.UnblockedDamageHitCount = 2;
        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.TearAsunder, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng);
        Assert.Equal(hpBefore - 15, enemy.Hp); // 5 dmg × 3 hits
    }
}
