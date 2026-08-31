namespace Sts2Emulator.Core;

public enum BuffId
{
    Strength,
    Dexterity,
    Vulnerable,
    Weak,
    Frail,
    Poison,
    Burn,
    Ritual, // enemy: gain N Strength at end of each turn (skips turn applied)
    DemonForm, // player: gain N Strength at start of each player turn
    Aggression, // player: start of turn, add a random upgraded card
    Hellraiser, // player: whenever you draw a Strike, play it automatically
    Rage, // player: gain N block when playing an Attack; removed at end of player turn
    FeelNoPain, // player: gain N block when any card is exhausted
    DarkEmbrace, // player: draw N cards when any card is exhausted
    Barricade, // player: block does not clear at start of turn
    Colossus, // player: Vulnerable enemies deal half attack damage
    Corruption, // player: Skills cost 0 and exhaust
    Inferno, // player: after unblocked self-damage on player turn, deal N damage to all enemies
    InfernoSelfDamage, // player: unblockable HP loss at start of each player turn from Inferno
    Metallicize, // player: gain N block at end of player turn
    FlameBarrier, // player: deal N damage to melee attackers; removed at end of enemy turn
    Juggernaut, // player: deal N unpowered damage to random enemy when gaining block
    RupturePower, // player: gain 1 Strength when losing HP from card effects
    Juggling, // player: copy the third Attack played each turn into hand
    Stampede, // player: auto-play random Attacks when play phase starts
    Vicious, // player: draw N cards when applying Vulnerable
    OneTwoPunch, // player: duplicate the next N Attack cards this turn
    CrueltyPower, // player: increase Vulnerable multiplier by N%
    PyrePower, // player: gain N extra energy each turn
    UnmovablePower, // player: first N block gains each turn are doubled
    CrimsonMantleBlock, // player: gain N block at start of turn
    CrimsonMantleSelfDamage, // player: lose N HP at start of turn
    SetupStrikePower, // player: temporary Strength marker
    EntropyPower, // player: transform N cards in hand at turn start
    FastenPower, // player: gain extra block from Defend cards
    AutomationPower, // player: gain N energy after every 10 drawn cards
    CalamityPower, // player: generate N random Attacks after each played Attack
    MayhemPower, // player: auto-play N cards from the top of draw pile each turn
    PanachePower, // player: deal N damage to all enemies after every five card plays
    PrepTimePower, // player: gain N Vigor at the start of each player turn
    RollingBoulderPower, // player: deal N damage to all enemies at turn start, then grow
    StratagemPower, // player: pull N cards from draw pile to hand after shuffling
    TheBombPower, // player: turns remaining until The Bomb explodes
    TheBombDamage, // player: damage dealt by The Bomb when it expires
    Vigor, // player: add N damage to the next Attack, then expire
    NoBlock, // player: card-based block gains are prevented for N enemy turns
    NoDraw, // player: no further cards can be drawn this turn (Battle Trance)
    TheGambitPower, // player: the next unblocked powered attack kills you outright
    ClawDamage, // player: bonus damage for future Claw plays
    Focus, // player: orb-like generated effects scale by N
    Slow, // enemy: powered attack damage taken scales with cards played this turn
    SlowCount, // enemy: dynamic Slow counter reset at the start of its side turn
    Buffer, // player: prevent the next N HP-loss instances
    EchoForm, // player: duplicate the first N card plays each turn
    Loop, // player: trigger the front orb N extra times at start of turn
    MachineLearning, // player: draw N extra cards each turn
    Storm, // player: channel N Lightning orbs after playing a Power
    Thunder, // player: after Lightning evokes, deal N unpowered damage to its target
    CreativeAi, // player: add random Power cards at draw start
    Coolant, // player: gain block per distinct orb type at turn start
    Feral, // player: first N zero-cost Attacks each turn return to hand
    FeralUsed, // hidden counter for Feral returns used this turn
    Hailstorm, // player: if any Frost orb is present, deal N to all at turn end
    SignalBoost, // player: next N Powers are played twice
    Smokestack, // player: generated Status cards deal N to all
    Spinner, // player: channel N Glass orbs after energy reset
    Subroutine, // player: gain N energy after playing a Power
    TemporaryFocus, // player: remove N Focus at end of player turn
    TrashToTreasure, // player: generated Status cards channel random orbs
    ConsumingShadow, // player: evoke last orb N times at player turn end
    Envenom, // player: apply Poison when attacks deal damage
    InfiniteBlades, // player: add Shivs at turn start
    NoxiousFumes, // player: apply Poison to all enemies each turn
    NextTurnEnergy, // player: gain N energy next turn
    NextTurnDraw, // player: draw N extra cards next turn
    ToolsOfTheTrade, // player: draw then discard one at turn start
    ShivDamage, // player: bonus damage for Shiv-like cards
    Afterimage, // player: gain N block after playing a card
    FranticEscapePlayedCountUnused, // replaced by CardInstance.CostBump; id kept so the

