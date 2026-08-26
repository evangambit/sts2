namespace Sts2Emulator.Core.Effects;

public static class CardEffects
{
    /// <summary>
    /// Statuses that damage whoever is holding them when the turn ends. The game marks
    /// these with HasTurnEndInHandEffect and damages for the card's own damage value;
    /// Beckon also fires at turn end but loses HP directly, so it is handled separately.
    /// </summary>
    public static bool BurnsHolderAtTurnEnd(int defId) =>
        defId is ST.Burn or ST.Infection or ST.Toxic or ST.Wither;

    public static void Apply(
        CardDef def,
        bool upgraded,
        CombatState state,
        Random rng,
        CardInstance card = default
    )
    {
        switch (def.Id)
        {
            case IC.AscendersBane:
            case ST.Dazed:
            case ST.Infection:
            case ST.Burn:
            case ST.Disintegration:
            case ST.Wound:
            case ST.Wither:
            case ST.SpoilsMap:
                break;

            case ST.Slimed:
                DrawCards(state, 1, rng);
                break;

            // Toxic is playable and exhausts, and that is the whole point of it: its 5
            // damage is a turn-end-in-hand effect, so paying 1 to exhaust it is how you
            // avoid the damage rather than a way to take it.
            case ST.Toxic:
                break;

            case ST.Beckon:
            case ST.Debris:
            case ST.Enthralled:
                break;

            case ST.FranticEscape: // 1-cost status, increments Sandpit on enemy; cost increases per play
            {
                var target = FirstEnemy(state);
                if (target != null)
                {
                    BuffSystem.Apply(target.Buffs, BuffId.Sandpit, 1);
                }

                state.PlayedCardCostBump++;
                break;
            }

            // ── Ironclad Attacks ─────────────────────────────────────────────────

            case IC.Break: // 1-cost, 20/30 dmg + Vulnerable 5/7
            {
                var target = FirstEnemy(state);
                if (target != null)
                {
                    DealDamageToEnemy(state, target, Dmg(def, upgraded, card));
                    ApplyEnemyDebuffToTarget(
                        state,
                        target,
                        BuffId.Vulnerable,
                        upgraded ? 7 : 5,
                        rng
                    );
                }
                break;
            }

            case IC.Bludgeon: // 3-cost, 32/42 dmg
                DealDamage(state, Dmg(def, upgraded, card));
                break;

            case IC.Anger: // 0-cost, 6/8 dmg + add copy to discard
                DealDamage(state, Dmg(def, upgraded, card));
                state.DiscardPile.Add(new CardInstance(def.Id, upgraded));
                break;

            // ── Colourless cards that were falling through to the approximation ──

            case CL.ThrummingHatchet: // 1-cost, 11/14 dmg, and it comes back next turn
            {
                var target = FirstEnemy(state);
                if (target != null)
                {
                    DealDamageToEnemy(state, target, Dmg(def, upgraded, card));
                }

                // BeforeHandDraw puts it back in hand next turn if it was played this
                // one, which is the queue Feral already uses.
                state.ReturnToHandBeforeDraw.Add(card with { FreeThisTurn = false });

                break;
            }

            case CL.Fisticuffs: // 1-cost, 7/9 dmg, block equal to the damage DEALT
            {
                var target = FirstEnemy(state);
                if (target != null)
                {
                    // Block is what actually landed plus overkill, not the printed number
                    // -- so a Vulnerable target pays out more and a blocked one less.
                    int dealt = DealDamageToEnemy(state, target, Dmg(def, upgraded, card));
                    GainBlock(state, dealt, rng);
                }

                break;
            }

            case CL.Jackpot: // 3-cost, 25/30 dmg, then three free cards into hand
            {
                var target = FirstEnemy(state);
                if (target != null)
                {
                    DealDamageToEnemy(state, target, Dmg(def, upgraded, card));
                }

                AddZeroCostCardsToHand(state, 3, upgraded);
                break;
            }

            case CL.SeekerStrike: // 1-cost, 9/12 dmg, then one of three draw-pile cards
            {
                var target = FirstEnemy(state);
                if (target != null)
                {
                    DealDamageToEnemy(state, target, Dmg(def, upgraded, card));
                }

                OpenDrawPileSampleSelection(state, def.Id, 3);
                break;
            }

            case CL.Catastrophe: // 2-cost: auto-play 2/3 cards off the draw pile
            {
                AutoPlayFromDrawPile(state, upgraded ? 3 : 2);
                break;
            }

            case 546: // Cascade -- X-cost: auto-play X (+1 upgraded) off the draw pile
            {
                // Cascade used to grant Strength, which is a different card entirely.
                AutoPlayFromDrawPile(state, state.Energy + (upgraded ? 1 : 0));
                state.Energy = 0;
                break;
            }

            case CL.BeatDown: // 3-cost: auto-play 3/4 Attacks out of the discard pile
            {
                AutoPlayAttacksFromDiscard(state, upgraded ? 4 : 3);
                break;
            }

            case CL.HiddenGem: // 1-cost: a draw-pile card gains Replay 2/3
            {
                GrantReplayToADrawPileCard(state, upgraded ? 3 : 2);
                break;
            }

            case IC.Bash: // 2-cost, 8/10 dmg + Vulnerable 2/3
            {
                var target = FirstEnemy(state);
                if (target != null)
                {
                    DealDamageToEnemy(state, target, Dmg(def, upgraded, card));
                    ApplyEnemyDebuffToTarget(
                        state,
                        target,
                        BuffId.Vulnerable,
                        upgraded ? 3 : 2,
                        rng
                    );
                }
                break;
            }

            case IC.BodySlam: // 1/0-cost, dmg = player's current block
                DealDamage(state, state.PlayerBlock);
                break;

            case IC.IronWave: // 1-cost, gain 5/7 block, then deal 5/7 damage
                GainBlock(state, Blk(def, upgraded, card), rng);
                DealDamage(state, Dmg(def, upgraded, card));
                break;

            case IC.Breakthrough: // 1-cost, lose 1 HP + 9/13 dmg to ALL enemies
                LoseHp(state, 1);
                DealDamageToAll(state, Dmg(def, upgraded, card));
                break;

            case IC.AshenStrike: // 1-cost, 6 + 3/4 per exhausted card
                DealDamage(state, 6 + state.ExhaustPile.Count * (upgraded ? 4 : 3));
                break;

            case IC.Bully: // 0-cost, 4 + 2/3 * target's Vulnerable stacks
            {
                var t = FirstEnemy(state);
                int vuln = t != null ? BuffSystem.Get(t.Buffs, BuffId.Vulnerable) : 0;
                DealDamage(state, 4 + (upgraded ? 3 : 2) * vuln);
                break;
            }

            case IC.Cinder: // 2-cost, 18/24 dmg + exhaust a random card from hand
                DealDamage(state, Dmg(def, upgraded, card));
                ExhaustRandomCardFromHand(state, rng);
                break;

            case IC.Conflagration: // 1-cost, 2 dmg × 4/5 hits to ALL enemies
                DealDamageToAllMultiHit(state, 2, upgraded ? 5 : 4);
                break;

            case CL.DramaticEntrance: // 0-cost, 11/15 damage to ALL enemies, exhaust
                DealDamageToAll(state, Dmg(def, upgraded, card));
                break;

            case CL.Bolas: // 0-cost, 3/4 damage; returns to hand before next turn's draw
                DealDamage(state, Dmg(def, upgraded, card));
                break;

            case CL.DarkShackles: // 0-cost, enemy loses 9/15 Strength this turn, exhaust
                ApplyTemporaryStrengthDownToEnemy(state, upgraded ? 15 : 9);
                break;

            case CL.Volley: // X-cost, 10/14 damage X times to random enemies
            {
                int x = state.Energy;
                state.Energy = 0;
                DealDamageToRandomEnemiesMultiHit(state, Dmg(def, upgraded, card), x, rng);
                break;
            }

            case CL.Omnislice: // 0-cost, 8/11 damage + splash effective first-hit damage to other enemies
                DealOmnislice(state, Dmg(def, upgraded, card));
                break;

            case CL.Prolong: // 0-cost, gain current block again next turn; upgrade removes exhaust
                BuffSystem.Apply(state.PlayerBuffs, BuffId.BlockNextTurn, state.PlayerBlock);
                break;

            case CL.Salvo: // 1-cost, 12/16 damage + retain remaining hand this turn
                DealDamage(state, Dmg(def, upgraded, card));
                BuffSystem.Apply(state.PlayerBuffs, BuffId.RetainHand, 1);
                break;

            case AN.NeowsFury: // 1-cost, 10/14 damage + move 2/3 discard cards to hand, exhaust
                DealDamage(state, Dmg(def, upgraded, card));
                MoveDiscardCardsToHand(state, upgraded ? 3 : 2);
                break;

            case IC.Dismantle: // 1-cost, 8/10 dmg, hits twice if target is Vulnerable
            {
                var t = FirstEnemy(state);
                int hits = (t != null && BuffSystem.Get(t.Buffs, BuffId.Vulnerable) > 0) ? 2 : 1;
                DealDamageMultiHit(state, Dmg(def, upgraded, card), hits, rng);
                break;
            }

            case IC.FiendFire: // 2-cost, exhaust hand, deal 7/10 dmg per card exhausted
            {
                int count = state.Hand.Count;
                while (state.Hand.Count > 0)
                {
                    ExhaustCard(state, state.Hand[0], rng: rng);
                    state.Hand.RemoveAt(0);
                }
                DealDamageMultiHit(state, Dmg(def, upgraded, card), count, rng);
                break;
            }

            case IC.FightMe: // 2-cost, 5/6 dmg twice, gain 3/4 Strength, enemy gains 1 Strength
                DealDamageMultiHit(state, Dmg(def, upgraded, card), 2, rng);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, upgraded ? 4 : 3);
                ApplyEnemyDebuff(state, BuffId.Strength, 1, rng);
                break;

            case IC.Headbutt: // 1-cost, 9/12 dmg + put a chosen discarded card on top of draw
                DealDamage(state, Dmg(def, upgraded, card));
                if (state.DiscardPile.Count > 0)
                {
                    // CardSelectCmd.FromCombatPile(Discard) — the player picks.
                    OpenCardSelection(
                        state,
                        CardSelectionKind.DiscardToDrawPileTop,
                        state.DiscardPile.Count,
                        def.Id,
                        autoPick: state.DiscardPile.Count - 1
                    );
                }
                break;

            case IC.Hemokinesis: // 1-cost, lose 2 HP then deal 15/20 dmg
                LoseHp(state, 2);
                DealDamage(state, Dmg(def, upgraded, card));
                break;

            case IC.MoltenFist: // 1-cost, 10/14 dmg + reapply target's Vulnerable if it survives
            {
                var target = FirstEnemy(state);
                if (target != null)
                {
                    DealDamageToEnemy(state, target, Dmg(def, upgraded, card));
                    int vulnerable =
                        target.Hp > 0 ? BuffSystem.Get(target.Buffs, BuffId.Vulnerable) : 0;
                    if (vulnerable > 0)
                    {
                        int before = vulnerable;
                        BuffSystem.Apply(target.Buffs, BuffId.Vulnerable, vulnerable);
                        DrawForVicious(
                            state,
                            BuffId.Vulnerable,
                            before,
                            BuffSystem.Get(target.Buffs, BuffId.Vulnerable),
                            rng
                        );
                    }
                }
                break;
            }

            case IC.Feed: // 1-cost, 10/12 dmg; if kills gain 3/4 max HP; exhaust
            {
                var feedTarget = FirstEnemy(state);
                if (feedTarget != null)
                {
                    DealDamageToEnemy(state, feedTarget, Dmg(def, upgraded, card));
                    if (feedTarget.Hp <= 0)
                    {
                        state.PlayerMaxHp += upgraded ? 4 : 3;
                    }
                }
                break;
            }

            case IC.Mangle: // 3-cost, 15/20 dmg + enemy loses Strength 10/15 this turn
                DealDamage(state, Dmg(def, upgraded, card));
                ApplyTemporaryStrengthDownToEnemy(state, upgraded ? 15 : 10);
                break;

            case IC.HowlFromBeyond: // 3-cost, 16/21 dmg to ALL enemies
                DealDamageToAll(state, Dmg(def, upgraded, card));
                break;

            case IC.PactsEnd: // 0-cost, 17/23 dmg to ALL enemies if 3+ cards are exhausted
                // OnPlay is wrapped in CanDealDamage, which is
                // CardPile.GetCards(Exhaust).Count() >= CardsVar(3). Below that the card
                // does nothing at all; the emulator used to swing regardless.
                if (state.ExhaustPile.Count >= 3)
                {
                    DealDamageToAll(state, Dmg(def, upgraded, card));
                }
                break;

            case IC.Pillage: // 1-cost, 6/9 dmg + draw until drawing a non-Attack
                DealDamage(state, Dmg(def, upgraded, card));
                DrawUntilNonAttack(state, rng);
                break;

            case IC.PerfectedStrike: // 2-cost, 6 + 2/3 per Strike card in all piles
                DealDamage(
                    state,
                    6
                        + (CountStrikeCards(state) + (def.Name.Contains("Strike") ? 1 : 0))
                            * (upgraded ? 3 : 2)
                );
                break;

            case IC.PommelStrike: // 1-cost, 9/10 dmg + draw 1/2
                DealDamage(state, Dmg(def, upgraded, card));
                DrawCards(state, upgraded ? 2 : 1, rng);
                break;

            case IC.PrimalForce: // 0-cost, transform all Attacks in hand to GiantRocks
            {
                for (int i = 0; i < state.Hand.Count; i++)
                {
                    var handCard = state.Hand[i];
                    if (GeneratedData.Cards.Get(handCard.DefId).Type == CardType.Attack)
                    {
                        state.Hand[i] = new CardInstance(IC.GiantRock, upgraded);
                    }
                }
                break;
            }

            case IC.SetupStrike: // 1-cost, 7/9 dmg + 2/3 temporary Strength
            {
                DealDamage(state, Dmg(def, upgraded, card));
                int strength = upgraded ? 3 : 2;
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, strength);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.TemporaryStrength, strength);
                break;
            }

            case IC.Spite: // 0-cost, 5 dmg; 2/3 hits if the player lost HP this turn
                DealDamageMultiHit(
                    state,
                    Dmg(def, upgraded, card),
                    state.PlayerHpLostThisTurn > 0 ? (upgraded ? 3 : 2) : 1,
                    rng
                );
                break;

            case IC.Stomp: // 3-cost, reduced by Attacks played this turn, 12/15 dmg to ALL enemies
                DealDamageToAll(state, Dmg(def, upgraded, card));
                break;

            case IC.Stoke: // 1-cost, exhaust hand and add random cards
            {
                int count = state.Hand.Count;
                foreach (var handCard in state.Hand.ToArray())
                {
                    ExhaustCard(state, handCard, rng: rng);
                }

                state.Hand.Clear();
                for (int i = 0; i < count; i++)
                {
                    if (state.Hand.Count < MaxCardsInHand)
                    {
                        int defId = _ironcladPool[
                            CardGenerationRng(state, rng).Next(_ironcladPool.Length)
                        ];
                        state.Hand.Add(new CardInstance(defId, upgraded));
                    }
                }
                break;
            }

            case IC.SwordBoomerang: // 1-cost, 3 dmg × 3/4 hits to random enemies
                // TargetingRandomOpponents, and AttackCommand re-rolls the target inside
                // its per-hit loop — so each hit picks again, rather than the card picking
                // one enemy and hitting it N times.
                DealDamageToRandomEnemiesMultiHit(state, 3, upgraded ? 4 : 3, rng);
                break;

            case IC.Tank: // 1/0-cost, apply TankPower (multiplayer only)
                break;

            case IC.Thunderclap: // 1-cost, 4/7 dmg to ALL + Vulnerable 1 to ALL
                DealDamageToAll(state, Dmg(def, upgraded, card));
                ApplyAllEnemyDebuff(state, BuffId.Vulnerable, 1, rng);
                break;

            case IC.Unmovable: // 2/1-cost, double first block gain each turn
                BuffSystem.Apply(state.PlayerBuffs, BuffId.UnmovablePower, 1);
                break;

            case IC.TwinStrike: // 1-cost, 5/7 dmg × 2 hits
                DealDamageMultiHit(state, Dmg(def, upgraded, card), 2, rng);
                break;

            case IC.Unrelenting: // 2-cost, 14/20 dmg + FreeAttackPower 1 (next Attack costs 0)
                DealDamage(state, Dmg(def, upgraded, card));
                BuffSystem.Apply(state.PlayerBuffs, BuffId.FreeAttackPower, 1);
                break;

            case IC.Rampage: // 1-cost, 9 dmg, and this copy permanently gains 5/9 more
                // OnPlay hits for the card's CURRENT damage and then raises its own
                // DamageVar by DynamicVar("Increase", 5m), OnUpgrade +4. The growth lives
                // on the copy, so it survives into the discard pile and comes back around
                // with it; a second Rampage in the deck grows on its own schedule.
                DealDamage(state, Dmg(def, upgraded, card) + card.BonusDamage);
                state.PlayedCardBonusDamage += upgraded ? 9 : 5;
                break;

            case IC.TearAsunder: // 2-cost, 5/7 dmg × (1 + unblocked damage hits received this combat)
            {
                int hits = 1 + state.UnblockedDamageHitCount;
                DealDamageMultiHit(state, Dmg(def, upgraded, card), hits, rng);
                break;
            }

            case IC.Thrash: // 1-cost, 4/6 dmg × 2 + exhaust a random Attack from hand
                DealDamageMultiHit(state, Dmg(def, upgraded, card), 2, rng);
                ExhaustRandomCardOfTypeFromHand(state, CardType.Attack, rng);
                break;

            case IC.Uppercut: // 2-cost, 13/13 dmg + Weak 1/2 + Vulnerable 1/2
                DealDamage(state, Dmg(def, upgraded, card));
                ApplyEnemyDebuff(state, BuffId.Weak, upgraded ? 2 : 1, rng);
                ApplyEnemyDebuff(state, BuffId.Vulnerable, upgraded ? 2 : 1, rng);
                break;

            case IC.Whirlwind: // X-cost, 5/8 dmg × (energy spent) to ALL enemies
            {
                int x = state.Energy;
                state.Energy = 0;
                DealDamageToAllMultiHit(state, Dmg(def, upgraded, card), x);
                break;
            }

            // ── Ironclad Skills ──────────────────────────────────────────────────

            case IC.Armaments: // 1-cost, gain 5 block + upgrade 1 card/all cards if upgraded
                GainBlock(state, Blk(def, upgraded, card), rng);
                if (upgraded)
                {
                    UpgradeAllCardsInHand(state);
                }
                else
                {
                    UpgradeFirstCardInHand(state);
                }

                break;

            case IC.Brand: // 0-cost, lose 1 HP, exhaust a CHOSEN card, gain 1/2 Strength
                LoseHp(state, 1);
                // The game applies the Strength after the exhaust resolves. Nothing reads
                // Strength during an exhaust, so granting it first is observationally the
                // same and keeps the pending selection as the last thing this play does.
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, upgraded ? 2 : 1);
                if (state.Hand.Count > 0)
                {
                    OpenCardSelection(
                        state,
                        CardSelectionKind.ExhaustFromHand,
                        state.Hand.Count,
                        def.Id,
                        autoPick: CardSelectionRng(state, rng).Next(state.Hand.Count)
                    );
                }
                break;

            case IC.BattleTrance: // 0-cost, draw 3/4, then no more drawing this turn
                DrawCards(state, upgraded ? 4 : 3, rng);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.NoDraw, 1);
                break;

            case IC.BloodWall: // 2-cost, lose 2 HP + gain 16/20 block
                LoseHp(state, 2);
                GainBlock(state, Blk(def, upgraded, card), rng);
                break;

            case IC.Bloodletting: // 0-cost, lose 3 HP + gain 2/3 energy
                LoseHp(state, 3);
                state.Energy += upgraded ? 3 : 2;
                break;

            case IC.BurningPact: // 1-cost, exhaust a chosen card, then draw 2/3
                // CardSelectCmd.FromHand then CardPileCmd.Draw. The draw must follow the
                // choice, or the cards it draws become candidates for their own exhaust.
                if (state.Hand.Count > 0)
                {
                    OpenCardSelection(
                        state,
                        CardSelectionKind.ExhaustFromHandThenDraw,
                        state.Hand.Count,
                        def.Id,
                        autoPick: CardSelectionRng(state, rng).Next(state.Hand.Count),
                        amount: upgraded ? 3 : 2
                    );
                }

                if (state.PendingSelection is null)
                {
                    DrawCards(state, upgraded ? 3 : 2, rng);
                }
                break;

            case IC.Colossus: // 1-cost, gain 5/8 block; Vulnerable enemies deal half attack damage this turn
                GainBlock(state, Blk(def, upgraded, card), rng);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Colossus, 1);
                break;

            case IC.Dominate: // 1-cost, Vulnerable 1/2 to enemy, gain Strength = its Vulnerable
            {
                // PowerVar<VulnerablePower>(1m) with OnUpgrade UpgradeValueBy(1m); this
                // used to apply 1 whether or not the card was upgraded.
                ApplyEnemyDebuff(state, BuffId.Vulnerable, upgraded ? 2 : 1, rng);
                var t = FirstEnemy(state);
                if (t != null)
                {
                    BuffSystem.Apply(
                        state.PlayerBuffs,
                        BuffId.Strength,
                        BuffSystem.Get(t.Buffs, BuffId.Vulnerable)
                    );
                }

                break;
            }

            case IC.DrumOfBattle: // 1-cost, draw 2; on self-exhaust gain 2/3 energy
                DrawCards(state, 2, rng);
                break;

            case IC.EvilEye: // 1-cost, gain block twice if a card exhausted this turn
                GainBlock(state, Blk(def, upgraded, card), rng);
                if (state.CardsExhaustedThisTurn > 0)
                {
                    GainBlock(state, Blk(def, upgraded, card), rng);
                }

                break;

            case IC.ExpectAFight: // 2/1-cost, gain 1 energy per Attack in hand
            {
                int attackCount = state.Hand.Count(card =>
                    GeneratedData.Cards.Get(card.DefId).Type == CardType.Attack
                );
                state.Energy += attackCount;
                break;
            }

            case IC.FlameBarrier: // 2-cost, 12/16 block + FlameBarrier 4/6
                GainBlock(state, Blk(def, upgraded, card), rng);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.FlameBarrier, upgraded ? 6 : 4);
                break;

            case IC.ForgottenRitual: // 1-cost, gain 3/4 energy only if a card exhausted this turn
                if (state.CardsExhaustedThisTurn > 0)
                {
                    state.Energy += upgraded ? 4 : 3;
                }

                break;

            case IC.Havoc: // 1/0-cost, play top card of draw pile and exhaust it
            {
                if (state.DrawPile.Count > 0)
                {
                    var top = state.DrawPile[0];
                    state.RemoveFromDrawPileAt(0);
                    var topDef = GeneratedData.Cards.Get(top.DefId);
                    PlayNestedCard(topDef, top.Upgraded, state, rng);
                    ExhaustCard(state, top, rng: rng);
                }
                break;
            }

            case IC.InfernalBlade: // 1/0-cost, add a random Ironclad Attack to hand free this turn
                AddRandomInfernalBladeAttack(state, rng);
                break;

            case IC.NotYet: // 2-cost, heal 10/13 HP
                state.PlayerHp = Math.Min(state.PlayerHp + (upgraded ? 13 : 10), state.PlayerMaxHp);
                break;

            case IC.OneTwoPunch: // 1-cost, the next 1/2 Attack cards are played twice this turn
                BuffSystem.Apply(state.PlayerBuffs, BuffId.OneTwoPunch, upgraded ? 2 : 1);
                break;

            case IC.Offering: // 0-cost, lose 6 HP + gain 2 energy + draw 3/5
                LoseHp(state, 6);
                state.Energy += 2;
                DrawCards(state, upgraded ? 5 : 3, rng);
                break;

            case IC.SecondWind: // 1-cost, exhaust non-Attacks, gain 5/7 block per
            {
                int blockEach = upgraded ? 7 : 5;
                var nonAtk = state
                    .Hand.Where(c => GeneratedData.Cards.Get(c.DefId).Type != CardType.Attack)
                    .ToList();
                foreach (var c in nonAtk)
                {
                    state.Hand.Remove(c);
                    ExhaustCard(state, c, rng: rng);
                    GainBlock(state, blockEach, rng);
                }
                break;
            }

            case IC.Restlessness: // 0-cost, if this was the only card in hand, draw and gain energy
                if (state.Hand.Count == 0)
                {
                    DrawCards(state, upgraded ? 3 : 2, rng);
                    state.Energy += upgraded ? 3 : 2;
                }
                break;

            case IC.ShrugItOff: // 1-cost, 8/11 block + draw 1
                GainBlock(state, Blk(def, upgraded, card), rng);
                DrawCards(state, 1, rng);
                break;

            case IC.UltimateDefend: // 1-cost, 11/15 block
                GainBlock(state, Blk(def, upgraded, card), rng);
                break;

            case IC.Impervious: // 2-cost, 30/40 block + exhaust (Exhaust handled by CardDef)
                GainBlock(state, Blk(def, upgraded, card), rng);
                break;

            case IC.Splash: // 1-cost, approximate generated off-character attack with a free Strike
                state.Hand.Add(new CardInstance(IC.StrikeIronclad, upgraded));
                break;

            case IC.Stampede: // 2/1-cost, auto-play random Attacks at play-phase start (tracked)
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Stampede, 1);
                break;

            case IC.Taunt: // 1-cost, 7/8 block + Vulnerable 1/2 to enemy
                // OnUpgrade raises BOTH the block and the Vulnerable by 1; the debuff
                // used to stay at 1.
                GainBlock(state, Blk(def, upgraded, card), rng);
                ApplyEnemyDebuff(state, BuffId.Vulnerable, upgraded ? 2 : 1, rng);
                break;

            case IC.Tremble: // 1-cost, Vulnerable 3/4 to enemy
                ApplyEnemyDebuff(state, BuffId.Vulnerable, upgraded ? 4 : 3, rng);
                break;
            case IC.TrueGrit: // 1-cost, gain 7/9 block; exhaust a card (chosen when upgraded)
            {
                GainBlock(state, Blk(def, upgraded, card), rng);
                if (state.Hand.Count > 0)
                {
                    if (upgraded)
                    {
                        // CardSelectCmd.FromHand — upgraded lets the player pick.
                        OpenCardSelection(
                            state,
                            CardSelectionKind.ExhaustFromHand,
                            state.Hand.Count,
                            def.Id,
                            autoPick: CardSelectionRng(state, rng).Next(state.Hand.Count)
                        );
                    }
                    else
                    {
                        // Unupgraded stays random: Rng.CombatCardSelection.NextItem(hand).
                        int index = CardSelectionRng(state, rng).Next(state.Hand.Count);
                        var c = state.Hand[index];
                        state.Hand.RemoveAt(index);
                        ExhaustCard(state, c, rng: rng);
                    }
                }
                break;
            }

            // ── Ironclad Power Cards ─────────────────────────────────────────────

            case IC.Barricade: // 3/2-cost, block no longer expires
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Barricade, 1);
                break;

            case IC.Aggression: // 1-cost, start of turn add a random upgraded Ironclad card (Innate when upgraded)
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Aggression, 1);
                break;

            case IC.Corruption: // 3/2-cost, Skills cost 0 and exhaust
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Corruption, 1);
                break;

            case IC.CrimsonMantle: // 1-cost, start of turn lose N HP and gain 8/10 block; increment N when played
                BuffSystem.Apply(state.PlayerBuffs, BuffId.CrimsonMantleSelfDamage, 1);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.CrimsonMantleBlock, upgraded ? 10 : 8);
                break;

            case IC.Pyre: // 2-cost, gain 1/2 Max Energy
                BuffSystem.Apply(state.PlayerBuffs, BuffId.PyrePower, upgraded ? 2 : 1);
                break;

            case IC.Cruelty: // 1-cost, increase Vulnerable multiplier by 25/50%
                BuffSystem.Apply(state.PlayerBuffs, BuffId.CrueltyPower, upgraded ? 50 : 25);
                break;

            case IC.DarkEmbrace: // 2-cost, draw 1 card when a card is exhausted (upgraded costs 1)
                BuffSystem.Apply(state.PlayerBuffs, BuffId.DarkEmbrace, 1);
                break;

            case IC.DemonicShield: // 0-cost, lose 1 HP, double target ally's block (self in SP)
                LoseHp(state, 1);
                GainBlock(state, state.PlayerBlock, rng);
                break;

            case IC.DemonForm: // 3-cost, gain 2/3 Strength each player turn start
                BuffSystem.Apply(state.PlayerBuffs, BuffId.DemonForm, upgraded ? 3 : 2);
                break;

            case IC.FeelNoPain: // 1-cost, gain 3/4 block when exhausting cards
                BuffSystem.Apply(state.PlayerBuffs, BuffId.FeelNoPain, upgraded ? 4 : 3);
                break;

            case IC.Hellraiser: // 2-cost, whenever you draw a Strike, play it (upgraded costs 1)
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Hellraiser, 1);
                break;

            case IC.Inflame: // 1-cost, immediately gain 2/3 Strength
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, upgraded ? 3 : 2);
                break;

            case IC.Inferno: // 1-cost, self-damage each turn; taking unblocked self-damage burns all enemies
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Inferno, upgraded ? 9 : 6);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.InfernoSelfDamage, 1);
                break;

            case IC.Juggernaut: // 2-cost, deal 6/8 dmg when gaining block
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Juggernaut, upgraded ? 8 : 6);
                break;

            case IC.Juggling: // 1-cost, copy the third Attack played each turn into hand
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Juggling, 1);
                break;

            case IC.Nostalgia: // 1/0-cost, first Attack/Skill each turn goes on top of draw pile
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Nostalgia, 1);
                break;

            case IC.Rage: // 0-cost, gain 3/5 block when playing an Attack this turn
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Rage, upgraded ? 5 : 3);
                break;

            case IC.Rupture: // 1-cost, gain 1/2 Strength when losing HP via card effects
                BuffSystem.Apply(state.PlayerBuffs, BuffId.RupturePower, upgraded ? 2 : 1);
                break;

            case IC.StoneArmor: // 1-cost, gain 4/6 Plating (block each end of turn, decays 1/turn)
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Plating, upgraded ? 6 : 4);
                break;

            case IC.Vicious: // 1-cost, draw 1/2 whenever you apply Vulnerable
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Vicious, upgraded ? 2 : 1);
                break;

            // ── Colorless ────────────────────────────────────────────────────────

            case CL.GangUp: // 1-cost, 5 + 5/7 per hit an ALLY landed on the target this turn
                // CalculationBaseVar(5m) with ExtraDamageVar(5m) per qualifying ally hit;
                // OnUpgrade raises the PER-HIT damage, not the base. The card is
                // MultiplayerOnly and singleplayer has no allies, so the multiplier is
                // always zero and an upgraded Gang Up still hits for 5.
                DealDamage(state, 5);
                break;

            case CL.GoldAxe: // 1-cost, damage equals cards played this combat
                DealDamage(state, state.CardsPlayedThisCombat);
                break;

            case CL.MindBlast: // 1/0-cost, damage equals draw pile size, Innate
                DealDamage(state, state.DrawPile.Count);
                break;

            case CL.Rend: // 2-cost, 15/18 + 5/8 per non-temporary debuff on target
            {
                var target = FirstEnemy(state);
                int debuffs = target != null ? CountRendDebuffs(target) : 0;
                DealDamage(state, (upgraded ? 18 : 15) + debuffs * (upgraded ? 8 : 5));
                break;
            }

            case CL.Alchemize: // 1/0-cost, gain random potion
                ProcureRandomPotion(state, rng);
                break;

            case CL.Anointed: // 1-cost, draw all Rare cards from draw pile
                DrawRareCards(state, upgraded, rng);
                break;

            case CL.Discovery: // 1-cost, choose one of three generated cards; free this turn
            {
                // CardFactory.GetDistinctForCombat(..., 3, Rng.CombatCardGeneration) then
                // a choose-a-card screen. The game's canSkip is not modelled: every action
                // in a selection is a candidate, and skipping would need one of its own.
                OpenGeneratedCardSelection(state, def.Id, optionCount: 3, rng);

                break;
            }

            case CL.Finesse: // 0-cost, 4/7 block + draw 1
                // BlockVar(4m) with OnUpgrade UpgradeValueBy(3m), and Cards.g.cs had
                // 4(+3) all along; this case hardcoded 2/4.
                GainBlock(state, Blk(def, upgraded, card), rng);
                DrawCards(state, 1, rng);
                break;

            case CL.FlashOfSteel: // 0-cost, 5/8 dmg + draw 1
                // DamageVar(5m) with OnUpgrade UpgradeValueBy(3m). This case used to
                // hardcode 3/6, ignoring the extracted card data that had it right.
                DealDamage(state, Dmg(def, upgraded, card));
                DrawCards(state, 1, rng);
                break;

            case CL.HandOfGreed: // 2-cost, 20/25 dmg; gain 20/25 gold on fatal
            {
                var target = FirstEnemy(state);
                if (target != null)
                {
                    int hpBefore = target.Hp;
                    DealDamageToEnemy(state, target, upgraded ? 25 : 20);
                    if (
                        target.Hp <= 0
                        && hpBefore > 0
                        && !BuffSystem.Has(target.Buffs, BuffId.Minion)
                    )
                    {
                        state.PlayerGold += RelicEffects.ModifyGoldGained(
                            state.Relics,
                            upgraded ? 25 : 20
                        );
                    }
                }
                break;
            }

            case CL.BelieveInYou: // 0-cost multiplayer ally energy; self in single-player
                state.Energy += upgraded ? 3 : 2;
                break;

            case CL.HuddleUp: // 1-cost multiplayer team draw; self in single-player
                DrawCards(state, upgraded ? 3 : 2, rng);
                break;

            case CL.Impatience: // 0-cost, draw if no Attacks remain in hand
                if (state.Hand.All(c => GeneratedData.Cards.Get(c.DefId).Type != CardType.Attack))
                {
                    DrawCards(state, upgraded ? 3 : 2, rng);
                }

                break;

            case CL.JackOfAllTrades: // 0-cost, add 1/2 random colorless cards, exhaust
                AddRandomColorlessCardsToHand(state, upgraded ? 2 : 1, rng);
                break;

            case CL.MasterOfStrategy: // 0-cost, draw 3/4, exhaust
                DrawCards(state, upgraded ? 4 : 3, rng);
                break;

            case CL.Mimic: // 1-cost, gain block equal to target ally's block; self in SP
                GainBlock(state, state.PlayerBlock, rng);
                break;

            case CL.PanicButton: // 0-cost, 30/40 block, then no card block for 2 turns
                GainBlock(state, upgraded ? 40 : 30, rng);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.NoBlock, 2);
                break;

            case CL.Production: // 0-cost, gain 2/3 energy, exhaust
                state.Energy += upgraded ? 3 : 2;
                break;

            case CL.Purity: // 0-cost, exhaust up to 3/5 CHOSEN cards from hand
                if (state.Hand.Count > 0)
                {
                    OpenCardSelection(
                        state,
                        CardSelectionKind.ExhaustFromHandRepeated,
                        state.Hand.Count,
                        def.Id,
                        autoPick: 0,
                        amount: upgraded ? 5 : 3
                    );
                }
                break;

            case CL.SecretTechnique: // 0-cost, put a CHOSEN Skill from the draw pile in hand
                OpenDrawPileSelection(state, def.Id, CardType.Skill);
                break;

            case CL.SecretWeapon: // 0-cost, put a CHOSEN Attack from the draw pile in hand
                OpenDrawPileSelection(state, def.Id, CardType.Attack);
                break;

            case CL.ThinkingAhead: // 0-cost, draw 2 then put a CHOSEN card back on top of draw
                DrawCards(state, 2, rng);
                if (state.Hand.Count > 0)
                {
                    OpenCardSelection(
                        state,
                        CardSelectionKind.HandToDrawPileTop,
                        state.Hand.Count,
                        def.Id,
                        autoPick: 0
                    );
                }
                break;

            case CL.TheBomb: // 2-cost, after 3 turns deal 40/50 to ALL enemies
                BuffSystem.Apply(state.PlayerBuffs, BuffId.TheBombPower, 3);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.TheBombDamage, upgraded ? 50 : 40);
                break;

            case CL.Scrawl: // 1-cost, draw until hand is full, exhaust
                DrawCards(state, MaxCardsInHand - state.Hand.Count, rng);
                break;

            case CL.Shockwave: // 2-cost, Weak and Vulnerable 3/5 to ALL enemies, exhaust
                ApplyAllEnemyDebuff(state, BuffId.Weak, upgraded ? 5 : 3, rng);
                ApplyAllEnemyDebuff(state, BuffId.Vulnerable, upgraded ? 5 : 3, rng);
                break;

            case CL.Automation: // 1/0-cost, every 10 drawn cards gain 1 energy
                BuffSystem.Apply(state.PlayerBuffs, BuffId.AutomationPower, 1);
                break;

            case CL.BeaconOfHope: // multiplayer-only shared block; no-op in single-player
                break;

            case CL.Calamity: // 3/2-cost, after each Attack add a random Attack to hand
                BuffSystem.Apply(state.PlayerBuffs, BuffId.CalamityPower, 1);
                break;

            case CL.Entropy: // 1-cost, transform 1 card in hand each turn
                // CardsVar(1), and OnUpgrade only adds CardKeyword.Innate — the amount
                // does not move. This used to transform two cards when upgraded.
                BuffSystem.Apply(state.PlayerBuffs, BuffId.EntropyPower, 1);
                break;

            case CL.EternalArmor: // 3-cost, gain 9/12 Plating
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Plating, upgraded ? 12 : 9);
                break;

            case CL.Fasten: // 1-cost, Defend cards give 4/6 extra block
                BuffSystem.Apply(state.PlayerBuffs, BuffId.FastenPower, upgraded ? 6 : 4);
                break;

            case CL.Mayhem: // 2/1-cost, auto-play top draw card each turn
                BuffSystem.Apply(state.PlayerBuffs, BuffId.MayhemPower, 1);
                break;

            case CL.Panache: // 0-cost, every five played cards deal 10/14 to ALL
                BuffSystem.Apply(state.PlayerBuffs, BuffId.PanachePower, upgraded ? 14 : 10);
                break;

            case CL.PrepTime: // 1-cost, gain 4/6 Vigor each turn
                BuffSystem.Apply(state.PlayerBuffs, BuffId.PrepTimePower, upgraded ? 6 : 4);
                break;

            case CL.Prowess: // 1-cost, gain 1/2 Strength and Dexterity
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, upgraded ? 2 : 1);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Dexterity, upgraded ? 2 : 1);
                break;

            case CL.RollingBoulder: // 3-cost, start of turn damage to ALL, grows by 5
                BuffSystem.Apply(state.PlayerBuffs, BuffId.RollingBoulderPower, upgraded ? 10 : 5);
                break;

            case CL.Stratagem: // 1/0-cost, after shuffle move a card from draw to hand
                BuffSystem.Apply(state.PlayerBuffs, BuffId.StratagemPower, 1);
                break;

            // ── Silent ──────────────────────────────────────────────────────────

            case SI.Abrasive: // 3-cost, gain 1 Dexterity and 4/6 Thorns
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Dexterity, 1);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Thorns, upgraded ? 6 : 4);
                break;

            case SI.Accelerant: // 1-cost, poison support power; approximate with Envenom stacks
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Envenom, upgraded ? 2 : 1);
                break;

            case SI.Accuracy: // 1-cost, Shivs deal 4/6 more damage
                BuffSystem.Apply(state.PlayerBuffs, BuffId.ShivDamage, upgraded ? 6 : 4);
                break;

            case SI.Acrobatics: // 1-cost, draw 3/4 then discard a CHOSEN card
                DrawCards(state, upgraded ? 4 : 3, rng);
                OpenDiscardSelection(state, def.Id, 1);
                break;

            case SI.Adrenaline: // 0-cost, gain 1/2 energy and draw 2, exhaust
                state.Energy += upgraded ? 2 : 1;
                DrawCards(state, 2, rng);
                break;

            case SI.Afterimage: // 1-cost, gain block after each played card
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Afterimage, 1);
                break;

            case SI.Anticipate: // 0-cost, gain 2/3 Dexterity via AnticipatePower
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Dexterity, upgraded ? 3 : 2);
                break;

            case SI.Assassinate: // 0-cost, 10/13 damage + Vulnerable 1/2, exhaust
            {
                var target = FirstEnemy(state);
                if (target != null)
                {
                    DealDamageToEnemy(state, target, Dmg(def, upgraded, card));
                    ApplyEnemyDebuffToTarget(
                        state,
                        target,
                        BuffId.Vulnerable,
                        upgraded ? 2 : 1,
                        rng
                    );
                }
                break;
            }

            case SI.Backflip: // 1-cost, 5/8 block and draw 2
                GainBlock(state, Blk(def, upgraded, card), rng);
                DrawCards(state, 2, rng);
                break;

            case SI.Backstab: // 0-cost, 11/15 damage, exhaust
                DealDamage(state, Dmg(def, upgraded, card));
                break;

            case SI.BladeOfInk: // 1-cost, add 2/3 Inky Shivs
                AddGeneratedCardsToHand(state, SI.Shiv, upgraded ? 3 : 2);
                break;

            case SI.BladeDance: // 1-cost, add 3/4 Shivs, exhaust
                AddGeneratedCardsToHand(state, SI.Shiv, upgraded ? 4 : 3);
                break;

            case SI.Blur: // 1-cost, 5/8 block and retain block next turn
                GainBlock(state, Blk(def, upgraded, card), rng);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Barricade, 1);
                break;

            case SI.BouncingFlask: // 2-cost, apply 3 poison 3/4 times to random enemies
                for (int i = 0; i < (upgraded ? 4 : 3); i++)
                {
                    ApplyEnemyDebuff(state, BuffId.Poison, 3, rng);
                }
                break;

            case SI.BubbleBubble: // 1-cost, apply 9/12 poison if target is already poisoned
            {
                var target = FirstEnemy(state);
                if (target != null && BuffSystem.Get(target.Buffs, BuffId.Poison) > 0)
                {
                    ApplyEnemyDebuffToTarget(state, target, BuffId.Poison, upgraded ? 12 : 9, rng);
                }
                break;
            }

            case SI.BulletTime: // 3/2-cost, make hand free this turn and prevent draw
                MakeHandFreeThisTurn(state);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.NoBlock, 1);
                break;

            case SI.Burst: // 1-cost, next 1/2 Skills are played twice; approximated by Attack duplicate hook
                BuffSystem.Apply(state.PlayerBuffs, BuffId.OneTwoPunch, upgraded ? 2 : 1);
                break;

            case SI.CalculatedGamble: // 0-cost, discard hand and draw that many; upgrade retains
            {
                int count = state.Hand.Count;
                DiscardFirstCardsFromHand(state, count);
                DrawCards(state, count, rng);
                break;
            }

            case SI.CloakAndDagger: // 1-cost, 6 block and add 1/2 Shivs
                GainBlock(state, Blk(def, upgraded, card), rng);
                AddGeneratedCardsToHand(state, SI.Shiv, upgraded ? 2 : 1);
                break;

            case SI.CorrosiveWave: // 1-cost power; approximate as poison+weak to all
                ApplyAllEnemyDebuff(state, BuffId.Poison, upgraded ? 5 : 3, rng);
                ApplyAllEnemyDebuff(state, BuffId.Weak, upgraded ? 3 : 2, rng);
                break;

            case SI.DaggerSpray: // 1-cost, 4/6 damage twice to all enemies
                DealDamageToAllMultiHit(state, Dmg(def, upgraded, card), 2);
                break;

            case SI.DaggerThrow: // 1-cost, 9/12 damage, draw 1, discard a CHOSEN card
                DealDamage(state, Dmg(def, upgraded, card));
                DrawCards(state, 1, rng);
                OpenDiscardSelection(state, def.Id, 1);
                break;

            case SI.Dash: // 2-cost, 10/13 block and 10/13 damage
                GainBlock(state, Blk(def, upgraded, card), rng);
                DealDamage(state, Dmg(def, upgraded, card));
                break;

            case SI.DeadlyPoison: // 1-cost, poison 5/7
                ApplyEnemyDebuff(state, BuffId.Poison, upgraded ? 7 : 5, rng);
                break;

            case SI.DefendSilent: // 1-cost, 5/8 block
            case SI.Deflect: // 0-cost, 4/7 block
                GainBlock(state, Blk(def, upgraded, card), rng);
                break;

            case SI.DodgeAndRoll: // 1-cost, 4/6 block and same block next turn
            {
                int amount = Blk(def, upgraded, card);
                GainBlock(state, amount, rng);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.BlockNextTurn, amount);
                break;
            }

            case SI.EchoingSlash: // 1-cost, 10/13 to all; gains damage per kill in the real game
                DealDamageToAll(state, Dmg(def, upgraded, card));
                break;

            case SI.Envenom: // 2-cost, attacks apply 1/2 poison on damage
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Envenom, upgraded ? 2 : 1);
                break;

            case SI.EscapePlan: // 0-cost, draw 1; if Skill, gain 3/5 block
            {
                bool drewSkill =
                    state.DrawPile.Count > 0
                    && GeneratedData.Cards.Get(state.DrawPile[0].DefId).Type == CardType.Skill;
                DrawCards(state, 1, rng);
                if (drewSkill)
                {
                    GainBlock(state, Blk(def, upgraded, card), rng);
                }
                break;
            }

            case SI.Expertise: // 1-cost, draw until hand has 6/7 cards
                DrawCards(state, Math.Max(0, (upgraded ? 7 : 6) - state.Hand.Count), rng);
                break;

            case SI.Expose: // 0-cost, remove block/artifact and apply Vulnerable 2/3
            {
                var target = FirstEnemy(state);
                if (target != null)
                {
                    target.Block = 0;
                    BuffSystem.TryConsumeArtifact(target.Buffs);
                    ApplyEnemyDebuffToTarget(
                        state,
                        target,
                        BuffId.Vulnerable,
                        upgraded ? 3 : 2,
                        rng
                    );
                }
                break;
            }

            case SI.FanOfKnives: // 2-cost power, create 4/5 Shivs and continue creating each turn
                AddGeneratedCardsToHand(state, SI.Shiv, upgraded ? 5 : 4);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.InfiniteBlades, upgraded ? 5 : 4);
                break;

            case SI.Finisher: // 1-cost, 6/8 damage once per Attack played this turn
                // CalculationBase 0 + 1 per finished Attack play, and AttackCommand's hit
                // loop simply does not run at zero — no minimum hit.
                DealDamageMultiHit(
                    state,
                    Dmg(def, upgraded, card),
                    state.AttackCardsPlayedThisTurn,
                    rng
                );
                break;

            case SI.Flanking: // 2/1-cost, next turn energy approximation
                BuffSystem.Apply(state.PlayerBuffs, BuffId.NextTurnEnergy, 2);
                break;

            case SI.Flechettes: // 1-cost, 5/7 damage once per Skill in hand
                // Same shape as Finisher: no Skills in hand means no hits at all.
                DealDamageMultiHit(
                    state,
                    Dmg(def, upgraded, card),
                    CountCardsOfTypeInHand(state, CardType.Skill),
                    rng
                );
                break;

            case SI.FlickFlack: // 1-cost, 6/8 damage to all
                DealDamageToAll(state, Dmg(def, upgraded, card));
                break;

            case SI.Scare: // 0-cost, Weak 2/3
            case SI.Haze: // 1-cost, Weak 2/3
                ApplyEnemyDebuff(state, BuffId.Weak, upgraded ? 3 : 2, rng);
                break;

            case SI.Footwork: // 1-cost, 2/3 Dexterity
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Dexterity, upgraded ? 3 : 2);
                break;

            case SI.GrandFinale: // 0-cost, playable with empty draw pile, 60/75 to all
                if (state.DrawPile.Count == 0)
                {
                    DealDamageToAll(state, Dmg(def, upgraded, card));
                }
                break;

            case SI.HandTrick: // 1-cost, 7/10 block and mark a CHOSEN Skill Sly
            {
                GainBlock(state, Blk(def, upgraded, card), rng);
                // `CardSelectCmd.FromHand` filtered to `card.Type == Skill &&
                // !card.IsSlyThisTurn`. The marking was the whole second half of the card
                // and did nothing at all before: Sly was unmodelled, so Hand Trick was
                // seven block.
                var skills = new List<int>();
                for (int i = 0; i < state.Hand.Count; i++)
                {
                    var candidate = state.Hand[i];
                    if (
                        GeneratedData.Cards.Get(candidate.DefId).Type == CardType.Skill
                        && !candidate.IsSlyThisTurn()
                    )
                    {
                        skills.Add(i);
                    }
                }

                OpenCardSelection(
                    state,
                    CardSelectionKind.MarkHandCardSly,
                    skills,
                    def.Id,
                    autoPick: skills.Count > 0 ? skills[0] : 0
                );
                break;
            }

            case SI.HiddenDaggers: // 1-cost, discard 2 CHOSEN cards, then add 2 Shivs
                // The card deals NO damage: its two vars are CardsVar(2) -- how many to
                // discard -- and Shivs = 2. The emulator was dealing the CardsVar as
                // damage and reading the upgrade as a third Shiv, where upgrading in fact
                // leaves the count at two and UPGRADES the Shivs it makes.
                //
                // The discard comes first and the Shivs after, which is the ordering the
                // selection's follow-up exists for: a Shiv created before the screen
                // opened would be a candidate for the discard it was created by.
                OpenDiscardSelection(
                    state,
                    def.Id,
                    2,
                    [.. Enumerable.Repeat(new CardInstance(SI.Shiv, upgraded), 2)]
                );
                break;

            case SI.InfiniteBlades: // 1-cost, add one Shiv each turn
                BuffSystem.Apply(state.PlayerBuffs, BuffId.InfiniteBlades, 1);
                break;

            case SI.KnifeTrap: // 1-cost, 4/6 Thorns
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Thorns, upgraded ? 6 : 4);
                break;

            case SI.LeadingStrike: // 1-cost Strike, 3/6 damage and add 2 Shivs
                DealDamage(state, Dmg(def, upgraded, card));
                AddGeneratedCardsToHand(state, SI.Shiv, 2);
                break;

            case SI.LegSweep: // 2-cost, 11/14 block and Weak 3/4
                GainBlock(state, Blk(def, upgraded, card), rng);
                ApplyEnemyDebuff(state, BuffId.Weak, upgraded ? 4 : 3, rng);
                break;

            case SI.Malaise: // X-cost, enemy loses X Strength and gains X Weak
            {
                int x = state.Energy;
                ApplyTemporaryStrengthDownToEnemy(state, x);
                ApplyEnemyDebuff(state, BuffId.Weak, x, rng);
                state.Energy = 0;
                break;
            }

            case SI.MasterPlanner: // power approximation: extra card next turn
                BuffSystem.Apply(state.PlayerBuffs, BuffId.NextTurnDraw, upgraded ? 2 : 1);
                break;

            case SI.MementoMori: // 1-cost, 9 damage + 4/5 per card discarded this turn
                // CalculationBaseVar(9m) with ExtraDamageVar(4m) per card discarded this
                // turn; OnUpgrade raises the PER-DISCARD damage, not the base. The
                // discard counter is not modelled, so this is the zero-discard case —
                // the old 8/12 was wrong even there.
                DealDamage(state, 9);
                break;

            case SI.Mirage: // 1-cost, 10/14 block
                GainBlock(state, upgraded ? 14 : 10, rng);
                break;

            case SI.Murder: // 2-cost, high damage approximation
                DealDamage(state, upgraded ? 35 : 25);
                break;

            case SI.Neutralize: // 0-cost, 3/4 damage and Weak 1/2
            {
                var target = FirstEnemy(state);
                if (target != null)
                {
                    DealDamageToEnemy(state, target, Dmg(def, upgraded, card));
                    ApplyEnemyDebuffToTarget(state, target, BuffId.Weak, upgraded ? 2 : 1, rng);
                }
                break;
            }

            case SI.Nightmare: // 3/2-cost, duplicate first hand card 3 times
                DuplicateFirstCardInHand(state, 3);
                break;

            case SI.NoxiousFumes: // 1-cost, poison all enemies each turn
                BuffSystem.Apply(state.PlayerBuffs, BuffId.NoxiousFumes, upgraded ? 3 : 2);
                break;

            case SI.Outbreak: // 1-cost power, poison burst approximation
                BuffSystem.Apply(state.PlayerBuffs, BuffId.NoxiousFumes, upgraded ? 5 : 3);
                break;

            case SI.PhantomBlades: // 1-cost power, retained Shivs approximation
                BuffSystem.Apply(state.PlayerBuffs, BuffId.InfiniteBlades, upgraded ? 4 : 3);
                break;

            case SI.PiercingWail: // 1-cost, all enemies lose 6/8 Strength this turn
                foreach (var enemy in state.Enemies.Where(e => e.Hp > 0))
                {
                    if (BuffSystem.TryConsumeArtifact(enemy.Buffs))
                    {
                        continue;
                    }
                    BuffSystem.Apply(enemy.Buffs, BuffId.Strength, upgraded ? -8 : -6);
                    BuffSystem.Apply(enemy.Buffs, BuffId.TemporaryStrength, upgraded ? 8 : 6);
                }
                break;

            case SI.Pinpoint: // 3-cost, 15/19 damage; cost reduces this turn in game
                DealDamage(state, Dmg(def, upgraded, card));
                break;

            case SI.PoisonedStab: // 1-cost, 6/8 damage and Poison 3/4
            {
                var target = FirstEnemy(state);
                if (target != null)
                {
                    DealDamageToEnemy(state, target, Dmg(def, upgraded, card));
                    ApplyEnemyDebuffToTarget(state, target, BuffId.Poison, upgraded ? 4 : 3, rng);
                }
                break;
            }

            case SI.Pounce: // 2-cost, 14/20 damage and next Skill free
                // PowerCmd.Apply<FreeSkillPower>(1) — the next Skill costs 0. This used to
                // grant NextTurnEnergy, which is a different effect on a different turn.
                DealDamage(state, Dmg(def, upgraded, card));
                BuffSystem.Apply(state.PlayerBuffs, BuffId.FreeSkillPower, 1);
                break;

            case SI.PreciseCut: // 0-cost, 13/16 damage LESS 2 per other card in hand
                // CalculationBaseVar(13m) with ExtraDamageVar(2m) times -(hand count,
                // excluding this card). The old formula scaled up with Attacks played,
                // which is neither the right direction nor the right input.
                DealDamage(state, Math.Max(0, (upgraded ? 16 : 13) - 2 * state.Hand.Count));
                break;

            case SI.Predator: // 2-cost, 15/20 damage and draw 2 more next turn
                DealDamage(state, Dmg(def, upgraded, card));
                BuffSystem.Apply(state.PlayerBuffs, BuffId.NextTurnDraw, upgraded ? 3 : 2);
                break;

            case SI.Prepared: // 0-cost, draw 1/2 then discard that many CHOSEN cards
                DrawCards(state, upgraded ? 2 : 1, rng);
                OpenDiscardSelection(state, def.Id, upgraded ? 2 : 1);
                break;

            case SI.Reflex: // 3-cost, draw 2/3
                DrawCards(state, upgraded ? 3 : 2, rng);
                break;

            case SI.Ricochet: // 2-cost, 3 damage 4/5 times to random enemies
                DealDamageMultiHit(state, Dmg(def, upgraded, card), upgraded ? 5 : 4, rng);
                break;

            case SI.SerpentForm: // 3-cost power, poison-like scaling approximation
                BuffSystem.Apply(state.PlayerBuffs, BuffId.NoxiousFumes, upgraded ? 6 : 4);
                break;

            case SI.ShadowStep: // 1/0-cost, discard hand and gain Intangible
                DiscardFirstCardsFromHand(state, state.Hand.Count);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Intangible, 1);
                break;

            case SI.Shadowmeld: // 1/0-cost, retain hand for one turn
                BuffSystem.Apply(state.PlayerBuffs, BuffId.RetainHand, 1);
                break;

            case SI.Skewer: // X-cost, 8/11 damage X times
            {
                int x = state.Energy;
                state.Energy = 0;
                DealDamageMultiHit(state, Dmg(def, upgraded, card), x, rng);
                break;
            }

            case SI.Slice: // 0-cost, 6/9 damage
                DealDamage(state, Dmg(def, upgraded, card));
                break;

            case SI.Snakebite: // 2-cost, Retain, Poison 7/10
                ApplyEnemyDebuff(state, BuffId.Poison, upgraded ? 10 : 7, rng);
                break;

            case SI.Sneaky: // 2-cost, stealth/block power approximation
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Afterimage, upgraded ? 2 : 1);
                break;

            case SI.Speedster: // 2-cost, draw/energy next-turn power approximation
                BuffSystem.Apply(state.PlayerBuffs, BuffId.NextTurnEnergy, upgraded ? 3 : 2);
                break;

            case SI.StormOfSteel: // 1-cost, discard hand and add upgraded Shivs if upgraded
            {
                int count = state.Hand.Count;
                DiscardFirstCardsFromHand(state, count);
                for (int i = 0; i < count && state.Hand.Count < MaxCardsInHand; i++)
                {
                    state.Hand.Add(new CardInstance(SI.Shiv, upgraded, FreeThisTurn: true));
                }
                break;
            }

            case SI.Strangle: // 1-cost, 8/10 damage and StranglePower 2
                // PowerVar<StranglePower>(2m), which OnUpgrade leaves alone — only the
                // damage upgrades. StranglePower itself is not modelled; Vulnerable 2
                // stands in for it, so the stand-in must not scale with the upgrade
                // either.
                DealDamage(state, Dmg(def, upgraded, card));
                ApplyEnemyDebuff(state, BuffId.Vulnerable, 2, rng);
                break;

            case SI.StrikeSilent: // 1-cost, 6/9 damage
                DealDamage(state, Dmg(def, upgraded, card));
                break;

            case SI.SuckerPunch: // 1-cost, 8/10 damage and Weak 1/2
            case SI.Suppress: // 0-cost, 11/17 damage and Weak 3/5
            {
                var target = FirstEnemy(state);
                if (target != null)
                {
                    DealDamageToEnemy(state, target, Dmg(def, upgraded, card));
                    int weak = def.Id == SI.Suppress ? (upgraded ? 5 : 3) : (upgraded ? 2 : 1);
                    ApplyEnemyDebuffToTarget(state, target, BuffId.Weak, weak, rng);
                }
                break;
            }

            case SI.Survivor: // 1-cost, 8/11 block and discard a CHOSEN card
                GainBlock(state, Blk(def, upgraded, card), rng);
                OpenDiscardSelection(state, def.Id, 1);
                break;

            case SI.Tactician: // 3-cost, gain 1/2 energy
                state.Energy += upgraded ? 2 : 1;
                break;

            case SI.TheHunt: // 1-cost, 10/15 damage; kill power approximation
                DealDamage(state, Dmg(def, upgraded, card));
                break;

            case SI.ToolsOfTheTrade: // 1/0-cost, draw then discard each turn
                BuffSystem.Apply(state.PlayerBuffs, BuffId.ToolsOfTheTrade, 1);
                break;

            case SI.Tracking: // 2/1-cost, weak-tracking power approximation
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Vicious, upgraded ? 2 : 1);
                break;

            case SI.Untouchable: // 2-cost, gain block equal to remaining draw pile plus 6/9
                GainBlock(state, Blk(def, upgraded, card) + state.DrawPile.Count, rng);
                break;

            case SI.UpMySleeve: // 2-cost, add 2 Shivs; cost drops in combat in real game
                AddGeneratedCardsToHand(state, SI.Shiv, upgraded ? 3 : 2);
                break;

            case SI.WellLaidPlans: // 1-cost, retain 1/2 cards
                BuffSystem.Apply(state.PlayerBuffs, BuffId.RetainHand, upgraded ? 2 : 1);
                break;

            case SI.WraithForm: // 3-cost, Intangible 2/3 and lose Dexterity each turn approximation
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Intangible, upgraded ? 3 : 2);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Dexterity, -1);
                break;

            // -- Remaining generated cards ----------------------------------------------
            case 5: // AdaptiveStrike
            case 8: // Afterlife
            case 11: // Alignment
            case 12: // AllForOne
            case 16: // Apotheosis
            case 17: // Apparition
            case 19: // Arsenal
            case 22: // AstralPulse
            case 26: // BallLightning
            case 27: // BansheesCry
            case 28: // Barrage
            case 33: // BeamCell
            case 35: // BeatIntoShape
            case 37: // Begone
            case 39: // BiasedCognition
            case 40: // BigBang
            case 41: // BlackHole
            case 44: // BlightStrike
            case 49: // Bodyguard
            case 52: // Bombardment
            case 53: // BoneShards
            case 54: // BoostAway
            case 55: // BootSequence
            case 56: // BorrowedTime
            case 61: // BrightestFlame
            case 63: // Buffer
            case 64: // BulkUp
            case 67: // Bulwark
            case 68: // BundleOfJoy
            case 71: // Bury
            case 72: // ByrdSwoop
            case 74: // Calcify
            case 76: // CallOfTheVoid
            case 77: // Caltrops
            case 78: // Capacitor
            case 79: // CaptureSpirit
            case 81: // CelestialMight
            case 82: // Chaos
            case 83: // Charge
            case 84: // ChargeBattery
            case 85: // ChildOfTheStars
            case 86: // Chill
            case 88: // Clash
            case 89: // Claw
            case 90: // Cleanse
            case 92: // CloakOfStars
            case 93: // ColdSnap
            case 94: // CollisionCourse
            case 96: // Comet
            case 97: // Compact
            case 98: // CompileDriver
            case 100: // Conqueror
            case 101: // ConsumingShadow
            case 102: // Convergence
            case 103: // Coolant
            case 104: // Coolheaded
            case 105: // Coordinate
            case 108: // CosmicIndifference
            case 109: // Countdown
            case 110: // CrashLanding
            case 111: // CreativeAi
            case 112: // CrescentSpear
            case 115: // CrushUnder
            case 118: // DanseMacabre
            case 120: // Darkness
            case 124: // Deathbringer
            case 125: // DeathMarch
            case 126: // DeathsDoor
            case 127: // Debilitate
            case 129: // DecisionsDecisions
            case 130: // DefendDefect
            case 131: // DefendIronclad
            case 132: // DefendNecrobinder
            case 133: // DefendRegent
            case 135: // Defile
            case 137: // Defragment
            case 138: // Defy
            case 139: // Delay
            case 140: // Demesne
            case 143: // Devastate
            case 144: // DevourLife
            case 145: // Dirge
            case 148: // Distraction
            case 151: // DoubleEnergy
            case 152: // DrainPower
            case 154: // Dredge
            case 156: // Dualcast
            case 157: // DualWield
            case 158: // DyingStar
            case 159: // EchoForm
            case 161: // Eidolon
            case 162: // EndOfDays
            case 163: // EnergySurge
            case 164: // EnfeeblingTouch
            case 165: // Enlightenment
            case 167: // Entrench
            case 170: // Equilibrium
            case 171: // Eradicate
            case 178: // Exterminate
            case 179: // FallingStar
            case 182: // Fear
            case 184: // FeedingFrenzy
            case 186: // Feral
            case 187: // Fetch
            case 190: // FightThrough
            case 194: // FlakCannon
            case 198: // Flatten
            case 201: // FocusedStrike
            case 203: // ForbiddenGrimoire
            case 204: // ForegoneConclusion
            case 207: // Friendship
            case 208: // Ftl
            case 209: // Fuel
            case 210: // Furnace
            case 211: // Fusion
            case 212: // GammaBlast
            case 214: // GatherLight
            case 215: // Genesis
            case 216: // GeneticAlgorithm
            case 217: // GiantRock
            case 218: // Glacier
            case 219: // Glasswork
            case 220: // Glimmer
            case 221: // GlimpseBeyond
            case 222: // Glitterstream
            case 223: // Glow
            case 224: // GoForTheEyes
            case 227: // Graveblast
            case 228: // GraveWarden
            case 229: // Guards
            case 230: // GuidingStar
            case 231: // GunkUp
            case 232: // Hailstorm
            case 233: // HammerTime
            case 236: // Hang
            case 237: // Haunt
            case 241: // HeavenlyDrill
            case 242: // Hegemony
            case 243: // HeirloomHammer
            case 244: // HelixDrill
            case 245: // HelloWorld
            case 248: // HiddenCache
            case 251: // HighFive
            case 252: // Hologram
            case 253: // Hotfix
            case 256: // Hyperbeam
            case 257: // IAmInvincible
            case 258: // IceLance
            case 259: // Ignition
            case 266: // Intercept
            case 267: // Invoke
            case 269: // Iteration
            case 274: // KinglyKick
            case 275: // KinglyPunch
            case 277: // Knockdown
            case 278: // KnockoutBlow
            case 279: // KnowThyPlace
            case 280: // Largesse
            case 282: // Leap
            case 283: // LegionOfBone
            case 285: // Lethality
            case 286: // Lift
            case 287: // LightningRod
            case 288: // Loop
            case 289: // Luminesce
            case 290: // LunarBlast
            case 291: // MachineLearning
            case 292: // MadScience
            case 293: // MakeItSo
            case 296: // ManifestAuthority
            case 299: // Maul
            case 301: // Melancholy
            case 303: // Metamorphosis
            case 304: // MeteorShower
            case 305: // MeteorStrike
            case 308: // MinionDiveBomb
            case 309: // MinionSacrifice
            case 310: // MinionStrike
            case 312: // Misery
            case 314: // MomentumStrike
            case 315: // MonarchsGaze
            case 316: // Monologue
            case 317: // MultiCast
            case 319: // NecroMastery
            case 320: // NegativePulse
            case 322: // Neurosurge
            case 324: // NeutronAegis
            case 326: // NoEscape
            case 330: // Null
            case 331: // Oblivion
            case 335: // Orbit
            case 337: // Outmaneuver
            case 338: // Overclock
            case 340: // Pagestorm
            case 341: // PaleBlueDot
            case 344: // Parry
            case 345: // Parse
            case 346: // ParticleWall
            case 347: // Patter
            case 348: // Peck
            case 351: // PhotonCut
            case 354: // PillarOfCreation
            case 357: // Poke
            case 367: // Prophesize
            case 368: // Protector
            case 370: // PullAggro
            case 371: // PullFromBelow
            case 373: // Putrefy
            case 375: // Quadcast
            case 376: // Quasar
            case 377: // Radiate
            case 379: // Rainbow
            case 380: // Rally
            case 382: // Rattle
            case 383: // Reanimate
            case 384: // Reap
            case 385: // ReaperForm
            case 386: // Reave
            case 387: // Reboot
            case 388: // Rebound
            case 389: // RefineBlade
            case 390: // Reflect
            case 392: // Refract
            case 393: // Relax
            case 395: // Resonance
            case 398: // RightHandHand
            case 399: // RipAndTear
            case 400: // RocketPunch
            case 402: // RoyalGamble
            case 403: // Royalties
            case 405: // Sacrifice
            case 408: // Scavenge
            case 409: // Scourge
            case 410: // Scrape
            case 412: // SculptingStrike
            case 413: // Seance
            case 418: // SeekingEdge
            case 419: // SentryMode
            case 422: // SevenStars
            case 423: // Severance
            case 425: // ShadowShield
            case 427: // SharedFate
            case 428: // Shatter
            case 429: // ShiningStrike
            case 430: // Shiv
            case 432: // Shroud
            case 434: // SicEm
            case 435: // SignalBoost
            case 437: // Skim
            case 438: // SleightOfFlesh
            case 441: // Smokestack
            case 443: // Snap
            case 445: // SolarStrike
            case 446: // Soul
            case 447: // SoulStorm
            case 448: // SovereignBlade
            case 449: // Sow
            case 450: // SpectrumShift
            case 452: // Spinner
            case 453: // SpiritOfAsh
            case 456: // SpoilsOfBattle
            case 457: // SporeMind
            case 458: // Spur
            case 459: // Squash
            case 460: // Squeeze
            case 461: // Stack
            case 463: // Stardust
            case 467: // Storm
            case 471: // StrikeDefect
            case 472: // StrikeIronclad
            case 473: // StrikeNecrobinder
            case 474: // StrikeRegent
            case 476: // Subroutine
            case 478: // SummonForth
            case 479: // Sunder
            case 480: // Supercritical
            case 481: // Supermassive
            case 484: // SweepingBeam
            case 485: // SweepingGaze
            case 487: // SwordSage
            case 488: // Synchronize
            case 489: // Synthesis
            case 491: // TagTeam
            case 495: // Tempest
            case 496: // Terraforming
            case 497: // TeslaCoil
            case 501: // TheScythe
            case 502: // TheSealedThrone
            case 503: // TheSmith
            case 507: // Thunder
            case 509: // TimesUp
            case 511: // ToricToughness
            case 514: // Transfigure
            case 515: // TrashToTreasure
            case 518: // Turbo
            case 520: // Tyranny
            case 523: // Undeath
            case 524: // Unleash
            case 530: // Uproar
            case 531: // Veilpiercer
            case 532: // Venerate
            case 534: // VoidForm
            case 536: // Voltaic
            case 539: // Whistle
            case 540: // WhiteNoise
            case 541: // Wish
            case 542: // Wisp
            case 544: // WroughtInWar
            case 545: // Zap
                ApplyGeneratedCardApproximation(def, upgraded, state, rng, card);
                break;

            // ── Fallback ─────────────────────────────────────────────────────────

            default:
                ApplyGeneratedCardApproximation(def, upgraded, state, rng, card);
                break;
        }
    }

    // ── Card-pile helpers ─────────────────────────────────────────────────────

    private static void DrawRareCards(CombatState state, bool retain, Random rng)
    {
        var rareIndices = state
            .DrawPile.Select((c, i) => new { Card = c, Index = i })
            .Where(x => GeneratedData.Cards.Get(x.Card.DefId).Rarity == CardRarity.Rare)
            .OrderByDescending(x => x.Index)
            .ToList();

        foreach (var item in rareIndices)
        {
            if (state.Hand.Count >= MaxCardsInHand)
            {
                break;
            }

            state.Hand.Add(item.Card with { Retain = retain });
            state.RemoveFromDrawPileAt(item.Index);
        }
    }

    private static void ProcureRandomPotion(CombatState state, Random rng)
    {
        for (int i = 0; i < state.MaxPotionSlots; i++)
        {
            if (state.PotionSlots[i] == 0)
            {
                // PotionFactory.CreateRandomPotionInCombat reads
                // Rng.CombatPotionGeneration, not the combat rng.
                state.PotionSlots[i] = (state.PotionGenerationRng ?? rng).Next(1, 64);
                break;
            }
        }
    }

    internal static void TransformRandomCardInHand(CombatState state, Random rng)
    {
        if (state.Hand.Count == 0)
        {
            return;
        }

        var selectionRng = CardSelectionRng(state, rng);
        int idx = selectionRng.Next(state.Hand.Count);
        int defId = _ironcladPool[selectionRng.Next(_ironcladPool.Length)];
        state.Hand[idx] = new CardInstance(defId, false);
    }

    public static void DrawCards(CombatState state, int count, Random rng)
    {
        // NoDrawPower stops every later draw this turn, whatever asks for it.
        if (BuffSystem.Get(state.PlayerBuffs, BuffId.NoDraw) > 0)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            if (state.DrawPile.Count == 0)
            {
                ShuffleDiscardIntoDraw(state, rng);
            }
            if (state.DrawPile.Count == 0)
            {
                break;
            }

            var card = state.DrawPile[0];
            state.RemoveFromDrawPileAt(0);
            CountDrawnCardForAutomation(state);

            if (
                BuffSystem.Get(state.PlayerBuffs, BuffId.Hellraiser) > 0
                && IsStrikeCard(card.DefId)
            )
            {
                state.AutoPlayQueue.Add(card);
            }
            else if (state.Hand.Count < MaxCardsInHand)
            {
                // Slither.AfterCardDrawn re-rolls the card's cost for this combat, but
                // only when it actually lands in HAND -- a card that goes to the discard
                // because the hand is full keeps whatever cost it had.
                state.Hand.Add(RollSlitherCost(state, card, rng));
            }
            else
            {
                state.DiscardPile.Add(card);
            }
        }
    }

    /// <summary>
    /// Slither's cost: <c>Rng.CombatEnergyCosts.NextInt(4)</c>, so 0..3, re-rolled every
    /// time the card is drawn to hand.
    /// </summary>
    /// <remarks>
    /// Every draw INTO HAND, which includes the opening one. <c>Slither.AfterCardDrawn</c>
    /// has no exemption for it, and a live capture of a Wood Carvings run settles it: a
    /// Bash enchanted at floor 8 opened the floor-9 fight costing 1 rather than its
    /// printed 2. <c>CombatFactory</c> deals the opening hand straight off the draw pile,
    /// so it has to call this itself.
    /// </remarks>
    public static CardInstance RollSlitherCost(CombatState state, CardInstance card, Random rng)
    {
        if (card.Enchantment != Enchantment.Slither)
        {
            return card;
        }

        int cost = (state.EnergyCostRng as Random ?? rng).Next(4);
        return card with { CostForCombat = cost };
    }

    private static void CountDrawnCardForAutomation(CombatState state)
    {
        int automation = BuffSystem.Get(state.PlayerBuffs, BuffId.AutomationPower);
        if (automation <= 0)
        {
            return;
        }

        state.DrawnCardsSinceAutomationProc++;
        if (state.DrawnCardsSinceAutomationProc >= 10)
        {
            state.DrawnCardsSinceAutomationProc = 0;
            state.Energy += automation;
        }
    }

    private static bool IsStrikeCard(int defId)
    {
        var name = GeneratedData.Cards.Get(defId).Name;
        return name.Contains("Strike", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The game's CardPileCmd.Shuffle: the discard pile and whatever is left of the
    /// draw pile are merged (discard first, then draw), sorted by ModelId, and then
    /// Fisher-Yates'd from the Shuffle stream.
    ///
    /// The sort is the part that is easy to get wrong. ListExtensions.StableShuffle
    /// sorts before shuffling so that two piles holding the same cards in different
    /// orders shuffle alike, and CardModel sorts by ModelId — an ordinal comparison of
    /// the slugified class name — then by upgrade level. Sorting by our own numeric ids
    /// instead lands the pile in a different order, and Fisher-Yates over a different
    /// order draws a different sequence out of the same stream.
    /// </summary>
    /// <summary>
    /// Ordinal rank of each card's <c>Entry</c>, indexed by def id.
    /// </summary>
    /// <remarks>
    /// The shuffle canonicalises the pile by Entry before permuting it, so that the same
    /// cards in a different pile order still shuffle to the same result. Comparing the
    /// STRINGS to do that was the single largest allocation in the emulator's hottest
    /// path — LINQ's OrderBy builds its own buffer, key array and comparer chain on every
    /// reshuffle. The ranks are the same ordering, precomputed once, as ints.
    /// </remarks>
    private static readonly int[] EntryRank = BuildEntryRank();

    private static int[] BuildEntryRank()
    {
        var all = GeneratedData.Cards.All;
        var ids = new int[all.Length];
        var entries = new string[all.Length];
        int highest = 0;
        for (int i = 0; i < all.Length; i++)
        {
            ids[i] = all[i].Id;
            entries[i] = all[i].Entry;
            highest = Math.Max(highest, all[i].Id);
        }

        Array.Sort(entries, ids, StringComparer.Ordinal);
        var rank = new int[highest + 1];
        for (int i = 0; i < ids.Length; i++)
        {
            rank[ids[i]] = i;
        }

        return rank;
    }

    // Reused across shuffles so a reshuffle allocates nothing once the piles have been
    // this big before. ThreadStatic because a tree search forks runs, and two handles on
    // two threads must not share a scratch buffer.
    [ThreadStatic]
    private static CardInstance[]? _shuffleCards;

    [ThreadStatic]
    private static long[]? _shuffleKeys;

    public static void ShuffleDiscardIntoDraw(CombatState state, Random rng)
    {
        int count = state.DiscardPile.Count + state.DrawPile.Count;
        if (_shuffleCards is null || _shuffleCards.Length < count)
        {
            int size = Math.Max(64, count * 2);
            _shuffleCards = new CardInstance[size];
            _shuffleKeys = new long[size];
        }

        var cards = _shuffleCards;
        var keys = _shuffleKeys!;

        // Discard first and then draw, which is the order the merged list used to be
        // built in -- and the low bits of the key are the position, so equal cards keep
        // it. That is what makes this sort STABLE, as OrderBy's was: two cards can match
        // on Entry and Upgraded while differing in an enchantment the key does not see,
        // and an unstable sort would put them either way round and shuffle differently.
        int index = 0;
        foreach (var card in state.DiscardPile)
        {
            cards[index] = card;
            keys[index] = Key(card, index);
            index++;
        }

        foreach (var card in state.DrawPile)
        {
            cards[index] = card;
            keys[index] = Key(card, index);
            index++;
        }

        Array.Sort(keys, cards, 0, count);

        state.DrawPile.Clear();
        for (int i = 0; i < count; i++)
        {
            state.DrawPile.Add(cards[i]);
        }

        ShufflePile(state.DrawPile, state.ShuffleRng ?? rng);
        state.ForgetDrawOrder();
        state.DiscardPile.Clear();
        MoveStratagemCardsToHandAfterShuffle(state);

        static long Key(CardInstance card, int position)
        {
            int id = Math.Abs(card.DefId);
            int rank = id < EntryRank.Length ? EntryRank[id] : EntryRank.Length;
            return ((long)rank << 32) | ((long)(card.Upgraded ? 1 : 0) << 31) | (uint)position;
        }
    }

    public static void ShufflePile<T>(IList<T> pile, Random rng)
    {
        for (int i = pile.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (pile[i], pile[j]) = (pile[j], pile[i]);
        }
    }

    // Adds card to exhaust pile and triggers exhaust hooks.
    public static void ExhaustCard(
        CombatState state,
        CardInstance card,
        bool causedByEthereal = false,
        Random? rng = null
    )
    {
        state.ExhaustPile.Add(card with { FreeThisTurn = false });
        state.CardsExhaustedThisTurn++;
        if (card.DefId == IC.DrumOfBattle)
        {
            state.Energy += card.Upgraded ? 3 : 2;
        }

        int fnp = BuffSystem.Get(state.PlayerBuffs, BuffId.FeelNoPain);
        if (fnp > 0)
        {
            state.PlayerBlock += BuffSystem.IncomingBlock(fnp, state.PlayerBuffs);
        }

        int de = BuffSystem.Get(state.PlayerBuffs, BuffId.DarkEmbrace);
        if (de > 0)
        {
            if (causedByEthereal)
            {
                state.EtherealExhaustCount++;
            }
            else if (rng != null)
            {
                DrawCards(state, de, rng);
            }
        }
    }

    // ── Combat helpers ────────────────────────────────────────────────────────

    public static void DealDamage(CombatState state, int amount)
    {
        var target = FirstEnemy(state);
        if (target != null)
        {
            DealDamageToEnemy(state, target, amount);
        }
    }

    public static void DealDamageToPlayer(CombatState state, int amount)
    {
        amount = BuffSystem.CapIncomingDamage(amount, state.PlayerBuffs);
        int absorbed = Math.Min(state.PlayerBlock, amount);
        state.PlayerBlock -= absorbed;
        int hpLoss = amount - absorbed;
        if (hpLoss > 0)
        {
            int buffer = BuffSystem.Get(state.PlayerBuffs, BuffId.Buffer);
            if (buffer > 0)
            {
                if (buffer == 1)
                {
                    BuffSystem.Remove(state.PlayerBuffs, BuffId.Buffer);
                }
                else
                {
                    BuffSystem.Apply(state.PlayerBuffs, BuffId.Buffer, -1);
                }

                return;
            }

            state.PlayerHp -= hpLoss;
            state.PlayerHpLostThisTurn += hpLoss;
            // Hook.AfterDamageReceived does not care who dealt the damage, so a card that
            // hits its own owner arms Centennial Puzzle and Self-Forming Clay too.
            RelicEffects.ApplyAfterUnblockedDamageReceived(state);
            RelicEffects.ApplyAfterPlayerHpChanged(state);
        }
    }

    /// <summary>
    /// Inserts cards at CardPilePosition.Random, which the game resolves as one
    /// Rng.Shuffle.NextInt(count + 1) per card — not a shuffle of the whole pile.
    /// </summary>
    /// <summary>
    /// <c>CardPileCmd.AddGeneratedCardToCombat(card, PileType.Discard, CardPilePosition.Random)</c>.
    /// </summary>
    /// <remarks>
    /// The same rule as the draw-pile version and off the same stream — a random discard
    /// placement is not a cosmetic detail, because it decides the order the pile comes
    /// back in when it is reshuffled, and it spends shuffle draws either way.
    /// </remarks>
    internal static void AddCardToDiscardPileRandomly(
        CombatState state,
        int cardId,
        int count,
        Random rng
    )
    {
        var placementRng = state.ShuffleRng ?? rng;
        for (int i = 0; i < count; i++)
        {
            state.DiscardPile.Insert(
                placementRng.Next(state.DiscardPile.Count + 1),
                new CardInstance(cardId, false)
            );
        }
    }

    internal static void AddCardToDrawPileRandomly(
        CombatState state,
        int cardId,
        int count,
        Random rng
    )
    {
        // CardPilePosition.Random draws from Rng.Shuffle, not the combat stream: the
        // insert point is Shuffle.NextInt(pile.Count + 1). Rolling it off the wrong
        // stream lands the card in a plausible place on a different turn, which shows up
        // as damage that arrives early or late rather than as anything obviously random.
        var placementRng = state.ShuffleRng ?? rng;
        for (int i = 0; i < count; i++)
        {
            // A shuffle-in, not a placement: the player does not see where it went.
            state.InsertIntoDrawPile(
                placementRng.Next(state.DrawPile.Count + 1),
                new CardInstance(cardId, false)
            );
        }
    }

    public static void DealDamageToAll(CombatState state, int amount)
    {
        foreach (var enemy in state.Enemies.Where(e => e.Hp > 0).ToList())
        {
            DealDamageToEnemy(state, enemy, amount);
        }
    }

    // Deals `amount` to first living enemy, `hits` times.
    /// <summary>
    /// A single-targeted attack that lands <paramref name="hits" /> times on ONE enemy.
    /// </summary>
    /// <remarks>
    /// <c>AttackCommand.Execute</c> re-filters its possible targets before every hit and
    /// breaks out when none are alive. For a single-targeted attack that list is just the
    /// one chosen target, so a target that dies partway through EATS the remaining hits --
    /// they are not rolled onto whoever is standing next.
    ///
    /// The target was re-resolved per hit here, and <c>FirstEnemy</c> falls back to the
    /// first living enemy once the chosen one is dead. So Twin Strike into a 3 hp slime
    /// killed it and put the second 5 into a different slime, against a live capture where
    /// the game dealt that damage to nobody.
    /// </remarks>
    public static void DealDamageMultiHit(CombatState state, int amount, int hits, Random rng)
    {
        var target = FirstEnemy(state);
        for (int i = 0; i < hits; i++)
        {
            if (target is null || target.Hp <= 0)
            {
                break;
            }

            DealDamageToEnemy(state, target, amount);
        }
    }

    // Deals `amount` to every living enemy, repeated `hits` times.
    public static void DealDamageToAllMultiHit(CombatState state, int amount, int hits)
    {
        for (int i = 0; i < hits; i++)
        {
            foreach (var enemy in state.Enemies.Where(e => e.Hp > 0).ToList())
            {
                DealDamageToEnemy(state, enemy, amount);
            }
        }
    }

    /// <summary>
    /// Plays a card from inside another card's effect (Havoc, Mayhem). There is no way to
    /// hand a selection screen back to the caller from here, so any choice the nested card
    /// raises resolves itself. Saves and restores rather than clearing, so a nested play
    /// inside an auto-play does not hand agency back to the outer one.
    /// </summary>
    private static void PlayNestedCard(
        CardDef def,
        bool upgraded,
        CombatState state,
        Random rng,
        CardInstance card = default
    )
    {
        bool wasAutoPlaying = state.AutoPlaying;
        state.AutoPlaying = true;
        try
        {
            Apply(def, upgraded, state, rng, card);
        }
        finally
        {
            state.AutoPlaying = wasAutoPlaying;
        }
    }

    /// <summary>
    /// Raises a card-selection screen, or resolves it immediately when the caller cannot
    /// answer one.
    ///
    /// An auto-played card (Havoc, Hellraiser, Stampede, Mayhem) resolves inside a queue
    /// the engine is already draining, with no way to hand control back mid-drain, so it
    /// falls back to <paramref name="autoPick" /> — the behaviour every one of these
    /// choices had before selection existed.
    /// </summary>
    private static void OpenCardSelection(
        CombatState state,
        CardSelectionKind kind,
        int candidateCount,
        int sourceCardDefId,
        int autoPick,
        int amount = 0
    ) =>
        OpenCardSelection(
            state,
            kind,
            [.. Enumerable.Range(0, candidateCount)],
            sourceCardDefId,
            autoPick,
            amount
        );

    /// <summary>
    /// Opens a selection over an explicit set of pile indices, which is how a card that
    /// only offers part of a pile (Secret Weapon's Attacks) keeps its filter in the
    /// candidate list rather than in the resolution.
    /// </summary>
    private static void OpenCardSelection(
        CombatState state,
        CardSelectionKind kind,
        List<int> candidates,
        int sourceCardDefId,
        int autoPick,
        int amount = 0
    )
    {
        if (candidates.Count == 0)
        {
            return;
        }

        if (state.AutoPlaying)
        {
            ResolveSelectionImmediately(state, kind, autoPick);
            return;
        }

        state.PendingSelection = new PendingCardSelection
        {
            Kind = kind,
            Candidates = candidates,
            SourceCardDefId = sourceCardDefId,
            Amount = amount,
        };
    }

    /// <summary>
    /// Auto-plays cards off the draw pile, as Catastrophe and Cascade do. The game shuffles
    /// the pile on the Shuffle stream and takes the front, preferring a playable card and
    /// falling back to any card when every one left is Unplayable.
    /// </summary>
    private static void AutoPlayFromDrawPile(CombatState state, int count)
    {
        for (int i = 0; i < count && state.DrawPile.Count > 0; i++)
        {
            int index = state.DrawPile.FindIndex(c => !GeneratedData.Cards.Get(c.DefId).Unplayable);
            if (index < 0)
            {
                index = 0;
            }

            var card = state.DrawPile[index];
            state.RemoveFromDrawPileAt(index);
            state.AutoPlayQueue.Add(card);
        }
    }

    /// <summary>
    /// Beat Down's three Attacks out of the discard pile. Unplayable attacks are skipped,
    /// which is the filter the card itself carries.
    /// </summary>
    private static void AutoPlayAttacksFromDiscard(CombatState state, int count)
    {
        for (int i = 0; i < count; i++)
        {
            int index = state.DiscardPile.FindIndex(c =>
            {
                var d = GeneratedData.Cards.Get(c.DefId);
                return d.Type == CardType.Attack && !d.Unplayable;
            });
            if (index < 0)
            {
                return;
            }

            var card = state.DiscardPile[index];
            state.DiscardPile.RemoveAt(index);
            state.AutoPlayQueue.Add(card);
        }
    }

    /// <summary>
    /// Jackpot's three free cards: rolled from the character pool, restricted to a printed
    /// cost of zero and no X, upgraded when Jackpot is, straight into hand.
    /// </summary>
    private static void AddZeroCostCardsToHand(CombatState state, int count, bool upgraded)
    {
        var pool = GeneratedData
            .CardPools.Ironclad.ToArray()
            .Where(id =>
            {
                var d = GeneratedData.Cards.Get(id);
                return d.Cost == 0 && !d.HasEnergyCostX;
            })
            .ToArray();
        if (pool.Length == 0)
        {
            return;
        }

        var rng = state.CardGenerationRng;
        for (int i = 0; i < count && state.Hand.Count < MaxCardsInHand; i++)
        {
            int id = pool[(rng as Random ?? new Random(0)).Next(pool.Length)];
            state.Hand.Add(new CardInstance(id, upgraded));
        }
    }

    /// <summary>
    /// Seeker Strike: a sample of the draw pile, of which one card comes to hand. The
    /// sample is what the card offers, so it lives in the candidate list.
    /// </summary>
    private static void OpenDrawPileSampleSelection(
        CombatState state,
        int sourceCardDefId,
        int sample
    )
    {
        var candidates = Enumerable.Range(0, state.DrawPile.Count).Take(sample).ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        OpenCardSelection(
            state,
            CardSelectionKind.DrawPileToHand,
            candidates,
            sourceCardDefId,
            autoPick: candidates[0]
        );
    }

    /// <summary>
    /// Hidden Gem: one card in the draw pile gains Replay. The game prefers an Attack,
    /// Skill or Power that is playable and has no replay yet, and settles for any playable
    /// card when there is no such thing.
    /// </summary>
    private static void GrantReplayToADrawPileCard(CombatState state, int replay)
    {
        var eligible = Enumerable
            .Range(0, state.DrawPile.Count)
            .Where(i =>
            {
                var d = GeneratedData.Cards.Get(state.DrawPile[i].DefId);
                return !d.Unplayable
                    && d.Type is not (CardType.Status or CardType.Curse)
                    && state.DrawPile[i].ReplayCount < 1;
            })
            .ToList();
        var preferred = eligible
            .Where(i =>
                GeneratedData.Cards.Get(state.DrawPile[i].DefId).Type
                    is CardType.Attack
                        or CardType.Skill
                        or CardType.Power
            )
            .ToList();
        var pick = preferred.Count > 0 ? preferred : eligible;
        if (pick.Count == 0)
        {
            return;
        }

        var rng = state.CardSelectionRng;
        int index = pick[(rng as Random ?? new Random(0)).Next(pick.Count)];
        state.DrawPile[index] = state.DrawPile[index] with
        {
            ReplayCount = state.DrawPile[index].ReplayCount + replay,
        };
    }

    /// <summary>
    /// Offers every draw-pile card of a type, for the cards that let you fetch one.
    /// Nothing opens when the pile holds none, which is the game's behaviour too.
    /// </summary>
    private static void OpenDrawPileSelection(CombatState state, int sourceCardDefId, CardType type)
    {
        var candidates = state
            .DrawPile.Select((card, index) => (card, index))
            .Where(entry => GeneratedData.Cards.Get(entry.card.DefId).Type == type)
            .Select(entry => entry.index)
            .ToList();

        OpenCardSelection(
            state,
            CardSelectionKind.DrawPileToHand,
            candidates,
            sourceCardDefId,
            autoPick: candidates.Count > 0 ? candidates[0] : 0
        );
    }

    /// <summary>
    /// Rolls distinct cards off the generation stream and offers them, for a card that
    /// lets you pick one to create. The options live on the selection because they are in
    /// no pile until the choice is made.
    /// </summary>
    private static void OpenGeneratedCardSelection(
        CombatState state,
        int sourceCardDefId,
        int optionCount,
        Random rng
    )
    {
        var generationRng = CardGenerationRng(state, rng);
        var options = new List<int>();
        for (int attempt = 0; attempt < optionCount * 8 && options.Count < optionCount; attempt++)
        {
            int defId = _colorlessPool[generationRng.Next(_colorlessPool.Length)];
            if (!options.Contains(defId))
            {
                options.Add(defId);
            }
        }

        if (options.Count == 0)
        {
            return;
        }

        if (state.AutoPlaying)
        {
            if (state.Hand.Count < MaxCardsInHand)
            {
                state.Hand.Add(new CardInstance(options[0], false, FreeThisTurn: true));
            }

            return;
        }

        state.PendingSelection = new PendingCardSelection
        {
            Kind = CardSelectionKind.GeneratedCardToHand,
            Candidates = [.. Enumerable.Range(0, options.Count)],
            SourceCardDefId = sourceCardDefId,
            GeneratedCandidates = options,
        };
    }

    /// <summary>
    /// Opens the discard screen the five discard-a-CHOSEN-card Silent cards raise, and
    /// carries whatever the card does afterwards.
    /// </summary>
    /// <remarks>
    /// The tail matters for Hidden Daggers, whose Shivs are created AFTER the discard and
    /// so must not be candidates for it. It is flushed here rather than at the call site
    /// because there are two ways the screen never opens — an empty hand, and an
    /// auto-played card that answers its own selections — and the tail has to run in both.
    /// </remarks>
    internal static void OpenDiscardSelection(
        CombatState state,
        int sourceCardDefId,
        int amount,
        List<CardInstance>? afterwards = null
    )
    {
        var tail = afterwards ?? [];
        if (state.Hand.Count == 0)
        {
            AddCardsToHand(state, tail);
            return;
        }

        if (state.AutoPlaying)
        {
            for (int pick = 0; pick < amount && state.Hand.Count > 0; pick++)
            {
                ResolveSelectionImmediately(state, CardSelectionKind.DiscardFromHandRepeated, 0);
            }

            AddCardsToHand(state, tail);
            return;
        }

        state.PendingSelection = new PendingCardSelection
        {
            Kind = CardSelectionKind.DiscardFromHandRepeated,
            Candidates = [.. Enumerable.Range(0, state.Hand.Count)],
            SourceCardDefId = sourceCardDefId,
            Amount = amount,
            AfterSelectionToHand = tail,
        };
    }

    /// <summary>Reopens the discard screen for its next pick.</summary>
    internal static void ReopenDiscardSelection(
        CombatState state,
        int sourceCardDefId,
        int remaining,
        List<CardInstance> afterwards
    ) => OpenDiscardSelection(state, sourceCardDefId, remaining, afterwards);

    internal static void AddCardsToHand(CombatState state, List<CardInstance> cards)
    {
        foreach (var card in cards)
        {
            if (state.Hand.Count >= MaxCardsInHand)
            {
                state.DiscardPile.Add(card);
                continue;
            }

            state.Hand.Add(card);
        }
    }

    /// <summary>Reopens Purity's screen for its next pick; see CardSelectionKind.</summary>
    internal static void ReopenExhaustSelection(
        CombatState state,
        int sourceCardDefId,
        int remaining
    ) =>
        OpenCardSelection(
            state,
            CardSelectionKind.ExhaustFromHandRepeated,
            state.Hand.Count,
            sourceCardDefId,
            autoPick: 0,
            amount: remaining
        );

    private static void ResolveSelectionImmediately(
        CombatState state,
        CardSelectionKind kind,
        int index
    )
    {
        switch (kind)
        {
            case CardSelectionKind.DiscardToDrawPileTop when index < state.DiscardPile.Count:
            {
                var card = state.DiscardPile[index];
                state.DiscardPile.RemoveAt(index);
                state.TopDeck(card);
                break;
            }

            case CardSelectionKind.ExhaustFromHand when index < state.Hand.Count:
            case CardSelectionKind.ExhaustFromHandThenDraw when index < state.Hand.Count:
            case CardSelectionKind.ExhaustFromHandRepeated when index < state.Hand.Count:
            {
                var card = state.Hand[index];
                state.Hand.RemoveAt(index);
                ExhaustCard(state, card);
                break;
            }

            case CardSelectionKind.DrawPileToHand when index < state.DrawPile.Count:
            {
                var card = state.DrawPile[index];
                state.RemoveFromDrawPileAt(index);
                state.Hand.Add(card);
                break;
            }

            case CardSelectionKind.HandToDrawPileTop when index < state.Hand.Count:
            {
                var card = state.Hand[index];
                state.Hand.RemoveAt(index);
                state.TopDeck(card);
                break;
            }

            case CardSelectionKind.MarkHandCardSly when index < state.Hand.Count:
                state.Hand[index] = state.Hand[index] with { SlyThisTurn = true };
                break;

            case CardSelectionKind.DiscardFromHandRepeated when index < state.Hand.Count:
            {
                var card = state.Hand[index];
                state.Hand.RemoveAt(index);
                DiscardMovedCards(state, [card]);
                break;
            }
        }
    }

    /// <summary>
    /// The stream an effect draws from when it picks WHICH existing card to act on.
    ///
    /// The game reads Rng.CombatCardSelection for these (Cinder, Thrash, True Grit,
    /// EntropyPower); drawing from the combat rng instead desynchronises the stream for
    /// everything after it, exactly as target choice did before TargetRng.
    /// </summary>
    private static Random CardSelectionRng(CombatState state, Random rng) =>
        state.CardSelectionRng ?? rng;

    /// <summary>
    /// The stream an effect draws from when it rolls up a NEW card — Stoke, Splash,
    /// Infernal Blade and Discovery all read Rng.CombatCardGeneration in the game, which
    /// is a different subsystem from the one that picks among existing cards.
    /// </summary>
    private static Random CardGenerationRng(CombatState state, Random rng) =>
        state.CardGenerationRng ?? rng;

    /// <summary>
    /// Picks the enemy an effect hits at random, off the run's combat_targets stream.
    ///
    /// The game draws every target choice from <c>Rng.CombatTargets</c>
    /// (JuggernautPower: <c>Rng.CombatTargets.NextItem(HittableEnemies)</c>), so drawing
    /// from the combat RNG desynchronises the stream for everything that follows, the
    /// same way enemy intents did before AiRng existed.
    /// </summary>
    internal static EnemyState? RandomLivingEnemy(CombatState state, Random? rng)
    {
        var living = state.Enemies.Where(e => e.Hp > 0).ToList();
        if (living.Count == 0)
        {
            return null;
        }

        // TargetRng is the run's stream and is set by every real entry point; rng is the
        // combat RNG, which single-combat tests pass. Only a caller with neither lands on
        // the first enemy, and that is the shape of the bug this replaced — so prefer
        // threading an rng over relying on it.
        var targetRng = state.TargetRng ?? rng;
        return targetRng == null ? living[0] : living[targetRng.Next(living.Count)];
    }

    private static void DealDamageToRandomEnemiesMultiHit(
        CombatState state,
        int amount,
        int hits,
        Random rng
    )
    {
        for (int i = 0; i < hits; i++)
        {
            var target = RandomLivingEnemy(state, rng);
            if (target == null)
            {
                return;
            }

            DealDamageToEnemy(state, target, amount);
        }
    }

    private static int DealDamageToEnemy(CombatState state, EnemyState target, int amount)
    {
        TriggerEnemyThorns(state, target);

        int damage = BuffSystem.IncomingDamage(amount, state.PlayerBuffs, target.Buffs);
        int slowCount = BuffSystem.Get(target.Buffs, BuffId.SlowCount);
        if (BuffSystem.Get(target.Buffs, BuffId.Slow) > 0 && slowCount > 0)
        {
            damage = (int)(damage * (1f + 0.1f * slowCount));
        }

        int cap = BuffSystem.Get(target.Buffs, BuffId.HardToKill);
        if (cap > 0)
        {
            damage = Math.Min(damage, cap);
        }

        int absorbed = Math.Min(target.Block, damage);
        target.Block -= absorbed;
        int hpLoss = damage - absorbed;

        int hardened = BuffSystem.Get(target.Buffs, BuffId.HardenedShell);
        if (hardened > 0)
        {
            hpLoss = Math.Min(hpLoss, hardened);
            BuffSystem.Apply(target.Buffs, BuffId.HardenedShell, -hpLoss);
        }

        int slippery = BuffSystem.Get(target.Buffs, BuffId.Slippery);
        if (slippery > 0 && hpLoss >= 1)
        {
            hpLoss = 1;
            BuffSystem.Apply(target.Buffs, BuffId.Slippery, -1);
        }
        target.Hp = Math.Max(0, target.Hp - hpLoss);
        if (hpLoss > 0)
        {
            EnemyAI.TriggerShriekIfWounded(target);
        }

        // BurrowedPower.AfterBlockBroken -- checked on every hit, because breaking the
        // burrow is the only way to interrupt a Tunneler and it can happen mid-attack.
        EnemyAI.BreakBurrowIfBlockGone(target);

        // SlumberPower.AfterDamageReceived: a sleeper loses a point of sleep for every
        // INSTANCE of unblocked damage, so a multi-hit attack wakes it faster than one
        // big one. Counting turns alone was right only for a beetle nobody hit -- and it
        // sleeps behind Plating, so hitting it is exactly what a player does.
        if (hpLoss > 0 && BuffSystem.Get(target.Buffs, BuffId.Slumber) > 0)
        {
            BuffSystem.Apply(target.Buffs, BuffId.Slumber, -1);
        }

        if (hpLoss > 0)
        {
            int envenom = BuffSystem.Get(state.PlayerBuffs, BuffId.Envenom);
            if (envenom > 0)
            {
                BuffSystem.Apply(target.Buffs, BuffId.Poison, envenom);
            }

            int personalHive = BuffSystem.Get(target.Buffs, BuffId.PersonalHive);
            for (int i = 0; i < personalHive; i++)
            {
                state.BottomDeck(new CardInstance(ST.Dazed, false));
            }

            int curlUp = BuffSystem.Get(target.Buffs, BuffId.CurlUp);
            if (curlUp > 0)
            {
                target.Block += curlUp;
                BuffSystem.Remove(target.Buffs, BuffId.CurlUp);
            }

            // SkittishPower.AfterAttack: the FIRST card each turn to land unblocked
            // damage on the gardener makes it flinch behind N block. Unlike Curl Up the
            // power stays -- it is spent for the turn, not consumed -- and the flag
            // clears when the player's turn ends. This sits inside the `hpLoss > 0`
            // branch because the game requires UnblockedDamage != 0: a hit the gardener
            // fully blocks does not set it off.
            int skittish = BuffSystem.Get(target.Buffs, BuffId.Skittish);
            if (skittish > 0 && BuffSystem.Get(target.Buffs, BuffId.SkittishSpent) == 0)
            {
                BuffSystem.Apply(target.Buffs, BuffId.SkittishSpent, 1);
                target.Block += skittish;
            }
        }
        if (target.Hp == 0)
        {
            OnEnemyDeath(state, target);
        }

        return hpLoss;
    }

    private static void OnEnemyDeath(CombatState state, EnemyState enemy)
    {
        // ShrinkerBeetle: permanent Shrink (ShrinkPower) is removed when its applier dies.
        if (enemy.DefId == KE.ShrinkerBeetle)
        {
            BuffSystem.Remove(state.PlayerBuffs, BuffId.Shrink);
        }
    }

    public static void GainBlock(CombatState state, int amount, Random? rng = null) =>
        GainBlock(state, amount, rng, powered: true);

    public static void GainUnpoweredBlock(CombatState state, int amount, Random? rng = null) =>
        GainBlock(state, amount, rng, powered: false);

    private static void GainBlock(
        CombatState state,
        int amount,
        Random? rng = null,
        bool powered = true,
        bool isDefend = false
    )
    {
        int effective = powered
            ? BuffSystem.IncomingBlock(amount, state.PlayerBuffs, isDefend)
            : amount;
        if (effective <= 0)
        {
            return;
        }

        int unmovable = BuffSystem.Get(state.PlayerBuffs, BuffId.UnmovablePower);
        if (unmovable > state.BlockGainsThisTurn)
        {
            effective *= 2;
            state.BlockGainsThisTurn++;
        }

        state.PlayerBlock += effective;

        // Juggernaut: JuggernautPower.AfterBlockGained deals base.Amount to
        // Rng.CombatTargets.NextItem(HittableEnemies) as ValueProp.Unpowered.
        int jug = BuffSystem.Get(state.PlayerBuffs, BuffId.Juggernaut);
        if (jug > 0)
        {
            var target = RandomLivingEnemy(state, rng);
            if (target != null)
            {
                DealUnpoweredDamageToEnemy(state, target, jug);
            }
        }
    }

    // Deals unblockable, unpowered HP loss to the player and triggers Rupture.
    public static void LoseHp(CombatState state, int amount)
    {
        int hpBefore = state.PlayerHp;
        int buffer = BuffSystem.Get(state.PlayerBuffs, BuffId.Buffer);
        if (amount > 0 && buffer > 0)
        {
            if (buffer == 1)
            {
                BuffSystem.Remove(state.PlayerBuffs, BuffId.Buffer);
            }
            else
            {
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Buffer, -1);
            }

            return;
        }

        state.PlayerHp = Math.Max(0, state.PlayerHp - amount);
        state.PlayerHpLostThisTurn += Math.Max(0, hpBefore - state.PlayerHp);

        int rupt = BuffSystem.Get(state.PlayerBuffs, BuffId.RupturePower);
        if (rupt > 0 && hpBefore > state.PlayerHp)
        {
            BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, rupt);
        }

        TriggerInfernoAfterPlayerSelfDamage(state, hpBefore - state.PlayerHp);
    }

    public static void ChannelOrb(CombatState state, OrbType type, Random? rng = null)
    {
        if (state.OrbCapacity <= 0)
        {
            return;
        }

        if (state.Orbs.Count >= state.OrbCapacity)
        {
            EvokeNextOrb(state, rng, dequeue: true);
        }

        if (state.Orbs.Count < state.OrbCapacity)
        {
            state.Orbs.Add(
                new OrbState(type, type == OrbType.Dark ? DarkBaseEvokeValue(state) : 0)
            );
            if (type == OrbType.Lightning)
            {
                state.LightningOrbsChanneledThisCombat++;
            }
        }
    }

    public static void EvokeNextOrb(CombatState state, Random? rng, bool dequeue = true)
    {
        if (state.Orbs.Count == 0)
        {
            return;
        }

        var orb = state.Orbs[0];
        EvokeOrb(state, orb, rng);
        if (dequeue)
        {
            state.Orbs.RemoveAt(0);
        }
    }

    public static void EvokeLastOrb(CombatState state, Random? rng)
    {
        if (state.Orbs.Count == 0)
        {
            return;
        }

        var orb = state.Orbs[^1];
        EvokeOrb(state, orb, rng);
        state.Orbs.RemoveAt(state.Orbs.Count - 1);
    }

    public static void ChannelRandomOrb(CombatState state, Random rng) =>
        ChannelOrb(state, (OrbType)rng.Next(4));

    public static void TriggerOrbPassive(CombatState state, int index, Random rng)
    {
        if ((uint)index >= (uint)state.Orbs.Count)
        {
            return;
        }

        var orb = state.Orbs[index];
        switch (orb.Type)
        {
            case OrbType.Lightning:
                DealUnpoweredDamageToRandomEnemy(state, LightningPassiveValue(state), rng);
                break;
            case OrbType.Frost:
                GainUnpoweredBlock(state, FrostPassiveValue(state), rng);
                break;
            case OrbType.Dark:
                state.Orbs[index] = orb with
                {
                    EvokeValue = orb.EvokeValue + DarkPassiveValue(state),
                };
                break;
            case OrbType.Plasma:
                state.Energy += 1;
                break;
            case OrbType.Glass:
                DrawCards(state, 1, rng);
                break;
        }
    }

    public static void TriggerAllOrbBeforeTurnEndPassives(CombatState state, Random rng)
    {
        for (int i = 0; i < state.Orbs.Count; i++)
        {
            if (state.Orbs[i].Type != OrbType.Plasma)
            {
                TriggerOrbPassive(state, i, rng);
            }
        }
    }

    public static void TriggerAllOrbAfterTurnStartPassives(CombatState state, Random rng)
    {
        for (int i = 0; i < state.Orbs.Count; i++)
        {
            if (state.Orbs[i].Type == OrbType.Plasma)
            {
                TriggerOrbPassive(state, i, rng);
            }
        }
    }

    public static void AddRandomDefectPowerCardsToHand(CombatState state, int count, Random rng)
    {
        int[] powers =
        [
            39, // BiasedCognition
            63, // Buffer
            64, // BulkUp
            78, // Capacitor
            111, // CreativeAi
            137, // Defragment
            159, // EchoForm
            288, // Loop
            291, // MachineLearning
            419, // SentryMode
            467, // Storm
            507, // Thunder
            515, // TrashToTreasure
        ];

        for (int i = 0; i < count && state.Hand.Count < MaxCardsInHand; i++)
        {
            state.Hand.Add(new CardInstance(powers[rng.Next(powers.Length)], false));
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static bool ApplyDefectCard(
        CardDef def,
        bool upgraded,
        CombatState state,
        Random rng,
        CardInstance card
    )
    {
        switch (def.Name)
        {
            case "StrikeDefect":
            case "AdaptiveStrike":
            case "MomentumStrike":
            case "TeslaCoil":
            case "Synthesis":
            case "WroughtInWar":
                DealDamage(state, Dmg(def, upgraded, card));
                if (def.Name == "AdaptiveStrike")
                {
                    state.DiscardPile.Add(new CardInstance(def.Id, upgraded, CostForCombat: 0));
                }
                else if (def.Name == "MomentumStrike")
                {
                    card = card with { CostForCombat = 0 };
                }
                else if (def.Name == "Synthesis")
                {
                    BuffSystem.Apply(state.PlayerBuffs, BuffId.FreeAttackPower, 1);
                }

                return true;
            case "DefendDefect":
            case "Leap":
            case "BootSequence":
            case "BoostAway":
            case "FightThrough":
            case "Glasswork":
            case "Glacier":
            case "ShadowShield":
            case "ChargeBattery":
            case "LightningRod":
                GainBlock(state, Blk(def, upgraded, card), rng);
                if (def.Name == "BoostAway")
                {
                    AddGeneratedStatusToDiscard(state, ST.Dazed, rng);
                }
                else if (def.Name == "FightThrough")
                {
                    AddGeneratedStatusToDiscard(state, ST.Wound, rng);
                    AddGeneratedStatusToDiscard(state, ST.Wound, rng);
                }
                else if (def.Name == "Glasswork")
                {
                    ChannelOrb(state, OrbType.Glass);
                }
                else if (def.Name == "Glacier")
                {
                    ChannelOrb(state, OrbType.Frost);
                    ChannelOrb(state, OrbType.Frost);
                }
                else if (def.Name == "ShadowShield")
                {
                    ChannelOrb(state, OrbType.Dark);
                }
                else if (def.Name == "ChargeBattery")
                {
                    BuffSystem.Apply(state.PlayerBuffs, BuffId.NextTurnEnergy, 1);
                }
                else if (def.Name == "LightningRod")
                {
                    BuffSystem.Apply(state.PlayerBuffs, BuffId.Thunder, upgraded ? 3 : 2);
                }

                return true;
            case "BallLightning":
                DealDamage(state, Dmg(def, upgraded, card));
                ChannelOrb(state, OrbType.Lightning);
                return true;
            case "ColdSnap":
                DealDamage(state, Dmg(def, upgraded, card));
                ChannelOrb(state, OrbType.Frost);
                return true;
            case "Null":
            {
                var target = FirstEnemy(state);
                if (target != null)
                {
                    DealDamageToEnemy(state, target, Dmg(def, upgraded, card));
                    ApplyEnemyDebuffToTarget(state, target, BuffId.Weak, upgraded ? 3 : 2, rng);
                }

                ChannelOrb(state, OrbType.Dark);
                return true;
            }
            case "Barrage":
                DealDamageMultiHit(state, Dmg(def, upgraded, card), state.Orbs.Count, rng);
                return true;
            case "BeamCell":
            {
                var target = FirstEnemy(state);
                if (target != null)
                {
                    DealDamageToEnemy(state, target, Dmg(def, upgraded, card));
                    ApplyEnemyDebuffToTarget(
                        state,
                        target,
                        BuffId.Vulnerable,
                        upgraded ? 2 : 1,
                        rng
                    );
                }

                return true;
            }
            case "Claw":
                DealDamage(
                    state,
                    Dmg(def, upgraded, card) + BuffSystem.Get(state.PlayerBuffs, BuffId.ClawDamage)
                );
                BuffSystem.Apply(state.PlayerBuffs, BuffId.ClawDamage, upgraded ? 3 : 2);
                return true;
            case "CompileDriver":
                DealDamage(state, Dmg(def, upgraded, card));
                DrawCards(state, state.Orbs.Select(o => o.Type).Distinct().Count(), rng);
                return true;
            case "Coolheaded":
                ChannelOrb(state, OrbType.Frost);
                DrawCards(state, upgraded ? 2 : 1, rng);
                return true;
            case "Zap":
                ChannelOrb(state, OrbType.Lightning);
                return true;
            case "Dualcast":
                EvokeNextOrb(state, rng, dequeue: false);
                EvokeNextOrb(state, rng);
                return true;
            case "Capacitor":
                state.OrbCapacity = Math.Min(10, state.OrbCapacity + (upgraded ? 3 : 2));
                return true;
            case "Defragment":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Focus, upgraded ? 2 : 1);
                return true;
            case "BiasedCognition":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Focus, upgraded ? 5 : 4);
                return true;
            case "Buffer":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Buffer, upgraded ? 2 : 1);
                return true;
            case "Loop":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Loop, upgraded ? 2 : 1);
                return true;
            case "MachineLearning":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.MachineLearning, 1);
                return true;
            case "Storm":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Storm, upgraded ? 2 : 1);
                return true;
            case "Thunder":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Thunder, upgraded ? 8 : 6);
                return true;
            case "EchoForm":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.EchoForm, 1);
                return true;
            case "Darkness":
                ChannelOrb(state, OrbType.Dark);
                for (int i = 0; i < state.Orbs.Count; i++)
                {
                    if (state.Orbs[i].Type != OrbType.Dark)
                    {
                        continue;
                    }

                    TriggerOrbPassive(state, i, rng);
                    if (upgraded)
                    {
                        TriggerOrbPassive(state, i, rng);
                    }
                }

                return true;
            case "Chill":
                foreach (var _ in state.Enemies.Where(e => e.Hp > 0))
                {
                    ChannelOrb(state, OrbType.Frost);
                }

                return true;
            case "Fusion":
            case "Ignition":
                ChannelOrb(state, OrbType.Plasma);
                return true;
            case "Chaos":
                for (int i = 0; i < (upgraded ? 2 : 1); i++)
                {
                    ChannelOrb(state, (OrbType)rng.Next(4));
                }

                return true;
            case "Rainbow":
                ChannelOrb(state, OrbType.Lightning);
                ChannelOrb(state, OrbType.Frost);
                ChannelOrb(state, OrbType.Dark);
                return true;
            case "IceLance":
                DealDamage(state, Dmg(def, upgraded, card));
                for (int i = 0; i < 3; i++)
                {
                    ChannelOrb(state, OrbType.Frost);
                }

                return true;
            case "MeteorStrike":
                DealDamage(state, Dmg(def, upgraded, card));
                for (int i = 0; i < 3; i++)
                {
                    ChannelOrb(state, OrbType.Plasma);
                }

                return true;
            case "Tempest":
            {
                int x = state.Energy + (upgraded ? 1 : 0);
                state.Energy = 0;
                for (int i = 0; i < x; i++)
                {
                    ChannelOrb(state, OrbType.Lightning);
                }

                return true;
            }
            case "MultiCast":
            {
                int x = state.Energy + (upgraded ? 1 : 0);
                state.Energy = 0;
                for (int i = 0; i < x; i++)
                {
                    EvokeNextOrb(state, rng, dequeue: i == x - 1);
                }

                return true;
            }
            case "Quadcast":
                for (int i = 0; i < 4; i++)
                {
                    EvokeNextOrb(state, rng, dequeue: i == 3);
                }

                return true;
            case "Shatter":
            {
                DealDamageToAll(state, Dmg(def, upgraded, card));
                int orbCount = state.Orbs.Count;
                for (int i = 0; i < orbCount; i++)
                {
                    EvokeNextOrb(state, rng, dequeue: false);
                    EvokeNextOrb(state, rng);
                }

                return true;
            }
            case "Hyperbeam":
                DealDamageToAll(state, Dmg(def, upgraded, card));
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Focus, -3);
                return true;
            case "Sunder":
            {
                var target = FirstEnemy(state);
                if (target != null)
                {
                    int hpBefore = target.Hp;
                    DealDamageToEnemy(state, target, Dmg(def, upgraded, card));
                    if (hpBefore > 0 && target.Hp == 0)
                    {
                        state.Energy += 3;
                    }
                }

                return true;
            }
            case "Turbo":
                state.Energy += upgraded ? 3 : 2;
                return true;
            case "DoubleEnergy":
                state.Energy *= 2;
                return true;
            case "AllForOne":
                DealDamage(state, Dmg(def, upgraded, card));
                MoveZeroCostDiscardCardsToHand(state);
                return true;
            case "BulkUp":
                state.OrbCapacity = Math.Max(0, state.OrbCapacity - 2);
                while (state.Orbs.Count > state.OrbCapacity)
                {
                    state.Orbs.RemoveAt(state.Orbs.Count - 1);
                }

                BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, upgraded ? 3 : 2);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Dexterity, upgraded ? 3 : 2);
                return true;
            case "Compact":
                GainBlock(state, upgraded ? 7 : 6, rng);
                TransformStatusesInHandToFuel(state, upgraded);
                return true;
            case "ConsumingShadow":
                for (int i = 0; i < (upgraded ? 3 : 2); i++)
                {
                    ChannelOrb(state, OrbType.Dark);
                }

                BuffSystem.Apply(state.PlayerBuffs, BuffId.ConsumingShadow, 1);
                return true;
            case "Coolant":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Coolant, upgraded ? 3 : 2);
                return true;
            case "CreativeAi":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.CreativeAi, 1);
                return true;
            case "Feral":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Feral, 1);
                return true;
            case "FlakCannon":
            {
                int statuses = ExhaustStatusesOutsideExhaustPile(state, rng);
                DealDamageToRandomEnemiesMultiHit(state, Dmg(def, upgraded, card), statuses, rng);
                return true;
            }
            case "FocusedStrike":
                DealDamage(state, Dmg(def, upgraded, card));
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Focus, upgraded ? 2 : 1);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.TemporaryFocus, upgraded ? 2 : 1);
                return true;
            case "GeneticAlgorithm":
                GainBlock(state, Blk(def, upgraded, card), rng);
                return true;
            case "Hailstorm":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Hailstorm, upgraded ? 8 : 6);
                return true;
            case "HelixDrill":
                DealDamageMultiHit(state, Dmg(def, upgraded, card), state.Energy, rng);
                return true;
            case "Hologram":
                GainBlock(state, Blk(def, upgraded, card), rng);
                MoveDiscardCardsToHand(state, 1);
                return true;
            case "Hotfix":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Focus, 2);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.TemporaryFocus, 2);
                return true;
            case "Iteration":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.NextTurnDraw, upgraded ? 3 : 2);
                return true;
            case "Refract":
                DealDamage(state, Dmg(def, upgraded, card));
                for (int i = 0; i < 2; i++)
                {
                    ChannelOrb(state, OrbType.Glass);
                }

                return true;
            case "RocketPunch":
                DealDamage(state, Dmg(def, upgraded, card));
                DrawCards(state, upgraded ? 2 : 1, rng);
                return true;
            case "Scavenge":
                ExhaustFirstCardsFromHand(state, 1, rng);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.NextTurnEnergy, upgraded ? 3 : 2);
                return true;
            case "SignalBoost":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.SignalBoost, 1);
                return true;
            case "Smokestack":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Smokestack, upgraded ? 7 : 5);
                return true;
            case "Spinner":
                ChannelOrb(state, OrbType.Glass);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Spinner, 1);
                return true;
            case "Subroutine":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Subroutine, 1);
                return true;
            case "Synchronize":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Focus, 2);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.TemporaryFocus, 2);
                return true;
            case "TrashToTreasure":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.TrashToTreasure, 1);
                return true;
            case "Uproar":
                DealDamageMultiHit(state, Dmg(def, upgraded, card), 2, rng);
                AutoPlayFirstDrawPileAttack(state, rng);
                return true;
            case "Voltaic":
                for (int i = 0; i < state.LightningOrbsChanneledThisCombat; i++)
                {
                    ChannelOrb(state, OrbType.Lightning);
                }

                return true;
            case "WhiteNoise":
                AddRandomDefectPowerCardsToHand(state, 1, rng);
                if (state.Hand.Count > 0)
                {
                    state.Hand[^1] = state.Hand[^1] with { FreeThisTurn = true };
                }

                return true;
            case "EnergySurge":
                state.Energy += upgraded ? 3 : 2;
                return true;
            case "Supercritical":
                state.Energy += upgraded ? 6 : 4;
                return true;
            case "Skim":
                DrawCards(state, upgraded ? 4 : 3, rng);
                return true;
            case "Overclock":
                DrawCards(state, upgraded ? 3 : 2, rng);
                AddGeneratedStatusToDiscard(state, ST.Burn, rng);
                return true;
            case "Reboot":
                // Reboot puts the hand into the draw pile and then calls the game's
                // shuffle, which folds the discard pile in as well.
                state.DrawPile.AddRange(state.Hand);
                state.Hand.Clear();
                ShuffleDiscardIntoDraw(state, rng);
                DrawCards(state, upgraded ? 6 : 4, rng);
                return true;
            case "Scrape":
            {
                DealDamage(state, Dmg(def, upgraded, card));
                int before = state.Hand.Count;
                DrawCards(state, upgraded ? 5 : 4, rng);
                for (int i = state.Hand.Count - 1; i >= before; i--)
                {
                    var drawnDef = GeneratedData.Cards.Get(state.Hand[i].DefId);
                    int cost =
                        state.Hand[i].CostForCombat == int.MinValue
                            ? drawnDef.Cost
                            : state.Hand[i].CostForCombat;
                    if (cost != 0 || drawnDef.Cost < 0)
                    {
                        state.DiscardPile.Add(state.Hand[i] with { FreeThisTurn = false });
                        state.Hand.RemoveAt(i);
                    }
                }

                return true;
            }
            case "SweepingBeam":
                DealDamageToAll(state, Dmg(def, upgraded, card));
                DrawCards(state, 1, rng);
                return true;
            case "Ftl":
                DealDamage(state, Dmg(def, upgraded, card));
                if (state.CardPlaysThisTurn < (upgraded ? 4 : 3))
                {
                    DrawCards(state, 1, rng);
                }

                return true;
            case "GoForTheEyes":
            {
                var target = FirstEnemy(state);
                if (target != null)
                {
                    DealDamageToEnemy(state, target, Dmg(def, upgraded, card));
                    if (target.CurrentIntent.Type == IntentType.Attack)
                    {
                        ApplyEnemyDebuffToTarget(state, target, BuffId.Weak, upgraded ? 2 : 1, rng);
                    }
                }

                return true;
            }
            case "GunkUp":
                DealDamageMultiHit(state, Dmg(def, upgraded, card), 3, rng);
                AddGeneratedStatusToDiscard(state, ST.Slimed, rng);
                return true;
        }

        return false;
    }

    private static bool ApplyNecrobinderCard(
        CardDef def,
        bool upgraded,
        CombatState state,
        Random rng,
        CardInstance card
    )
    {
        switch (def.Name)
        {
            case "StrikeNecrobinder":
            case "BansheesCry":
            case "Bury":
            case "Defile":
            case "Eradicate":
            case "Reap":
            case "Sow":
            case "Veilpiercer":
                DealDamage(state, Dmg(def, upgraded, card));
                return true;
            case "DefendNecrobinder":
            case "Undeath":
                GainBlock(state, Blk(def, upgraded, card), rng);
                if (def.Name == "Undeath")
                {
                    state.DiscardPile.Add(new CardInstance(446, false));
                }

                return true;
            case "Afterlife":
                SummonOsty(state, upgraded ? 9 : 6);
                return true;
            case "Bodyguard":
                SummonOsty(state, upgraded ? 7 : 5);
                return true;
            case "Cleanse":
                SummonOsty(state, upgraded ? 5 : 3);
                ExhaustFirstDrawPileCard(state, rng);
                return true;
            case "Dirge":
                SummonOsty(state, upgraded ? 4 : 3);
                AddSoulsToDrawPile(state, upgraded ? 4 : 3, upgraded);
                return true;
            case "NecroMastery":
                SummonOsty(state, upgraded ? 13 : 10);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, 1);
                return true;
            case "PullAggro":
                SummonOsty(state, upgraded ? 5 : 4);
                GainBlock(state, upgraded ? 9 : 7, rng);
                return true;
            case "Reanimate":
                SummonOsty(state, upgraded ? 25 : 20);
                return true;
            case "Spur":
                SummonOsty(state, upgraded ? 5 : 3);
                if (state.OstyHp > 0)
                {
                    state.OstyHp = Math.Min(state.OstyMaxHp, state.OstyHp + (upgraded ? 7 : 5));
                }

                return true;
            case "BoneShards":
                if (state.OstyHp > 0)
                {
                    DealDamage(state, upgraded ? 12 : 9);
                    KillOsty(state);
                }

                GainBlock(state, upgraded ? 12 : 9, rng);
                return true;
            case "Fetch":
                DealOstyDamage(state, upgraded ? 6 : 3);
                if (state.OstyHp > 0)
                {
                    DrawCards(state, 1, rng);
                }

                return true;
            case "Flatten":
                DealOstyDamage(state, upgraded ? 16 : 12);
                return true;
            case "Poke":
                DealOstyDamage(state, upgraded ? 9 : 6);
                return true;
            case "Rattle":
                DealOstyDamage(state, upgraded ? 9 : 7);
                return true;
            case "RightHandHand":
                DealOstyDamage(state, upgraded ? 6 : 4);
                return true;
            case "Sacrifice":
                if (state.OstyMaxHp > 0)
                {
                    int block = state.OstyMaxHp * 2;
                    KillOsty(state);
                    GainBlock(state, block, rng);
                }

                return true;
            case "SicEm":
                DealOstyDamage(state, upgraded ? 6 : 5);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, upgraded ? 3 : 2);
                return true;
            case "Snap":
                DealOstyDamage(state, upgraded ? 10 : 7);
                AddSoulToDiscard(state, retain: true);
                return true;
            case "BlightStrike":
            {
                var target = FirstEnemy(state);
                if (target != null)
                {
                    DealDamageToEnemy(state, target, Dmg(def, upgraded, card));
                    ApplyEnemyDebuffToTarget(state, target, BuffId.Doom, 4, rng);
                }

                return true;
            }
            case "Deathbringer":
                ApplyEnemyDebuff(state, BuffId.Doom, upgraded ? 26 : 21, rng);
                ApplyEnemyDebuff(state, BuffId.Weak, 1, rng);
                return true;
            case "EndOfDays":
                ApplyAllEnemyDebuff(state, BuffId.Doom, upgraded ? 37 : 29, rng);
                KillDoomedEnemies(state);
                return true;
            case "NegativePulse":
                GainBlock(state, upgraded ? 6 : 5, rng);
                ApplyAllEnemyDebuff(state, BuffId.Doom, upgraded ? 11 : 7, rng);
                return true;
            case "Oblivion":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Doom, upgraded ? 4 : 3);
                return true;
            case "Scourge":
                ApplyEnemyDebuff(state, BuffId.Doom, upgraded ? 16 : 13, rng);
                DrawCards(state, upgraded ? 2 : 1, rng);
                return true;
            case "Defy":
                GainBlock(state, Blk(def, upgraded, card), rng);
                ApplyEnemyDebuff(state, BuffId.Weak, 1, rng);
                return true;
            case "Delay":
                GainBlock(state, Blk(def, upgraded, card), rng);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.NextTurnEnergy, upgraded ? 2 : 1);
                return true;
            case "DeathsDoor":
            {
                int gains = state.PlayerHp <= state.PlayerMaxHp / 2 ? (upgraded ? 4 : 3) : 1;
                for (int i = 0; i < gains; i++)
                {
                    GainBlock(state, upgraded ? 7 : 6, rng);
                }

                return true;
            }
            case "Fear":
            {
                var target = FirstEnemy(state);
                if (target != null)
                {
                    DealDamageToEnemy(state, target, Dmg(def, upgraded, card));
                    ApplyEnemyDebuffToTarget(
                        state,
                        target,
                        BuffId.Vulnerable,
                        upgraded ? 2 : 1,
                        rng
                    );
                }

                return true;
            }
            case "Putrefy":
                ApplyAllEnemyDebuff(state, BuffId.Weak, upgraded ? 2 : 1, rng);
                ApplyAllEnemyDebuff(state, BuffId.Vulnerable, upgraded ? 2 : 1, rng);
                return true;
            case "Parse":
                DrawCards(state, upgraded ? 4 : 3, rng);
                return true;
            case "BorrowedTime":
                state.Energy += upgraded ? 6 : 4;
                BuffSystem.Apply(state.PlayerBuffs, BuffId.NoBlock, 1);
                return true;
            case "Neurosurge":
                state.Energy += upgraded ? 4 : 3;
                DrawCards(state, 2, rng);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.NoBlock, 1);
                return true;
            case "Wisp":
                state.Energy += 1;
                return true;
            case "DrainPower":
                DealDamage(state, Dmg(def, upgraded, card));
                UpgradeDiscardCards(state, upgraded ? 3 : 2);
                return true;
            case "Dredge":
                MoveDiscardCardsToHand(state, 3);
                return true;
            case "GraveWarden":
                GainBlock(state, upgraded ? 11 : 8, rng);
                AddSoulsToDrawPile(state, 1, upgraded: false);
                return true;
            case "Graveblast":
                DealDamage(state, Dmg(def, upgraded, card));
                MoveDiscardCardsToHand(state, 1);
                return true;
            case "Reave":
                DealDamage(state, Dmg(def, upgraded, card));
                AddSoulsToDrawPile(state, 1, upgraded);
                return true;
            case "Severance":
                DealDamage(state, Dmg(def, upgraded, card));
                AddSoulsToDrawPile(state, 1, false);
                AddSoulToDiscard(state);
                AddSoulToHand(state);
                return true;
            case "GlimpseBeyond":
                AddSoulsToDrawPile(state, upgraded ? 4 : 3, upgraded: false);
                return true;
            case "CaptureSpirit":
                LoseHp(state, upgraded ? 4 : 3);
                AddSoulsToDrawPile(state, upgraded ? 4 : 3, upgraded: false);
                return true;
            case "Eidolon":
                ExhaustFirstCardsFromHand(state, state.Hand.Count, rng);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Intangible, 1);
                return true;
            case "SharedFate":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, upgraded ? 2 : 1);
                ApplyTemporaryStrengthDownToEnemy(state, upgraded ? 2 : 1);
                return true;
            case "Shroud":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.BlockNextTurn, upgraded ? 3 : 2);
                return true;
            case "SoulStorm":
                DealDamageToAll(state, state.ExhaustPile.Count * (upgraded ? 3 : 2));
                return true;
            case "TheScythe":
                DealDamage(state, 13 + state.CardsExhaustedThisTurn * (upgraded ? 2 : 1));
                return true;
            case "TimesUp":
                DealDamage(state, state.CardsPlayedThisCombat * (upgraded ? 2 : 1));
                return true;
            case "DeathMarch":
                DealDamage(state, 8 + state.DrawnCardsSinceAutomationProc * (upgraded ? 6 : 4));
                return true;
            case "Hang":
                DealDamage(state, Dmg(def, upgraded, card));
                ApplyEnemyDebuff(state, BuffId.Constrict, 2, rng);
                return true;
            case "Invoke":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.NextTurnEnergy, upgraded ? 3 : 2);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.CrimsonMantleBlock, upgraded ? 3 : 2);
                return true;
            case "PullFromBelow":
                DealDamageMultiHit(
                    state,
                    Dmg(def, upgraded, card),
                    Math.Max(1, state.EtherealExhaustCount),
                    rng
                );
                return true;
            case "SculptingStrike":
                DealDamage(state, Dmg(def, upgraded, card));
                if (state.Hand.Count > 0)
                {
                    state.Hand[0] = state.Hand[0] with { Retain = true };
                }

                return true;
            case "Seance":
                if (state.DrawPile.Count > 0)
                {
                    state.DrawPile[0] = new CardInstance(446, false);
                }

                return true;
            case "Transfigure":
                TransformRandomCardInHand(state, rng);
                state.Energy += 1;
                return true;
            case "Unleash":
                DealDamage(state, 6 + state.OstyMaxHp / Math.Max(1, upgraded ? 3 : 4));
                return true;
            case "Squeeze":
                DealDamage(state, 5 + state.OstyMaxHp * (upgraded ? 6 : 5));
                return true;
            case "Protector":
                DealDamage(state, (upgraded ? 5 : 0) + state.OstyMaxHp);
                return true;
            case "Calcify":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Plating, upgraded ? 6 : 4);
                return true;
            case "CallOfTheVoid":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.NextTurnDraw, 1);
                return true;
            case "Countdown":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.TheBombPower, 6);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.TheBombDamage, upgraded ? 9 : 6);
                return true;
            case "DanseMacabre":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.NextTurnEnergy, 2);
                return true;
            case "Debilitate":
                DealDamage(state, Dmg(def, upgraded, card));
                ApplyTemporaryStrengthDownToEnemy(state, upgraded ? 3 : 2);
                return true;
            case "Demesne":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.NextTurnEnergy, 1);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.NextTurnDraw, 1);
                return true;
            case "DevourLife":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.NoxiousFumes, upgraded ? 2 : 1);
                return true;
            case "EnfeeblingTouch":
                ApplyTemporaryStrengthDownToEnemy(state, upgraded ? 6 : 3);
                return true;
            case "ForbiddenGrimoire":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.DarkEmbrace, 1);
                return true;
            case "Friendship":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, upgraded ? 1 : 2);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.NextTurnEnergy, 1);
                return true;
            case "Melancholy":
            case "Misery":
            case "ReaperForm":
            case "SentryMode":
            case "SleightOfFlesh":
            case "SpiritOfAsh":
            case "Pagestorm":
            case "NoEscape":
            case "Lethality":
            case "HighFive":
            case "Haunt":
            case "LegionOfBone":
            case "ReanimatePower":
                ApplyBaseDamageAndBlock(def, upgraded, state, card, rng);
                return true;
        }

        return false;
    }

    private static bool ApplyRegentCard(
        CardDef def,
        bool upgraded,
        CombatState state,
        Random rng,
        CardInstance card
    )
    {
        switch (def.Name)
        {
            case "StrikeRegent":
            case "AstralPulse":
            case "Bombardment":
            case "Devastate":
            case "KinglyKick":
                DealDamage(state, Dmg(def, upgraded, card));
                return true;
            case "DefendRegent":
            case "Bulwark":
            case "CloakOfStars":
            case "CosmicIndifference":
            case "IAmInvincible":
            case "ParticleWall":
            case "Reflect":
                GainBlock(state, Blk(def, upgraded, card), rng);
                if (def.Name == "CosmicIndifference")
                {
                    MoveFirstHandCardToTopOfDrawPile(state);
                }
                else if (def.Name == "IAmInvincible")
                {
                    AutoPlayFirstDrawPileAttack(state, rng);
                }
                else if (def.Name == "Reflect")
                {
                    BuffSystem.Apply(state.PlayerBuffs, BuffId.Thorns, 1);
                }

                return true;
            case "CelestialMight":
                DealDamageMultiHit(state, Dmg(def, upgraded, card), upgraded ? 4 : 3, rng);
                return true;
            case "Comet":
            case "FallingStar":
            case "GammaBlast":
            case "MeteorShower":
            {
                var target = FirstEnemy(state);
                if (target != null)
                {
                    DealDamageToEnemy(state, target, Dmg(def, upgraded, card));
                    int debuff =
                        def.Name == "Comet" ? 3
                        : def.Name is "GammaBlast" or "MeteorShower" ? 2
                        : 1;
                    ApplyEnemyDebuffToTarget(state, target, BuffId.Weak, debuff, rng);
                    ApplyEnemyDebuffToTarget(state, target, BuffId.Vulnerable, debuff, rng);
                }

                return true;
            }
            case "BeatIntoShape":
                DealDamage(
                    state,
                    Dmg(def, upgraded, card) + state.PlayerBlock / Math.Max(1, upgraded ? 3 : 4)
                );
                return true;
            case "CollisionCourse":
                DealDamage(state, Dmg(def, upgraded, card));
                state.Hand.Add(new CardInstance(532, false, FreeThisTurn: true));
                return true;
            case "CrashLanding":
                DealDamage(state, Dmg(def, upgraded, card));
                AddRandomRegentCardsToHand(state, 2, rng);
                return true;
            case "CrushUnder":
                DealDamage(state, Dmg(def, upgraded, card));
                ApplyTemporaryStrengthDownToEnemy(state, upgraded ? 2 : 1);
                return true;
            case "DecisionsDecisions":
                DrawCards(state, upgraded ? 5 : 3, rng);
                return true;
            case "DyingStar":
                DealDamage(state, Dmg(def, upgraded, card));
                ApplyTemporaryStrengthDownToEnemy(state, upgraded ? 5 : 3);
                return true;
            case "GatherLight":
                GainBlock(state, Blk(def, upgraded, card), rng);
                state.Stars += 1;
                return true;
            case "Glitterstream":
                GainBlock(state, upgraded ? 13 : 11, rng);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.BlockNextTurn, upgraded ? 7 : 5);
                return true;
            case "GuidingStar":
                DealDamage(state, Dmg(def, upgraded, card));
                DrawCards(state, upgraded ? 3 : 2, rng);
                return true;
            case "HeavenlyDrill":
                DealDamage(state, Dmg(def, upgraded, card));
                if (state.Stars >= 4)
                {
                    state.Stars -= 4;
                    state.Energy += 4;
                }

                return true;
            case "Hegemony":
                DealDamage(state, Dmg(def, upgraded, card));
                BuffSystem.Apply(state.PlayerBuffs, BuffId.NextTurnEnergy, upgraded ? 3 : 2);
                return true;
            case "HeirloomHammer":
                DealDamage(state, Dmg(def, upgraded, card));
                AddRandomRegentCardsToHand(state, upgraded ? 2 : 1, rng);
                return true;
            case "KinglyPunch":
                DealDamage(
                    state,
                    Dmg(def, upgraded, card) + state.CardsPlayedThisCombat * (upgraded ? 6 : 4)
                );
                return true;
            case "KnockoutBlow":
            {
                var target = FirstEnemy(state);
                if (target != null)
                {
                    int hpBefore = target.Hp;
                    DealDamageToEnemy(state, target, Dmg(def, upgraded, card));
                    if (hpBefore > 0 && target.Hp == 0)
                    {
                        state.Stars += 5;
                    }
                }

                return true;
            }
            case "LunarBlast":
                DealDamageMultiHit(state, Dmg(def, upgraded, card), Math.Max(1, state.Stars), rng);
                return true;
            case "MakeItSo":
                DealDamage(state, Dmg(def, upgraded, card));
                return true;
            case "ManifestAuthority":
                GainBlock(state, Blk(def, upgraded, card), rng);
                AddRandomRegentCardsToHand(state, 1, rng);
                return true;
            case "Patter":
                GainBlock(state, Blk(def, upgraded, card), rng);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Vigor, upgraded ? 3 : 2);
                return true;
            case "PhotonCut":
                DealDamage(state, Dmg(def, upgraded, card));
                DrawCards(state, upgraded ? 2 : 1, rng);
                MoveFirstHandCardToTopOfDrawPile(state);
                return true;
            case "PillarOfCreation":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.BlockNextTurn, upgraded ? 4 : 3);
                return true;
            case "Radiate":
                DealDamageMultiHit(state, Dmg(def, upgraded, card), Math.Max(1, state.Stars), rng);
                return true;
            case "SevenStars":
                DealDamageMultiHit(state, Dmg(def, upgraded, card), 7, rng);
                return true;
            case "ShiningStrike":
                DealDamage(state, Dmg(def, upgraded, card));
                state.Stars += 2;
                return true;
            case "SolarStrike":
                DealDamage(state, Dmg(def, upgraded, card));
                state.Stars += upgraded ? 2 : 1;
                return true;
            case "Stardust":
            {
                int x = state.Stars;
                state.Stars = 0;
                DealDamageMultiHit(state, Dmg(def, upgraded, card), x, rng);
                return true;
            }
        }

        return false;
    }

    private static bool ApplyMiscGeneratedCard(
        CardDef def,
        bool upgraded,
        CombatState state,
        Random rng,
        CardInstance card
    )
    {
        switch (def.Name)
        {
            case "ByrdSwoop":
            case "Clash":
            case "GiantRock":
            case "Maul":
            case "Rebound":
            case "Squash":
            case "UltimateStrike":
            case "TagTeam":
            case "Knockdown":
            case "Whistle":
            case "MinionDiveBomb":
                DealDamage(state, Dmg(def, upgraded, card));
                if (def.Name == "Squash")
                {
                    ApplyEnemyDebuff(state, BuffId.Vulnerable, upgraded ? 3 : 2, rng);
                }
                else if (def.Name == "Knockdown")
                {
                    ApplyEnemyDebuff(state, BuffId.Stunned, upgraded ? 3 : 2, rng);
                }
                else if (def.Name == "Whistle")
                {
                    ApplyEnemyDebuff(state, BuffId.Stunned, 1, rng);
                }
                else if (def.Name == "SeekerStrike")
                {
                    // The game shuffles the draw pile and offers three; the emulator
                    // offers every Attack in it rather than a sampled three.
                    OpenDrawPileSelection(state, def.Id, CardType.Attack);
                }

                return true;
            case "Exterminate":
                DealDamageMultiHit(state, Dmg(def, upgraded, card), 4, rng);
                return true;
            case "Peck":
                DealDamageMultiHit(state, Dmg(def, upgraded, card), upgraded ? 4 : 3, rng);
                return true;
            case "RipAndTear":
                DealDamageToRandomEnemiesMultiHit(state, Dmg(def, upgraded, card), 2, rng);
                return true;
            case "Intercept":
                GainBlock(state, Blk(def, upgraded, card), rng);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Intangible, 1);
                return true;
            case "Lift":
            case "Rally":
            case "TheGambit":
            case "ToricToughness":
            case "MinionSacrifice":
                GainBlock(state, Blk(def, upgraded, card), rng);
                if (def.Name == "TheGambit")
                {
                    // TheGambitPower kills you on the next unblocked powered attack.
                    // NoBlock stood in for it, which is a far milder card.
                    BuffSystem.Apply(state.PlayerBuffs, BuffId.TheGambitPower, 1);
                }

                return true;
            case "Relax":
                GainBlock(state, Blk(def, upgraded, card), rng);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.NextTurnDraw, upgraded ? 3 : 2);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.NextTurnEnergy, upgraded ? 3 : 2);
                return true;
            case "MadScience":
                DealDamage(state, upgraded ? 14 : 12);
                GainBlock(state, upgraded ? 10 : 8, rng);
                ApplyEnemyDebuff(state, BuffId.Weak, 2, rng);
                ApplyEnemyDebuff(state, BuffId.Vulnerable, 2, rng);
                state.Energy += 2;
                DrawCards(state, 3, rng);
                return true;
            case "MinionStrike":
                DealDamage(state, Dmg(def, upgraded, card));
                DrawCards(state, 1, rng);
                return true;
            case "Shiv":
                DealDamage(
                    state,
                    Dmg(def, upgraded, card) + BuffSystem.Get(state.PlayerBuffs, BuffId.ShivDamage)
                );
                return true;
            case "SovereignBlade":
                DealDamageMultiHit(state, Dmg(def, upgraded, card), 1, rng);
                if (state.PlayerBlock > 0)
                {
                    GainBlock(state, state.PlayerBlock, rng);
                }

                return true;
            case "SporeMind":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.NoBlock, 1);
                return true;
        }

        return false;
    }

    private static void ApplyGeneratedCardApproximation(
        CardDef def,
        bool upgraded,
        CombatState state,
        Random rng,
        CardInstance card
    )
    {
        if (def.Type is CardType.Status or CardType.Curse)
        {
            return;
        }

        if (ApplyDefectCard(def, upgraded, state, rng, card))
        {
            return;
        }

        if (ApplyNecrobinderCard(def, upgraded, state, rng, card))
        {
            return;
        }

        if (ApplyRegentCard(def, upgraded, state, rng, card))
        {
            return;
        }

        if (ApplyMiscGeneratedCard(def, upgraded, state, rng, card))
        {
            return;
        }

        ApplyBaseDamageAndBlock(def, upgraded, state, card, rng);

        switch (def.Name)
        {
            case "Alignment":
            case "EnergySurge":
            case "Luminesce":
            case "Supercritical":
            case "Wisp":
                state.Energy += upgraded ? 2 : 1;
                break;
            case "BorrowedTime":
                state.Energy += upgraded ? 3 : 2;
                BuffSystem.Apply(state.PlayerBuffs, BuffId.NoBlock, 1);
                break;
            case "Acrobatics":
                DrawCards(state, upgraded ? 4 : 3, rng);
                DiscardFirstCardsFromHand(state, 1);
                break;
            case "Accuracy":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.ShivDamage, upgraded ? 6 : 4);
                break;
            case "Adrenaline":
                state.Energy += upgraded ? 2 : 1;
                DrawCards(state, 2, rng);
                break;
            case "Afterimage":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Afterimage, 1);
                break;
            case "Afterlife":
                DrawCards(state, upgraded ? 2 : 1, rng);
                break;
            case "Anticipate":
            case "Prepared":
                DrawCards(state, upgraded ? 2 : 1, rng);
                DiscardFirstCardsFromHand(state, 1);
                break;
            case "Apotheosis":
                UpgradeAllCardsInHand(state);
                UpgradePile(state.DrawPile);
                UpgradePile(state.DiscardPile);
                break;
            case "Apparition":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Intangible, 1);
                break;
            case "Backflip":
            case "Coolheaded":
            case "EscapePlan":
            case "Finesse":
            case "FlashOfSteel":
            case "PommelStrike":
            case "ShrugItOff":
            case "SweepingBeam":
                DrawCards(state, 1, rng);
                break;
            case "BattleTrance":
                DrawCards(state, upgraded ? 4 : 3, rng);
                break;
            case "Begone":
            case "Charge":
            case "Guards":
            case "Seance":
            case "Transfigure":
                TransformRandomCardInHand(state, rng);
                break;
            case "BigBang":
            case "BrightestFlame":
            case "Fuel":
                state.Energy += upgraded ? 3 : 2;
                DrawCards(state, upgraded ? 3 : 2, rng);
                break;
            case "BladeDance":
                AddGeneratedCardsToHand(state, 430, upgraded ? 4 : 3);
                break;
            case "Bodyguard":
                GainBlock(state, upgraded ? 12 : 8, rng);
                break;
            case "Blur":
            case "Equilibrium":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.RetainHand, 1);
                break;
            case "BootSequence":
            case "Mirage":
            case "Sacrifice":
                GainBlock(state, upgraded ? 14 : 10, rng);
                break;
            case "BladeOfInk":
            case "ForegoneConclusion":
            case "HiddenCache":
            case "Hotfix":
            case "KnowThyPlace":
            case "Spur":
            case "TheSmith":
            case "UpMySleeve":
                DrawCards(state, 1, rng);
                state.Energy += upgraded ? 1 : 0;
                break;
            case "BouncingFlask":
                ApplyEnemyDebuff(state, BuffId.Poison, upgraded ? 12 : 9, rng);
                break;
            case "BubbleBubble":
            case "Putrefy":
                ApplyEnemyDebuff(state, BuffId.Poison, upgraded ? 8 : 5, rng);
                break;
            case "BundleOfJoy":
            case "Dirge":
            case "Distraction":
            case "GlimpseBeyond":
            case "Largesse":
            case "Metamorphosis":
            case "Quasar":
            case "WhiteNoise":
                AddRandomClassCardToHand(state, rng, upgraded);
                break;
            case "Burst":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.OneTwoPunch, upgraded ? 2 : 1);
                break;
            case "BulletTime":
            case "Enlightenment":
                MakeHandFreeThisTurn(state);
                break;
            case "CalculatedGamble":
                DiscardFirstCardsFromHand(state, state.Hand.Count);
                DrawCards(state, state.DiscardPile.Count, rng);
                break;
            case "Caltrops":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Thorns, upgraded ? 5 : 3);
                break;
            case "ChargeBattery":
            case "DodgeAndRoll":
            case "Prolong":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.BlockNextTurn, upgraded ? 8 : 5);
                break;
            case "Chill":
            case "Chaos":
            case "Coolant":
            case "Fusion":
            case "Zap":
                ApplyOrbLikeValue(state, def.Name, upgraded, rng);
                break;
            case "Cleanse":
                BuffSystem.Remove(state.PlayerBuffs, BuffId.Vulnerable);
                BuffSystem.Remove(state.PlayerBuffs, BuffId.Weak);
                BuffSystem.Remove(state.PlayerBuffs, BuffId.Frail);
                break;
            case "Claw":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.ClawDamage, upgraded ? 3 : 2);
                break;
            case "CloakAndDagger":
                AddGeneratedCardsToHand(state, 430, upgraded ? 2 : 1);
                break;
            case "Conqueror":
            case "Deathbringer":
            case "Eidolon":
            case "FeedingFrenzy":
            case "NoEscape":
            case "Oblivion":
            case "Resonance":
            case "SharedFate":
            case "Synchronize":
            case "Terraforming":
            case "Voltaic":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, upgraded ? 2 : 1);
                break;
            case "Convergence":
            case "Flanking":
            case "Monologue":
            case "Shadowmeld":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.RetainHand, upgraded ? 2 : 1);
                break;
            case "Coordinate":
                // CoordinatePower is a TemporaryStrengthPower at PowerVar<StrengthPower>(5m),
                // OnUpgrade +3 — temporary Strength for an ally, which is the player in
                // singleplayer. It was grouped with the retain-hand cards, which is a
                // different effect entirely.
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, upgraded ? 8 : 5);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.TemporaryStrength, upgraded ? 8 : 5);
                break;
            case "CorrosiveWave":
                ApplyAllEnemyDebuff(state, BuffId.Poison, upgraded ? 5 : 3, rng);
                ApplyAllEnemyDebuff(state, BuffId.Weak, upgraded ? 3 : 2, rng);
                break;
            case "DaggerThrow":
                DrawCards(state, 1, rng);
                DiscardFirstCardsFromHand(state, 1);
                break;
            case "DeadlyPoison":
            case "PoisonedStab":
            case "Snakebite":
                ApplyEnemyDebuff(state, BuffId.Poison, upgraded ? 7 : 5, rng);
                break;
            case "Defragment":
            case "BiasedCognition":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Focus, upgraded ? 2 : 1);
                break;
            case "DualWield":
            case "Nightmare":
                DuplicateFirstCardInHand(state, upgraded ? 3 : 2);
                break;
            case "Dualcast":
                ApplyOrbLikeValue(state, "Zap", upgraded, rng);
                ApplyOrbLikeValue(state, "Zap", upgraded, rng);
                break;
            case "Dredge":
                MoveDiscardCardsToHand(state, upgraded ? 2 : 1);
                break;
            case "EndOfDays":
                DealUnpoweredDamageToAll(state, upgraded ? 37 : 29);
                break;
            case "DoubleEnergy":
                state.Energy += Math.Max(0, state.Energy);
                break;
            case "EnfeeblingTouch":
            case "Haze":
            case "Scare":
                ApplyEnemyDebuff(state, BuffId.Weak, upgraded ? 3 : 2, rng);
                break;
            case "Entrench":
                GainBlock(state, state.PlayerBlock, rng);
                break;
            case "Envenom":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Envenom, 1);
                break;
            case "Expertise":
                DrawCards(state, Math.Max(0, (upgraded ? 7 : 6) - state.Hand.Count), rng);
                break;
            case "Expose":
            case "Neutralize":
            case "PiercingWail":
            case "SuckerPunch":
                ApplyEnemyDebuff(state, BuffId.Weak, upgraded ? 2 : 1, rng);
                break;
            case "Footwork":
            case "Prowess":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Dexterity, upgraded ? 3 : 2);
                break;
            case "GeneticAlgorithm":
            case "Stack":
                GainBlock(
                    state,
                    upgraded ? state.DiscardPile.Count + 3 : state.DiscardPile.Count,
                    rng
                );
                break;
            case "Ignition":
            case "Invoke":
            case "MultiCast":
            case "Quadcast":
            case "Rainbow":
                ApplyOrbLikeValue(state, "Zap", upgraded, rng);
                ApplyOrbLikeValue(state, "ColdSnap", upgraded, rng);
                break;
            case "KnifeTrap":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Thorns, upgraded ? 6 : 4);
                break;
            case "LegionOfBone":
            case "SummonForth":
                AddRandomClassCardToHand(state, rng, freeThisTurn: true);
                break;
            case "Glimmer":
            case "Glow":
            case "Parse":
            case "Prophesize":
            case "Reflex":
            case "Scourge":
            case "Soul":
            case "SpoilsOfBattle":
                DrawCards(state, upgraded ? 2 : 1, rng);
                break;
            case "GoForTheEyes":
                ApplyEnemyDebuff(state, BuffId.Weak, upgraded ? 2 : 1, rng);
                break;
            case "HiddenDaggers":
                AddGeneratedCardsToHand(state, 430, upgraded ? 3 : 2);
                break;
            case "Hologram":
                MoveDiscardCardsToHand(state, 1);
                break;
            case "InfiniteBlades":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.InfiniteBlades, 1);
                break;
            case "LegSweep":
                ApplyEnemyDebuff(state, BuffId.Weak, upgraded ? 3 : 2, rng);
                break;
            case "Malaise":
                ApplyTemporaryStrengthDownToEnemy(state, state.Energy);
                ApplyEnemyDebuff(state, BuffId.Weak, state.Energy, rng);
                state.Energy = 0;
                break;
            case "NoxiousFumes":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.NoxiousFumes, upgraded ? 3 : 2);
                break;
            case "Overclock":
                DrawCards(state, upgraded ? 3 : 2, rng);
                state.DiscardPile.Add(new CardInstance(10011, false));
                break;
            case "Outmaneuver":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.NextTurnEnergy, upgraded ? 3 : 2);
                break;
            case "Predator":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.NextTurnDraw, upgraded ? 3 : 2);
                break;
            case "Reboot":
                DiscardFirstCardsFromHand(state, state.Hand.Count);
                DrawCards(state, upgraded ? 6 : 4, rng);
                break;
            case "Reanimate":
                MoveDiscardCardsToHand(state, upgraded ? 2 : 1);
                break;
            case "RefineBlade":
                UpgradeFirstCardInHand(state);
                break;
            case "RoyalGamble":
                state.Energy += upgraded ? 10 : 9;
                break;
            case "Scavenge":
                ExhaustRandomCardFromHand(state, rng);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.NextTurnEnergy, upgraded ? 3 : 2);
                break;
            case "ShadowStep":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Intangible, 1);
                break;
            case "SignalBoost":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Focus, upgraded ? 2 : 1);
                break;
            case "Skim":
                DrawCards(state, upgraded ? 4 : 3, rng);
                break;
            case "StormOfSteel":
                int handCount = state.Hand.Count;
                DiscardFirstCardsFromHand(state, handCount);
                AddGeneratedCardsToHand(state, 430, handCount);
                break;
            case "Survivor":
                DiscardFirstCardsFromHand(state, 1);
                break;
            case "Tactician":
            case "Turbo":
                state.Energy += upgraded ? 3 : 2;
                break;
            case "Tempest":
                DealUnpoweredDamageToAll(state, state.Energy * (upgraded ? 6 : 4));
                state.Energy = 0;
                break;
            case "Abrasive":
            case "Accelerant":
            case "Arsenal":
            case "BulkUp":
            case "Calcify":
            case "ChildOfTheStars":
            case "Feral":
            case "Friendship":
            case "Furnace":
            case "Genesis":
            case "HammerTime":
            case "Lethality":
            case "MasterPlanner":
            case "MonarchsGaze":
            case "NecroMastery":
            case "Neurosurge":
            case "NeutronAegis":
            case "PaleBlueDot":
            case "Parry":
            case "ReaperForm":
            case "Royalties":
            case "SeekingEdge":
            case "SerpentForm":
            case "Sneaky":
            case "SpectrumShift":
            case "Speedster":
            case "SpiritOfAsh":
            case "Subroutine":
            case "SwordSage":
            case "TheSealedThrone":
            case "Thunder":
            case "Tracking":
            case "TrashToTreasure":
            case "Tyranny":
            case "VoidForm":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, upgraded ? 2 : 1);
                break;
            case "BlackHole":
            case "CallOfTheVoid":
            case "ConsumingShadow":
            case "Countdown":
            case "DanseMacabre":
            case "Demesne":
            case "DevourLife":
            case "ForbiddenGrimoire":
            case "Haunt":
            case "Outbreak":
            case "Pagestorm":
            case "PhantomBlades":
            case "SentryMode":
            case "Shroud":
            case "SleightOfFlesh":
            case "Smokestack":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.NextTurnDraw, upgraded ? 2 : 1);
                break;
            case "Buffer":
            case "EchoForm":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Intangible, upgraded ? 2 : 1);
                break;
            case "Capacitor":
            case "Hailstorm":
            case "Iteration":
            case "Loop":
            case "MachineLearning":
            case "Orbit":
            case "Spinner":
            case "Storm":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Focus, upgraded ? 2 : 1);
                break;
            case "CreativeAi":
            case "FanOfKnives":
            case "HelloWorld":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.InfiniteBlades, upgraded ? 2 : 1);
                break;
            case "CrescentSpear":
            case "DeathMarch":
            case "Flatten":
            case "MementoMori":
            case "Poke":
            case "PreciseCut":
            case "Rattle":
            case "RightHandHand":
            case "Snap":
            case "Squeeze":
            case "SweepingGaze":
            case "TimesUp":
                DealDamage(state, upgraded ? 12 : 8);
                break;
            case "Murder":
            case "Supermassive":
            case "TheScythe":
                DealDamage(state, upgraded ? 35 : 25);
                break;
            case "Fetch":
            case "HighFive":
            case "Protector":
            case "SicEm":
            case "SoulStorm":
            case "Unleash":
                AddRandomClassCardToHand(state, rng, freeThisTurn: true);
                DealDamage(state, upgraded ? 10 : 7);
                break;
            case "ToolsOfTheTrade":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.ToolsOfTheTrade, 1);
                break;
            case "Venerate":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, upgraded ? 2 : 1);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Dexterity, upgraded ? 2 : 1);
                break;
            case "WellLaidPlans":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.RetainHand, upgraded ? 2 : 1);
                break;
            case "Wish":
                state.PlayerGold += RelicEffects.ModifyGoldGained(state.Relics, upgraded ? 30 : 25);
                break;
            case "WraithForm":
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Intangible, upgraded ? 3 : 2);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Dexterity, -1);
                break;
        }
    }

    private static void ApplyBaseDamageAndBlock(
        CardDef def,
        bool upgraded,
        CombatState state,
        CardInstance card,
        Random? rng
    )
    {
        int dmg = Dmg(def, upgraded, card);
        if (def.Name == "Claw")
        {
            dmg += BuffSystem.Get(state.PlayerBuffs, BuffId.ClawDamage);
        }

        if (def.Name is "Shiv" or "HiddenDaggers")
        {
            dmg += BuffSystem.Get(state.PlayerBuffs, BuffId.ShivDamage);
        }

        int blk = Blk(def, upgraded, card);
        if (dmg > 0)
        {
            DealDamage(state, dmg);
        }

        if (blk > 0)
        {
            GainBlock(state, blk, rng, isDefend: def.Name.Contains("Defend"));
        }
    }

    /// <summary>
    /// The card's attack damage, including the Sharp enchantment. Sharp's
    /// EnchantDamageAdditive adds its amount to any powered attack value, so it
    /// rides along with the card's own damage stat -- per hit for multi-hit cards,
    /// and on top of the upgrade.
    /// </summary>
    private static int Dmg(CardDef def, bool upgraded, CardInstance card)
    {
        int damage =
            (upgraded ? def.BaseDamage + def.UpgradeDamage : def.BaseDamage)
            + card.EnchantedWith(Enchantment.Sharp);

        // Vigorous.EnchantDamageAdditive adds its amount while the enchantment is Normal
        // and nothing once it has fired.
        if (card.Enchantment == Enchantment.Vigorous && !card.EnchantSpent)
        {
            damage += card.EnchantAmount;
        }

        // TezcatarasEmber.EnchantDamageAdditive adds its amount to a powered attack, with
        // no once-only status -- every play, like Sharp.
        if (card.Enchantment == Enchantment.TezcatarasEmber)
        {
            damage += card.EnchantAmount;
        }

        // Corrupted.EnchantDamageMultiplicative is 1.5x on a powered attack, every play --
        // it has no once-only status of its own.
        if (card.Enchantment == Enchantment.Corrupted)
        {
            damage = (int)(damage * 1.5m);
        }

        return damage;
    }

    /// <summary>
    /// The card's block, including the Nimble enchantment.
    /// </summary>
    private static int Blk(CardDef def, bool upgraded, CardInstance card) =>
        (upgraded ? def.BaseBlock + def.UpgradeBlock : def.BaseBlock)
        + card.EnchantedWith(Enchantment.Nimble)
        // Goopy.EnchantBlockAdditive is `Amount - 1`, so a freshly goopied card at 1 adds
        // NOTHING and only starts paying once it has been played.
        + (card.Enchantment == Enchantment.Goopy ? Math.Max(0, card.EnchantAmount - 1) : 0);

    private static EnemyState? FirstEnemy(CombatState state)
    {
        int idx = state.TargetEnemyIndex;
        if (idx >= 0 && idx < state.Enemies.Count && state.Enemies[idx].Hp > 0)
        {
            return state.Enemies[idx];
        }

        return state.Enemies.FirstOrDefault(e => e.Hp > 0);
    }

    private static void ExhaustRandomCardFromHand(CombatState state, Random rng)
    {
        if (state.Hand.Count == 0)
        {
            return;
        }

        int index = CardSelectionRng(state, rng).Next(state.Hand.Count);
        var card = state.Hand[index];
        state.Hand.RemoveAt(index);
        ExhaustCard(state, card, rng: rng);
    }

    private static void ExhaustRandomCardOfTypeFromHand(
        CombatState state,
        CardType type,
        Random rng
    )
    {
        var candidates = state
            .Hand.Select((c, i) => (card: c, idx: i))
            .Where(t => GeneratedData.Cards.Get(t.card.DefId).Type == type)
            .ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        var chosen = candidates[CardSelectionRng(state, rng).Next(candidates.Count)];
        state.Hand.RemoveAt(chosen.idx);
        ExhaustCard(state, chosen.card, rng: rng);
    }

    private static void UpgradeFirstCardInHand(CombatState state)
    {
        for (int i = 0; i < state.Hand.Count; i++)
        {
            if (!IsUpgradable(state.Hand[i]))
            {
                continue;
            }

            state.Hand[i] = state.Hand[i] with { Upgraded = true };
            return;
        }
    }

    private static void UpgradeAllCardsInHand(CombatState state)
    {
        for (int i = 0; i < state.Hand.Count; i++)
        {
            if (IsUpgradable(state.Hand[i]))
            {
                state.Hand[i] = state.Hand[i] with { Upgraded = true };
            }
        }
    }

    private static bool IsUpgradable(CardInstance card)
    {
        if (card.Upgraded)
        {
            return false;
        }

        var def = GeneratedData.Cards.Get(card.DefId);
        return def.Type is not (CardType.Status or CardType.Curse);
    }

    internal const int MaxCardsInHand = 10;

    private static void DrawUntilNonAttack(CombatState state, Random rng)
    {
        while (state.Hand.Count < MaxCardsInHand)
        {
            int handCountBefore = state.Hand.Count;
            DrawCards(state, 1, rng);
            if (state.Hand.Count == handCountBefore)
            {
                return;
            }

            var drawnCard = state.Hand[^1];
            if (GeneratedData.Cards.Get(drawnCard.DefId).Type != CardType.Attack)
            {
                return;
            }
        }
    }

    private static void MoveDiscardCardsToHand(CombatState state, int count)
    {
        int cardsToMove = Math.Min(count, MaxCardsInHand - state.Hand.Count);
        for (int i = 0; i < cardsToMove && state.DiscardPile.Count > 0; i++)
        {
            var card = state.DiscardPile[0];
            state.DiscardPile.RemoveAt(0);
            state.Hand.Add(card with { FreeThisTurn = false });
        }
    }

    private static void MoveZeroCostDiscardCardsToHand(CombatState state)
    {
        for (int i = 0; i < state.DiscardPile.Count && state.Hand.Count < MaxCardsInHand; )
        {
            var card = state.DiscardPile[i];
            var def = GeneratedData.Cards.Get(card.DefId);
            int cost = card.CostForCombat == int.MinValue ? def.Cost : card.CostForCombat;
            if (cost == 0 && def.Type is CardType.Attack or CardType.Skill or CardType.Power)
            {
                state.DiscardPile.RemoveAt(i);
                state.Hand.Add(card with { FreeThisTurn = false });
                continue;
            }

            i++;
        }
    }

    private static void TransformStatusesInHandToFuel(CombatState state, bool upgraded)
    {
        for (int i = 0; i < state.Hand.Count; i++)
        {
            if (GeneratedData.Cards.Get(state.Hand[i].DefId).Type == CardType.Status)
            {
                state.Hand[i] = new CardInstance(209, upgraded);
            }
        }
    }

    private static int ExhaustStatusesOutsideExhaustPile(CombatState state, Random rng)
    {
        int count = 0;
        count += ExhaustStatusesFromPile(state, state.Hand, rng);
        count += ExhaustStatusesFromPile(state, state.DrawPile, rng);
        count += ExhaustStatusesFromPile(state, state.DiscardPile, rng);
        return count;
    }

    private static int ExhaustStatusesFromPile(
        CombatState state,
        List<CardInstance> pile,
        Random rng
    )
    {
        int count = 0;
        for (int i = pile.Count - 1; i >= 0; i--)
        {
            if (GeneratedData.Cards.Get(pile[i].DefId).Type != CardType.Status)
            {
                continue;
            }

            var card = pile[i];
            pile.RemoveAt(i);
            ExhaustCard(state, card, rng: rng);
            count++;
        }

        return count;
    }

    private static void AddGeneratedStatusToDiscard(CombatState state, int statusId, Random rng)
    {
        state.DiscardPile.Add(new CardInstance(statusId, false));
        int smokestack = BuffSystem.Get(state.PlayerBuffs, BuffId.Smokestack);
        if (smokestack > 0)
        {
            DealUnpoweredDamageToAll(state, smokestack);
        }

        int trashToTreasure = BuffSystem.Get(state.PlayerBuffs, BuffId.TrashToTreasure);
        for (int i = 0; i < trashToTreasure; i++)
        {
            ChannelRandomOrb(state, rng);
        }
    }

    private static void AutoPlayFirstDrawPileAttack(CombatState state, Random rng)
    {
        int index = state.DrawPile.FindIndex(card =>
        {
            var def = GeneratedData.Cards.Get(card.DefId);
            return def.Type == CardType.Attack && !def.Unplayable;
        });
        if (index < 0)
        {
            index = state.DrawPile.FindIndex(card =>
                GeneratedData.Cards.Get(card.DefId).Type == CardType.Attack
            );
        }

        if (index < 0)
        {
            return;
        }

        var card = state.DrawPile[index];
        state.RemoveFromDrawPileAt(index);
        var def = GeneratedData.Cards.Get(card.DefId);
        PlayNestedCard(def, card.Upgraded, state, rng, card);
        if (card.IsExhaust())
        {
            ExhaustCard(state, card, rng: rng);
        }
        else
        {
            state.DiscardPile.Add(card with { FreeThisTurn = false });
        }
    }

    private static void SummonOsty(CombatState state, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (state.OstyHp > 0)
        {
            state.OstyMaxHp += amount;
            state.OstyHp += amount;
        }
        else
        {
            state.OstyMaxHp = amount;
            state.OstyHp = amount;
        }
    }

    private static void KillOsty(CombatState state)
    {
        state.OstyHp = 0;
        state.OstyMaxHp = 0;
    }

    private static void DealOstyDamage(CombatState state, int amount)
    {
        if (state.OstyHp <= 0)
        {
            return;
        }

        DealDamage(state, amount);
    }

    private static void ExhaustFirstDrawPileCard(CombatState state, Random rng)
    {
        if (state.DrawPile.Count == 0)
        {
            return;
        }

        var card = state.DrawPile[0];
        state.RemoveFromDrawPileAt(0);
        ExhaustCard(state, card, rng: rng);
    }

    private static void AddSoulToHand(CombatState state)
    {
        if (state.Hand.Count < MaxCardsInHand)
        {
            state.Hand.Add(new CardInstance(446, false));
        }
    }

    private static void AddSoulToDiscard(CombatState state, bool retain = false)
    {
        state.DiscardPile.Add(new CardInstance(446, false, Retain: retain));
    }

    private static void AddSoulsToDrawPile(CombatState state, int count, bool upgraded)
    {
        for (int i = 0; i < count; i++)
        {
            state.BottomDeck(new CardInstance(446, upgraded));
        }
    }

    private static void AddRandomRegentCardsToHand(CombatState state, int count, Random rng)
    {
        int[] cards =
        [
            11, // Alignment
            22, // AstralPulse
            83, // Charge
            81, // CelestialMight
            138, // Defy
            214, // GatherLight
            223, // Glow
            230, // GuidingStar
            274, // KinglyKick
            275, // KinglyPunch
            429, // ShiningStrike
            445, // SolarStrike
            532, // Venerate
        ];

        for (int i = 0; i < count && state.Hand.Count < MaxCardsInHand; i++)
        {
            state.Hand.Add(new CardInstance(cards[rng.Next(cards.Length)], false));
        }
    }

    private static void UpgradeDiscardCards(CombatState state, int count)
    {
        for (int i = 0; i < state.DiscardPile.Count && count > 0; i++)
        {
            if (!IsUpgradable(state.DiscardPile[i]))
            {
                continue;
            }

            state.DiscardPile[i] = state.DiscardPile[i] with { Upgraded = true };
            count--;
        }
    }

    private static void KillDoomedEnemies(CombatState state)
    {
        foreach (var enemy in state.Enemies.Where(e => e.Hp > 0).ToList())
        {
            int doom = BuffSystem.Get(enemy.Buffs, BuffId.Doom);
            if (doom > 0 && enemy.Hp <= doom)
            {
                enemy.Hp = 0;
            }
        }
    }

    private static void TriggerInfernoAfterPlayerSelfDamage(CombatState state, int unblockedDamage)
    {
        int inferno = BuffSystem.Get(state.PlayerBuffs, BuffId.Inferno);
        if (!state.PlayerTurn || unblockedDamage <= 0 || inferno <= 0)
        {
            return;
        }

        foreach (var enemy in state.Enemies.Where(e => e.Hp > 0).ToList())
        {
            DealUnpoweredDamageToEnemy(enemy, inferno);
        }
    }

    private static void DealOmnislice(CombatState state, int amount)
    {
        var target = FirstEnemy(state);
        if (target is null)
        {
            return;
        }

        int splashDamage = DealDamageToEnemy(state, target, amount);
        if (splashDamage <= 0)
        {
            return;
        }

        foreach (
            var enemy in state.Enemies.Where(e => e.Hp > 0 && !ReferenceEquals(e, target)).ToList()
        )
        {
            DealUnpoweredDamageToEnemy(state, enemy, splashDamage, triggerThorns: true);
        }
    }

    private static void AddRandomInfernalBladeAttack(CombatState state, Random rng)
    {
        if (state.Hand.Count >= MaxCardsInHand)
        {
            return;
        }

        int[] options = [.. _infernalBladeAttackPool];
        for (int i = options.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (options[i], options[j]) = (options[j], options[i]);
        }

        state.Hand.Add(new CardInstance(options[0], false, FreeThisTurn: true));
    }

    private static void DealUnpoweredDamageToEnemy(EnemyState target, int amount) =>
        DealUnpoweredDamageToEnemy(null, target, amount, triggerThorns: false);

    internal static void DealUnpoweredDamageToEnemy(
        CombatState state,
        EnemyState target,
        int amount
    ) => DealUnpoweredDamageToEnemy(state, target, amount, triggerThorns: false);

    private static void DealUnpoweredDamageToEnemy(
        CombatState? state,
        EnemyState target,
        int amount,
        bool triggerThorns
    )
    {
        if (triggerThorns && state != null)
        {
            TriggerEnemyThorns(state, target);
        }

        int damage = BuffSystem.CapIncomingDamage(Math.Max(0, amount), target.Buffs);
        int cap = BuffSystem.Get(target.Buffs, BuffId.HardToKill);
        if (cap > 0)
        {
            damage = Math.Min(damage, cap);
        }

        int absorbed = Math.Min(target.Block, damage);
        target.Block -= absorbed;
        int hpLoss = damage - absorbed;
        int slippery = BuffSystem.Get(target.Buffs, BuffId.Slippery);
        if (slippery > 0 && hpLoss >= 1)
        {
            hpLoss = 1;
            BuffSystem.Apply(target.Buffs, BuffId.Slippery, -1);
        }
        target.Hp = Math.Max(0, target.Hp - hpLoss);
    }

    private static void DealUnpoweredDamageToRandomEnemy(CombatState state, int amount, Random? rng)
    {
        var target = RandomLivingEnemy(state, rng);
        if (target != null)
        {
            DealUnpoweredDamageToEnemy(state, target, amount);
        }
    }

    private static void EvokeOrb(CombatState state, OrbState orb, Random? rng)
    {
        switch (orb.Type)
        {
            case OrbType.Lightning:
            {
                var target = FirstEnemy(state);
                if (target == null)
                {
                    return;
                }

                DealUnpoweredDamageToEnemy(state, target, LightningEvokeValue(state));
                int thunder = BuffSystem.Get(state.PlayerBuffs, BuffId.Thunder);
                if (thunder > 0 && target.Hp > 0)
                {
                    DealUnpoweredDamageToEnemy(state, target, thunder);
                }

                break;
            }
            case OrbType.Frost:
                GainUnpoweredBlock(state, FrostEvokeValue(state), rng);
                break;
            case OrbType.Dark:
            {
                var target = state.Enemies.Where(e => e.Hp > 0).MinBy(e => e.Hp);
                if (target != null)
                {
                    DealUnpoweredDamageToEnemy(state, target, orb.EvokeValue);
                }

                break;
            }
            case OrbType.Plasma:
                state.Energy += 2;
                break;
            case OrbType.Glass:
                DrawCards(state, 2, new Random(0));
                break;
        }
    }

    private static int LightningPassiveValue(CombatState state) =>
        Math.Max(0, 3 + BuffSystem.Get(state.PlayerBuffs, BuffId.Focus));

    private static int LightningEvokeValue(CombatState state) =>
        Math.Max(0, 8 + BuffSystem.Get(state.PlayerBuffs, BuffId.Focus));

    private static int FrostPassiveValue(CombatState state) =>
        Math.Max(0, 2 + BuffSystem.Get(state.PlayerBuffs, BuffId.Focus));

    private static int FrostEvokeValue(CombatState state) =>
        Math.Max(0, 5 + BuffSystem.Get(state.PlayerBuffs, BuffId.Focus));

    private static int DarkPassiveValue(CombatState state) =>
        Math.Max(0, 6 + BuffSystem.Get(state.PlayerBuffs, BuffId.Focus));

    private static int DarkBaseEvokeValue(CombatState state) =>
        Math.Max(0, 6 + BuffSystem.Get(state.PlayerBuffs, BuffId.Focus));

    private static void TriggerEnemyThorns(CombatState state, EnemyState target)
    {
        int thorns = BuffSystem.Get(target.Buffs, BuffId.Thorns);
        if (thorns <= 0)
        {
            return;
        }

        int hpBeforeThorns = state.PlayerHp;
        state.PlayerHp = Math.Max(
            0,
            state.PlayerHp - BuffSystem.CapIncomingDamage(thorns, state.PlayerBuffs)
        );
        state.PlayerHpLostThisTurn += Math.Max(0, hpBeforeThorns - state.PlayerHp);
    }

    private static void ApplyEnemyDebuff(CombatState state, BuffId id, int magnitude, Random rng)
    {
        var target = FirstEnemy(state);
        if (target == null)
        {
            return;
        }

        ApplyEnemyDebuffToTarget(state, target, id, magnitude, rng);
    }

    private static void ApplyEnemyDebuffToTarget(
        CombatState state,
        EnemyState target,
        BuffId id,
        int magnitude,
        Random rng
    )
    {
        if (target.Hp <= 0)
        {
            return;
        }

        int before = BuffSystem.Get(target.Buffs, id);
        BuffSystem.Apply(target.Buffs, id, magnitude);
        DrawForVicious(state, id, before, BuffSystem.Get(target.Buffs, id), rng);
    }

    private static void ApplyTemporaryStrengthDownToEnemy(CombatState state, int amount)
    {
        var target = FirstEnemy(state);
        if (target == null)
        {
            return;
        }

        if (BuffSystem.TryConsumeArtifact(target.Buffs))
        {
            return;
        }

        BuffSystem.Apply(target.Buffs, BuffId.Strength, -amount);
        BuffSystem.Apply(target.Buffs, BuffId.TemporaryStrength, amount);
    }

    private static void ApplyAllEnemyDebuff(CombatState state, BuffId id, int magnitude, Random rng)
    {
        foreach (var enemy in state.Enemies.Where(e => e.Hp > 0))
        {
            int before = BuffSystem.Get(enemy.Buffs, id);
            BuffSystem.Apply(enemy.Buffs, id, magnitude);
            DrawForVicious(state, id, before, BuffSystem.Get(enemy.Buffs, id), rng);
        }
    }

    private static void DrawForVicious(
        CombatState state,
        BuffId id,
        int before,
        int after,
        Random rng
    )
    {
        int vicious = BuffSystem.Get(state.PlayerBuffs, BuffId.Vicious);
        if (id == BuffId.Vulnerable && vicious > 0 && after > before)
        {
            DrawCards(state, vicious, rng);
        }
    }

    private static readonly HashSet<string> _strikeNames = new(StringComparer.Ordinal)
    {
        "StrikeIronclad",
        "StrikeSilent",
        "StrikeDefect",
        "StrikeRegent",
        "StrikeNecrobinder",
        "TwinStrike",
        "PommelStrike",
        "PerfectedStrike",
        "SetupStrike",
        "AshenStrike",
        "AdaptiveStrike",
        "BlightStrike",
        "FocusedStrike",
        "LeadingStrike",
        "MeteorStrike",
        "MinionStrike",
        "MomentumStrike",
        "SculptingStrike",
        "SeekerStrike",
        "ShiningStrike",
        "SolarStrike",
        "UltimateStrike",
    };

    internal static void AddRandomUpgradedIroncladCardToHand(
        CombatState state,
        int count,
        Random rng
    )
    {
        for (int i = 0; i < count; i++)
        {
            if (state.Hand.Count >= MaxCardsInHand)
            {
                break;
            }

            int defId = _ironcladPool[CardGenerationRng(state, rng).Next(_ironcladPool.Length)];
            state.Hand.Add(new CardInstance(defId, true));
        }
    }

    internal static void AddRandomAttackCardsToHand(CombatState state, int count, Random rng)
    {
        for (int i = 0; i < count; i++)
        {
            if (state.Hand.Count >= MaxCardsInHand)
            {
                break;
            }

            int defId = _attackPool[CardGenerationRng(state, rng).Next(_attackPool.Length)];
            state.Hand.Add(new CardInstance(defId, false));
        }
    }

    public static void DealUnpoweredDamageToAll(CombatState state, int amount)
    {
        foreach (var enemy in state.Enemies.Where(e => e.Hp > 0).ToList())
        {
            DealUnpoweredDamageToEnemy(state, enemy, amount, triggerThorns: false);
        }
    }

    public static void ApplyPoisonToAllEnemies(CombatState state, int amount, Random rng) =>
        ApplyAllEnemyDebuff(state, BuffId.Poison, amount, rng);

    public static void KillDoomedEnemiesForTurnEnd(CombatState state) => KillDoomedEnemies(state);

    public static void DiscardFirstCardsFromHand(CombatState state, int count)
    {
        var moved = new List<CardInstance>();
        for (int i = 0; i < count && state.Hand.Count > 0; i++)
        {
            moved.Add(state.Hand[0]);
            state.Hand.RemoveAt(0);
        }

        DiscardMovedCards(state, moved);
    }

    /// <summary>
    /// Puts cards that have left the hand into the discard pile the way
    /// <c>CardCmd.DiscardAndDraw</c> does — collecting the Sly ones as they move and
    /// playing each of them once the rest are down.
    /// </summary>
    /// <remarks>
    /// This is the chokepoint on purpose. Every effect-driven discard in the emulator
    /// reaches the pile through here or through the selection resolution beside it, so Sly
    /// is answered once rather than at a dozen call sites — and the sites are in two
    /// different dispatches, the `case SI.X:` arms and the by-NAME fallback, which is
    /// exactly the shape that gets a rule applied to half of them.
    ///
    /// The END-OF-TURN hand discard deliberately does NOT come through here. That cleanup
    /// does not route through <c>CardCmd.Discard</c> in the game either, so holding a
    /// Tactician to the end of the turn is not a free point of energy.
    ///
    /// A Sly card is queued instead of being added to the pile rather than as well as: the
    /// game adds it and then plays it FROM the discard, and playing it here sends it to
    /// whichever pile a played card lands in. Doing both would leave a copy behind.
    /// </remarks>
    internal static void DiscardMovedCards(CombatState state, List<CardInstance> cards)
    {
        var sly = new List<CardInstance>();
        foreach (var card in cards)
        {
            var moved = card with { FreeThisTurn = false };
            if (moved.IsSlyThisTurn())
            {
                sly.Add(moved);
                continue;
            }

            state.DiscardPile.Add(moved);
        }

        state.AutoPlayQueue.AddRange(sly);
    }

    public static void AddGeneratedCardsToHand(CombatState state, int cardId, int count)
    {
        for (int i = 0; i < count && state.Hand.Count < MaxCardsInHand; i++)
        {
            state.Hand.Add(new CardInstance(cardId, false, FreeThisTurn: true));
        }
    }

    private static int CountRendDebuffs(EnemyState enemy)
    {
        int count = 0;
        foreach (var buff in enemy.Buffs)
        {
            if (
                buff.Id
                is BuffId.Vulnerable
                    or BuffId.Weak
                    or BuffId.Frail
                    or BuffId.Poison
                    or BuffId.Burn
                    or BuffId.Shrink
                    or BuffId.Tangled
                    or BuffId.Constrict
                    or BuffId.Smoggy
                    or BuffId.Hex
                    or BuffId.Dampen
                    or BuffId.Disintegration
            )
            {
                count++;
            }
        }
        return count;
    }

    private static int CountCardsOfTypeInHand(CombatState state, CardType type)
    {
        int count = 0;
        foreach (var handCard in state.Hand)
        {
            if (GeneratedData.Cards.Get(handCard.DefId).Type == type)
            {
                count++;
            }
        }
        return count;
    }

    private static void AddRandomColorlessCardsToHand(CombatState state, int count, Random rng)
    {
        for (int i = 0; i < count; i++)
        {
            if (state.Hand.Count >= MaxCardsInHand)
            {
                break;
            }

            int defId = _colorlessPool[CardGenerationRng(state, rng).Next(_colorlessPool.Length)];
            state.Hand.Add(new CardInstance(defId, false));
        }
    }

    private static void AddRandomClassCardToHand(CombatState state, Random rng, bool freeThisTurn)
    {
        if (state.Hand.Count >= MaxCardsInHand)
        {
            return;
        }

        int defId = _generatedClassPool[
            CardGenerationRng(state, rng).Next(_generatedClassPool.Length)
        ];
        state.Hand.Add(new CardInstance(defId, false, FreeThisTurn: freeThisTurn));
    }

    private static void DuplicateFirstCardInHand(CombatState state, int count)
    {
        if (state.Hand.Count == 0)
        {
            return;
        }

        var card = state.Hand[0];
        for (int i = 0; i < count && state.Hand.Count < MaxCardsInHand; i++)
        {
            state.Hand.Add(card with { FreeThisTurn = true });
        }
    }

    private static void ExhaustFirstCardsFromHand(CombatState state, int count, Random rng)
    {
        for (int i = 0; i < count && state.Hand.Count > 0; i++)
        {
            var handCard = state.Hand[0];
            state.Hand.RemoveAt(0);
            ExhaustCard(state, handCard, rng: rng);
        }
    }

    private static void UpgradePile(List<CardInstance> pile)
    {
        for (int i = 0; i < pile.Count; i++)
        {
            if (IsUpgradable(pile[i]))
            {
                pile[i] = pile[i] with { Upgraded = true };
            }
        }
    }

    private static void MakeHandFreeThisTurn(CombatState state)
    {
        for (int i = 0; i < state.Hand.Count; i++)
        {
            state.Hand[i] = state.Hand[i] with { FreeThisTurn = true };
        }
    }

    private static void ApplyOrbLikeValue(
        CombatState state,
        string cardName,
        bool upgraded,
        Random? rng
    )
    {
        int focus = BuffSystem.Get(state.PlayerBuffs, BuffId.Focus);
        switch (cardName)
        {
            case "Zap":
            case "BallLightning":
            case "Chaos":
            {
                var target = FirstEnemy(state);
                if (target != null)
                {
                    DealUnpoweredDamageToEnemy(target, Math.Max(0, (upgraded ? 10 : 8) + focus));
                }

                break;
            }
            case "ColdSnap":
            case "Coolheaded":
            case "Glacier":
            case "Chill":
            case "Coolant":
                GainUnpoweredBlock(state, Math.Max(0, (upgraded ? 7 : 5) + focus), rng);
                break;
            case "Darkness":
                DealUnpoweredDamageToAll(state, Math.Max(0, (upgraded ? 9 : 6) + focus));
                break;
            case "Fusion":
                state.Energy += upgraded ? 2 : 1;
                break;
        }
    }

    private static void MoveFirstDrawPileCardOfTypeToHand(CombatState state, CardType type)
    {
        if (state.Hand.Count >= MaxCardsInHand)
        {
            return;
        }

        int index = state.DrawPile.FindIndex(card =>
            GeneratedData.Cards.Get(card.DefId).Type == type
        );
        if (index < 0)
        {
            return;
        }

        var drawCard = state.DrawPile[index];
        state.RemoveFromDrawPileAt(index);
        state.Hand.Add(drawCard with { FreeThisTurn = false });
    }

    private static void MoveFirstHandCardToTopOfDrawPile(CombatState state)
    {
        if (state.Hand.Count == 0)
        {
            return;
        }

        var handCard = state.Hand[0];
        state.Hand.RemoveAt(0);
        state.TopDeck(handCard with { FreeThisTurn = false });
    }

    private static void MoveStratagemCardsToHandAfterShuffle(CombatState state)
    {
        int stratagem = BuffSystem.Get(state.PlayerBuffs, BuffId.StratagemPower);
        for (int i = 0; i < stratagem && state.DrawPile.Count > 0; i++)
        {
            if (state.Hand.Count >= MaxCardsInHand)
            {
                return;
            }

            var drawCard = state.DrawPile[0];
            state.RemoveFromDrawPileAt(0);
            state.Hand.Add(drawCard with { FreeThisTurn = false });
        }
    }

    private static readonly int[] _ironcladPool =
    [
        IC.Aggression,
        IC.Anger,
        IC.Armaments,
        IC.AshenStrike,
        IC.Barricade,
        IC.BattleTrance,
        IC.BloodWall,
        IC.Bloodletting,
        IC.Bludgeon,
        IC.BodySlam,
        IC.Brand,
        IC.Break,
        IC.Breakthrough,
        IC.Bully,
        IC.BurningPact,
        IC.Cinder,
        IC.Colossus,
        IC.Conflagration,
        IC.Corruption,
        IC.CrimsonMantle,
        IC.Cruelty,
        IC.DarkEmbrace,
        IC.DemonForm,
        IC.DemonicShield,
        IC.Dismantle,
        IC.Dominate,
        IC.DrumOfBattle,
        IC.EvilEye,
        IC.ExpectAFight,
        IC.Feed,
        IC.FeelNoPain,
        IC.FiendFire,
        IC.FightMe,
        IC.FlameBarrier,
        IC.ForgottenRitual,
        IC.Havoc,
        IC.Headbutt,
        IC.Hellraiser,
        IC.Hemokinesis,
        IC.HowlFromBeyond,
        IC.Impervious,
        IC.InfernalBlade,
        IC.Inferno,
        IC.Inflame,
        IC.IronWave,
        IC.Juggernaut,
        IC.Juggling,
        IC.Mangle,
        IC.MoltenFist,
        IC.NotYet,
        IC.Offering,
        IC.OneTwoPunch,
        IC.PactsEnd,
        IC.PerfectedStrike,
        IC.Pillage,
        IC.PommelStrike,
        IC.PrimalForce,
        IC.Pyre,
        IC.Rage,
        IC.Rampage,
        IC.Rupture,
        IC.SecondWind,
        IC.SetupStrike,
        IC.ShrugItOff,
        IC.Spite,
        IC.Stampede,
        IC.Stoke,
        IC.Stomp,
        IC.StoneArmor,
        IC.SwordBoomerang,
        IC.Tank,
        IC.Taunt,
        IC.TearAsunder,
        IC.Thrash,
        IC.Thunderclap,
        IC.Tremble,
        IC.TrueGrit,
        IC.TwinStrike,
        IC.Unmovable,
        IC.Unrelenting,
        IC.Uppercut,
        IC.Vicious,
        IC.Whirlwind,
    ];

    private static readonly int[] _infernalBladeAttackPool =
    [
        IC.Anger,
        IC.AshenStrike,
        IC.BodySlam,
        IC.Break,
        IC.Breakthrough,
        IC.Bludgeon,
        IC.Bully,
        IC.Cinder,
        IC.Conflagration,
        IC.Dismantle,
        IC.FiendFire,
        IC.FightMe,
        IC.Headbutt,
        IC.Hemokinesis,
        IC.HowlFromBeyond,
        IC.Mangle,
        IC.MoltenFist,
        IC.PactsEnd,
        IC.PerfectedStrike,
        IC.Pillage,
        IC.PommelStrike,
        IC.Rampage,
        IC.SetupStrike,
        IC.Spite,
        IC.Stomp,
        IC.SwordBoomerang,
        IC.TearAsunder,
        IC.Thrash,
        IC.Thunderclap,
        IC.TwinStrike,
        IC.Unrelenting,
        IC.Uppercut,
        IC.Whirlwind,
    ];

    private static readonly int[] _attackPool = _infernalBladeAttackPool;

    private static readonly int[] _generatedClassPool =
    [
        IC.StrikeIronclad,
        IC.DefendIronclad,
        IC.Bash,
        24, // Backflip
        42, // BladeDance
        84, // ChargeBattery
        93, // ColdSnap
        104, // Coolheaded
        117, // DaggerThrow
        123, // DeadlyPoison
        136, // Deflect
        172, // EscapePlan
        208, // Ftl
        224, // GoForTheEyes
        282, // Leap
        323, // Neutralize
        356, // PoisonedStab
        361, // Predator
        392, // Refract
        410, // Scrape
        439, // Slice
        475, // StrikeSilent
        545, // Zap
    ];

    private static readonly int[] _colorlessPool =
    [
        CL.Alchemize,
        CL.Anointed,
        CL.Bolas,
        CL.DarkShackles,
        CL.Discovery,
        CL.DramaticEntrance,
        CL.Finesse,
        CL.FlashOfSteel,
        CL.GangUp,
        CL.GoldAxe,
        CL.HandOfGreed,
        CL.Impatience,
        CL.JackOfAllTrades,
        CL.MasterOfStrategy,
        CL.MindBlast,
        CL.Panache,
        CL.PanicButton,
        CL.Purity,
        CL.Scrawl,
        CL.SecretTechnique,
        CL.SecretWeapon,
        CL.Shockwave,
        CL.ThinkingAhead,
    ];

    private static int CountStrikeCards(CombatState state)
    {
        int count = 0;
        foreach (
            var pile in new[] { state.Hand, state.DrawPile, state.DiscardPile, state.ExhaustPile }
        )
        {
            foreach (var c in pile)
            {
                if (GeneratedData.Cards.Get(c.DefId).Name.Contains("Strike"))
                {
                    count++;
                }
            }
        }

        return count;
    }
}
