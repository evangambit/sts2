using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Sts2Emulator.Core.Run;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// The shared pool's commons and uncommons, read against
// MegaCrit.Sts2.Core.Models.Relics/*.cs. None of these were modelled at all: the reward
// code could hand every one of them over and nothing would happen.

public class CandelabraTests
{
    /// <summary>Turn TWO only — `TurnNumber == 2`, not "from turn two onwards".</summary>
    [Fact]
    public void TwoEnergyOnTheSecondTurnOnly()
    {
        var fight = Fight.WithRelics(RelicEffects.Candelabra);
        fight.State.PlayerHp = 999;
        int baseline = fight.State.MaxEnergy;
        Assert.Equal(baseline, fight.State.Energy);

        fight.EndTurn();
        Assert.Equal(baseline + 2, fight.State.Energy);

        fight.EndTurn();
        Assert.Equal(baseline, fight.State.Energy);
    }
}

public class MercuryHourglassTests
{
    [Fact]
    public void ThreeUnpoweredDamageToEveryEnemyEachTurn()
    {
        var fight = Fight.WithRelics(RelicEffects.MercuryHourglass);
        fight.State.PlayerHp = 999;
        var before = fight.State.Enemies.Select(e => e.Hp).ToList();

        fight.EndTurn();

        Assert.All(
            fight.State.Enemies.Select((e, i) => (e, i)),
            pair => Assert.True(pair.e.Hp <= before[pair.i] - 3)
        );
    }
}

public class MiniatureCannonTests
{
    /// <summary>Three more damage from an UPGRADED attack card, and nothing from a plain one.</summary>
    [Theory]
    [InlineData(false, 6)]
    [InlineData(true, 9 + 3)]
    public void UpgradedAttacksHitHarder(bool upgraded, int damage)
    {
        var fight = Fight.WithRelics(RelicEffects.MiniatureCannon);
        fight.State.Hand = [Card(SI.StrikeSilent, upgraded)];
        fight.State.Energy = 3;
        int before = fight.Enemy0.Hp;

        fight.Play();

        Assert.Equal(before - damage, fight.Enemy0.Hp);
    }
}

public class StrikeDummyTests
{
    /// <summary>
    /// Three more from a card tagged `CardTag.Strike`. Tags are not extracted; unlike the
    /// Defend side the name is not an exact stand-in, and the entry slug is the closest
    /// available reading.
    /// </summary>
    [Fact]
    public void StrikeTaggedCardsHitHarderAndOthersDoNot()
    {
        var fight = Fight.WithRelics(RelicEffects.StrikeDummy);
        fight.State.Hand = [Card(SI.StrikeSilent), Card(SI.Slice)];
        fight.State.Energy = 9;

        int before = fight.Enemy0.Hp;
        fight.Play();
        Assert.Equal(before - (6 + 3), fight.Enemy0.Hp);

        before = fight.Enemy0.Hp;
        fight.Play();
        Assert.Equal(before - 6, fight.Enemy0.Hp);
    }
}

public class PenNibTests
{
    /// <summary>Every TENTH Attack played is doubled; the nine before it are not.</summary>
    [Fact]
    public void TheTenthAttackIsDoubled()
    {
        var fight = Fight.WithRelics(RelicEffects.PenNib);
        fight.State.Energy = 999;
        fight.State.Hand = [.. Enumerable.Range(0, 10).Select(_ => Card(SI.Slice))];

        for (int i = 0; i < 9; i++)
        {
            int before = fight.Enemy0.Hp;
            fight.Play();
            Assert.Equal(before - 6, fight.Enemy0.Hp);
        }

        int last = fight.Enemy0.Hp;
        fight.Play();

        Assert.Equal(last - 12, fight.Enemy0.Hp);
    }

