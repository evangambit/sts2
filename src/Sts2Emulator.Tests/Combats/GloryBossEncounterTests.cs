using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// AeonglassBoss: one Aeonglass, holding ArtifactPower(3) from the moment it arrives.
/// </summary>
public class AeonglassTests
{
    private static Fight Glass(int ascension = 8) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.Aeonglass, ascension);

    /// <summary>
    /// `WitheringPresencePower` starts at `_baseCardsLeft = 6`, which is what the live
    /// capture of turn one shows.
    /// </summary>
    [Fact]
    public void ItArrivesCountingSixCards()
    {
        var fight = Glass();

        Assert.Equal(6, fight.EnemyBuffAmount(BuffId.WitheringPresence));
    }

    /// <summary>
    /// `AfterCardPlayed` takes one off for every card the player plays, and at zero a
    /// Wither joins their hand and the count goes back to 6. The boss used to carry
    /// Artifact and nothing else, so a 535-HP fight handed out no Withers at all.
    /// </summary>
    [Fact]
    public void EverySixthCardPlayedIsAWither()
    {
        var fight = Glass();
        fight.State.PlayerHp = 900;
        fight.State.Hand.Clear();
        for (int i = 0; i < 6; i++)
        {
            fight.State.Hand.Add(new CardInstance(IC.DefendIronclad, false));
        }

        fight.State.Energy = 9;
        for (int i = 0; i < 5; i++)
        {
            fight.Play(0);
        }

        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.WitheringPresence));
        Assert.DoesNotContain(fight.State.Hand, card => card.DefId == ST.Wither);

        fight.Play(0);

        Assert.Contains(fight.State.Hand, card => card.DefId == ST.Wither);
        Assert.Equal(6, fight.EnemyBuffAmount(BuffId.WitheringPresence));
    }

    /// <summary>
    /// EBB -> EYE_LASERS -> INCREASING_INTENSITY, cycling.
    ///
    /// INCREASING_INTENSITY declares StatusIntent before BuffIntent, so it announces as a
    /// Debuff whose number is WitherAmount — the Withers it adds — and not as the Buff of
    /// 2 the emulator reported. The Strength it takes is
    /// <c>IncreasingIntensityBaseStrength + AdditionalStrength</c>, and AdditionalStrength
    /// counts the times the move has already run, so the second helping is one larger than
    /// the first: the announcements climb by 4 then by 5 at A9, not by a flat 4 twice.
    /// </summary>
    [Theory]
    [InlineData(8, 26, 11, 1, 3)]
    [InlineData(9, 32, 12, 2, 4)]
    public void ItEbbsLasersThenIntensifies(
        int ascension,
        int ebb,
        int lasers,
        int wither,
        int strength
    )
    {
        var fight = Glass(ascension);
        var seen = GloryNormal.Cycle(fight, fight.State.Enemies[0], 6);

        Assert.Equal(
            [
                (IntentType.Attack, ebb, 1),
                // EYE_LASERS: MultiAttackIntent(EyeLasersDamage, 2), which had been folded.
                (IntentType.Attack, lasers * 2, 2),
                (IntentType.Debuff, wither, 1),
                (IntentType.Attack, ebb + strength, 1),
                (IntentType.Attack, (lasers + strength) * 2, 2),
                (IntentType.Debuff, wither, 1),
            ],
            seen
        );
    }

    /// <summary>
    /// EbbMove gains EbbBlock, a flat 33, and INCREASING_INTENSITY does not. The block
    /// used to sit in the buff branch, which meant the intensity move gained it and the
    /// EBB that owns it did not.
    /// </summary>
    [Fact]
    public void OnlyTheEbbBlocks()
    {
        var fight = Glass();
        var glass = fight.State.Enemies[0];
        fight.State.PlayerHp = 9999;

        fight.EndTurn(); // EBB
        Assert.Equal(33, glass.Block);

        fight.State.PlayerHp = 9999;
        fight.EndTurn(); // EYE_LASERS
        Assert.Equal(0, glass.Block);

        fight.State.PlayerHp = 9999;
        fight.EndTurn(); // INCREASING_INTENSITY
        Assert.Equal(0, glass.Block);
    }

    /// <summary>
    /// INCREASING_INTENSITY puts WitherAmount Withers in the discard and climbs its own
    /// Strength. The old handler dealt attack damage on this turn and applied an
    /// EbbPower(3) that nothing in the current build ever applies — and <c>BuffId.Ebb</c>
    /// was read nowhere, so it was a debuff the player carried and never paid.
    /// </summary>
    [Theory]
    [InlineData(8, 1, 3)]
    [InlineData(9, 2, 4)]
    public void TheIntensityMoveWithersAndClimbs(int ascension, int wither, int strength)
    {
        var fight = Glass(ascension);
        var glass = fight.State.Enemies[0];
        fight.State.PlayerHp = 9999;

        fight.Turns(3); // EBB, EYE_LASERS, INCREASING_INTENSITY

        Assert.Equal(wither, GloryNormal.Copies(fight, ST.Wither));
        Assert.Equal(strength, BuffSystem.Get(glass.Buffs, BuffId.Strength));

        for (int turn = 0; turn < 3; turn++)
        {
            fight.State.PlayerHp = 9999;
            glass.Hp = 9999;
            fight.EndTurn();
        }

        Assert.Equal(wither * 2, GloryNormal.Copies(fight, ST.Wither));
        Assert.Equal(strength * 2 + 1, BuffSystem.Get(glass.Buffs, BuffId.Strength));
    }
}

