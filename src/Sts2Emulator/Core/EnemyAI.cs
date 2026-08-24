namespace Sts2Emulator.Core;

using Effects;

public static class EnemyAI
{
    public static void ChooseIntents(
        List<EnemyState> enemies,
        int turn,
        Random rng,
        Random? aiRng = null,
        int ascension = Ascension.DefaultLevel
    )
    {
        var effectiveAiRng = aiRng ?? rng;
        foreach (var enemy in enemies.Where(e => e.Hp > 0))
        {
            // A killed illusion announces the revive it is about to spend the turn on, and
            // does not roll a move for a turn it will not act in.
            if (BuffSystem.Get(enemy.Buffs, BuffId.Reviving) > 0)
            {
                continue;
            }

            enemy.CurrentIntent = SelectIntent(enemy, effectiveAiRng, ascension, enemies);
            enemy.SecondaryIntent = SecondaryIntentFor(enemy);
        }
    }

    public static void UpdateSecondaryIntents(IEnumerable<EnemyState> enemies)
    {
        foreach (var enemy in enemies)
        {
            enemy.SecondaryIntent = SecondaryIntentFor(enemy);
        }
    }

    public static void ExecuteIntent(EnemyState enemy, CombatState state, Random rng)
    {
        // Enemy data is ascension-dependent; read the run's level once here so every
        // damage and buff below picks the same branch the live game would.
        int ascension = state.AscensionLevel;
        bool wasBuffMove = enemy.CurrentIntent.Type == IntentType.Buff;
        // VigorPower is spent by the attack that benefits from it. Captured here because a
        // move can GRANT Vigor as it swings — Terror Eel's THRASH does — and the grant must
        // survive to the next turn while the amount it swung with does not.
        int vigorSpentThisTurn =
            enemy.CurrentIntent.Type == IntentType.Attack
                ? BuffSystem.Get(enemy.Buffs, BuffId.Vigor)
                : 0;

        enemy.Block = 0; // block clears at start of enemy turn
        if (BuffSystem.Get(enemy.Buffs, BuffId.Stunned) > 0)
        {
            BuffSystem.Apply(enemy.Buffs, BuffId.Stunned, -1);
            if (enemy.DefId != KE.CorpseSlug)
            {
                enemy.MoveIndex++;
            }

            ApplyHighVoltageEndOfEnemyTurn(enemy);
            RestoreTemporaryEnemyStrength(enemy);
            return;
        }

        switch (enemy.CurrentIntent.Type)
        {
            case IntentType.Attack:
            {
                if (enemy.DefId == KE.Toadpole && enemy.MoveIndex % 3 == 1)
                {
                    // SpikeSpitMove spends the Spiken thorns before it swings; the hits
                    // themselves come from the intent's declared Hits below.
                    BuffSystem.Apply(enemy.Buffs, BuffId.Thorns, -2);
                }

                if (enemy.DefId == KE.SnappingJaxfruit)
                {
                    // ENERGY_ORB is an attack plus StrengthPower(2), and it loops on
                    // itself — so the jaxfruit's announcement climbs 3, 5, 7. The buff sat
                    // in the debuff branch, which its attack intent no longer reaches, so
                    // it never grew at all.
                    DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                    BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 2);
                    break;
                }

                if (enemy.DefId == KE.LivingFog && enemy.MoveIndex % 2 == 1)
                {
                    // BLOAT: an attack plus a SummonIntent. GetNextSlot decides whether a
                    // bomb fits — LivingFogNormal declares five bomb slots.
                    if (state.Enemies.Count(e => e.Hp > 0 && e.DefId == KE.GasBomb) < 5)
                    {
                        // Not stunned: LivingFog.BloatMove adds the bomb with a plain
                        // CreatureCmd.Add, where a monster that means to sit out the
                        // turn it arrives sets StartStunned (Wriggler does). The bomb
                        // goes off in the enemy phase that summoned it.
                        var bloatBomb = CreateEnemy(
                            KE.GasBomb,
                            rng,
                            new Intent(
                                IntentType.Attack,
                                Ascension.Value(ascension, Ascension.DeadlyEnemies, 9, 8)
                            ),
                            state: state
                        );
                        BuffSystem.Apply(bloatBomb.Buffs, BuffId.Minion, 1);
                        // The bomb takes a slot BEFORE the fog, which is where a live
                        // capture shows it: [Gas Bomb, Living Fog]. Appending it put the
                        // fog first, so the same target index named a different creature
                        // on each side -- a replayed run aimed its strikes at the fog
                        // where the game killed the bomb, then ate the eight the bomb was
                        // never alive to deal. Fogmog's eye is inserted the same way.
                        state.Enemies.Insert(
                            state.Enemies.IndexOf(enemy),
                            Effects.RelicEffects.Spawned(state, bloatBomb)
                        );
                    }

                    DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                    break;
                }

                if (enemy.DefId == KE.TerrorEel && enemy.MoveIndex % 2 == 1)
                {
                    // THRASH_MOVE: three hits, then VigorPower(6) — not Strength, which is
                    // what the emulator granted. Vigor is spent by its next attack, so the
                    // Crash after a Thrash announces six higher and the one after that
                    // does not.
                    DealAttack(
                        enemy,
                        state,
                        enemy.CurrentIntent.Magnitude,
                        enemy.CurrentIntent.Hits
                    );
                    BuffSystem.Apply(enemy.Buffs, BuffId.Vigor, 6);
                    break;
                }

                if (enemy.DefId == KE.SkulkingColony && enemy.MoveIndex % 4 == 2)
                {
                    // INERTIA_MOVE: the attack the intent announces, plus the Strength its
                    // BuffIntent stands for.
                    DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                    BuffSystem.Apply(
                        enemy.Buffs,
                        BuffId.Strength,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 4, 2)
                    );
                    break;
                }

                if (enemy.DefId == KE.CubexConstruct && (enemy.MoveIndex - 1) % 3 is 0 or 1)
                {
                    // REPEATER_BLAST: the attack the intent announces, plus the
                    // StrengthPower(2) its BuffIntent stands for.
                    DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                    BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 2);
                    break;
                }