    /// <summary>Skills do not advance the count — it is `cardPlay.Card.Type != Attack`.</summary>
    [Fact]
    public void SkillsDoNotAdvanceTheCount()
    {
        var fight = Fight.WithRelics(RelicEffects.PenNib);
        fight.State.Energy = 999;

        // Five Defends first: if they counted, the tenth ATTACK would arrive early.
        fight.State.Hand = [.. Enumerable.Range(0, 5).Select(_ => Card(SI.DefendSilent))];
        while (fight.State.Hand.Count > 0)
        {
            fight.Play(0);
        }

        fight.State.Hand = [.. Enumerable.Range(0, 9).Select(_ => Card(SI.Slice))];
        for (int i = 0; i < 9; i++)
        {
            int before = fight.Enemy0.Hp;
            fight.Play(0);
            Assert.Equal(before - 6, fight.Enemy0.Hp);
        }

        fight.State.Hand = [Card(SI.Slice)];
        int last = fight.Enemy0.Hp;
        fight.Play(0);

        Assert.Equal(last - 12, fight.Enemy0.Hp);
    }
}

public class RippleBasinTests
{
    /// <summary>Four unpowered block at end of turn, but only if NO Attack was played.</summary>
    // The relic is added to an ALREADY-BUILT fight rather than through Fight.WithRelics,
    // because that helper generates a real encounter whose enemies attack -- and an
    // attack eats the block before the assertion can read it. Ripple Basin fires at turn
    // end, so it does not need to be present when the combat is built.
    private static Fight Basin()
    {
        var fight = Fight.Hand().Energy(3).Enemy(hp: 200);
        fight.State.PlayerHp = 999;
        fight.State.Relics.Add(new RelicInstance(RelicEffects.RippleBasin));
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Barricade, 1);
        return fight;
    }

    [Fact]
    public void BlocksOnATurnWithNoAttack()
    {
        var fight = Basin();
        fight.State.Hand = [];

        fight.EndTurn();

        Assert.Equal(4, fight.State.PlayerBlock);
    }

    [Fact]
    public void AnAttackThisTurnCancelsIt()
    {
        var fight = Basin();
        fight.State.Hand = [Card(SI.Slice)];
        fight.Play();

        fight.EndTurn();

        Assert.Equal(0, fight.State.PlayerBlock);
    }
}

public class VambraceTests
{
    /// <summary>The FIRST card block of a combat is doubled, and only the first.</summary>
    [Fact]
    public void TheFirstCardBlockIsDoubled()
    {
        var fight = Fight.WithRelics(RelicEffects.Vambrace);
        fight.State.Hand = [Card(SI.DefendSilent), Card(SI.DefendSilent)];
        fight.State.Energy = 9;

        fight.Play();
        Assert.Equal(10, fight.State.PlayerBlock);

        fight.Play();
        Assert.Equal(10 + 5, fight.State.PlayerBlock);
    }

    /// <summary>
    /// A card that gains no block does not spend it — the game latches in
    /// `AfterModifyingBlockAmount`, which only runs once an amount above zero lands.
    /// </summary>
    [Fact]
    public void ACardThatGainsNoBlockDoesNotSpendIt()
    {
        var fight = Fight.WithRelics(RelicEffects.Vambrace);
        fight.State.Hand = [Card(SI.Slice), Card(SI.DefendSilent)];
        fight.State.Energy = 9;

        fight.Play(); // an attack: no block, so nothing latches
        fight.Play();

        Assert.Equal(10, fight.State.PlayerBlock);
    }
}

public class JossPaperTests
{
    /// <summary>Every FIVE cards exhausted draws one.</summary>
    [Fact]
    public void FiveExhaustsDrawACard()
    {
        var fight = Fight.WithRelics(RelicEffects.JossPaper);
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 6; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.Backstab, false));
        }

        fight.State.Hand = [];
        for (int i = 0; i < 4; i++)
        {
            CardEffects.ExhaustCard(fight.State, new CardInstance(SI.Slice, false), rng: new Random(0));
        }

        Assert.Empty(fight.State.Hand);

        CardEffects.ExhaustCard(fight.State, new CardInstance(SI.Slice, false), rng: new Random(0));

        Assert.Single(fight.State.Hand);
    }

    /// <summary>
    /// An Ethereal exhaust is banked and folded in at the END of the turn — the relic
    /// counts those separately on purpose.
    /// </summary>
    [Fact]
    public void EtherealExhaustsAreCountedAtTheEndOfTheTurn()
    {
        var fight = Fight.Hand().Energy(3).Enemy(hp: 200);
        fight.State.PlayerHp = 999;
        fight.State.Relics.Add(new RelicInstance(RelicEffects.JossPaper));
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 20; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.Backstab, false));
        }

        fight.State.Hand = [];
        for (int i = 0; i < 5; i++)
        {
            CardEffects.ExhaustCard(
                fight.State,
                new CardInstance(SI.Slice, false),
                causedByEthereal: true,
                rng: new Random(0)
            );
        }

        // Nothing yet -- they are banked.
        Assert.Empty(fight.State.Hand);
        Assert.Equal(5, fight.State.EtherealExhaustsThisTurn);

        fight.EndTurn();

        // The five land together AFTER the flush, so the card drawn survives into the new
        // hand alongside the turn's own five.
        Assert.Equal(6, fight.State.Hand.Count);
    }
}