    // ordinals after it do not move -- they reach the observation.
    RetainHand, // player: keep remaining hand at end of player turn
    BlockNextTurn, // player: gain N unpowered block after next turn's block clear
    Nostalgia, // player: first N Attack/Skill cards each turn go on top of draw pile
    TemporaryStrength, // player/enemy: remove this much Strength at side turn end
    Artifact, // prevent the next N debuffs
    HardToKill, // damage taken per hit is capped at N
    Thorns, // enemy: retaliatory damage, currently observed but not triggered
    Shrink, // player debuff from Shrinker Beetle, currently observed but not otherwise modeled
    Ravenous, // enemy: gain Strength and skip next move when an ally dies
    Stunned, // enemy: skip the next intent
    Slippery, // enemy: each unblocked hit loses at most 1 HP, then decrements
    Skittish, // enemy: the first card to land unblocked damage each turn gives it N block
    SkittishSpent, // enemy: Skittish's HasGainedBlockThisTurn, cleared when the player's turn ends
    Surprise, // Gremlin Merc: spawn reinforcements on death
    SummonCooldown, // Two-Tailed Rat: turns until Call for Backup is available
    Shriek, // Terror Eel: HP threshold; an unblocked hit at or below it triggers Terror
    TerrorQueued, // Terror Eel: Shriek has fired, so TERROR_MOVE follows the stunned turn
    BackupCount, // Two-Tailed Rat: number of successful backup calls
    Plating, // Sewer Clam: recurring block that decays each turn
    Suck, // Fossil Stalker: gain Strength after each unblocked attack command
    Stock, // Axebot: respawn with one fewer stock when killed
    HardenedShell, // Skulking Colony: cannot lose more than N HP each turn
    Hatch, // Tough Egg: countdown marker before the hatch move
    HighVoltage, // Zapbot: gain Strength at the end of each enemy turn
    Territorial, // Byrdonis: gain Strength at the end of each enemy turn
    PersonalHive, // Entomancer: add Dazed to draw when hit by powered attacks
    Galvanic, // Globe Head: Power cards damage the player
    Rampart, // Living Shield: block Turret Operators at player turn start
    CurlUp, // Louse Progenitor: block once after taking powered card damage
    Infested, // Phrog Parasite: spawn Wrigglers when killed

