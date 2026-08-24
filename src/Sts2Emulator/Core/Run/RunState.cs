using Sts2Emulator.Core.Rng;

namespace Sts2Emulator.Core.Run;

public enum DeckSelection
{
    None = 0,

    /// <summary>Apply <c>PendingSelectionArg</c> as an <see cref="Enchantment"/>.</summary>
    Enchant,

    /// <summary>Transform the chosen card into the card id in <c>PendingSelectionArg</c>.</summary>
    TransformTo,

    /// <summary>Upgrade the chosen card.</summary>
    Upgrade,

    /// <summary>Transform the chosen card into a rolled one (CardCmd.TransformToRandom).</summary>
    TransformToRandom,

    /// <summary>Remove the chosen card from the deck.</summary>
    Remove,
}

/// <summary>One act's generated rooms, as <c>ActModel._rooms</c> holds them.</summary>
public sealed record ActRooms(
    int Act,
    int[] Events,
    int[] NormalEncounters,
    int[] EliteEncounters,
    int BossEncounterId
);

public sealed class RunState
{
    public string StringSeed = "";
    public RunRngSet Rng = new("0");
    public PlayerRngSet PlayerRng = new(new RunRngSet("0"));
    public int PlayerHp;
    public int PlayerMaxHp;
    public int Gold;
    public int Floor;
    public int Act;
    public RunPhase Phase;
    public List<CardInstance> Deck = [];
    public List<RelicInstance> Relics = [];

    /// <summary>
    /// Relics spent for the rest of the run (the game's RelicModel.IsUsedUp). A combat
    /// rebuilds its relic list from ids, so anything one-shot per run has to be carried
    /// across the boundary rather than living on the combat's RelicInstance.
    /// </summary>
    public List<int> UsedUpRelics = [];

    /// <summary>
    /// Potion rewards still owed. The reward screen carries one potion at a time, so
    /// anything that offers several -- Tiny Mailbox's two, the Potion Courier's three
    /// Foul Potions, Whispering Hollow's two random ones -- queues the rest here and
    /// offers them as the screen frees up.
    ///
    /// A 0 means "roll one when it reaches the screen", which is what
    /// <c>PotionReward(player)</c> does: it carries no potion and rolls in
    /// <c>Populate</c>. A non-zero entry is a potion the event named outright.
    /// </summary>
    public List<int> PendingPotionRewards = [];
    public int[] PotionSlots = new int[3];

    /// <summary>
    /// How many of those slots the run may actually fill.
    /// </summary>
    /// <remarks>
    /// <c>Player.CreateForNewRun</c> passes a literal 3 and
    /// <c>Player.initialMaxPotionSlotCount</c> is 3, but every live capture at A8 reports
    /// <c>max_potion_slots: 2</c> -- the emulator models A8, so 2 is the base here and the
    /// decompiled constant is the un-ascended one. Phial Holster's
    /// <c>GainMaxPotionCount(1)</c> is what moves it, and a capture holding three potions
    /// is what proved it moves at all.
    /// </remarks>
    public int MaxPotionSlots = 2;
    public int CurrentNodeType;
    public int[] NeowOptions = new int[3];
    public int[] RewardCards = new int[3];
    public int RewardGold;
    public int RewardPotion;
    public bool RewardCardPending;

    /// <summary>
    /// Card rewards still owed from Kaleidoscope, each drawn from other characters'
    /// pools. The relic offers two at once and the player answers them one after the
    /// other, so the second has to survive the first being resolved.
    /// </summary>
    public int PendingOtherCharacterCardRewards;

    /// <summary>
    /// Neow is still on screen with nothing left but "Proceed". The game returns to the
    /// ancient after its rewards are answered and waits for one more input before the
    /// map; going straight to the map skips a decision the player actually makes.
    /// </summary>
    public bool NeowAwaitingProceed;

    /// <summary>
    /// An EVENT opened the reward screen currently up, so answering it returns to the
    /// event's result page rather than to the map.
    /// </summary>
    /// <remarks>
    /// Every event that hands out rewards does it the same way: <c>await
    /// RewardsCmd.OfferCustom(...)</c> and then <c>SetEventFinished(...)</c> on the line
    /// below, so the result page is shown once the screen is answered. Neow already had
    /// this and events did not, which cost the run one Proceed every time.
    /// </remarks>
    public bool EventAwaitingProceed;
    public bool ReturnToRewardScreenAfterCardReward;
    public int[] MapNodeTypes = new int[RunConstants.MapChoices];
    public int[] MapChoices = new int[RunConstants.MapChoices];
    public int[] ShopCards = new int[7];
    public int[] ShopRelics = new int[3];
    public int[] ShopPotions = new int[3];
    public int[] ShopCosts = new int[14];
    public bool[] RewardUpgraded = new bool[3];
    public int RelicReward;
    public int EventId;
    public int? EventValue0;
    public int? EventValue1;