public class ReptileTrinketTests
{
    /// <summary>
    /// Three Strength when a potion is used, and it is a `TemporaryStrengthPower` — handed
    /// back at the end of the turn.
    /// </summary>
    [Fact]
    public void APotionGivesTemporaryStrength()
    {
        var fight = Fight.WithRelics(RelicEffects.ReptileTrinket);
        fight.State.PlayerHp = 999;
        fight.State.PotionSlots[0] = 1;

        fight.Potion(0);
        Assert.Equal(3, fight.PlayerBuffAmount(BuffId.Strength));

        fight.EndTurn();

        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.Strength));
    }
}

public class PetrifiedToadTests
{
    private const int PotionShapedRock = 45;

    [Fact]
    public void AShapedRockIsProcuredBeforeEveryCombat()
    {
        var fight = Fight.WithRelics(RelicEffects.PetrifiedToad);

        Assert.Contains(PotionShapedRock, fight.State.PotionSlots.ToArray());
    }
}

public class BowlerHatTests
{
    /// <summary>
    /// Gold gained is multiplied by a `DynamicVar("GoldIncrease", 1.25m)`. A decimal
    /// multiply and then a truncation to int, so 15 becomes 18.
    /// </summary>
    [Fact]
    public void GoldGainedIsRaisedByAQuarter()
    {
        var plain = new RunState();
        Assert.Equal(100, RelicEffects.ModifyGoldGained(plain.Relics, 100));

        var hatted = new RunState { Relics = [new RelicInstance(RelicEffects.BowlerHat)] };
        Assert.Equal(125, RelicEffects.ModifyGoldGained(hatted.Relics, 100));
        Assert.Equal(18, RelicEffects.ModifyGoldGained(hatted.Relics, 15));
    }

    /// <summary>Ectoplasm zeroes gold, and zero times anything is still zero.</summary>
    [Fact]
    public void EctoplasmStillWins()
    {
        var both = new RunState
        {
            Relics =
            [
                new RelicInstance(RelicEffects.BowlerHat),
                new RelicInstance(RelicEffects.Ectoplasm),
            ],
        };

        Assert.Equal(0, RelicEffects.ModifyGoldGained(both.Relics, 100));
    }
}

public class LuckyFyshTests
{
    [Fact]
    public void EveryCardAddedToTheDeckIsFifteenGold()
    {
        var state = new RunState { Relics = [new RelicInstance(RelicEffects.LuckyFysh)] };
        int before = state.Gold;

        RunNonCombatEffects.AddCardToDeck(state, new CardInstance(SI.Slice, false));
        RunNonCombatEffects.AddCardToDeck(state, new CardInstance(SI.Slice, false));

        Assert.Equal(before + 30, state.Gold);
    }
}

public class BookOfFiveRingsTests
{
    /// <summary>
    /// `CardsAddedSinceLastTrigger` is `CardsAdded % 5`, so the heal lands on every fifth
    /// card rather than once when five is reached.
    /// </summary>
    [Fact]
    public void EveryFifthCardAddedHealsTwenty()
    {
        var state = new RunState
        {
            Relics = [new RelicInstance(RelicEffects.BookOfFiveRings)],
            PlayerHp = 10,
            PlayerMaxHp = 200,
        };

        for (int i = 0; i < 4; i++)
        {
            RunNonCombatEffects.AddCardToDeck(state, new CardInstance(SI.Slice, false));
        }

        Assert.Equal(10, state.PlayerHp);

        RunNonCombatEffects.AddCardToDeck(state, new CardInstance(SI.Slice, false));
        Assert.Equal(30, state.PlayerHp);

        for (int i = 0; i < 5; i++)
        {
            RunNonCombatEffects.AddCardToDeck(state, new CardInstance(SI.Slice, false));
        }

        Assert.Equal(50, state.PlayerHp);
    }
}

