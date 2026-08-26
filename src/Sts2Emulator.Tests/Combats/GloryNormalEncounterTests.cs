using System.Collections.Generic;
using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

internal static class GloryNormal
{
    /// <summary>
    /// What an enemy ANNOUNCES over the next turns, both sides kept alive: the type, the
    /// damage a player would read off the intent, and the hit count.
    ///
    /// Announced rather than raw, unlike <see cref="GloryWeak.Cycle"/>, because that is
    /// where a folded multi-hit shows itself. A creature holding Strength announces base
    /// plus Strength PER HIT, so 4x6 at +2 reads 36 and a folded 24 reads 26 — the two
    /// agree only while the creature has no Strength, which is the first turn and no
    /// other.
    /// </summary>
    public static List<(IntentType Type, int Damage, int Hits)> Cycle(
        Fight fight,
        EnemyState subject,
        int turns
    )
    {
        var seen = new List<(IntentType, int, int)>();
        for (int turn = 0; turn < turns; turn++)
        {
            foreach (var enemy in fight.State.Enemies)
            {
                enemy.Hp = 9999;
            }

            fight.State.PlayerHp = 9999;
            seen.Add(
                (
                    subject.CurrentIntent.Type,
                    subject.CurrentIntent.AnnouncedDamage(subject.Buffs, fight.State.PlayerBuffs),
                    subject.CurrentIntent.Hits
                )
            );
            fight.EndTurn();
        }

        return seen;
    }

    /// <summary>
    /// How many copies of a status card the fight holds, wherever they are. Counting the
    /// discard alone is not the same question: a reshuffle moves the pile into the draw
    /// pile mid-fight, so three turns after a Vomit Ichor the discard can honestly hold
    /// one Slimed and the fight still hold ten.
    /// </summary>
    public static int Copies(Fight fight, int cardId) =>
        new[]
        {
            fight.State.Hand,
            fight.State.DrawPile,
            fight.State.DiscardPile,
            fight.State.ExhaustPile,
        }.Sum(pile => pile.Count(card => card.DefId == cardId));
}

/// <summary>
/// AxebotsNormal: one Axebot, which comes back twice.
/// </summary>
public class AxebotTests
{
    private static Fight Bot(int ascension = 8) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.Axebot, ascension);

    /// <summary>
    /// The machine's initial state is HAMMER_UPPERCUT, and ONE_TWO follows up back to it:
    /// a two-move alternation, not the three-cycle through BOOT_UP the emulator ran.
    /// BOOT_UP is only reachable as the initial state of a bot built with a stock
    /// override, which is what a respawn does.
    /// </summary>
    [Theory]
    [InlineData(8, 12, 9)]
    [InlineData(9, 14, 10)]
    public void ItUppercutsAndOneTwosForever(int ascension, int uppercut, int oneTwo)
    {
        var fight = Bot(ascension);
        var seen = GloryNormal.Cycle(fight, fight.State.Enemies[0], 5);

        Assert.Equal(
            [
                (IntentType.Attack, uppercut, 1),
                (IntentType.Attack, oneTwo * 2, 2),
                (IntentType.Attack, uppercut, 1),
                (IntentType.Attack, oneTwo * 2, 2),
                (IntentType.Attack, uppercut, 1),
            ],
            seen
        );
    }

    /// <summary>
    /// HammerUppercutMove applies WeakPower(2) and FrailPower(2) after it swings. The
    /// Axebot had no attack rider at all, so both were simply absent.
    /// </summary>
    [Fact]
    public void TheUppercutWeakensAndFrails()
    {
        var fight = Bot();
        fight.State.PlayerHp = 9999;
        fight.EndTurn();

        Assert.Equal(2, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Weak));
        Assert.Equal(2, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Frail));
    }

    /// <summary>
    /// ONE_TWO is <c>MultiAttackIntent(OneTwoDamage, 2)</c>, and the two hits land
    /// separately — which is what a per-instance hook on the player's side counts.
    /// </summary>
    [Fact]
    public void TheOneTwoLandsTwice()
    {
        var fight = Bot();
        var bot = fight.State.Enemies[0];
        bot.MoveIndex = 2;
        EnemyAI.ChooseIntents([bot], 0, new Random(0), ascension: 8);
        fight.State.PlayerHp = 9999;
        fight.State.PlayerBlock = 0;
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Thorns, 1);

        int before = bot.Hp;
        EnemyAI.ExecuteIntent(bot, fight.State, new Random(0));

        Assert.Equal(9999 - 18, fight.State.PlayerHp);
        Assert.Equal(before - 2, bot.Hp);
    }

    /// <summary>
    /// A respawn rebuilds the bot with a stock override, so its machine starts on
    /// BOOT_UP: block, and BootUpStrGain x (2 - StockAmount) Strength — nothing for the
    /// bot that opened the fight, one helping on the first respawn and two on the second.
    /// </summary>
    [Theory]
    [InlineData(8, 10, 3)]
    [InlineData(9, 15, 4)]
    public void ARespawnedBotBootsUp(int ascension, int block, int strengthPerStock)
    {
        var fight = Bot(ascension);
        var bot = fight.State.Enemies[0];
        bot.MoveIndex = 0;
        BuffSystem.Apply(bot.Buffs, BuffId.Stock, -1); // as the first respawn leaves it
        EnemyAI.ChooseIntents([bot], 0, new Random(0), ascension: ascension);

        Assert.Equal(new Intent(IntentType.Defend, block), bot.CurrentIntent);

        EnemyAI.ExecuteIntent(bot, fight.State, new Random(0));

        Assert.Equal(block, bot.Block);
        Assert.Equal(strengthPerStock, BuffSystem.Get(bot.Buffs, BuffId.Strength));
    }
}

