using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Logging;

namespace CardProbMod;

/// <summary>
/// 流派检测结果
/// </summary>
public class ArchetypeResult
{
    public Archetype? Primary { get; set; }
    public float PrimaryScore { get; set; }
    public Archetype? Secondary { get; set; }
    public float SecondaryScore { get; set; }
    public bool IsHybrid { get; set; }
    public bool IsUnformed { get; set; } // 流派尚未成型

    public string DisplayName => IsUnformed ? "未成型"
        : IsHybrid ? $"{Primary!.DisplayName}+{Secondary!.DisplayName}"
        : Primary!.DisplayName;
}

/// <summary>
/// 牌组结构分析结果
/// </summary>
public class DeckProfile
{
    public int TotalCards { get; set; }
    public int AttackCount { get; set; }
    public int SkillCount { get; set; }
    public int PowerCount { get; set; }
    public bool NeedsAoe { get; set; }
    public bool NeedsBlock { get; set; }
    public bool NeedsDraw { get; set; }
    public bool DeckTooLarge { get; set; } // >25张 → 倾向于 skip
}

public static class DeckAnalyzer
{
    // 阈值
    private const float UnformedThreshold = 3.0f;   // 低于此分 = 流派未成型
    private const float HybridGapThreshold = 2.0f;  // 前两名差距小于此 = 混合流派

    // 过牌卡（通用）
    private static readonly HashSet<string> DrawCards = new(StringComparer.OrdinalIgnoreCase)
    {
        "Acrobatics", "Backflip", "Offering", "BattleTrance", "PommelStrike",
        "BurningPact", "Coolheaded", "Skim", "MachineLearning", "Adrenaline",
        "ShrugItOff", "Prepared"
    };

    // AOE 卡
    private static readonly HashSet<string> AoeCards = new(StringComparer.OrdinalIgnoreCase)
    {
        "Whirlwind", "Inferno", "FanOfKnives", "DaggerSpray", "CloakAndDagger",
        "Tempest", "Electrodynamics", "NoxiousFumes", "CorrosiveWave",
        "Bombardment", "MeteorShower", "NegativePulse", "Deathbringer"
    };

    public static bool IsDrawCard(string card) => DrawCards.Contains(card);
    public static bool IsAoeCard(string card) => AoeCards.Contains(card);
    public static bool IsBlockCard(string card) => BlockCards.Contains(card);

    // 格挡卡
    private static readonly HashSet<string> BlockCards = new(StringComparer.OrdinalIgnoreCase)
    {
        "ShrugItOff", "Impervious", "FlameBarrier", "Footwork", "Backflip",
        "Glacier", "ColdSnap", "Chill", "Dash", "Reflect", "CloakOfStars",
        "GatherLight", "ParticleWall", "Delay", "ShadowShield", "Bodyguard"
    };

    // 角色初始卡 → 用于角色识别
    private static readonly Dictionary<string, string> StarterCardToCharacter = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Bash"] = "Ironclad",
        ["Neutralize"] = "Silent",
        ["Survivor"] = "Silent",
        ["Zap"] = "Defect",
        ["Dualcast"] = "Defect",
        ["Venerate"] = "Regent",
        ["FallingStar"] = "Regent",
        ["Bodyguard"] = "Necrobinder",
        ["Unleash"] = "Necrobinder",
    };

    /// <summary>
    /// 从牌组中推断角色
    /// </summary>
    public static string DetectCharacter(ICollection<string> deck)
    {
        // 方法1：检查初始卡
        foreach (var card in deck)
        {
            if (StarterCardToCharacter.TryGetValue(card, out var character))
                return character;
        }

        // 方法2：看哪个角色的流派卡最多
        var charScores = new Dictionary<string, int>();
        foreach (var archetype in ArchetypeDatabase.All)
        {
            if (!charScores.ContainsKey(archetype.Character))
                charScores[archetype.Character] = 0;

            foreach (var card in deck)
            {
                if (archetype.SignatureCards.Contains(card) || archetype.CoreCards.Contains(card))
                    charScores[archetype.Character]++;
            }
        }

        string bestChar = "";
        int bestScore = 0;
        foreach (var kvp in charScores)
        {
            if (kvp.Value > bestScore)
            {
                bestScore = kvp.Value;
                bestChar = kvp.Key;
            }
        }
        return bestChar;
    }

    /// <summary>
    /// 检测当前牌组最匹配的流派
    /// </summary>
    public static ArchetypeResult DetectArchetype(ICollection<string> deck, string character)
    {
        var archetypes = string.IsNullOrEmpty(character)
            ? ArchetypeDatabase.All
            : ArchetypeDatabase.GetByCharacter(character);

        if (archetypes.Count == 0)
            archetypes = ArchetypeDatabase.All;

        // 对每个流派计算匹配分
        var scores = new List<(Archetype archetype, float score)>();
        foreach (var arch in archetypes)
        {
            float score = 0;
            foreach (var card in deck)
            {
                score += arch.GetCardWeight(card);
            }
            scores.Add((arch, score));
        }

        // 按分数降序
        scores.Sort((a, b) => b.score.CompareTo(a.score));

        var result = new ArchetypeResult();
        if (scores.Count == 0 || scores[0].score < UnformedThreshold)
        {
            result.IsUnformed = true;
            if (scores.Count > 0)
            {
                result.Primary = scores[0].archetype;
                result.PrimaryScore = scores[0].score;
            }
            return result;
        }

        result.Primary = scores[0].archetype;
        result.PrimaryScore = scores[0].score;

        if (scores.Count > 1)
        {
            result.Secondary = scores[1].archetype;
            result.SecondaryScore = scores[1].score;

            if (scores[1].score >= UnformedThreshold &&
                (scores[0].score - scores[1].score) < HybridGapThreshold)
            {
                result.IsHybrid = true;
            }
        }

        return result;
    }

    /// <summary>
    /// 分析牌组结构，检测缺陷
    /// </summary>
    public static DeckProfile AnalyzeDeck(ICollection<string> deck)
    {
        var profile = new DeckProfile { TotalCards = deck.Count };

        int drawCount = 0;
        int aoeCount = 0;
        int blockCount = 0;

        foreach (var card in deck)
        {
            if (DrawCards.Contains(card)) drawCount++;
            if (AoeCards.Contains(card)) aoeCount++;
            if (BlockCards.Contains(card)) blockCount++;
        }

        // 简单的缺陷检测规则
        profile.NeedsDraw = drawCount < 2;
        profile.NeedsAoe = aoeCount < 1;
        profile.NeedsBlock = blockCount < 2;
        profile.DeckTooLarge = deck.Count > 25;

        return profile;
    }
}