    /// <summary>
    /// `BattlewornDummyTimeLimitPower`, on the Battleworn Dummy's Battle Friend: a counter
    /// that decrements at the end of every one of its own side turns, and at 1 flags the
    /// encounter as RAN OUT OF TIME and makes the dummy ESCAPE. Three turns to kill it,
    /// and failing costs the event's reward rather than the run.
    /// </summary>
    BattlewornDummyTimeLimit,
    PainfulStabs, // Test Subject: every hit that lands unblocked adds a Wound
    Nemesis, // Test Subject phase 3: toggles Intangible at the end of every enemy turn
    Soar, // Owl Magistrate: halves powered attack damage against it while it flies
    VitalSpark, // Infested Prism: playing a Skill taints the player for the round
    Tainted, // the player: powered attacks land for this much more, until the enemy turn ends
    PossessSpeed, // The Forgotten: how much Dexterity it has stolen, returned when it dies
    Accelerant, // the player: enemy Poison triggers this many EXTRA times each turn
    CorrosiveWave, // the player: every card DRAWN poisons all enemies; lasts one turn
    Outbreak, // the player: every third Poison applied damages all enemies for this much
    OutbreakCounter, // how many of those three have been applied so far
    Burst, // the player: the next N SKILLS are played twice
    MasterPlanner, // the player: every Skill played becomes Sly for the rest of the combat
    WraithForm, // the player: lose this much Dexterity at the start of every turn
    Speedster, // the player: a card drawn MID-TURN damages all enemies for this much
    Tracking, // the player: card attacks against a Weak target are multiplied by this
    PhantomBlades, // the player: Shivs Retain, and the FIRST Shiv each turn hits for this much more
    SerpentForm, // the player: every card played damages a random enemy for this much
    TheHunt, // the player: a marker that The Hunt landed a kill; the reward is the effect
    TemporaryDexterity, // the player: Dexterity that is handed back at the end of the turn
    Blur, // the player: block survives this many more turn starts, then stops
    FanOfKnives, // the player: every Shiv targets ALL enemies rather than one
    Tangled, // Vine Shambler card debuff, currently tracked as a player debuff
    Constrict, // Slithering Strangler pressure debuff, currently tracked
    Smoggy, // Living Fog card affliction debuff, currently tracked
    Illusion, // Fogmog summon marker
    Minion, // Living Fog Gas Bomb marker
    PaperCuts, // Scroll of Biting max-HP chip at end of enemy action
    Hex,
    Dampen,
    Adaptable,
    Asleep,
    ChainsOfBinding,
    Ebb,
    Enrage,
    Intangible,
    Plow,
    Sandpit,
    SteamEruption,
    Disintegration,
    FreeAttackPower, // player: next N Attacks cost 0; decrements on each Attack played
    FreeSkillPower, // player: next N Skills cost 0; decrements on each Skill played
    Doom,

    /// <summary>
    /// An illusion that has been killed and is spending its next turn coming back.
    /// <c>IllusionPower.AfterDeath</c> forces a REVIVE_MOVE with
    /// <c>MustPerformOnceBeforeTransitioning</c>, so the turn is spent healing rather
    /// than acting, and <c>ShouldAllowHitting</c> is false for its owner meanwhile.
    /// </summary>
    Reviving,

    /// <summary>
    /// The Slumbering Beetle's sleep, counted DOWN by two different things.
    /// </summary>
    /// <remarks>
    /// <c>SlumberPower</c> decrements on every enemy-side turn end AND on every instance
    /// of UNBLOCKED damage its owner takes; at zero the beetle is stunned awake into
    /// ROLL_OUT. Modelled as three quiet turns alone, a beetle the player was hitting
    /// woke on schedule here and early in the game — and hitting it is the obvious play,
    /// since it sleeps behind Plating. Appended, like every id here: these ordinals
    /// reach the observation.
    /// </remarks>
    Slumber,

    /// <summary>
    /// A Decimillipede segment's <c>ReattachPower</c>: how much it heals when it comes
    /// back, and the marker that it comes back at all.
    /// </summary>
    /// <remarks>
    /// A dead segment spends one turn as DEAD_MOVE and then REATTACHes for its Amount —
    /// 25 — unless every OTHER segment is already dead, which is the only way the fight
    /// is won. The emulator left a killed segment dead, so the elite could be taken apart
    /// one piece at a time.
    /// </remarks>
    Reattach,

    /// <summary>
    /// The Kaiser Crab's <c>SurroundedPower</c>, on the PLAYER: which way they are facing.
    /// </summary>
    /// <remarks>
    /// Magnitude 1 is facing Right and 2 is facing Left, matching the power's own
    /// <c>Direction</c> enum, which starts at Right. An attack from the half at the
    /// player's BACK lands at 1.5x — while both halves live that is the Crusher, and the
    /// emulator had the multiplier baked into the Crusher's announced damage instead, so
    /// it never stopped when it should.
    /// </remarks>
    Surrounded,

    /// <summary>Marker: this creature attacks from the player's left. See [[Surrounded]].</summary>
    BackAttackLeft,

