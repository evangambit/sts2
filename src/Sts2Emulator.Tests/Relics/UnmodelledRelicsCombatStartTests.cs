using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The relics whose whole effect is a turn-one opener: `AfterSideTurnStart` or
/// `BeforeSideTurnStart` guarded on `TurnNumber &lt;= 1`, which is a once-per-fight grant
/// wearing a per-turn hook. Cracked Core already had that shape; these eight did not exist
/// at all.
/// </summary>
/// <remarks>
/// Every one of these had a row in `Relics.g.cs` and no id constant anywhere, which is the
/// state `audit_relics.py` calls "unmodelled": the run can be handed the relic, it appears
/// in the relic list and in the observation, and it does nothing. That is worse than a
/// wrong number, because there is no arm to read and find wrong.
/// </remarks>
public class TurnOneOpenerRelicTests
{
    /// <summary>`VeryHotCocoa`: EnergyVar(4) -- an Ancient relic worth a whole extra turn.</summary>
    [Fact]
    public void VeryHotCocoaOpensWithFourExtraEnergy()
    {
        var plain = Fight.WithRelics();
        var cocoa = Fight.WithRelics(RelicEffects.VeryHotCocoa);

        Assert.Equal(plain.State.Energy + 4, cocoa.State.Energy);
    }

    /// <summary>Turn one only -- the next turn refills to the ordinary maximum.</summary>
    [Fact]
    public void TheCocoaDoesNotPourAgain()
    {
        var plain = Fight.WithRelics();
        var cocoa = Fight.WithRelics(RelicEffects.VeryHotCocoa);

        plain.EndTurn();
        cocoa.EndTurn();

        Assert.Equal(plain.State.Energy, cocoa.State.Energy);
    }

    /// <summary>`RunicCapacitor`: RepeatVar(3) orb slots, `OrbCmd.AddSlots`.</summary>
    [Fact]
    public void RunicCapacitorAddsThreeOrbSlots()
    {
        var plain = Fight.WithRelics();
        var capacitor = Fight.WithRelics(RelicEffects.RunicCapacitor);

        Assert.Equal(plain.State.OrbCapacity + 3, capacitor.State.OrbCapacity);
    }

    /// <summary>`SymbioticVirus`: `DynamicVar("Dark", 1m)` channelled.</summary>
    [Fact]
    public void SymbioticVirusChannelsOneDark()
    {
        var fight = Fight.WithRelics(RelicEffects.SymbioticVirus, RelicEffects.RunicCapacitor);

        Assert.Equal(1, fight.State.Orbs.Count(orb => orb.Type == OrbType.Dark));
    }

    /// <summary>`TwistedFunnel`: Poison 4 on every hittable enemy.</summary>
    [Fact]
    public void TwistedFunnelPoisonsTheWholeRoom()
    {
        // The encounter's own enemies: `.Enemy()` replaces the roster AFTER setup has run,
        // which is too late for anything that fires at combat start. Encounter 3 is three
        // enemies with no Artifact between them -- encounter 1's pair both hold one, and
        // Artifact swallows a debuff whole.
        var fight = Fight.Encounter(3, RelicEffects.TwistedFunnel);

        Assert.All(
            fight.State.Enemies,
            enemy => Assert.Equal(4, BuffSystem.Get(enemy.Buffs, BuffId.Poison))
        );
    }

    /// <summary>
    /// Poison is a debuff, so Artifact eats it: against encounter 1, whose pair each hold
    /// Artifact 2, the funnel lands nothing and spends one charge apiece. Worth pinning --
    /// the relic reads as unconditional and its whole value can be swallowed on turn one.
    /// </summary>
    [Fact]
    public void ArtifactSwallowsTheFunnelsPoison()
    {
        var plain = Fight.WithRelics();
        var funnel = Fight.WithRelics(RelicEffects.TwistedFunnel);

        for (int i = 0; i < funnel.State.Enemies.Count; i++)
        {
            Assert.Equal(0, BuffSystem.Get(funnel.State.Enemies[i].Buffs, BuffId.Poison));
            Assert.Equal(
                BuffSystem.Get(plain.State.Enemies[i].Buffs, BuffId.Artifact) - 1,
                BuffSystem.Get(funnel.State.Enemies[i].Buffs, BuffId.Artifact)
            );
        }
    }

    /// <summary>`FencingManual`: ForgeVar(10) -- a Sovereign Blade from a Common relic.</summary>
    [Fact]
    public void FencingManualForgesABladeAtTen()
    {
        var fight = Fight.WithRelics(RelicEffects.FencingManual);

        Assert.Contains(
            fight.State.Hand.Concat(fight.State.DrawPile).Concat(fight.State.DiscardPile),
            card => GeneratedData.Cards.Get(card.DefId).Name == "SovereignBlade"
        );
    }

    /// <summary>`OrangeDough`: CardsVar(2) DISTINCT colourless cards into hand.</summary>
    [Fact]
    public void OrangeDoughOpensWithTwoColourlessCards()
    {
        var plain = Fight.WithRelics();
        var dough = Fight.WithRelics(RelicEffects.OrangeDough);

        Assert.Equal(plain.State.Hand.Count + 2, dough.State.Hand.Count);
        var added = dough.State.Hand.TakeLast(2).ToList();
        Assert.Equal(2, added.Select(card => card.DefId).Distinct().Count());
        Assert.All(
            added,
            card =>
                Assert.Contains(card.DefId, GeneratedData.CardPools.Colorless.ToArray())
        );
    }