public class PotionBeltTests
{
    [Fact]
    public void TwoMorePotionSlotsOnPickup()
    {
        var state = new RunState();
        int before = state.MaxPotionSlots;

        RunNonCombatEffects.ApplyRelicPickup(state, RelicEffects.PotionBelt);

        Assert.Equal(before + 2, state.MaxPotionSlots);
    }

    /// <summary>Picking it up twice cannot pay twice — the grant is inside the not-held branch.</summary>
    [Fact]
    public void ASecondPickupPaysNothing()
    {
        var state = new RunState();
        int before = state.MaxPotionSlots;

        RunNonCombatEffects.ApplyRelicPickup(state, RelicEffects.PotionBelt);
        RunNonCombatEffects.ApplyRelicPickup(state, RelicEffects.PotionBelt);

        Assert.Equal(before + 2, state.MaxPotionSlots);
    }
}

// The five that needed the room-entered seam. `Hook.AfterRoomEntered` fires from each
// room's Enter() -- Combat, Merchant, Treasure, Event and RestSite -- and the emulator's
// map dispatch is the equivalent point.

public class EternalFeatherTests
{
    /// <summary>Three HP per FIVE cards in the deck, integer division, on entering a rest site.</summary>
    [Theory]
    [InlineData(4, 0)]
    [InlineData(5, 3)]
    [InlineData(12, 6)]
    public void HealsPerFiveCardsAtARestSite(int deckSize, int heal)
    {
        var state = new RunState
        {
            Relics = [new RelicInstance(RelicEffects.EternalFeather)],
            PlayerHp = 10,
            PlayerMaxHp = 200,
            Deck = [.. Enumerable.Range(0, deckSize).Select(_ => new CardInstance(SI.Slice, false))],
        };

        RelicEffects.ApplyAfterRoomEntered(state, isRestSite: true, cameFromUnknown: false);

        Assert.Equal(10 + heal, state.PlayerHp);
    }

    [Fact]
    public void OtherRoomsPayNothing()
    {
        var state = new RunState
        {
            Relics = [new RelicInstance(RelicEffects.EternalFeather)],
            PlayerHp = 10,
            PlayerMaxHp = 200,
            Deck = [.. Enumerable.Range(0, 20).Select(_ => new CardInstance(SI.Slice, false))],
        };

        RelicEffects.ApplyAfterRoomEntered(state, isRestSite: false, cameFromUnknown: true);

        // Only Planisphere pays on an unknown; the Feather is a rest-site relic.
        Assert.Equal(10, state.PlayerHp);
    }
}

public class PlanisphereTests
{
    /// <summary>
    /// Five HP on entering a room that came from a "?" — the relic reads the MAP POINT,
    /// not the room, so it pays out even when the "?" turned out to be a fight.
    /// </summary>
    [Fact]
    public void HealsOnAnUnknownMapPoint()
    {
        var state = new RunState
        {
            Relics = [new RelicInstance(RelicEffects.Planisphere)],
            PlayerHp = 10,
            PlayerMaxHp = 200,
        };

        RelicEffects.ApplyAfterRoomEntered(state, isRestSite: false, cameFromUnknown: true);

        Assert.Equal(15, state.PlayerHp);
    }

    [Fact]
    public void AKnownRoomPaysNothing()
    {
        var state = new RunState
        {
            Relics = [new RelicInstance(RelicEffects.Planisphere)],
            PlayerHp = 10,
            PlayerMaxHp = 200,
        };

        RelicEffects.ApplyAfterRoomEntered(state, isRestSite: true, cameFromUnknown: false);

        Assert.Equal(10, state.PlayerHp);
    }
}

