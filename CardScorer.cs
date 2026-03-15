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
    public string Breakdown { get; set; } = ""; // 详细计算过程

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

    // 通用好牌 — 在任何流派中都有价值的卡（含强力无色牌）
    private static readonly HashSet<string> UniversallyGood = new(StringComparer.OrdinalIgnoreCase)
    {
        // 角色牌
        "Offering", "BattleTrance", "Acrobatics", "Footwork", "Adrenaline",
        "Defragment", "EchoForm", "Buffer", "WraithForm",
        // 无色牌
        "Apotheosis", "DarkShackles", "Shockwave", "SecretTechnique", "SecretWeapon",
        "MasterOfStrategy", "Jackpot", "HandOfGreed", "Equilibrium", "PanicButton",
        "Mayhem", "Scrawl", "BeatDown", "Prowess", "TheBomb", "Panache",
        "Purity", "Discovery", "Finesse", "FlashOfSteel"
    };

    /// <summary>
    /// 为候选卡牌计算推荐分（含局势感知）
    /// </summary>
    public static CardRecommendation Score(
        string cardInternalName,
        ArchetypeResult archetype,
        DeckProfile deckProfile,
        ICollection<string> currentDeck,
        int hp = 0,
        int maxHp = 0,
        int floor = 0)
    {
        // 1. 流派协同分 (0-100)
        float archetypeScore = ComputeArchetypeScore(cardInternalName, archetype);

        // 2. 统计分 (0-100) — 基于现有CSV数据
        float statScore = ComputeStatScore(cardInternalName);

        // 3. 牌组需求分 (0-100)
        float needScore = ComputeNeedScore(cardInternalName, deckProfile, currentDeck);

        // 根据牌组大小动态调整权重 — 前期需要生存基础，后期追求流派协同
        int deckSize = currentDeck.Count;
        float wArch, wStat, wNeed;
        string phase;
        if (archetype.IsUnformed)
        {
            wArch = 0.10f; wStat = 0.50f; wNeed = 0.40f;
            phase = "未成型";
        }
        else if (deckSize <= 12)
        {
            wArch = 0.20f; wStat = 0.35f; wNeed = 0.45f;
            phase = "前期";
        }
        else if (deckSize <= 18)
        {
            wArch = 0.35f; wStat = 0.30f; wNeed = 0.35f;
            phase = "中期";
        }
        else if (deckSize <= 25)
        {
            wArch = 0.50f; wStat = 0.25f; wNeed = 0.25f;
            phase = "成型";
        }
        else
        {
            wArch = 0.55f; wStat = 0.20f; wNeed = 0.25f;
            phase = "后期";
        }

        float finalScore = archetypeScore * wArch + statScore * wStat + needScore * wNeed;
        string reason;

        if (archetype.IsUnformed)
        {
            reason = GetNeedReason(cardInternalName, deckProfile);
            if (string.IsNullOrEmpty(reason))
                reason = "流派未成型，参考统计";
        }
        else
        {
            reason = GetArchetypeReason(cardInternalName, archetype);
        }

        // 构建自然语言详情
        var bd = new System.Text.StringBuilder();

        // 阶段说明
        string phaseDesc = phase switch
        {
            "前期" => $"前期({deckSize}张) 优先生存",
            "中期" => $"中期({deckSize}张) 兼顾流派",
            "成型" => $"流派成型({deckSize}张)",
            "后期" => $"后期({deckSize}张) 控制牌组",
            _ => $"流派未成型({deckSize}张)"
        };
        bd.AppendLine(phaseDesc);

        // 流派评价
        string archDesc = archetypeScore switch
        {
            >= 85 => "流派核心，强烈契合",
            >= 70 => "流派关键牌",
            >= 55 => "与流派有一定协同",
            >= 40 => "与流派关系一般",
            _ => "与当前流派无关"
        };
        bd.AppendLine($"流派: {archDesc}");

        // 统计评价
        string statDesc = statScore switch
        {
            >= 80 => "社区公认强牌",
            >= 60 => "统计表现不错",
            >= 40 => "统计表现一般",
            _ => "统计数据偏低"
        };
        bd.AppendLine($"数据: {statDesc} ({GetStatDetail(cardInternalName)})");

        // 需求说明
        string needReason = GetNeedReason(cardInternalName, deckProfile);
        if (!string.IsNullOrEmpty(needReason))
            bd.AppendLine($"牌组{needReason}，此牌可补");
        else if (needScore <= 40)
            bd.AppendLine("牌组不缺此类牌");

        if (UniversallyGood.Contains(cardInternalName))
            bd.AppendLine("任何流派都好用的通用牌");

        // 重复卡惩罚 — currentDeck 是模拟牌组（已含候选牌自身），需减1得到实际持有数
        int dupeCount = CountCard(currentDeck, cardInternalName) - 1;
        if (dupeCount >= 2)
        {
            finalScore *= 0.6f;
            reason = $"已有{dupeCount}张，慎选";
            bd.AppendLine($"已有{dupeCount}张，扣分");
        }
        else if (dupeCount == 1 && archetypeScore < 70)
        {
            finalScore *= 0.85f;
            bd.AppendLine("已有1张，轻微扣分");
        }

        // 牌组过大惩罚
        if (deckProfile.DeckTooLarge && archetypeScore < 60)
        {
            finalScore *= 0.7f;
            reason = "牌组臃肿，建议跳过";
            bd.AppendLine("牌组太大，不建议加牌");
        }

        // 血量感知 — 血量低时，格挡/防御牌加分
        if (maxHp > 0)
        {
            float hpRatio = (float)hp / maxHp;
            if (hpRatio < 0.4f && DeckAnalyzer.IsBlockCard(cardInternalName))
            {
                finalScore += 12f;
                if (reason == "一般")
                    reason = "血量低，需防御";
                bd.AppendLine("血量低，防御牌加分");
            }
        }

        // Clamp
        finalScore = Math.Clamp(finalScore, 0, 100);
        bd.AppendLine($"综合评分: {finalScore:F0}/100");

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
            5 => "#E8A840",  // 柔橙 — 传说
            4 => "#B07CD8",  // 柔紫 — 史诗
            3 => "#5A9BD5",  // 柔蓝 — 稀有
            2 => "#7DBF7D",  // 柔绿 — 普通
            _ => "#A0A0A0"   // 柔灰 — 垃圾
        };
        rec.Reason = reason;
        rec.Breakdown = bd.ToString().TrimEnd();

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

    /// <summary>
    /// 在一组候选卡中标记最优选择
    /// </summary>
    public static void MarkBestPick(List<CardRecommendation> recs, List<string> cardNames)
    {
        if (recs.Count <= 1) return;

        int bestIdx = 0;
        for (int i = 1; i < recs.Count; i++)
        {
            if (recs[i].FinalScore > recs[bestIdx].FinalScore)
                bestIdx = i;
        }

        // 如果最优的也只是可跳/不推荐，升级为"可选"并标注三选一最优
        var best = recs[bestIdx];
        if (best.Stars <= 2)
        {
            best.Stars = 3;
            best.Verdict = "三选一最优";
            best.ColorHex = "#5A9BD5";
        }
        else if (best.Stars == 3)
        {
            best.Verdict = "三选一最优";
        }

        // 标记非最优的
        for (int i = 0; i < recs.Count; i++)
        {
            if (i == bestIdx) continue;
            if (recs[i].Stars <= 2 && recs[i].FinalScore < best.FinalScore - 5)
                recs[i].Reason = "非最优选";
        }
    }

    /// <summary>
    /// 牌组查看模式 — 评估卡牌在当前牌组中的适配度、是否删除、锻造优先级
    /// </summary>
    public static CardRecommendation ScoreDeckCard(
        string cardInternalName,
        ArchetypeResult archetype,
        DeckProfile deckProfile,
        ICollection<string> currentDeck)
    {
        var bd = new System.Text.StringBuilder();

        // ── 牌组总览 ──
        bd.AppendLine("── 牌组总览 ──");
        int formation = ComputeFormation(archetype, currentDeck);
        if (archetype.IsUnformed)
            bd.AppendLine($"流派未成型 ({currentDeck.Count}张)");
        else if (archetype.IsHybrid && archetype.Secondary != null)
            bd.AppendLine($"{archetype.Primary!.DisplayName}+{archetype.Secondary.DisplayName} 成型{formation}% ({currentDeck.Count}张)");
        else
            bd.AppendLine($"{archetype.DisplayName} 成型{formation}% ({currentDeck.Count}张)");

        var needs = new System.Collections.Generic.List<string>();
        if (deckProfile.NeedsDraw) needs.Add("过牌");
        if (deckProfile.NeedsAoe) needs.Add("AOE");
        if (deckProfile.NeedsBlock) needs.Add("格挡");
        if (needs.Count > 0)
            bd.AppendLine($"缺: {string.Join("、", needs)}");
        if (deckProfile.DeckTooLarge)
            bd.AppendLine("牌组臃肿，建议精简");

        if (archetype.IsUnformed)
            bd.AppendLine("建议: 拿强力单卡，等流派成型");
        else if (formation < 40)
            bd.AppendLine($"建议: 补充{archetype.Primary!.DisplayName}核心牌");
        else if (formation < 70)
            bd.AppendLine("建议: 精选高协同牌，控制数量");
        else
            bd.AppendLine("建议: 流派已成型，控制牌数");

        bd.AppendLine();
        bd.AppendLine("── 此牌 ──");

        float fitScore = 50f;
        string reason = "";

        bool isBasic = cardInternalName.StartsWith("Strike") || cardInternalName.StartsWith("Defend");
        if (isBasic)
        {
            fitScore = 20f;
            reason = "基础牌，优先删除";
            bd.AppendLine("基础牌，有机会应优先删除");
        }
        else if (archetype.Primary != null && !archetype.IsUnformed)
        {
            if (archetype.Primary.AntiCards.Contains(cardInternalName))
            {
                fitScore = 15f;
                reason = $"与{archetype.Primary.DisplayName}冲突";
                bd.AppendLine($"与{archetype.Primary.DisplayName}反协同");
                bd.AppendLine("★ 建议删除");
            }
            else if (archetype.Primary.SignatureCards.Contains(cardInternalName))
            {
                fitScore = 95f;
                reason = $"{archetype.Primary.DisplayName}标志牌";
                bd.AppendLine($"{archetype.Primary.DisplayName}的标志性卡牌");
            }
            else if (archetype.Primary.CoreCards.Contains(cardInternalName))
            {
                fitScore = 82f;
                reason = $"{archetype.Primary.DisplayName}关键";
                bd.AppendLine($"{archetype.Primary.DisplayName}的关键组件");
            }
            else if (archetype.Primary.SynergyCards.Contains(cardInternalName))
            {
                fitScore = 65f;
                reason = $"协同{archetype.Primary.DisplayName}";
                bd.AppendLine($"与{archetype.Primary.DisplayName}有协同");
            }
            else
            {
                bool found = false;
                if (archetype.IsHybrid && archetype.Secondary != null)
                {
                    if (archetype.Secondary.SignatureCards.Contains(cardInternalName))
                    { fitScore = 78f; reason = $"{archetype.Secondary.DisplayName}核心"; found = true; bd.AppendLine($"{archetype.Secondary.DisplayName}的核心牌"); }
                    else if (archetype.Secondary.CoreCards.Contains(cardInternalName))
                    { fitScore = 68f; reason = $"{archetype.Secondary.DisplayName}关键"; found = true; bd.AppendLine($"{archetype.Secondary.DisplayName}的关键牌"); }
                    else if (archetype.Secondary.SynergyCards.Contains(cardInternalName))
                    { fitScore = 55f; reason = $"协同{archetype.Secondary.DisplayName}"; found = true; bd.AppendLine($"与{archetype.Secondary.DisplayName}有协同"); }
                    else if (archetype.Secondary.AntiCards.Contains(cardInternalName))
                    { fitScore = 25f; reason = $"与{archetype.Secondary.DisplayName}冲突"; found = true; bd.AppendLine($"与{archetype.Secondary.DisplayName}冲突，可考虑删除"); }
                }

                if (!found)
                {
                    if (UniversallyGood.Contains(cardInternalName))
                    {
                        fitScore = 72f;
                        reason = "通用好牌";
                        bd.AppendLine("通用强力卡牌，值得保留");
                    }
                    else
                    {
                        float statScore = ComputeStatScore(cardInternalName);
                        fitScore = statScore * 0.6f + 20f;
                        string statDetail = GetStatDetail(cardInternalName);
                        string shortStat = CardDatabase.Data.TryGetValue(cardInternalName, out var cdb2)
                            ? $"胜{cdb2.WinRate:F0}%/选{cdb2.PickRate:F0}%" : "";
                        if (fitScore >= 55)
                        { reason = $"统计尚可 {shortStat}"; bd.AppendLine($"与流派无关，但统计尚可"); }
                        else if (fitScore >= 40)
                        { reason = $"流派无关 {shortStat}"; bd.AppendLine($"与当前流派无关联"); }
                        else
                        { reason = $"偏弱 {shortStat}"; bd.AppendLine($"与流派无关且偏弱"); }
                    }
                }
            }
        }
        else
        {
            float statScore = ComputeStatScore(cardInternalName);
            fitScore = statScore;
            string statDetail = GetStatDetail(cardInternalName);
            string shortStat = CardDatabase.Data.TryGetValue(cardInternalName, out var cdb)
                ? $"胜{cdb.WinRate:F0}%/选{cdb.PickRate:F0}%" : "";
            if (UniversallyGood.Contains(cardInternalName))
            { fitScore = Math.Max(fitScore, 72f); reason = $"通用好牌 {shortStat}"; bd.AppendLine("通用强力卡牌"); }
            else if (statScore >= 65)
            { reason = $"统计强力 {shortStat}"; bd.AppendLine("统计表现优秀"); }
            else if (statScore >= 45)
            { reason = $"统计一般 {shortStat}"; bd.AppendLine("统计表现一般"); }
            else
            { reason = $"统计偏低 {shortStat}"; bd.AppendLine("统计偏低"); }
        }

        // 重复检查
        int dupeCount = CountCard(currentDeck, cardInternalName);
        if (dupeCount >= 3)
        {
            fitScore = Math.Min(fitScore, 25f);
            reason = $"已有{dupeCount}张，过多";
            bd.AppendLine($"已有{dupeCount}张，副本过多，建议删除");
        }
        else if (dupeCount == 2 && fitScore < 75)
        {
            fitScore *= 0.8f;
            bd.AppendLine($"已有{dupeCount}张");
        }

        // 锻造建议
        string forge = GetForgeRating(cardInternalName, archetype);
        if (!string.IsNullOrEmpty(forge))
            bd.AppendLine($"锻造: {forge}");

        fitScore = Math.Clamp(fitScore, 0, 100);

        var rec = new CardRecommendation { FinalScore = fitScore, Reason = reason };

        if (isBasic || fitScore < 25)
        {
            rec.Stars = 1; rec.Verdict = "建议删除"; rec.ColorHex = "#A0A0A0";
        }
        else if (fitScore < 45)
        {
            rec.Stars = 2; rec.Verdict = "可精简"; rec.ColorHex = "#7DBF7D";
        }
        else
        {
            rec.Stars = fitScore switch { >= 85 => 5, >= 70 => 4, >= 55 => 3, _ => 2 };
            rec.Verdict = rec.Stars switch { 5 => "核心牌", 4 => "重要", 3 => "适配", _ => "可精简" };
            rec.ColorHex = rec.Stars switch { 5 => "#E8A840", 4 => "#B07CD8", 3 => "#5A9BD5", 2 => "#7DBF7D", _ => "#A0A0A0" };
        }

        rec.Breakdown = bd.ToString().TrimEnd();
        return rec;
    }

    /// <summary>
    /// 计算流派成型度百分比
    /// </summary>
    public static int ComputeFormation(ArchetypeResult archetype, ICollection<string> deck)
    {
        if (archetype.Primary == null || archetype.IsUnformed) return 0;

        int keyHit = 0;
        int keyTotal = archetype.Primary.SignatureCards.Count + archetype.Primary.CoreCards.Count;
        int synergyHit = 0;

        foreach (var card in deck)
        {
            if (archetype.Primary.SignatureCards.Contains(card)) keyHit++;
            else if (archetype.Primary.CoreCards.Contains(card)) keyHit++;
            else if (archetype.Primary.SynergyCards.Contains(card)) synergyHit++;
        }

        if (keyTotal == 0) return 0;
        float pct = (float)keyHit / keyTotal;
        float synergyBonus = Math.Min(0.2f, synergyHit * 0.04f);
        return Math.Clamp((int)((pct + synergyBonus) * 100), 0, 100);
    }

    private static string GetForgeRating(string cardName, ArchetypeResult archetype)
    {
        if (cardName.StartsWith("Strike") || cardName.StartsWith("Defend"))
            return "不建议，优先删除";

        if (archetype.Primary == null || archetype.IsUnformed)
        {
            if (UniversallyGood.Contains(cardName)) return "★★★ 优先";
            return "";
        }

        if (archetype.Primary.SignatureCards.Contains(cardName)) return "★★★ 优先";
        if (archetype.Primary.CoreCards.Contains(cardName)) return "★★☆ 推荐";
        if (archetype.Primary.SynergyCards.Contains(cardName)) return "★☆☆ 可以";
        if (archetype.Primary.AntiCards.Contains(cardName)) return "不建议";
        if (UniversallyGood.Contains(cardName)) return "★★☆ 推荐";
        return "";
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

        // 通用好牌保底 — 至少 Core 级别
        if (UniversallyGood.Contains(cardName) && weight < 2.0f)
            weight = 2.0f;

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

    /// <summary>
    /// 返回带具体数据的统计描述（胜率/选取率/综合分）
    /// </summary>
    private static string GetStatDetail(string cardName)
    {
        if (!CardDatabase.Data.TryGetValue(cardName, out var data))
            return "无统计数据";
        return $"胜率{data.WinRate:F1}% 选取{data.PickRate:F1}% 综合{data.Score:F1}";
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
