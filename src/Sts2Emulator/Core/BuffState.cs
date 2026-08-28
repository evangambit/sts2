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
}

public record struct BuffState(BuffId Id, int Magnitude);