    /// <summary>
    /// The Crystal Sphere's board, while one is open. It carries its own copy of the
    /// event's stream because the minigame keeps drawing from it -- the board is laid out
    /// on entry and the rewards roll off the same stream when the last divination is
    /// spent, so a fresh Rng seeded from the event id would replay the placement draws.
    /// </summary>
    public CrystalSphereGame? CrystalSphere;
    public GameRng? CrystalSphereRng;

    /// <summary>
    /// The current event's own Rng stream, and the entry it was seeded from.
    ///
    /// <para>
    /// The game gives an event ONE <c>base.Rng</c> for its lifetime, so a second draw
    /// continues where the first left off. This was rebuilt per call instead, which put
    /// every draw at position 0 -- each value plausible on its own, and wrong the moment
    /// an event drew twice.
    /// </para>
    /// </summary>
    public GameRng? EventRngStream;

    public string? EventRngName;

    /// <summary>The Relic Trader's three-relic shelf, drawn once when the event opens.</summary>
    public List<int>? EventRelicStock;

    /// <summary>
    /// Gold rewards still owed. The reward screen carries one pile at a time, the way it
    /// carries one potion at a time, so anything that offers several -- the Crystal
    /// Sphere can uncover seven -- queues the rest here.
    ///
    /// The game shows them all at once and lets the player claim them in any order; the
    /// queue claims them one at a time instead. The gold that ends up in the purse is the
    /// same either way, which is why the difference is left to stand: only the action
    /// index moves.
    /// </summary>
    public List<int> PendingGoldRewards = [];

    /// <summary>
    /// Card offers still owed, each already rolled: three card ids the player will choose
    /// one of. The Crystal Sphere can uncover a card reward of each rarity and the screen
    /// shows one offer at a time, so the rest wait here.
    ///
    /// Rolled on the way in rather than on the way out because that is when the game rolls
    /// them: RewardsSet populates every reward as the screen opens, in the order the
    /// rewards were listed, and those draws all come off the same stream.
    /// </summary>
    public List<int[]> PendingCardOffers = [];
    public CombatState? ActiveCombat;
    public CountingRandom? ActiveCombatRng;
    public bool LastPlayerWon;
    public int CompletedCombatRoomsBeforeCurrent;
    public Dictionary<(int Col, int Row), RunMapNode> MapNodes = [];
    public (int Col, int Row) CurrentMapCoord;
    public (int Col, int Row)?[] MapOptionCoords = new (int Col, int Row)?[RunConstants.MapChoices];

    /// <summary>
    /// The rooms generated for the acts this run has not reached yet.
    /// </summary>
    /// <remarks>
    /// The game rolls every act's rooms at run start, off one UpFront stream, in index
    /// order — so act 2's encounters were decided before the player left Neow. Keeping
    /// them is what lets the act transition install them rather than generate from a
    /// stream that has moved on.
    /// </remarks>
    public List<ActRooms> LaterActRooms = [];

    public int[] NormalEncounterSequence = [];
    public int[] EliteEncounterSequence = [];
    public int BossEncounterId;
    public int NormalEncountersVisited;
    public int EliteEncountersVisited;
    public int[] EventSequence = [];
    public int EventSequenceIndex;

    /// <summary>
    /// <c>RunState.VisitedEventIds</c>: an event the run has already seen is skipped
    /// rather than offered twice, however the sequence wraps.
    /// </summary>
    public List<int> VisitedEventIds = [];

    /// <summary>
    /// The event entry a pending deck selection belongs to, or null when it does not
    /// come from an event. Every event passes its OWN Rng to CardCmd.TransformToRandom;
    /// only NewLeaf uses Rng.Niche.
    /// </summary>
    public string? PendingSelectionEventEntry;

    /// <summary>
    /// The run's relic queues, shuffled once at run start. Every relic reward pulls from
    /// the player's; the shared one exists so that a relic pulled by one player is gone
    /// for the others, and is kept because populating it consumes 112 UpFront draws that
    /// sit between the seed and everything else in the run.
    /// </summary>
    public RelicGrabBag RelicBag = new();
    public RelicGrabBag SharedRelicBag = new(refreshAllowed: true);

    public double CardRarityOffset;
    public double PotionRewardOdds = 0.4;
    public bool PendingRelicReward;

    /// <summary>
    /// Relic rewards still owed, each already rolled. The screen carries one at a time,
    /// the way it carries one potion at a time, so anything past the first waits here.
    /// </summary>
    public List<int> PendingBonusRelicRewards = [];

