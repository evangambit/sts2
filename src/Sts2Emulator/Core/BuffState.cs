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
    FranticEscapePlayedCount, // player: track plays to increase cost
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
    PainfulStabs, // Test Subject phase 2 marker
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
}

public record struct BuffState(BuffId Id, int Magnitude);
