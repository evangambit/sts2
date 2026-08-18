namespace Sts2Emulator.Core;

// Action encoding:
//   0..hand.Count-1  → play card at that hand index (targeting first enemy, or TargetEnemyIndex)
//   hand.Count       → end turn
//   hand.Count+1..   → use potion at slot (index - hand.Count - 1)
//
// While state.PendingSelection is open the encoding is replaced entirely:
//   0..Candidates.Count-1 → answer the card-selection screen with that candidate

public static class CombatEngine
{
    public static StepResult Step(
        CombatState state,
        int action,
        Random rng,
        int targetEnemyIndex = -1
    )
    {
        state.TargetEnemyIndex = targetEnemyIndex;

        // A pending card selection owns the action space until it is answered: the game
        // will not let you play, end the turn or quaff while its selection screen is up.
        if (state.PendingSelection is not null)
        {
            return ResolveCardSelection(state, action, rng);
        }

        int endTurnAction = state.Hand.Count;
        StepResult result;

        if (action == endTurnAction)
        {
            result = EndTurn(state, rng);
        }
        else if (action < endTurnAction)
        {
            result = PlayCard(state, action, rng);
        }
        else
        {
            int potionSlot = action - endTurnAction - 1;
            result = UsePotion(state, potionSlot, rng);
        }

        // Auto-plays use first-living enemy, not the explicit target.
        state.TargetEnemyIndex = -1;

        // Process auto-plays (e.g. from Hellraiser).
        while (state.AutoPlayQueue.Count > 0 && !result.Terminal)
        {
            var next = state.AutoPlayQueue[0];
            state.AutoPlayQueue.RemoveAt(0);
            AutoPlay(state, next, rng);

            // Re-check terminality after auto-play.
            bool playerDead = state.PlayerHp <= 0;
            bool allDead = state.Enemies.All(e => e.Hp <= 0);
            if (playerDead || allDead)
            {
                result = result with { Terminal = true, PlayerWon = allDead && !playerDead };
            }
        }

        return result;
    }

    private static StepResult PlayCard(CombatState state, int handIndex, Random rng)
    {
        var card = state.Hand[handIndex];
        var def = GeneratedData.Cards.Get(card.DefId);

        int effectiveCost = EffectiveCost(card, def, state);
        int energyToSpend = Math.Max(0, effectiveCost);
        if (
            def.Unplayable
            || energyToSpend > state.Energy
            || IsBlockedBySmoggy(def, state)
            || IsBlockedByEnthralled(card, state)
        )
        {
            return StepResult.Invalid;
        }
        bool feralReturn =
            def.Type == CardType.Attack
            && energyToSpend == 0
            && BuffSystem.Get(state.PlayerBuffs, BuffId.Feral)
                > BuffSystem.Get(state.PlayerBuffs, BuffId.FeralUsed);

        // Snapshot HP before effects.
        int playerHpBefore = state.PlayerHp;
        Span<int> enemyHpsBefore = stackalloc int[state.Enemies.Count];
        for (int i = 0; i < state.Enemies.Count; i++)
        {
            enemyHpsBefore[i] = state.Enemies[i].Hp;
        }

        state.Energy -= energyToSpend;
        state.Hand.RemoveAt(handIndex);
        if (def.Type == CardType.Skill && BuffSystem.Get(state.PlayerBuffs, BuffId.Smoggy) > 0)
        {
            state.SkillPlayedWhileSmoggy = true;
        }

        // FreeAttackPower: consume one stack before the card effect runs (BeforeCardPlayed timing).
        if (def.Type == CardType.Attack)
        {
            int freeAtk = BuffSystem.Get(state.PlayerBuffs, BuffId.FreeAttackPower);
            if (freeAtk > 0)
            {
                BuffSystem.Apply(state.PlayerBuffs, BuffId.FreeAttackPower, -1);
            }
        }

        Effects.CardEffects.Apply(def, card.Upgraded, state, rng, card);
        int extraPlays =
            state.CardPlaysThisTurn < BuffSystem.Get(state.PlayerBuffs, BuffId.EchoForm) ? 1 : 0;
        int signalBoost = BuffSystem.Get(state.PlayerBuffs, BuffId.SignalBoost);
        if (def.Type == CardType.Power && signalBoost > 0)
        {
            extraPlays++;
            if (signalBoost == 1)
            {
                BuffSystem.Remove(state.PlayerBuffs, BuffId.SignalBoost);
            }
            else
            {
                BuffSystem.Apply(state.PlayerBuffs, BuffId.SignalBoost, -1);
            }
        }

        for (int i = 0; i < extraPlays; i++)
        {
            Effects.CardEffects.Apply(def, card.Upgraded, state, rng, card);
        }
        if (def.Type == CardType.Attack)
        {
            int oneTwoPunch = BuffSystem.Get(state.PlayerBuffs, BuffId.OneTwoPunch);
            if (oneTwoPunch > 0)
            {
                Effects.CardEffects.Apply(def, card.Upgraded, state, rng, card);
                if (oneTwoPunch == 1)
                {
                    BuffSystem.Remove(state.PlayerBuffs, BuffId.OneTwoPunch);
                }
                else
                {
                    BuffSystem.Apply(state.PlayerBuffs, BuffId.OneTwoPunch, -1);
                }
            }
            QueueAttackPlayLifecycleEffects(state, card);
            BuffSystem.Remove(state.PlayerBuffs, BuffId.Vigor);
        }
        HandleEnemyDeaths(state, enemyHpsBefore, rng);

        // Rage: gain block when playing an Attack.
        if (def.Type == CardType.Attack)
        {
            state.AttackCardsPlayedThisTurn++;
            if (state.AttackCardsPlayedThisTurn == 3)
            {
                int juggling = BuffSystem.Get(state.PlayerBuffs, BuffId.Juggling);
                for (int i = 0; i < juggling; i++)
                {
                    state.Hand.Add(new CardInstance(card.DefId, card.Upgraded));
                }
            }

            int rage = BuffSystem.Get(state.PlayerBuffs, BuffId.Rage);
            if (rage > 0)
            {
                Effects.CardEffects.GainBlock(state, rage, rng);
            }

            int calamity = BuffSystem.Get(state.PlayerBuffs, BuffId.CalamityPower);
            if (calamity > 0)
            {
                Effects.CardEffects.AddRandomAttackCardsToHand(state, calamity, rng);
            }
        }

        // Corruption: Skills exhaust instead of discard.
        bool corruptedSkill =
            def.Type == CardType.Skill && BuffSystem.Get(state.PlayerBuffs, BuffId.Corruption) > 0;
        if (def.Type == CardType.Power)
        {
            // Played powers leave hand without firing exhaust hooks.
        }
        else if (ShouldExhaustAfterPlay(def, card) || corruptedSkill)
        {
            Effects.CardEffects.ExhaustCard(state, card, rng: rng);
        }
        else if (feralReturn)
        {
            state.Hand.Add(card with { FreeThisTurn = false });
            BuffSystem.Apply(state.PlayerBuffs, BuffId.FeralUsed, 1);
        }
        else if (ShouldPlaceOnDrawPileAfterPlay(state, def))
        {
            state.DrawPile.Insert(0, card with { FreeThisTurn = false });
        }
        else
        {
            state.DiscardPile.Add(card with { FreeThisTurn = false });
        }

        IncrementPlayedCardTypeCounters(state, def);
        ApplyAfterCardPlayedPowers(state, def, rng);
        Effects.RelicEffects.ApplyAfterPlayerHpChanged(state);

        bool playerDead = state.PlayerHp <= 0;
        bool allDead = state.Enemies.All(e => e.Hp <= 0);

        return new StepResult(
            Terminal: playerDead || allDead,
            PlayerWon: allDead && !playerDead,
            Reward: ComputeReward(state, playerDead, allDead, playerHpBefore, enemyHpsBefore)
        );
    }

