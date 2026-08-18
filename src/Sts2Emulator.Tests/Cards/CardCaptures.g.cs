// AUTO-GENERATED — do not edit. Re-run scripts/generate_card_capture_tests.py to update.
// Expected values come from the live game via scripts/capture_card.py, never from the
// emulator: re-capturing a fixture re-reads ground truth, so regenerating cannot mask a
// regression. Hand-written per-card tests live in Cards/<Class>/<Card>Tests.cs.
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

public class CardCaptureTests
{
    [Fact]
    public void IronWave_Base_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card IronWave --encounter CorpseSlugsWeak --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(IC.IronWave), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.StrikeIronclad))
            .PlayerHp(64, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.AscendersBane), Card(IC.StrikeIronclad))
            .Enemy(defId: 17, hp: 28, maxHp: 28, buffs: [new BuffState(BuffId.Ravenous, 4)])
            .Enemy(defId: 17, hp: 29, maxHp: 29, buffs: [new BuffState(BuffId.Ravenous, 4)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(64, fight.State.PlayerHp);
        Assert.Equal(5, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(23, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 0));
        Assert.Equal(29, fight.State.Enemies[1].Hp);
        Assert.Equal(0, fight.State.Enemies[1].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 1));
    }

    [Fact]
    public void MoltenFist_Base_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card MoltenFist --encounter CorpseSlugsWeak --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(IC.MoltenFist), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.StrikeIronclad))
            .PlayerHp(64, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.AscendersBane), Card(IC.StrikeIronclad))
            .Enemy(defId: 17, hp: 28, maxHp: 28, buffs: [new BuffState(BuffId.Ravenous, 4)])
            .Enemy(defId: 17, hp: 29, maxHp: 29, buffs: [new BuffState(BuffId.Ravenous, 4)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(64, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        Assert.Equal(18, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 0));
        Assert.Equal(29, fight.State.Enemies[1].Hp);
        Assert.Equal(0, fight.State.Enemies[1].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 1));
    }

    [Fact]
    public void MoltenFist_Upgraded_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card MoltenFist --upgraded --encounter CorpseSlugsWeak --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(IC.MoltenFist, upgraded: true), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.StrikeIronclad))
            .PlayerHp(64, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.AscendersBane), Card(IC.StrikeIronclad))
            .Enemy(defId: 17, hp: 28, maxHp: 28, buffs: [new BuffState(BuffId.Ravenous, 4)])
            .Enemy(defId: 17, hp: 29, maxHp: 29, buffs: [new BuffState(BuffId.Ravenous, 4)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(64, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        Assert.Equal(14, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 0));
        Assert.Equal(29, fight.State.Enemies[1].Hp);
        Assert.Equal(0, fight.State.Enemies[1].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 1));
    }

    [Fact]
    public void MoltenFist_Base_Vulnerable_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card MoltenFist --encounter CorpseSlugsWeak --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(IC.MoltenFist), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.StrikeIronclad))
            .PlayerHp(64, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.AscendersBane), Card(IC.StrikeIronclad))
            .Enemy(defId: 17, hp: 28, maxHp: 28, buffs: [new BuffState(BuffId.Ravenous, 4), new BuffState(BuffId.Vulnerable, 2)])
            .Enemy(defId: 17, hp: 29, maxHp: 29, buffs: [new BuffState(BuffId.Ravenous, 4)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(64, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        Assert.Equal(13, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 0));
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Vulnerable, 0));
        Assert.Equal(29, fight.State.Enemies[1].Hp);
        Assert.Equal(0, fight.State.Enemies[1].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 1));
    }

    [Fact]
    public void Thunderclap_Base_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Thunderclap --encounter CorpseSlugsWeak --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(IC.Thunderclap), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.StrikeIronclad))
            .PlayerHp(64, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.AscendersBane), Card(IC.StrikeIronclad))
            .Enemy(defId: 17, hp: 28, maxHp: 28, buffs: [new BuffState(BuffId.Ravenous, 4)])
            .Enemy(defId: 17, hp: 29, maxHp: 29, buffs: [new BuffState(BuffId.Ravenous, 4)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(64, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(24, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 0));
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Vulnerable, 0));
        Assert.Equal(25, fight.State.Enemies[1].Hp);
        Assert.Equal(0, fight.State.Enemies[1].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 1));
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Vulnerable, 1));
    }
}