    /// <summary>Marker: this creature attacks from the player's right.</summary>
    BackAttackRight,

    /// <summary>
    /// <c>CrabRagePower</c>: when its partner dies, this half takes Strength 6 and 99
    /// block. Killing one side of the Kaiser Crab enrages the other, and none of that
    /// was modelled — so the boss could be halved for free.
    /// </summary>
    CrabRage,

    /// <summary>
    /// <c>MindRotPower</c>: draw this many fewer cards. One of the Knowledge Demon's
    /// curses.
    /// </summary>
    MindRot,

    /// <summary>
    /// <c>SlothPower</c>: play at most this many cards a turn. `ShouldPlay` returns false
    /// once the count is reached, so the cards are not unplayable — the turn simply
    /// stops accepting them.
    /// </summary>
    Sloth,

    /// <summary>
    /// <c>WasteAwayPower</c>: <c>ModifyMaxEnergy</c> subtracts this, so every turn starts
    /// with less.
    /// </summary>
    WasteAway,

    /// <summary>
    /// <c>BurrowedPower</c>: the Tunneler is dug in behind its block.
    /// </summary>
    /// <remarks>
    /// Three things ride on it, and the emulator had none of them. `ShouldClearBlock`
    /// returns FALSE for its owner, so a burrowed Tunneler keeps its block across turns
    /// instead of losing it at the start of each one. `AfterBlockBroken` stuns it into
    /// DIZZY_MOVE and then back to BITE_MOVE, which is the only way out of the burrow —
    /// otherwise it hits from below forever. And `AfterRemoved` takes the rest of the
    /// block with it.
    /// </remarks>
    Burrowed,

    /// <summary>
    /// <c>WellLaidPlansPower</c>: how many cards its owner may CHOOSE to keep at the end
    /// of each turn, for the rest of the combat.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="RetainHand" />, which keeps the WHOLE hand and counts
    /// down — "keep everything for one turn" and "keep one card every turn forever" are
    /// two different rules and cannot share an id, the same way Blur could not share
    /// Barricade's (E154).
    ///
    /// Appended rather than filed next to RetainHand ON PURPOSE: the observation writes
    /// `(int)buff.Id` straight into the vector, so inserting a value renumbers every buff
    /// after it and silently changes the meaning of every committed fixture.
    /// </remarks>
    WellLaidPlans,

    /// <summary>
    /// <c>LightningRodPower</c>: channels a Lightning orb at each turn's energy reset and
    /// DECREMENTS, so an amount of 2 buys two turns of orbs rather than two orbs at once.
    /// </summary>
    /// <remarks>
    /// The power's own comment explains the timing and it is not incidental: it fires at
    /// AfterEnergyReset rather than BeforeSideTurnStart "so the player will still get
    /// benefits from orbs that might be evoked to make room for the new Lightning Orb" —
    /// a Plasma evoked to make room would otherwise have its energy wiped by the reset,
    /// and a Frost's block cleared.
    /// </remarks>
    LightningRod,

    /// <summary>
    /// <c>ShadowStepPower</c>: converts itself into <see cref="DoubleDamage" /> at the
    /// start of its owner's next turn and then removes itself. Shadow Step's real payload,
    /// held for a turn.
    /// </summary>
    ShadowStep,

    /// <summary>
    /// <c>DoubleDamagePower.ModifyDamageMultiplicative</c>: a flat <c>2m</c> on a powered
    /// CARD attack by its owner — the amount is a stack count, not a multiplier, so two
    /// stacks are still double and last two turns. Decrements at the end of the owner's
    /// side turn.
    /// </summary>
    DoubleDamage,

    /// <summary>
    /// <c>ShadowmeldPower.ModifyBlockMultiplicative</c>: block its owner gains is
    /// multiplied by <c>2^Amount</c>, and the power is removed at the end of their side
    /// turn — so it doubles a single turn's block, however that block was gained.
    /// </summary>
    Shadowmeld,