    private static StepResult EndTurn(CombatState state, Random rng)
    {
        // Snapshot HP before enemies act.
        int playerHpBefore = state.PlayerHp;
        Span<int> enemyHpsBefore = stackalloc int[state.Enemies.Count];
        for (int i = 0; i < state.Enemies.Count; i++)
        {
            enemyHpsBefore[i] = state.Enemies[i].Hp;
        }

        // ── End of player turn ────────────────────────────────────────────────
        TickTheBomb(state);

        // Metallicize: gain block at end of player turn.
        int metallicize = BuffSystem.Get(state.PlayerBuffs, BuffId.Metallicize);
        if (metallicize > 0)
        {
            Effects.CardEffects.GainBlock(state, metallicize, rng);
        }
        // Plating (Stone Armor): gain block at end of player turn.
        int plating = BuffSystem.Get(state.PlayerBuffs, BuffId.Plating);
        if (plating > 0)
        {
            Effects.CardEffects.GainBlock(state, plating, rng);
        }

        int hailstorm = BuffSystem.Get(state.PlayerBuffs, BuffId.Hailstorm);
        if (hailstorm > 0 && state.Orbs.Any(o => o.Type == OrbType.Frost))
        {
            Effects.CardEffects.DealUnpoweredDamageToAll(state, hailstorm);
        }

        Effects.CardEffects.TriggerAllOrbBeforeTurnEndPassives(state, rng);

        int consumingShadow = BuffSystem.Get(state.PlayerBuffs, BuffId.ConsumingShadow);
        for (int i = 0; i < consumingShadow; i++)
        {
            Effects.CardEffects.EvokeLastOrb(state, rng);
        }

        AutoPlayStampedeAttacks(state, rng);

        Effects.RelicEffects.ApplyEndOfPlayerTurn(state, rng);
        bool allDeadAfterEndTurnPowers = state.Enemies.All(e => e.Hp <= 0);
        if (allDeadAfterEndTurnPowers)
        {
            return new StepResult(
                Terminal: true,
                PlayerWon: true,
                Reward: ComputeReward(state, false, true, playerHpBefore, enemyHpsBefore)
            );
        }

        // Tick player debuffs at end of player turn (Vulnerable etc. tick down).
        BuffSystem.TickEndOfTurn(state.PlayerBuffs);

        int temporaryStrength = BuffSystem.Get(state.PlayerBuffs, BuffId.TemporaryStrength);
        if (temporaryStrength != 0)
        {
            BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, -temporaryStrength);
            BuffSystem.Remove(state.PlayerBuffs, BuffId.TemporaryStrength);
        }

        int temporaryFocus = BuffSystem.Get(state.PlayerBuffs, BuffId.TemporaryFocus);
        if (temporaryFocus != 0)
        {
            BuffSystem.Apply(state.PlayerBuffs, BuffId.Focus, -temporaryFocus);
            BuffSystem.Remove(state.PlayerBuffs, BuffId.TemporaryFocus);
        }

        // Rage expires at end of player turn.
        BuffSystem.Remove(state.PlayerBuffs, BuffId.Rage);
        BuffSystem.Remove(state.PlayerBuffs, BuffId.OneTwoPunch);

        int constrict = BuffSystem.Get(state.PlayerBuffs, BuffId.Constrict);
        if (constrict > 0)
        {
            state.PlayerHp = Math.Max(0, state.PlayerHp - constrict);
        }

        int disintegration = BuffSystem.Get(state.PlayerBuffs, BuffId.Disintegration);
        if (disintegration > 0)
        {
            state.PlayerHp = Math.Max(0, state.PlayerHp - disintegration);
        }

        // Move hand to discard, exhausting ethereal cards unless a retain-hand effect is active.
        int retainHand = BuffSystem.Get(state.PlayerBuffs, BuffId.RetainHand);
        var nextHand = new List<CardInstance>();
        foreach (var card in state.Hand)
        {
            var def = GeneratedData.Cards.Get(card.DefId);
            if (def.Ethereal && !(def.Id == 159 && card.Upgraded))
            {
                Effects.CardEffects.ExhaustCard(state, card, causedByEthereal: true, rng: rng);
                continue;
            }

            // Status card end-of-turn effects
            if (def.Id == Effects.ST.Burn)
            {
                Effects.CardEffects.DealDamageToPlayer(state, 2);
            }
            else if (def.Id == Effects.ST.Toxic)
            {
                Effects.CardEffects.DealDamageToPlayer(state, 5);
            }
            else if (def.Id == Effects.ST.Beckon)
            {
                state.PlayerHp = Math.Max(0, state.PlayerHp - 6); // Beckon is unblockable
            }

            if (retainHand > 0 || def.Retain)
            {
                nextHand.Add(card with { FreeThisTurn = false });
            }
            else
            {
                state.DiscardPile.Add(card with { FreeThisTurn = false });
            }
        }
        state.Hand.Clear();
        state.Hand.AddRange(nextHand);