public class PantographTests
{
    /// <summary>
    /// 25 HP before a BOSS combat. `AfterRoomEntered` only sets a display status; the heal
    /// is `BeforeCombatStart` gated on the room type.
    /// </summary>
    [Fact]
    public void HealsTwentyFiveBeforeABoss()
    {
        var state = new RunState
        {
            Relics = [new RelicInstance(RelicEffects.Pantograph)],
            PlayerHp = 10,
            PlayerMaxHp = 200,
        };

        RelicEffects.ApplyBeforeBossCombat(state);

        Assert.Equal(35, state.PlayerHp);
    }

    [Fact]
    public void ItCannotOverhealPastTheMaximum()
    {
        var state = new RunState
        {
            Relics = [new RelicInstance(RelicEffects.Pantograph)],
            PlayerHp = 70,
            PlayerMaxHp = 80,
        };

        RelicEffects.ApplyBeforeBossCombat(state);

        Assert.Equal(80, state.PlayerHp);
    }
}

public class AmethystAubergineTests
{
    /// <summary>Fifteen gold after a combat room, and none after the final act's boss.</summary>
    [Fact]
    public void FifteenGoldAfterACombatRoom()
    {
        var state = new RunState { Relics = [new RelicInstance(RelicEffects.AmethystAubergine)] };

        Assert.Equal(15, RelicEffects.ExtraCombatRewardGold(state, isFinalActBoss: false));
        Assert.Equal(0, RelicEffects.ExtraCombatRewardGold(state, isFinalActBoss: true));
    }

    /// <summary>
    /// It goes through `ModifyGoldGained`, so the other gold relics still apply to it —
    /// Ectoplasm zeroes it and Bowler Hat raises it.
    /// </summary>
    [Fact]
    public void TheOtherGoldRelicsStillApply()
    {
        var ecto = new RunState
        {
            Relics =
            [
                new RelicInstance(RelicEffects.AmethystAubergine),
                new RelicInstance(RelicEffects.Ectoplasm),
            ],
        };
        Assert.Equal(0, RelicEffects.ExtraCombatRewardGold(ecto, isFinalActBoss: false));

        var hat = new RunState
        {
            Relics =
            [
                new RelicInstance(RelicEffects.AmethystAubergine),
                new RelicInstance(RelicEffects.BowlerHat),
            ],
        };
        Assert.Equal(18, RelicEffects.ExtraCombatRewardGold(hat, isFinalActBoss: false));
    }
}

public class JuzuBraceletTests
{
    /// <summary>A "?" can never be a Monster room while it is held.</summary>
    [Fact]
    public void ItForbidsMonsterRoomsFromUnknowns()
    {
        var without = new RunState();
        Assert.False(RelicEffects.ForbidsUnknownMonsterRooms(without));

        var with = new RunState { Relics = [new RelicInstance(RelicEffects.JuzuBracelet)] };
        Assert.True(RelicEffects.ForbidsUnknownMonsterRooms(with));
    }

    /// <summary>
    /// The wiring — that `RollUnknownMapPointNodeType` consults the predicate — is NOT
    /// covered here.
    /// </summary>
    /// <remarks>
    /// An end-to-end walk was written and thrown away: driving the map through `Step`
    /// looking for a "?" either never found one inside a bounded walk or did not
    /// terminate, and a test that has to be babysat into passing is worse than an honest
    /// gap. The roll is private and the odds are act-dependent, so covering it properly
    /// needs a seeded map fixture that lands on a "?" — which is the map suite's shape,
    /// not this file's.
    ///
    /// What IS covered: the predicate above, and the one line in
    /// `RollUnknownMapPointNodeType` that removes `NodeNormal` from `allowedRoomTypes`
    /// sits beside the shop blacklist that the map tests already exercise.
    /// </remarks>
    [Fact]
    public void TheRollConsultsThePredicate()
    {
        // A guard rather than a behaviour test: if the removal is ever deleted, the
        // predicate is left with no caller and this says so.
        string engine = System.IO.File.ReadAllText(
            System.IO.Path.Combine(
                RepoRoot(),
                "src",
                "Sts2Emulator",
                "Core",
                "Run",
                "RunEngine.cs"
            )
        );

        Assert.Contains("ForbidsUnknownMonsterRooms(State)", engine);
        Assert.Contains("allowedRoomTypes.Remove(RunConstants.NodeNormal)", engine);
    }

    private static string RepoRoot()
    {
        var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "HANDOFF.md")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