/// <summary>
/// GlobeHeadNormal: one Globe Head.
/// </summary>
public class GlobeHeadTests
{
    private static Fight Globe(int ascension = 8) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.GlobeHead, ascension);

    /// <summary>
    /// SHOCKING_SLAP -> THUNDER_STRIKE -> GALVANIC_BURST, cycling. Only GALVANIC_BURST
    /// takes Strength, so the announcement climbs on the second pass — and the three-hit
    /// THUNDER_STRIKE climbs by three times as much as the others, which is exactly what
    /// folding it into one number could not express.
    /// </summary>
    [Theory]
    [InlineData(8, 13, 6, 16)]
    [InlineData(9, 14, 7, 17)]
    public void ItSlapsThenStrikesThenBursts(int ascension, int slap, int thunder, int burst)
    {
        var fight = Globe(ascension);
        var seen = GloryNormal.Cycle(fight, fight.State.Enemies[0], 6);

        Assert.Equal(
            [
                (IntentType.Attack, slap, 1),
                (IntentType.Attack, thunder * 3, 3),
                (IntentType.Attack, burst, 1),
                (IntentType.Attack, slap + 2, 1),
                (IntentType.Attack, (thunder + 2) * 3, 3),
                (IntentType.Attack, burst + 2, 1),
            ],
            seen
        );
    }

    /// <summary>
    /// ShockingSlap applies FrailPower(2); GalvanicBurstMove takes StrengthPower(2). The
    /// Strength sat in <c>ApplyBuffIntent</c>, and all three of the Globe Head's moves are
    /// attacks — so it never ran, and had it run it would have fired on all three.
    /// </summary>
    [Fact]
    public void OnlyTheSlapFrailsAndOnlyTheBurstBuffs()
    {
        var fight = Globe();
        var globe = fight.State.Enemies[0];
        fight.State.PlayerHp = 9999;

        fight.EndTurn(); // SHOCKING_SLAP
        Assert.Equal(2, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Frail));
        Assert.Equal(0, BuffSystem.Get(globe.Buffs, BuffId.Strength));

        fight.State.PlayerHp = 9999;
        fight.EndTurn(); // THUNDER_STRIKE
        Assert.Equal(0, BuffSystem.Get(globe.Buffs, BuffId.Strength));

        fight.State.PlayerHp = 9999;
        fight.EndTurn(); // GALVANIC_BURST
        Assert.Equal(2, BuffSystem.Get(globe.Buffs, BuffId.Strength));
    }
}