        if (retainHand == 1)
        {
            BuffSystem.Remove(state.PlayerBuffs, BuffId.RetainHand);
        }
        else if (retainHand > 1)
        {
            BuffSystem.Apply(state.PlayerBuffs, BuffId.RetainHand, -1);
        }

        // Tick enemy debuffs before enemies act (Vulnerable/Weak on enemies tick down).
        foreach (var enemy in state.Enemies.ToArray())
        {
            BuffSystem.TickEndOfTurn(enemy.Buffs);
        }

        Effects.CardEffects.KillDoomedEnemiesForTurnEnd(state);

        // ── Enemy turns ───────────────────────────────────────────────────────
        state.PlayerTurn = false;
        foreach (var enemy in state.Enemies.Where(e => e.Hp > 0).ToArray())
        {
            // Poison damage at start of enemy turn.
            int poison = BuffSystem.Get(enemy.Buffs, BuffId.Poison);
            if (poison > 0)
            {
                enemy.Hp -= poison;
                BuffSystem.Apply(enemy.Buffs, BuffId.Poison, -1);
                if (enemy.Hp <= 0)
                {
                    continue;
                }
            }

            int sandpit = BuffSystem.Get(enemy.Buffs, BuffId.Sandpit);
            if (sandpit > 0)
            {
                BuffSystem.Apply(enemy.Buffs, BuffId.Sandpit, -1);
                if (BuffSystem.Get(enemy.Buffs, BuffId.Sandpit) == 0)
                {
                    state.PlayerHp = 0;
                    return new StepResult(Terminal: true, PlayerWon: false, Reward: -1f);
                }
            }

            EnemyAI.ExecuteIntent(enemy, state, rng);
        }
        HandleEnemyDeaths(state, enemyHpsBefore, rng);