                if (enemy.DefId == KE.Fogmog && enemy.LastMove is 1 or 2)
                {
                    // SWIPE: attack plus StrengthPower(1) to itself.
                    DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                    BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 1);
                    break;
                }

                if (enemy.DefId == KE.GremlinMerc)
                {
                    // Every Merc move steals through ThieveryPower; DOUBLE_SMASH adds
                    // WeakPower(2) and HEHE adds StrengthPower(2) to itself. The hits
                    // themselves come from the intent's Hits.
                    DealAttack(
                        enemy,
                        state,
                        enemy.CurrentIntent.Magnitude,
                        enemy.CurrentIntent.Hits
                    );
                    StealGremlinMercGold(enemy, state);
                    if (enemy.MoveIndex % 3 == 1)
                    {
                        BuffSystem.Apply(state.PlayerBuffs, BuffId.Weak, 2);
                    }
                    else if (enemy.MoveIndex % 3 == 2)
                    {
                        BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 2);
                    }

                    break;
                }

                if (enemy.DefId == KE.LivingFog && enemy.MoveIndex == 0)
                {
                    // ADVANCED_GAS: the attack, plus the SmoggyPower(1) its
                    // CardDebuffIntent stands for -- which the debuff branch never
                    // applied at all, it only dealt the damage.
                    DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                    BuffSystem.Apply(state.PlayerBuffs, BuffId.Smoggy, 1);
                    break;
                }

                if (enemy.DefId == KE.SludgeSpinner && enemy.LastMove == 0)
                {
                    // OIL_SPRAY: attack plus the WeakPower(1) its DebuffIntent stands
                    // for. It used to be announced as a debuff and resolve in the debuff
                    // branch, which its attack intent no longer reaches.
                    DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                    BuffSystem.Apply(state.PlayerBuffs, BuffId.Weak, 1);
                    break;
                }

                if (enemy.DefId == KE.SludgeSpinner && enemy.LastMove == 2)
                {
                    // RAGE: attack plus the BuffIntent beside it. The live readout calls
                    // this an Attack, so it executes here rather than in the buff branch —
                    // and the Strength has to come with it, or every later Slam and Oil
                    // Spray announces low.
                    DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                    BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 3);
                    break;
                }

                if (enemy.DefId == KE.VineShambler && enemy.MoveIndex % 3 == 1)
                {
                    // GRASPING_VINES: the attack the intent announces, plus the card
                    // debuff its second intent stands for.
                    DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                    BuffSystem.Apply(state.PlayerBuffs, BuffId.Tangled, 1);
                    break;
                }

                if (
                    enemy.DefId == KE.TestSubject
                    && BuffSystem.Get(enemy.Buffs, BuffId.PainfulStabs) > 0
                )
                {
                    DealAttack(enemy, state, 11, 3 + Math.Max(0, enemy.LastMove));
                    enemy.LastMove++;
                    break;
                }

                if (
                    enemy.DefId == KE.TestSubject
                    && BuffSystem.Get(enemy.Buffs, BuffId.Adaptable) == 0
                    && (enemy.MoveIndex - 4) % 3 == 0
                )
                {
                    DealAttack(enemy, state, 11, 3);
                    break;
                }

                // A Fossil Stalker special case used to sit here, firing on whichever
                // turn MoveIndex happened to be 2 and dealing a two-hit Lash at its A9
                // damage regardless of the move the machine had chosen — which also
                // doubled the Strength its SuckPower grants, since Suck triggers per hit.
                // The intent's own Hits carries this now.

                int baseDamage = enemy.CurrentIntent.Magnitude;
                if (enemy.DefId == KE.FlailKnight)
                {
                    baseDamage = Math.Max(
                        0,
                        baseDamage - BuffSystem.Get(enemy.Buffs, BuffId.Strength)
                    );
                }

                // Hits, not one: the riders below belong to the attack as a whole, and a
                // multi-hit intent used to break out above them -- which is how Punch
                // Construct's FAST_PUNCH lost the Frail its DebuffIntent declares.
                DealAttack(enemy, state, baseDamage, Math.Max(1, enemy.CurrentIntent.Hits));

                if (enemy.DefId == KE.GasBomb)
                {
                    enemy.Hp = 0;
                }

                if (enemy.DefId == KE.ThievingHopper && enemy.MoveIndex == 0)
                {
                    StealDrawOrDiscardCard(state);
                }

                if (enemy.DefId == KE.LouseProgenitor && enemy.MoveIndex == 0)
                {
                    BuffSystem.Apply(state.PlayerBuffs, BuffId.Frail, 2);
                }

                if (enemy.DefId == KE.Fabricator)
                {
                    SummonFabricatorBots(enemy, state, rng, includeDefensive: false);
                }

                if (enemy.DefId == KE.ScrollOfBiting)
                {
                    ApplyPaperCuts(enemy, state);
                }

                if (enemy.DefId == KE.TurretOperator)
                {
                    enemy.Block += 25;
                }

                if (enemy.DefId == KE.PunchConstruct && enemy.MoveIndex % 3 == 1)
                {
                    BuffSystem.Apply(state.PlayerBuffs, BuffId.Frail, 1);
                }

                // TACKLE_MOVE is an attack plus a DebuffIntent -- FrailPower(1) on the
                // target -- and the readout follows the attack, so it resolves here. It
                // sat in the debuff branch, which the stalker's Attack intent never
                // reaches, so the Frail was never applied at all and every Defend after
                // it blocked five where the game blocked three. LastMove, not MoveIndex:
                // this monster rolls its move rather than cycling.
                if (enemy.DefId == KE.FossilStalker && enemy.LastMove == 1)
                {
                    BuffSystem.Apply(state.PlayerBuffs, BuffId.Frail, 1);
                }

                // FRAIL_SPORES_MOVE is SingleAttackIntent(SporeDamage) then DebuffIntent,
                // so the readout calls it an Attack and it resolves here. Its
                // PowerCmd.Apply<FrailPower>(2) sat in the debuff branch, which the move's
                // Attack intent never reaches -- the damage landed and the Frail did not,
                // so every Defend after it blocked five where the game blocked three.
                if (enemy.DefId == KE.Flyconid && enemy.LastMove == 1)
                {
                    BuffSystem.Apply(state.PlayerBuffs, BuffId.Frail, 2);
                }

                if (enemy.DefId == KE.DecimillipedeSegment && enemy.MoveIndex % 3 == 1)
                {
                    BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 2);
                }

                if (enemy.DefId == KE.DecimillipedeSegment && enemy.MoveIndex % 3 == 2)
                {
                    BuffSystem.Apply(state.PlayerBuffs, BuffId.Weak, 1);
                }

                if (enemy.DefId == KE.MagiKnight && enemy.MoveIndex % 5 == 0)
                {
                    enemy.Block += BuffSystem.IncomingBlock(9, enemy.Buffs);
                }

                if (enemy.DefId == KE.CeremonialBeast && enemy.MoveIndex > 0)
                {
                    BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 2);
                }

                if (enemy.DefId == KE.Crusher && enemy.MoveIndex % 5 == 2)
                {
                    BuffSystem.Apply(state.PlayerBuffs, BuffId.Weak, 2);
                    BuffSystem.Apply(state.PlayerBuffs, BuffId.Frail, 2);
                }
                if (enemy.DefId == KE.Crusher && enemy.MoveIndex % 5 == 4)
                {
                    enemy.Block += BuffSystem.IncomingBlock(18, enemy.Buffs);
                }

                if (enemy.DefId == KE.KinPriest && enemy.MoveIndex % 4 == 0)
                {
                    BuffSystem.Apply(state.PlayerBuffs, BuffId.Frail, 1);
                }

                if (enemy.DefId == KE.KinPriest && enemy.MoveIndex % 4 == 1)
                {
                    BuffSystem.Apply(state.PlayerBuffs, BuffId.Weak, 1);
                }

                if (enemy.DefId == KE.LagavulinMatriarch && enemy.MoveIndex % 4 == 3)
                {
                    enemy.Block += BuffSystem.IncomingBlock(14, enemy.Buffs);
                }

                if (enemy.DefId == KE.SoulFysh && enemy.MoveIndex % 5 == 2)
                {
                    AddStatus(state, ST.Beckon, 1);
                }

                if (enemy.DefId == KE.SoulFysh && enemy.MoveIndex % 5 == 4)
                {
                    BuffSystem.Apply(state.PlayerBuffs, BuffId.Vulnerable, 3);
                }

                if (enemy.DefId == KE.Vantom && enemy.MoveIndex % 4 == 2)
                {
                    AddStatus(state, ST.Wound, 3);
                }

                if (enemy.DefId == KE.WaterfallGiant && enemy.MoveIndex > 0)
                {
                    BuffSystem.Apply(enemy.Buffs, BuffId.SteamEruption, 3);
                }

                if (enemy.DefId == KE.WaterfallGiant && (enemy.MoveIndex - 1) % 5 == 0)
                {
                    BuffSystem.Apply(state.PlayerBuffs, BuffId.Weak, 1);
                }

                if (
                    enemy.DefId == KE.TestSubject
                    && BuffSystem.Get(enemy.Buffs, BuffId.Adaptable) > 0
                    && BuffSystem.Get(enemy.Buffs, BuffId.PainfulStabs) == 0
                    && enemy.MoveIndex % 2 == 1
                )
                {
                    BuffSystem.Apply(state.PlayerBuffs, BuffId.Vulnerable, 1);
                }

                break;
            }

            case IntentType.Defend:
                if (enemy.DefId == KE.Guardbot)
                {
                    foreach (
                        var ally in state.Enemies.Where(e => e.Hp > 0 && e.DefId == KE.Fabricator)
                    )
                    {
                        ally.Block += BuffSystem.IncomingBlock(15, ally.Buffs);
                    }

                    break;
                }

                enemy.Block += BuffSystem.IncomingBlock(enemy.CurrentIntent.Magnitude, enemy.Buffs);
                if (enemy.DefId == KE.Axebot && enemy.MoveIndex % 3 == 0)
                {
                    int stock = BuffSystem.Get(enemy.Buffs, BuffId.Stock);
                    BuffSystem.Apply(enemy.Buffs, BuffId.Strength, Math.Max(0, 2 - stock) * 4);
                }

                break;

            case IntentType.Buff:
                ApplyBuffIntent(enemy, state, rng);
                break;

            case IntentType.Debuff:
                ApplyDebuffIntent(enemy, state, rng);
                break;
        }

        if (
            enemy.DefId == KE.Nibbit
            && enemy.CurrentIntent.Type == IntentType.Attack
            && enemy.MoveIndex % 3 == 1
        )
        {
            enemy.Block += BuffSystem.IncomingBlock(6, enemy.Buffs);
        }

        if (enemy.DefId == KE.BowlbugEgg && enemy.CurrentIntent.Type == IntentType.Attack)
        {
            enemy.Block += BuffSystem.IncomingBlock(8, enemy.Buffs);
        }

        if (enemy.DefId == KE.TwoTailedRat && enemy.CurrentIntent.Type != IntentType.Buff)
        {
            TickRatSummonCooldown(enemy);
        }

        // PlatingPower decrements on AfterSideTurnStart and grants its block on
        // BeforeSideTurnEndEarly -- in that order -- so the block a turn ends with is the
        // ALREADY decremented amount. Granting first and decrementing after left every
        // plated enemy a point of block ahead of the game for the whole fight.
        if (BuffSystem.Get(enemy.Buffs, BuffId.Plating) > 0)
        {
            // ...except on round one, which AfterSideTurnStart skips for enemies. So a
            // plated enemy ends its first turn on the full amount and only starts giving
            // ground on its second: a live Sewer Clam holds 9 block at Plating 9, then 8
            // at Plating 8. CombatState.Turn counts from ZERO, so the first enemy phase of
            // a fight is Turn 0 -- reading it as 1 puts the whole decay a turn late.
            if (enemy.DefId != KE.LagavulinMatriarch && state.Turn > 0)
            {
                BuffSystem.Apply(enemy.Buffs, BuffId.Plating, -1);
            }

            enemy.Block += BuffSystem.IncomingBlock(
                BuffSystem.Get(enemy.Buffs, BuffId.Plating),
                enemy.Buffs
            );
        }

        if (enemy.DefId == KE.LagavulinMatriarch && BuffSystem.Get(enemy.Buffs, BuffId.Asleep) > 0)
        {
            BuffSystem.Apply(enemy.Buffs, BuffId.Asleep, -1);
            // SLEEP_MOVE loops on itself for as long as AsleepPower lasts, and only then
            // does SLEEP_BRANCH send her to SLASH. Sleeping turns are all one move, so
            // they must not walk the four-move ring — parking the index here leaves the
            // increment below to start that ring at SLASH on the turn she wakes.
            enemy.MoveIndex = 0;
        }

        enemy.MoveIndex++;

        // Ritual: gain Strength at end of each enemy turn except the turn it was applied.
        if (!wasBuffMove)
        {
            int ritual = BuffSystem.Get(enemy.Buffs, BuffId.Ritual);
            if (ritual > 0)
            {
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, ritual);
            }
        }

        if (vigorSpentThisTurn > 0)
        {
            BuffSystem.Apply(enemy.Buffs, BuffId.Vigor, -vigorSpentThisTurn);
        }

        ApplyHighVoltageEndOfEnemyTurn(enemy);
        RestoreTemporaryEnemyStrength(enemy);
    }

    private static void ApplyHighVoltageEndOfEnemyTurn(EnemyState enemy)
    {
        int highVoltage = BuffSystem.Get(enemy.Buffs, BuffId.HighVoltage);
        if (highVoltage > 0)
        {
            BuffSystem.Apply(enemy.Buffs, BuffId.Strength, highVoltage);
        }

        int territorial = BuffSystem.Get(enemy.Buffs, BuffId.Territorial);
        if (territorial > 0)
        {
            BuffSystem.Apply(enemy.Buffs, BuffId.Strength, territorial);
        }
    }

    private static void RestoreTemporaryEnemyStrength(EnemyState enemy)
    {
        int temporaryStrength = BuffSystem.Get(enemy.Buffs, BuffId.TemporaryStrength);
        if (temporaryStrength == 0)
        {
            return;
        }

        BuffSystem.Apply(enemy.Buffs, BuffId.Strength, temporaryStrength);
        BuffSystem.Remove(enemy.Buffs, BuffId.TemporaryStrength);
    }

    // ── Per-enemy intent selection ─────────────────────────────────────────────

    private static Intent SelectIntent(
        EnemyState enemy,
        Random rng,
        int ascension = Ascension.DefaultLevel,
        // The roster, for the handful of monsters whose eligibility depends on what
        // their neighbours have already announced this pass. Rolls happen in roster
        // order, so a monster reads the moves picked before its own.
        List<EnemyState>? roster = null
    )
    {
        switch (enemy.DefId)
        {
            case KE.CalcifiedCultist:
                // Turn 0: Incantation (Buff). Turn 1+: DarkStrikeDamage, which loops.
                return enemy.MoveIndex == 0
                    ? new Intent(IntentType.Buff, 0)
                    : new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 11, 9)
                    );

            case KE.DampCultist:
                // Turn 0: Incantation (Buff). Turn 1+: DarkStrikeDamage, which loops.
                // This was a flat 3 — the Deadly branch — so it hit triple at A8.
                return enemy.MoveIndex == 0
                    ? new Intent(IntentType.Buff, 0)
                    : new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 3, 1)
                    );

            case KE.Chomper:
                // Alternates Clamp (9x2) and Screech (add Dazed).
                return enemy.MoveIndex % 2 == 0
                    ? new Intent(IntentType.Attack, 18)
                    : new Intent(IntentType.Debuff, 3);

            case KE.Exoskeleton:
                return rng.Next(3) switch
                {
                    0 => new Intent(IntentType.Attack, 4),
                    1 => new Intent(IntentType.Attack, 9),
                    _ => new Intent(IntentType.Buff, 0),
                };

            case KE.FuzzyWurmCrawler:
                return (enemy.MoveIndex % 3) == 1
                    ? new Intent(IntentType.Buff, 0)
                    // AcidGoopDamage
                    : new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 6, 4)
                    );

            case KE.Mawler:
            {
                // A RandomBranchState, not a cycle: RIP_AND_TEAR and CLAW are CannotRepeat
                // and ROAR is UseOnlyOnce, all weight 1, entered at CLAW. Modelling it as
                // MoveIndex % 3 put the emulator a whole move out of phase from turn two.
                const int ripAndTear = 0;
                const int roar = 1;
                const int claw = 2;
                int move;
                if (enemy.MoveIndex == 0)
                {
                    move = claw;
                }
                else
                {
                    var eligible = new List<int>();
                    foreach (int candidate in (int[])[ripAndTear, roar, claw])
                    {
                        bool repeats = candidate == enemy.LastMove;
                        bool roarSpent = candidate == roar && enemy.OnceOnlyMoveUsed;
                        if (!repeats && !roarSpent)
                        {
                            eligible.Add(candidate);
                        }
                    }

                    move = eligible[rng.Next(eligible.Count)];
                }

                enemy.OnceOnlyMoveUsed |= move == roar;
                enemy.LastMove = move;
                return move switch
                {
                    // ClawDamage x 2
                    claw => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 5, 4),
                        Hits: 2
                    ),
                    // RoarMove applies VulnerablePower(3).
                    roar => new Intent(IntentType.Debuff, 3),
                    // RipAndTearDamage
                    _ => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 16, 14)
                    ),
                };
            }

            case KE.GremlinMerc:
                // GIMME_MOVE, DOUBLE_SMASH_MOVE and HEHE_MOVE all lead with an attack
                // intent, and the live readout announces all three as Attacks. Every value
                // keys off ToughEnemies, which is LIVE at A8 — converting them to
                // DeadlyEnemies made this Merc hit for 14 where the game hits for 16.
                return (enemy.MoveIndex % 3) switch
                {
                    // GimmeDamage x GimmeRepeat
                    0 => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.ToughEnemies, 8, 7),
                        Hits: 2
                    ),
                    // DoubleSmashDamage x DoubleSmashRepeat, plus WeakPower(2)
                    1 => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.ToughEnemies, 7, 6),
                        Hits: 2
                    ),
                    // HeheDamage, plus StrengthPower(2) to itself
                    _ => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.ToughEnemies, 9, 8)
                    ),
                };

            case KE.SneakyGremlin:
                // TackleDamage
                return enemy.MoveIndex == 0
                    ? new Intent(IntentType.Unknown, 0)
                    : new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 10, 9)
                    );

            case KE.FatGremlin:
                return enemy.MoveIndex == 0
                    ? new Intent(IntentType.Unknown, 0)
                    : new Intent(IntentType.Buff, 0);

            case KE.Inklet:
            {
                // JAB leads into a branch of {PIERCING_GAZE, WHIRLWIND}; both of those lead
                // back to JAB. So an Inklet alternates JAB with a rolled move — and the
                // MIDDLE one opens on WHIRLWIND rather than JAB, which is what
                // MonsterMoveStateMachine's initialState says and what put this roster's
                // second Inklet on the wrong move from turn one.
                const int jab = 0;
                const int whirlwind = 1;
                const int piercingGaze = 2;
                int move;
                if (enemy.LastMove < 0)
                {
                    move = enemy.MoveIndex == 1 ? whirlwind : jab;
                }
                else if (enemy.LastMove != jab)
                {
                    move = jab;
                }
                else
                {
                    // Both branches are CannotRepeat, which RandomBranchState scores
                    // against the LAST LOGGED MOVE — and this branch is only ever reached
                    // from JAB, so neither is ever excluded and the roll is always over
                    // two. Excluding the move before the jab made it a roll over one on
                    // half the turns, which is a different draw from the same stream.
                    move = PickBranch([piercingGaze, whirlwind], rng);
                }

                enemy.LastMove = move;
                enemy.MoveHistory.Add(move);
                return move switch
                {
                    // JabDamage
                    jab => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 4, 3)
                    ),
                    // WhirlwindDamage x 3
                    whirlwind => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 3, 2),
                        Hits: 3
                    ),
                    // PiercingGazeDamage
                    _ => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 11, 10)
                    ),
                };
            }

            case KE.Flyconid:
            {
                // INITIAL rolls {FRAIL_SPORES, SMASH}; every move then rolls RAND, which is
                // {VULNERABLE_SPORES, FRAIL_SPORES, SMASH} with cooldowns of 3, 2 and none.
                // The emulator rolled a flat 6-way weighting that could open on
                // VULNERABLE_SPORES, which the opening branch does not offer at all.
                const int vulnerableSpores = 0;
                const int frailSpores = 1;
                const int smash = 2;
                var eligible = new List<int>();
                if (enemy.LastMove < 0)
                {
                    eligible.Add(frailSpores);
                    eligible.Add(smash);
                }
                else
                {
                    foreach (
                        var (candidate, cooldown) in ((int, int)[])
                            [(vulnerableSpores, 3), (frailSpores, 2), (smash, 0)]
                    )
                    {
                        bool onCooldown = enemy
                            .MoveHistory.AsEnumerable()
                            .Reverse()
                            .Take(cooldown)
                            .Contains(candidate);
                        if (candidate != enemy.LastMove && !onCooldown)
                        {
                            eligible.Add(candidate);
                        }
                    }
                }

                int move = eligible.Count > 0 ? PickBranch(eligible, rng) : smash;
                enemy.LastMove = move;
                enemy.MoveHistory.Add(move);
                return move switch
                {
                    vulnerableSpores => new Intent(IntentType.Debuff, 2),
                    // SporeDamage; FRAIL_SPORES is attack + debuff, announced as an attack.
                    frailSpores => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 9, 8)
                    ),
                    // SmashDamage
                    _ => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 12, 11)
                    ),
                };
            }

            case KE.SnappingJaxfruit:
                // EnergyDamage; ENERGY_ORB loops on itself forever
                return new Intent(
                    IntentType.Attack,
                    Ascension.Value(ascension, Ascension.DeadlyEnemies, 4, 3)
                );

            case KE.BowlbugRock:
                return enemy.MoveIndex % 2 == 0
                    ? new Intent(IntentType.Attack, 16)
                    : new Intent(IntentType.Unknown, 0);

            case KE.BowlbugEgg:
                return new Intent(IntentType.Attack, 8);

            case KE.BowlbugNectar:
                return (enemy.MoveIndex % 3) switch
                {
                    1 => new Intent(IntentType.Buff, 16),
                    _ => new Intent(IntentType.Attack, 3),
                };

            case KE.BowlbugSilk:
                return enemy.MoveIndex % 2 == 0
                    ? new Intent(IntentType.Debuff, 1)
                    : new Intent(IntentType.Attack, 10);

            case KE.Tunneler:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Attack, 15),
                    1 => new Intent(IntentType.Defend, 37),
                    _ => new Intent(IntentType.Attack, 26),
                };

            case KE.ThievingHopper:
                return (enemy.MoveIndex % 5) switch
                {
                    0 => new Intent(IntentType.Attack, 19),
                    1 => new Intent(IntentType.Buff, 5),
                    2 => new Intent(IntentType.Attack, 23),
                    3 => new Intent(IntentType.Attack, 16),
                    _ => new Intent(IntentType.Unknown, 0),
                };

            case KE.Myte:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => enemy.CurrentIntent.Type == IntentType.Attack
                        ? new Intent(IntentType.Attack, 6)
                        : new Intent(IntentType.Debuff, 2),
                    1 => new Intent(IntentType.Attack, 15),
                    _ => new Intent(IntentType.Attack, 6),
                };

            case KE.SlumberingBeetle:
                return enemy.MoveIndex < 3
                    ? new Intent(IntentType.Unknown, 0)
                    : new Intent(IntentType.Attack, 18);

            case KE.SpinyToad:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Buff, 5),
                    1 => new Intent(IntentType.Attack, 25),
                    _ => new Intent(IntentType.Attack, 19),
                };

            case KE.Ovicopter:
                return (enemy.MoveIndex % 4) switch
                {
                    0 => new Intent(IntentType.Buff, 0),
                    1 => new Intent(IntentType.Attack, 17),
                    2 => new Intent(IntentType.Debuff, 8),
                    _ => new Intent(IntentType.Buff, 4),
                };

            case KE.LouseProgenitor:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Attack, 10),
                    1 => new Intent(IntentType.Defend, 18),
                    _ => new Intent(IntentType.Attack, 16),
                };

            case KE.HunterKiller:
                return enemy.MoveIndex == 0
                    ? new Intent(IntentType.Debuff, 1)
                    : new Intent(IntentType.Attack, rng.Next(3) == 0 ? 19 : 24);

            case KE.Axebot:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Defend, 15),
                    1 => new Intent(IntentType.Attack, 20),
                    _ => new Intent(IntentType.Attack, 14),
                };

            case KE.DevotedSculptor:
                return enemy.MoveIndex == 0
                    ? new Intent(IntentType.Buff, 9)
                    : new Intent(IntentType.Attack, 15);

            case KE.Fabricator:
                return rng.Next(2) == 0
                    ? new Intent(IntentType.Buff, 0)
                    : new Intent(IntentType.Attack, 21);

            case KE.FrogKnight:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Buff, 5),
                    1 => new Intent(IntentType.Attack, 23),
                    _ => new Intent(IntentType.Attack, 14),
                };

            case KE.GlobeHead:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Attack, 21),
                    1 => new Intent(IntentType.Attack, 21),
                    _ => new Intent(IntentType.Attack, 17),
                };

            case KE.LivingShield:
                return new Intent(IntentType.Attack, enemy.MoveIndex == 0 ? 6 : 18);

            case KE.TurretOperator:
                return (enemy.MoveIndex % 3) == 2
                    ? new Intent(IntentType.Buff, 1)
                    : new Intent(IntentType.Attack, 20);

            case KE.OwlMagistrate:
                return (enemy.MoveIndex % 4) switch
                {
                    0 => new Intent(IntentType.Attack, 17),
                    1 => new Intent(IntentType.Attack, 24),
                    2 => new Intent(IntentType.Buff, 1),
                    _ => new Intent(IntentType.Attack, 36),
                };

            case KE.ScrollOfBiting:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Attack, 16),
                    1 => new Intent(IntentType.Attack, 12),
                    _ => new Intent(IntentType.Buff, 2),
                };

            case KE.SlimedBerserker:
                return (enemy.MoveIndex % 4) switch
                {
                    0 => new Intent(IntentType.Debuff, 10),
                    1 => new Intent(IntentType.Attack, 20),
                    2 => new Intent(IntentType.Buff, 3),
                    _ => new Intent(IntentType.Attack, 33),
                };

            case KE.TheLost:
                return (enemy.MoveIndex % 2) == 0
                    ? new Intent(IntentType.Debuff, 2)
                    : new Intent(IntentType.Attack, 10);

            case KE.TheForgotten:
                return (enemy.MoveIndex % 2) == 0
                    ? new Intent(IntentType.Debuff, 2)
                    : new Intent(IntentType.Attack, 15);

            case KE.TheObscura:
                return enemy.MoveIndex == 0
                    ? new Intent(IntentType.Buff, 0)
                    : rng.Next(3) switch
                    {
                        0 => new Intent(IntentType.Attack, 11),
                        1 => new Intent(IntentType.Buff, 3),
                        _ => new Intent(IntentType.Attack, 7),
                    };

            case KE.Parafright:
                return new Intent(IntentType.Attack, 17);

            case KE.Wriggler:
                return (enemy.MoveIndex % 2) == 0
                    ? new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 7, 6)
                    )
                    : new Intent(IntentType.Buff, 1);

            case KE.FakeMerchant:
                return enemy.MoveIndex switch
                {
                    0 => new Intent(IntentType.Attack, 15),
                    1 => new Intent(IntentType.Attack, 16),
                    2 => new Intent(IntentType.Attack, 10),
                    _ => new Intent(IntentType.Buff, 2),
                };

            case KE.FlailKnight:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Buff, 3),
                    1 => new Intent(IntentType.Attack, 20),
                    _ => new Intent(IntentType.Attack, 23),
                };

            case KE.BygoneEffigy:
                return enemy.MoveIndex switch
                {
                    0 => new Intent(IntentType.Unknown, 0),
                    1 => new Intent(IntentType.Buff, 10),
                    _ => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 15, 13)
                    ),
                };

            case KE.Entomancer:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Buff, 1),
                    1 => new Intent(IntentType.Attack, 24),
                    _ => new Intent(IntentType.Attack, 20),
                };

            case KE.InfestedPrism:
                return (enemy.MoveIndex % 4) switch
                {
                    0 => new Intent(IntentType.Attack, 17),
                    1 => new Intent(IntentType.Attack, 13),
                    2 => new Intent(IntentType.Attack, 18),
                    _ => new Intent(IntentType.Attack, 10),
                };

            case KE.PhrogParasite:
                // INFECT_MOVE's StatusIntent(3), then LashDamage x 4.
                return (enemy.MoveIndex % 2) == 0
                    ? new Intent(IntentType.Debuff, 3)
                    : new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 5, 4),
                        Hits: 4
                    );

            case KE.SoulNexus:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Attack, 31),
                    1 => new Intent(IntentType.Attack, 28),
                    _ => new Intent(IntentType.Debuff, 19),
                };

            case KE.TerrorEel:
                if (BuffSystem.Get(enemy.Buffs, BuffId.Stunned) > 0)
                {
                    return new Intent(IntentType.Unknown, 0);
                }

                // TERROR_MOVE, the turn after the stun: VulnerablePower(99).
                if (BuffSystem.Get(enemy.Buffs, BuffId.TerrorQueued) > 0)
                {
                    return new Intent(IntentType.Debuff, 99);
                }

                // CrashDamage, then THRASH_MOVE: ThrashDamage x ThrashRepeat plus a
                // BuffIntent, which the live readout announces as the attack.
                return (enemy.MoveIndex % 2) == 0
                    ? new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 18, 16)
                    )
                    : new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 4, 3),
                        Hits: 3
                    );

            case KE.Byrdonis:
                // SwoopDamage, alternating with PeckDamage x PeckRepeat.
                return (enemy.MoveIndex % 2) == 0
                    ? new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 19, 17)
                    )
                    : new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 4, 3),
                        Hits: 3
                    );

            case KE.DecimillipedeSegment:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Attack, 12),
                    1 => new Intent(IntentType.Attack, 7),
                    _ => new Intent(IntentType.Attack, 9),
                };

            case KE.SpectralKnight:
                return enemy.MoveIndex switch
                {
                    0 => new Intent(IntentType.Debuff, 2),
                    1 => new Intent(IntentType.Attack, 17),
                    _ => new Intent(IntentType.Attack, 12),
                };

            case KE.MagiKnight:
                return (enemy.MoveIndex % 5) switch
                {
                    0 => new Intent(IntentType.Attack, 7),
                    1 => new Intent(IntentType.Debuff, 1),
                    2 => new Intent(IntentType.Attack, 11),
                    3 => new Intent(IntentType.Defend, 9),
                    _ => new Intent(IntentType.Attack, 40),
                };

            case KE.MechaKnight:
                return (enemy.MoveIndex % 4) switch
                {
                    0 => new Intent(IntentType.Attack, 30),
                    1 => new Intent(IntentType.Debuff, 4),
                    2 => new Intent(IntentType.Buff, 15),
                    _ => new Intent(IntentType.Attack, 40),
                };

            case KE.PhantasmalGardener:
                // BITE -> LASH -> FLAIL -> ENLARGE, a fixed ring. Bite, Lash and the flail
                // repeat do NOT scale with ascension; only EnlargeStr does. An automated
                // conversion pass matched Lash's 7 to SkittishAmount and Flail's 3 to
                // EnlargeStr, which made the flail announce 2 where the game announces 3.
                return (enemy.MoveIndex % 4) switch
                {
                    // BiteDamage
                    0 => new Intent(IntentType.Attack, 5),
                    // LashDamage
                    1 => new Intent(IntentType.Attack, 7),
                    // FlailDamage(1) x FlailRepeat
                    2 => new Intent(IntentType.Attack, 1, Hits: 3),
                    // ENLARGE_MOVE's BuffIntent, worth EnlargeStr
                    _ => new Intent(
                        IntentType.Buff,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 3, 2)
                    ),
                };

            case KE.Aeonglass:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Attack, 32),
                    1 => new Intent(IntentType.Attack, 24),
                    _ => new Intent(IntentType.Buff, 2),
                };

            case KE.CeremonialBeast:
                return enemy.MoveIndex == 0
                    ? new Intent(
                        IntentType.Buff,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 160, 150)
                    )
                    : new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 20, 18)
                    );

            case KE.Crusher:
                return (enemy.MoveIndex % 5) switch
                {
                    0 => new Intent(IntentType.Attack, 21),
                    1 => new Intent(IntentType.Attack, 6),
                    2 => new Intent(IntentType.Attack, 20),
                    3 => new Intent(IntentType.Buff, 3),
                    _ => new Intent(IntentType.Attack, 21),
                };

            case KE.Rocket:
                return (enemy.MoveIndex % 5) switch
                {
                    0 => new Intent(IntentType.Attack, 4),
                    1 => new Intent(IntentType.Attack, 20),
                    2 => new Intent(IntentType.Buff, 3),
                    3 => new Intent(IntentType.Attack, 35),
                    _ => new Intent(IntentType.Unknown, 0),
                };

            case KE.KnowledgeDemon:
                return enemy.MoveIndex switch
                {
                    0 => new Intent(IntentType.Debuff, 0),
                    1 => new Intent(IntentType.Attack, 18),
                    2 => new Intent(IntentType.Attack, 27),
                    _ => new Intent(IntentType.Buff, 13),
                };

            case KE.LagavulinMatriarch:
                return BuffSystem.Get(enemy.Buffs, BuffId.Asleep) > 0
                    ? new Intent(IntentType.Unknown, 0)
                    : (enemy.MoveIndex % 4) switch
                    {
                        // SlashDamage
                        1 => new Intent(
                            IntentType.Attack,
                            Ascension.Value(ascension, Ascension.DeadlyEnemies, 21, 19)
                        ),
                        // DISEMBOWEL_MOVE: DisembowelDamage twice, which has to stay two
                        // hits rather than one of 20 — the matriarch gains Strength off
                        // her own SOUL_SIPHON, and Strength lands on every hit.
                        2 => new Intent(
                            IntentType.Attack,
                            Ascension.Value(ascension, Ascension.DeadlyEnemies, 10, 9),
                            Hits: 2
                        ),
                        // Slash2Damage. Its 14 is the DeadlyEnemies branch and A8 is
                        // below that; the 14 that IS live at A8 is Slash2Block, applied
                        // separately with the DefendIntent.
                        3 => new Intent(
                            IntentType.Attack,
                            Ascension.Value(ascension, Ascension.DeadlyEnemies, 14, 12)
                        ),
                        // SOUL_SIPHON_MOVE: a DebuffIntent and a BuffIntent.
                        _ => new Intent(IntentType.Debuff, 2),
                    };

            case KE.Queen:
                return enemy.MoveIndex switch
                {
                    0 => new Intent(IntentType.Debuff, 3),
                    1 => new Intent(IntentType.Debuff, 99),
                    _ => new Intent(IntentType.Buff, 20),
                };

            case KE.TorchHeadAmalgam:
                return (enemy.MoveIndex % 5) switch
                {
                    0 or 1 => new Intent(IntentType.Attack, 19),
                    2 => new Intent(IntentType.Attack, 24),
                    _ => new Intent(IntentType.Attack, 15),
                };

            case KE.SoulFysh:
                return (enemy.MoveIndex % 5) switch
                {
                    0 => new Intent(IntentType.Debuff, 2),
                    1 => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 17, 16)
                    ),
                    2 => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 8, 7)
                    ),
                    3 => new Intent(IntentType.Buff, 2),
                    _ => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 15, 13)
                    ),
                };

            case KE.TestSubject:
                if (BuffSystem.Get(enemy.Buffs, BuffId.PainfulStabs) > 0)
                {
                    return new Intent(IntentType.Attack, 11 * (3 + Math.Max(0, enemy.LastMove)));
                }

                if (BuffSystem.Get(enemy.Buffs, BuffId.Adaptable) == 0)
                {
                    return ((enemy.MoveIndex - 4) % 3) switch
                    {
                        0 => new Intent(IntentType.Attack, 33),
                        1 => new Intent(IntentType.Attack, 45),
                        _ => new Intent(IntentType.Buff, 5),
                    };
                }

                return (enemy.MoveIndex % 2) == 0
                    ? new Intent(IntentType.Attack, 22)
                    : new Intent(IntentType.Attack, 16);

            case KE.TheInsatiable:
                return enemy.MoveIndex switch
                {
                    0 => new Intent(IntentType.Buff, 0),
                    1 => new Intent(IntentType.Attack, 18),
                    2 => new Intent(IntentType.Attack, 31),
                    3 => new Intent(IntentType.Buff, 3),
                    _ => new Intent(IntentType.Attack, 18),
                };

            case KE.KinFollower:
                return (enemy.MoveIndex % 3) switch
                {
                    // QuickSlashDamage, which does not scale with ascension
                    0 => new Intent(IntentType.Attack, 5),
                    // BoomerangDamage x 2, pre-multiplied here into a single swing
                    1 => new Intent(IntentType.Attack, 2, Hits: 2),
                    _ => new Intent(
                        IntentType.Buff,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 3, 2)
                    ),
                };

            case KE.KinPriest:
                return (enemy.MoveIndex % 4) switch
                {
                    // OrbOfFrailtyDamage, then OrbOfWeaknessDamage — both attacks that
                    // carry a DebuffIntent, and both 8 at A8 rather than the 9 pinned here.
                    0 => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 9, 8)
                    ),
                    1 => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 9, 8)
                    ),
                    // BeamDamage x 3, which does not scale
                    2 => new Intent(IntentType.Attack, 3, Hits: 3),
                    _ => new Intent(
                        IntentType.Buff,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 3, 2)
                    ),
                };

            case KE.Vantom:
                return (enemy.MoveIndex % 4) switch
                {
                    0 => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 8, 7)
                    ),
                    // InkyLanceDamage x 2, which was pre-multiplied to its A9 total
                    1 => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 7, 6),
                        Hits: 2
                    ),
                    // DismemberDamage; DISMEMBER also carries a StatusIntent(3)
                    2 => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 30, 26)
                    ),
                    _ => new Intent(IntentType.Buff, 2),
                };

            case KE.WaterfallGiant:
                // PRESSURIZE once, then the five-move ring STOMP, RAM, SIPHON,
                // PRESSURE_GUN, PRESSURE_UP — PRESSURE_UP's FollowUpState is STOMP, so
                // the ring never returns to PRESSURIZE.
                if (enemy.MoveIndex == 0)
                {
                    return new Intent(
                        IntentType.Buff,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 20, 15)
                    );
                }

                return ((enemy.MoveIndex - 1) % 5) switch
                {
                    // StompDamage; STOMP_MOVE is an attack plus a DebuffIntent and a
                    // BuffIntent, and the live readout announces the attack — as it does
                    // for RAM, PRESSURE_GUN and PRESSURE_UP. Only PRESSURIZE and SIPHON
                    // are pure non-attacks.
                    0 => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 16, 15)
                    ),
                    // RamDamage
                    1 => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 11, 10)
                    ),
                    // SIPHON_MOVE: HealIntent plus BuffIntent, no attack.
                    2 => new Intent(
                        IntentType.Buff,
                        Ascension.Value(ascension, Ascension.ToughEnemies, 15, 10)
                    ),
                    // PRESSURE_GUN_MOVE announces a lambda, not a constant: every firing
                    // adds PressureGunIncrease to CurrentPressureGunDamage, so the ring
                    // is 5 damage worse each time round.
                    3 => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 23, 20)
                            + (PressureGunIncrease * ((enemy.MoveIndex - 4) / 5))
                    ),
                    // PressureUpDamage; PRESSURE_UP_MOVE is an attack plus a BuffIntent.
                    _ => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 14, 13)
                    ),
                };

            case KE.CubexConstruct:
                // CHARGE_UP happens once: EXPEL's FollowUpState is the first
                // REPEATER_BLAST, not the charge. Cycling all four re-charged every
                // fourth turn, where the live game blasts.
                if (enemy.MoveIndex == 0)
                {
                    return new Intent(IntentType.Buff, 0);
                }

                return ((enemy.MoveIndex - 1) % 3) switch
                {
                    // BlastDamage; REPEATER_BLAST is attack + buff, twice over, and the
                    // live readout announces the attack — growing 9, 11, 13, 15 as the
                    // construct stacks the Strength each blast hands it.
                    0 or 1 => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 8, 7)
                    ),
                    // ExpelDamage x 2
                    _ => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 6, 5),
                        Hits: 2
                    ),
                };

            case KE.VineShambler:
                return (enemy.MoveIndex % 3) switch
                {
                    // SwipeDamage x 2
                    0 => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 7, 6),
                        Hits: 2
                    ),
                    // GraspingVinesDamage. The MoveState lists SingleAttackIntent first
                    // and CardDebuffIntent second, and the live game announces the attack
                    // — a sweep caught this reported as a Debuff.
                    1 => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 9, 8)
                    ),
                    // ChompDamage
                    _ => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 18, 16)
                    ),
                };

            case KE.SlitheringStrangler:
                // CONSTRICT -> a branch of {THWACK, LASH}, and both of those lead straight
                // back to CONSTRICT. So it alternates the debuff with a ROLLED attack;
                // MoveIndex % 3 made it a fixed three-cycle instead. Both branches are
                // CanRepeatForever, so the roll is always over two.
                if (enemy.MoveIndex % 2 == 0)
                {
                    return new Intent(IntentType.Debuff, 3);
                }

                return PickBranch([0, 1], rng) == 0
                    // ThwackDamage; THWACK also carries a DefendIntent.
                    ? new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 8, 7)
                    )
                    // LashDamage
                    : new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 13, 12)
                    );

            case KE.HauntedShip:
                // HAUNT.FollowUpState is SWIPE, and SWIPE and STOMP then point at each
                // other — so the opening debuff happens once and never comes round again.
                // This used to cycle all three on MoveIndex % 3, re-haunting every third turn.
                if (enemy.MoveIndex == 0)
                {
                    return new Intent(IntentType.Debuff, 5);
                }

                return enemy.MoveIndex % 2 == 1
                    // SwipeDamage
                    ? new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 14, 13)
                    )
                    // StompDamage x StompRepeat
                    : new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 5, 4),
                        Hits: 3
                    );

            case KE.LivingFog:
                // ADVANCED_GAS happens ONCE: its FollowUpState is BLOAT, and BLOAT and
                // SUPER_GAS_BLAST then point at each other forever. Cycling all three on
                // MoveIndex % 3 re-gassed every third turn. Same shape as Haunted Ship.
                if (enemy.MoveIndex == 0)
                {
                    // AdvancedGasDamage. ADVANCED_GAS_MOVE declares SingleAttackIntent
                    // first and CardDebuffIntent second, so the readout announces an
                    // attack -- the same shape as the Sludge Spinner's oil spray.
                    return new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 9, 8)
                    );
                }

                return enemy.MoveIndex % 2 == 1
                    // BloatDamage; BLOAT is an attack plus a SummonIntent.
                    ? new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 6, 5)
                    )
                    // SuperGasBlastDamage
                    : new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 9, 8)
                    );

            case KE.Fogmog:
            {
                // ILLUSION -> SWIPE -> a RandomBranchState weighted 0.4 SWIPE_RANDOM /
                // 0.6 HEADBUTT; SWIPE_RANDOM -> HEADBUTT -> SWIPE -> branch again. The
                // emulator ran a flat three-cycle, which re-summoned every third turn.
                const int illusion = 0;
                const int swipe = 1;
                const int swipeRandom = 2;
                const int headbutt = 3;
                int move = enemy.LastMove switch
                {
                    -1 => illusion,
                    illusion => swipe,
                    swipeRandom => headbutt,
                    headbutt => swipe,
                    // After SWIPE the machine rolls: 0.4 for another swipe, 0.6 headbutt.
                    _ => rng.NextDouble() <= 0.4 ? swipeRandom : headbutt,
                };
                enemy.LastMove = move;
                return move switch
                {
                    illusion => new Intent(IntentType.Buff, 0),
                    // HeadbuttDamage
                    headbutt => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 16, 14)
                    ),
                    // SwipeDamage. SWIPE attacks and hands itself StrengthPower(1), and
                    // the live readout announces the attack.
                    _ => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 9, 8)
                    ),
                };
            }

            case KE.EyeWithTeeth:
                return new Intent(IntentType.Debuff, 3);

            case KE.GasBomb:
                return new Intent(
                    IntentType.Attack,
                    Ascension.Value(ascension, Ascension.DeadlyEnemies, 9, 8)
                );

            case KE.AxeRubyRaider:
                // BigSwingDamage / SwingDamage
                return (enemy.MoveIndex % 3) == 2
                    ? new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 13, 12)
                    )
                    : new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 6, 5)
                    );

            case KE.AssassinRubyRaider:
                // KillshotDamage; KILLSHOT_MOVE loops on itself forever.
                return new Intent(
                    IntentType.Attack,
                    Ascension.Value(ascension, Ascension.DeadlyEnemies, 11, 10)
                );

            case KE.BruteRubyRaider:
                // BeatDamage, alternating with ROAR_MOVE's buff.
                return enemy.MoveIndex % 2 == 0
                    ? new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 8, 7)
                    )
                    : new Intent(IntentType.Buff, 0);

            case KE.CrossbowRubyRaider:
                // FireDamage
                return enemy.MoveIndex % 2 == 0
                    ? new Intent(IntentType.Defend, 3)
                    : new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 16, 14)
                    );

            case KE.TrackerRubyRaider:
                // HoundsDamage(1) x HoundsRepeat, which is the ascension-dependent part —
                // the emulator announced a flat 9, its A9 repeat count, as one hit.
                return enemy.MoveIndex == 0
                    ? new Intent(IntentType.Debuff, 2)
                    : new Intent(
                        IntentType.Attack,
                        1,
                        Hits: Ascension.Value(ascension, Ascension.DeadlyEnemies, 9, 8)
                    );

            case KE.Seapunk:
                return (enemy.MoveIndex % 3) switch
                {
                    // SeaKickDamage
                    0 => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 13, 11)
                    ),
                    // SpinningKickDamage x SpinningKickRepeat (2 x 4, no ascension term).
                    // Pre-multiplied to 8, this announced 9 against the live game's 12: the
                    // Strength from Bubble Burp lands on each of the four hits.
                    1 => new Intent(IntentType.Attack, 2, Hits: 4),
                    _ => new Intent(IntentType.Buff, 0),
                };

            case KE.ShrinkerBeetle:
                return enemy.MoveIndex == 0
                    ? new Intent(IntentType.Debuff, 1)
                    : (
                        enemy.MoveIndex % 2 == 1
                            // ChompDamage
                            ? new Intent(
                                IntentType.Attack,
                                Ascension.Value(ascension, Ascension.DeadlyEnemies, 8, 7)
                            )
                            // StompDamage
                            : new Intent(
                                IntentType.Attack,
                                Ascension.Value(ascension, Ascension.DeadlyEnemies, 14, 13)
                            )
                    );

            case KE.Nibbit:
                // Alone Nibbit: Butt, Slice+block, Hiss loop.
                return (enemy.MoveIndex % 3) switch
                {
                    // ButtDamage
                    0 => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 13, 12)
                    ),
                    // SliceDamage
                    1 => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 7, 6)
                    ),
                    _ => new Intent(IntentType.Buff, 0),
                };

            case KE.LeafSlimeS:
            {
                // Attacks are LeafSlimeS.TackleDamage.
                // Both branches CannotRepeat → strictly alternating, but RandomBranchState
                // always consumes 1 RNG call (even on initialization and forced transitions).
                double pick = rng.NextDouble();
                if (enemy.LastMove == -1)
                {
                    // Initialization: 50/50 (both available in empty StateLog).
                    if (pick < 0.5)
                    {
                        enemy.LastMove = 0;
                        return new Intent(
                            IntentType.Attack,
                            Ascension.Value(ascension, Ascension.DeadlyEnemies, 4, 3)
                        );
                    }
                    enemy.LastMove = 1;
                    return new Intent(IntentType.Debuff, 1);
                }
                if (enemy.LastMove == 0)
                {
                    // Last was Attack (CannotRepeat) → forced Debuff.
                    enemy.LastMove = 1;
                    return new Intent(IntentType.Debuff, 1);
                }
                // Last was Debuff (CannotRepeat) → forced Attack.
                enemy.LastMove = 0;
                return new Intent(
                    IntentType.Attack,
                    Ascension.Value(ascension, Ascension.DeadlyEnemies, 4, 3)
                );
            }

            case KE.TwigSlimeS:
                // TackleDamage
                return new Intent(
                    IntentType.Attack,
                    Ascension.Value(ascension, Ascension.DeadlyEnemies, 5, 4)
                );

            case KE.LeafSlimeM:
                return enemy.MoveIndex % 2 == 0
                    // ClumpDamage
                    ? new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 9, 8)
                    )
                    : new Intent(IntentType.Debuff, 2);

            case KE.TwigSlimeM:
            {
                // Initialization call (MoveIndex=1, LastMove=-1): initial state is STICKY_SHOT,
                // no RNG consumed (the state machine starts at the pre-set initial state).
                if (enemy.MoveIndex == 1 && enemy.LastMove == -1)
                {
                    return new Intent(IntentType.Debuff, 1);
                }

                // RandomBranchState always consumes 1 RNG call.
                double pick = rng.NextDouble();
                // LastMove -1: round-2 call after initial Sticky Shot (CannotRepeat) → force Attack.
                // LastMove  1: last chosen move was Sticky Shot (CannotRepeat) → force Attack.
                if (enemy.LastMove is -1 or 1)
                {
                    enemy.LastMove = 0;
                    return new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 12, 11)
                    );
                }
                // LastMove 2: two consecutive attacks (CanRepeatXTimes=2 exhausted) → force Sticky.
                if (enemy.LastMove == 2)
                {
                    enemy.LastMove = 1;
                    return new Intent(IntentType.Debuff, 1);
                }
                // LastMove 0: one consecutive attack → 50/50.
                if (pick < 0.5)
                {
                    enemy.LastMove = 2;
                    return new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 12, 11)
                    );
                }
                enemy.LastMove = 1;
                return new Intent(IntentType.Debuff, 1);
            }

            case KE.TwoTailedRat:
            {
                // Four branches, all reached from every move, with weights that MOVE: when
                // the rat can summon, CALL_FOR_BACKUP weighs 0.75 and the other three a
                // twelfth each; when it cannot, backup weighs nothing and the others weigh
                // one. SCREECH also carries a cooldown of 3 on top of CannotRepeat. The
                // emulator summoned whenever it was able and otherwise cycled on MoveIndex.
                const int scratch = 0;
                const int bite = 1;
                const int screech = 2;
                const int backup = 3;

                int move;
                if (enemy.LastMove < 0 && !enemy.StartsOnBranch)
                {
                    // A rat that started the fight opens on its StarterMoveIndex move.
                    move = enemy.MoveIndex % 3;
                }
                else
                {
                    // CanSummon() also refuses when any OTHER living rat has already
                    // queued CALL_FOR_BACKUP, which is what stops a pack from all
                    // summoning at once: the rats roll in roster order, and the first to
                    // pick backup takes the option away from the rest of the pass.
                    bool anotherRatIsCalling =
                        roster?.Any(other =>
                            other != enemy
                            && other.Hp > 0
                            && other.DefId == KE.TwoTailedRat
                            && other.LastMove == backup
                        ) ?? false;
                    // CanSummon() also needs a free slot, and TwoTailedRatsNormal only
                    // declares five: once the pack fills them GetNextSlot comes back
                    // empty and backup weighs nothing, so the last rat attacks instead.
                    bool slotIsFree = (roster?.Count(other => other.Hp > 0) ?? 0) < RatSlots;
                    bool canSummon =
                        BuffSystem.Get(enemy.Buffs, BuffId.SummonCooldown) <= 0
                        && BuffSystem.Get(enemy.Buffs, BuffId.BackupCount) < 3
                        && slotIsFree
                        && !anotherRatIsCalling;
                    float ordinary = canSummon ? 1f / 12f : 1f;
                    bool screechOnCooldown = enemy
                        .MoveHistory.AsEnumerable()
                        .Reverse()
                        .Take(3)
                        .Contains(screech);

                    move = PickWeightedBranch(
                        [
                            (scratch, enemy.LastMove == scratch ? 0f : ordinary),
                            (bite, enemy.LastMove == bite ? 0f : ordinary),
                            (
                                screech,
                                enemy.LastMove == screech || screechOnCooldown ? 0f : ordinary
                            ),
                            // UseOnlyOnce: a rat calls for backup once in a combat.
                            (backup, canSummon && !enemy.OnceOnlyMoveUsed ? 0.75f : 0f),
                        ],
                        rng
                    );
                }

                enemy.OnceOnlyMoveUsed |= move == backup;
                enemy.LastMove = move;
                enemy.MoveHistory.Add(move);
                return move switch
                {
                    // ScratchDamage
                    scratch => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 9, 8)
                    ),
                    // DiseaseBiteDamage
                    bite => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 7, 6)
                    ),
                    screech => new Intent(IntentType.Debuff, 1),
                    _ => new Intent(IntentType.Buff, 0),
                };
            }

            case KE.CorpseSlug:
                return (enemy.MoveIndex % 3) switch
                {
                    // MultiAttackIntent(WhipSlapDamage, WhipSlapRepeat): 3, twice over,
                    // no ascension term. Folding it into a single 6 is the same number
                    // only while the slug has no Strength -- and Ravenous hands it
                    // Strength every time an ally dies. The game adds Strength to EACH
                    // hit, so at Strength 4 the game announces 14 and a single 6 gives
                    // 10. A live Underdocks capture reads "7x2" on this intent.
                    0 => new Intent(IntentType.Attack, 3, Hits: 2),
                    // GlompDamage
                    1 => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 9, 8)
                    ),
                    _ => new Intent(IntentType.Debuff, 2),
                };

            case KE.SludgeSpinner:
            {
                int move;
                if (enemy.MoveIndex == 0)
                {
                    move = 0;
                }
                else
                {
                    int[] pool = enemy.LastMove switch
                    {
                        0 => [1, 2],
                        1 => [0, 2],
                        _ => [0, 1],
                    };
                    move = pool[rng.Next(pool.Length)];
                }
                enemy.LastMove = move;
                return move switch
                {
                    // OilSprayDamage. OIL_SPRAY_MOVE declares SingleAttackIntent first
                    // and DebuffIntent second, and a live capture reads Attack '8' then
                    // Debuff -- an attack that also applies Weak, not a debuff carrying
                    // damage.
                    0 => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 9, 8)
                    ),
                    // SlamDamage
                    1 => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 12, 11)
                    ),
                    // RageDamage; RAGE is attack + buff, and the live readout calls it
                    // an Attack — unlike OIL_SPRAY two cases up, which it calls a Debuff.
                    _ => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 7, 6)
                    ),
                };
            }

            case KE.Toadpole:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Buff, 0),
                    // SpikeSpitDamage x SpikeSpitRepeat (repeat is 3, no ascension term)
                    1 => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 4, 3),
                        Hits: 3
                    ),
                    // WhirlDamage
                    _ => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 8, 7)
                    ),
                };

            case KE.FossilStalker:
            {
                // AddBranch(state, 2) is the maxRepeats overload: uniform over the three
                // moves, weight 1, each barred only once it has come up twice running.
                // Entered at LATCH. The emulator had a hand-written opening sequence and
                // then a free roll, which drifted from the live game by turn two.
                const int latch = 0;
                const int tackle = 1;
                const int lash = 2;
                int move;
                // LastMove, not MoveIndex: this encounter builds its stalker with
                // moveIndex 1, so an "is this the first move" test written against
                // MoveIndex never fires and the machine rolled a branch at combat setup —
                // one draw the game never makes, which desynchronises every roll after it.
                if (enemy.LastMove < 0)
                {
                    move = latch;
                }
                else
                {
                    var eligible = new List<int>();
                    foreach (int candidate in (int[])[latch, tackle, lash])
                    {
                        bool spent = candidate == enemy.LastMove && enemy.LastMoveRepeats >= 2;
                        if (!spent)
                        {
                            eligible.Add(candidate);
                        }
                    }

                    move = PickBranch(eligible, rng);
                }

                enemy.LastMoveRepeats = move == enemy.LastMove ? enemy.LastMoveRepeats + 1 : 1;
                enemy.LastMove = move;
                return move switch
                {
                    // LatchDamage
                    latch => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 14, 12)
                    ),
                    // TackleDamage; TACKLE is attack + debuff (Frail), and a live trace
                    // announces it as an Attack — 15 with the six Strength Suck had
                    // handed over by then.
                    tackle => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 11, 9)
                    ),
                    // LashDamage x LashRepeat (repeat is 2, no ascension term)
                    _ => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 4, 3),
                        Hits: 2
                    ),
                };
            }

            case KE.PunchConstruct:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Defend, 10),
                    // FastPunchDamage x 2
                    1 => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 6, 5),
                        Hits: 2
                    ),
                    // StrongPunchDamage
                    _ => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 16, 14)
                    ),
                };

            case KE.SewerClam:
                return enemy.MoveIndex % 2 == 0
                    ? new Intent(IntentType.Buff, 0)
                    // JetDamage
                    : new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 11, 10)
                    );

            case KE.Guardbot:
                return new Intent(IntentType.Defend, 15);

            case KE.Noisebot:
                return new Intent(IntentType.Debuff, 2);

            case KE.Stabbot:
                return new Intent(IntentType.Debuff, 12);

            case KE.Zapbot:
                return new Intent(IntentType.Attack, 15);

            case KE.ToughEgg:
                return BuffSystem.Get(enemy.Buffs, BuffId.Hatch) > 0
                    ? new Intent(IntentType.Buff, 0)
                    : new Intent(IntentType.Attack, 5);

            case KE.SkulkingColony:
                return (enemy.MoveIndex % 4) switch
                {
                    // ZoomDamage, twice over
                    0 or 1 => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 16, 14)
                    ),
                    // InertiaDamage; INERTIA_MOVE is an attack plus a BuffIntent
                    2 => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 11, 9)
                    ),
                    // PiercingStabsDamage x PiercingStabsRepeat
                    _ => new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 8, 7),
                        Hits: 2
                    ),
                };

            case KE.TheAdversaryMkOne:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Attack, 12),
                    1 => new Intent(IntentType.Attack, 15),
                    _ => new Intent(IntentType.Buff, 16),
                };

            case KE.TheAdversaryMkTwo:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Attack, 13),
                    1 => new Intent(IntentType.Attack, 16),
                    _ => new Intent(IntentType.Buff, 18),
                };

            case KE.TheAdversaryMkThree:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Attack, 15),
                    1 => new Intent(IntentType.Attack, 18),
                    _ => new Intent(IntentType.Buff, 20),
                };

            case KE.Architect:
            case KE.BattleFriendV1:
            case KE.BattleFriendV2:
            case KE.BattleFriendV3:
            case KE.Byrdpip:
            case KE.Osty:
            case KE.PaelsLegion:
                return new Intent(IntentType.Unknown, 0);

            default:
                return GeneratedData.Enemies.ChooseIntent(enemy.DefId, enemy.MoveIndex, 0, rng);
        }
    }

    /// <summary>
    /// RandomBranchState.GetNextState: roll NextFloat(total weight), then walk the branches
    /// in the order they were added, subtracting each weight until the roll runs out. With
    /// every weight at 1 the distribution is uniform, but the DRAW is not the same as
    /// Next(n) — same stream, different number — so a fight only tracks the live game if
    /// the roll is taken the way the game takes it.
    /// </summary>
    private static int PickBranch(List<int> eligible, Random rng) =>
        PickWeightedBranch([.. eligible.Select(move => (move, 1f))], rng);

    /// <summary>
    /// The weighted form. Weights are not always 1: a Two-Tailed Rat that can summon
    /// weighs the summon at 0.75 and its three other moves at a twelfth each, so the roll
    /// is over a total that is not the branch count.
    /// </summary>
    private static int PickWeightedBranch(List<(int Move, float Weight)> branches, Random rng)
    {
        float total = branches.Sum(branch => branch.Weight);
        if (total <= 0f)
        {
            return branches[^1].Move;
        }

        float roll = (float)(rng.NextDouble() * total);
        foreach (var branch in branches)
        {
            roll -= branch.Weight;
            if (roll <= 0f)
            {
                return branch.Move;
            }
        }

        return branches[^1].Move;
    }

    private static Intent? SecondaryIntentFor(EnemyState enemy)
    {
        return enemy.DefId switch
        {
            KE.GremlinMerc when enemy.MoveIndex % 3 is 1 or 2 => new Intent(
                IntentType.Attack,
                enemy.CurrentIntent.Magnitude
            ),
            // OIL_SPRAY's second intent is the Weak it applies, RAGE's the Strength it
            // takes. Keyed off the move actually chosen: the spinner picks at random, so
            // MoveIndex says nothing about which move this is.
            KE.SludgeSpinner when enemy.LastMove == 0 => new Intent(IntentType.Debuff, 1),
            KE.SludgeSpinner when enemy.LastMove == 2 => new Intent(IntentType.Buff, 3),
            // ADVANCED_GAS's second intent is the card debuff it inflicts.
            KE.LivingFog when enemy.MoveIndex == 0 => new Intent(IntentType.Debuff, 1),
            KE.VineShambler when enemy.MoveIndex % 3 == 1 => new Intent(IntentType.Debuff, 1),
            KE.Stabbot => new Intent(IntentType.Attack, enemy.CurrentIntent.Magnitude),
            KE.SkulkingColony when enemy.MoveIndex % 4 == 2 => new Intent(
                IntentType.Attack,
                enemy.CurrentIntent.Magnitude
            ),
            KE.TheAdversaryMkOne
            or KE.TheAdversaryMkTwo
            or KE.TheAdversaryMkThree when enemy.MoveIndex % 3 == 2 => new Intent(
                IntentType.Attack,
                enemy.CurrentIntent.Magnitude
            ),
            KE.Flyconid
                when enemy.CurrentIntent.Type == IntentType.Debuff
                    && enemy.CurrentIntent.Magnitude > 2 => new Intent(
                IntentType.Attack,
                enemy.CurrentIntent.Magnitude
            ),
            _ => null,
        };
    }

    // ── Per-enemy buff actions ─────────────────────────────────────────────────

    private static void ApplyBuffIntent(EnemyState enemy, CombatState state, Random rng)
    {
        // Buff amounts are ascension-dependent too (Nibbit's Hiss gives 2 at A8, 3 at A9).
        int ascension = state.AscensionLevel;
        switch (enemy.DefId)
        {
            case KE.CalcifiedCultist:
                // Incantation: apply 2 Ritual to self (gains +2 Strength each subsequent turn).
                BuffSystem.Apply(enemy.Buffs, BuffId.Ritual, 2);
                break;

            case KE.DampCultist:
                // Incantation: IncantationAmount of Ritual to self. The comment used to
                // say "deadly ascension value" and mean it — 6 at every level.
                BuffSystem.Apply(
                    enemy.Buffs,
                    BuffId.Ritual,
                    Ascension.Value(ascension, Ascension.DeadlyEnemies, 6, 5)
                );
                break;

            case KE.Nibbit:
                // Hiss: HissStrengthGain.
                BuffSystem.Apply(
                    enemy.Buffs,
                    BuffId.Strength,
                    Ascension.Value(ascension, Ascension.DeadlyEnemies, 3, 2)
                );
                break;

            case KE.Exoskeleton:
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 2);
                break;

            case KE.FuzzyWurmCrawler:
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 7);
                break;

            case KE.Seapunk:
                // BubbleBlock is a ToughEnemies value (live at A8); BubbleStr is a
                // DeadlyEnemies one (not live at A8).
                enemy.Block += BuffSystem.IncomingBlock(
                    Ascension.Value(ascension, Ascension.ToughEnemies, 8, 7),
                    enemy.Buffs
                );
                BuffSystem.Apply(
                    enemy.Buffs,
                    BuffId.Strength,
                    Ascension.Value(ascension, Ascension.DeadlyEnemies, 2, 1)
                );
                break;

            case KE.SnappingJaxfruit:
                DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 2);
                break;

            case KE.BowlbugNectar:
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, enemy.CurrentIntent.Magnitude);
                break;

            case KE.CubexConstruct:
                // CHARGE_UP only buffs; the blasts execute in the attack branch.
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 2);
                break;

            case KE.SewerClam:
                // PRESSURIZE_MOVE is StrengthPower(4) and nothing else. The block it also
                // used to gain here was invented: the clam's block comes from PlatingPower
                // alone, which grants it to every owner at the end of its side's turn, so
                // adding it a second time on the clam's buff turn gave it twice what the
                // game shows.
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 4);
                break;

            case KE.Fogmog:
            {
                // SWIPE executes in the attack branch now; what is left here is ILLUSION,
                // and ILLUSION_MOVE is the machine's INITIAL state with nothing leading
                // back to it -- it happens once per combat and the eye persists from then
                // on by reviving. This used to be guarded on "no eye is alive" and to
                // sweep away any dead one first, which was standing in for the revive:
                // with IllusionPower modelled that guard deletes an eye in the middle of
                // coming back and hands the fight a fresh one in its place.
                {
                    // Fogmog's ILLUSION_MOVE adds the eye with a plain CreatureCmd.Add.
                    // In the whole monster set only Wriggler sets StartStunned, so nothing
                    // else arrives stunned — and the enemy phase iterates a snapshot of
                    // the roster anyway, so a newcomer already sits out the phase that
                    // made it. Stunning on top of that delayed the eye's three Dazed by a
                    // turn, which a live capture shows in the player's hand.
                    var eye = CreateEnemy(
                        KE.EyeWithTeeth,
                        rng,
                        new Intent(IntentType.Debuff, 3),
                        state: state
                    );
                    BuffSystem.Apply(eye.Buffs, BuffId.Illusion, 1);
                    state.Enemies.Insert(
                        state.Enemies.IndexOf(enemy),
                        Effects.RelicEffects.Spawned(state, eye)
                    );
                }

                break;
            }

            case KE.BruteRubyRaider:
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 3);
                break;

            case KE.Toadpole:
                BuffSystem.Apply(enemy.Buffs, BuffId.Thorns, 2);
                break;

            case KE.FatGremlin:
                enemy.Hp = 0;
                break;

            case KE.ThievingHopper:
                BuffSystem.Apply(enemy.Buffs, BuffId.Slippery, enemy.CurrentIntent.Magnitude);
                break;

            case KE.SpinyToad:
                BuffSystem.Apply(enemy.Buffs, BuffId.Thorns, enemy.CurrentIntent.Magnitude);
                break;

            case KE.Ovicopter:
                if (enemy.CurrentIntent.Magnitude > 0)
                {
                    BuffSystem.Apply(enemy.Buffs, BuffId.Strength, enemy.CurrentIntent.Magnitude);
                }
                else
                {
                    SummonToughEggs(enemy, state, rng);
                }

                break;

            case KE.Axebot:
                enemy.Block += BuffSystem.IncomingBlock(15, enemy.Buffs);
                break;

            case KE.DevotedSculptor:
                BuffSystem.Apply(enemy.Buffs, BuffId.Ritual, enemy.CurrentIntent.Magnitude);
                break;

            case KE.Fabricator:
                SummonFabricatorBots(enemy, state, rng, includeDefensive: true);
                break;

            case KE.FrogKnight:
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, enemy.CurrentIntent.Magnitude);
                break;

            case KE.GlobeHead:
                DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 2);
                break;

            case KE.TurretOperator:
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, enemy.CurrentIntent.Magnitude);
                enemy.Block += 25;
                break;

            case KE.ScrollOfBiting:
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, enemy.CurrentIntent.Magnitude);
                ApplyPaperCuts(enemy, state);
                break;

            case KE.SlimedBerserker:
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Weak, 3);
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 3);
                break;

            case KE.TheObscura:
                if (enemy.MoveIndex == 0)
                {
                    SummonParafright(enemy, state);
                }
                else
                {
                    foreach (var ally in state.Enemies.Where(e => e.Hp > 0))
                    {
                        BuffSystem.Apply(ally.Buffs, BuffId.Strength, 3);
                    }
                }

                break;

            case KE.Wriggler:
                // WRIGGLE_MOVE adds an Infection, not a Dazed — the same status its
                // parent deals three of, and one that burns for 3 in hand at end of turn.
                AddStatus(state, ST.Infection, enemy.CurrentIntent.Magnitude);
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 2);
                break;

            case KE.FakeMerchant:
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, enemy.CurrentIntent.Magnitude);
                break;

            case KE.FlailKnight:
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 3);
                break;

            case KE.BygoneEffigy:
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, enemy.CurrentIntent.Magnitude);
                break;

            case KE.Entomancer:
                int hive = BuffSystem.Get(enemy.Buffs, BuffId.PersonalHive);
                if (hive < 3)
                {
                    BuffSystem.Apply(enemy.Buffs, BuffId.PersonalHive, 1);
                    BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 1);
                }
                else
                {
                    BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 2);
                }
                break;

            case KE.InfestedPrism:
                if (enemy.CurrentIntent.Magnitude > 0)
                {
                    DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                }

                enemy.Block += BuffSystem.IncomingBlock(22, enemy.Buffs);
                break;

            case KE.PhantasmalGardener:
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, enemy.CurrentIntent.Magnitude);
                break;

            case KE.CeremonialBeast:
                BuffSystem.Apply(enemy.Buffs, BuffId.Plow, enemy.CurrentIntent.Magnitude);
                break;

            case KE.Crusher:
            case KE.Rocket:
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, enemy.CurrentIntent.Magnitude);
                break;

            case KE.KnowledgeDemon:
                DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                enemy.Hp = Math.Min(enemy.MaxHp, enemy.Hp + 30);
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 3);
                break;

            case KE.Aeonglass:
                AddStatus(state, ST.Wither, enemy.CurrentIntent.Magnitude);
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 4);
                enemy.Block += BuffSystem.IncomingBlock(33, enemy.Buffs);
                break;

            case KE.Queen:
                foreach (var ally in state.Enemies.Where(e => e.Hp > 0 && e.DefId != KE.Queen))
                {
                    BuffSystem.Apply(ally.Buffs, BuffId.Strength, 1);
                }

                enemy.Block += BuffSystem.IncomingBlock(enemy.CurrentIntent.Magnitude, enemy.Buffs);
                break;

            case KE.SoulFysh:
                BuffSystem.Apply(enemy.Buffs, BuffId.Intangible, enemy.CurrentIntent.Magnitude);
                break;

            case KE.TheInsatiable:
                if (enemy.MoveIndex == 0)
                {
                    BuffSystem.Apply(state.PlayerBuffs, BuffId.Sandpit, 4);
                    Effects.CardEffects.AddCardToDrawPileRandomly(state, ST.FranticEscape, 3, rng);
                    AddStatus(state, ST.FranticEscape, 3);
                }
                else
                {
                    BuffSystem.Apply(enemy.Buffs, BuffId.Strength, enemy.CurrentIntent.Magnitude);
                }
                break;

            case KE.KinFollower:
            case KE.KinPriest:
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, enemy.CurrentIntent.Magnitude);
                break;

            case KE.Vantom:
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, enemy.CurrentIntent.Magnitude);
                break;

            case KE.WaterfallGiant:
                // Two different moves announce as a buff. PRESSURIZE converts the whole
                // announced amount into Steam Eruption; SIPHON heals for it and racks up
                // the same flat 3 the giant's other moves do.
                if (enemy.MoveIndex == 0)
                {
                    BuffSystem.Apply(
                        enemy.Buffs,
                        BuffId.SteamEruption,
                        enemy.CurrentIntent.Magnitude
                    );
                }
                else
                {
                    enemy.Hp = Math.Min(enemy.MaxHp, enemy.Hp + enemy.CurrentIntent.Magnitude);
                    BuffSystem.Apply(enemy.Buffs, BuffId.SteamEruption, 3);
                }

                break;

            case KE.TwoTailedRat:
                SummonRatBackup(enemy, state, rng);
                break;

            case KE.TestSubject:
                AddStatus(state, ST.Burn, enemy.CurrentIntent.Magnitude);
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 3);
                break;

            case KE.Guardbot:
                foreach (var ally in state.Enemies.Where(e => e.Hp > 0 && e.DefId == KE.Fabricator))
                {
                    ally.Block += BuffSystem.IncomingBlock(15, ally.Buffs);
                }
                break;

            case KE.ToughEgg:
                BuffSystem.Remove(enemy.Buffs, BuffId.Hatch);
                enemy.Buffs.RemoveAll(buff => buff.Id != BuffId.Minion);
                int hatchlingHp = rng.Next(20, 24);
                enemy.Hp = hatchlingHp;
                enemy.MaxHp = hatchlingHp;
                break;

            case KE.TheAdversaryMkOne:
                DealAttackDamage(enemy, state, 8);
                DealAttackDamage(enemy, state, 8);
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 2);
                break;

            case KE.TheAdversaryMkTwo:
                DealAttackDamage(enemy, state, 9);
                DealAttackDamage(enemy, state, 9);
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 3);
                break;

            case KE.TheAdversaryMkThree:
                DealAttackDamage(enemy, state, 10);
                DealAttackDamage(enemy, state, 10);
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 4);
                break;
        }
    }

    private static void ApplyDebuffIntent(EnemyState enemy, CombatState state, Random rng)
    {
        switch (enemy.DefId)
        {
            case KE.Chomper:
                AddStatus(state, ST.Dazed, 3);
                break;

            case KE.LeafSlimeS:
            case KE.TwigSlimeM:
                AddStatus(state, ST.Slimed, 1);
                break;

            case KE.LeafSlimeM:
                AddStatus(state, ST.Slimed, 2);
                break;

            case KE.TwoTailedRat:
            case KE.CorpseSlug:
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Frail, enemy.CurrentIntent.Magnitude);
                break;

            case KE.TerrorEel:
                BuffSystem.Apply(
                    state.PlayerBuffs,
                    BuffId.Vulnerable,
                    enemy.CurrentIntent.Magnitude
                );
                BuffSystem.Remove(enemy.Buffs, BuffId.TerrorQueued);
                // TERROR_MOVE's FollowUpState is CRASH, and the increment at the end of
                // this turn is what carries the index there.
                enemy.MoveIndex = -1;
                break;

            case KE.LagavulinMatriarch:
                // SOUL_SIPHON takes the Strength and Dexterity it gives itself: the
                // matriarch's attacks climb by 2 every fourth turn, which is the whole
                // shape of the fight after the first cycle.
                BuffSystem.Apply(
                    state.PlayerBuffs,
                    BuffId.Strength,
                    -enemy.CurrentIntent.Magnitude
                );
                BuffSystem.Apply(
                    state.PlayerBuffs,
                    BuffId.Dexterity,
                    -enemy.CurrentIntent.Magnitude
                );
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, enemy.CurrentIntent.Magnitude);
                break;

            case KE.ShrinkerBeetle:
                // Applies permanent Shrink (–1 = infinite; tied to ShrinkerBeetle's life).
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Shrink, -1);
                break;

            case KE.Mawler:
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Vulnerable, 3);
                break;

            case KE.Flyconid:
                // VULNERABLE_SPORES_MOVE, the only one of the three that is a bare
                // DebuffIntent. FRAIL_SPORES used to be handled here too, keyed on the
                // intent's magnitude -- but it announces as an Attack and so never
                // arrived; its rider lives with the attack now.
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Vulnerable, 2);
                break;

            case KE.LivingFog:
                DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Smoggy, 1);
                break;

            case KE.BowlbugSilk:
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Weak, 1);
                break;

            case KE.Myte:
                AddStatusToHand(state, ST.Toxic, enemy.CurrentIntent.Magnitude);
                break;

            case KE.Ovicopter:
                DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Vulnerable, 2);
                break;

            case KE.LouseProgenitor:
                DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Frail, 2);
                break;

            case KE.HunterKiller:
                BuffSystem.Apply(
                    state.PlayerBuffs,
                    BuffId.Vulnerable,
                    enemy.CurrentIntent.Magnitude
                );
                break;

            case KE.FakeMerchant:
                DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Frail, 1);
                break;

            case KE.PhrogParasite:
                AddStatus(state, ST.Infection, enemy.CurrentIntent.Magnitude);
                break;

            case KE.SoulNexus:
                DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Vulnerable, 2);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Weak, 2);
                break;

            case KE.SpectralKnight:
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Hex, enemy.CurrentIntent.Magnitude);
                break;

            case KE.MechaKnight:
                AddStatusToHand(state, ST.Burn, enemy.CurrentIntent.Magnitude);
                break;

            case KE.MagiKnight:
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Dampen, enemy.CurrentIntent.Magnitude);
                break;

            case KE.Aeonglass:
                DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Ebb, 3);
                break;

            case KE.KnowledgeDemon:
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Disintegration, 6);
                break;

            case KE.Queen:
                BuffSystem.Apply(
                    state.PlayerBuffs,
                    BuffId.ChainsOfBinding,
                    enemy.CurrentIntent.Magnitude
                );
                break;

            case KE.SoulFysh:
                Effects.CardEffects.AddCardToDrawPileRandomly(state, ST.Beckon, 1, rng);
                AddStatus(state, ST.Beckon, enemy.CurrentIntent.Magnitude - 1);
                break;

            case KE.TestSubject:
                AddStatusToHand(state, ST.Burn, enemy.CurrentIntent.Magnitude);
                break;

            case KE.FrogKnight:
                DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Frail, 2);
                break;

            case KE.SlimedBerserker:
                AddStatus(state, ST.Slimed, enemy.CurrentIntent.Magnitude);
                break;

            case KE.TheLost:
                BuffSystem.Apply(
                    state.PlayerBuffs,
                    BuffId.Strength,
                    -enemy.CurrentIntent.Magnitude
                );
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, enemy.CurrentIntent.Magnitude);
                break;

            case KE.TheForgotten:
                enemy.Block += BuffSystem.IncomingBlock(8, enemy.Buffs);
                BuffSystem.Apply(
                    state.PlayerBuffs,
                    BuffId.Dexterity,
                    -enemy.CurrentIntent.Magnitude
                );
                BuffSystem.Apply(enemy.Buffs, BuffId.Dexterity, enemy.CurrentIntent.Magnitude);
                break;

            case KE.EyeWithTeeth:
                AddStatus(state, ST.Dazed, 3);
                break;

            case KE.TrackerRubyRaider:
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Frail, 2);
                break;

            case KE.PunchConstruct:
                DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Frail, 1);
                break;

            case KE.SlitheringStrangler:
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Constrict, 3);
                break;

            case KE.HauntedShip:
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Weak, 3);
                AddStatus(state, ST.Dazed, 5);
                break;

            case KE.Noisebot:
                AddStatus(state, ST.Dazed, 1);
                Effects.CardEffects.AddCardToDrawPileRandomly(state, ST.Dazed, 1, rng);
                break;

            case KE.Stabbot:
                DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Frail, 1);
                break;
        }
    }

    /// <summary>
    /// One attack of <paramref name="hits"/> hits, as a single <c>AttackCommand</c>.
    /// </summary>
    /// <remarks>
    /// The hits land one at a time so block, Thorns and every per-instance effect see each
    /// of them -- but <c>SuckPower.AfterAttack</c> fires ONCE for the whole command, with
    /// <c>Amount x</c> the number of hits that dealt unblocked damage. Triggering it per
    /// hit instead fed the Strength it grants back into the SAME attack's later hits: a
    /// Fossil Stalker's two-hit Lash landed its second swing three higher than the game's,
    /// and every announcement after it followed.
    /// </remarks>
    private static void DealAttack(EnemyState enemy, CombatState state, int baseDamage, int hits)
    {
        int landed = 0;
        for (int i = 0; i < hits; i++)
        {
            if (DealAttackDamage(enemy, state, baseDamage, triggerSuck: false))
            {
                landed++;
            }
        }

        TriggerSuck(enemy, landed);
    }

    private static bool DealAttackDamage(
        EnemyState enemy,
        CombatState state,
        int baseDamage,
        bool triggerSuck = true
    )
    {
        int damage = BuffSystem.IncomingDamage(baseDamage, enemy.Buffs, state.PlayerBuffs);
        if (
            BuffSystem.Get(state.PlayerBuffs, BuffId.Colossus) > 0
            && BuffSystem.Get(enemy.Buffs, BuffId.Vulnerable) > 0
        )
        {
            damage /= 2;
        }
        int absorbed = Math.Min(state.PlayerBlock, damage);
        state.PlayerBlock -= absorbed;
        int unblocked = damage - absorbed;
        if (unblocked > 0)
        {
            state.UnblockedDamageHitCount++;
        }

        state.PlayerHp = Math.Max(0, state.PlayerHp - unblocked);

        // TheGambitPower.AfterDamageReceived: any unblocked powered attack removes the
        // power and kills the owner outright. Modelling it as a lesser debuff would tell
        // an agent the card is cheap when it can end the run.
        if (unblocked > 0 && BuffSystem.Get(state.PlayerBuffs, BuffId.TheGambitPower) > 0)
        {
            BuffSystem.Remove(state.PlayerBuffs, BuffId.TheGambitPower);
            state.PlayerHp = 0;
        }

        if (unblocked > 0)
        {
            Effects.RelicEffects.ApplyAfterUnblockedDamageReceived(state);
            // Red Skull and Lizard Tail both answer a changed HP total, and a multi-hit
            // intent must not land its later hits on a player the relic already revived.
            Effects.RelicEffects.ApplyAfterPlayerHpChanged(state);
        }

        if (unblocked > 0 && triggerSuck)
        {
            TriggerSuck(enemy);
        }

        ApplyPlayerThorns(enemy, state);
        ApplyPlayerFlameBarrier(enemy, state);
        return unblocked > 0;
    }

    /// <summary>
    /// <c>FlameBarrierPower.AfterDamageReceived</c>: the attacker takes <c>Amount</c>
    /// unpowered damage back for every instance of damage the player receives from a
    /// powered attack.
    /// </summary>
    /// <remarks>
    /// Three things about that hook, each of which this used to get wrong. It fires PER
    /// HIT, so a multi-hit attack pays for every swing. It fires whether or not block
    /// absorbed the hit -- <c>CreatureCmd</c> guards <c>AfterCurrentHpChanged</c> on
    /// <c>UnblockedDamage > 0</c> and pointedly does NOT guard this one. And it is skipped
    /// when the hit killed its target (<c>!WasTargetKilled || !IsDead</c>), so a player who
    /// dies to the blow does not retaliate -- one a relic revives does.
    ///
    /// <para>
    /// It lived in the attack branch's generic tail, past eighteen <c>break</c>s, so it
    /// answered only single-hit attacks by monsters with no special case: zero retaliation
    /// against a multi-hit intent, and none against a Snapping Jaxfruit or a Sludge Spinner
    /// either. Here it is per hit for every attack, the way Thorns already was.
    /// </para>
    /// <para>
    /// Thorns is the near neighbour and NOT the same hook -- <c>ThornsPower</c> is
    /// <c>BeforeDamageReceived</c>, so in the game it resolves before the blow lands and
    /// can kill an attacker mid-attack. <c>ApplyPlayerThorns</c> runs after. That gap is
    /// separate from this one and is not addressed here.
    /// </para>
    /// </remarks>
    private static void ApplyPlayerFlameBarrier(EnemyState enemy, CombatState state)
    {
        int flameBarrier = BuffSystem.Get(state.PlayerBuffs, BuffId.FlameBarrier);
        if (flameBarrier <= 0 || state.PlayerHp <= 0)
        {
            return;
        }

        int absorbed = Math.Min(enemy.Block, flameBarrier);
        enemy.Block -= absorbed;
        enemy.Hp = Math.Max(0, enemy.Hp - (flameBarrier - absorbed));
    }

    private static void TriggerSuck(EnemyState enemy, int hitCount = 1)
    {
        int suck = BuffSystem.Get(enemy.Buffs, BuffId.Suck);
        if (suck > 0 && hitCount > 0)
        {
            BuffSystem.Apply(enemy.Buffs, BuffId.Strength, suck * hitCount);
        }
    }

    private static void ApplyPlayerThorns(EnemyState enemy, CombatState state)
    {
        int thorns = BuffSystem.Get(state.PlayerBuffs, BuffId.Thorns);
        if (thorns <= 0)
        {
            return;
        }

        int absorbed = Math.Min(enemy.Block, thorns);
        enemy.Block -= absorbed;
        enemy.Hp = Math.Max(0, enemy.Hp - (thorns - absorbed));
    }

    private static void StealGremlinMercGold(EnemyState enemy, CombatState state)
    {
        int amount = Math.Min(20, state.PlayerGold);
        if (amount <= 0)
        {
            return;
        }

        state.PlayerGold -= amount;
        enemy.StolenGold += amount;
    }

    private static void StealDrawOrDiscardCard(CombatState state)
    {
        if (state.DrawPile.Count > 0)
        {
            state.RemoveFromDrawPileAt(0);
        }
        else if (state.DiscardPile.Count > 0)
        {
            state.DiscardPile.RemoveAt(0);
        }
    }

    private static void ApplyPaperCuts(EnemyState enemy, CombatState state)
    {
        int amount = BuffSystem.Get(enemy.Buffs, BuffId.PaperCuts);
        if (amount <= 0)
        {
            return;
        }

        state.PlayerMaxHp = Math.Max(1, state.PlayerMaxHp - amount);
        state.PlayerHp = Math.Min(state.PlayerHp, state.PlayerMaxHp);
    }

    /// <summary>
    /// ShriekPower.AfterDamageReceived: an unblocked hit that leaves the Terror Eel at or
    /// below its threshold stuns it, and the turn after the stun it performs TERROR —
    /// which lands Vulnerable 99 on the player. The power is removed as it fires, so this
    /// happens once a combat. Nothing else in act 1 can be reached only by hurting a
    /// monster, which is why this went unmodelled: a capture that plays no cards never
    /// scratches a 150 HP elite.
    /// </summary>
    internal static void TriggerShriekIfWounded(EnemyState enemy)
    {
        int threshold = BuffSystem.Get(enemy.Buffs, BuffId.Shriek);
        if (threshold <= 0 || enemy.Hp > threshold || enemy.Hp <= 0)
        {
            return;
        }

        BuffSystem.Remove(enemy.Buffs, BuffId.Shriek);
        BuffSystem.Apply(enemy.Buffs, BuffId.TerrorQueued, 1);
        BuffSystem.Apply(enemy.Buffs, BuffId.Stunned, 1);
        // CreatureCmd.Stun changes the move immediately, so the intent the player is
        // looking at becomes the stun rather than whatever was announced.
        enemy.CurrentIntent = new Intent(IntentType.Unknown, 0);
    }

    /// <summary>TwoTailedRatsNormal.Slots: five, three of them taken at the start.</summary>
    private const int RatSlots = 5;

    /// <summary>WaterfallGiant.PressureGunIncrease: what each firing adds.</summary>
    private const int PressureGunIncrease = 5;

    private static void AddStatus(CombatState state, int cardId, int count)
    {
        for (int i = 0; i < count; i++)
        {
            state.DiscardPile.Add(new CardInstance(cardId, false));
        }
    }

    private static void AddStatusToHand(CombatState state, int cardId, int count)
    {
        for (int i = 0; i < count; i++)
        {
            state.Hand.Add(new CardInstance(cardId, false));
        }
    }

    private static void SummonToughEggs(EnemyState enemy, CombatState state, Random rng)
    {
        int eggsToAdd = Math.Min(3, 6 - state.Enemies.Count);
        int insertIndex = state.Enemies.IndexOf(enemy);
        for (int i = 0; i < eggsToAdd; i++)
        {
            // Not stunned — Ovicopter adds eggs with a plain CreatureCmd.Add, and the
            // egg's own first move is its hatch, so the delay is already modelled once.
            var egg = CreateEnemy(
                KE.ToughEgg,
                rng,
                new Intent(IntentType.Unknown, 0),
                state: state
            );
            BuffSystem.Apply(egg.Buffs, BuffId.Minion, 1);
            state.Enemies.Insert(insertIndex + i, Effects.RelicEffects.Spawned(state, egg));
        }
    }

    private static void SummonFabricatorBots(
        EnemyState enemy,
        CombatState state,
        Random rng,
        bool includeDefensive
    )
    {
        int insertIndex = state.Enemies.IndexOf(enemy);
        if (includeDefensive && state.Enemies.Count < 6)
        {
            int defensive = rng.Next(2) == 0 ? KE.Guardbot : KE.Noisebot;
            var bot = CreateEnemy(
                defensive,
                rng,
                BotIntent(defensive),
                stunned: true,
                state: state
            );
            BuffSystem.Apply(bot.Buffs, BuffId.Minion, 1);
            state.Enemies.Insert(insertIndex++, Effects.RelicEffects.Spawned(state, bot));
        }
        if (state.Enemies.Count < 6)
        {
            int aggro = rng.Next(2) == 0 ? KE.Zapbot : KE.Stabbot;
            var bot = CreateEnemy(aggro, rng, BotIntent(aggro), stunned: true, state: state);
            BuffSystem.Apply(bot.Buffs, BuffId.Minion, 1);
            state.Enemies.Insert(insertIndex, Effects.RelicEffects.Spawned(state, bot));
        }
    }

    private static Intent BotIntent(int defId) =>
        defId switch
        {
            KE.Guardbot => new Intent(IntentType.Defend, 15),
            KE.Noisebot => new Intent(IntentType.Debuff, 2),
            KE.Zapbot => new Intent(IntentType.Attack, 15),
            KE.Stabbot => new Intent(IntentType.Debuff, 12),
            _ => new Intent(IntentType.Unknown, 0),
        };

    private static void SummonParafright(EnemyState enemy, CombatState state)
    {
        // Not stunned: TheObscura adds it with a plain CreatureCmd.Add.
        var parafright = CreateEnemy(
            KE.Parafright,
            new Random(0),
            new Intent(IntentType.Attack, 17),
            state: state
        );
        BuffSystem.Apply(parafright.Buffs, BuffId.Illusion, 1);
        state.Enemies.Insert(
            state.Enemies.IndexOf(enemy),
            Effects.RelicEffects.Spawned(state, parafright)
        );
    }

    /// <summary>
    /// The highest of <c>TwoTailedRatsNormal.Slots</c> no living creature stands in, or
    /// -1 when the pack has filled them.
    /// </summary>
    private static int LastFreeRatSlot(CombatState state)
    {
        for (int slot = RatSlots - 1; slot >= 0; slot--)
        {
            if (!state.Enemies.Any(e => e.Hp > 0 && e.Slot == slot))
            {
                return slot;
            }
        }

        return -1;
    }

    /// <summary>Puts a slotted creature into the roster in slot order.</summary>
    private static void InsertBySlot(CombatState state, EnemyState enemy)
    {
        int at = state.Enemies.FindIndex(other => other.Slot > enemy.Slot);
        if (at < 0)
        {
            state.Enemies.Add(enemy);
            return;
        }

        state.Enemies.Insert(at, enemy);
    }

    private static bool CanRatSummon(EnemyState enemy, Random rng)
    {
        return BuffSystem.Get(enemy.Buffs, BuffId.SummonCooldown) <= 0
            && BuffSystem.Get(enemy.Buffs, BuffId.BackupCount) < 3
            && rng.NextDouble() < 0.75;
    }

    private static void TickRatSummonCooldown(EnemyState enemy)
    {
        if (enemy.DefId != KE.TwoTailedRat)
        {
            return;
        }

        int cooldown = BuffSystem.Get(enemy.Buffs, BuffId.SummonCooldown);
        if (cooldown > 0)
        {
            BuffSystem.Apply(enemy.Buffs, BuffId.SummonCooldown, -1);
        }
    }

    private static void SummonRatBackup(EnemyState enemy, CombatState state, Random rng)
    {
        // CallForBackup: Slots.LastOrDefault(s => no creature holds s). The slots a dead
        // rat leaves behind are free again, so the answer moves during the fight -- and
        // CanSummon() fails outright when there is none.
        int slot = LastFreeRatSlot(state);
        if (slot < 0)
        {
            return;
        }

        // A summoned rat has StarterMoveIndex == -1, so its machine starts ON the branch
        // rather than on a move — which means its very first selection rolls, where a rat
        // that began the fight does not.
        // Not stunned: CallForBackup adds the rat with a plain CreatureCmd.Add, and the
        // enemy phase already iterates a snapshot of the roster — so the newcomer sits out
        // the phase that summoned it and then fights normally. A stun on top of that costs
        // it a second turn, which is a summoned rat's whole first attack.
        var summoned = CreateEnemy(
            KE.TwoTailedRat,
            rng,
            new Intent(IntentType.Unknown, 0),
            state: state
        );
        summoned.StartsOnBranch = true;
        summoned.Slot = slot;
        // The roster reads in slot order. With the three starters alive the last free
        // slot is "second" and the newcomer leads the pack, which is what this used to
        // assume unconditionally; once the rat in "fifth" has died the last free slot is
        // "fifth" and the newcomer joins the BACK, so a hardcoded front insert named a
        // different creature than the game on every target index from then on.
        InsertBySlot(state, Effects.RelicEffects.Spawned(state, summoned));

        int nextBackupCount =
            state
                .Enemies.Where(e => e.DefId == KE.TwoTailedRat)
                .Select(e => BuffSystem.Get(e.Buffs, BuffId.BackupCount))
                .DefaultIfEmpty(0)
                .Max() + 1;
        foreach (var rat in state.Enemies.Where(e => e.DefId == KE.TwoTailedRat))
        {
            int current = BuffSystem.Get(rat.Buffs, BuffId.BackupCount);
            BuffSystem.Apply(rat.Buffs, BuffId.BackupCount, nextBackupCount - current);
        }
    }

    /// <summary>
    /// A summon's HP, rolled the way CombatState.CreateCreature rolls it:
    /// SetUniqueMonsterHpValue on the Niche stream, over the monster's band minus the
    /// MaxHp of the creatures ALREADY ON THAT SIDE. Note the exclusion is the roster as
    /// it stands and not every value the combat has ever used, so a dead enemy's HP is
    /// available again. When the band is exhausted the game rolls it flat instead.
    /// </summary>
    internal static int RollSummonedHp(int min, int max, CombatState? state, Random rng)
    {
        var niche = state?.NicheHpRng;
        if (niche == null)
        {
            return rng.Next(min, max + 1);
        }

        var available = Enumerable.Range(min, max - min + 1).ToHashSet();
        available.ExceptWith(
            state!.Enemies.Where(other => other.Hp > 0).Select(other => other.MaxHp)
        );
        return available.Count == 0
            ? niche.Next(min, max + 1)
            : available.ElementAt(niche.Next(0, available.Count));
    }

    private static EnemyState CreateEnemy(
        int defId,
        Random rng,
        Intent intent,
        bool stunned = false,
        int ascension = Ascension.DefaultLevel,
        // The combat, for the HP roll. Without it the roll falls back to the combat
        // stream, which is not where the game takes it from.
        CombatState? state = null
    )
    {
        var def = GeneratedData.Enemies.Get(defId);
        var band = def.HpBand(ascension);
        int hp = RollSummonedHp(band.Min, band.Max, state, rng);
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

        if (defId == KE.ToughEgg)
        {
            BuffSystem.Apply(enemy.Buffs, BuffId.Hatch, 1);
        }

        if (defId == KE.Zapbot)
        {
            BuffSystem.Apply(enemy.Buffs, BuffId.HighVoltage, 2);
        }

        if (defId == KE.TwoTailedRat)
        {
            BuffSystem.Apply(enemy.Buffs, BuffId.SummonCooldown, 2);
        }

        return enemy;
    }
}

