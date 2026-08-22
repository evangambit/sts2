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
    public int[] NormalEncounterSequence = [];
    public int[] EliteEncounterSequence = [];
    public int BossEncounterId;
    public int NormalEncountersVisited;
    public int EliteEncountersVisited;
    public int[] EventSequence = [];
    public int EventSequenceIndex;

    /// <summary>
    /// The run's relic queues, shuffled once at run start. Every relic reward pulls from
    /// the player's; the shared one exists so that a relic pulled by one player is gone
    /// for the others, and is kept because populating it consumes 112 UpFront draws that
    /// sit between the seed and everything else in the run.
    /// </summary>
    public RelicGrabBag RelicBag = new();
    public RelicGrabBag SharedRelicBag = new(refreshAllowed: true);

    public int WingedBootsTimesUsed;
    public double CardRarityOffset;
    public double PotionRewardOdds = 0.4;
    public bool PendingRelicReward;
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
