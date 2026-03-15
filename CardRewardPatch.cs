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

            // Hover 详情层 — detailLabel 作为 detailBg 子节点，开启裁剪防止溢出
            float detailHeight = 280;
            var detailBg = new ColorRect();
            detailBg.Color = new Color(0.03f, 0.03f, 0.03f, 0.97f);
            detailBg.SetSize(new Vector2(boxWidth, detailHeight));
            detailBg.SetPosition(new Vector2(0, -detailHeight - 4));
            detailBg.ClipContents = true;
            detailBg.Visible = false;
            container.AddChild(detailBg);

            var detailLabel = new Label();
            detailLabel.SetSize(new Vector2(boxWidth - 16, detailHeight - 8));
            detailLabel.SetPosition(new Vector2(8, 4));
            detailLabel.HorizontalAlignment = HorizontalAlignment.Left;
            detailLabel.VerticalAlignment = VerticalAlignment.Top;
            detailLabel.AddThemeFontSizeOverride("font_size", 16);
            detailLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f, 1));
            detailLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 1));
            detailLabel.AddThemeConstantOverride("shadow_offset_x", 1);
            detailLabel.AddThemeConstantOverride("shadow_offset_y", 1);
            detailBg.AddChild(detailLabel);

            // 鼠标悬停事件
            bg.MouseFilter = Control.MouseFilterEnum.Stop;
            bg.MouseEntered += () => { detailBg.Visible = true; };
            bg.MouseExited += () => { detailBg.Visible = false; };

            __instance.AddChild(container);

            SetupTracker(__instance, container, border, label, detailLabel, uiName);
        }
        catch (System.Exception ex)
        {
            Log.Error($"[CardMod] 补丁执行出错: {ex.Message}");
        }
    }

    private static void SetupTracker(NCard cardNode, Node2D container, ColorRect border, Label label, Label detailLabel, string myUiName)
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
            bool isDetailView = false;

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
                if (n.Contains("inspect") || n.Contains("detail") || n.Contains("zoom")
                    || n.Contains("preview") || n.Contains("popup") || n.Contains("focus")
                    || n.Contains("enlarged") || n.Contains("magnif"))
                    isDetailView = true;

                current = current.GetParent();
            }

            // 也通过卡牌全局缩放比判断放大模式（备用检测）
            float cardScale = 1f;
            try
            {
                var gt = ((CanvasItem)cardNode).GetGlobalTransform();
                cardScale = gt.X.Length();
                if (cardScale > 1.2f)
                    isDetailView = true;
            }
            catch { }

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

            // 动态排版：放大模式 → 反向缩放 + 移到右侧空白区域
            if (isDetailView)
            {
                float invScale = 1f / cardScale;
                container.Scale = new Vector2(invScale, invScale);
                container.Position = new Vector2(-550f / cardScale, -200f / cardScale);
            }
            else
            {
                container.Scale = Vector2.One;
                if (isShop && !isGridOrDeck)
                    container.Position = new Vector2(-boxWidth / 2, shopY);
                else
                    container.Position = new Vector2(-boxWidth / 2, defaultY);
            }

            // 2. 读取卡牌模型
            var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic;
            var modelObj = cardNode.GetType().GetField("_model", flags)?.GetValue(cardNode)
                        ?? cardNode.GetType().GetProperty("Model", flags)?.GetValue(cardNode);

            if (modelObj == null)
            {
                // 尝试更多属性名
                modelObj = cardNode.GetType().GetProperty("CardModel", flags)?.GetValue(cardNode)
                        ?? cardNode.GetType().GetProperty("Card", flags)?.GetValue(cardNode);
            }
            if (modelObj == null)
            {
                container.Visible = false;
                return;
            }

            string internalName = modelObj.GetType().Name;

            // 基础牌隐藏（牌组查看模式下显示，方便标记删除建议）
            if (!isGridOrDeck && (internalName.StartsWith("Strike") || internalName.StartsWith("Defend")))
            {
                container.Visible = false;
                foreach (Node child in cardNode.GetChildren())
                {
                    if (child.Name.ToString().StartsWith("CardStatsUI"))
                        (child as CanvasItem)!.Visible = false;
                }
                return;
            }

            // 诅咒/状态牌隐藏 — 敌人强制给的牌不需要评分
            bool isCurseOrStatus = false;
            try
            {
                // 检查模型的基类名或 CardType 属性
                var modelType = modelObj.GetType();
                string baseTypeName = modelType.BaseType?.Name ?? "";
                if (baseTypeName.Contains("Curse") || baseTypeName.Contains("Status"))
                    isCurseOrStatus = true;

                // 也检查 CardType / Type 属性
                if (!isCurseOrStatus)
                {
                    var cardTypeProp = modelType.GetProperty("CardType", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                                   ?? modelType.GetProperty("Type", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (cardTypeProp != null)
                    {
                        string typeName = cardTypeProp.GetValue(modelObj)?.ToString() ?? "";
                        if (typeName.Contains("Curse") || typeName.Contains("Status"))
                            isCurseOrStatus = true;
                    }
                }

                // 完整诅咒/状态/Token牌名单
                if (!isCurseOrStatus)
                {
                    isCurseOrStatus = internalName switch
                    {
                        // Curse (18)
                        "AscendersBane" or "BadLuck" or "Clumsy" or "CurseOfTheBell"
                        or "Debt" or "Decay" or "Doubt" or "Enthralled" or "Folly"
                        or "Greed" or "Guilty" or "Injury" or "Normality" or "PoorSleep"
                        or "Regret" or "Shame" or "SporeMind" or "Writhe"
                        // Status (11)
                        or "Beckon" or "Burn" or "Dazed" or "Debris" or "FranticEscape"
                        or "Infection" or "Slimed" or "Soot" or "Toxic" or "Void" or "Wound"
                        // Token状态牌
                        or "Disintegration" or "MindRot" or "Sloth" or "WasteAway"
                        => true,
                        _ => false
                    };
                }
            }
            catch { }

            if (isCurseOrStatus)
            {
                container.Visible = false;
                return;
            }

            container.Visible = true;

            // 3. 计算推荐分
            var deck = GameStateReader.GetDeck();
            var character = GameStateReader.GetCharacter();
            var gold = GameStateReader.GetGold();
            var hp = GameStateReader.GetHp();
            var maxHp = GameStateReader.GetMaxHp();
            var floor = GameStateReader.GetFloor();

            if (deck.Count > 0)
            {
                if (isGridOrDeck)
                {
                    // 牌组查看模式 — 评估适配度、锻造建议
                    var archetype = DeckAnalyzer.DetectArchetype(deck, character);
                    var deckProfile = DeckAnalyzer.AnalyzeDeck(deck);
                    var rec = CardScorer.ScoreDeckCard(internalName, archetype, deckProfile, deck);
                    int formation = CardScorer.ComputeFormation(archetype, deck);

                    string stars = new string('★', rec.Stars) + new string('☆', 5 - rec.Stars);
                    string line1 = $"{stars} {rec.Verdict}";
                    string line2 = rec.Reason;
                    string line3 = archetype.IsUnformed
                        ? $"未成型 | {deck.Count}张"
                        : $"{archetype.DisplayName} {formation}% | {deck.Count}张";

                    label.Text = $"{line1}\n{line2}\n{line3}";
                    label.Modulate = Color.FromHtml(rec.ColorHex);
                    border.Color = Color.FromHtml(rec.ColorHex);
                    detailLabel.Text = rec.Breakdown;
                }
                else
                {
                // 模拟"选了这张牌之后"的牌组，基于模拟牌组评分
                var simDeck = new System.Collections.Generic.List<string>(deck);
                simDeck.Add(internalName);

                var archetype = DeckAnalyzer.DetectArchetype(simDeck, character);
                var deckProfile = DeckAnalyzer.AnalyzeDeck(simDeck);

                CardRecommendation rec;
                if (isShop)
                    rec = CardScorer.ScoreShopItem(internalName, 0, gold, archetype, deckProfile, simDeck);
                else
                    rec = CardScorer.Score(internalName, archetype, deckProfile, simDeck, hp, maxHp, floor);

                // 相对排名：找同级兄弟卡节点，做三选一对比
                if (!isShop)
                {
                    try
                    {
                        var parent = cardNode.GetParent();
                        if (parent != null)
                        {
                            var siblingNames = new System.Collections.Generic.List<string>();
                            var siblingRecs = new System.Collections.Generic.List<CardRecommendation>();
                            int myIndex = -1;

                            foreach (Godot.Node sibling in parent.GetChildren())
                            {
                                if (sibling is NCard sibCard)
                                {
                                    var sibModel = sibCard.GetType().GetField("_model", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(sibCard)
                                                ?? sibCard.GetType().GetProperty("Model", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(sibCard);
                                    if (sibModel == null) continue;
                                    string sibName = sibModel.GetType().Name;
                                    if (sibName.StartsWith("Strike") || sibName.StartsWith("Defend")) continue;

                                    // 每张候选牌都模拟"选了它之后"的牌组
                                    var sibSimDeck = new System.Collections.Generic.List<string>(deck);
                                    sibSimDeck.Add(sibName);
                                    var sibArchetype = DeckAnalyzer.DetectArchetype(sibSimDeck, character);
                                    var sibProfile = DeckAnalyzer.AnalyzeDeck(sibSimDeck);

                                    var sibRec = CardScorer.Score(sibName, sibArchetype, sibProfile, sibSimDeck, hp, maxHp, floor);
                                    siblingNames.Add(sibName);
                                    siblingRecs.Add(sibRec);

                                    if (sibName == internalName)
                                        myIndex = siblingRecs.Count - 1;
                                }
                            }

                            if (siblingRecs.Count >= 2 && myIndex >= 0)
                            {
                                CardScorer.MarkBestPick(siblingRecs, siblingNames);
                                rec = siblingRecs[myIndex];
                            }
                        }
                    }
                    catch { /* 兄弟节点读取失败不影响主逻辑 */ }
                }

                string stars = new string('★', rec.Stars) + new string('☆', 5 - rec.Stars);
                string line1 = $"{stars} {rec.Verdict}";
                string line2 = rec.Reason;
                string line3 = archetype.IsUnformed
                    ? $"牌组{deck.Count}张 | 统计分{rec.FinalScore:F0}"
                    : $"{archetype.DisplayName} | 分数{rec.FinalScore:F0}";

                // line3 += $" [{internalName}]"; // 调试用，正式版关闭

                // 牌组臃肿提示跳过
                if (deckProfile.DeckTooLarge && rec.Stars <= 3)
                    line2 = $"牌组{deck.Count}张，建议跳过";

                label.Text = $"{line1}\n{line2}\n{line3}";
                label.Modulate = Color.FromHtml(rec.ColorHex);
                border.Color = Color.FromHtml(rec.ColorHex);
                detailLabel.Text = rec.Breakdown;
                }
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