/// <summary>
/// OwlMagistrateNormal: one Owl Magistrate.
/// </summary>
public class OwlMagistrateTests
{
    private static Fight Owl(int ascension = 8) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.OwlMagistrate, ascension);

    /// <summary>
    /// MAGISTRATE_SCRUTINY -> PECK_ASSAULT -> JUDICIAL_FLIGHT -> VERDICT, cycling. The
    /// peck is six hits of four, which had been folded into a single 24; VERDICT's
    /// VulnerablePower(4) is why the scrutiny that follows announces half again.
    /// </summary>
    [Theory]
    [InlineData(8, 16, 33)]
    [InlineData(9, 17, 36)]
    public void ItScrutinisesPecksFliesThenPassesVerdict(int ascension, int scrutiny, int verdict)
    {
        var fight = Owl(ascension);
        var seen = GloryNormal.Cycle(fight, fight.State.Enemies[0], 5);

        Assert.Equal(
            [
                (IntentType.Attack, scrutiny, 1),
                // PeckAssaultDamage is 4 at both ascension levels.
                (IntentType.Attack, 24, 6),
                (IntentType.Buff, 1, 1),
                (IntentType.Attack, verdict, 1),
                (IntentType.Attack, scrutiny * 3 / 2, 1),
            ],
            seen
        );
    }

    /// <summary>
    /// PECK_ASSAULT lands six times, not once for 24: a per-instance hook on the player's
    /// side triggers six times, which is the half of a folded multi-hit that a corrected
    /// total still would not fix.
    /// </summary>
    [Fact]
    public void ThePeckLandsSixTimes()
    {
        var fight = Owl();
        var owl = fight.State.Enemies[0];
        owl.MoveIndex = 1;
        EnemyAI.ChooseIntents([owl], 0, new Random(0), ascension: 8);
        fight.State.PlayerHp = 9999;
        fight.State.PlayerBlock = 0;
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Thorns, 1);

        int before = owl.Hp;
        EnemyAI.ExecuteIntent(owl, fight.State, new Random(0));

        Assert.Equal(9999 - 24, fight.State.PlayerHp);
        Assert.Equal(before - 6, owl.Hp);
    }

    /// <summary>
    /// JUDICIAL_FLIGHT applies SoarPower(1) and VERDICT takes it away. Soar halves POWERED
    /// attack damage against the owl — attack damage from Attack cards and from creatures
    /// attacking, which is exactly what `BuffSystem.IncomingDamage` computes and nothing
    /// else. It was recorded as unmodelled (O19) on the reading that the emulator could
    /// not tell a powered attack from an unpowered one; it can, because the two damage
    /// helpers are split along that line already.
    /// </summary>
    [Fact]
    public void SoarHalvesAttacksWhileTheOwlIsFlying()
    {
        var fight = Owl();
        var owl = fight.State.Enemies[0];
        fight.State.PlayerHp = 9999;

        fight.Turns(3); // SCRUTINY, PECK_ASSAULT, JUDICIAL_FLIGHT
        Assert.Equal(1, BuffSystem.Get(owl.Buffs, BuffId.Soar));

        owl.Block = 0;
        int before = owl.Hp;
        fight.State.Hand = [new CardInstance(IC.StrikeIronclad, false)];
        fight.State.Energy = 3;
        fight.Play(0, target: 0);

        // A Strike is 6; half of it, rounded down as the game's decimal cast does, is 3.
        Assert.Equal(before - 3, owl.Hp);

        fight.State.PlayerHp = 9999;
        fight.EndTurn(); // VERDICT brings it down

        Assert.Equal(0, BuffSystem.Get(owl.Buffs, BuffId.Soar));
    }

    /// <summary>VerdictMove applies VulnerablePower(4), which the emulator never did.</summary>
    [Fact]
    public void TheVerdictLeavesThePlayerVulnerable()
    {
        var fight = Owl();
        var owl = fight.State.Enemies[0];
        owl.MoveIndex = 3;
        EnemyAI.ChooseIntents([owl], 0, new Random(0), ascension: 8);
        fight.State.PlayerHp = 9999;

        EnemyAI.ExecuteIntent(owl, fight.State, new Random(0));

        Assert.Equal(4, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Vulnerable));
    }
}