    /// <summary>
    /// <c>FreePowerPower.TryModifyEnergyCostInCombatLate</c>: the next N POWER cards cost
    /// nothing. Synthesis grants it. Sibling of <see cref="FreeAttackPower" /> and
    /// <see cref="FreeSkillPower" />, and distinct from both — the emulator was granting
    /// FreeAttackPower for it, which is a different card type entirely.
    /// </summary>
    FreePowerPower,

    /// <summary>
    /// <c>IterationPower.AfterCardDrawn</c>: the FIRST Status card drawn in a turn draws
    /// this many cards. A status-shaped draw engine, not a flat next-turn draw.
    /// </summary>
    Iteration,

    /// <summary>
    /// <c>BiasedCognitionPower.AfterSideTurnStart</c>: takes this much Focus back at the
    /// start of every turn, for the rest of the combat. The drain is the card's cost, and
    /// the emulator granted the Focus without it.
    /// </summary>
    BiasedCognition,

    /// <summary>
    /// <c>BlackHolePower.AfterStarsGained</c>: damage to every enemy each time the player
    /// GAINS stars -- Unpowered, so Strength does not raise it. Appended, like every other
    /// member: <c>CombatObservation</c> writes <c>(int)buff.Id</c>, so an insertion
    /// renumbers everything after it.
    /// </summary>
    BlackHole,

    /// <summary>
    /// <c>NoEnergyGainPower.ModifyEnergyGain</c> returns 0: the owner gains no energy for
    /// the rest of the turn. The only implementer of the energy-modifier chain, and Expect
    /// A Fight's entire cost -- the card was granting its energy and never applying it.
    /// Removed <c>AfterSideTurnEnd</c>, so it lasts the turn it was played and no longer.
    /// </summary>
    NoEnergyGain,

    /// <summary>
    /// <c>OrbitPower.AfterEnergySpent</c>: every FOUR energy spent over the combat pays
    /// this much energy back. The count is cumulative and does not reset per turn, and it
    /// starts when the power lands -- energy spent before Orbit was played does not count.
    /// </summary>
    Orbit,

    /// <summary>
    /// <c>NecroMasteryPower.AfterCurrentHpChanged</c>: when OSTY loses HP, every enemy
    /// takes that loss times this amount, <c>Unblockable | Unpowered</c>. It reads the
    /// pet's HP change, not the player's, which is why Osty had to become something that
    /// can be damaged before the power could exist at all.
    /// </summary>
    NecroMastery,

    /// <summary>
    /// <c>StranglePower</c>, a DEBUFF on an enemy: it takes this much unblockable,
    /// unpowered damage every time the player plays a card, until its own side turn ends.
    /// Nothing like the Vulnerable that used to stand in for it.
    ///
    /// The game snapshots the amount in <c>BeforeCardPlayed</c> and pays it in
    /// <c>AfterCardPlayed</c>, which is how a Strangle does not trigger on the very card
    /// that applied or stacked it.
    /// </summary>
    Strangle,

    /// <summary>
    /// <c>SicEmPower</c>, a DEBUFF on an enemy: when OSTY damages it, Osty is summoned for
    /// this amount — which on a living pet is `GainMaxHp`, so the pet GROWS. Removed when
    /// the enemy's own side turn ends, like Strangle.
    ///
    /// The emulator used to give the PLAYER Strength for this card: wrong target, wrong
    /// effect, wrong number.
    /// </summary>
    SicEm,

    /// <summary>
    /// <c>VeilpiercerPower.TryModifyEnergyCostInCombatLate</c>: ETHEREAL cards cost
    /// nothing, and <c>BeforeCardPlayed</c> decrements one stack per Ethereal played. A
    /// keyword-scoped cousin of <see cref="FreeAttackPower" /> and friends, which are
    /// scoped by card TYPE -- so an Ethereal Attack consumes this, not FreeAttackPower.
    /// </summary>
    Veilpiercer,

    /// <summary>
    /// <c>HangPower</c>, a DEBUFF on an enemy: it multiplies damage aimed at its owner by
    /// its own AMOUNT, but only when the card doing the damage is Hang itself. Hang tops
    /// it up by <c>Math.Max(2, amount)</c> AFTER dealing its damage, so the counter runs
    /// 2, 4, 8, 16 and each Hang lands at the previous stack's multiple.
    ///
    /// The card-source gate is why this cannot live in <c>BuffSystem.IncomingDamage</c>
    /// with Tracking and Double Damage: that function cannot see what card is attacking,
    /// and every other attack must be left alone.
    /// </summary>
    Hang,