// Known enemy def IDs (from Generated/Enemies.g.cs).
public static class KE
{
    public const int CalcifiedCultist = 14;
    public const int Aeonglass = 1;
    public const int Architect = 2;
    public const int Axebot = 4;
    public const int BattleFriendV1 = 10004;
    public const int BattleFriendV2 = 10005;
    public const int BattleFriendV3 = 10006;
    public const int Chomper = 16;
    public const int CorpseSlug = 17;
    public const int CeremonialBeast = 15;
    public const int Crusher = 19;
    public const int AxeRubyRaider = 5;
    public const int AssassinRubyRaider = 3;
    public const int BowlbugEgg = 6;
    public const int BowlbugNectar = 7;
    public const int BowlbugRock = 8;
    public const int BowlbugSilk = 9;
    public const int BruteRubyRaider = 10;
    public const int BygoneEffigy = 11;
    public const int Byrdonis = 12;
    public const int Byrdpip = 13;
    public const int DecimillipedeSegment = 22;
    public const int Entomancer = 24;
    public const int CrossbowRubyRaider = 18;
    public const int DampCultist = 21;
    public const int DevotedSculptor = 23;
    public const int EyeWithTeeth = 26;
    public const int Exoskeleton = 25;
    public const int Fabricator = 27;
    public const int FakeMerchant = 10003;
    public const int Flyconid = 30;
    public const int FlailKnight = 29;
    public const int Fogmog = 31;
    public const int FossilStalker = 32;
    public const int FrogKnight = 33;
    public const int GasBomb = 35;
    public const int GlobeHead = 36;
    public const int Guardbot = 38;
    public const int FuzzyWurmCrawler = 34;
    public const int FatGremlin = 28;
    public const int GremlinMerc = 37;
    public const int HauntedShip = 39;
    public const int HunterKiller = 40;
    public const int Inklet = 42;
    public const int InfestedPrism = 41;
    public const int KinFollower = 43;
    public const int KinPriest = 44;
    public const int KnowledgeDemon = 45;
    public const int LagavulinMatriarch = 46;
    public const int LivingFog = 49;
    public const int LivingShield = 50;
    public const int LouseProgenitor = 51;
    public const int LeafSlimeM = 47;
    public const int LeafSlimeS = 48;
    public const int Mawler = 53;
    public const int MagiKnight = 52;
    public const int MechaKnight = 54;
    public const int Myte = 55;
    public const int Nibbit = 56;
    public const int Noisebot = 57;
    public const int Osty = 58;
    public const int Ovicopter = 59;
    public const int OwlMagistrate = 60;
    public const int PaelsLegion = 61;
    public const int Parafright = 62;
    public const int PhantasmalGardener = 63;
    public const int PhrogParasite = 64;
    public const int Queen = 66;
    public const int Rocket = 67;
    public const int PunchConstruct = 65;
    public const int ScrollOfBiting = 68;
    public const int Seapunk = 69;
    public const int SewerClam = 70;
    public const int ShrinkerBeetle = 71;
    public const int SkulkingColony = 72;
    public const int SneakyGremlin = 78;
    public const int SnappingJaxfruit = 77;
    public const int SpinyToad = 82;
    public const int SlitheringStrangler = 74;
    public const int SludgeSpinner = 75;
    public const int SlumberingBeetle = 76;
    public const int SlimedBerserker = 73;
    public const int SoulNexus = 80;
    public const int SoulFysh = 79;
    public const int SpectralKnight = 81;
    public const int Stabbot = 83;
    public const int TerrorEel = 84;
    public const int Toadpole = 93;
    public const int TorchHeadAmalgam = 94;
    public const int ThievingHopper = 92;
    public const int TheAdversaryMkOne = 85;
    public const int TheAdversaryMkThree = 86;
    public const int TheAdversaryMkTwo = 87;
    public const int TheForgotten = 88;
    public const int TheInsatiable = 89;
    public const int TheLost = 90;
    public const int TheObscura = 91;
    public const int TrackerRubyRaider = 96;
    public const int ToughEgg = 95;
    public const int TwigSlimeM = 99;
    public const int TwigSlimeS = 100;
    public const int Tunneler = 97;
    public const int TwoTailedRat = 101;
    public const int TurretOperator = 98;
    public const int TestSubject = 10007;
    public const int CubexConstruct = 20;
    public const int VineShambler = 103;
    public const int Vantom = 102;
    public const int WaterfallGiant = 104;
    public const int Wriggler = 105;
    public const int Zapbot = 106;
}