/// <summary>
/// SlimedBerserkerNormal: one Slimed Berserker.
/// </summary>
public class SlimedBerserkerTests
{
    private static Fight Berserker(int ascension = 8) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.SlimedBerserker, ascension);

    /// <summary>
    /// VOMIT_ICHOR -> FURIOUS_PUMMELING -> LEECHING_HUG -> SMOTHER, cycling.
    /// LEECHING_HUG declares its DebuffIntent BEFORE its BuffIntent, so the readout calls
    /// it a Debuff — it had been typed Buff — and the Strength it takes on the same turn
    /// is why SMOTHER announces three more than SmotherDamage.
    /// </summary>
    [Theory]
    [InlineData(8, 4, 30)]
    [InlineData(9, 5, 33)]
    public void ItVomitsPummelsHugsThenSmothers(int ascension, int pummel, int smother)
    {
        var fight = Berserker(ascension);
        var seen = GloryNormal.Cycle(fight, fight.State.Enemies[0], 6);

        Assert.Equal(
            [
                (IntentType.Debuff, 10, 1),
                (IntentType.Attack, pummel * 4, 4),
                (IntentType.Debuff, 3, 1),
                (IntentType.Attack, smother + 3, 1),
                (IntentType.Debuff, 10, 1),
                (IntentType.Attack, (pummel + 3) * 4, 4),
            ],
            seen
        );
    }

    /// <summary>
    /// Two of the berserker's moves announce as Debuffs now, and they do different things:
    /// VOMIT_ICHOR is ten Slimed into the discard, LEECHING_HUG is Weak on the player and
    /// Strength on itself.
    /// </summary>
    [Fact]
    public void TheTwoDebuffMovesAreToldApart()
    {
        var fight = Berserker();
        var berserker = fight.State.Enemies[0];
        fight.State.PlayerHp = 9999;

        fight.EndTurn(); // VOMIT_ICHOR
        Assert.Equal(10, GloryNormal.Copies(fight, ST.Slimed));
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Weak));

        fight.State.PlayerHp = 9999;
        fight.EndTurn(); // FURIOUS_PUMMELING
        fight.State.PlayerHp = 9999;
        fight.EndTurn(); // LEECHING_HUG

        Assert.Equal(3, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Weak));
        Assert.Equal(3, BuffSystem.Get(berserker.Buffs, BuffId.Strength));
        Assert.Equal(10, GloryNormal.Copies(fight, ST.Slimed));
    }
}

