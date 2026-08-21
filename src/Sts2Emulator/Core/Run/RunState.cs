using Sts2Emulator.Core.Rng;

namespace Sts2Emulator.Core.Run;

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
    /// Potion rewards still owed by a rest site (Tiny Mailbox offers two). The reward
    /// screen carries one potion at a time, so the second is queued here and offered once
    /// the first is claimed.
    /// </summary>
    public int PendingRestPotions;
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
    public int WingedBootsTimesUsed;
    public double CardRarityOffset;
    public double PotionRewardOdds = 0.4;
    public bool PendingRelicReward;
    public int ShopRemovalsUsed;
    public int? TransformSelectedDeckIndex;
    public int PendingSelfHelpBookEnchantType;
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