    /// <summary>
    /// Neow's Bones adds its curse only once its two relics have been claimed:
    /// <c>AfterObtained</c> awaits the RewardsSet's <c>Offer()</c> and adds the curse on
    /// the line after. Rolling it up front would be the same two streams in the same order
    /// -- the relics come off Rewards and the curse off Niche -- but only until a claimed
    /// relic's own pickup draws from Niche, which several of the candidates do.
    /// </summary>
    public bool PendingNeowsBonesCurse;

    /// <summary>
    /// Hefty Tablet's Injury, which arrives WITH the card its screen offers rather than
    /// before it: <c>AfterObtained</c> awaits the choice, then adds a list holding the
    /// Injury with the chosen card inserted at its front. A live capture shows both land
    /// in the same snapshot.
    /// </summary>
    public bool PendingHeftyTabletCurse;
    public int ShopRemovalsUsed;
    public int? TransformSelectedDeckIndex;

    /// <summary>
    /// A deck selection an event has opened and is waiting on: what is being done to the
    /// chosen cards, the enchantment or target card it is being done with, and how many
    /// are still to pick. Half the Act 1 events end in one of these -- enchant this,
    /// transform that -- and each used to need its own flag; Self-Help Book's was the
    /// only one, so every other event silently resolved without ever asking.
    /// </summary>
    public DeckSelection PendingSelectionKind;
    public int PendingSelectionArg;
    public int PendingSelectionCount;

    /// <summary>
    /// Cards an event has ROLLED and is offering, and how many of them the player still
    /// gets to keep. Distinct from the selection above, which picks out of the deck the
    /// player already has: Brain Leech rolls five cards and hands over one, Room Full of
    /// Cheese rolls eight and hands over two, so the grid is the source rather than the
    /// deck. A picked card leaves the offer, the way it leaves the game's grid.
    /// </summary>
    public int[] PendingOfferCards = [];

    /// <summary>
    /// Scroll Boxes' two bundles, three card ids each, flat: bundle 0 then bundle 1.
    /// Empty when no bundle screen is up.
    /// </summary>
    public int[] BundleOffer = [];

    /// <summary>The bundle the player has highlighted, or -1 for none yet.</summary>
    public int SelectedBundle = -1;
    public int PendingOfferPicks;

    /// <summary>
    /// Which page of a multi-page event is showing; 0 is the one it opens on. Most
    /// events answer a choice by finishing, but a few answer it with a fresh page of
    /// their own options -- Punch Off's "I Can Take Them" leads to a page whose only
    /// option is the fight itself.
    /// </summary>
    public int EventPage;

    /// <summary>
    /// A card the event adds once the selection above finishes, and how many copies.
    /// Three events pay for a removal with a curse -- <c>RemoveFromDeck(cards)</c> and
    /// then <c>AddCurseToDeck&lt;T&gt;</c> -- so the curse has to survive the screen.
    /// </summary>
    public int PendingSelectionFollowUpCard;
    public int PendingSelectionFollowUpCount;

    /// <summary>
    /// HP the event takes once the selection finishes. Whispering Hollow's Hug transforms
    /// the chosen card FIRST and only then charges for it, so a capture taken while the
    /// selector is open shows the player still at full health.
    /// </summary>
    public int PendingSelectionFollowUpHpLoss;

    /// <summary>
    /// The event that opened this selection is not finished by it. The Endless Conveyor's
    /// Jelly Liver transforms a card and then the belt turns and offers the next dish, so
    /// resolving the selection has to return to the event rather than end it.
    /// </summary>
    public bool PendingSelectionReturnsToEvent;
    public bool PendingRestUpgrade;
    public bool RestResultPending;
    public int UnknownMapPointsVisited;
    public double UnknownMapPointMonsterOdds = 0.1;
    public double UnknownMapPointEliteOdds = -1.0;
    public double UnknownMapPointTreasureOdds = 0.02;
    public double UnknownMapPointShopOdds = 0.03;
    public int LastResolvedRoomType = RunConstants.NodeNone;
}

public sealed class RunMapNode
{
    public int Col { get; set; }
    public int Row { get; set; }
    public int NodeType { get; set; }
    public List<(int Col, int Row)> Children { get; } = [];
    public List<(int Col, int Row)> Parents { get; } = [];
    public int EncounterId { get; set; }

    /// <summary>
    /// The game's <c>MapPoint.CanBeModified</c>. False for the rows whose type is
    /// forced during map generation (row 1, the treasure row, the final rest row);
    /// those points are excluded from post-prune type repair.
    /// </summary>
    public bool CanBeModified { get; set; } = true;

    public RunMapNode(int col, int row)
    {
        Col = col;
        Row = row;
    }
}