/// <summary>
/// FrogKnightNormal: one Frog Knight, behind Plating.
/// </summary>
public class FrogKnightTests
{
    private static Fight Knight(int ascension = 8) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.FrogKnight, ascension);

    /// <summary>
    /// TONGUE_LASH -> STRIKE_DOWN_EVIL -> FOR_THE_QUEEN, and the conditional branch after
    /// the buff sends it back to the lash while the knight is above half HP. The emulator
    /// ran a `% 3` with the strike and the buff the wrong way round.
    /// </summary>
    [Theory]
    [InlineData(8, 13, 21)]
    [InlineData(9, 14, 23)]
    public void ItLashesStrikesThenCallsForTheQueen(int ascension, int lash, int strike)
    {
        var fight = Knight(ascension);
        var seen = GloryNormal.Cycle(fight, fight.State.Enemies[0], 5);

        Assert.Equal(
            [
                (IntentType.Attack, lash, 1),
                (IntentType.Attack, strike, 1),
                (IntentType.Buff, 5, 1),
                // FOR_THE_QUEEN's Strength, which used to land a turn early.
                (IntentType.Attack, lash + 5, 1),
                (IntentType.Attack, strike + 5, 1),
            ],
            seen
        );
    }

    /// <summary>
    /// BEETLE_CHARGE is offered only once, and only when the branch is reached with the
    /// knight below half HP and not yet charged. It was **unreachable** — the emulator's
    /// three-cycle had no arm for it at all, so the knight's biggest move never happened.
    /// </summary>
    [Theory]
    [InlineData(8, 35)]
    [InlineData(9, 40)]
    public void ItChargesOnceItIsHurt(int ascension, int charge)
    {
        var fight = Knight(ascension);
        var knight = fight.State.Enemies[0];
        fight.State.PlayerHp = 9999;

        fight.Turns(2); // TONGUE_LASH, STRIKE_DOWN_EVIL -> it announces FOR_THE_QUEEN
        knight.Hp = knight.MaxHp / 2 - 1;
        fight.State.PlayerHp = 9999;
        fight.EndTurn(); // FOR_THE_QUEEN resolves, and the branch is taken

        // The intent's Magnitude is BeetleChargeDamage itself; FOR_THE_QUEEN's Strength
        // is added when the announcement is read, not when the move is chosen.
        Assert.Equal(new Intent(IntentType.Attack, charge), knight.CurrentIntent);

        // Once charged the branch never offers it again, however hurt the knight gets.
        fight.State.PlayerHp = 9999;
        fight.Turns(4);
        knight.Hp = 1;
        fight.State.PlayerHp = 9999;
        fight.EndTurn();

        Assert.NotEqual(charge, knight.CurrentIntent.Magnitude);
    }

    /// <summary>
    /// TongueLashMove applies FrailPower(2). It sat in <c>ApplyDebuffIntent</c>, and the
    /// Frog Knight has no Debuff intent at all, so it never ran — the fourth rider found
    /// in the wrong branch.
    /// </summary>
    [Fact]
    public void TheTongueLashFrails()
    {
        var fight = Knight();
        fight.State.PlayerHp = 9999;

        fight.EndTurn(); // TONGUE_LASH
        Assert.Equal(2, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Frail));
    }

    /// <summary>PlatingAmount was on the ToughEnemies branch, so it was 19 below A8 too.</summary>
    [Theory]
    [InlineData(8, 19)]
    [InlineData(0, 15)]
    public void ItsPlatingFollowsAscension(int ascension, int plating)
    {
        var knight = Knight(ascension).State.Enemies[0];

        Assert.Equal(plating, knight.Block);
        Assert.Equal(plating, BuffSystem.Get(knight.Buffs, BuffId.Plating));
    }
}

/// <summary>
/// MechaKnightNormal: one Mecha Knight, behind ArtifactPower(3).
/// </summary>
public class MechaKnightTests
{
    private static Fight Mecha(int ascension = 8) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.MechaKnight, ascension);

    /// <summary>
    /// CHARGE, then FLAMETHROWER -> WINDUP -> HEAVY_CLEAVE forever. HEAVY_CLEAVE follows
    /// up to the flamethrower rather than to the opening, so the charge happens ONCE — a
    /// `% 4` brought it back every fourth turn, and the windup's Strength makes every
    /// number after it climb, so the whole readout drifted.
    /// </summary>
    [Theory]
    [InlineData(8, 25, 35)]
    [InlineData(9, 30, 40)]
    public void ItChargesOnceThenCyclesThree(int ascension, int charge, int cleave)
    {
        var fight = Mecha(ascension);
        var seen = GloryNormal.Cycle(fight, fight.State.Enemies[0], 7);

        Assert.Equal(
            [
                (IntentType.Attack, charge, 1),
                (IntentType.Debuff, 4, 1),
                (IntentType.Defend, 15, 1),
                // WINDUP's StrengthPower(5), on every attack from here on.
                (IntentType.Attack, cleave + 5, 1),
                (IntentType.Debuff, 4, 1),
                (IntentType.Defend, 15, 1),
                (IntentType.Attack, cleave + 10, 1),
            ],
            seen
        );
    }

    /// <summary>
    /// WINDUP declares its DefendIntent before its BuffIntent, so it announces as a
    /// Defend. It had been typed Buff — and there is no Mecha Knight case in
    /// <c>ApplyBuffIntent</c>, so the move **did nothing at all**: no block, no Strength.
    /// </summary>
    [Fact]
    public void TheWindupBlocksAndBuffs()
    {
        var fight = Mecha();
        var knight = fight.State.Enemies[0];
        fight.State.PlayerHp = 9999;

        fight.Turns(2); // CHARGE, FLAMETHROWER
        Assert.Equal(0, BuffSystem.Get(knight.Buffs, BuffId.Strength));

        fight.State.PlayerHp = 9999;
        fight.EndTurn(); // WINDUP

        Assert.Equal(15, knight.Block);
        Assert.Equal(5, BuffSystem.Get(knight.Buffs, BuffId.Strength));
    }

    /// <summary>FlamethrowerMove puts its four Burns in the HAND, not the discard.</summary>
    [Fact]
    public void TheFlamethrowerFillsTheHand()
    {
        var fight = Mecha();
        fight.State.PlayerHp = 9999;

        fight.Turns(2); // CHARGE, then FLAMETHROWER resolves

        Assert.Equal(4, fight.State.Hand.Count(card => card.DefId == ST.Burn));
    }
}

