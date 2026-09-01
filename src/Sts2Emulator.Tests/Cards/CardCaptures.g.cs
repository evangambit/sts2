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
    public void AdaptiveStrike_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card AdaptiveStrike --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(5), Card(130), Card(130), Card(545), Card(156), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(471), Card(471), Card(471), Card(471), Card(130), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Equal(2, fight.State.DiscardPile.Count);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(72, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Afterlife_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Afterlife --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(8), Card(IC.AscendersBane), Card(132), Card(524), Card(49), Card(473))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(132), Card(473), Card(132), Card(473), Card(132), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(7, fight.State.OstyHp);
        Assert.Equal(7, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Alignment_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Alignment --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(11), Card(133), Card(133), Card(474), Card(133), Card(IC.AscendersBane))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(532), Card(474), Card(179), Card(133), Card(474), Card(474))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 3);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(11, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(0, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void AllForOne_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card AllForOne --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(12), Card(156), Card(130), Card(130), Card(IC.AscendersBane), Card(471))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(471), Card(471), Card(130), Card(545), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(80, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Anger_Base_CorpseSlugsWeak_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Anger --encounter CorpseSlugsWeak --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(IC.Anger), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.AscendersBane))
            .PlayerHp(64, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.Bash))
            .Enemy(defId: 17, hp: 27, maxHp: 27, buffs: [new BuffState(BuffId.Ravenous, 4)])
            .Enemy(defId: 17, hp: 29, maxHp: 29, buffs: [new BuffState(BuffId.Ravenous, 4)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(64, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Equal(2, fight.State.DiscardPile.Count);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(21, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 0));
        Assert.Equal(29, fight.State.Enemies[1].Hp);
        Assert.Equal(0, fight.State.Enemies[1].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 1));
    }

    [Fact]
    public void Arsenal_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Arsenal --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(19), Card(474), Card(133), Card(133), Card(474), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(133), Card(IC.AscendersBane), Card(532), Card(474), Card(179), Card(474))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 3);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.Stars);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Arsenal));
        fight.PlayerPowersAre(BuffId.Arsenal);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void AshenStrike_Base_CorpseSlugsWeak_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card AshenStrike --encounter CorpseSlugsWeak --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(IC.AshenStrike), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad))
            .PlayerHp(64, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.AscendersBane), Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad))
            .Enemy(defId: 17, hp: 27, maxHp: 27, buffs: [new BuffState(BuffId.Ravenous, 4)])
            .Enemy(defId: 17, hp: 28, maxHp: 28, buffs: [new BuffState(BuffId.Ravenous, 4)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(64, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(21, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 0));
        Assert.Equal(28, fight.State.Enemies[1].Hp);
        Assert.Equal(0, fight.State.Enemies[1].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 1));
    }

    [Fact]
    public void AstralPulse_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card AstralPulse --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(22), Card(179), Card(133), Card(IC.AscendersBane), Card(474), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(133), Card(474), Card(474), Card(133), Card(532), Card(474))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 3);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(0, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(78, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Backstab_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Backstab --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(SI.Backstab), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad))
            .PlayerHp(61, 80)
            .Energy(9)
            .Draw(Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.AscendersBane), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.Bash))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(79, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void BallLightning_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card BallLightning --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(26), Card(130), Card(471), Card(545), Card(471), Card(IC.AscendersBane))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(130), Card(471), Card(130), Card(471), Card(156))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Equal(2, fight.State.Orbs.Count);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[1].Type);
        fight.PlayerPowersAre();
        Assert.Equal(83, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void BansheesCry_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card BansheesCry --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(27), Card(132), Card(132), Card(473), Card(473), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(IC.AscendersBane), Card(132), Card(524), Card(473), Card(49))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(0, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(57, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Barrage_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Barrage --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(28), Card(471), Card(130), Card(130), Card(156), Card(471))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(471), Card(471), Card(130), Card(IC.AscendersBane), Card(545), Card(130))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(85, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Bash_Base_CorpseSlugsWeak_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Bash --encounter CorpseSlugsWeak --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(IC.Bash), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad))
            .PlayerHp(64, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.DefendIronclad), Card(IC.AscendersBane))
            .Enemy(defId: 17, hp: 28, maxHp: 28, buffs: [new BuffState(BuffId.Ravenous, 4)])
            .Enemy(defId: 17, hp: 29, maxHp: 29, buffs: [new BuffState(BuffId.Ravenous, 4)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(64, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(20, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 0));
        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Vulnerable, 0));
        Assert.Equal(29, fight.State.Enemies[1].Hp);
        Assert.Equal(0, fight.State.Enemies[1].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 1));
    }

    [Fact]
    public void BeamCell_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card BeamCell --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(33), Card(130), Card(130), Card(471), Card(130), Card(IC.AscendersBane))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(156), Card(471), Card(545), Card(130), Card(471), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(87, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Vulnerable, 0));
    }

    [Fact]
    public void BeatDown_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card BeatDown --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(CL.BeatDown), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad))
            .PlayerHp(61, 80)
            .Energy(9)
            .Draw(Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.AscendersBane), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.Bash))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(6, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void BeatIntoShape_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card BeatIntoShape --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(35), Card(133), Card(474), Card(532), Card(133), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(133), Card(179), Card(474), Card(474), Card(474), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 3);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(85, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Beckon_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Beckon --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(ST.Beckon), Card(132), Card(132), Card(473), Card(473), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(IC.AscendersBane), Card(132), Card(524), Card(473), Card(49))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void BiasedCognition_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card BiasedCognition --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(39), Card(130), Card(130), Card(130), Card(471), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(471), Card(471), Card(156), Card(IC.AscendersBane), Card(471), Card(545))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(4, fight.PlayerBuffAmount(BuffId.Focus));
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.BiasedCognition));
        fight.PlayerPowersAre(BuffId.Focus, BuffId.BiasedCognition);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void BigBang_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card BigBang --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(40), Card(133), Card(474), Card(179), Card(474), Card(IC.AscendersBane))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(133), Card(133), Card(474), Card(133), Card(474), Card(532))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(10, fight.State.Energy);
        Assert.Equal(5, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        Assert.Equal(10, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void BlackHole_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card BlackHole --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(41), Card(179), Card(532), Card(474), Card(133), Card(IC.AscendersBane))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(133), Card(474), Card(133), Card(474), Card(474), Card(133))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        Assert.Equal(3, fight.PlayerBuffAmount(BuffId.BlackHole));
        fight.PlayerPowersAre(BuffId.BlackHole);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void BlightStrike_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card BlightStrike --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(44), Card(132), Card(473), Card(132), Card(473), Card(473))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(IC.AscendersBane), Card(524), Card(132), Card(132), Card(49))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(82, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(8, fight.EnemyBuffAmount(BuffId.Doom, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Bludgeon_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Bludgeon --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(IC.Bludgeon), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad))
            .PlayerHp(64, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.AscendersBane), Card(IC.DefendIronclad), Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.DefendIronclad))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(64, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(6, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(58, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void BodySlam_Base_Sharp_CorpseSlugsWeak_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card BodySlam --encounter CorpseSlugsWeak --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(IC.BodySlam) with { Enchantment = Enchantment.Sharp, EnchantAmount = 5 }, Card(IC.DefendIronclad), Card(IC.AscendersBane), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.DefendIronclad))
            .PlayerHp(64, 80)
            .Energy(9)
            .Draw(Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad))
            .Enemy(defId: 17, hp: 28, maxHp: 28, buffs: [new BuffState(BuffId.Ravenous, 4)])
            .Enemy(defId: 17, hp: 27, maxHp: 27, buffs: [new BuffState(BuffId.Ravenous, 4)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(64, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(23, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 0));
        Assert.Equal(27, fight.State.Enemies[1].Hp);
        Assert.Equal(0, fight.State.Enemies[1].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 1));
    }

    [Fact]
    public void Bodyguard_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Bodyguard --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(49), Card(132), Card(132), Card(IC.AscendersBane), Card(473), Card(473))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(132), Card(524), Card(132), Card(473), Card(49), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(6, fight.State.OstyHp);
        Assert.Equal(6, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Bolas_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Bolas --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(CL.Bolas), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad))
            .PlayerHp(61, 80)
            .Energy(9)
            .Draw(Card(IC.AscendersBane), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.DefendIronclad), Card(IC.DefendIronclad))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(87, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Bombardment_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Bombardment --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(52), Card(133), Card(133), Card(474), Card(474), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(IC.AscendersBane), Card(133), Card(532), Card(474), Card(179))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 3);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(6, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(72, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void BoneShards_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card BoneShards --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(53), Card(473), Card(132), Card(132), Card(473), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(132), Card(IC.AscendersBane), Card(524), Card(473), Card(49), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(9, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(81, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(0, fight.State.OstyHp);
    }

    [Fact]
    public void BoostAway_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card BoostAway --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(54), Card(130), Card(545), Card(130), Card(471), Card(471))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(130), Card(471), Card(IC.AscendersBane), Card(156), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(6, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Equal(2, fight.State.DiscardPile.Count);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void BootSequence_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card BootSequence --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(55), Card(471), Card(130), Card(130), Card(471), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(IC.AscendersBane), Card(156), Card(471), Card(545), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(10, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void BorrowedTime_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card BorrowedTime --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(56), Card(IC.AscendersBane), Card(49), Card(132), Card(473), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(132), Card(524), Card(473), Card(473), Card(132), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(12, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.BorrowedTime));
        fight.PlayerPowersAre(BuffId.BorrowedTime);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Breakthrough_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Breakthrough --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(IC.Breakthrough), Card(IC.DefendIronclad), Card(IC.AscendersBane), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.DefendIronclad))
            .PlayerHp(62, 80)
            .Energy(9)
            .Draw(Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(81, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void BrightestFlame_Base_CorpseSlugsWeak_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card BrightestFlame --encounter CorpseSlugsWeak --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(61), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.StrikeIronclad))
            .PlayerHp(64, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.AscendersBane), Card(IC.StrikeIronclad))
            .Enemy(defId: 17, hp: 28, maxHp: 28, buffs: [new BuffState(BuffId.Ravenous, 4)])
            .Enemy(defId: 17, hp: 29, maxHp: 29, buffs: [new BuffState(BuffId.Ravenous, 4)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(64, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(11, fight.State.Energy);
        Assert.Equal(4, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(28, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 0));
        Assert.Equal(29, fight.State.Enemies[1].Hp);
        Assert.Equal(0, fight.State.Enemies[1].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 1));
    }

    [Fact]
    public void Buffer_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Buffer --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(63), Card(545), Card(130), Card(IC.AscendersBane), Card(471), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(471), Card(471), Card(130), Card(156), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Buffer));
        fight.PlayerPowersAre(BuffId.Buffer);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void BulkUp_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card BulkUp --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(64), Card(471), Card(130), Card(471), Card(130), Card(545))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(156), Card(471), Card(IC.AscendersBane), Card(471), Card(130), Card(130))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(2, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.Strength));
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.Dexterity));
        fight.PlayerPowersAre(BuffId.Strength, BuffId.Dexterity);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Bulwark_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Bulwark --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(67), Card(133), Card(133), Card(474), Card(474), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(IC.AscendersBane), Card(133), Card(532), Card(474), Card(179))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 3);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(12, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void BundleOfJoy_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card BundleOfJoy --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(68), Card(179), Card(133), Card(474), Card(133), Card(474))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(133), Card(474), Card(532), Card(133), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 3);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Bury_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Bury --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(71), Card(49), Card(132), Card(473), Card(132), Card(473))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(132), Card(473), Card(524), Card(132), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(5, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(38, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Calcify_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Calcify --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(74), Card(473), Card(473), Card(49), Card(132), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(132), Card(473), Card(132), Card(IC.AscendersBane), Card(524))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(4, fight.PlayerBuffAmount(BuffId.Calcify));
        fight.PlayerPowersAre(BuffId.Calcify);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void CallOfTheVoid_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card CallOfTheVoid --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(76), Card(132), Card(473), Card(49), Card(132), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(132), Card(IC.AscendersBane), Card(473), Card(473), Card(524), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.CallOfTheVoid));
        fight.PlayerPowersAre(BuffId.CallOfTheVoid);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Capacitor_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Capacitor --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(78), Card(130), Card(471), Card(156), Card(130), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(545), Card(471), Card(471), Card(471), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(5, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Cascade_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Cascade --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(546), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.StrikeIronclad))
            .PlayerHp(64, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.AscendersBane), Card(IC.StrikeIronclad))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(64, fight.State.PlayerHp);
        Assert.Equal(10, fight.State.PlayerBlock);
        Assert.Equal(0, fight.State.Energy);
        Assert.Empty(fight.State.DrawPile);
        Assert.Equal(7, fight.State.DiscardPile.Count);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(72, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void CelestialMight_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card CelestialMight --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(81), Card(133), Card(IC.AscendersBane), Card(474), Card(532), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(179), Card(474), Card(474), Card(133), Card(474), Card(133))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 3);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(72, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void ChargeBattery_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card ChargeBattery --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(84), Card(130), Card(471), Card(471), Card(130), Card(545))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(156), Card(130), Card(130), Card(IC.AscendersBane), Card(471), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(7, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.NextTurnEnergy));
        fight.PlayerPowersAre(BuffId.NextTurnEnergy);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void ChildOfTheStars_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card ChildOfTheStars --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(85), Card(179), Card(133), Card(474), Card(532), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(133), Card(474), Card(133), Card(IC.AscendersBane), Card(474))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 3);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.Stars);
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.ChildOfTheStars));
        fight.PlayerPowersAre(BuffId.ChildOfTheStars);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Chill_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Chill --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(86), Card(471), Card(545), Card(130), Card(IC.AscendersBane), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(156), Card(130), Card(130), Card(471), Card(471), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Equal(2, fight.State.Orbs.Count);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(OrbType.Frost, fight.State.Orbs[1].Type);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Cinder_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Cinder --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(IC.Cinder), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad))
            .PlayerHp(61, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.AscendersBane), Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(72, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Claw_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Claw --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(89), Card(545), Card(130), Card(471), Card(471), Card(471))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(IC.AscendersBane), Card(156), Card(471), Card(130), Card(130), Card(130))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(87, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void CloakOfStars_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card CloakOfStars --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(92), Card(133), Card(133), Card(474), Card(474), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(IC.AscendersBane), Card(133), Card(532), Card(474), Card(179))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 3);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(7, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(2, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void ColdSnap_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card ColdSnap --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(93), Card(156), Card(130), Card(130), Card(471), Card(IC.AscendersBane))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(545), Card(471), Card(471), Card(471), Card(130))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Equal(2, fight.State.Orbs.Count);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(OrbType.Frost, fight.State.Orbs[1].Type);
        fight.PlayerPowersAre();
        Assert.Equal(84, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void CollisionCourse_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card CollisionCourse --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(94), Card(133), Card(133), Card(474), Card(133), Card(IC.AscendersBane))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(532), Card(474), Card(179), Card(133), Card(474), Card(474))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(79, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Comet_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Comet --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(96), Card(179), Card(133), Card(474), Card(532), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(133), Card(474), Card(133), Card(IC.AscendersBane), Card(474))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(4, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(57, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(3, fight.EnemyBuffAmount(BuffId.Weak, 0));
        Assert.Equal(3, fight.EnemyBuffAmount(BuffId.Vulnerable, 0));
    }

    [Fact]
    public void Compact_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Compact --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(97), Card(130), Card(130), Card(471), Card(471), Card(471))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(545), Card(130), Card(IC.AscendersBane), Card(471), Card(130), Card(156))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(6, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void CompileDriver_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card CompileDriver --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(98), Card(130), Card(130), Card(IC.AscendersBane), Card(471), Card(471))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(156), Card(130), Card(471), Card(545), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(5, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(83, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Conqueror_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Conqueror --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(100), Card(133), Card(133), Card(474), Card(474), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(IC.AscendersBane), Card(133), Card(532), Card(474), Card(179))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 3);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Conqueror, 0));
    }

    [Fact]
    public void ConsumingShadow_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card ConsumingShadow --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(101), Card(471), Card(471), Card(130), Card(156), Card(IC.AscendersBane))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(471), Card(130), Card(130), Card(471), Card(545), Card(130))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Equal(3, fight.State.Orbs.Count);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(OrbType.Dark, fight.State.Orbs[1].Type);
        Assert.Equal(6, fight.State.Orbs[1].EvokeValue);
        Assert.Equal(OrbType.Dark, fight.State.Orbs[2].Type);
        Assert.Equal(6, fight.State.Orbs[2].EvokeValue);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.ConsumingShadow));
        fight.PlayerPowersAre(BuffId.ConsumingShadow);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void ConsumingShadow_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card ConsumingShadow --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(101), Card(132), Card(132), Card(49), Card(524), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(473), Card(473), Card(473), Card(132), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.ConsumingShadow));
        fight.PlayerPowersAre(BuffId.ConsumingShadow);
        Assert.Equal(84, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Convergence_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Convergence --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(102), Card(179), Card(133), Card(474), Card(133), Card(474))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(133), Card(474), Card(532), Card(133), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 3);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.Stars);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.RetainHand));
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.NextTurnEnergy));
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.StarNextTurn));
        fight.PlayerPowersAre(BuffId.RetainHand, BuffId.NextTurnEnergy, BuffId.StarNextTurn);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Coolant_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Coolant --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(103), Card(IC.AscendersBane), Card(130), Card(156), Card(545), Card(471))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(471), Card(130), Card(471), Card(130), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.Coolant));
        fight.PlayerPowersAre(BuffId.Coolant);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Coolheaded_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Coolheaded --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(104), Card(545), Card(156), Card(471), Card(130), Card(IC.AscendersBane))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(471), Card(130), Card(471), Card(471), Card(130))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(5, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Equal(2, fight.State.Orbs.Count);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(OrbType.Frost, fight.State.Orbs[1].Type);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void CosmicIndifference_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card CosmicIndifference --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(108), Card(133), Card(IC.AscendersBane), Card(474), Card(532), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(179), Card(474), Card(474), Card(133), Card(474), Card(133))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 3);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(6, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Countdown_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Countdown --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(109), Card(132), Card(473), Card(473), Card(524), Card(473))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(132), Card(132), Card(49), Card(IC.AscendersBane), Card(132))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(6, fight.PlayerBuffAmount(BuffId.Countdown));
        fight.PlayerPowersAre(BuffId.Countdown);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void CrashLanding_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card CrashLanding --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(110), Card(474), Card(133), Card(133), Card(474), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(133), Card(IC.AscendersBane), Card(532), Card(474), Card(179), Card(474))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(69, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void CreativeAi_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card CreativeAi --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(111), Card(471), Card(130), Card(130), Card(545), Card(471))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(471), Card(471), Card(156), Card(IC.AscendersBane), Card(130))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(6, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.CreativeAi));
        fight.PlayerPowersAre(BuffId.CreativeAi);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void CrescentSpear_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card CrescentSpear --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(112), Card(179), Card(133), Card(IC.AscendersBane), Card(474), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(133), Card(474), Card(474), Card(133), Card(532), Card(474))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(8, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(78, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void CrushUnder_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card CrushUnder --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(115), Card(133), Card(474), Card(532), Card(133), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(133), Card(179), Card(474), Card(474), Card(474), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(83, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(-1, fight.EnemyBuffAmount(BuffId.Strength, 0));
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.TemporaryStrength, 0));
    }

    [Fact]
    public void DanseMacabre_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card DanseMacabre --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(118), Card(524), Card(132), Card(132), Card(IC.AscendersBane), Card(473))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(132), Card(473), Card(473), Card(132), Card(49), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(4, fight.PlayerBuffAmount(BuffId.DanseMacabre));
        fight.PlayerPowersAre(BuffId.DanseMacabre);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Darkness_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Darkness --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(120), Card(IC.AscendersBane), Card(130), Card(471), Card(471), Card(545))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(156), Card(471), Card(130), Card(130), Card(130), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Equal(2, fight.State.Orbs.Count);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(OrbType.Dark, fight.State.Orbs[1].Type);
        Assert.Equal(12, fight.State.Orbs[1].EvokeValue);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void DeadlyPoison_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card DeadlyPoison --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(SI.DeadlyPoison), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.AscendersBane))
            .PlayerHp(61, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.DefendIronclad))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(5, fight.EnemyBuffAmount(BuffId.Poison, 0));
    }

    [Fact]
    public void DeathMarch_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card DeathMarch --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(125), Card(49), Card(132), Card(473), Card(524), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(132), Card(473), Card(132), Card(IC.AscendersBane), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(82, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Deathbringer_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Deathbringer --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(124), Card(132), Card(132), Card(473), Card(473), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(IC.AscendersBane), Card(132), Card(524), Card(473), Card(49))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(21, fight.EnemyBuffAmount(BuffId.Doom, 0));
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Weak, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void DeathsDoor_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card DeathsDoor --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(126), Card(132), Card(IC.AscendersBane), Card(473), Card(524), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(49), Card(473), Card(473), Card(132), Card(473), Card(132))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(6, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Debilitate_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Debilitate --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(127), Card(49), Card(132), Card(473), Card(132), Card(473))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(132), Card(473), Card(524), Card(132), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(80, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Debilitate, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void DefendDefect_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card DefendDefect --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(130), Card(130), Card(130), Card(471), Card(471), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(471), Card(IC.AscendersBane), Card(130), Card(156), Card(471), Card(545))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(5, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void DefendNecrobinder_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card DefendNecrobinder --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(132), Card(132), Card(473), Card(49), Card(IC.AscendersBane), Card(473))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(132), Card(473), Card(132), Card(132), Card(524))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(5, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void DefendRegent_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card DefendRegent --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(133), Card(133), Card(IC.AscendersBane), Card(474), Card(532), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(179), Card(474), Card(474), Card(133), Card(474), Card(133))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 3);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(5, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Defile_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Defile --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(135), Card(132), Card(IC.AscendersBane), Card(473), Card(524), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(49), Card(473), Card(473), Card(132), Card(473), Card(132))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(77, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Defragment_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Defragment --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(137), Card(130), Card(471), Card(130), Card(471), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(156), Card(IC.AscendersBane), Card(545), Card(471), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Focus));
        fight.PlayerPowersAre(BuffId.Focus);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Defy_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Defy --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(138), Card(132), Card(473), Card(49), Card(473), Card(IC.AscendersBane))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(132), Card(132), Card(473), Card(132), Card(473), Card(524))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(6, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Weak, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Delay_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Delay --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(139), Card(473), Card(132), Card(132), Card(524), Card(473))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(473), Card(132), Card(IC.AscendersBane), Card(49), Card(132))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(11, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.NextTurnEnergy));
        fight.PlayerPowersAre(BuffId.NextTurnEnergy);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Demesne_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Demesne --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(140), Card(524), Card(132), Card(473), Card(49), Card(IC.AscendersBane))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(132), Card(132), Card(473), Card(473), Card(132), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(6, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Demesne));
        fight.PlayerPowersAre(BuffId.Demesne);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Devastate_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Devastate --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(143), Card(133), Card(133), Card(474), Card(474), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(IC.AscendersBane), Card(133), Card(532), Card(474), Card(179))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(5, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(60, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void DevourLife_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card DevourLife --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(144), Card(524), Card(473), Card(49), Card(IC.AscendersBane), Card(473))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(132), Card(132), Card(132), Card(473), Card(132))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.DevourLife));
        fight.PlayerPowersAre(BuffId.DevourLife);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Dirge_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Dirge --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(145), Card(132), Card(132), Card(473), Card(473), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(IC.AscendersBane), Card(132), Card(524), Card(473), Card(49))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(0, fight.State.Energy);
        Assert.Equal(15, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(28, fight.State.OstyHp);
        Assert.Equal(28, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Dismantle_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Dismantle --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(IC.Dismantle), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.AscendersBane))
            .PlayerHp(61, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.Bash))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(82, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void DoubleEnergy_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card DoubleEnergy --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(151), Card(130), Card(471), Card(130), Card(545), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(471), Card(IC.AscendersBane), Card(471), Card(130), Card(156), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(16, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void DrainPower_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card DrainPower --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(152), Card(132), Card(132), Card(473), Card(132), Card(IC.AscendersBane))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(524), Card(473), Card(49), Card(132), Card(473), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(80, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Dredge_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Dredge --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(154), Card(473), Card(132), Card(132), Card(473), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(132), Card(IC.AscendersBane), Card(524), Card(473), Card(49), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Dualcast_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Dualcast --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(156), Card(130), Card(IC.AscendersBane), Card(471), Card(156), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(545), Card(471), Card(471), Card(130), Card(471), Card(130))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Empty(fight.State.Orbs);
        fight.PlayerPowersAre();
        Assert.Equal(74, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void DyingStar_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card DyingStar --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(158), Card(133), Card(474), Card(474), Card(474), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(133), Card(IC.AscendersBane), Card(532), Card(179), Card(133))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(6, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(81, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(-9, fight.EnemyBuffAmount(BuffId.Strength, 0));
        Assert.Equal(9, fight.EnemyBuffAmount(BuffId.TemporaryStrength, 0));
    }

    [Fact]
    public void EchoForm_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card EchoForm --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(159), Card(130), Card(130), Card(156), Card(471), Card(545))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(IC.AscendersBane), Card(471), Card(130), Card(130), Card(471), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(6, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.EchoForm));
        fight.PlayerPowersAre(BuffId.EchoForm);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Eidolon_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Eidolon --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(161), Card(49), Card(132), Card(IC.AscendersBane), Card(473), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(132), Card(473), Card(473), Card(132), Card(524), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Equal(5, fight.State.ExhaustPile.Count);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void EndOfDays_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card EndOfDays --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(162), Card(132), Card(473), Card(524), Card(132), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(132), Card(49), Card(473), Card(473), Card(473), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(6, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(29, fight.EnemyBuffAmount(BuffId.Doom, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void EnergySurge_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card EnergySurge --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(163), Card(471), Card(130), Card(130), Card(130), Card(471))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(471), Card(545), Card(IC.AscendersBane), Card(471), Card(130), Card(156))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(10, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void EnfeeblingTouch_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card EnfeeblingTouch --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(164), Card(132), Card(473), Card(473), Card(473), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(132), Card(IC.AscendersBane), Card(524), Card(49), Card(132))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(-8, fight.EnemyBuffAmount(BuffId.Strength, 0));
        Assert.Equal(8, fight.EnemyBuffAmount(BuffId.TemporaryStrength, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Enlightenment_Base_CorpseSlugsWeak_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Enlightenment --encounter CorpseSlugsWeak --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(165), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.StrikeIronclad))
            .PlayerHp(64, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.AscendersBane), Card(IC.StrikeIronclad))
            .Enemy(defId: 17, hp: 28, maxHp: 28, buffs: [new BuffState(BuffId.Ravenous, 4)])
            .Enemy(defId: 17, hp: 29, maxHp: 29, buffs: [new BuffState(BuffId.Ravenous, 4)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(64, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(28, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 0));
        Assert.Equal(29, fight.State.Enemies[1].Hp);
        Assert.Equal(0, fight.State.Enemies[1].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 1));
    }

    [Fact]
    public void Entrench_Base_CorpseSlugsWeak_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Entrench --encounter CorpseSlugsWeak --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(167), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.StrikeIronclad))
            .PlayerHp(64, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.AscendersBane), Card(IC.StrikeIronclad))
            .Enemy(defId: 17, hp: 28, maxHp: 28, buffs: [new BuffState(BuffId.Ravenous, 4)])
            .Enemy(defId: 17, hp: 29, maxHp: 29, buffs: [new BuffState(BuffId.Ravenous, 4)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(64, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(28, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 0));
        Assert.Equal(29, fight.State.Enemies[1].Hp);
        Assert.Equal(0, fight.State.Enemies[1].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 1));
    }

    [Fact]
    public void Expose_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Expose --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(SI.Expose), Card(IC.AscendersBane), Card(IC.DefendIronclad), Card(IC.Bash), Card(IC.DefendIronclad), Card(IC.StrikeIronclad))
            .PlayerHp(61, 80)
            .Energy(9)
            .Draw(Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Vulnerable, 0));
    }

    [Fact]
    public void FallingStar_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card FallingStar --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(179), Card(133), Card(474), Card(179), Card(474), Card(IC.AscendersBane))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(133), Card(133), Card(474), Card(133), Card(474), Card(532))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 3);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(1, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(82, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Weak, 0));
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Vulnerable, 0));
    }

    [Fact]
    public void FanOfKnives_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card FanOfKnives --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(SI.FanOfKnives), Card(IC.DefendIronclad), Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.AscendersBane))
            .PlayerHp(61, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.FanOfKnives));
        fight.PlayerPowersAre(BuffId.FanOfKnives);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Fasten_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Fasten --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(CL.Fasten), Card(49), Card(132), Card(473), Card(132), Card(473))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(132), Card(473), Card(524), Card(132), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(4, fight.PlayerBuffAmount(BuffId.FastenPower));
        fight.PlayerPowersAre(BuffId.FastenPower);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Fear_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Fear --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(182), Card(132), Card(473), Card(473), Card(132), Card(49))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(524), Card(132), Card(132), Card(IC.AscendersBane), Card(473), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(83, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Vulnerable, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void FeedingFrenzy_Base_CorpseSlugsWeak_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card FeedingFrenzy --encounter CorpseSlugsWeak --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(184), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.StrikeIronclad))
            .PlayerHp(64, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.AscendersBane), Card(IC.StrikeIronclad))
            .Enemy(defId: 17, hp: 28, maxHp: 28, buffs: [new BuffState(BuffId.Ravenous, 4)])
            .Enemy(defId: 17, hp: 29, maxHp: 29, buffs: [new BuffState(BuffId.Ravenous, 4)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(64, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(5, fight.PlayerBuffAmount(BuffId.Strength));
        Assert.Equal(5, fight.PlayerBuffAmount(BuffId.TemporaryStrength));
        fight.PlayerPowersAre(BuffId.Strength, BuffId.TemporaryStrength);
        Assert.Equal(28, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 0));
        Assert.Equal(29, fight.State.Enemies[1].Hp);
        Assert.Equal(0, fight.State.Enemies[1].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 1));
    }

    [Fact]
    public void Feral_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Feral --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(186), Card(130), Card(471), Card(130), Card(471), Card(471))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(471), Card(IC.AscendersBane), Card(156), Card(130), Card(130), Card(545))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Feral));
        fight.PlayerPowersAre(BuffId.Feral);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Feral_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Feral --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(186), Card(132), Card(132), Card(473), Card(473), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(IC.AscendersBane), Card(132), Card(524), Card(473), Card(49))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Feral));
        fight.PlayerPowersAre(BuffId.Feral);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Fetch_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Fetch --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(187), Card(49), Card(132), Card(473), Card(473), Card(473))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(IC.AscendersBane), Card(524), Card(473), Card(132), Card(132), Card(132))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(5, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(87, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void FiendFire_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card FiendFire --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(IC.FiendFire), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.StrikeIronclad))
            .PlayerHp(64, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.AscendersBane), Card(IC.StrikeIronclad))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(64, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Equal(6, fight.State.ExhaustPile.Count);
        fight.PlayerPowersAre();
        Assert.Equal(55, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void FightThrough_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card FightThrough --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(190), Card(471), Card(471), Card(545), Card(130), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(471), Card(130), Card(471), Card(130), Card(IC.AscendersBane), Card(156))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(13, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Equal(3, fight.State.DiscardPile.Count);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Finesse_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Finesse --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(CL.Finesse), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad))
            .PlayerHp(61, 80)
            .Energy(9)
            .Draw(Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.AscendersBane), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.Bash))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(4, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(5, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void FlakCannon_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card FlakCannon --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(194), Card(130), Card(130), Card(471), Card(471), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(471), Card(IC.AscendersBane), Card(545), Card(471), Card(156))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void FlashOfSteel_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card FlashOfSteel --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(CL.FlashOfSteel), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad))
            .PlayerHp(61, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.AscendersBane), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.Bash))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(5, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(85, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Flatten_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Flatten --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(198), Card(132), Card(473), Card(473), Card(473), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(132), Card(IC.AscendersBane), Card(524), Card(49), Card(132))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(78, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Flechettes_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Flechettes --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(SI.Flechettes), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad))
            .PlayerHp(61, 80)
            .Energy(9)
            .Draw(Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.AscendersBane), Card(IC.DefendIronclad))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(80, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void FocusedStrike_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card FocusedStrike --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(201), Card(156), Card(471), Card(130), Card(471), Card(IC.AscendersBane))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(545), Card(471), Card(471), Card(130), Card(130), Card(130))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Focus));
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.TemporaryFocus));
        fight.PlayerPowersAre(BuffId.Focus, BuffId.TemporaryFocus);
        Assert.Equal(81, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void ForbiddenGrimoire_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card ForbiddenGrimoire --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(203), Card(132), Card(473), Card(524), Card(473), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(132), Card(132), Card(IC.AscendersBane), Card(49), Card(473), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.ForbiddenGrimoire));
        fight.PlayerPowersAre(BuffId.ForbiddenGrimoire);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void ForegoneConclusion_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card ForegoneConclusion --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(204), Card(133), Card(474), Card(474), Card(133), Card(179))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(532), Card(133), Card(133), Card(IC.AscendersBane), Card(474), Card(474))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.ForegoneConclusion));
        fight.PlayerPowersAre(BuffId.ForegoneConclusion);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void ForgottenRitual_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card ForgottenRitual --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(IC.ForgottenRitual), Card(132), Card(132), Card(473), Card(473), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(IC.AscendersBane), Card(132), Card(524), Card(473), Card(49))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Friendship_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Friendship --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(207), Card(473), Card(49), Card(132), Card(IC.AscendersBane), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(524), Card(132), Card(132), Card(473), Card(473), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(-2, fight.PlayerBuffAmount(BuffId.Strength));
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Friendship));
        fight.PlayerPowersAre(BuffId.Strength, BuffId.Friendship);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Ftl_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Ftl --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(208), Card(471), Card(130), Card(471), Card(471), Card(471))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(130), Card(IC.AscendersBane), Card(130), Card(545), Card(156))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(5, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(85, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Fuel_Base_CorpseSlugsWeak_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Fuel --encounter CorpseSlugsWeak --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(209), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.StrikeIronclad))
            .PlayerHp(64, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.AscendersBane), Card(IC.StrikeIronclad))
            .Enemy(defId: 17, hp: 28, maxHp: 28, buffs: [new BuffState(BuffId.Ravenous, 4)])
            .Enemy(defId: 17, hp: 29, maxHp: 29, buffs: [new BuffState(BuffId.Ravenous, 4)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(64, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(10, fight.State.Energy);
        Assert.Equal(5, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(28, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 0));
        Assert.Equal(29, fight.State.Enemies[1].Hp);
        Assert.Equal(0, fight.State.Enemies[1].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 1));
    }

    [Fact]
    public void Furnace_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Furnace --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(210), Card(474), Card(179), Card(133), Card(IC.AscendersBane), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(532), Card(133), Card(133), Card(474), Card(474), Card(474))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        Assert.Equal(5, fight.PlayerBuffAmount(BuffId.Furnace));
        fight.PlayerPowersAre(BuffId.Furnace);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Fusion_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Fusion --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(211), Card(471), Card(471), Card(130), Card(130), Card(471))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(IC.AscendersBane), Card(471), Card(130), Card(156), Card(545), Card(130))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Equal(2, fight.State.Orbs.Count);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(OrbType.Plasma, fight.State.Orbs[1].Type);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void GammaBlast_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card GammaBlast --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(212), Card(474), Card(133), Card(133), Card(179), Card(474))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(133), Card(474), Card(474), Card(532), Card(IC.AscendersBane), Card(133))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(6, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(77, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Weak, 0));
        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Vulnerable, 0));
    }

    [Fact]
    public void GatherLight_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card GatherLight --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(214), Card(IC.AscendersBane), Card(133), Card(474), Card(474), Card(179))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(532), Card(474), Card(133), Card(133), Card(133), Card(474))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(8, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(10, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Genesis_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Genesis --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(215), Card(133), Card(474), Card(133), Card(474), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(133), Card(532), Card(IC.AscendersBane), Card(179), Card(474), Card(474))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.Genesis));
        fight.PlayerPowersAre(BuffId.Genesis);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void GeneticAlgorithm_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card GeneticAlgorithm --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(216), Card(471), Card(471), Card(471), Card(156), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(545), Card(130), Card(130), Card(IC.AscendersBane), Card(130), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(1, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Glacier_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Glacier --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(218), Card(130), Card(471), Card(471), Card(471), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(471), Card(130), Card(130), Card(156), Card(545), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(6, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Equal(3, fight.State.Orbs.Count);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(OrbType.Frost, fight.State.Orbs[1].Type);
        Assert.Equal(OrbType.Frost, fight.State.Orbs[2].Type);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Glasswork_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Glasswork --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(219), Card(130), Card(471), Card(156), Card(IC.AscendersBane), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(471), Card(471), Card(471), Card(130), Card(545))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(5, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Equal(2, fight.State.Orbs.Count);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(OrbType.Glass, fight.State.Orbs[1].Type);
        Assert.Equal(4, fight.State.Orbs[1].PassiveValue);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void GlimpseBeyond_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card GlimpseBeyond --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(221), Card(49), Card(132), Card(473), Card(473), Card(473))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(IC.AscendersBane), Card(524), Card(473), Card(132), Card(132), Card(132))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(9, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Glitterstream_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Glitterstream --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(222), Card(133), Card(474), Card(133), Card(179), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(IC.AscendersBane), Card(474), Card(133), Card(532), Card(474))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(11, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        Assert.Equal(5, fight.PlayerBuffAmount(BuffId.BlockNextTurn));
        fight.PlayerPowersAre(BuffId.BlockNextTurn);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Glow_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Glow --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(223), Card(133), Card(133), Card(474), Card(474), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(IC.AscendersBane), Card(133), Card(532), Card(474), Card(179))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(5, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(10, fight.State.Stars);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.NextTurnDraw));
        fight.PlayerPowersAre(BuffId.NextTurnDraw);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void GoForTheEyes_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card GoForTheEyes --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(224), Card(471), Card(IC.AscendersBane), Card(130), Card(545), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(130), Card(471), Card(156), Card(471), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(87, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Weak, 0));
    }

    [Fact]
    public void GoldAxe_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card GoldAxe --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(CL.GoldAxe), Card(IC.StrikeIronclad), Card(IC.AscendersBane), Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad))
            .PlayerHp(61, 80)
            .Energy(9)
            .Draw(Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void GraveWarden_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card GraveWarden --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(228), Card(49), Card(132), Card(473), Card(132), Card(473))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(132), Card(473), Card(524), Card(132), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(8, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(7, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Graveblast_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Graveblast --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(227), Card(473), Card(132), Card(132), Card(524), Card(473))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(473), Card(132), Card(IC.AscendersBane), Card(49), Card(132))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(86, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void GuidingStar_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card GuidingStar --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(230), Card(133), Card(133), Card(474), Card(474), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(IC.AscendersBane), Card(133), Card(532), Card(474), Card(179))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(4, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(7, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(78, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void GunkUp_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card GunkUp --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(231), Card(130), Card(471), Card(130), Card(471), Card(545))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(156), Card(471), Card(130), Card(IC.AscendersBane), Card(130), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Equal(2, fight.State.DiscardPile.Count);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(78, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Hailstorm_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Hailstorm --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(232), Card(130), Card(130), Card(156), Card(471), Card(471))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(IC.AscendersBane), Card(545), Card(130), Card(471), Card(471), Card(130))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(6, fight.PlayerBuffAmount(BuffId.Hailstorm));
        fight.PlayerPowersAre(BuffId.Hailstorm);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void HammerTime_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card HammerTime --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(233), Card(179), Card(133), Card(474), Card(133), Card(474))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(133), Card(474), Card(532), Card(133), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.HammerTime));
        fight.PlayerPowersAre(BuffId.HammerTime);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void HandOfGreed_Base_CorpseSlugsWeak_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card HandOfGreed --encounter CorpseSlugsWeak --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(CL.HandOfGreed), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.StrikeIronclad))
            .PlayerHp(64, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.AscendersBane), Card(IC.StrikeIronclad))
            .Enemy(defId: 17, hp: 28, maxHp: 28, buffs: [new BuffState(BuffId.Ravenous, 4)])
            .Enemy(defId: 17, hp: 29, maxHp: 29, buffs: [new BuffState(BuffId.Ravenous, 4)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(64, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(8, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 0));
        Assert.Equal(29, fight.State.Enemies[1].Hp);
        Assert.Equal(0, fight.State.Enemies[1].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 1));
    }

    [Fact]
    public void Hang_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Hang --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(236), Card(132), Card(IC.AscendersBane), Card(473), Card(524), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(49), Card(473), Card(473), Card(132), Card(473), Card(132))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(80, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Hang, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Haunt_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Haunt --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(237), Card(473), Card(132), Card(473), Card(132), Card(49))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(524), Card(473), Card(IC.AscendersBane), Card(473), Card(132), Card(132))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(6, fight.PlayerBuffAmount(BuffId.Haunt));
        fight.PlayerPowersAre(BuffId.Haunt);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Hegemony_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Hegemony --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(242), Card(179), Card(133), Card(474), Card(532), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(133), Card(474), Card(133), Card(IC.AscendersBane), Card(474))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.NextTurnEnergy));
        fight.PlayerPowersAre(BuffId.NextTurnEnergy);
        Assert.Equal(75, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void HeirloomHammer_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card HeirloomHammer --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(243), Card(133), Card(133), Card(474), Card(474), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(IC.AscendersBane), Card(133), Card(532), Card(474), Card(179))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(70, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void HelixDrill_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card HelixDrill --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(244), Card(IC.AscendersBane), Card(156), Card(471), Card(545), Card(471))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(471), Card(130), Card(130), Card(471), Card(130))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void HelloWorld_Base_CorpseSlugsWeak_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card HelloWorld --encounter CorpseSlugsWeak --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(245), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.StrikeIronclad))
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
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.HelloWorld));
        fight.PlayerPowersAre(BuffId.HelloWorld);
        Assert.Equal(28, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 0));
        Assert.Equal(29, fight.State.Enemies[1].Hp);
        Assert.Equal(0, fight.State.Enemies[1].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 1));
    }

    [Fact]
    public void Hemokinesis_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Hemokinesis --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(IC.Hemokinesis), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad))
            .PlayerHp(64, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.DefendIronclad), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(62, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(75, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void HiddenCache_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card HiddenCache --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(248), Card(133), Card(IC.AscendersBane), Card(474), Card(532), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(179), Card(474), Card(474), Card(133), Card(474), Card(133))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(10, fight.State.Stars);
        Assert.Equal(3, fight.PlayerBuffAmount(BuffId.StarNextTurn));
        fight.PlayerPowersAre(BuffId.StarNextTurn);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void HighFive_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card HighFive --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(251), Card(473), Card(49), Card(132), Card(IC.AscendersBane), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(524), Card(132), Card(132), Card(473), Card(473), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(79, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Vulnerable, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Hologram_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Hologram --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(252), Card(471), Card(IC.AscendersBane), Card(156), Card(471), Card(471))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(545), Card(130), Card(130), Card(130), Card(130), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(3, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Hotfix_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Hotfix --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(253), Card(130), Card(471), Card(471), Card(471), Card(156))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(545), Card(IC.AscendersBane), Card(130), Card(471), Card(130), Card(130))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.Focus));
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.TemporaryFocus));
        fight.PlayerPowersAre(BuffId.Focus, BuffId.TemporaryFocus);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Hyperbeam_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Hyperbeam --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(256), Card(156), Card(471), Card(545), Card(130), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(IC.AscendersBane), Card(471), Card(130), Card(130), Card(471), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(-3, fight.PlayerBuffAmount(BuffId.Focus));
        fight.PlayerPowersAre(BuffId.Focus);
        Assert.Equal(62, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void IAmInvincible_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card IAmInvincible --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(257), Card(179), Card(133), Card(474), Card(133), Card(474))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(133), Card(474), Card(532), Card(133), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(10, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void IceLance_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card IceLance --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(258), Card(130), Card(IC.AscendersBane), Card(471), Card(471), Card(471))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(545), Card(130), Card(130), Card(130), Card(156), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(6, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Equal(3, fight.State.Orbs.Count);
        Assert.Equal(OrbType.Frost, fight.State.Orbs[0].Type);
        Assert.Equal(OrbType.Frost, fight.State.Orbs[1].Type);
        Assert.Equal(OrbType.Frost, fight.State.Orbs[2].Type);
        fight.PlayerPowersAre();
        Assert.Equal(63, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Invoke_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Invoke --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(267), Card(473), Card(49), Card(132), Card(IC.AscendersBane), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(132), Card(132), Card(473), Card(524), Card(473), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.SummonNextTurn));
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.NextTurnEnergy));
        fight.PlayerPowersAre(BuffId.SummonNextTurn, BuffId.NextTurnEnergy);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void IronWave_Base_CorpseSlugsWeak_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card IronWave --encounter CorpseSlugsWeak --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(IC.IronWave), Card(IC.DefendIronclad), Card(IC.AscendersBane), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.DefendIronclad))
            .PlayerHp(64, 80)
            .Energy(9)
            .Draw(Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad))
            .Enemy(defId: 17, hp: 28, maxHp: 28, buffs: [new BuffState(BuffId.Ravenous, 4)])
            .Enemy(defId: 17, hp: 27, maxHp: 27, buffs: [new BuffState(BuffId.Ravenous, 4)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(64, fight.State.PlayerHp);
        Assert.Equal(5, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(23, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 0));
        Assert.Equal(27, fight.State.Enemies[1].Hp);
        Assert.Equal(0, fight.State.Enemies[1].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 1));
    }

    [Fact]
    public void Iteration_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Iteration --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(269), Card(130), Card(130), Card(471), Card(471), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(471), Card(IC.AscendersBane), Card(130), Card(156), Card(471), Card(545))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.Iteration));
        fight.PlayerPowersAre(BuffId.Iteration);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Juggernaut_Base_CorpseSlugsWeak_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Juggernaut --encounter CorpseSlugsWeak --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(IC.Juggernaut), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.StrikeIronclad))
            .PlayerHp(64, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.AscendersBane), Card(IC.StrikeIronclad))
            .Enemy(defId: 17, hp: 28, maxHp: 28, buffs: [new BuffState(BuffId.Ravenous, 4)])
            .Enemy(defId: 17, hp: 29, maxHp: 29, buffs: [new BuffState(BuffId.Ravenous, 4)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(64, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(6, fight.PlayerBuffAmount(BuffId.Juggernaut));
        fight.PlayerPowersAre(BuffId.Juggernaut);
        Assert.Equal(28, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 0));
        Assert.Equal(29, fight.State.Enemies[1].Hp);
        Assert.Equal(0, fight.State.Enemies[1].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 1));
    }

    [Fact]
    public void Juggernaut_Upgraded_CorpseSlugsWeak_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Juggernaut --upgraded --encounter CorpseSlugsWeak --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(IC.Juggernaut, upgraded: true), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.StrikeIronclad))
            .PlayerHp(64, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.AscendersBane), Card(IC.StrikeIronclad))
            .Enemy(defId: 17, hp: 28, maxHp: 28, buffs: [new BuffState(BuffId.Ravenous, 4)])
            .Enemy(defId: 17, hp: 29, maxHp: 29, buffs: [new BuffState(BuffId.Ravenous, 4)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(64, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(8, fight.PlayerBuffAmount(BuffId.Juggernaut));
        fight.PlayerPowersAre(BuffId.Juggernaut);
        Assert.Equal(28, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 0));
        Assert.Equal(29, fight.State.Enemies[1].Hp);
        Assert.Equal(0, fight.State.Enemies[1].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 1));
    }

    [Fact]
    public void KinglyKick_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card KinglyKick --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(274), Card(133), Card(IC.AscendersBane), Card(474), Card(532), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(179), Card(474), Card(474), Card(133), Card(474), Card(133))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(5, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(63, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void KinglyPunch_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card KinglyPunch --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(275), Card(133), Card(474), Card(179), Card(474), Card(IC.AscendersBane))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(133), Card(133), Card(474), Card(133), Card(474), Card(532))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(82, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void KnockoutBlow_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card KnockoutBlow --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(278), Card(474), Card(133), Card(133), Card(532), Card(474))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(474), Card(133), Card(IC.AscendersBane), Card(179), Card(133))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(6, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(60, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void KnowThyPlace_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card KnowThyPlace --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(279), Card(133), Card(133), Card(474), Card(133), Card(IC.AscendersBane))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(532), Card(474), Card(179), Card(133), Card(474), Card(474))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Weak, 0));
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Vulnerable, 0));
    }

    [Fact]
    public void Leap_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Leap --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(282), Card(545), Card(130), Card(471), Card(130), Card(471))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(471), Card(130), Card(471), Card(156), Card(130), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(9, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void LegionOfBone_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card LegionOfBone --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(283), Card(132), Card(132), Card(473), Card(132), Card(IC.AscendersBane))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(524), Card(473), Card(49), Card(132), Card(473), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(7, fight.State.OstyHp);
        Assert.Equal(7, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Lethality_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Lethality --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(285), Card(49), Card(473), Card(IC.AscendersBane), Card(473), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(132), Card(473), Card(132), Card(132), Card(473), Card(524))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(50, fight.PlayerBuffAmount(BuffId.Lethality));
        fight.PlayerPowersAre(BuffId.Lethality);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void LightningRod_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card LightningRod --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(287), Card(130), Card(IC.AscendersBane), Card(471), Card(156), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(545), Card(471), Card(471), Card(130), Card(471), Card(130))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(4, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.LightningRod));
        fight.PlayerPowersAre(BuffId.LightningRod);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Loop_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Loop --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(288), Card(130), Card(471), Card(545), Card(471), Card(IC.AscendersBane))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(130), Card(471), Card(130), Card(471), Card(156))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Loop));
        fight.PlayerPowersAre(BuffId.Loop);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void LunarBlast_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card LunarBlast --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(290), Card(133), Card(133), Card(474), Card(474), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(IC.AscendersBane), Card(133), Card(532), Card(474), Card(179))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void MachineLearning_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card MachineLearning --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(291), Card(471), Card(130), Card(130), Card(156), Card(471))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(471), Card(471), Card(130), Card(IC.AscendersBane), Card(545), Card(130))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.MachineLearning));
        fight.PlayerPowersAre(BuffId.MachineLearning);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void MakeItSo_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card MakeItSo --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(293), Card(179), Card(133), Card(474), Card(133), Card(474))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(133), Card(474), Card(532), Card(133), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(84, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void ManifestAuthority_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card ManifestAuthority --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(296), Card(133), Card(IC.AscendersBane), Card(474), Card(532), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(179), Card(474), Card(474), Card(133), Card(474), Card(133))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(7, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Maul_Base_CorpseSlugsWeak_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Maul --encounter CorpseSlugsWeak --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(299), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.StrikeIronclad))
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
        fight.PlayerPowersAre();
        Assert.Equal(18, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 0));
        Assert.Equal(29, fight.State.Enemies[1].Hp);
        Assert.Equal(0, fight.State.Enemies[1].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 1));
    }

    [Fact]
    public void Melancholy_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Melancholy --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(301), Card(132), Card(473), Card(49), Card(IC.AscendersBane), Card(473))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(132), Card(473), Card(132), Card(132), Card(524))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(13, fight.State.PlayerBlock);
        Assert.Equal(6, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void MeteorShower_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card MeteorShower --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(304), Card(133), Card(474), Card(179), Card(474), Card(IC.AscendersBane))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(133), Card(133), Card(474), Card(133), Card(474), Card(532))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(7, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(76, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Weak, 0));
        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Vulnerable, 0));
    }

    [Fact]
    public void MeteorStrike_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card MeteorStrike --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(305), Card(130), Card(130), Card(471), Card(130), Card(IC.AscendersBane))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(156), Card(471), Card(545), Card(130), Card(471), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(4, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Equal(3, fight.State.Orbs.Count);
        Assert.Equal(OrbType.Plasma, fight.State.Orbs[0].Type);
        Assert.Equal(OrbType.Plasma, fight.State.Orbs[1].Type);
        Assert.Equal(OrbType.Plasma, fight.State.Orbs[2].Type);
        fight.PlayerPowersAre();
        Assert.Equal(58, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void MindBlast_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card MindBlast --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(CL.MindBlast), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.Bash))
            .PlayerHp(61, 80)
            .Energy(9)
            .Draw(Card(IC.DefendIronclad), Card(IC.AscendersBane), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(84, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Misery_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Misery --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(312), Card(132), Card(49), Card(524), Card(473), Card(473))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(132), Card(IC.AscendersBane), Card(473), Card(132), Card(132), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(83, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Modded_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Modded --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(547), Card(471), Card(130), Card(130), Card(471), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(IC.AscendersBane), Card(156), Card(471), Card(545), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(5, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(4, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void MoltenFist_Base_CorpseSlugsWeak_Ironclad_MatchesLiveCapture()
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
        fight.PlayerPowersAre();
        Assert.Equal(18, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 0));
        Assert.Equal(29, fight.State.Enemies[1].Hp);
        Assert.Equal(0, fight.State.Enemies[1].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 1));
    }

    [Fact]
    public void MoltenFist_Upgraded_CorpseSlugsWeak_Ironclad_MatchesLiveCapture()
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
        fight.PlayerPowersAre();
        Assert.Equal(14, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 0));
        Assert.Equal(29, fight.State.Enemies[1].Hp);
        Assert.Equal(0, fight.State.Enemies[1].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 1));
    }

    [Fact]
    public void MoltenFist_Base_Vulnerable_CorpseSlugsWeak_Ironclad_MatchesLiveCapture()
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
        fight.PlayerPowersAre();
        Assert.Equal(13, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 0));
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Vulnerable, 0));
        Assert.Equal(29, fight.State.Enemies[1].Hp);
        Assert.Equal(0, fight.State.Enemies[1].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 1));
    }

    [Fact]
    public void MomentumStrike_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card MomentumStrike --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(314), Card(545), Card(130), Card(IC.AscendersBane), Card(471), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(471), Card(471), Card(130), Card(156), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(80, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void MonarchsGaze_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card MonarchsGaze --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(315), Card(474), Card(133), Card(133), Card(532), Card(474))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(474), Card(133), Card(IC.AscendersBane), Card(179), Card(133))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.MonarchsGaze));
        fight.PlayerPowersAre(BuffId.MonarchsGaze);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Monologue_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Monologue --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(316), Card(133), Card(133), Card(474), Card(133), Card(IC.AscendersBane))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(532), Card(474), Card(179), Card(133), Card(474), Card(474))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.MonologueApplied));
        fight.PlayerPowersAre(BuffId.MonologueApplied, BuffId.Monologue);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void MultiCast_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card MultiCast --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(317), Card(130), Card(471), Card(156), Card(130), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(545), Card(471), Card(471), Card(471), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(0, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Empty(fight.State.Orbs);
        fight.PlayerPowersAre();
        Assert.Equal(18, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void NecroMastery_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card NecroMastery --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(319), Card(49), Card(132), Card(473), Card(524), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(132), Card(473), Card(132), Card(IC.AscendersBane), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.NecroMastery));
        fight.PlayerPowersAre(BuffId.NecroMastery);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(6, fight.State.OstyHp);
        Assert.Equal(6, fight.State.OstyMaxHp);
    }

    [Fact]
    public void NegativePulse_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card NegativePulse --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(320), Card(49), Card(132), Card(473), Card(IC.AscendersBane), Card(473))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(132), Card(473), Card(524), Card(132), Card(132))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(5, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(7, fight.EnemyBuffAmount(BuffId.Doom, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Neurosurge_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Neurosurge --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(322), Card(473), Card(49), Card(132), Card(473), Card(IC.AscendersBane))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(132), Card(132), Card(473), Card(132), Card(473), Card(524))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(12, fight.State.Energy);
        Assert.Equal(4, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.PlayerBuffAmount(BuffId.Neurosurge));
        fight.PlayerPowersAre(BuffId.Neurosurge);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Neutralize_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Neutralize --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(SI.Neutralize), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.DefendIronclad))
            .PlayerHp(61, 80)
            .Energy(9)
            .Draw(Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(87, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Weak, 0));
    }

    [Fact]
    public void NeutronAegis_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card NeutronAegis --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(324), Card(179), Card(133), Card(474), Card(133), Card(474))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(133), Card(474), Card(532), Card(133), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(4, fight.State.Stars);
        Assert.Equal(8, fight.PlayerBuffAmount(BuffId.Plating));
        fight.PlayerPowersAre(BuffId.Plating);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void NoEscape_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card NoEscape --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(326), Card(473), Card(132), Card(49), Card(473), Card(473))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(132), Card(524), Card(IC.AscendersBane), Card(132), Card(132))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(10, fight.EnemyBuffAmount(BuffId.Doom, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Nostalgia_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Nostalgia --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(IC.Nostalgia), Card(132), Card(IC.AscendersBane), Card(473), Card(524), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(49), Card(473), Card(473), Card(132), Card(473), Card(132))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Nostalgia));
        fight.PlayerPowersAre(BuffId.Nostalgia);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Null_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Null --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(330), Card(130), Card(471), Card(471), Card(471), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(471), Card(130), Card(IC.AscendersBane), Card(156), Card(545), Card(130))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Equal(2, fight.State.Orbs.Count);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(OrbType.Dark, fight.State.Orbs[1].Type);
        Assert.Equal(6, fight.State.Orbs[1].EvokeValue);
        fight.PlayerPowersAre();
        Assert.Equal(80, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Weak, 0));
    }

    [Fact]
    public void Oblivion_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Oblivion --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(331), Card(473), Card(473), Card(132), Card(49), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(132), Card(132), Card(473), Card(IC.AscendersBane), Card(473), Card(524))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(3, fight.EnemyBuffAmount(BuffId.Oblivion, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Omnislice_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Omnislice --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(CL.Omnislice), Card(IC.AscendersBane), Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad))
            .PlayerHp(61, 80)
            .Energy(9)
            .Draw(Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(82, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Orbit_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Orbit --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(335), Card(474), Card(133), Card(133), Card(474), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(133), Card(IC.AscendersBane), Card(532), Card(474), Card(179), Card(474))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        Assert.True(fight.PlayerBuffAmount(BuffId.Orbit) > 0, "the game reported ORBIT_POWER and the emulator has none");
        fight.PlayerPowersAre(BuffId.Orbit);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Outbreak_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Outbreak --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(SI.Outbreak), Card(132), Card(132), Card(132), Card(473), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(473), Card(524), Card(IC.AscendersBane), Card(473), Card(49))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.OutbreakCounter));
        fight.PlayerPowersAre(BuffId.OutbreakCounter, BuffId.Outbreak);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Overclock_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Overclock --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(338), Card(130), Card(471), Card(471), Card(130), Card(545))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(156), Card(130), Card(130), Card(IC.AscendersBane), Card(471), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(4, fight.State.DrawPile.Count);
        Assert.Equal(2, fight.State.DiscardPile.Count);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void PactsEnd_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card PactsEnd --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(IC.PactsEnd), Card(49), Card(473), Card(524), Card(132), Card(473))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(132), Card(473), Card(132), Card(132), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Pagestorm_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Pagestorm --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(340), Card(49), Card(IC.AscendersBane), Card(132), Card(473), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(132), Card(132), Card(524), Card(473), Card(473), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Pagestorm));
        fight.PlayerPowersAre(BuffId.Pagestorm);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void PaleBlueDot_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card PaleBlueDot --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(341), Card(179), Card(133), Card(IC.AscendersBane), Card(474), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(133), Card(474), Card(474), Card(133), Card(532), Card(474))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.PaleBlueDot));
        fight.PlayerPowersAre(BuffId.PaleBlueDot);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Parry_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Parry --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(344), Card(IC.AscendersBane), Card(133), Card(532), Card(179), Card(474))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(133), Card(474), Card(133), Card(474), Card(133), Card(474))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        Assert.Equal(10, fight.PlayerBuffAmount(BuffId.Parry));
        fight.PlayerPowersAre(BuffId.Parry);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Parse_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Parse --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(345), Card(132), Card(IC.AscendersBane), Card(473), Card(473), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(524), Card(132), Card(473), Card(473), Card(49), Card(132))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(3, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void ParticleWall_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card ParticleWall --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(346), Card(133), Card(474), Card(532), Card(133), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(133), Card(179), Card(474), Card(474), Card(474), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(9, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(7, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Patter_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Patter --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(347), Card(49), Card(132), Card(473), Card(132), Card(473))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(132), Card(473), Card(524), Card(132), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(8, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.Vigor));
        fight.PlayerPowersAre(BuffId.Vigor);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Patter_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Patter --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(347), Card(133), Card(474), Card(474), Card(474), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(133), Card(IC.AscendersBane), Card(532), Card(179), Card(133))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(8, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.Vigor));
        fight.PlayerPowersAre(BuffId.Vigor);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void PerfectedStrike_Base_CorpseSlugsWeak_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card PerfectedStrike --encounter CorpseSlugsWeak --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(IC.PerfectedStrike), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.AscendersBane))
            .PlayerHp(64, 80)
            .Energy(9)
            .Draw(Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad))
            .Enemy(defId: 17, hp: 29, maxHp: 29, buffs: [new BuffState(BuffId.Ravenous, 4)])
            .Enemy(defId: 17, hp: 27, maxHp: 27, buffs: [new BuffState(BuffId.Ravenous, 4)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(64, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(11, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 0));
        Assert.Equal(27, fight.State.Enemies[1].Hp);
        Assert.Equal(0, fight.State.Enemies[1].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 1));
    }

    [Fact]
    public void PhantomBlades_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card PhantomBlades --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(SI.PhantomBlades), Card(132), Card(473), Card(524), Card(132), Card(IC.AscendersBane))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(49), Card(473), Card(473), Card(132), Card(132))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.PlayerBuffAmount(BuffId.PhantomBlades));
        fight.PlayerPowersAre(BuffId.PhantomBlades);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void PillarOfCreation_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card PillarOfCreation --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(354), Card(133), Card(133), Card(474), Card(474), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(IC.AscendersBane), Card(133), Card(532), Card(474), Card(179))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        Assert.Equal(3, fight.PlayerBuffAmount(BuffId.PillarOfCreation));
        fight.PlayerPowersAre(BuffId.PillarOfCreation);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void PoisonedStab_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card PoisonedStab --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(SI.PoisonedStab), Card(IC.AscendersBane), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad))
            .PlayerHp(61, 80)
            .Energy(9)
            .Draw(Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(84, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(3, fight.EnemyBuffAmount(BuffId.Poison, 0));
    }

    [Fact]
    public void Poke_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Poke --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(357), Card(132), Card(132), Card(473), Card(132), Card(IC.AscendersBane))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(524), Card(473), Card(49), Card(132), Card(473), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(84, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Pounce_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Pounce --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(SI.Pounce), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad))
            .PlayerHp(61, 80)
            .Energy(9)
            .Draw(Card(IC.DefendIronclad), Card(IC.Bash), Card(IC.AscendersBane), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.FreeSkillPower));
        fight.PlayerPowersAre(BuffId.FreeSkillPower);
        Assert.Equal(76, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Prolong_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Prolong --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(CL.Prolong), Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad))
            .PlayerHp(61, 80)
            .Energy(9)
            .Draw(Card(IC.AscendersBane), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Prolong_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Prolong --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(CL.Prolong), Card(132), Card(132), Card(473), Card(473), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(IC.AscendersBane), Card(132), Card(524), Card(473), Card(49))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Prophesize_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Prophesize --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(367), Card(179), Card(133), Card(474), Card(133), Card(474))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(133), Card(474), Card(532), Card(133), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Single(fight.State.DrawPile);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Protector_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Protector --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(368), Card(49), Card(132), Card(473), Card(132), Card(473))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(132), Card(473), Card(524), Card(132), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(79, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void PullAggro_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card PullAggro --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(370), Card(473), Card(473), Card(IC.AscendersBane), Card(473), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(49), Card(132), Card(473), Card(524), Card(132), Card(132))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(7, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(5, fight.State.OstyHp);
        Assert.Equal(5, fight.State.OstyMaxHp);
    }

    [Fact]
    public void PullFromBelow_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card PullFromBelow --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(371), Card(132), Card(132), Card(473), Card(473), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(IC.AscendersBane), Card(132), Card(524), Card(473), Card(49))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Putrefy_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Putrefy --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(373), Card(473), Card(132), Card(473), Card(49), Card(473))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(524), Card(132), Card(132), Card(132), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Weak, 0));
        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Vulnerable, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Quadcast_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Quadcast --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(375), Card(471), Card(545), Card(130), Card(IC.AscendersBane), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(156), Card(130), Card(130), Card(471), Card(471), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Empty(fight.State.Orbs);
        fight.PlayerPowersAre();
        Assert.Equal(58, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Radiate_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Radiate --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(377), Card(179), Card(133), Card(474), Card(532), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(133), Card(474), Card(133), Card(IC.AscendersBane), Card(474))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(63, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Rainbow_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Rainbow --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(379), Card(545), Card(130), Card(471), Card(471), Card(471))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(IC.AscendersBane), Card(156), Card(471), Card(130), Card(130), Card(130))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Equal(3, fight.State.Orbs.Count);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(OrbType.Frost, fight.State.Orbs[1].Type);
        Assert.Equal(OrbType.Dark, fight.State.Orbs[2].Type);
        Assert.Equal(6, fight.State.Orbs[2].EvokeValue);
        fight.PlayerPowersAre();
        Assert.Equal(82, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Rampage_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Rampage --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(IC.Rampage), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.AscendersBane))
            .PlayerHp(61, 80)
            .Energy(9)
            .Draw(Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(81, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Rattle_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Rattle --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(382), Card(49), Card(132), Card(473), Card(132), Card(473))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(132), Card(473), Card(524), Card(132), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(83, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Reanimate_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Reanimate --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(383), Card(IC.AscendersBane), Card(49), Card(132), Card(473), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(132), Card(524), Card(473), Card(473), Card(132), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(6, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(21, fight.State.OstyHp);
        Assert.Equal(21, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Reap_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Reap --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(384), Card(132), Card(473), Card(49), Card(473), Card(IC.AscendersBane))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(132), Card(132), Card(473), Card(132), Card(473), Card(524))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(6, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(63, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void ReaperForm_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card ReaperForm --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(385), Card(473), Card(473), Card(132), Card(473), Card(49))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(132), Card(473), Card(132), Card(132), Card(IC.AscendersBane), Card(524))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(6, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.ReaperForm));
        fight.PlayerPowersAre(BuffId.ReaperForm);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Reave_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Reave --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(386), Card(49), Card(132), Card(473), Card(132), Card(473))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(132), Card(473), Card(524), Card(132), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(7, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(81, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Reboot_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Reboot --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(387), Card(156), Card(130), Card(130), Card(471), Card(IC.AscendersBane))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(545), Card(471), Card(471), Card(471), Card(130))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(7, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Rebound_Base_CorpseSlugsWeak_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Rebound --encounter CorpseSlugsWeak --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(388), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.StrikeIronclad))
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
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Rebound));
        fight.PlayerPowersAre(BuffId.Rebound);
        Assert.Equal(19, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 0));
        Assert.Equal(29, fight.State.Enemies[1].Hp);
        Assert.Equal(0, fight.State.Enemies[1].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 1));
    }

    [Fact]
    public void RefineBlade_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card RefineBlade --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(389), Card(532), Card(133), Card(133), Card(474), Card(IC.AscendersBane))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(133), Card(179), Card(474), Card(474), Card(474), Card(133))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.NextTurnEnergy));
        fight.PlayerPowersAre(BuffId.NextTurnEnergy);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Reflect_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Reflect --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(390), Card(133), Card(133), Card(474), Card(474), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(IC.AscendersBane), Card(133), Card(532), Card(474), Card(179))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(15, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(6, fight.State.Stars);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Reflect));
        fight.PlayerPowersAre(BuffId.Reflect);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Refract_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Refract --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(392), Card(130), Card(130), Card(IC.AscendersBane), Card(471), Card(471))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(156), Card(130), Card(471), Card(545), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(6, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Equal(3, fight.State.Orbs.Count);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(OrbType.Glass, fight.State.Orbs[1].Type);
        Assert.Equal(4, fight.State.Orbs[1].PassiveValue);
        Assert.Equal(OrbType.Glass, fight.State.Orbs[2].Type);
        Assert.Equal(4, fight.State.Orbs[2].PassiveValue);
        fight.PlayerPowersAre();
        Assert.Equal(72, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Rend_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Rend --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(CL.Rend), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad))
            .PlayerHp(61, 80)
            .Energy(9)
            .Draw(Card(IC.AscendersBane), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(75, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Resonance_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Resonance --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(395), Card(179), Card(133), Card(474), Card(133), Card(474))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(133), Card(474), Card(532), Card(133), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(6, fight.State.Stars);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Strength));
        fight.PlayerPowersAre(BuffId.Strength);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(-1, fight.EnemyBuffAmount(BuffId.Strength, 0));
    }

    [Fact]
    public void Restlessness_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Restlessness --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(IC.Restlessness), Card(132), Card(49), Card(IC.AscendersBane), Card(473), Card(524))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(132), Card(132), Card(132), Card(473), Card(473), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void RightHandHand_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card RightHandHand --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(398), Card(524), Card(132), Card(132), Card(473), Card(IC.AscendersBane))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(132), Card(49), Card(473), Card(473), Card(473), Card(132))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(86, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void RocketPunch_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card RocketPunch --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(400), Card(471), Card(471), Card(130), Card(156), Card(IC.AscendersBane))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(471), Card(130), Card(130), Card(471), Card(545), Card(130))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(5, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(77, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void RoyalGamble_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card RoyalGamble --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(402), Card(133), Card(IC.AscendersBane), Card(474), Card(532), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(179), Card(474), Card(474), Card(133), Card(474), Card(133))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        Assert.Equal(13, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Royalties_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Royalties --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(403), Card(133), Card(IC.AscendersBane), Card(474), Card(532), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(179), Card(474), Card(474), Card(133), Card(474), Card(133))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        Assert.Equal(30, fight.PlayerBuffAmount(BuffId.Royalties));
        fight.PlayerPowersAre(BuffId.Royalties);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Sacrifice_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Sacrifice --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(405), Card(473), Card(132), Card(473), Card(132), Card(524))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(132), Card(473), Card(IC.AscendersBane), Card(49), Card(132))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(2, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(0, fight.State.OstyHp);
    }

    [Fact]
    public void Scourge_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Scourge --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(409), Card(132), Card(132), Card(473), Card(473), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(IC.AscendersBane), Card(132), Card(524), Card(473), Card(49))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(5, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(13, fight.EnemyBuffAmount(BuffId.Doom, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Scrape_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Scrape --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(410), Card(130), Card(130), Card(471), Card(471), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(471), Card(IC.AscendersBane), Card(130), Card(156), Card(471), Card(545))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(2, fight.State.DrawPile.Count);
        Assert.Equal(5, fight.State.DiscardPile.Count);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(83, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Scrape_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Scrape --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(410), Card(132), Card(132), Card(473), Card(473), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(IC.AscendersBane), Card(132), Card(524), Card(473), Card(49))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(2, fight.State.DrawPile.Count);
        Assert.Equal(5, fight.State.DiscardPile.Count);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(83, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void SecondWind_Base_CorpseSlugsWeak_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card SecondWind --encounter CorpseSlugsWeak --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(IC.SecondWind), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.StrikeIronclad))
            .PlayerHp(64, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.AscendersBane), Card(IC.StrikeIronclad))
            .Enemy(defId: 17, hp: 28, maxHp: 28, buffs: [new BuffState(BuffId.Ravenous, 4)])
            .Enemy(defId: 17, hp: 29, maxHp: 29, buffs: [new BuffState(BuffId.Ravenous, 4)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(64, fight.State.PlayerHp);
        Assert.Equal(10, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Equal(2, fight.State.ExhaustPile.Count);
        fight.PlayerPowersAre();
        Assert.Equal(28, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 0));
        Assert.Equal(29, fight.State.Enemies[1].Hp);
        Assert.Equal(0, fight.State.Enemies[1].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 1));
    }

    [Fact]
    public void SeekingEdge_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card SeekingEdge --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(418), Card(474), Card(474), Card(133), Card(532), Card(IC.AscendersBane))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(133), Card(133), Card(474), Card(179), Card(133))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.SeekingEdge));
        fight.PlayerPowersAre(BuffId.SeekingEdge);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void SentryMode_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card SentryMode --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(419), Card(473), Card(132), Card(473), Card(132), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(524), Card(IC.AscendersBane), Card(132), Card(49), Card(473), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.SentryMode));
        fight.PlayerPowersAre(BuffId.SentryMode);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void SevenStars_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card SevenStars --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(422), Card(133), Card(474), Card(179), Card(474), Card(IC.AscendersBane))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(133), Card(133), Card(474), Card(133), Card(474), Card(532))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(2, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(41, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Severance_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Severance --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(423), Card(132), Card(132), Card(473), Card(473), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(IC.AscendersBane), Card(132), Card(524), Card(473), Card(49))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(7, fight.State.DrawPile.Count);
        Assert.Equal(2, fight.State.DiscardPile.Count);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(77, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void ShadowShield_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card ShadowShield --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(425), Card(545), Card(130), Card(471), Card(130), Card(471))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(471), Card(130), Card(471), Card(156), Card(130), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(11, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Equal(2, fight.State.Orbs.Count);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(OrbType.Dark, fight.State.Orbs[1].Type);
        Assert.Equal(6, fight.State.Orbs[1].EvokeValue);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void SharedFate_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card SharedFate --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(427), Card(524), Card(132), Card(IC.AscendersBane), Card(473), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(473), Card(132), Card(473), Card(49), Card(132))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        Assert.Equal(-2, fight.PlayerBuffAmount(BuffId.Strength));
        fight.PlayerPowersAre(BuffId.Strength);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(-2, fight.EnemyBuffAmount(BuffId.Strength, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Shatter_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Shatter --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(428), Card(130), Card(IC.AscendersBane), Card(471), Card(156), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(545), Card(471), Card(471), Card(130), Card(471), Card(130))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Empty(fight.State.Orbs);
        fight.PlayerPowersAre();
        Assert.Equal(67, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void ShiningStrike_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card ShiningStrike --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(429), Card(133), Card(474), Card(179), Card(474), Card(IC.AscendersBane))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(133), Card(133), Card(474), Card(133), Card(474), Card(532))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(7, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(11, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(82, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Shockwave_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Shockwave --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(CL.Shockwave), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.StrikeIronclad))
            .PlayerHp(61, 80)
            .Energy(9)
            .Draw(Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.AscendersBane), Card(IC.DefendIronclad), Card(IC.StrikeIronclad))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(3, fight.EnemyBuffAmount(BuffId.Weak, 0));
        Assert.Equal(3, fight.EnemyBuffAmount(BuffId.Vulnerable, 0));
    }

    [Fact]
    public void Shroud_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Shroud --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(432), Card(IC.AscendersBane), Card(473), Card(524), Card(132), Card(49))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(132), Card(473), Card(132), Card(132), Card(473), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.Shroud));
        fight.PlayerPowersAre(BuffId.Shroud);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void SicEm_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card SicEm --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(434), Card(132), Card(473), Card(473), Card(132), Card(49))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(524), Card(132), Card(132), Card(IC.AscendersBane), Card(473), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(85, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(3, fight.EnemyBuffAmount(BuffId.SicEm, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void SignalBoost_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card SignalBoost --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(435), Card(130), Card(471), Card(545), Card(471), Card(IC.AscendersBane))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(130), Card(471), Card(130), Card(471), Card(156))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.SignalBoost));
        fight.PlayerPowersAre(BuffId.SignalBoost);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Skewer_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Skewer --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(SI.Skewer), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.DefendIronclad))
            .PlayerHp(61, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.AscendersBane), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.Bash), Card(IC.StrikeIronclad))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(0, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(18, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Skim_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Skim --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(437), Card(471), Card(130), Card(130), Card(156), Card(471))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(471), Card(471), Card(130), Card(IC.AscendersBane), Card(545), Card(130))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(3, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void SleightOfFlesh_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card SleightOfFlesh --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(438), Card(132), Card(473), Card(49), Card(473), Card(IC.AscendersBane))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(132), Card(132), Card(473), Card(132), Card(473), Card(524))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.PlayerBuffAmount(BuffId.SleightOfFlesh));
        fight.PlayerPowersAre(BuffId.SleightOfFlesh);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Slice_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Slice --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(SI.Slice), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad))
            .PlayerHp(61, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.AscendersBane), Card(IC.Bash), Card(IC.DefendIronclad), Card(IC.StrikeIronclad))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(84, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Smokestack_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Smokestack --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(441), Card(130), Card(130), Card(471), Card(130), Card(IC.AscendersBane))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(156), Card(471), Card(545), Card(130), Card(471), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(5, fight.PlayerBuffAmount(BuffId.Smokestack));
        fight.PlayerPowersAre(BuffId.Smokestack);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Smokestack_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Smokestack --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(441), Card(132), Card(49), Card(132), Card(473), Card(473))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(132), Card(132), Card(473), Card(IC.AscendersBane), Card(524), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(5, fight.PlayerBuffAmount(BuffId.Smokestack));
        fight.PlayerPowersAre(BuffId.Smokestack);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void SolarStrike_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card SolarStrike --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(445), Card(474), Card(133), Card(133), Card(532), Card(474))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(474), Card(133), Card(IC.AscendersBane), Card(179), Card(133))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(10, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(81, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Soul_Base_CorpseSlugsWeak_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Soul --encounter CorpseSlugsWeak --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(446), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.StrikeIronclad))
            .PlayerHp(64, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.AscendersBane), Card(IC.StrikeIronclad))
            .Enemy(defId: 17, hp: 28, maxHp: 28, buffs: [new BuffState(BuffId.Ravenous, 4)])
            .Enemy(defId: 17, hp: 29, maxHp: 29, buffs: [new BuffState(BuffId.Ravenous, 4)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(64, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(4, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(28, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 0));
        Assert.Equal(29, fight.State.Enemies[1].Hp);
        Assert.Equal(0, fight.State.Enemies[1].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 1));
    }

    [Fact]
    public void SoulStorm_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card SoulStorm --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(447), Card(IC.AscendersBane), Card(49), Card(132), Card(473), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(132), Card(524), Card(473), Card(473), Card(132), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(81, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Sow_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Sow --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(449), Card(49), Card(132), Card(473), Card(524), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(132), Card(473), Card(132), Card(IC.AscendersBane), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(82, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void SpectrumShift_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card SpectrumShift --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(450), Card(133), Card(133), Card(474), Card(133), Card(IC.AscendersBane))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(532), Card(474), Card(179), Card(133), Card(474), Card(474))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.SpectrumShift));
        fight.PlayerPowersAre(BuffId.SpectrumShift);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Spinner_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Spinner --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(452), Card(471), Card(130), Card(130), Card(471), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(IC.AscendersBane), Card(156), Card(471), Card(545), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Spinner));
        fight.PlayerPowersAre(BuffId.Spinner);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Spinner_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Spinner --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(452), Card(473), Card(132), Card(132), Card(524), Card(473))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(473), Card(132), Card(IC.AscendersBane), Card(49), Card(132))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Spinner));
        fight.PlayerPowersAre(BuffId.Spinner);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void SpiritOfAsh_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card SpiritOfAsh --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(453), Card(132), Card(473), Card(49), Card(473), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(132), Card(IC.AscendersBane), Card(132), Card(473), Card(524))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(4, fight.PlayerBuffAmount(BuffId.SpiritOfAsh));
        fight.PlayerPowersAre(BuffId.SpiritOfAsh);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void SpoilsOfBattle_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card SpoilsOfBattle --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(456), Card(133), Card(133), Card(IC.AscendersBane), Card(474), Card(474))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(133), Card(532), Card(133), Card(474), Card(179), Card(474))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(4, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void SporeMind_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card SporeMind --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(457), Card(132), Card(132), Card(473), Card(473), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(IC.AscendersBane), Card(132), Card(524), Card(473), Card(49))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Spur_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Spur --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(458), Card(524), Card(132), Card(473), Card(49), Card(IC.AscendersBane))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(132), Card(132), Card(473), Card(473), Card(132), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(4, fight.State.OstyHp);
        Assert.Equal(4, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Squash_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Squash --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(459), Card(132), Card(IC.AscendersBane), Card(473), Card(524), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(49), Card(473), Card(473), Card(132), Card(473), Card(132))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(80, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Vulnerable, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Squeeze_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Squeeze --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(460), Card(132), Card(132), Card(473), Card(473), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(IC.AscendersBane), Card(132), Card(524), Card(473), Card(49))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(6, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(60, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Stardust_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Stardust --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(463), Card(474), Card(133), Card(133), Card(474), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(133), Card(IC.AscendersBane), Card(532), Card(474), Card(179), Card(474))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(0, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(45, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Storm_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Storm --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(467), Card(545), Card(130), Card(IC.AscendersBane), Card(471), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(471), Card(471), Card(130), Card(156), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Storm));
        fight.PlayerPowersAre(BuffId.Storm);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Strangle_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Strangle --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(SI.Strangle), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.DefendIronclad))
            .PlayerHp(61, 80)
            .Energy(9)
            .Draw(Card(IC.AscendersBane), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(82, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Strangle, 0));
    }

    [Fact]
    public void StrikeDefect_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card StrikeDefect --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(471), Card(545), Card(130), Card(471), Card(156), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(471), Card(130), Card(471), Card(130), Card(IC.AscendersBane), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(84, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void StrikeIronclad_Base_Cruelty_Vulnerable_CorpseSlugsWeak_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card StrikeIronclad --encounter CorpseSlugsWeak --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.StrikeIronclad))
            .PlayerHp(64, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.AscendersBane), Card(IC.StrikeIronclad))
            .PlayerBuff(BuffId.CrueltyPower, 25)
            .Enemy(defId: 17, hp: 28, maxHp: 28, buffs: [new BuffState(BuffId.Ravenous, 4), new BuffState(BuffId.Vulnerable, 2)])
            .Enemy(defId: 17, hp: 29, maxHp: 29, buffs: [new BuffState(BuffId.Ravenous, 4)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(64, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(25, fight.PlayerBuffAmount(BuffId.CrueltyPower));
        fight.PlayerPowersAre(BuffId.CrueltyPower);
        Assert.Equal(18, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 0));
        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Vulnerable, 0));
        Assert.Equal(29, fight.State.Enemies[1].Hp);
        Assert.Equal(0, fight.State.Enemies[1].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 1));
    }

    [Fact]
    public void StrikeNecrobinder_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card StrikeNecrobinder --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(473), Card(524), Card(132), Card(473), Card(49), Card(IC.AscendersBane))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(132), Card(132), Card(473), Card(473), Card(132), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(84, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void StrikeRegent_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card StrikeRegent --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(474), Card(179), Card(133), Card(474), Card(133), Card(474))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(133), Card(474), Card(532), Card(133), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 3);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(84, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Subroutine_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Subroutine --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(476), Card(130), Card(471), Card(156), Card(130), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(545), Card(471), Card(471), Card(471), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Subroutine));
        fight.PlayerPowersAre(BuffId.Subroutine);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void SuckerPunch_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card SuckerPunch --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(SI.SuckerPunch), Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.AscendersBane))
            .PlayerHp(61, 80)
            .Energy(9)
            .Draw(Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(82, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Weak, 0));
    }

    [Fact]
    public void SummonForth_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card SummonForth --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(478), Card(133), Card(133), Card(474), Card(474), Card(474))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(179), Card(133), Card(IC.AscendersBane), Card(474), Card(133), Card(532))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Sunder_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Sunder --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(479), Card(130), Card(471), Card(471), Card(471), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(471), Card(130), Card(IC.AscendersBane), Card(156), Card(545), Card(130))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(6, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(66, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Supercritical_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Supercritical --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(480), Card(130), Card(471), Card(471), Card(130), Card(545))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(156), Card(130), Card(130), Card(IC.AscendersBane), Card(471), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(13, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void SweepingBeam_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card SweepingBeam --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(484), Card(471), Card(545), Card(130), Card(IC.AscendersBane), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(156), Card(130), Card(130), Card(471), Card(471), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(5, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(84, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void SweepingGaze_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card SweepingGaze --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(485), Card(132), Card(132), Card(473), Card(473), Card(473))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(49), Card(132), Card(IC.AscendersBane), Card(473), Card(132), Card(524))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(80, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void SwordSage_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card SwordSage --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(487), Card(133), Card(474), Card(532), Card(133), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(133), Card(179), Card(474), Card(474), Card(474), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.SwordSage));
        fight.PlayerPowersAre(BuffId.SwordSage);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Synchronize_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Synchronize --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(488), Card(545), Card(130), Card(471), Card(471), Card(471))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(IC.AscendersBane), Card(156), Card(471), Card(130), Card(130), Card(130))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.Focus));
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.TemporaryFocus));
        fight.PlayerPowersAre(BuffId.Focus, BuffId.TemporaryFocus);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Synthesis_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Synthesis --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(489), Card(156), Card(130), Card(130), Card(471), Card(IC.AscendersBane))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(545), Card(471), Card(471), Card(471), Card(130))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.FreePowerPower));
        fight.PlayerPowersAre(BuffId.FreePowerPower);
        Assert.Equal(76, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Tempest_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Tempest --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(495), Card(130), Card(130), Card(471), Card(471), Card(471))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(545), Card(130), Card(IC.AscendersBane), Card(471), Card(130), Card(156))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(0, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Equal(3, fight.State.Orbs.Count);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[1].Type);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[2].Type);
        fight.PlayerPowersAre();
        Assert.Equal(34, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Terraforming_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Terraforming --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(496), Card(133), Card(474), Card(474), Card(474), Card(133))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(133), Card(IC.AscendersBane), Card(532), Card(179), Card(133))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        Assert.Equal(6, fight.PlayerBuffAmount(BuffId.Vigor));
        fight.PlayerPowersAre(BuffId.Vigor);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void TeslaCoil_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card TeslaCoil --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(497), Card(130), Card(130), Card(IC.AscendersBane), Card(471), Card(471))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(156), Card(130), Card(471), Card(545), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(84, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void TheScythe_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card TheScythe --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(501), Card(132), Card(473), Card(49), Card(IC.AscendersBane), Card(473))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(132), Card(473), Card(132), Card(132), Card(524))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(77, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void TheSealedThrone_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card TheSealedThrone --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(502), Card(179), Card(133), Card(474), Card(474), Card(474))
            .PlayerHp(43, 75)
            .Energy(9)
            .Draw(Card(IC.AscendersBane), Card(532), Card(474), Card(133), Card(133), Card(133))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(43, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(6, fight.State.Stars);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.TheSealedThrone));
        fight.PlayerPowersAre(BuffId.TheSealedThrone);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void TheSmith_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card TheSmith --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(503), Card(474), Card(133), Card(133), Card(532), Card(474))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(474), Card(133), Card(IC.AscendersBane), Card(179), Card(133))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(5, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Thrash_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Thrash --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(IC.Thrash), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.Bash), Card(IC.StrikeIronclad))
            .PlayerHp(61, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.AscendersBane), Card(IC.DefendIronclad), Card(IC.DefendIronclad))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(82, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Thunder_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Thunder --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(507), Card(471), Card(471), Card(130), Card(156), Card(IC.AscendersBane))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(471), Card(130), Card(130), Card(471), Card(545), Card(130))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(6, fight.PlayerBuffAmount(BuffId.Thunder));
        fight.PlayerPowersAre(BuffId.Thunder);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Thunderclap_Base_CorpseSlugsWeak_Ironclad_MatchesLiveCapture()
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
        fight.PlayerPowersAre();
        Assert.Equal(24, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 0));
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Vulnerable, 0));
        Assert.Equal(25, fight.State.Enemies[1].Hp);
        Assert.Equal(0, fight.State.Enemies[1].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 1));
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Vulnerable, 1));
    }

    [Fact]
    public void TimesUp_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card TimesUp --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(509), Card(132), Card(49), Card(524), Card(473), Card(473))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(132), Card(IC.AscendersBane), Card(473), Card(132), Card(132), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void ToricToughness_Base_CorpseSlugsWeak_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card ToricToughness --encounter CorpseSlugsWeak --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(511), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.StrikeIronclad))
            .PlayerHp(64, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.AscendersBane), Card(IC.StrikeIronclad))
            .Enemy(defId: 17, hp: 28, maxHp: 28, buffs: [new BuffState(BuffId.Ravenous, 4)])
            .Enemy(defId: 17, hp: 29, maxHp: 29, buffs: [new BuffState(BuffId.Ravenous, 4)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(64, fight.State.PlayerHp);
        Assert.Equal(5, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.ToricToughness));
        fight.PlayerPowersAre(BuffId.ToricToughness);
        Assert.Equal(28, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 0));
        Assert.Equal(29, fight.State.Enemies[1].Hp);
        Assert.Equal(0, fight.State.Enemies[1].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 1));
    }

    [Fact]
    public void TrashToTreasure_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card TrashToTreasure --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(515), Card(IC.AscendersBane), Card(130), Card(156), Card(545), Card(471))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(471), Card(130), Card(471), Card(130), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.TrashToTreasure));
        fight.PlayerPowersAre(BuffId.TrashToTreasure);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Tremble_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Tremble --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(IC.Tremble), Card(49), Card(132), Card(473), Card(524), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(132), Card(473), Card(132), Card(IC.AscendersBane), Card(473))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(3, fight.EnemyBuffAmount(BuffId.Vulnerable, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Turbo_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Turbo --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(518), Card(545), Card(156), Card(471), Card(130), Card(IC.AscendersBane))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(471), Card(130), Card(471), Card(471), Card(130))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(11, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Equal(2, fight.State.DiscardPile.Count);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Tyranny_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Tyranny --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(520), Card(133), Card(474), Card(474), Card(133), Card(179))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(532), Card(133), Card(133), Card(IC.AscendersBane), Card(474), Card(474))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Tyranny));
        fight.PlayerPowersAre(BuffId.Tyranny);
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void UltimateStrike_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card UltimateStrike --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(CL.UltimateStrike), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad))
            .PlayerHp(61, 80)
            .Energy(9)
            .Draw(Card(IC.AscendersBane), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.Bash))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(76, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Undeath_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Undeath --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(523), Card(49), Card(524), Card(473), Card(132), Card(IC.AscendersBane))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(132), Card(473), Card(132), Card(473), Card(473), Card(132))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(7, fight.State.PlayerBlock);
        Assert.Equal(9, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Equal(2, fight.State.DiscardPile.Count);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Unleash_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Unleash --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(524), Card(132), Card(IC.AscendersBane), Card(473), Card(524), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(49), Card(473), Card(473), Card(132), Card(473), Card(132))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(83, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Uppercut_Base_ByrdonisElite_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Uppercut --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(IC.Uppercut), Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.AscendersBane), Card(IC.StrikeIronclad), Card(IC.DefendIronclad))
            .PlayerHp(61, 80)
            .Energy(9)
            .Draw(Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(IC.Bash), Card(IC.StrikeIronclad))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(77, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Weak, 0));
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Vulnerable, 0));
    }

    [Fact]
    public void Uppercut_Base_CorpseSlugsWeak_Ironclad_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Uppercut --encounter CorpseSlugsWeak --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(IC.Uppercut), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.AscendersBane))
            .PlayerHp(64, 80)
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.Bash))
            .Enemy(defId: 17, hp: 27, maxHp: 27, buffs: [new BuffState(BuffId.Ravenous, 4)])
            .Enemy(defId: 17, hp: 29, maxHp: 29, buffs: [new BuffState(BuffId.Ravenous, 4)]);

        fight.Play(index: 0, target: 0);

        Assert.Equal(64, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(14, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 0));
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Weak, 0));
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Vulnerable, 0));
        Assert.Equal(29, fight.State.Enemies[1].Hp);
        Assert.Equal(0, fight.State.Enemies[1].Block);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Ravenous, 1));
    }

    [Fact]
    public void Uproar_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Uproar --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(530), Card(471), Card(130), Card(130), Card(545), Card(471))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(471), Card(471), Card(156), Card(IC.AscendersBane), Card(130))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(7, fight.State.Energy);
        Assert.Equal(5, fight.State.DrawPile.Count);
        Assert.Equal(2, fight.State.DiscardPile.Count);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(72, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Veilpiercer_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Veilpiercer --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(531), Card(473), Card(132), Card(132), Card(49), Card(473))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(132), Card(473), Card(473), Card(524), Card(IC.AscendersBane), Card(132))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Veilpiercer));
        fight.PlayerPowersAre(BuffId.Veilpiercer);
        Assert.Equal(80, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Venerate_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Venerate --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(532), Card(49), Card(132), Card(473), Card(132), Card(473))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(132), Card(473), Card(524), Card(132), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void Venerate_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Venerate --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(532), Card(474), Card(133), Card(133), Card(532), Card(474))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(474), Card(474), Card(133), Card(IC.AscendersBane), Card(179), Card(133))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 3);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(5, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void WhiteNoise_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card WhiteNoise --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(540), Card(130), Card(471), Card(545), Card(130), Card(130))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(130), Card(IC.AscendersBane), Card(471), Card(471), Card(156), Card(471))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Wisp_Base_ByrdonisElite_Necrobinder_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Wisp --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(542), Card(132), Card(132), Card(473), Card(473), Card(132))
            .PlayerHp(52, 66)
            .Energy(9)
            .Draw(Card(473), Card(IC.AscendersBane), Card(132), Card(524), Card(473), Card(49))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OstyHp = 1;
        fight.State.OstyMaxHp = 1;

        fight.Play(index: 0, target: 0);

        Assert.Equal(52, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(10, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.DiscardPile);
        Assert.Single(fight.State.ExhaustPile);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    [Fact]
    public void WroughtInWar_Base_ByrdonisElite_Regent_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card WroughtInWar --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(544), Card(179), Card(133), Card(474), Card(474), Card(474))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(IC.AscendersBane), Card(532), Card(474), Card(133), Card(133), Card(133))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        CardEffects.GainStars(fight.State, 9);

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(9, fight.State.Stars);
        fight.PlayerPowersAre();
        Assert.Equal(83, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }

    [Fact]
    public void Zap_Base_ByrdonisElite_Defect_MatchesLiveCapture()
    {
        // Captured from the live game (v0.107.1) by
        // scripts/capture_card.py --card Zap --encounter ByrdonisElite --seed ABCDEF.
        // Every number below is the game's, not the emulator's.
        var fight = Fight.Hand(Card(545), Card(545), Card(130), Card(471), Card(130), Card(471))
            .PlayerHp(60, 75)
            .Energy(9)
            .Draw(Card(471), Card(130), Card(471), Card(156), Card(130), Card(IC.AscendersBane))
            .Enemy(defId: 12, hp: 90, maxHp: 90, buffs: [new BuffState(BuffId.Territorial, 1)]);
        fight.State.OrbCapacity = 3;
        fight.State.BaseOrbSlots = 3;
        fight.State.Orbs.Add(new OrbState(OrbType.Lightning));

        fight.Play(index: 0, target: 0);

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(8, fight.State.Energy);
        Assert.Equal(6, fight.State.DrawPile.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.OrbCapacity);
        Assert.Equal(2, fight.State.Orbs.Count);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[1].Type);
        fight.PlayerPowersAre();
        Assert.Equal(90, fight.State.Enemies[0].Hp);
        Assert.Equal(0, fight.State.Enemies[0].Block);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Territorial, 0));
    }
}
