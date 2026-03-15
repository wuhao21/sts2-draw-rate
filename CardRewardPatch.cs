using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Cards;
using System.Collections.Generic;
using System.Reflection;

namespace CardProbMod;

[HarmonyPatch]
public static class FinalPatch
{
    static IEnumerable<MethodBase> TargetMethods()
    {
        var type = typeof(NCard);
        yield return type.GetMethod("_Ready", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)!;
    }

    [HarmonyPostfix]
    public static void Postfix(NCard __instance)
    {
        try
        {
            string uniqueId = System.Guid.NewGuid().ToString("N").Substring(0, 8);
            string uiName = "CardStatsUI_" + uniqueId;

            var container = new Node2D();
            container.Name = uiName;

            float boxWidth = 260;
            float boxHeight = 110;

            container.Position = new Vector2(-boxWidth / 2, 160);
            // 不设置 ZIndex，避免刺穿 Godot 的黑色遮罩层
            container.Visible = false;

            var border = new ColorRect();
            border.SetSize(new Vector2(boxWidth + 4, boxHeight + 4));
            border.SetPosition(new Vector2(-2, -2));
            container.AddChild(border);

            var bg = new ColorRect();
            bg.Color = new Color(0.05f, 0.05f, 0.05f, 0.95f);
            bg.SetSize(new Vector2(boxWidth, boxHeight));
            container.AddChild(bg);

            var label = new Label();
            label.SetSize(new Vector2(boxWidth, boxHeight));
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.AddThemeFontSizeOverride("font_size", 20);
            label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 1));
            label.AddThemeConstantOverride("shadow_offset_x", 1);
            label.AddThemeConstantOverride("shadow_offset_y", 1);

            container.AddChild(label);
            __instance.AddChild(container);

            SetupTracker(__instance, container, border, label, uiName);
        }
        catch (System.Exception ex)
        {
            Log.Error($"[CardMod] 补丁执行出错: {ex.Message}");
        }
    }

    private static void SetupTracker(NCard cardNode, Node2D container, ColorRect border, Label label, string myUiName)
    {
        var timer = new Godot.Timer();
        timer.WaitTime = 0.2f;
        timer.Autostart = true;
        container.AddChild(timer);

        float defaultY = 160f;
        float shopY = -300f;
        float boxWidth = 260f;

        timer.Timeout += () =>
        {
            if (!GodotObject.IsInstanceValid(container) || !GodotObject.IsInstanceValid(cardNode)) return;

            // 清理克隆造成的僵尸节点
            foreach (Node child in cardNode.GetChildren())
            {
                string childName = child.Name.ToString();
                if (childName.StartsWith("CardStatsUI") && childName != myUiName)
                {
                    child.Name = "Killed_" + System.Guid.NewGuid().ToString();
                    child.QueueFree();
                }
            }

            // 1. 判断上下文
            bool isCombat = false;
            bool isShop = false;
            bool isGridOrDeck = false;

            Node current = cardNode.GetParent();
            while (current != null)
            {
                string n = current.Name.ToString().ToLower();

                if (n.Contains("battle") || n.Contains("combat") || n.Contains("hand"))
                    isCombat = true;
                if (n.Contains("shop") || n.Contains("merchant") || n.Contains("store"))
                    isShop = true;
                if (n.Contains("grid") || n.Contains("deck") || n.Contains("pile") || n.Contains("select") || n.Contains("remove"))
                    isGridOrDeck = true;

                current = current.GetParent();
            }

            // 战斗中隐藏
            if (isCombat)
            {
                container.Visible = false;
                foreach (Node child in cardNode.GetChildren())
                {
                    if (child.Name.ToString().StartsWith("CardStatsUI"))
                        (child as CanvasItem)!.Visible = false;
                }
                return;
            }

            // 动态排版
            if (isShop && !isGridOrDeck)
                container.Position = new Vector2(-boxWidth / 2, shopY);
            else
                container.Position = new Vector2(-boxWidth / 2, defaultY);

            // 2. 读取卡牌模型
            var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic;
            var modelObj = cardNode.GetType().GetField("_model", flags)?.GetValue(cardNode)
                        ?? cardNode.GetType().GetProperty("Model", flags)?.GetValue(cardNode);

            if (modelObj == null)
            {
                container.Visible = false;
                return;
            }

            string internalName = modelObj.GetType().Name;

            // 基础牌隐藏
            if (internalName.StartsWith("Strike") || internalName.StartsWith("Defend"))
            {
                container.Visible = false;
                foreach (Node child in cardNode.GetChildren())
                {
                    if (child.Name.ToString().StartsWith("CardStatsUI"))
                        (child as CanvasItem)!.Visible = false;
                }
                return;
            }

            container.Visible = true;

            // 3. 计算推荐分
            var deck = GameStateReader.GetDeck();
            var character = GameStateReader.GetCharacter();
            var gold = GameStateReader.GetGold();

            if (deck.Count > 0)
            {
                // 有牌组数据 → 动态推荐
                var archetype = DeckAnalyzer.DetectArchetype(deck, character);
                var deckProfile = DeckAnalyzer.AnalyzeDeck(deck);

                CardRecommendation rec;
                if (isShop)
                    rec = CardScorer.ScoreShopItem(internalName, 0, gold, archetype, deckProfile, deck);
                else
                    rec = CardScorer.Score(internalName, archetype, deckProfile, deck);

                string stars = new string('★', rec.Stars) + new string('☆', 5 - rec.Stars);
                string line1 = $"{stars} {rec.Verdict}";
                string line2 = rec.Reason;
                string line3 = archetype.IsUnformed
                    ? $"牌组{deck.Count}张 | 统计分{rec.FinalScore:F0}"
                    : $"{archetype.DisplayName} | 分数{rec.FinalScore:F0}";

                // 牌组臃肿提示跳过
                if (deckProfile.DeckTooLarge && rec.Stars <= 3)
                    line2 = $"牌组{deck.Count}张，建议跳过";

                label.Text = $"{line1}\n{line2}\n{line3}";
                label.Modulate = Color.FromHtml(rec.ColorHex);
                border.Color = Color.FromHtml(rec.ColorHex);
            }
            else
            {
                // 无牌组数据 → 降级为原始统计模式
                if (CardDatabase.Data.TryGetValue(internalName, out var stats))
                {
                    string rank = stats.Rank;
                    label.Text = $"【{rank}】\n胜率{stats.WinRate}% 选取{stats.PickRate}%\n综合: {stats.Score:F1}";
                    label.Modulate = Color.FromHtml(stats.ColorHex);
                    border.Color = Color.FromHtml(stats.ColorHex);
                }
                else
                {
                    label.Text = $"缺失: {internalName}";
                    label.Modulate = new Color(0.8f, 0.8f, 0.8f, 1);
                    border.Color = new Color(0.3f, 0.3f, 0.3f, 1);
                }
            }
        };
    }
}
