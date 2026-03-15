using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MegaCrit.Sts2.Core.Logging;

namespace CardProbMod;

/// <summary>
/// 通过反射读取游戏状态
/// 路径: RunManager.Instance.State.Players[0].Deck.Cards / .Gold / .Character
/// </summary>
public static class GameStateReader
{
    private static bool _bound;
    private static Type? _runManagerType;
    private static PropertyInfo? _instanceProp;
    private static PropertyInfo? _stateProp;
    private static PropertyInfo? _playersProp;
    private static PropertyInfo? _deckProp;
    private static PropertyInfo? _cardsProp;
    private static PropertyInfo? _goldProp;
    private static PropertyInfo? _characterProp;
    private static PropertyInfo? _relicsProp;

    // 缓存
    private static List<string> _cachedDeck = new();
    private static List<string> _cachedRelics = new();
    private static string _cachedCharacter = "";
    private static int _cachedGold = 0;
    private static DateTime _lastRead = DateTime.MinValue;
    private static readonly TimeSpan ReadInterval = TimeSpan.FromSeconds(1);

    public static void Initialize()
    {
        try
        {
            BindReflectionPaths();
        }
        catch (Exception ex)
        {
            Log.Error($"[CardMod] GameStateReader 初始化失败: {ex.Message}");
        }
    }

    public static List<string> GetDeck()
    {
        TryRefreshState();
        return _cachedDeck;
    }

    public static string GetCharacter()
    {
        TryRefreshState();
        return _cachedCharacter;
    }

    public static int GetGold()
    {
        TryRefreshState();
        return _cachedGold;
    }

    public static List<string> GetRelics()
    {
        TryRefreshState();
        return _cachedRelics;
    }

    private static void BindReflectionPaths()
    {
        // 找到 RunManager 类型
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.GetName().Name != "sts2") continue;

            _runManagerType = asm.GetType("MegaCrit.Sts2.Core.Runs.RunManager");
            if (_runManagerType == null)
            {
                Log.Error("[CardMod] 未找到 RunManager 类型");
                return;
            }
            break;
        }

        if (_runManagerType == null) return;

        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        // RunManager.Instance (static property)
        _instanceProp = _runManagerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        // RunManager.State (private property)
        _stateProp = _runManagerType.GetProperty("State", flags);

        if (_instanceProp == null) Log.Error("[CardMod] 未找到 RunManager.Instance");
        if (_stateProp == null) Log.Error("[CardMod] 未找到 RunManager.State");

        _bound = _instanceProp != null && _stateProp != null;
        Log.Info($"[CardMod] GameStateReader 绑定完成: {(_bound ? "成功" : "失败")}");
    }

    private static void TryRefreshState()
    {
        if (DateTime.Now - _lastRead < ReadInterval) return;
        _lastRead = DateTime.Now;

        if (!_bound) return;

        try
        {
            // RunManager.Instance
            var runManager = _instanceProp!.GetValue(null);
            if (runManager == null) return;

            // .State
            var state = _stateProp!.GetValue(runManager);
            if (state == null) return;

            // .Players (lazy bind)
            if (_playersProp == null)
            {
                _playersProp = state.GetType().GetProperty("Players");
                if (_playersProp == null) { Log.Error("[CardMod] 未找到 State.Players"); return; }
            }

            var players = _playersProp.GetValue(state);
            if (players == null) return;

            // Players[0]
            object? player = null;
            if (players is IList list && list.Count > 0)
                player = list[0];
            else if (players is IEnumerable enumerable)
            {
                foreach (var p in enumerable) { player = p; break; }
            }
            if (player == null) return;

            var playerType = player.GetType();

            // Deck
            if (_deckProp == null)
                _deckProp = playerType.GetProperty("Deck", BindingFlags.Public | BindingFlags.Instance);
            var deck = _deckProp?.GetValue(player);

            if (deck != null)
            {
                if (_cardsProp == null)
                    _cardsProp = deck.GetType().GetProperty("Cards", BindingFlags.Public | BindingFlags.Instance);
                var cards = _cardsProp?.GetValue(deck);

                if (cards is IEnumerable cardEnum)
                {
                    _cachedDeck.Clear();
                    foreach (var card in cardEnum)
                        _cachedDeck.Add(card.GetType().Name);
                }
            }

            // Gold
            if (_goldProp == null)
                _goldProp = playerType.GetProperty("Gold", BindingFlags.Public | BindingFlags.Instance);
            var goldVal = _goldProp?.GetValue(player);
            if (goldVal is int g) _cachedGold = g;

            // Relics
            if (_relicsProp == null)
                _relicsProp = playerType.GetProperty("Relics", BindingFlags.Public | BindingFlags.Instance);
            var relics = _relicsProp?.GetValue(player);
            if (relics is IEnumerable relicEnum)
            {
                _cachedRelics.Clear();
                foreach (var relic in relicEnum)
                    _cachedRelics.Add(relic.GetType().Name);
            }

            // Character
            if (_characterProp == null)
                _characterProp = playerType.GetProperty("Character", BindingFlags.Public | BindingFlags.Instance);
            var charObj = _characterProp?.GetValue(player);
            if (charObj != null)
                _cachedCharacter = charObj.GetType().Name;

            // 如果角色名不在预期列表中，从牌组推断
            if (!IsKnownCharacter(_cachedCharacter) && _cachedDeck.Count > 0)
                _cachedCharacter = DeckAnalyzer.DetectCharacter(_cachedDeck);
        }
        catch (Exception ex)
        {
            Log.Error($"[CardMod] 读取状态失败: {ex.Message}");
        }
    }

    private static bool IsKnownCharacter(string name)
    {
        return name switch
        {
            "Ironclad" or "IroncladCharacter" or "Silent" or "SilentCharacter"
            or "Defect" or "DefectCharacter" or "Regent" or "RegentCharacter"
            or "Necrobinder" or "NecrobinderCharacter" => true,
            _ => false
        };
    }
}
