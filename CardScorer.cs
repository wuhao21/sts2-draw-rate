using System;
using System.Collections.Generic;

namespace CardProbMod;

/// <summary>
/// 推荐评分结果
/// </summary>
public class CardRecommendation
{
    public float FinalScore { get; set; }   // 0-100
    public int Stars { get; set; }          // 1-5
    public string Verdict { get; set; } = "";  // 极力推荐 / 推荐 / 可选 / 可跳 / 不推荐
    public string Reason { get; set; } = "";   // 简短原因
    public string ColorHex { get; set; } = "#FFFFFF";

    public static CardRecommendation Missing(string cardName) => new()
    {
        FinalScore = -1,
        Stars = 0,
        Verdict = "未知",
        Reason = cardName,
        ColorHex = "#808080"
    };
}

public static class CardScorer
{
    // 权重配置
    private const float W_Archetype = 0.50f;   // 流派协同权重
    private const float W_Statistics = 0.30f;   // 统计数据权重
    private const float W_DeckNeed = 0.20f;     // 牌组需求权重

    // 通用好牌 — 在任何流派中都有价值的卡
    private static readonly HashSet<string> UniversallyGood = new(StringComparer.OrdinalIgnoreCase)
    {
        "Offering", "BattleTrance", "Acrobatics", "Footwork", "Adrenaline",
        "Apotheosis", "Apparition", "WellLaidPlans", "EscapePlan",
        "Defragment", "EchoForm", "Buffer", "WraithForm"
    };

    /// <summary>
    /// 为候选卡牌计算推荐分
    /// </summary>
    public static CardRecommendation Score(
        string cardInternalName,
        ArchetypeResult archetype,
        DeckProfile deckProfile,
        ICollection<string> currentDeck)
    {
        // 1. 流派协同分 (0-100)
        float archetypeScore = ComputeArchetypeScore(cardInternalName, archetype);

        // 2. 统计分 (0-100) — 基于现有CSV数据
        float statScore = ComputeStatScore(cardInternalName);

        // 3. 牌组需求分 (0-100)
        float needScore = ComputeNeedScore(cardInternalName, deckProfile, currentDeck);

        // 加权总分
        float finalScore;
        string reason;

        if (archetype.IsUnformed)
        {
            // 流派未成型时，统计数据权重增大
            finalScore = statScore * 0.6f + needScore * 0.3f + archetypeScore * 0.1f;
            reason = GetNeedReason(cardInternalName, deckProfile);
            if (string.IsNullOrEmpty(reason))
                reason = "流派未成型，参考统计";
        }
        else
        {
            finalScore = archetypeScore * W_Archetype + statScore * W_Statistics + needScore * W_DeckNeed;
            reason = GetArchetypeReason(cardInternalName, archetype);
        }

        // 重复卡惩罚（已有2张以上同名卡）
        int dupeCount = CountCard(currentDeck, cardInternalName);
        if (dupeCount >= 2)
        {
            finalScore *= 0.6f;
            reason = $"已有{dupeCount}张，慎选";
        }
        else if (dupeCount == 1)
        {
            // 轻微惩罚，除非是核心卡
            if (archetypeScore < 70)
            {
                finalScore *= 0.85f;
            }
        }

        // 牌组过大惩罚
        if (deckProfile.DeckTooLarge && archetypeScore < 60)
        {
            finalScore *= 0.7f;
            reason = "牌组臃肿，建议跳过";
        }

        // Clamp
        finalScore = Math.Clamp(finalScore, 0, 100);

        var rec = new CardRecommendation { FinalScore = finalScore };
        rec.Stars = finalScore switch
        {
            >= 80 => 5,
            >= 65 => 4,
            >= 50 => 3,
            >= 35 => 2,
            _ => 1
        };
        rec.Verdict = rec.Stars switch
        {
            5 => "极力推荐",
            4 => "推荐",
            3 => "可选",
            2 => "可跳",
            _ => "不推荐"
        };
        rec.ColorHex = rec.Stars switch
        {
            5 => "#FFD700",  // 金
            4 => "#5BFF5B",  // 绿
            3 => "#E0E0E0",  // 白
            2 => "#FF9933",  // 橙
            _ => "#FF4444"   // 红
        };
        rec.Reason = reason;

        return rec;
    }