/// <summary>
/// QueenBoss: the Queen and her Torch Head Amalgam.
/// </summary>
public class QueenTests
{
    private static Fight Court(int ascension = 8) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.Queen, ascension);

    /// <summary>
    /// The Queen's raw announcements over the next turns. Raw rather than announced,
    /// unlike the rest of this file, because YOU_ARE_MINE leaves the player on Vulnerable
    /// 99 — so every announced number after turn two carries a 1.5x that says nothing
    /// about the move machine. Only the Queen is kept alive: the amalgam's fate is the
    /// input this fight turns on.
    /// </summary>
    private static List<(IntentType Type, int Magnitude, int Hits)> Cycle(Fight fight, int turns)
    {
        var queen = fight.State.Enemies[1];
        var seen = new List<(IntentType, int, int)>();
        for (int turn = 0; turn < turns; turn++)
        {
            queen.Hp = 9999;
            fight.State.PlayerHp = 9999;
            seen.Add(
                (queen.CurrentIntent.Type, queen.CurrentIntent.Magnitude, queen.CurrentIntent.Hits)
            );
            fight.EndTurn();
        }

        return seen;
    }

    [Fact]
    public void TheEncounterIsAnAmalgamAndTheQueen()
    {
        Assert.Equal([KE.TorchHeadAmalgam, KE.Queen], Court().EnemyDefIds);
    }

    /// <summary>
    /// While the amalgam lives, BURN_BRIGHT_FOR_ME loops on itself through a
    /// ConditionalBranchState: Strength for the amalgam, block for her.
    /// </summary>
    [Fact]
    public void SheBurnsBrightForAsLongAsTheAmalgamLives()
    {
        var fight = Court();
        var amalgam = fight.State.Enemies[0];
        var seen = Cycle(fight, 5);

        Assert.Equal(
            [
                (IntentType.Debuff, 3, 1),
                (IntentType.Debuff, 99, 1),
                (IntentType.Buff, 20, 1),
                (IntentType.Buff, 20, 1),
                (IntentType.Buff, 20, 1),
            ],
            seen
        );
        // One Strength per burn, to the teammate rather than to herself.
        Assert.Equal(3, BuffSystem.Get(amalgam.Buffs, BuffId.Strength));
        Assert.Equal(0, BuffSystem.Get(fight.State.Enemies[1].Buffs, BuffId.Strength));
    }

    /// <summary>
    /// Once the amalgam is dead the branch sends her to OFF_WITH_YOUR_HEAD -> EXECUTION ->
    /// ENRAGE, cycling. **The emulator burned bright forever**, so all three of these were
    /// unreachable and the Queen never dealt damage at all.
    /// </summary>
    [Theory]
    [InlineData(8, 3, 15)]
    [InlineData(9, 4, 18)]
    public void WithTheAmalgamDeadSheExecutes(int ascension, int head, int execution)
    {
        var fight = Court(ascension);
        fight.State.Enemies[0].Hp = 0;
        var seen = Cycle(fight, 6);

        Assert.Equal(
            [
                (IntentType.Debuff, 3, 1),
                (IntentType.Debuff, 99, 1),
                // OFF_WITH_YOUR_HEAD: MultiAttackIntent(OffWithYourHeadDamage, 5).
                (IntentType.Attack, head, 5),
                (IntentType.Attack, execution, 1),
                (IntentType.Buff, 2, 1),
                (IntentType.Attack, head, 5),
            ],
            seen
        );
    }

    /// <summary>
    /// Two of her moves announce as Debuffs and they are not the same debuff. PUPPET_
    /// STRINGS is ChainsOfBinding 3; YOU_ARE_MINE is Frail, Weak and Vulnerable at 99
    /// apiece, and it used to reach the same branch and hand out ChainsOfBinding 99.
    /// </summary>
    [Fact]
    public void HerTwoDebuffMovesAreToldApart()
    {
        var fight = Court();
        fight.State.PlayerHp = 9999;

        fight.EndTurn(); // PUPPET_STRINGS
        Assert.Equal(3, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.ChainsOfBinding));
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Frail));

        fight.State.PlayerHp = 9999;
        fight.EndTurn(); // YOU_ARE_MINE

        Assert.Equal(99, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Frail));
        Assert.Equal(99, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Weak));
        Assert.Equal(99, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Vulnerable));
        Assert.Equal(3, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.ChainsOfBinding));
    }

    /// <summary>
    /// <c>Queen.AfterDeath</c> replaces an ALREADY ANNOUNCED burn-bright with an enrage,
    /// so killing the amalgam on that turn does not buy the player a wasted enemy turn.
    /// </summary>
    [Fact]
    public void KillingTheAmalgamMidAnnouncementEnragesHerInstead()
    {
        var fight = Court();
        var amalgam = fight.State.Enemies[0];
        var queen = fight.State.Enemies[1];
        fight.State.PlayerHp = 9999;
        fight.Turns(2); // PUPPET_STRINGS, YOU_ARE_MINE -> she announces BURN_BRIGHT

        Assert.Equal(new Intent(IntentType.Buff, 20), queen.CurrentIntent);

        amalgam.Hp = 1;
        fight.State.Hand = [new CardInstance(IC.StrikeIronclad, false)];
        fight.State.Energy = 3;
        fight.Play();

        Assert.Equal(0, amalgam.Hp);
        Assert.Equal(new Intent(IntentType.Buff, 2), queen.CurrentIntent);

        fight.State.PlayerHp = 9999;
        fight.EndTurn();

        Assert.Equal(2, BuffSystem.Get(queen.Buffs, BuffId.Strength));
        Assert.Equal(0, queen.Block);
    }
}

