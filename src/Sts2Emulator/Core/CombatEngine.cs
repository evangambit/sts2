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

        // Anything queued before the player has acted goes off FIRST. Imbued's turn-1
        // auto-play is the only thing that queues at combat start, and it fires in the
        // game's pre-play phase — before the player's first move, not after it. The loop
        // further down drains what an action ITSELF queues; this drains what was already
        // waiting.
        DrainAutoPlayQueue(state, rng);

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
            // SurroundedPower.BeforeCardPlayed, and it fires BEFORE the card resolves.
            TurnPlayerTowardTarget(state, state.Hand[action]);
            result = PlayCard(state, action, rng);
        }
        else
        {
            int potionSlot = action - endTurnAction - 1;
            // SurroundedPower.BeforePotionUsed, the same rule for a targeted potion. The
            // potion's own target is not tracked, so this uses the aimed-at enemy.
            TurnPlayerTowardTarget(state, card: null);
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
            bool playerDead = PlayerIsDead(state);
            bool allDead = NoPrimaryEnemyLeft(state);
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
            || IsBlockedBySloth(state)
            || Effects.RelicEffects.BlocksFurtherCardPlays(state)
        )
        {
            return StepResult.Invalid;
        }

        state.CardsPlayedThisTurn++;
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
        // What this play actually cost, which is what CardPlay.Resources.EnergyValue
        // reports: an X card is printed at zero and takes the rest of the bar inside its
        // own effect, so the printed cost would tell a relic the play was free.
        int energySpent = def.HasEnergyCostX ? state.Energy : energyToSpend;
        state.Hand.RemoveAt(handIndex);
        if (def.Type == CardType.Skill && BuffSystem.Get(state.PlayerBuffs, BuffId.Smoggy) > 0)
        {
            state.SkillPlayedWhileSmoggy = true;
        }

        // FreeAttackPower / FreeSkillPower: consume one stack before the card effect runs
        // (BeforeCardPlayed timing).
        if (
            def.Type == CardType.Attack
            && BuffSystem.Get(state.PlayerBuffs, BuffId.FreeAttackPower) > 0
        )
        {
            BuffSystem.Apply(state.PlayerBuffs, BuffId.FreeAttackPower, -1);
        }
        else if (
            def.Type == CardType.Skill
            && BuffSystem.Get(state.PlayerBuffs, BuffId.FreeSkillPower) > 0
        )
        {
            BuffSystem.Apply(state.PlayerBuffs, BuffId.FreeSkillPower, -1);
        }

        ApplyEnchantmentOnPlay(state, card, rng);
        Effects.CardEffects.Apply(def, card.Upgraded, state, rng, card);
        int extraPlays =
            state.CardPlaysThisTurn < BuffSystem.Get(state.PlayerBuffs, BuffId.EchoForm) ? 1 : 0;

        // Spiral.EnchantPlayCount(original) => original + Times, and Times is 1.
        extraPlays += card.EnchantedWith(Enchantment.Spiral);

        // Hidden Gem's Replay rides on the copy it was granted to, the same way.
        extraPlays += card.ReplayCount;
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
            Effects.CardEffects.ExhaustCard(
                state,
                card with
                {
                    EnchantSpent = card.EnchantSpent || state.PlayedCardEnchantSpent,
                    EnchantAmount = card.EnchantAmount + (state.PlayedCardEnchantGrew ? 1 : 0),
                    CostBump = card.CostBump + state.PlayedCardCostBump,
                },
                rng: rng
            );
        }
        else if (feralReturn)
        {
            state.Hand.Add(
                card with
                {
                    FreeThisTurn = false,
                    BonusDamage = card.BonusDamage + state.PlayedCardBonusDamage,
                    EnchantSpent = card.EnchantSpent || state.PlayedCardEnchantSpent,
                    EnchantAmount = card.EnchantAmount + (state.PlayedCardEnchantGrew ? 1 : 0),
                    CostBump = card.CostBump + state.PlayedCardCostBump,
                }
            );
            BuffSystem.Apply(state.PlayerBuffs, BuffId.FeralUsed, 1);
        }
        else if (ShouldPlaceOnDrawPileAfterPlay(state, def))
        {
            state.TopDeck(
                card with
                {
                    FreeThisTurn = false,
                    BonusDamage = card.BonusDamage + state.PlayedCardBonusDamage,
                    EnchantSpent = card.EnchantSpent || state.PlayedCardEnchantSpent,
                    EnchantAmount = card.EnchantAmount + (state.PlayedCardEnchantGrew ? 1 : 0),
                    CostBump = card.CostBump + state.PlayedCardCostBump,
                }
            );
        }
        else
        {
            state.DiscardPile.Add(
                card with
                {
                    FreeThisTurn = false,
                    BonusDamage = card.BonusDamage + state.PlayedCardBonusDamage,
                    EnchantSpent = card.EnchantSpent || state.PlayedCardEnchantSpent,
                    EnchantAmount = card.EnchantAmount + (state.PlayedCardEnchantGrew ? 1 : 0),
                    CostBump = card.CostBump + state.PlayedCardCostBump,
                }
            );
        }

        state.PlayedCardBonusDamage = 0;
        state.PlayedCardEnchantSpent = false;
        state.PlayedCardEnchantGrew = false;
        state.PlayedCardCostBump = 0;
        IncrementPlayedCardTypeCounters(state, def);
        ApplyAfterCardPlayedPowers(state, def, rng, energySpent);
        Effects.RelicEffects.ApplyAfterPlayerHpChanged(state);

        bool playerDead = PlayerIsDead(state);
        bool allDead = NoPrimaryEnemyLeft(state);

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

        // TangledPower.AfterSideTurnEnd removes itself when its OWNER's side turn ends —
        // the player's. Removing it at the start of the player turn instead meant the Vine
        // Shambler applied it during the enemy turn and it was gone before a single card
        // could be taxed, which made the debuff do nothing at all.
        BuffSystem.Remove(state.PlayerBuffs, BuffId.Tangled);

        bool allDeadAfterEndTurnPowers = NoPrimaryEnemyLeft(state);
        if (allDeadAfterEndTurnPowers)
        {
            return new StepResult(
                Terminal: true,
                PlayerWon: true,
                Reward: ComputeReward(state, false, true, playerHpBefore, enemyHpsBefore)
            );
        }

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
        // Battle Trance's NoDrawPower only gags the turn it was played on.
        BuffSystem.Remove(state.PlayerBuffs, BuffId.NoDraw);

        // Both of these deal DAMAGE (CreatureCmd.Damage with ValueProp.Unpowered), so
        // block absorbs them. Taking it off HP directly is indistinguishable in a capture
        // that plays no cards and so holds no block, and wrong in every fight that does.
        int constrict = BuffSystem.Get(state.PlayerBuffs, BuffId.Constrict);
        if (constrict > 0)
        {
            Effects.CardEffects.DealDamageToPlayer(state, constrict);
        }

        int disintegration = BuffSystem.Get(state.PlayerBuffs, BuffId.Disintegration);
        if (disintegration > 0)
        {
            Effects.CardEffects.DealDamageToPlayer(state, disintegration);
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

            // Status card end-of-turn effects. Burn, Infection, Toxic and Wither all burn
            // their holder for the card's own damage value, so they read it from the card
            // rather than repeating four literals that the extractor already carries.
            if (Effects.CardEffects.BurnsHolderAtTurnEnd(def.Id))
            {
                Effects.CardEffects.DealDamageToPlayer(state, def.BaseDamage);
            }
            else if (def.Id == Effects.ST.Beckon)
            {
                // Beckon is unblockable -- but not uncappable: Intangible caps the HP
                // lost by any route, which is what its second hook is for.
                state.PlayerHp = Math.Max(
                    0,
                    state.PlayerHp - BuffSystem.CapHpLoss(6, state.PlayerBuffs)
                );
            }

            if (retainHand > 0 || card.IsRetained())
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

        Effects.CardEffects.KillDoomedEnemiesForTurnEnd(state);

        // ── Enemy turns ───────────────────────────────────────────────────────
        state.PlayerTurn = false;

        // SkittishPower.AfterSideTurnEnd fires for the side that is NOT its owner's, so
        // a gardener may flinch again once the player's turn is over.
        foreach (var enemy in state.Enemies)
        {
            BuffSystem.Remove(enemy.Buffs, BuffId.SkittishSpent);
        }
        // A reviving illusion is at 0 HP and still takes its turn -- that turn IS the
        // revive. It has to stay in the roster's own order while it does, so it comes back
        // where it stood rather than at one end.
        foreach (
            var enemy in state
                .Enemies.Where(e => e.Hp > 0 || BuffSystem.Get(e.Buffs, BuffId.Reviving) > 0)
                .ToArray()
        )
        {
            if (BuffSystem.Get(enemy.Buffs, BuffId.Reviving) > 0)
            {
                // IllusionPower.ReviveMove: Heal(MaxHp - CurrentHp), then back to the move
                // it was on. It cannot be hit while reviving, which the emulator gets for
                // free -- everything targets by "alive", and it is not yet.
                //
                // A Decimillipede segment reattaches for a FIXED amount instead, so it
                // comes back hurt: 25 of a 46-to-52 pool.
                //
                // The Test Subject comes back as a BIGGER creature, so its max HP moves
                // too, and it swaps powers on the way.
                BuffSystem.Remove(enemy.Buffs, BuffId.Reviving);
                if (enemy.DefId == KE.TestSubject)
                {
                    RespawnTestSubject(enemy, state.AscensionLevel);
                    continue;
                }

                int reattach = BuffSystem.Get(enemy.Buffs, BuffId.Reattach);
                enemy.Hp = reattach > 0 ? Math.Min(enemy.MaxHp, enemy.Hp + reattach) : enemy.MaxHp;
                // REATTACH_MOVE's FollowUpState is the machine's RandomBranchState, not
                // the cycle -- so a segment that comes back ROLLS its next move rather
                // than resuming where it fell. The Fogmog's eye returns to the move it was
                // on, which is why this is keyed on the reattach rather than on reviving.
                enemy.RollsNextMove = reattach > 0;
                continue;
            }

            // Poison damage at start of enemy turn.
            int poison = BuffSystem.Get(enemy.Buffs, BuffId.Poison);
            if (poison > 0)
            {
                // PoisonPower runs its own damage through Hook.ModifyDamage with the Cap
                // flag set, so an intangible creature loses 1 to poison however deep it is.
                enemy.Hp -= BuffSystem.CapHpLoss(poison, enemy.Buffs);
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

        // A Gas Bomb kills itself as it explodes and leaves the roster. Left in place at
        // zero HP it kept announcing an intent every turn, so a Living Fog fight grew an
        // extra attacker per Bloat where the live game shows one bomb appear and go.
        state.Enemies.RemoveAll(e => e.Hp <= 0 && e.DefId == KE.GasBomb);

        TickDurationDebuffs(state);
        EnemyAI.ToggleNemesisIntangible(state);

        HandleEnemyDeaths(state, enemyHpsBefore, rng);

        // The combat is over the moment the enemy phase leaves nobody standing.
        // CombatManager.ExecuteEnemyTurn awaits CheckWinCondition after EVERY enemy and
        // returns as soon as IsInProgress goes false, so the game never begins another
        // player turn -- while this ran the whole of one, drew a hand and reshuffled to
        // find it. That reshuffle is not free: Rng.Shuffle is a RUN-level stream, so a
        // fight whose last enemy dies (or, as a Fat Gremlin does, escapes) on the enemy
        // turn left it ahead of the game's by the size of a pile, and every hand dealt
        // for the rest of the run came off the wrong position. The last thing that can
        // add or remove an enemy is HandleEnemyDeaths -- Gremlin Merc reinforcements and
        // Phrog Parasite wrigglers both arrive there -- so the check belongs after it.
        bool playerDeadAfterEnemyTurn = PlayerIsDead(state);
        bool allDeadAfterEnemyTurn = NoPrimaryEnemyLeft(state);
        if (playerDeadAfterEnemyTurn || allDeadAfterEnemyTurn)
        {
            return new StepResult(
                Terminal: true,
                PlayerWon: allDeadAfterEnemyTurn && !playerDeadAfterEnemyTurn,
                Reward: ComputeReward(
                    state,
                    playerDeadAfterEnemyTurn,
                    allDeadAfterEnemyTurn,
                    playerHpBefore,
                    enemyHpsBefore
                )
            );
        }

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
        state.CardsPlayedThisTurn = 0;

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
            state.PlayerHp -= BuffSystem.CapHpLoss(playerPoison, state.PlayerBuffs);
            BuffSystem.Apply(state.PlayerBuffs, BuffId.Poison, -1);
            if (PlayerIsDead(state))
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

        // Draw five cards -- less MindRot, which is `Math.Max(0, count - Amount)` on the
        // whole draw rather than a per-card effect.
        Effects.CardEffects.DrawCards(
            state,
            Math.Max(
                0,
                5
                    + BuffSystem.Get(state.PlayerBuffs, BuffId.MachineLearning)
                    + Effects.RelicEffects.ExtraHandDraw(state)
                    - BuffSystem.Get(state.PlayerBuffs, BuffId.MindRot)
            ),
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
        AutoPlayHowlsFromExhaust(state, rng);

        // Enemies choose their next intent.
        EnemyAI.ChooseIntents(state.Enemies, state.Turn, rng, state.AiRng, state.AscensionLevel);
        Effects.RelicEffects.ApplyAfterPlayerHpChanged(state);

        bool playerDead = PlayerIsDead(state);
        bool allDead = NoPrimaryEnemyLeft(state);

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
    /// <summary>FairyInABottle, looked up by name rather than pinned to an id.</summary>
    private static readonly int FairyInABottlePotionId =
        GeneratedData.Potions.FindId("FairyInABottle")
        ?? throw new InvalidOperationException("No potion named FairyInABottle");

    /// <summary>
    /// Whether the player is really dead, after anything that refuses to let them be.
    /// </summary>
    /// <remarks>
    /// FairyInABottle is an Automatic potion whose <c>ShouldDie</c> returns false for its
    /// owner; <c>AfterPreventingDeath</c> then runs its <c>OnUse</c>, healing
    /// <c>Max(MaxHp * 0.3, 1)</c>. Nothing modelled it, so a run holding one died where
    /// the game had it stand back up -- a live capture shows the player go from 1 hp to
    /// 24 in the middle of a boss fight, 30% of an 80 maximum.
    /// </remarks>
    private static bool PlayerIsDead(CombatState state)
    {
        if (state.PlayerHp > 0)
        {
            return false;
        }

        int slot = Array.IndexOf(state.PotionSlots, FairyInABottlePotionId);
        if (slot < 0)
        {
            return true;
        }

        state.PotionSlots[slot] = 0;
        state.PlayerHp = Math.Max((int)(state.PlayerMaxHp * 0.3m), 1);
        return false;
    }

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

    /// <summary>
    /// <c>SlothPower.ShouldPlay</c>: <c>_cardsPlayedThisTurn &lt; Amount</c>.
    /// </summary>
    /// <remarks>
    /// A cap on the turn, not on the cards: nothing becomes Unplayable, the turn simply
    /// stops accepting plays once the count is reached. It counts EVERY card, which is
    /// why it reads the plain per-turn total rather than one of the typed ones.
    /// </remarks>
    private static bool IsBlockedBySloth(CombatState state)
    {
        int sloth = BuffSystem.Get(state.PlayerBuffs, BuffId.Sloth);
        return sloth > 0 && state.CardsPlayedThisTurn >= sloth;
    }

    private static int EffectiveMaxEnergy(CombatState state)
    {
        // WasteAwayPower.ModifyMaxEnergy subtracts its amount, so every turn starts short.
        return Math.Max(
            0,
            state.MaxEnergy
                + BuffSystem.Get(state.PlayerBuffs, BuffId.PyrePower)
                - BuffSystem.Get(state.PlayerBuffs, BuffId.WasteAway)
        );
    }

    /// <summary>Cost of a card in hand, for callers that hold no CardDef (relics).</summary>
    internal static int EffectiveCost(CardInstance card, CombatState state) =>
        EffectiveCost(card, GeneratedData.Cards.Get(card.DefId), state);

    // Returns the energy cost of a card after applying active powers (e.g. Corruption).
    /// <summary>
    /// The enchantment hooks that fire when a card is PLAYED, as EnchantmentModel.OnPlay.
    ///
    /// Sown and Swift each go off once and then set EnchantmentStatus.Disabled -- the
    /// spent flag here -- while Corrupted charges its 2 HP every single play. The flag is
    /// handed back through the state because CardEffects takes the card by value.
    /// </summary>
    private static void ApplyEnchantmentOnPlay(CombatState state, CardInstance card, Random rng)
    {
        switch (card.Enchantment)
        {
            case Enchantment.Sown when !card.EnchantSpent:
                state.Energy += card.EnchantAmount;
                state.PlayedCardEnchantSpent = true;
                break;
            case Enchantment.Swift when !card.EnchantSpent:
                Effects.CardEffects.DrawCards(state, card.EnchantAmount, rng);
                state.PlayedCardEnchantSpent = true;
                break;
            case Enchantment.Vigorous when !card.EnchantSpent:
                // Vigorous pays out through the damage calculation, and AfterCardPlayed
                // disables it whether or not the card actually attacked.
                state.PlayedCardEnchantSpent = true;
                break;
            case Enchantment.Corrupted:
                // Unblockable, unpowered, and every play -- not once.
                Effects.CardEffects.DealDamageToPlayer(state, 2);
                break;
            case Enchantment.Goopy:
                // AfterCardPlayed bumps the amount, and bumps the DECK version's too --
                // so a goopied Defend is worth one more block in every fight after this
                // one, for the rest of the run. The block itself is Amount - 1.
                state.PlayedCardEnchantGrew = true;
                break;
        }
    }

    private static int EffectiveCost(CardInstance card, CardDef def, CombatState state)
    {
        if (card.FreeThisTurn)
        {
            return 0;
        }

        // TezcatarasEmber.OnEnchant does `EnergyCost.UpgradeBy(-cost)` -- it zeroes the
        // card's printed cost for good, so this is not a "free this turn" flag.
        if (card.Enchantment == Enchantment.TezcatarasEmber)
        {
            return 0;
        }

        if (def.Type == CardType.Skill && BuffSystem.Get(state.PlayerBuffs, BuffId.Corruption) > 0)
        {
            return 0;
        }

        int cost = card.CostForCombat == int.MinValue ? def.Cost : card.CostForCombat;

        // The game says so on the card: base.EnergyCost.UpgradeBy(-1), extracted into
        // CardDef.UpgradeCost. This used to be three hand-written id lists covering
        // eighteen of the fifty-six cards that actually get cheaper, so Unmovable, Tank,
        // Corruption and the rest silently kept their unupgraded cost.
        if (card.Upgraded)
        {
            cost += def.UpgradeCost;
        }

        cost += Effects.RelicEffects.ExtraEnergyCost(state, def);

        if (def.Id == Effects.IC.Stomp)
        {
            cost -= state.AttackCardsPlayedThisTurn;
        }

        // FranticEscape's OnPlay ends with `base.EnergyCost.AddThisCombat(1)` -- on the
        // CARD, so only the copy that was played gets dearer. A player-wide counter made
        // every escape in the deck cost more the moment one was used, and a live capture
        // caught it as an energy shortfall: the game could still afford a Strike on turn
        // three where the emulator could not.
        cost += card.CostBump;

        if (def.Type == CardType.Attack)
        {
            cost += BuffSystem.Get(state.PlayerBuffs, BuffId.Tangled);
            if (BuffSystem.Get(state.PlayerBuffs, BuffId.FreeAttackPower) > 0)
            {
                return 0;
            }
        }

        if (
            def.Type == CardType.Skill
            && BuffSystem.Get(state.PlayerBuffs, BuffId.FreeSkillPower) > 0
        )
        {
            return 0;
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
                && !Effects.RelicEffects.BlocksFurtherCardPlays(state)
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
    /// <summary>
    /// <c>IChoosable.OnChosen</c> for each of the Knowledge Demon's four curses.
    /// </summary>
    /// <remarks>
    /// Every one applies a POWER rather than adding a card to the deck, which is why the
    /// emulator's old shape — a buff on the player — was right in kind and wrong in
    /// everything else: it applied Disintegration always, at a flat 6, with no choice.
    /// </remarks>
    private static void ApplyChosenCurse(CombatState state, int cardId, int disintegration)
    {
        switch (cardId)
        {
            case Effects.ST.Disintegration:
                // DynamicVars["DisintegrationPower"], which the demon overwrites per cast
                // from _disintegrationDamageValues -- 6, then 7, then 8.
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Disintegration, disintegration);
                break;
            case Effects.ST.MindRot:
                BuffSystem.Apply(state.PlayerBuffs, BuffId.MindRot, Run.RunConstants.MindRotAmount);
                break;
            case Effects.ST.Sloth:
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Sloth, Run.RunConstants.SlothAmount);
                break;
            case Effects.ST.WasteAway:
                BuffSystem.Apply(
                    state.PlayerBuffs,
                    BuffId.WasteAway,
                    Run.RunConstants.WasteAwayAmount
                );
                break;
        }
    }

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
                    state.TopDeck(card);
                }

                break;

            case CardSelectionKind.ExhaustFromHand:
            case CardSelectionKind.ExhaustFromHandThenDraw:
            case CardSelectionKind.ExhaustFromHandRepeated:
                if (index < state.Hand.Count)
                {
                    var card = state.Hand[index];
                    state.Hand.RemoveAt(index);
                    Effects.CardEffects.ExhaustCard(state, card, rng: rng);
                }

                if (selection.Kind == CardSelectionKind.ExhaustFromHandThenDraw)
                {
                    Effects.CardEffects.DrawCards(state, selection.Amount, rng);
                }

                // Purity asks again until its picks are spent or the hand empties.
                if (
                    selection.Kind == CardSelectionKind.ExhaustFromHandRepeated
                    && selection.Amount > 1
                    && state.Hand.Count > 0
                )
                {
                    Effects.CardEffects.ReopenExhaustSelection(
                        state,
                        selection.SourceCardDefId,
                        selection.Amount - 1
                    );
                }

                break;

            case CardSelectionKind.DrawPileToHand:
                if (
                    index < state.DrawPile.Count
                    && state.Hand.Count < Effects.CardEffects.MaxCardsInHand
                )
                {
                    var card = state.DrawPile[index];
                    state.RemoveFromDrawPileAt(index);
                    state.Hand.Add(card);
                }

                break;

            case CardSelectionKind.CurseOfKnowledge:
                if (index < selection.GeneratedCandidates.Count)
                {
                    ApplyChosenCurse(state, selection.GeneratedCandidates[index], selection.Amount);
                }

                break;

            case CardSelectionKind.GeneratedCardToHand:
                if (
                    index < selection.GeneratedCandidates.Count
                    && state.Hand.Count < Effects.CardEffects.MaxCardsInHand
                )
                {
                    state.Hand.Add(
                        new CardInstance(
                            selection.GeneratedCandidates[index],
                            false,
                            FreeThisTurn: true
                        )
                    );
                }

                break;

            case CardSelectionKind.HandToDrawPileTop:
                if (index < state.Hand.Count)
                {
                    var card = state.Hand[index];
                    state.Hand.RemoveAt(index);
                    state.TopDeck(card);
                }

                break;
        }

        return new StepResult(Terminal: false, PlayerWon: false, Reward: 0f);
    }

    /// <summary>
    /// Weak, Frail and Vulnerable count down once a round, AFTER the enemy side's turn —
    /// every one of them ticks in `AfterSideTurnEnd(side == CombatSide.Enemy)`. That
    /// timing is the whole point: an enemy attacking into the player's last point of
    /// Vulnerable still hits for 1.5x, and an enemy the player made Weak still swings
    /// weakened before the stack runs out. Ticking before the enemies act instead loses
    /// the final turn of every one of these debuffs.
    ///
    /// A debuff applied to the PLAYER also skips one tick — PowerCmd.Apply sets
    /// SkipNextDurationTick for any debuff landing on a player-side creature, which is
    /// what lets a Vulnerable applied during the enemy's turn still be at full value on
    /// the player's next turn. Enemies get no such grace. The skip is tracked by
    /// remembering what the player held when the round began: a stack that grew during
    /// the round was applied during it, so this tick is the one it skips.
    /// </summary>
    private static void TickDurationDebuffs(CombatState state)
    {
        foreach (var enemy in state.Enemies.ToArray())
        {
            BuffSystem.TickEndOfTurn(enemy.Buffs);
        }

        BuffSystem.TickEndOfTurn(state.PlayerBuffs, state.PlayerDebuffsAtRoundStart);
        state.PlayerDebuffsAtRoundStart = BuffSystem.DurationDebuffSnapshot(state.PlayerBuffs);
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

            // GremlinHorn.AfterDeath: EnergyVar(1) and CardsVar(1) for any creature that
            // dies on the other side. It checks only the SIDE -- not the
            // wasRemovalPrevented flag it is handed -- so it pays out even for a death
            // something else undoes, which is why it sits above the revive branches
            // rather than after them.
            if (Effects.RelicEffects.Has(state.Relics, Effects.RelicEffects.GremlinHorn))
            {
                state.Energy++;
                Effects.CardEffects.DrawCards(state, 1, rng);
            }

            if (BuffSystem.Get(state.Enemies[i].Buffs, BuffId.Surprise) > 0)
            {
                SpawnGremlinMercReinforcements(state, rng, state.Enemies[i].StolenGold);
            }
            else if (TryReviveIllusion(state.Enemies[i]))
            {
                continue;
            }
            else if (TryRespawnAxebot(state.Enemies[i], rng, state.AscensionLevel))
            {
                continue;
            }
            else if (TryReattachSegment(state, state.Enemies[i]))
            {
                continue;
            }
            else if (TryEnrageCrabPartner(state, state.Enemies[i]))
            {
                // Not a revive: the dead half stays dead, so this does not `continue`.
                TurnPlayerToFaceSurvivor(state);
            }

            if (state.Enemies[i].DefId == KE.TorchHeadAmalgam)
            {
                EnrageQueenIfWaitingToBurnBright(state);
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
                && !state.Enemies[i].Escaped
                && state.Enemies[i].HeistGold > 0
            )
            {
                // HeistPower.BeforeDeath adds an extra REWARD, and it does so in every
                // encounter -- the merc's own fight was excluded here, which is the one
                // fight the power exists for. ModifyGoldGained is applied when the row is
                // claimed, the way it is for the fight's ordinary gold.
                state.StolenBackGold += state.Enemies[i].HeistGold;
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
                // The readout changes the moment the ally dies: a live capture shows the
                // surviving slug announcing a Stun, not the attack it was going to make.
                // Leaving the old intent up told an agent to expect 12 damage from a
                // creature that is about to sit the turn out. Unknown is what a stunned
                // enemy already announces elsewhere (TerrorEel does it this way).
                enemy.CurrentIntent = new Intent(IntentType.Unknown, 0);
                enemy.SecondaryIntent = null;
            }
        }
    }

    /// <summary>
    /// <c>Creature.IsPrimaryEnemy</c>: an enemy that can stay alive on its own. The game's
    /// own words for the other kind are "a secondary enemy will automatically die unless
    /// there's also a living primary enemy", and what makes one secondary is carrying
    /// <c>MinionPower</c> or <c>IllusionPower</c>.
    /// </summary>
    private static bool IsPrimaryEnemy(EnemyState enemy) =>
        BuffSystem.Get(enemy.Buffs, BuffId.Minion) <= 0
        && BuffSystem.Get(enemy.Buffs, BuffId.Illusion) <= 0;

    /// <summary>
    /// Whether the combat is won, which <c>CombatManager.IsEnding</c> decides by asking
    /// whether any PRIMARY enemy is still alive.
    /// </summary>
    /// <remarks>
    /// Counting every creature instead is wrong in both directions. A Fogmog's eye revives
    /// forever, so a fight it outlives could never be won; and a Gas Bomb left over after
    /// the Living Fog dies would keep a finished fight running.
    /// </remarks>
    /// <summary>
    /// Whether the fight is won. A creature at 0 HP normally is not in it any more — but
    /// <c>AdaptablePower.ShouldStopCombatFromEnding</c> returns true, so a Test Subject
    /// waiting out its respawn turn still is.
    /// </summary>
    /// <remarks>
    /// Keyed on Adaptable rather than on Reviving, because Adaptable is the only power in
    /// the set that overrides that hook: an Eye With Teeth mid-revive is not a primary
    /// enemy at all, and a reattaching Decimillipede segment does NOT hold the fight open
    /// — emptying all three inside one window is how that elite is won.
    /// </remarks>
    private static bool NoPrimaryEnemyLeft(CombatState state) =>
        !state.Enemies.Any(enemy =>
            IsPrimaryEnemy(enemy)
            && (enemy.Hp > 0 || BuffSystem.Get(enemy.Buffs, BuffId.Adaptable) > 0)
        );

    /// <summary>
    /// <c>IllusionPower</c>: the owner is never removed from combat when it dies, keeps
    /// its buffs through the death, and spends its next turn on a forced REVIVE_MOVE that
    /// heals it back to full.
    /// </summary>
    /// <remarks>
    /// So a Fogmog's Eye With Teeth cannot be killed off -- swing at it and it is back at
    /// 6 HP a turn later, Distracting again. The emulator left it dead at 0, which is
    /// worse than a missing attacker: the live run's next swing at the eye resolved
    /// against the emulator's first LIVING enemy, so every blow the player spent on the
    /// illusion landed on the Fogmog instead and the fight ended floors early.
    /// </remarks>
    private static bool TryReviveIllusion(EnemyState enemy)
    {
        if (BuffSystem.Get(enemy.Buffs, BuffId.Illusion) <= 0)
        {
            return false;
        }

        if (BuffSystem.Get(enemy.Buffs, BuffId.Reviving) > 0)
        {
            return true;
        }

        BuffSystem.Apply(enemy.Buffs, BuffId.Reviving, 1);
        // A HealIntent, so the readout says what the turn will be spent on.
        enemy.CurrentIntent = new Intent(IntentType.Buff, 0);
        enemy.Block = 0;
        return true;
    }

    /// <summary>
    /// <c>ReattachPower</c>: a killed Decimillipede segment comes back one turn later.
    /// </summary>
    /// <remarks>
    /// <c>AfterDeath</c> puts it in DEAD_MOVE and <c>DoReattach</c> heals it by the
    /// power's Amount — but only <c>if (!AreAllOtherSegmentsDead())</c>. That guard IS
    /// the fight: the elite is won by emptying all three inside one window, and the
    /// emulator, which left a killed segment dead, let it be taken apart one at a time.
    ///
    /// It heals 25 rather than to full, which is what separates this from the Fogmog
    /// eye's revive — a segment that comes back is not a fresh one.
    /// </remarks>
    private static bool TryReattachSegment(CombatState state, EnemyState enemy)
    {
        if (BuffSystem.Get(enemy.Buffs, BuffId.Reattach) <= 0)
        {
            return false;
        }

        if (BuffSystem.Get(enemy.Buffs, BuffId.Reviving) > 0)
        {
            return true;
        }

        // The last one standing stays down, and takes the rest of the creature with it.
        bool anyOtherAlive = state.Enemies.Any(other =>
            !ReferenceEquals(other, enemy)
            && BuffSystem.Get(other.Buffs, BuffId.Reattach) > 0
            && (other.Hp > 0 || BuffSystem.Get(other.Buffs, BuffId.Reviving) > 0)
        );
        if (!anyOtherAlive)
        {
            return false;
        }

        BuffSystem.Apply(enemy.Buffs, BuffId.Reviving, 1);
        // A HealIntent, so the readout says what the turn is spent on.
        enemy.CurrentIntent = new Intent(IntentType.Buff, 0);
        enemy.Block = 0;
        return true;
    }

    /// <summary>
    /// <c>CrabRagePower.AfterDeath</c>: the surviving half takes Strength 6 and 99 block.
    /// </summary>
    /// <remarks>
    /// It fires for a death on its OWN side that is not its own, so killing one half of
    /// the Kaiser Crab enrages the other. None of it was modelled, which made halving the
    /// boss free — and the half left standing is the one the player then has to survive.
    /// </remarks>
    /// <summary>
    /// <c>Queen.AfterDeath</c>: when the Torch Head Amalgam dies and the Queen is alive,
    /// she stops burning bright — and if the move she has ALREADY announced is the one she
    /// was going to spend on her partner, <c>SetMoveImmediate(EnragedState)</c> replaces it
    /// there and then. So a player who kills the amalgam on the turn she declared
    /// BURN_BRIGHT_FOR_ME does not get a wasted enemy turn out of it; they get an enrage.
    /// </summary>
    /// <remarks>
    /// Every other consequence of the amalgam's death is read off the roster by
    /// <c>SelectIntent</c>, which is why only this one needs a hook: it is a change to an
    /// intent that has already been chosen, the same shape as Ravenous's stun.
    /// </remarks>
    private static void EnrageQueenIfWaitingToBurnBright(CombatState state)
    {
        foreach (var queen in state.Enemies)
        {
            if (queen.DefId != KE.Queen || queen.Hp <= 0 || queen.LastMove != QueenBurnBrightMove)
            {
                continue;
            }

            queen.LastMove = QueenEnrageMove;
            queen.CurrentIntent = new Intent(IntentType.Buff, 2);
        }
    }

    /// <summary>The Queen's own move numbering, as <c>EnemyAI.SelectIntent</c> assigns it.</summary>
    private const int QueenBurnBrightMove = 2;

    private const int QueenEnrageMove = 5;

    private static bool TryEnrageCrabPartner(CombatState state, EnemyState dead)
    {
        if (BuffSystem.Get(dead.Buffs, BuffId.CrabRage) <= 0)
        {
            return false;
        }

        bool enraged = false;
        foreach (var other in state.Enemies)
        {
            if (
                ReferenceEquals(other, dead)
                || other.Hp <= 0
                || BuffSystem.Get(other.Buffs, BuffId.CrabRage) <= 0
            )
            {
                continue;
            }

            BuffSystem.Apply(other.Buffs, BuffId.Strength, Run.RunConstants.CrabRageStrength);
            // BlockVar(99, ValueProp.Unpowered): Dexterity does not touch it.
            other.Block += Run.RunConstants.CrabRageBlock;
            enraged = true;
        }

        return enraged;
    }

    /// <summary>
    /// <c>SurroundedPower.BeforeCardPlayed</c>: the player turns to face what they aim at.
    /// </summary>
    /// <remarks>
    /// **This is the Kaiser Crab.** Whichever half you target, you turn to face it — and
    /// the OTHER half is then at your back, hitting for 1.5x. A live capture settles it:
    /// the Crusher opens at 18 against a base of 12 while the Rocket opens at its bare 3,
    /// and the moment the player's first card lands on the Crusher the bonus moves to
    /// the Rocket for the rest of the fight (27 for an 18 beam, 49 for a 33 laser).
    ///
    /// Modelling the turn on DEATH alone — which is where reading the decompiled source
    /// stopped — gets turn one right and every turn after it wrong.
    /// </remarks>
    private static void TurnPlayerTowardTarget(CombatState state, CardInstance? card)
    {
        int facing = BuffSystem.Get(state.PlayerBuffs, BuffId.Surrounded);
        if (facing == 0)
        {
            return;
        }

        // Only a card that AIMS at something turns the player: `cardPlay.Target != null`,
        // which is TargetType.AnyEnemy and nothing else. An AllEnemies attack -- a
        // Whirlwind, a Cleave -- performs no target selection and does NOT turn you.
        //
        // This was approximated as "is an attack" until the card table carried the real
        // TargetType, which is now extracted: 183 AnyEnemy, 35 AllEnemies, 9 Random.
        if (card is not null)
        {
            var def = GeneratedData.Cards.Get(Math.Abs(card.Value.DefId));
            if (def.Target != CardTarget.AnyEnemy)
            {
                return;
            }
        }

        // The aimed-at enemy, resolved the way every single-target effect resolves it:
        // the explicit target when there is one, else the first living enemy.
        int index = state.TargetEnemyIndex;
        var target =
            index >= 0 && index < state.Enemies.Count && state.Enemies[index].Hp > 0
                ? state.Enemies[index]
                : state.Enemies.FirstOrDefault(e => e.Hp > 0);
        if (target is null)
        {
            return;
        }

        FacePast(state, target, facing);
    }

    /// <summary>
    /// <c>SurroundedPower.UpdateDirection</c>: turn only if the target is the side the
    /// player's back is currently to.
    /// </summary>
    private static void FacePast(CombatState state, EnemyState target, int facing)
    {
        bool turn =
            facing == Run.RunConstants.FacingRight
                ? BuffSystem.Get(target.Buffs, BuffId.BackAttackLeft) > 0
                : BuffSystem.Get(target.Buffs, BuffId.BackAttackRight) > 0;
        if (!turn)
        {
            return;
        }

        BuffSystem.Remove(state.PlayerBuffs, BuffId.Surrounded);
        BuffSystem.Apply(
            state.PlayerBuffs,
            BuffId.Surrounded,
            facing == Run.RunConstants.FacingRight
                ? Run.RunConstants.FacingLeft
                : Run.RunConstants.FacingRight
        );
    }

    /// <summary>
    /// <c>SurroundedPower.AfterDeath</c>: the player turns to face whoever is left.
    /// </summary>
    /// <remarks>
    /// It only turns when every REMAINING hittable enemy is on one side — which for the
    /// Kaiser Crab means one half has died. Turning to face the survivor is what takes
    /// the 1.5x away from it, and it is why the multiplier cannot be a constant baked
    /// into the announced damage.
    /// </remarks>
    private static void TurnPlayerToFaceSurvivor(CombatState state)
    {
        int facing = BuffSystem.Get(state.PlayerBuffs, BuffId.Surrounded);
        if (facing == 0)
        {
            return;
        }

        var living = state.Enemies.Where(e => e.Hp > 0).ToArray();
        if (living.Length == 0)
        {
            return;
        }

        // Only when every remaining hittable enemy is on one side, which for this fight
        // means one half has died.
        bool allLeft = living.All(e => BuffSystem.Get(e.Buffs, BuffId.BackAttackLeft) > 0);
        bool allRight = living.All(e => BuffSystem.Get(e.Buffs, BuffId.BackAttackRight) > 0);
        if (allLeft || allRight)
        {
            FacePast(state, living[0], facing);
        }
    }

    private static bool TryRespawnAxebot(EnemyState enemy, Random rng, int ascension)
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
        var band = def.HpBand(ascension);
        enemy.Hp = rng.Next(band.Min, band.Max + 1);
        enemy.MaxHp = enemy.Hp;
        enemy.Block = 0;
        enemy.MoveIndex = 0;
        // A respawned Axebot is built with a stock override, which starts its machine on
        // BOOT_UP -- index 0 -- rather than on the HAMMER_UPPERCUT a fresh one opens with.
        enemy.CurrentIntent = new Intent(
            IntentType.Defend,
            Ascension.Value(ascension, Ascension.DeadlyEnemies, 15, 10)
        );
        BuffSystem.Apply(enemy.Buffs, BuffId.Stock, -1);
        return true;
    }

    /// <summary>
    /// <c>AdaptablePower.AfterDeath</c>: the Test Subject stops the combat ending, is not
    /// removed, and <c>TriggerDeadState</c> puts it in RESPAWN_MOVE — a state with
    /// <c>MustPerformOnceBeforeTransitioning</c>, so **the respawn costs it a turn**. The
    /// emulator used to heal it the instant it fell and announce an attack for the turn
    /// after, which handed the player neither the free turn nor the readout the game gives
    /// them. It is the Fogmog illusion's shape, and it uses the same machinery.
    /// </summary>
    private static bool TryRespawnTestSubject(EnemyState enemy)
    {
        if (enemy.DefId != KE.TestSubject || BuffSystem.Get(enemy.Buffs, BuffId.Adaptable) <= 0)
        {
            return false;
        }

        if (BuffSystem.Get(enemy.Buffs, BuffId.Reviving) > 0)
        {
            return true;
        }

        BuffSystem.Apply(enemy.Buffs, BuffId.Reviving, 1);
        // RESPAWN_MOVE declares a HealIntent and then a BuffIntent, so the readout is a
        // Buff — which is what the turn is spent on.
        enemy.CurrentIntent = new Intent(IntentType.Buff, 0);
        enemy.Block = 0;
        return true;
    }

    /// <summary>
    /// <c>RespawnMove</c> itself, on the enemy turn the revive is spent. Respawns 1 heals
    /// to SecondFormHp and takes PainfulStabs; respawns 2 heals to ThirdFormHp, takes
    /// Nemesis, and drops both of the earlier powers — which is what ends the second
    /// form's climbing Multi Claw.
    /// </summary>
    private static void RespawnTestSubject(EnemyState enemy, int ascension)
    {
        if (BuffSystem.Get(enemy.Buffs, BuffId.PainfulStabs) <= 0)
        {
            int second = Ascension.Value(ascension, Ascension.ToughEnemies, 212, 200);
            enemy.Hp = second;
            enemy.MaxHp = second;
            enemy.Block = 0;
            // MULTI_CLAW, whose hit count is read off MoveIndex - 2.
            enemy.MoveIndex = 2;
            BuffSystem.Apply(enemy.Buffs, BuffId.PainfulStabs, 1);
            return;
        }

        int third = Ascension.Value(ascension, Ascension.ToughEnemies, 313, 300);
        enemy.Hp = third;
        enemy.MaxHp = third;
        enemy.Block = 0;
        // PHASE3_LACERATE, the head of the third form's three-cycle.
        enemy.MoveIndex = 4;
        BuffSystem.Remove(enemy.Buffs, BuffId.Adaptable);
        BuffSystem.Remove(enemy.Buffs, BuffId.PainfulStabs);
        BuffSystem.Apply(enemy.Buffs, BuffId.Nemesis, 1);
    }

    private static void SpawnPhrogParasiteWrigglers(CombatState state, Random rng, EnemyState phrog)
    {
        int insertIndex = state.Enemies.IndexOf(phrog) + 1;
        for (int i = 0; i < 4 && state.Enemies.Count < 6; i++)
        {
            // The four do NOT act in step. Wriggler's INIT_MOVE is a conditional branch
            // on the creature's slot — wriggler1 and wriggler3 start on NASTY_BITE,
            // wriggler2 and wriggler4 on WRIGGLE — and they alternate from there, so the
            // pack always has half biting while the other half buys Strength. Spawning
            // them all on the same move made four bites land at once and then nothing.
            var wriggler = CreateEnemy(
                KE.Wriggler,
                rng,
                new Intent(IntentType.Unknown, 0),
                stunned: true,
                // SPAWNED_MOVE burns an index before INIT_MOVE reads the slot, so the
                // parity is inverted here: a wriggler that must OPEN on the bite starts
                // odd and lands on the bite once its stunned turn has ticked it over.
                moveIndex: (i + 1) % 2,
                state: state
            );
            state.Enemies.Insert(insertIndex + i, Effects.RelicEffects.Spawned(state, wriggler));
        }
    }

    private static void SpawnGremlinMercReinforcements(
        CombatState state,
        Random rng,
        int stolenGold
    )
    {
        // The HP roll goes through the run's Niche stream with the unique-HP rule, the
        // same as any other creature the game creates -- CombatState.CreateCreature calls
        // SetUniqueMonsterHpValue for EVERY enemy, spawned or not. Rolling off the combat
        // rng instead gave the merc's reinforcements a pair of numbers the game never
        // produced: a live capture splits it into a 15 and an 18, and this gave 12 and 17.
        // The sneaky gremlin is added before the fat one is rolled, so it is in the set
        // the fat one has to differ from.
        state.Enemies.Add(
            Effects.RelicEffects.Spawned(
                state,
                CreateEnemy(
                    78,
                    rng,
                    new Intent(IntentType.Unknown, 0),
                    stunned: true,
                    state.AscensionLevel,
                    state: state
                )
            )
        );
        var fatGremlin = CreateEnemy(
            28,
            rng,
            new Intent(IntentType.Unknown, 0),
            stunned: true,
            state.AscensionLevel,
            state: state
        );
        fatGremlin.HeistGold = stolenGold;
        // SurprisePower.AfterDeath marks the encounter only when the total taken is above
        // zero, and the mark is what separates "escaped with nothing" (half gold) from
        // "escaped with the loot" (none).
        state.MercGoldWasStolen |= stolenGold > 0;
        state.Enemies.Add(Effects.RelicEffects.Spawned(state, fatGremlin));
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

            // StampedePower picks with Rng.Shuffle.NextItem(items) — not the
            // card-selection stream, despite also choosing a card from hand.
            var stampedeRng = state.ShuffleRng ?? rng;
            int handIndex = attackIndexes[stampedeRng.Next(attackIndexes.Count)];
            AutoPlayCardFromHand(state, handIndex, rng);
        }
    }

    /// <summary>
    /// Howl From Beyond replays itself out of the exhaust pile.
    /// HowlFromBeyond.AfterAutoPostPlayPhaseEntered auto-plays the card whenever it is
    /// sitting in the owner's exhaust pile as the play phase begins, so exhausting it is
    /// not the end of it. The copy stays exhausted and fires again next turn.
    /// </summary>
    private static void AutoPlayHowlsFromExhaust(CombatState state, Random rng)
    {
        foreach (
            var card in state.ExhaustPile.Where(c => c.DefId == Effects.IC.HowlFromBeyond).ToList()
        )
        {
            if (NoPrimaryEnemyLeft(state))
            {
                return;
            }

            AutoPlay(state, card, rng);
        }
    }

    private static void AutoPlayMayhemCards(CombatState state, Random rng)
    {
        int mayhem = BuffSystem.Get(state.PlayerBuffs, BuffId.MayhemPower);
        for (int i = 0; i < mayhem && state.DrawPile.Count > 0; i++)
        {
            var card = state.DrawPile[0];
            state.RemoveFromDrawPileAt(0);
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

        // Same refusal as AutoPlayCore: a blocked card leaves hand for its result pile
        // without its effect running.
        if (Effects.RelicEffects.BlocksFurtherCardPlays(state))
        {
            state.Hand.RemoveAt(handIndex);
            if (ShouldExhaustAfterPlay(def, card))
            {
                Effects.CardEffects.ExhaustCard(state, card, rng: rng);
            }
            else
            {
                state.DiscardPile.Add(card with { FreeThisTurn = false });
            }

            return;
        }

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
            state.TopDeck(card with { FreeThisTurn = false });
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

    private static void ApplyAfterCardPlayedPowers(
        CombatState state,
        CardDef def,
        Random? rng,
        int energySpent = 0
    )
    {
        Effects.RelicEffects.ApplyAfterCardPlayed(state, def, rng, energySpent);

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

        if (def.Type == CardType.Skill)
        {
            // VitalSparkPower.AfterCardPlayed: a Skill carrying its Tainted affliction
            // stamps TaintedPower on the player. Read as the largest Vital Spark on the
            // board, the same way Galvanic is read above -- the affliction lands on the
            // CARD in the game, and modelling the card stamp rather than the board would
            // mean tracking an affliction per instance for one monster.
            int vitalSpark = state
                .Enemies.Where(e => e.Hp > 0)
                .Select(e => BuffSystem.Get(e.Buffs, BuffId.VitalSpark))
                .DefaultIfEmpty(0)
                .Max();
            if (vitalSpark > 0)
            {
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Tainted, vitalSpark);
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

        // Goopy.OnEnchant adds the Exhaust keyword to its card, so a goopied Defend
        // exhausts whatever its printed keywords say.
        if (card.Enchantment == Enchantment.Goopy)
        {
            return true;
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
    /// <summary>
    /// Play whatever is already waiting in the auto-play queue.
    /// </summary>
    /// <remarks>
    /// Separate from the loop that runs after an action because the two fire at different
    /// moments: that one handles what a card's own effect queued, this one handles what
    /// was queued before the player moved at all. Imbued is the only thing that does the
    /// latter, and it plays from the BOTTOM of the draw pile, where the turn-1 reorder
    /// put it.
    /// </remarks>
    private static void DrainAutoPlayQueue(CombatState state, Random rng)
    {
        while (state.AutoPlayQueue.Count > 0)
        {
            var next = state.AutoPlayQueue[0];
            state.AutoPlayQueue.RemoveAt(0);
            AutoPlay(state, next, rng);
            if (PlayerIsDead(state) || NoPrimaryEnemyLeft(state))
            {
                state.AutoPlayQueue.Clear();
                return;
            }
        }
    }

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

        // Hook.ShouldPlay gates auto-plays as well as chosen ones, and CardCmd.AutoPlay
        // answers a refusal with MoveToResultPileWithoutPlaying — the card is spent, its
        // effect never happens, and it does not count towards the limit that refused it.
        if (Effects.RelicEffects.BlocksFurtherCardPlays(state))
        {
            if (ShouldExhaustAfterPlay(def, card))
            {
                Effects.CardEffects.ExhaustCard(state, card, rng: rng);
            }
            else
            {
                state.DiscardPile.Add(card with { FreeThisTurn = false });
            }

            return;
        }

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
            state.TopDeck(card with { FreeThisTurn = false });
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
        bool stunned = false,
        int ascension = Ascension.DefaultLevel,
        int moveIndex = 0,
        // The combat, so the HP roll uses the stream the game uses. Without it the roll
        // falls back to the combat rng with no unique-HP rule.
        CombatState? state = null
    )
    {
        var def = GeneratedData.Enemies.Get(defId);
        var band = def.HpBand(ascension);
        int hp = EnemyAI.RollSummonedHp(band.Min, band.Max, state, rng);
        var enemy = new EnemyState
        {
            DefId = defId,
            Hp = hp,
            MaxHp = hp,
            CurrentIntent = intent,
            Buffs = [],
            MoveIndex = moveIndex,
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