    /// <summary>
    /// <c>SleightOfFleshPower.AfterPowerAmountChanged</c>: whenever the player lands a
    /// non-temporary DEBUFF on an enemy, that enemy takes this much Unpowered damage.
    /// A debuff engine, not a stat.
    /// </summary>
    SleightOfFlesh,

    /// <summary>
    /// <c>DemesnePower</c>: <c>ModifyHandDraw</c> AND <c>ModifyMaxEnergy</c>, both by its
    /// amount, for the rest of the combat. The emulator granted a one-shot NextTurnEnergy
    /// and NextTurnDraw instead -- a single turn of a permanent effect.
    /// </summary>
    Demesne,

    /// <summary>
    /// <c>OblivionPower</c>, a DEBUFF on an enemy: every card its applier plays gives that
    /// enemy this much Doom. It records the amount in <c>BeforeCardPlayed</c> and pays out
    /// in <c>AfterCardPlayed</c>, which is how it avoids triggering on the card that
    /// applied it, and it is removed when the player's side turn ends.
    /// </summary>
    Oblivion,

    /// <summary>
    /// <c>PagestormPower.AfterCardDrawn</c>: drawing an ETHEREAL card draws this many more.
    /// </summary>
    Pagestorm,

    /// <summary>
    /// <c>ReaperFormPower.AfterDamageGiven</c>: a POWERED attack by the player or their pet
    /// Dooms the target for <c>TotalDamage * Amount</c> — and TotalDamage is blocked plus
    /// unblocked, so an attack into a full shield still Dooms for all of it.
    /// </summary>
    ReaperForm,

    /// <summary>
    /// <c>SentryModePower.BeforeHandDraw</c>: puts this many Sweeping Gazes into HAND at
    /// the start of every turn, before the hand is drawn.
    /// </summary>
    SentryMode,

    /// <summary>
    /// <c>ShroudPower.AfterPowerAmountChanged</c>: gain this much Unpowered block whenever
    /// its owner applies DOOM to anyone. Pairs with <see cref="ReaperForm" />, which turns
    /// every attack into a Doom.
    /// </summary>
    Shroud,

    /// <summary>
    /// <c>CountdownPower.AfterSideTurnStart</c>: Dooms ONE random hittable enemy for this
    /// much at the start of every player turn, rolled on the CombatTargets stream.
    /// </summary>
    Countdown,

    /// <summary>
    /// <c>DevourLifePower.AfterCardPlayed</c>: playing a SOUL summons Osty for this much.
    /// </summary>
    DevourLife,

    /// <summary>
    /// <c>ForbiddenGrimoirePower.AfterCombatEnd</c>: adds this many extra card-REMOVAL
    /// rewards to the fight's rewards. The emulator has no removal reward to add, so the
    /// power is tracked and its payout is not modelled — see the catalog.
    /// </summary>
    ForbiddenGrimoire,

    /// <summary>
    /// <c>LethalityPower.ModifyDamageMultiplicative</c>: the FIRST Attack card played each
    /// turn hits for <c>1 + Amount/100</c>. Not every attack — the power counts the turn's
    /// Attack plays and pays out only while that count is still one.
    /// </summary>
    Lethality,

    /// <summary>
    /// <c>SpiritOfAshPower.BeforeCardPlayed</c>: playing an ETHEREAL card gains this much
    /// Unpowered block. The var is named BlockOnExhaust and the hook is not about
    /// exhausting at all.
    /// </summary>
    SpiritOfAsh,

    /// <summary>
    /// <c>CallOfTheVoidPower.BeforeHandDraw</c>: puts this many cards from the character's
    /// own pool into HAND at the start of every turn, each granted ETHEREAL.
    /// </summary>
    CallOfTheVoid,

    /// <summary>
    /// <c>DanseMacabrePower.BeforeCardPlayed</c>: gain this much Unpowered block whenever
    /// its owner plays a card whose RESOLVED cost is 2 or more. Resolved, so a card made
    /// free does not pay.
    /// </summary>
    DanseMacabre,