/// <summary>
/// FabricatorNormal: one Fabricator, which builds bots until the bench is full.
/// </summary>
public class FabricatorTests
{
    private static Fight Shop(int ascension = 8) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.Fabricator, ascension);

    /// <summary>
    /// A ConditionalBranchState on CanFabricate — fewer than four living teammates — and
    /// only then a roll between FABRICATE and FABRICATING_STRIKE. With the bench full it
    /// DISINTEGRATES, a move the emulator never reached: it rolled the pair
    /// unconditionally, so a Fabricator with four bots up kept summoning into a full
    /// board and its second attack never happened.
    /// </summary>
    [Theory]
    [InlineData(8, 11)]
    [InlineData(9, 13)]
    public void WithTheBenchFullItDisintegrates(int ascension, int disintegrate)
    {
        var fight = Shop(ascension);
        var fabricator = fight.State.Enemies[0];
        for (int i = 0; i < 4; i++)
        {
            fight.State.Enemies.Add(
                new EnemyState
                {
                    DefId = KE.Guardbot,
                    Hp = 20,
                    MaxHp = 20,
                }
            );
        }

        EnemyAI.ChooseIntents(fight.State.Enemies, 0, new Random(0), ascension: ascension);

        Assert.Equal(new Intent(IntentType.Attack, disintegrate), fabricator.CurrentIntent);
    }

    /// <summary>
    /// With room on the bench it rolls, and both arms are reachable — FABRICATE is a bare
    /// summon, FABRICATING_STRIKE summons as it swings and so announces as an attack.
    /// </summary>
    [Theory]
    [InlineData(8, 18)]
    [InlineData(9, 21)]
    public void WithRoomItRollsBetweenSummonAndStrike(int ascension, int strike)
    {
        var seen = new HashSet<Intent>();
        for (int seed = 0; seed < 24; seed++)
        {
            var fight = Fight.Encounter(CombatFactory.ActOneEncounter.Fabricator, ascension, seed);
            EnemyAI.ChooseIntents(fight.State.Enemies, 0, new Random(seed), ascension: ascension);
            seen.Add(fight.State.Enemies[0].CurrentIntent);
        }

        Assert.Contains(new Intent(IntentType.Buff, 0), seen);
        Assert.Contains(new Intent(IntentType.Attack, strike), seen);
        Assert.Equal(2, seen.Count);
    }

    /// <summary>
    /// The bots it builds. Two of the four carry a DeadlyEnemies pair and both were on the
    /// A9 branch — written in <c>BotIntent</c>, a switch EXPRESSION, which the ascension
    /// audit could not see until it learned to read more than `case` arms.
    /// </summary>
    [Theory]
    [InlineData(8, 11, 14)]
    [InlineData(9, 12, 15)]
    public void TheBotsCarryTheirOwnAscensionValues(int ascension, int stab, int zap)
    {
        var fight = Shop(ascension);
        var stabbot = new EnemyState
        {
            DefId = KE.Stabbot,
            Hp = 20,
            MaxHp = 20,
        };
        var zapbot = new EnemyState
        {
            DefId = KE.Zapbot,
            Hp = 20,
            MaxHp = 20,
        };
        fight.State.Enemies.AddRange([stabbot, zapbot]);

        EnemyAI.ChooseIntents(fight.State.Enemies, 0, new Random(0), ascension: ascension);

        // STAB_MOVE declares its attack BEFORE its debuff, so it announces as an Attack --
        // the emulator had it as a Debuff with the attack as its secondary, inverted.
        Assert.Equal(new Intent(IntentType.Attack, stab), stabbot.CurrentIntent);
        Assert.Equal(new Intent(IntentType.Debuff, 1), stabbot.SecondaryIntent);
        Assert.Equal(new Intent(IntentType.Attack, zap), zapbot.CurrentIntent);
    }
}

