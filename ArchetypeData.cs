using System.Collections.Generic;

namespace CardProbMod;

/// <summary>
/// 流派定义模型
/// </summary>
public class Archetype
{
    public string Character { get; set; } = "";
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";

    // 标志性卡牌 (权重3.0) - 看到就基本确认流派
    public HashSet<string> SignatureCards { get; set; } = new(System.StringComparer.OrdinalIgnoreCase);
    // 核心卡牌 (权重2.0) - 流派的重要组件
    public HashSet<string> CoreCards { get; set; } = new(System.StringComparer.OrdinalIgnoreCase);
    // 协同卡牌 (权重1.0) - 在该流派中发挥良好
    public HashSet<string> SynergyCards { get; set; } = new(System.StringComparer.OrdinalIgnoreCase);
    // 反协同卡牌 - 与该流派冲突
    public HashSet<string> AntiCards { get; set; } = new(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 获取卡牌在该流派中的协同权重
    /// </summary>
    public float GetCardWeight(string cardName)
    {
        if (SignatureCards.Contains(cardName)) return 3.0f;
        if (CoreCards.Contains(cardName)) return 2.0f;
        if (SynergyCards.Contains(cardName)) return 1.0f;
        if (AntiCards.Contains(cardName)) return -1.5f;
        return 0f;
    }
}

/// <summary>
/// 所有流派定义 — 基于卡牌效果描述和社区攻略数据
/// 卡牌内部名来源: ptrlrd/spire-codex (sts2.dll 反编译)
/// </summary>
public static class ArchetypeDatabase
{
    public static List<Archetype> All { get; } = new()
    {
        // ============================================================
        //  铁甲战士 (Ironclad) — 87张
        // ============================================================
        new Archetype
        {
            Character = "Ironclad",
            Name = "Block",
            DisplayName = "格挡流",
            SignatureCards = { "BodySlam", "Barricade", "Impervious" },
            CoreCards = { "ShrugItOff", "FlameBarrier", "Unmovable", "CrimsonMantle", "BloodWall",
                          "Entrench" },
            SynergyCards = { "Offering", "BattleTrance", "SecondWind", "TrueGrit", "Juggernaut", "Tank",
                             "StoneArmor", "Armaments", "IronWave", "Rage", "Grapple", "Taunt" },
            AntiCards = { "Whirlwind", "Rampage" }
        },
        new Archetype
        {
            Character = "Ironclad",
            Name = "Exhaust",
            DisplayName = "消耗流",
            SignatureCards = { "FeelNoPain", "DarkEmbrace", "Corruption" },
            CoreCards = { "BurningPact", "TrueGrit", "FiendFire", "SecondWind", "PactsEnd",
                          "AshenStrike" },
            SynergyCards = { "Offering", "ForgottenRitual", "EvilEye", "Cascade", "Havoc", "Stoke",
                             "Brand", "Cinder", "DrumOfBattle", "HowlFromBeyond", "Thrash" },
            AntiCards = { "Rampage", "PerfectedStrike" }
        },
        new Archetype
        {
            Character = "Ironclad",
            Name = "Strength",
            DisplayName = "力量流",
            SignatureCards = { "DemonForm", "Colossus" },
            CoreCards = { "Inflame", "Whirlwind", "Bludgeon", "PrimalForce", "Conflagration",
                          "MoltenFist" },
            SynergyCards = { "Offering", "BattleTrance", "Taunt", "Uppercut", "Cruelty", "Vicious",
                             "FightMe", "Dominate", "SetupStrike", "OneTwoPunch", "Bully",
                             "Dismantle", "Stomp", "Thunderclap", "Pillage", "Unrelenting" },
            AntiCards = { "BodySlam", "Barricade" }
        },
        new Archetype
        {
            Character = "Ironclad",
            Name = "SelfDamage",
            DisplayName = "烧血流",
            SignatureCards = { "Rupture", "Inferno" },
            CoreCards = { "Bloodletting", "Offering", "Hemokinesis", "Feed", "Breakthrough" },
            SynergyCards = { "BurningPact", "Hellraiser", "Pyre", "Aggression", "Thrash", "Brand",
                             "TearAsunder", "Spite", "BloodWall", "DemonicShield" },
            AntiCards = { }
        },

        // ============================================================
        //  静默猎手 (Silent) — 88张
        // ============================================================
        new Archetype
        {
            Character = "Silent",
            Name = "Poison",
            DisplayName = "毒流",
            SignatureCards = { "CorrosiveWave", "Accelerant", "Envenom" },
            CoreCards = { "NoxiousFumes", "DeadlyPoison", "BouncingFlask", "PoisonedStab",
                          "Outbreak" },
            SynergyCards = { "Acrobatics", "Footwork", "Backflip", "BulletTime", "Burst",
                             "WellLaidPlans", "BubbleBubble", "Haze", "Snakebite", "Mirage",
                             "Blur", "Strangle", "LegSweep" },
            AntiCards = { "Accuracy", "InfiniteBlades" }
        },
        new Archetype
        {
            Character = "Silent",
            Name = "Discard",
            DisplayName = "弃牌/Sly流",
            SignatureCards = { "Sneaky", "Tactician", "Reflex" },
            CoreCards = { "Acrobatics", "CalculatedGamble", "Prepared", "HandTrick",
                          "ToolsOfTheTrade", "Abrasive" },
            SynergyCards = { "Speedster", "Backflip", "MasterPlanner", "Adrenaline", "EscapePlan",
                             "FlickFlack", "Ricochet", "Untouchable", "MementoMori",
                             "DaggerThrow", "StormOfSteel", "ShadowStep", "GrandFinale",
                             "Expertise" },
            AntiCards = { "Accuracy" }
        },
        new Archetype
        {
            Character = "Silent",
            Name = "Shiv",
            DisplayName = "飞刀流",
            SignatureCards = { "Accuracy", "KnifeTrap" },
            CoreCards = { "BladeDance", "CloakAndDagger", "InfiniteBlades", "HiddenDaggers",
                          "FanOfKnives", "StormOfSteel" },
            SynergyCards = { "Finisher", "Footwork", "Backflip", "Burst", "Afterimage",
                             "PhantomBlades", "UpMySleeve", "LeadingStrike", "BladeOfInk",
                             "Adrenaline" },
            AntiCards = { "CorrosiveWave", "Envenom", "NoxiousFumes" }
        },

        // ============================================================
        //  储君 / 摄政者 (Regent) — 88张
        // ============================================================
        new Archetype
        {
            Character = "Regent",
            Name = "Stars",
            DisplayName = "星辰流",
            SignatureCards = { "DecisionsDecisions", "BigBang", "VoidForm" },
            CoreCards = { "Glow", "HiddenCache", "Radiate", "Reflect", "Genesis", "DyingStar",
                          "SevenStars", "GammaBlast", "Alignment", "Comet", "HeavenlyDrill" },
            SynergyCards = { "Convergence", "CloakOfStars", "GatherLight", "Stardust", "ParticleWall",
                             "Bombardment", "ChildOfTheStars", "MakeItSo", "Orbit", "ShiningStrike",
                             "RoyalGamble", "TheSealedThrone", "SolarStrike", "CrescentSpear",
                             "Venerate", "BlackHole", "LunarBlast", "Prophesize" },
            AntiCards = { "WroughtInWar", "SpoilsOfBattle" }
        },
        new Archetype
        {
            Character = "Regent",
            Name = "Forge",
            DisplayName = "铸剑流",
            SignatureCards = { "Conqueror", "Furnace" },
            CoreCards = { "WroughtInWar", "SummonForth", "BeatIntoShape", "SeekingEdge",
                          "CosmicIndifference", "Bulwark" },
            SynergyCards = { "SwordSage", "SpoilsOfBattle", "HeirloomHammer", "RefineBlade",
                             "ForegoneConclusion", "Guards", "Charge", "TheSmith", "Parry",
                             "HammerTime", "CollisionCourse", "Patter" },
            AntiCards = { "DecisionsDecisions", "BigBang", "VoidForm" }
        },

        // ============================================================
        //  缚灵师 / 亡灵契约师 (Necrobinder) — 88张
        // ============================================================
        new Archetype
        {
            Character = "Necrobinder",
            Name = "Doom",
            DisplayName = "灾厄流",
            SignatureCards = { "EndOfDays", "ReaperForm", "DeathsDoor" },
            CoreCards = { "NegativePulse", "BorrowedTime", "Deathbringer", "Scourge", "Shroud",
                          "TimesUp", "NoEscape", "Defy" },
            SynergyCards = { "Delay", "Countdown", "BlightStrike", "Defile", "EnfeeblingTouch",
                             "Fear", "Hang", "Oblivion", "SleightOfFlesh", "Neurosurge",
                             "Debilitate", "Putrefy", "Misery" },
            AntiCards = { }
        },
        new Archetype
        {
            Character = "Necrobinder",
            Name = "Osty",
            DisplayName = "奥斯提流",
            SignatureCards = { "Dirge", "Squeeze", "Protector" },
            CoreCards = { "Fetch", "Spur", "SicEm", "Rattle", "Calcify", "Flatten",
                          "Bodyguard", "HighFive", "NecroMastery" },
            SynergyCards = { "PullAggro", "Afterlife", "BorrowedTime", "GraveWarden",
                             "RightHandHand", "Sacrifice", "Poke", "Snap", "BoneShards",
                             "Cleanse", "LegionOfBone" },
            AntiCards = { }
        },
        new Archetype
        {
            Character = "Necrobinder",
            Name = "Souls",
            DisplayName = "灵魂流",
            SignatureCards = { "SoulStorm", "SpiritOfAsh", "DevourLife" },
            CoreCards = { "CaptureSpirit", "Graveblast", "Haunt", "Seance",
                          "DeathMarch", "Dredge", "Reanimate", "Pagestorm" },
            SynergyCards = { "BorrowedTime", "Wisp", "Invoke", "Severance", "GraveWarden",
                             "Transfigure", "CallOfTheVoid", "Reave", "SculptingStrike",
                             "Veilpiercer", "PullFromBelow", "GlimpseBeyond",
                             "BansheesCry", "Eidolon" },
            AntiCards = { }
        },

        // ============================================================
        //  缺陷体 / 故障机器人 (Defect) — 88张
        // ============================================================
        new Archetype
        {
            Character = "Defect",
            Name = "Claw",
            DisplayName = "利爪流",
            SignatureCards = { "Claw", "AllForOne" },
            CoreCards = { "Scrape", "Ftl", "MomentumStrike", "GoForTheEyes", "BeamCell",
                          "Feral" },
            SynergyCards = { "Hologram", "MachineLearning", "BootSequence",
                             "Overclock", "Compact", "Turbo", "GunkUp", "Uproar",
                             "AdaptiveStrike" },
            AntiCards = { }
        },
        new Archetype
        {
            Character = "Defect",
            Name = "Lightning",
            DisplayName = "闪电球流",
            SignatureCards = { "Storm", "Voltaic" },
            CoreCards = { "Thunder", "LightningRod", "Tempest", "TeslaCoil",
                          "BallLightning" },
            SynergyCards = { "Defragment", "Capacitor", "Overclock",
                             "MachineLearning", "EchoForm", "DoubleEnergy", "Buffer",
                             "SweepingBeam", "CompileDriver", "Fusion", "Subroutine" },
            AntiCards = { }
        },
        new Archetype
        {
            Character = "Defect",
            Name = "Frost",
            DisplayName = "冰球流",
            SignatureCards = { "Glacier" },
            CoreCards = { "Coolheaded", "Chill", "ColdSnap", "Coolant", "IceLance",
                          "Hailstorm", "Loop" },
            SynergyCards = { "Defragment", "Capacitor", "Buffer", "EchoForm",
                             "MachineLearning", "Compact", "ShadowShield", "Leap",
                             "ChargeBattery", "GeneticAlgorithm", "CompileDriver" },
            AntiCards = { }
        },
        new Archetype
        {
            Character = "Defect",
            Name = "Dark",
            DisplayName = "暗球流",
            SignatureCards = { "Darkness", "ConsumingShadow" },
            CoreCards = { "Null", "MultiCast", "Rainbow", "ShadowShield" },
            SynergyCards = { "Defragment", "Capacitor", "EchoForm", "Buffer",
                             "MachineLearning", "DoubleEnergy", "Loop", "Coolheaded",
                             "Shatter", "Reboot" },
            AntiCards = { }
        },
    };

    /// <summary>
    /// 获取某个角色的所有流派
    /// </summary>
    public static List<Archetype> GetByCharacter(string character)
    {
        var result = new List<Archetype>();
        foreach (var a in All)
        {
            if (a.Character.Equals(character, System.StringComparison.OrdinalIgnoreCase))
                result.Add(a);
        }
        return result;
    }
}