/// <summary>
/// SoulNexusBoss: one Soul Nexus.
/// </summary>
public class SoulNexusTests
{
    private static Fight Nexus(int ascension = 8, int seed = 0) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.SoulNexus, ascension, seed);

    /// <summary>
    /// SOUL_BURN opens and every move returns to one RandomBranchState whose three
    /// branches are weight 1 and CannotRepeat — so the fight is a flat roll over the two
    /// moves it did not just make. The emulator ran a fixed three-cycle and never touched
    /// the AI stream, which is a different question from getting the numbers right: this
    /// asserts the SHAPE, since the roll itself is what a seed decides.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    public void ItOpensOnSoulBurnAndThenNeverRepeatsAMove(int seed)
    {
        var fight = Nexus(seed: seed);
        var nexus = fight.State.Enemies[0];
        var moves = new List<int>();
        for (int turn = 0; turn < 12; turn++)
        {
            nexus.Hp = 9999;
            fight.State.PlayerHp = 9999;
            moves.Add(nexus.LastMove);
            fight.EndTurn();
        }

        Assert.Equal(0, moves[0]); // SOUL_BURN
        Assert.Equal(3, moves.Distinct().Count());
        for (int turn = 1; turn < moves.Count; turn++)
        {
            Assert.NotEqual(moves[turn - 1], moves[turn]);
        }
    }

    /// <summary>
    /// MAELSTROM is <c>MultiAttackIntent(MaelstromDamage, MaelstromRepeat)</c>, four hits
    /// rather than the one folded 28; DRAIN_LIFE declares its attack BEFORE its debuff, so
    /// it announces as an Attack — it had been typed Debuff, telling a policy that a
    /// 19-damage turn was a debuff turn.
    /// </summary>
    [Theory]
    [InlineData(8, 29, 6, 18)]
    [InlineData(9, 31, 7, 19)]
    public void EachMoveAnnouncesWhatItDeclares(int ascension, int burn, int maelstrom, int drain)
    {
        var fight = Nexus(ascension);
        var nexus = fight.State.Enemies[0];

        Assert.Equal(new Intent(IntentType.Attack, burn), nexus.CurrentIntent);

        nexus.LastMove = 2; // force the branch to be over SOUL_BURN and MAELSTROM
        var seen = new HashSet<Intent>();
        for (int attempt = 0; attempt < 40; attempt++)
        {
            nexus.LastMove = attempt % 3;
            EnemyAI.ChooseIntents([nexus], 0, new Random(attempt), ascension: ascension);
            seen.Add(nexus.CurrentIntent);
        }

        Assert.Contains(new Intent(IntentType.Attack, burn), seen);
        Assert.Contains(new Intent(IntentType.Attack, maelstrom, Hits: 4), seen);
        Assert.Contains(new Intent(IntentType.Attack, drain), seen);
    }

    /// <summary>DrainLifeMove applies VulnerablePower(2) and WeakPower(2) as it lands.</summary>
    [Fact]
    public void DrainLifeStillDebuffsFromTheAttackBranch()
    {
        var fight = Nexus();
        var nexus = fight.State.Enemies[0];
        nexus.LastMove = 2; // DRAIN_LIFE, as though the branch had just chosen it
        nexus.CurrentIntent = new Intent(IntentType.Attack, 18);
        fight.State.PlayerHp = 9999;

        EnemyAI.ExecuteIntent(nexus, fight.State, new Random(0));

        Assert.Equal(2, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Vulnerable));
        Assert.Equal(2, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Weak));
    }
}

