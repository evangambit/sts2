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
    public int[] PotionSlots = new int[3];
    public int CurrentNodeType;
    public int[] NeowOptions = new int[3];
    public int[] RewardCards = new int[3];
    public int RewardGold;
    public int RewardPotion;
    public bool RewardCardPending;
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

    public RunMapNode(int col, int row)
    {
        Col = col;
        Row = row;
    }
}