    /// <summary>
    /// <c>HauntPower.AfterCardPlayed</c>: playing a SOUL deals this much Unblockable,
    /// Unpowered damage to one random enemy.
    /// </summary>
    Haunt,

    /// <summary>
    /// <c>CalcifyPower.ModifyDamageAdditive</c>: adds this much to a POWERED attack whose
    /// dealer is OSTY. The player's own attacks get nothing.
    /// </summary>
    Calcify,

    /// <summary>
    /// <c>DebilitatePower</c>, a DEBUFF on an enemy that doubles the two multipliers it
    /// touches: Vulnerable against it goes from 1.5x to 2x
    /// (<c>amount + (amount - 1)</c>), and Weak on it goes from 0.75x to 0.5x
    /// (<c>amount - (1 - amount)</c>). The AMOUNT is a duration, not a scale — it
    /// decrements at its owner's side-turn end and the doubling never varies.
    /// </summary>
    Debilitate,

    /// <summary>
    /// <c>FriendshipPower.ModifyMaxEnergy</c>: this much more energy every turn for the
    /// rest of the combat. Friendship pays for it in Strength — it applies StrengthPower at
    /// a NEGATIVE amount.
    /// </summary>
    Friendship,

    /// <summary>
    /// <c>SummonNextTurnPower.AfterPlayerTurnStart</c>: summons Osty for this much at the
    /// start of the next player turn and then removes itself.
    /// </summary>
    SummonNextTurn,

    /// <summary>
    /// <c>NeurosurgePower.AfterSideTurnStart</c>, a DEBUFF the card puts on its own owner:
    /// it Dooms the PLAYER for this much at the start of every player turn. Doom kills its
    /// owner when their HP is at or below it, and the player is not exempt.
    /// </summary>
    Neurosurge,

    /// <summary>
    /// <c>BorrowedTimePower.TryModifyEnergyCostInCombat</c>: every card its owner plays
    /// costs this much MORE, and the power removes itself when their side turn ends. Not a
    /// Late hook, so the cards that are made free stay free.
    /// </summary>
    BorrowedTime,

    /// <summary>
    /// <c>ArsenalPower.AfterCardGeneratedForCombat</c>: every card its owner GENERATES
    /// gives them this much Strength. Any generated card, not only a Status.
    /// </summary>
    Arsenal,

    /// <summary>
    /// <c>ChildOfTheStarsPower.AfterStarsSpent</c>: this much Unpowered block PER STAR the
    /// owner spends. The hook fires from <c>CardModel.SpendStars</c> alone — paying a
    /// card's star cost — and not from every way stars can leave the counter.
    /// </summary>
    ChildOfTheStars,

    /// <summary>
    /// <c>SeekingEdgePower</c>. Its own summary says it "doesn't actually do anything on
    /// its own": the SOVEREIGN BLADE reads it and hits every enemy instead of one.
    /// </summary>
    SeekingEdge,

    /// <summary>
    /// <c>ParryPower</c>, the same shape: inert by itself, and the Sovereign Blade gains
    /// this much block after its attack — <c>CalculationBase 0 + Extra 1</c> per point.
    /// </summary>
    Parry,

    /// <summary>
    /// <c>ConquerorPower</c>, a DEBUFF on an enemy: a powered attack from a SOVEREIGN BLADE
    /// against it lands at DOUBLE — <c>cardSource is SovereignBlade</c> and nothing else.
    /// Decrements when its owner's side turn ends, so the amount is a turn count.
    /// </summary>
    Conqueror,

    /// <summary>
    /// <c>StarNextTurnPower.AfterEnergyReset</c>: gain this many stars at the start of the
    /// next turn, then remove itself. The star twin of <see cref="NextTurnEnergy" />.
    /// </summary>
    StarNextTurn,

    /// <summary>
    /// <c>ForegoneConclusionPower.BeforeHandDraw</c>: this many cards CHOSEN from the draw
    /// pile go to hand before the next draw, then the power removes itself.
    /// </summary>
    ForegoneConclusion,