/// <summary>
/// TestSubjectBoss: one Test Subject, which is really three creatures in a row.
/// </summary>
public class TestSubjectTests
{
    private static Fight Subject(int ascension = 8) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.TestSubject, ascension);

    /// <summary>First form: BITE and SKULL_BASH alternate, and the bash carries Vulnerable 1.</summary>
    [Theory]
    [InlineData(8, 20, 14)]
    [InlineData(9, 22, 16)]
    public void TheFirstFormBitesAndBashes(int ascension, int bite, int bash)
    {
        var fight = Subject(ascension);
        var seen = GloryNormal.Cycle(fight, fight.State.Enemies[0], 4);

        Assert.Equal(
            [
                (IntentType.Attack, bite, 1),
                (IntentType.Attack, bash, 1),
                // The bash left the player on Vulnerable 1, so the bite that follows
                // announces half again -- the game's own readout, since
                // AttackIntent.GetSingleDamage runs the move through Hook.ModifyDamage.
                // It lasts one turn, so the bash after that is back to its base.
                (IntentType.Attack, bite * 3 / 2, 1),
                (IntentType.Attack, bash, 1),
            ],
            seen
        );
        Assert.True(BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Vulnerable) > 0);
    }

    /// <summary>
    /// EnrageAmount was on the A9 branch, so the first form gained an extra Strength for
    /// every Skill the player played at A8.
    /// </summary>
    [Theory]
    [InlineData(8, 2)]
    [InlineData(9, 3)]
    public void ItsEnrageIsAscensionDependent(int ascension, int enrage)
    {
        Assert.Equal(
            enrage,
            BuffSystem.Get(Subject(ascension).State.Enemies[0].Buffs, BuffId.Enrage)
        );
    }

    /// <summary>
    /// Second form: MULTI_CLAW follows up to ITSELF and each performance adds a hit, so
    /// the count climbs 3, 4, 5. The announcement was `11 * (3 + max(0, LastMove))` --
    /// folded into one number, at the A9 damage, and off by one besides, since LastMove
    /// started at -1 and the first two claws both read three hits.
    /// </summary>
    [Theory]
    [InlineData(8, 10)]
    [InlineData(9, 11)]
    public void TheSecondFormsClawClimbsAHitAtATime(int ascension, int claw)
    {
        var fight = Subject(ascension);
        var subject = fight.State.Enemies[0];
        BuffSystem.Apply(subject.Buffs, BuffId.PainfulStabs, 1);
        subject.MoveIndex = 2;
        EnemyAI.ChooseIntents([subject], 0, new Random(0), ascension: ascension);

        var seen = GloryNormal.Cycle(fight, subject, 3);

        Assert.Equal(
            [
                (IntentType.Attack, claw * 3, 3),
                (IntentType.Attack, claw * 4, 4),
                (IntentType.Attack, claw * 5, 5),
            ],
            seen
        );
    }

    /// <summary>
    /// PainfulStabsPower puts a Wound in the discard for every hit that lands UNBLOCKED
    /// damage — a per-instance hook, so the climbing claw pays for itself in cards. It was
    /// modelled as a bare phase marker, and the hand-rolled attack path the second form
    /// used to take would not have triggered it in any case.
    /// </summary>
    [Fact]
    public void EveryClawThatLandsAddsAWound()
    {
        var fight = Subject();
        var subject = fight.State.Enemies[0];
        BuffSystem.Apply(subject.Buffs, BuffId.PainfulStabs, 1);
        subject.MoveIndex = 2;
        EnemyAI.ChooseIntents([subject], 0, new Random(0), ascension: 8);
        fight.State.PlayerHp = 9999;
        fight.State.PlayerBlock = 0;

        EnemyAI.ExecuteIntent(subject, fight.State, new Random(0));

        Assert.Equal(3, GloryNormal.Copies(fight, ST.Wound));
    }

    /// <summary>
    /// Third form: PHASE3_LACERATE -> BIG_POUNCE -> BURNING_GROWL, cycling. The lacerate
    /// is three hits rather than one folded 33, and BURNING_GROWL declares StatusIntent
    /// before BuffIntent, so it announces as a Debuff whose number is the Burns it adds.
    /// </summary>
    [Theory]
    [InlineData(8, 10, 3, 2)]
    [InlineData(9, 11, 5, 3)]
    public void TheThirdFormLaceratesPouncesAndGrowls(
        int ascension,
        int lacerate,
        int burns,
        int growlStrength
    )
    {
        var fight = Subject(ascension);
        var subject = fight.State.Enemies[0];
        BuffSystem.Remove(subject.Buffs, BuffId.Adaptable);
        subject.MoveIndex = 4;
        EnemyAI.ChooseIntents([subject], 0, new Random(0), ascension: ascension);

        var seen = GloryNormal.Cycle(fight, subject, 4);

        Assert.Equal(
            [
                (IntentType.Attack, lacerate * 3, 3),
                (IntentType.Attack, 45, 1),
                (IntentType.Debuff, burns, 1),
                // The growl's Strength, on each of the three hits.
                (IntentType.Attack, (lacerate + growlStrength) * 3, 3),
            ],
            seen
        );
        // BurningGrowlMove puts its Burns in the DISCARD; this case used to put them in
        // the player's hand, and nothing reached it while the move was typed Buff.
        Assert.Equal(burns, GloryNormal.Copies(fight, ST.Burn));
    }

    /// <summary>
    /// The forms are as tall as the ascension says. Both respawn heights were hardcoded to
    /// the ToughEnemies branch.
    /// </summary>
    [Theory]
    [InlineData(8, 212, 313)]
    [InlineData(0, 200, 300)]
    public void TheRespawnHeightsFollowAscension(int ascension, int second, int third)
    {
        var fight = Subject(ascension);
        var subject = fight.State.Enemies[0];
        fight.State.PlayerHp = 9999;

        Kill(fight, subject);
        fight.EndTurn(); // the respawn turn
        Assert.Equal(second, subject.MaxHp);
        Assert.Equal(second, subject.Hp);

        fight.State.PlayerHp = 9999;
        Kill(fight, subject);
        fight.EndTurn();
        Assert.Equal(third, subject.MaxHp);
        Assert.Equal(third, subject.Hp);
    }

    /// <summary>
    /// Kills the subject with a Strike, rather than by writing 0 into its HP: the death
    /// hooks fire off a drop the step itself made, so a hand-set 0 respawns nothing.
    /// </summary>
    private static void Kill(Fight fight, EnemyState subject)
    {
        subject.Hp = 1;
        fight.State.Hand = [new CardInstance(IC.StrikeIronclad, false)];
        fight.State.Energy = 3;
        fight.Play();
    }
}