    /// <summary>
    /// 商店物品评分 — 考虑性价比
    /// </summary>
    public static CardRecommendation ScoreShopItem(
        string cardInternalName,
        int price,
        int playerGold,
        ArchetypeResult archetype,
        DeckProfile deckProfile,
        ICollection<string> currentDeck)
    {
        var base_rec = Score(cardInternalName, archetype, deckProfile, currentDeck);

        // 性价比调整：花费超过持有金币50%的卡需要更高的评分才值得买
        if (playerGold > 0)
        {
            float costRatio = (float)price / playerGold;
            if (costRatio > 0.5f && base_rec.FinalScore < 70)
            {
                base_rec.FinalScore *= 0.8f;
                base_rec.Reason = $"性价比低 ({price}g)";
            }
        }

        // 重新计算星级
        base_rec.Stars = base_rec.FinalScore switch
        {
            >= 80 => 5,
            >= 65 => 4,
            >= 50 => 3,
            >= 35 => 2,
            _ => 1
        };

        return base_rec;
    }

    // ====== 内部评分方法 ======

    private static float ComputeArchetypeScore(string cardName, ArchetypeResult archetype)
    {
        if (archetype.Primary == null) return 50f; // 中性

        float weight = archetype.Primary.GetCardWeight(cardName);

        if (archetype.IsHybrid && archetype.Secondary != null)
        {
            float w2 = archetype.Secondary.GetCardWeight(cardName);
            weight = Math.Max(weight, w2 * 0.7f); // 副流派权重略低
        }

        // 通用好牌保底
        if (UniversallyGood.Contains(cardName) && weight < 1.0f)
            weight = 1.5f;

        // 映射: -1.5 ~ 3.0 → 0 ~ 100
        return MapRange(weight, -1.5f, 3.0f, 10f, 100f);
    }

    private static float ComputeStatScore(string cardName)
    {
        if (!CardDatabase.Data.TryGetValue(cardName, out var data))
            return 40f; // 无数据时给中性分

        // Score = WinRate * 0.6 + PickRate * 0.4 (与原始mod一致)
        // 映射到 0-100 范围
        // 原始分范围大约 0-55，映射到 0-100
        return MapRange(data.Score, 15f, 55f, 10f, 100f);
    }

    private static float ComputeNeedScore(string cardName, DeckProfile profile, ICollection<string> deck)
    {
        float score = 50f; // 基准

        if (profile.NeedsDraw && DeckAnalyzer.IsDrawCard(cardName))
            score += 25f;
        if (profile.NeedsAoe && DeckAnalyzer.IsAoeCard(cardName))
            score += 25f;
        if (profile.NeedsBlock && DeckAnalyzer.IsBlockCard(cardName))
            score += 20f;

        return Math.Clamp(score, 0, 100);
    }

    private static string GetArchetypeReason(string cardName, ArchetypeResult archetype)
    {
        if (archetype.Primary == null) return "";

        if (archetype.Primary.SignatureCards.Contains(cardName))
            return $"{archetype.Primary.DisplayName}核心";
        if (archetype.Primary.CoreCards.Contains(cardName))
            return $"{archetype.Primary.DisplayName}关键牌";
        if (archetype.Primary.SynergyCards.Contains(cardName))
            return $"协同{archetype.Primary.DisplayName}";
        if (archetype.Primary.AntiCards.Contains(cardName))
            return $"与{archetype.Primary.DisplayName}冲突";

        // 检查副流派
        if (archetype.IsHybrid && archetype.Secondary != null)
        {
            if (archetype.Secondary.SignatureCards.Contains(cardName))
                return $"{archetype.Secondary.DisplayName}核心";
            if (archetype.Secondary.CoreCards.Contains(cardName))
                return $"{archetype.Secondary.DisplayName}关键牌";
        }

        if (UniversallyGood.Contains(cardName))
            return "通用好牌";

        return "一般";
    }

    private static string GetNeedReason(string cardName, DeckProfile profile)
    {
        if (profile.NeedsDraw && DeckAnalyzer.IsDrawCard(cardName)) return "缺过牌";
        if (profile.NeedsAoe && DeckAnalyzer.IsAoeCard(cardName)) return "缺AOE";
        if (profile.NeedsBlock && DeckAnalyzer.IsBlockCard(cardName)) return "缺格挡";
        return "";
    }

    private static int CountCard(ICollection<string> deck, string cardName)
    {
        int count = 0;
        foreach (var c in deck)
            if (c.Equals(cardName, StringComparison.OrdinalIgnoreCase))
                count++;
        return count;
    }

    private static float MapRange(float value, float fromMin, float fromMax, float toMin, float toMax)
    {
        value = Math.Clamp(value, fromMin, fromMax);
        return toMin + (value - fromMin) / (fromMax - fromMin) * (toMax - toMin);
    }
}
