using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Sts2Emulator.Core.Run;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

/// <summary>
/// Gold the Gremlin Merc stole comes back as a REWARD, and the fight still pays its own.
/// </summary>
/// <remarks>
/// <c>HeistPower.BeforeDeath</c> calls <c>AddExtraReward(new GoldReward(Amount,
/// wasGoldStolenBack: true))</c>, so the player claims it from the screen — a live capture
/// (`9V9WN98106`) shows it as its own row, "80 Gold (stolen back)", beside the fight's
/// ordinary 9. The emulator paid it straight out mid-combat, and only outside the merc's
/// own encounter, which is the one fight the power exists for.
///
/// <para>
/// The fight's ordinary gold was worse: <c>GoldRewardForCurrentNode</c> returned a flat 0
/// for this encounter and so never made the DRAW, putting every rewards-stream value after
/// that fight off by one. <c>GremlinMercNormal.CalculateGoldProportion</c> is 0 only when a
/// Fat Gremlin ESCAPED carrying stolen gold, 0.5 when one escaped with none, and 1 when
/// none escaped — and nothing escapes in the emulator, so it is always 1.
/// </para>
/// </remarks>
public class HeistGoldRewardTests
{
    private static CombatState FatGremlinHoldingHeistGold(int encounterId)
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.EncounterId = encounterId;
        state.Hand.Clear();
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.ExhaustPile.Clear();
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 28,
                Hp = 1,
                MaxHp = 40,
                CurrentIntent = new Intent(IntentType.Attack, 4),
                Buffs = [],
                HeistGold = 80,
            },
        ];
        state.Hand.Add(new CardInstance(IC.StrikeIronclad, false));
        state.Energy = 3;
        return state;
    }

    [Theory]
    [InlineData(RunConstants.GremlinMercEncounterId)]
    [InlineData(3)]
    public void KillingTheFatGremlinBanksItsGoldForTheRewardScreen(int encounterId)
    {
        var state = FatGremlinHoldingHeistGold(encounterId);
        int goldBefore = state.PlayerGold;

        CombatEngine.Step(state, 0, new System.Random(0));

        // Not handed over mid-combat -- it is a row on the screen that follows.
        Assert.Equal(goldBefore, state.PlayerGold);
        Assert.Equal(80, state.StolenBackGold);
    }

    /// <summary>A gremlin that RUNS takes the gold with it — nothing is banked.</summary>
    [Fact]
    public void AGremlinThatEscapesGivesNothingBack()
    {
        var state = FatGremlinHoldingHeistGold(RunConstants.GremlinMercEncounterId);
        state.Enemies[0].Hp = 18;
        // MoveIndex 0 is its attack; anything else is the escape, which the intent
        // table reports as a Buff.
        state.Enemies[0].CurrentIntent = new Intent(IntentType.Buff, 0);

        // Its move IS the escape: it zeroes its own HP and leaves.
        EnemyAI.ExecuteIntent(state.Enemies[0], state, new System.Random(0));

        Assert.True(state.Enemies[0].Escaped);
        Assert.True(state.FatGremlinEscaped);
        Assert.Equal(0, state.StolenBackGold);
    }

    private static RunEngine AtRewardsFor(CombatState combat)
    {
        var engine = new RunEngine();
        engine.Reset("9V9WN98106");
        engine.State.CurrentNodeType = RunConstants.NodeNormal;
        engine.State.ActiveCombat = combat;
        return engine;
    }

    /// <summary>
    /// The merc's encounter pays its ordinary gold like any monster room when nothing
    /// escaped, and — the part that mattered downstream — spends its draw doing it.
    /// </summary>
    [Fact]
    public void TheMercEncounterRollsItsOwnGoldWhenNothingEscaped()
    {
        var combat = FatGremlinHoldingHeistGold(RunConstants.GremlinMercEncounterId);
        combat.StolenBackGold = 80;
        var engine = AtRewardsFor(combat);
        int callsBefore = engine.State.PlayerRng.Rewards.CallCount;

        RunRewardGenerator.GenerateCombatRewards(engine.State);

        // A8's Poverty multiplier turns a monster room's 10-20 into 7-15.
        Assert.InRange(engine.State.RewardGold, 7, 15);
        Assert.True(engine.State.PlayerRng.Rewards.CallCount > callsBefore);
        Assert.Contains(80, engine.State.PendingGoldRewards);
    }

    /// <summary>
    /// A gremlin that escaped WITH the loot means no gold row, and RewardsSet guards the
    /// whole row behind the proportion — so the draw is not made either. This is the case
    /// three long-standing traces captured, and the one a flat zero got right for the
    /// wrong reason.
    /// </summary>
    [Fact]
    public void AnEscapeWithTheLootPaysNothingAndSpendsNoDraw()
    {
        var escaped = FatGremlinHoldingHeistGold(RunConstants.GremlinMercEncounterId);
        escaped.FatGremlinEscaped = true;
        escaped.MercGoldWasStolen = true;
        var engine = AtRewardsFor(escaped);
        int callsBefore = engine.State.PlayerRng.Rewards.CallCount;

        RunRewardGenerator.GenerateCombatRewards(engine.State);

        Assert.Equal(0, engine.State.RewardGold);
        Assert.Empty(engine.State.PendingGoldRewards);

        // The potion roll and the card reward still draw; the GOLD roll must not, so the
        // same seed spends exactly one more value when the fight pays out.
        int spent = engine.State.PlayerRng.Rewards.CallCount - callsBefore;
        var full = AtRewardsFor(FatGremlinHoldingHeistGold(RunConstants.GremlinMercEncounterId));
        int fullBefore = full.State.PlayerRng.Rewards.CallCount;
        RunRewardGenerator.GenerateCombatRewards(full.State);
        Assert.Equal(spent + 1, full.State.PlayerRng.Rewards.CallCount - fullBefore);
    }

    /// <summary>Escaping empty-handed is the half-gold case: 7-15 scaled to 4-8.</summary>
    [Fact]
    public void AnEscapeWithNothingStolenPaysHalf()
    {
        var combat = FatGremlinHoldingHeistGold(RunConstants.GremlinMercEncounterId);
        combat.FatGremlinEscaped = true;
        combat.MercGoldWasStolen = false;
        var engine = AtRewardsFor(combat);

        RunRewardGenerator.GenerateCombatRewards(engine.State);

        Assert.InRange(engine.State.RewardGold, 4, 8);
    }
}