    /// <summary>
    /// `BigHat` does NOTHING for an Ironclad, and that is the game's answer rather than a
    /// gap: its filter is the OWN character pool narrowed to Ethereal, and the Ironclad
    /// pool contains no Ethereal card at all. `readOnlyList.Count > 0` is false and the
    /// block is skipped.
    /// </summary>
    /// <remarks>
    /// Neither does the Silent's pool. The relic is a Necrobinder card (eight Ethereal), a
    /// Regent one (two) and a marginal Defect one (Echo Form) -- so a Rare relic that
    /// reads as two free cards is dead weight for two of the five characters, and the
    /// emulator only runs one of them.
    /// </remarks>
    [Fact]
    public void BigHatFindsNothingToGiveAnIronclad()
    {
        var plain = Fight.WithRelics();
        var hat = Fight.WithRelics(RelicEffects.BigHat);

        Assert.Equal(plain.State.Hand.Count, hat.State.Hand.Count);
        Assert.Empty(
            GeneratedData
                .CardPools.Ironclad.ToArray()
                .Where(id => GeneratedData.Cards.Get(id).Ethereal)
        );
    }

    /// <summary>
    /// Given a pool that HAS Ethereal cards it hands over two distinct ones. Driven
    /// directly, because the emulator's only character is the one it does nothing for.
    /// </summary>
    [Fact]
    public void BigHatGivesTwoDistinctEtherealCardsWhenTheresAPool()
    {
        var fight = Fight.WithRelics();
        int before = fight.State.Hand.Count;

        CardEffects.AddDistinctEtherealCardsToHandFromPool(
            fight.State,
            GeneratedData.CardPools.Necrobinder,
            2,
            new System.Random(0)
        );

        Assert.Equal(before + 2, fight.State.Hand.Count);
        var added = fight.State.Hand.TakeLast(2).ToList();
        Assert.Equal(2, added.Select(card => card.DefId).Distinct().Count());
        Assert.All(added, card => Assert.True(GeneratedData.Cards.Get(card.DefId).Ethereal));
    }

    /// <summary>
    /// `PowerCell`: two ZERO-COST cards MOVED out of the draw pile into hand. A move, not
    /// a generation -- the cards already exist, and the pile is two shorter for it.
    /// </summary>
    [Fact]
    public void PowerCellPullsTwoFreeCardsOutOfTheDrawPile()
    {
        var fight = Fight.WithRelics();
        fight.State.DrawPile.Clear();
        int shiv = 430;
        for (int i = 0; i < 4; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(shiv, false));
        }

        fight.State.DrawPile.Add(new CardInstance(GeneratedData.Cards.FindId("Bash")!.Value, false));
        int handBefore = fight.State.Hand.Count;

        CardEffects.MoveZeroCostDrawCardsToHandForPowerCell(
            fight.State,
            2,
            new System.Random(0)
        );

        // A MOVE, not a generation: two out of the pile, two into hand.
        Assert.Equal(handBefore + 2, fight.State.Hand.Count);
        Assert.Equal(3, fight.State.DrawPile.Count);
        Assert.Equal(2, fight.State.Hand.TakeLast(2).Count(card => card.DefId == shiv));
    }

    /// <summary>The starter deck has no zero-cost card, so it opens with nothing.</summary>
    [Fact]
    public void PowerCellFindsNothingInAStarterDeck()
    {
        var plain = Fight.WithRelics();
        var cell = Fight.WithRelics(RelicEffects.PowerCell);

        Assert.Equal(plain.State.Hand.Count, cell.State.Hand.Count);
    }

    /// <summary>An X-cost card is never free, however low its cost reads.</summary>
    [Fact]
    public void PowerCellRefusesAnXCostCard()
    {
        var fight = Fight.WithRelics();
        fight.State.DrawPile.Clear();
        int xCost = GeneratedData
            .Cards.All.ToArray()
            .First(def => def.HasEnergyCostX && def.Cost == 0)
            .Id;
        fight.State.DrawPile.Add(new CardInstance(xCost, false));
        int handBefore = fight.State.Hand.Count;

        CardEffects.MoveZeroCostDrawCardsToHandForPowerCell(
            fight.State,
            2,
            new System.Random(0)
        );

        Assert.Equal(handBefore, fight.State.Hand.Count);
        Assert.Single(fight.State.DrawPile);
    }

    /// <summary>
    /// `Brimstone` has NO turn guard: 2 Strength to the player and 1 to every living
    /// enemy, every turn. The enemy half is the point -- it arms the room as fast as you.
    /// </summary>
    [Fact]
    public void BrimstoneArmsBothSidesEveryTurn()
    {
        var fight = Fight.WithRelics(RelicEffects.Brimstone);

        Assert.Equal(2, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));
        Assert.Equal(1, BuffSystem.Get(fight.State.Enemies[0].Buffs, BuffId.Strength));

        fight.EndTurn();

        Assert.Equal(4, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));
        Assert.Equal(2, BuffSystem.Get(fight.State.Enemies[0].Buffs, BuffId.Strength));
    }

    /// <summary>A dead enemy is not armed: the target list is `where c.IsAlive`.</summary>
    [Fact]
    public void BrimstoneSkipsTheDead()
    {
        var fight = Fight.WithRelics(RelicEffects.Brimstone);
        int dead = fight.State.Enemies.Count - 1;
        int armedSoFar = BuffSystem.Get(fight.State.Enemies[dead].Buffs, BuffId.Strength);
        fight.State.Enemies[dead].Hp = 0;

        fight.EndTurn();

        Assert.Equal(armedSoFar, BuffSystem.Get(fight.State.Enemies[dead].Buffs, BuffId.Strength));
    }
}