    /// <summary>
    /// <c>FurnacePower.AfterSideTurnStart</c>: a Forge of this much at the start of every
    /// player turn.
    /// </summary>
    Furnace,

    /// <summary>
    /// <c>GenesisPower.AfterEnergyReset</c>: this many stars at the start of every turn,
    /// for the rest of the combat. Unlike <see cref="StarNextTurn" /> it does not remove
    /// itself.
    /// </summary>
    Genesis,

    /// <summary>
    /// <c>HammerTimePower.AfterForge</c>: forges the same amount for every OTHER player.
    /// In a solo run there are none, so the power does nothing at all — it is tracked only
    /// because the game reports it and a capture compares the whole power set.
    /// </summary>
    HammerTime,

    /// <summary>
    /// <c>MonarchsGazePower.AfterDamageGiven</c>: every POWERED attack its owner lands takes
    /// this much temporary Strength off the TARGET.
    /// </summary>
    MonarchsGaze,

    /// <summary>
    /// <c>MonologuePower</c>: this much Strength for every card its owner plays, recorded in
    /// <c>BeforeCardPlayed</c> and paid in <c>AfterCardPlayed</c> so the card that applied
    /// it does not pay. At the owner's side-turn end the power removes itself and takes
    /// back everything it gave.
    /// </summary>
    Monologue,

    /// <summary>
    /// What <see cref="Monologue" /> has handed out so far, which is the number the game
    /// SHOWS for that power (`DisplayAmount => StrengthApplied`) and the amount it takes
    /// back at the end of the turn. The Outbreak/OutbreakCounter shape again.
    /// </summary>
    MonologueApplied,

    /// <summary>
    /// <c>PaleBlueDotPower.ModifyHandDraw</c>: draw this many more, but only when the player
    /// finished at least five card plays LAST turn — a threshold on the previous turn, not
    /// a running total.
    /// </summary>
    PaleBlueDot,

    /// <summary>
    /// <c>PillarOfCreationPower.AfterCardGeneratedForCombat</c>: this much Unpowered block
    /// for every card its owner GENERATES. The same hook Arsenal pays Strength from.
    /// </summary>
    PillarOfCreation,

    /// <summary>
    /// <c>ReflectPower.AfterDamageReceived</c>: a POWERED attack on its owner whose damage
    /// was BLOCKED sends that blocked amount back at the dealer as Unpowered damage. It
    /// decrements at its owner's side-turn START, so a Reflect covers the enemies' turn and
    /// is gone by the player's next one.
    /// </summary>
    Reflect,

    /// <summary>
    /// <c>RoyaltiesPower.AfterCombatEnd</c>: this much extra GOLD as its own reward row.
    /// </summary>
    Royalties,

    /// <summary>
    /// <c>SpectrumShiftPower.BeforeHandDraw</c>: this many distinct COLOURLESS cards into
    /// hand at the start of every turn, rolled on the card-generation stream.
    /// </summary>
    SpectrumShift,

    /// <summary>
    /// <c>SwordSagePower</c>: every SOVEREIGN BLADE the player holds gains this many
    /// REPLAYS — applied to the blades that exist when it lands and to any that enter
    /// combat afterwards, and taken back if the power is removed.
    /// </summary>
    SwordSage,

    /// <summary>
    /// <c>TyrannyPower</c>: draw this much more every turn, and EXHAUST that many cards
    /// CHOSEN from hand at the start of it. The draw is the price and the exhaust is the
    /// cost, both every turn.
    /// </summary>
    Tyranny,

    /// <summary>
    /// <c>TheSealedThronePower.BeforeCardPlayed</c>: a star for every card its owner plays.
    /// </summary>
    TheSealedThrone,

    /// <summary>
    /// <c>VoidFormPower</c>: the first this-many cards played each turn cost NOTHING — both
    /// <c>TryModifyEnergyCostInCombatLate</c> and <c>TryModifyStarCost</c> return zero until
    /// the count is spent. Auto-plays do not count towards it.
    /// </summary>
    VoidForm,
}

public record struct BuffState(BuffId Id, int Magnitude);