        // Restore temporary Strength debuffs applied this turn (e.g. DarkShackles).
        foreach (var enemy in state.Enemies)
        {
            int tempStr = BuffSystem.Get(enemy.Buffs, BuffId.TemporaryStrength);
            if (tempStr != 0)
            {
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, tempStr);
                BuffSystem.Remove(enemy.Buffs, BuffId.TemporaryStrength);
            }
        }

        // Dark Embrace: deferred draw for Ethereal cards exhausted at end of turn.
        int de = BuffSystem.Get(state.PlayerBuffs, BuffId.DarkEmbrace);
        if (de > 0 && state.EtherealExhaustCount > 0)
        {
            Effects.CardEffects.DrawCards(state, de * state.EtherealExhaustCount, rng);
            state.EtherealExhaustCount = 0;
        }

        int colossus = BuffSystem.Get(state.PlayerBuffs, BuffId.Colossus);
        if (colossus > 0)
        {
            BuffSystem.Apply(state.PlayerBuffs, BuffId.Colossus, -1);
        }

        int noBlock = BuffSystem.Get(state.PlayerBuffs, BuffId.NoBlock);
        if (noBlock == 1)
        {
            BuffSystem.Remove(state.PlayerBuffs, BuffId.NoBlock);
        }
        else if (noBlock > 1)
        {
            BuffSystem.Apply(state.PlayerBuffs, BuffId.NoBlock, -1);
        }

        // FlameBarrier expires after enemies have acted.
        BuffSystem.Remove(state.PlayerBuffs, BuffId.FlameBarrier);

        // ── Start of next player turn ─────────────────────────────────────────
        state.Turn++;
        state.PlayerTurn = true;
        state.Energy = EffectiveMaxEnergy(state);
        state.PlayerHpLostThisTurn = 0;

        Effects.CardEffects.TriggerAllOrbAfterTurnStartPassives(state, rng);

        int spinner = BuffSystem.Get(state.PlayerBuffs, BuffId.Spinner);
        for (int i = 0; i < spinner; i++)
        {
            Effects.CardEffects.ChannelOrb(state, OrbType.Glass);
        }

        // Poison damage at start of player turn.
        int playerPoison = BuffSystem.Get(state.PlayerBuffs, BuffId.Poison);
        if (playerPoison > 0)
        {
            state.PlayerHp -= playerPoison;
            BuffSystem.Apply(state.PlayerBuffs, BuffId.Poison, -1);
            if (state.PlayerHp <= 0)
            {
                return new StepResult(
                    Terminal: true,
                    PlayerWon: false,
                    Reward: ComputeReward(state, true, false, playerHpBefore, enemyHpsBefore)
                );
            }
        }

        int entropy = BuffSystem.Get(state.PlayerBuffs, BuffId.EntropyPower);
        for (int i = 0; i < entropy; i++)
        {
            Effects.CardEffects.TransformRandomCardInHand(state, rng);
        }

        // Barricade: block does not reset.
        if (BuffSystem.Get(state.PlayerBuffs, BuffId.Barricade) == 0)
        {
            state.PlayerBlock = 0;
        }

        ApplyBlockNextTurn(state, rng);

        int coolant = BuffSystem.Get(state.PlayerBuffs, BuffId.Coolant);
        if (coolant > 0)
        {
            Effects.CardEffects.GainUnpoweredBlock(
                state,
                coolant * state.Orbs.Select(o => o.Type).Distinct().Count()
            );
        }

        foreach (var enemy in state.Enemies)
        {
            BuffSystem.Remove(enemy.Buffs, BuffId.SlowCount);
            if (enemy.DefId == KE.SkulkingColony)
            {
                BuffSystem.Apply(
                    enemy.Buffs,
                    BuffId.HardenedShell,
                    20 - BuffSystem.Get(enemy.Buffs, BuffId.HardenedShell)
                );
            }
        }

        int rampart = state
            .Enemies.Where(e => e.Hp > 0)
            .Select(e => BuffSystem.Get(e.Buffs, BuffId.Rampart))
            .DefaultIfEmpty(0)
            .Max();
        if (rampart > 0)
        {
            foreach (
                var turret in state.Enemies.Where(e => e.Hp > 0 && e.DefId == KE.TurretOperator)
            )
            {
                turret.Block += rampart;
            }
        }

        Effects.RelicEffects.ApplyStartOfPlayerTurn(state, rng);

        int loop = BuffSystem.Get(state.PlayerBuffs, BuffId.Loop);
        for (int i = 0; i < loop; i++)
        {
            Effects.CardEffects.TriggerOrbPassive(state, 0, rng);
        }

        int prepTime = BuffSystem.Get(state.PlayerBuffs, BuffId.PrepTimePower);
        if (prepTime > 0)
        {
            BuffSystem.Apply(state.PlayerBuffs, BuffId.Vigor, prepTime);
        }

        int nextTurnEnergy = BuffSystem.Get(state.PlayerBuffs, BuffId.NextTurnEnergy);
        if (nextTurnEnergy > 0)
        {
            state.Energy += nextTurnEnergy;
            BuffSystem.Remove(state.PlayerBuffs, BuffId.NextTurnEnergy);
        }

        int rollingBoulder = BuffSystem.Get(state.PlayerBuffs, BuffId.RollingBoulderPower);
        if (rollingBoulder > 0)
        {
            Effects.CardEffects.DealUnpoweredDamageToAll(state, rollingBoulder);
            BuffSystem.Apply(state.PlayerBuffs, BuffId.RollingBoulderPower, 5);
        }

        int crimsonDmg = BuffSystem.Get(state.PlayerBuffs, BuffId.CrimsonMantleSelfDamage);
        if (crimsonDmg > 0)
        {
            Effects.CardEffects.LoseHp(state, crimsonDmg);
        }

        int crimsonBlock = BuffSystem.Get(state.PlayerBuffs, BuffId.CrimsonMantleBlock);
        if (crimsonBlock > 0)
        {
            Effects.CardEffects.GainUnpoweredBlock(state, crimsonBlock, rng);
        }

        // DemonForm: gain Strength at start of player turn.
        int demonForm = BuffSystem.Get(state.PlayerBuffs, BuffId.DemonForm);
        if (demonForm > 0)
        {
            BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, demonForm);
        }

        // Aggression: add random upgraded card at start of player turn.
        int aggression = BuffSystem.Get(state.PlayerBuffs, BuffId.Aggression);
        if (aggression > 0)
        {
            Effects.CardEffects.AddRandomUpgradedIroncladCardToHand(state, aggression, rng);
        }

        int infernoSelfDamage = BuffSystem.Get(state.PlayerBuffs, BuffId.InfernoSelfDamage);
        if (infernoSelfDamage > 0)
        {
            Span<int> enemyHpsBeforeInferno = stackalloc int[state.Enemies.Count];
            for (int i = 0; i < state.Enemies.Count; i++)
            {
                enemyHpsBeforeInferno[i] = state.Enemies[i].Hp;
            }

            Effects.CardEffects.LoseHp(state, infernoSelfDamage);
            HandleEnemyDeaths(state, enemyHpsBeforeInferno, rng);
        }

        // Plating decays by 1 at start of player turn.
        int platingNow = BuffSystem.Get(state.PlayerBuffs, BuffId.Plating);
        if (platingNow > 0)
        {
            if (platingNow == 1)
            {
                BuffSystem.Remove(state.PlayerBuffs, BuffId.Plating);
            }
            else
            {
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Plating, -1);
            }
        }

        BuffSystem.Remove(state.PlayerBuffs, BuffId.Tangled);
        BuffSystem.Remove(state.PlayerBuffs, BuffId.Smoggy);
        BuffSystem.Remove(state.PlayerBuffs, BuffId.FeralUsed);
        state.SkillPlayedWhileSmoggy = false;
        state.AttackCardsPlayedThisTurn = 0;
        state.AttackOrSkillCardsPlayedThisTurn = 0;
        state.CardPlaysThisTurn = 0;
        state.BlockGainsThisTurn = 0;
        state.CardsExhaustedThisTurn = 0;

        ReturnQueuedCardsToHandBeforeDraw(state);

        int creativeAi = BuffSystem.Get(state.PlayerBuffs, BuffId.CreativeAi);
        if (creativeAi > 0)
        {
            Effects.CardEffects.AddRandomDefectPowerCardsToHand(state, creativeAi, rng);
        }

        // Draw five cards.
        Effects.CardEffects.DrawCards(
            state,
            5 + BuffSystem.Get(state.PlayerBuffs, BuffId.MachineLearning),
            rng
        );
        int nextTurnDraw = BuffSystem.Get(state.PlayerBuffs, BuffId.NextTurnDraw);
        if (nextTurnDraw > 0)
        {
            Effects.CardEffects.DrawCards(state, nextTurnDraw, rng);
            BuffSystem.Remove(state.PlayerBuffs, BuffId.NextTurnDraw);
        }
        int toolsOfTheTrade = BuffSystem.Get(state.PlayerBuffs, BuffId.ToolsOfTheTrade);
        if (toolsOfTheTrade > 0)
        {
            Effects.CardEffects.DrawCards(state, toolsOfTheTrade, rng);
            Effects.CardEffects.DiscardFirstCardsFromHand(state, toolsOfTheTrade);
        }
        int infiniteBlades = BuffSystem.Get(state.PlayerBuffs, BuffId.InfiniteBlades);
        if (infiniteBlades > 0)
        {
            Effects.CardEffects.AddGeneratedCardsToHand(state, 430, infiniteBlades);
        }

        int noxiousFumes = BuffSystem.Get(state.PlayerBuffs, BuffId.NoxiousFumes);
        if (noxiousFumes > 0)
        {
            Effects.CardEffects.ApplyPoisonToAllEnemies(state, noxiousFumes, rng);
        }

        AutoPlayMayhemCards(state, rng);

        // Enemies choose their next intent.
        EnemyAI.ChooseIntents(state.Enemies, state.Turn, rng, state.AiRng, state.AscensionLevel);
        Effects.RelicEffects.ApplyAfterPlayerHpChanged(state);

        bool playerDead = state.PlayerHp <= 0;
        bool allDead = state.Enemies.All(e => e.Hp <= 0);

        return new StepResult(
            Terminal: playerDead || allDead,
            PlayerWon: allDead && !playerDead,
            Reward: ComputeReward(state, playerDead, allDead, playerHpBefore, enemyHpsBefore)
        );
    }

    private static StepResult UsePotion(CombatState state, int slot, Random? rng)
    {
        if (slot < 0 || slot >= state.PotionSlots.Length || state.PotionSlots[slot] == 0)
        {
            return StepResult.Invalid;
        }

        Effects.PotionEffects.Apply(state.PotionSlots[slot], state, rng);
        state.PotionSlots[slot] = 0;
        Effects.RelicEffects.ApplyAfterPlayerHpChanged(state);

        return new StepResult(Terminal: false, PlayerWon: false, Reward: 0f);
    }

    // Shaped reward: fraction of enemy HP dealt minus fraction of player HP lost,
    // plus ±1 terminal bonus for win/death.
    private static float ComputeReward(
        CombatState state,
        bool playerDead,
        bool allDead,
        int playerHpBefore,
        ReadOnlySpan<int> enemyHpsBefore
    )
    {
        float totalMaxHp = 0f;
        float dmgDealt = 0f;
        for (int i = 0; i < state.Enemies.Count; i++)
        {
            totalMaxHp += state.Enemies[i].MaxHp;
            if (i < enemyHpsBefore.Length)
            {
                dmgDealt += Math.Max(0, enemyHpsBefore[i] - state.Enemies[i].Hp);
            }
        }

        float dmgTaken = Math.Max(0, playerHpBefore - state.PlayerHp);

        float shaped =
            (totalMaxHp > 0f ? dmgDealt / totalMaxHp : 0f) - dmgTaken / (float)state.PlayerMaxHp;

        float terminal = (allDead && !playerDead) ? 1f : (playerDead ? -1f : 0f);

        return shaped + terminal;
    }

    private static int EffectiveMaxEnergy(CombatState state)
    {
        return state.MaxEnergy + BuffSystem.Get(state.PlayerBuffs, BuffId.PyrePower);
    }

    // Returns the energy cost of a card after applying active powers (e.g. Corruption).
    private static int EffectiveCost(CardInstance card, CardDef def, CombatState state)
    {
        if (card.FreeThisTurn)
        {
            return 0;
        }

        if (def.Type == CardType.Skill && BuffSystem.Get(state.PlayerBuffs, BuffId.Corruption) > 0)
        {
            return 0;
        }

        int cost = card.CostForCombat == int.MinValue ? def.Cost : card.CostForCombat;
        if (def.Id == Effects.IC.BodySlam && card.Upgraded)
        {
            cost -= 1;
        }

        if (def.Id == Effects.IC.Havoc && card.Upgraded)
        {
            cost -= 1;
        }

        if (def.Id == Effects.IC.InfernalBlade && card.Upgraded)
        {
            cost -= 1;
        }

        if (def.Id == Effects.IC.Nostalgia && card.Upgraded)
        {
            cost -= 1;
        }

        if (def.Id == Effects.IC.Stampede && card.Upgraded)
        {
            cost -= 1;
        }

        if (
            def.Id
                is Effects.CL.Automation
                    or Effects.CL.Calamity
                    or Effects.CL.Mayhem
                    or Effects.CL.MindBlast
                    or Effects.CL.Stratagem
            && card.Upgraded
        )
        {
            cost -= 1;
        }

        if (
            def.Id
                is 111 // CreativeAi
                    or 156 // Dualcast
                    or 186 // Feral
                    or 375 // Quadcast
                    or 435 // SignalBoost
                    or 476 // Subroutine
                    or 540 // WhiteNoise
                    or 545 // Zap
            && card.Upgraded
        )
        {
            cost -= 1;
        }

        if (def.Id == Effects.IC.Stomp)
        {
            cost -= state.AttackCardsPlayedThisTurn;
        }

        if (def.Id == Effects.ST.FranticEscape)
        {
            cost += BuffSystem.Get(state.PlayerBuffs, BuffId.FranticEscapePlayedCount);
        }

        if (def.Type == CardType.Attack)
        {
            cost += BuffSystem.Get(state.PlayerBuffs, BuffId.Tangled);
            if (BuffSystem.Get(state.PlayerBuffs, BuffId.FreeAttackPower) > 0)
            {
                return 0;
            }
        }
        return cost;
    }

    private static bool IsBlockedBySmoggy(CardDef def, CombatState state)
    {
        return def.Type == CardType.Skill
            && state.SkillPlayedWhileSmoggy
            && BuffSystem.Get(state.PlayerBuffs, BuffId.Smoggy) > 0;
    }

    private static bool IsBlockedByEnthralled(CardInstance card, CombatState state) =>
        card.DefId != Effects.ST.Enthralled
        && state.Hand.Any(handCard => handCard.DefId == Effects.ST.Enthralled);

    public static int[] ValidActions(CombatState state)
    {
        var actions = new List<int>();

        if (state.PendingSelection is { } selection)
        {
            for (int i = 0; i < selection.Candidates.Count; i++)
            {
                actions.Add(i);
            }

            return [.. actions];
        }

        for (int i = 0; i < state.Hand.Count; i++)
        {
            var def = GeneratedData.Cards.Get(state.Hand[i].DefId);
            int effectiveCost = EffectiveCost(state.Hand[i], def, state);
            int energyToSpend = Math.Max(0, effectiveCost);
            if (
                !def.Unplayable
                && energyToSpend <= state.Energy
                && !IsBlockedBySmoggy(def, state)
                && !IsBlockedByEnthralled(state.Hand[i], state)
            )
            {
                actions.Add(i);
            }
        }

        actions.Add(state.Hand.Count); // end turn always valid

        for (int s = 0; s < state.PotionSlots.Length; s++)
        {
            if (state.PotionSlots[s] != 0)
            {
                actions.Add(state.Hand.Count + 1 + s);
            }
        }

        return [.. actions];
    }

    /// <summary>
    /// Answers the open card-selection screen and closes it. Moving a card cannot end
    /// the combat, so this is never terminal.
    /// </summary>
    private static StepResult ResolveCardSelection(CombatState state, int action, Random rng)
    {
        var selection = state.PendingSelection!;
        if (action < 0 || action >= selection.Candidates.Count)
        {
            return StepResult.Invalid;
        }

        int index = selection.Candidates[action];
        state.PendingSelection = null;

        switch (selection.Kind)
        {
            case CardSelectionKind.DiscardToDrawPileTop:
                if (index < state.DiscardPile.Count)
                {
                    var card = state.DiscardPile[index];
                    state.DiscardPile.RemoveAt(index);
                    state.DrawPile.Insert(0, card);
                }

                break;

            case CardSelectionKind.ExhaustFromHand:
                if (index < state.Hand.Count)
                {
                    var card = state.Hand[index];
                    state.Hand.RemoveAt(index);
                    Effects.CardEffects.ExhaustCard(state, card, rng: rng);
                }

                break;
        }

        return new StepResult(Terminal: false, PlayerWon: false, Reward: 0f);
    }

    private static void HandleEnemyDeaths(
        CombatState state,
        ReadOnlySpan<int> enemyHpsBefore,
        Random rng
    )
    {
        for (int i = 0; i < state.Enemies.Count && i < enemyHpsBefore.Length; i++)
        {
            if (enemyHpsBefore[i] <= 0 || state.Enemies[i].Hp > 0)
            {
                continue;
            }

            if (BuffSystem.Get(state.Enemies[i].Buffs, BuffId.Surprise) > 0)
            {
                SpawnGremlinMercReinforcements(state, rng, state.Enemies[i].StolenGold);
            }
            else if (TryRespawnAxebot(state.Enemies[i], rng))
            {
                continue;
            }
            else if (TryRespawnTestSubject(state.Enemies[i]))
            {
                continue;
            }
            else if (BuffSystem.Get(state.Enemies[i].Buffs, BuffId.Infested) > 0)
            {
                SpawnPhrogParasiteWrigglers(state, rng, state.Enemies[i]);
                BuffSystem.Remove(state.Enemies[i].Buffs, BuffId.Infested);
            }
            else if (
                state.Enemies[i].DefId == KE.FatGremlin
                && state.EncounterId != Run.RunConstants.GremlinMercEncounterId
                && state.Enemies[i].HeistGold > 0
            )
            {
                state.PlayerGold += state.Enemies[i].HeistGold;
                state.Enemies[i].HeistGold = 0;
            }

            if (state.Enemies[i].DefId == KE.SlitheringStrangler)
            {
                BuffSystem.Remove(state.PlayerBuffs, BuffId.Constrict);
            }

            foreach (var enemy in state.Enemies.Where(e => e.Hp > 0))
            {
                int ravenous = BuffSystem.Get(enemy.Buffs, BuffId.Ravenous);
                if (ravenous <= 0)
                {
                    continue;
                }

                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, ravenous);
                BuffSystem.Apply(enemy.Buffs, BuffId.Stunned, 1);
            }
        }
    }

    private static bool TryRespawnAxebot(EnemyState enemy, Random rng)
    {
        if (enemy.DefId != KE.Axebot)
        {
            return false;
        }

        int stock = BuffSystem.Get(enemy.Buffs, BuffId.Stock);
        if (stock <= 0)
        {
            return false;
        }

        var def = GeneratedData.Enemies.Get(KE.Axebot);
        enemy.Hp = rng.Next(def.MinHp, def.MaxHp + 1);
        enemy.MaxHp = enemy.Hp;
        enemy.Block = 0;
        enemy.MoveIndex = 0;
        enemy.CurrentIntent = new Intent(IntentType.Defend, 15);
        BuffSystem.Apply(enemy.Buffs, BuffId.Stock, -1);
        return true;
    }

    private static bool TryRespawnTestSubject(EnemyState enemy)
    {
        if (enemy.DefId != KE.TestSubject || BuffSystem.Get(enemy.Buffs, BuffId.Adaptable) <= 0)
        {
            return false;
        }

        if (BuffSystem.Get(enemy.Buffs, BuffId.PainfulStabs) <= 0)
        {
            enemy.Hp = 212;
            enemy.MaxHp = 212;
            enemy.Block = 0;
            enemy.MoveIndex = 2;
            enemy.CurrentIntent = new Intent(IntentType.Attack, 33);
            BuffSystem.Apply(enemy.Buffs, BuffId.PainfulStabs, 1);
            return true;
        }

        enemy.Hp = 313;
        enemy.MaxHp = 313;
        enemy.Block = 0;
        enemy.MoveIndex = 4;
        enemy.CurrentIntent = new Intent(IntentType.Attack, 33);
        BuffSystem.Remove(enemy.Buffs, BuffId.Adaptable);
        BuffSystem.Remove(enemy.Buffs, BuffId.PainfulStabs);
        BuffSystem.Apply(enemy.Buffs, BuffId.Intangible, 1);
        return true;
    }

    private static void SpawnPhrogParasiteWrigglers(CombatState state, Random rng, EnemyState phrog)
    {
        int insertIndex = state.Enemies.IndexOf(phrog) + 1;
        for (int i = 0; i < 4 && state.Enemies.Count < 6; i++)
        {
            var wriggler = CreateEnemy(
                KE.Wriggler,
                rng,
                new Intent(IntentType.Unknown, 0),
                stunned: true
            );
            state.Enemies.Insert(insertIndex + i, wriggler);
        }
    }

    private static void SpawnGremlinMercReinforcements(
        CombatState state,
        Random rng,
        int stolenGold
    )
    {
        state.Enemies.Add(CreateEnemy(78, rng, new Intent(IntentType.Unknown, 0), stunned: true));
        var fatGremlin = CreateEnemy(28, rng, new Intent(IntentType.Unknown, 0), stunned: true);
        fatGremlin.HeistGold = stolenGold;
        state.Enemies.Add(fatGremlin);
    }

    private static void AutoPlayStampedeAttacks(CombatState state, Random rng)
    {
        int stampede = BuffSystem.Get(state.PlayerBuffs, BuffId.Stampede);
        for (int i = 0; i < stampede && state.Enemies.Any(e => e.Hp > 0); i++)
        {
            var attackIndexes = state
                .Hand.Select((card, index) => (card, index))
                .Where(item =>
                {
                    var def = GeneratedData.Cards.Get(item.card.DefId);
                    return def.Type == CardType.Attack && !def.Unplayable;
                })
                .Select(item => item.index)
                .ToList();
            if (attackIndexes.Count == 0)
            {
                return;
            }

            int handIndex = attackIndexes[rng.Next(attackIndexes.Count)];
            AutoPlayCardFromHand(state, handIndex, rng);
        }
    }

    private static void AutoPlayMayhemCards(CombatState state, Random rng)
    {
        int mayhem = BuffSystem.Get(state.PlayerBuffs, BuffId.MayhemPower);
        for (int i = 0; i < mayhem && state.DrawPile.Count > 0; i++)
        {
            var card = state.DrawPile[0];
            state.DrawPile.RemoveAt(0);
            AutoPlay(state, card, rng);
        }
    }

    private static void ReturnQueuedCardsToHandBeforeDraw(CombatState state)
    {
        foreach (var card in state.ReturnToHandBeforeDraw)
        {
            RemoveFirstMatchingCard(state.DiscardPile, card);
            RemoveFirstMatchingCard(state.DrawPile, card);
            RemoveFirstMatchingCard(state.ExhaustPile, card);
            state.Hand.Add(card with { FreeThisTurn = false });
        }
        state.ReturnToHandBeforeDraw.Clear();
    }

    private static void RemoveFirstMatchingCard(List<CardInstance> pile, CardInstance card)
    {
        int index = pile.FindIndex(pileCard =>
            pileCard.DefId == card.DefId && pileCard.Upgraded == card.Upgraded
        );
        if (index >= 0)
        {
            pile.RemoveAt(index);
        }
    }

    private static void AutoPlayCardFromHand(CombatState state, int handIndex, Random rng)
    {
        bool wasAutoPlaying = state.AutoPlaying;
        state.AutoPlaying = true;
        try
        {
            AutoPlayCardFromHandCore(state, handIndex, rng);
        }
        finally
        {
            state.AutoPlaying = wasAutoPlaying;
        }
    }

    private static void AutoPlayCardFromHandCore(CombatState state, int handIndex, Random rng)
    {
        var card = state.Hand[handIndex];
        var def = GeneratedData.Cards.Get(card.DefId);

        Span<int> enemyHpsBefore = stackalloc int[state.Enemies.Count];
        for (int i = 0; i < state.Enemies.Count; i++)
        {
            enemyHpsBefore[i] = state.Enemies[i].Hp;
        }

        state.Hand.RemoveAt(handIndex);
        Effects.CardEffects.Apply(def, card.Upgraded, state, rng, card);
        if (def.Type == CardType.Attack)
        {
            int oneTwoPunch = BuffSystem.Get(state.PlayerBuffs, BuffId.OneTwoPunch);
            if (oneTwoPunch > 0)
            {
                Effects.CardEffects.Apply(def, card.Upgraded, state, rng, card);
                if (oneTwoPunch == 1)
                {
                    BuffSystem.Remove(state.PlayerBuffs, BuffId.OneTwoPunch);
                }
                else
                {
                    BuffSystem.Apply(state.PlayerBuffs, BuffId.OneTwoPunch, -1);
                }
            }
            QueueAttackPlayLifecycleEffects(state, card);
            BuffSystem.Remove(state.PlayerBuffs, BuffId.Vigor);
        }
        HandleEnemyDeaths(state, enemyHpsBefore, rng);

        if (def.Type == CardType.Attack)
        {
            state.AttackCardsPlayedThisTurn++;
            if (state.AttackCardsPlayedThisTurn == 3)
            {
                int juggling = BuffSystem.Get(state.PlayerBuffs, BuffId.Juggling);
                for (int i = 0; i < juggling; i++)
                {
                    state.Hand.Add(new CardInstance(card.DefId, card.Upgraded));
                }
            }

            int rage = BuffSystem.Get(state.PlayerBuffs, BuffId.Rage);
            if (rage > 0)
            {
                Effects.CardEffects.GainBlock(state, rage, rng);
            }

            int calamity = BuffSystem.Get(state.PlayerBuffs, BuffId.CalamityPower);
            if (calamity > 0)
            {
                Effects.CardEffects.AddRandomAttackCardsToHand(state, calamity, rng);
            }
        }

        if (def.Type == CardType.Power)
        {
            // Played powers leave hand without firing exhaust hooks.
        }
        else if (ShouldExhaustAfterPlay(def, card))
        {
            Effects.CardEffects.ExhaustCard(state, card, rng: rng);
        }
        else if (ShouldPlaceOnDrawPileAfterPlay(state, def))
        {
            state.DrawPile.Insert(0, card with { FreeThisTurn = false });
        }
        else
        {
            state.DiscardPile.Add(card with { FreeThisTurn = false });
        }

        IncrementPlayedCardTypeCounters(state, def);
        ApplyAfterCardPlayedPowers(state, def, rng);
        Effects.RelicEffects.ApplyAfterPlayerHpChanged(state);
    }

    private static void QueueAttackPlayLifecycleEffects(CombatState state, CardInstance card)
    {
        if (card.DefId == Effects.CL.Bolas)
        {
            state.ReturnToHandBeforeDraw.Add(card with { FreeThisTurn = false });
        }
    }

    private static void ApplyAfterCardPlayedPowers(CombatState state, CardDef def, Random? rng)
    {
        if (def.Type == CardType.Skill)
        {
            foreach (var enemy in state.Enemies.Where(e => e.Hp > 0))
            {
                int enrage = BuffSystem.Get(enemy.Buffs, BuffId.Enrage);
                if (enrage > 0)
                {
                    BuffSystem.Apply(enemy.Buffs, BuffId.Strength, enrage);
                }
            }
        }

        if (def.Type == CardType.Power)
        {
            int storm = BuffSystem.Get(state.PlayerBuffs, BuffId.Storm);
            for (int i = 0; i < storm; i++)
            {
                Effects.CardEffects.ChannelOrb(state, OrbType.Lightning);
            }

            int subroutine = BuffSystem.Get(state.PlayerBuffs, BuffId.Subroutine);
            if (subroutine > 0)
            {
                state.Energy += subroutine;
            }

            int galvanic = state
                .Enemies.Where(e => e.Hp > 0)
                .Select(e => BuffSystem.Get(e.Buffs, BuffId.Galvanic))
                .DefaultIfEmpty(0)
                .Max();
            if (galvanic > 0)
            {
                Effects.CardEffects.DealDamageToPlayer(state, galvanic);
            }
        }

        int panache = BuffSystem.Get(state.PlayerBuffs, BuffId.PanachePower);
        if (panache > 0)
        {
            state.CardsPlayedSincePanacheProc++;
            if (state.CardsPlayedSincePanacheProc >= 5)
            {
                state.CardsPlayedSincePanacheProc = 0;
                Effects.CardEffects.DealUnpoweredDamageToAll(state, panache);
            }
        }

        int afterimage = BuffSystem.Get(state.PlayerBuffs, BuffId.Afterimage);
        if (afterimage > 0)
        {
            Effects.CardEffects.GainBlock(state, afterimage, rng);
        }

        foreach (var enemy in state.Enemies.Where(e => e.Hp > 0))
        {
            if (BuffSystem.Get(enemy.Buffs, BuffId.Slow) > 0)
            {
                BuffSystem.Apply(enemy.Buffs, BuffId.SlowCount, 1);
            }
        }
    }

    private static bool ShouldExhaustAfterPlay(CardDef def, CardInstance card)
    {
        if (def.Id == Effects.IC.Havoc)
        {
            return false;
        }

        if (def.Id == Effects.IC.Stampede)
        {
            return false;
        }

        if (
            def.Id
                is Effects.CL.Mimic
                    or Effects.CL.Purity
                    or Effects.CL.SecretTechnique
                    or Effects.CL.SecretWeapon
                    or Effects.CL.ThinkingAhead
            && card.Upgraded
        )
        {
            return false;
        }

        if (def.Id == Effects.CL.Prolong && card.Upgraded)
        {
            return false;
        }

        if (
            def.Id
                is 86 // Chill
                    or 211 // Fusion
                    or 253 // Hotfix
                    or 379 // Rainbow
                    or 488 // Synchronize
                    or 536 // Voltaic
            && card.Upgraded
        )
        {
            return false;
        }

        return def.Exhaust;
    }

    private static bool ShouldPlaceOnDrawPileAfterPlay(CombatState state, CardDef def)
    {
        int nostalgia = BuffSystem.Get(state.PlayerBuffs, BuffId.Nostalgia);
        return def.Id == 429
            || nostalgia > state.AttackOrSkillCardsPlayedThisTurn
                && (def.Type == CardType.Attack || def.Type == CardType.Skill);
    }

    /// <summary>
    /// Plays a queued card. The engine is mid-drain here and cannot hand a selection
    /// screen back to the caller, so anything that would raise one resolves itself —
    /// see CardEffects.OpenCardSelection.
    /// </summary>
    private static void AutoPlay(CombatState state, CardInstance card, Random rng)
    {
        bool wasAutoPlaying = state.AutoPlaying;
        state.AutoPlaying = true;
        try
        {
            AutoPlayCore(state, card, rng);
        }
        finally
        {
            state.AutoPlaying = wasAutoPlaying;
        }
    }

    private static void AutoPlayCore(CombatState state, CardInstance card, Random rng)
    {
        var def = GeneratedData.Cards.Get(card.DefId);

        // Auto-play picks its target the way CardCmd does when a played card has no
        // explicit one: Rng.CombatTargets.NextItem(HittableEnemies).
        int targetIndex = -1;
        if (def.Type == CardType.Attack)
        {
            var target = Effects.CardEffects.RandomLivingEnemy(state, rng);
            if (target != null)
            {
                targetIndex = state.Enemies.IndexOf(target);
            }
        }

        // Apply card effects.
        Effects.CardEffects.Apply(def, card.Upgraded, state, rng, card);
        if (def.Type == CardType.Attack)
        {
            QueueAttackPlayLifecycleEffects(state, card);
            BuffSystem.Remove(state.PlayerBuffs, BuffId.Vigor);
        }

        // Resolve status effects (Juggling, Rupture, etc. already handled in Apply or below).

        if (def.Type == CardType.Power)
        {
            // Played powers leave hand without firing exhaust hooks.
        }
        else if (ShouldExhaustAfterPlay(def, card))
        {
            Effects.CardEffects.ExhaustCard(state, card, rng: rng);
        }
        else if (ShouldPlaceOnDrawPileAfterPlay(state, def))
        {
            state.DrawPile.Insert(0, card with { FreeThisTurn = false });
        }
        else
        {
            state.DiscardPile.Add(card with { FreeThisTurn = false });
        }

        IncrementPlayedCardTypeCounters(state, def);
        ApplyAfterCardPlayedPowers(state, def, rng);
        Effects.RelicEffects.ApplyAfterPlayerHpChanged(state);
    }

    private static void IncrementPlayedCardTypeCounters(CombatState state, CardDef def)
    {
        state.CardPlaysThisTurn++;
        state.CardsPlayedThisCombat++;
        if (def.Type == CardType.Attack || def.Type == CardType.Skill)
        {
            state.AttackOrSkillCardsPlayedThisTurn++;
        }
    }

    private static void ApplyBlockNextTurn(CombatState state, Random? rng)
    {
        int blockNextTurn = BuffSystem.Get(state.PlayerBuffs, BuffId.BlockNextTurn);
        if (blockNextTurn <= 0)
        {
            return;
        }

        Effects.CardEffects.GainUnpoweredBlock(state, blockNextTurn, rng);
        BuffSystem.Remove(state.PlayerBuffs, BuffId.BlockNextTurn);
    }

    private static void TickTheBomb(CombatState state)
    {
        int turns = BuffSystem.Get(state.PlayerBuffs, BuffId.TheBombPower);
        if (turns <= 0)
        {
            return;
        }

        if (turns > 1)
        {
            BuffSystem.Apply(state.PlayerBuffs, BuffId.TheBombPower, -1);
            return;
        }

        Effects.CardEffects.DealUnpoweredDamageToAll(
            state,
            BuffSystem.Get(state.PlayerBuffs, BuffId.TheBombDamage)
        );
        BuffSystem.Remove(state.PlayerBuffs, BuffId.TheBombPower);
        BuffSystem.Remove(state.PlayerBuffs, BuffId.TheBombDamage);
    }

    private static EnemyState CreateEnemy(
        int defId,
        Random rng,
        Intent intent,
        bool stunned = false
    )
    {
        var def = GeneratedData.Enemies.Get(defId);
        int hp = rng.Next(def.MinHp, def.MaxHp + 1);
        var enemy = new EnemyState
        {
            DefId = defId,
            Hp = hp,
            MaxHp = hp,
            CurrentIntent = intent,
            Buffs = [],
        };
        if (stunned)
        {
            BuffSystem.Apply(enemy.Buffs, BuffId.Stunned, 1);
        }

        return enemy;
    }
}

public readonly record struct StepResult(bool Terminal, bool PlayerWon, float Reward)
{
    public static readonly StepResult Invalid = new(false, false, 0f);
}