/// <summary>
/// KnightsNormal: a Flail Knight, a Spectral Knight and a Magi Knight.
/// </summary>
public class KnightsTests
{
    private static Fight Trio(int ascension = 8) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.Knights, ascension);

    [Fact]
    public void TheEncounterIsThreeKnights()
    {
        Assert.Equal([KE.FlailKnight, KE.SpectralKnight, KE.MagiKnight], Trio().EnemyDefIds);
    }

    /// <summary>
    /// The Magi Knight opens POWER_SHIELD then DAMPEN, and MAGIC_BOMB returns to RAM — so
    /// those two happen ONCE and the fight is a three-cycle after them. A `% 5` brought
    /// both back every fifth turn, and all three of its damage numbers were the A9 branch.
    /// </summary>
    /// <remarks>
    /// PowerShieldBlock is a TOUGH pair, not a Deadly one, so it reads 9 at A8 and A9
    /// alike — the level below is what tells the two apart, which is why the block is
    /// checked separately.
    /// </remarks>
    [Theory]
    [InlineData(8, 6, 10, 35, 9)]
    [InlineData(9, 7, 11, 40, 9)]
    public void TheMagiKnightShieldsAndDampensOnce(
        int ascension,
        int shield,
        int spear,
        int bomb,
        int block
    )
    {
        var fight = Trio(ascension);
        var magi = fight.State.Enemies[2];
        var seen = GloryNormal.Cycle(fight, magi, 7);

        Assert.Equal(
            [
                (IntentType.Attack, shield, 1),
                (IntentType.Debuff, 1, 1),
                (IntentType.Attack, spear, 1),
                (IntentType.Defend, block, 1),
                (IntentType.Attack, bomb, 1),
                (IntentType.Attack, spear, 1),
                (IntentType.Defend, block, 1),
            ],
            seen
        );
    }

    /// <summary>
    /// POWER_SHIELD's DefendIntent, worth PowerShieldBlock — the ToughEnemies pair, which
    /// the emulator used at the high value for every level, and handed out every fifth
    /// turn rather than once.
    /// </summary>
    [Theory]
    [InlineData(8, 9)]
    [InlineData(0, 5)]
    public void ThePowerShieldBlocksAtItsOwnAscension(int ascension, int block)
    {
        var fight = Trio(ascension);
        var magi = fight.State.Enemies[2];
        fight.State.PlayerHp = 9999;

        fight.EndTurn(); // POWER_SHIELD

        Assert.Equal(block, magi.Block);
    }

    /// <summary>
    /// POWER_SHIELD is the opening and nothing returns to it, so its damage is announced
    /// once in the whole fight. The `% 5` handed it out every fifth turn.
    /// </summary>
    [Fact]
    public void TheShieldNeverComesRoundAgain()
    {
        var fight = Trio();
        var magi = fight.State.Enemies[2];
        var seen = GloryNormal.Cycle(fight, magi, 12);

        Assert.Equal((IntentType.Attack, 6, 1), seen[0]);
        Assert.DoesNotContain((IntentType.Attack, 6, 1), seen.Skip(1));
    }
}
