using System.Collections.Generic;
using NineGrid.Data;
using UnityEngine;
using UnityEngine.UI;

namespace NineGrid.GameFlow
{
    /// <summary>
    /// 事件系统控制器 —— 常驻单例，由 SceneFlowDirector 在 Event* 状态调用。
    /// 生成事件选项、显示叙事文本、处理玩家选择、应用效果、完成后推进主流程。
    /// 对应 web systems/post-battle.js + input/index.js handleEventClick。
    /// </summary>
    public sealed class EventController : MonoBehaviour
    {
        public static EventController Instance { get; private set; }

        /// <summary>事件面板是否正在显示（供 SceneFlowDirector 检查是否完成）。</summary>
        public bool IsPanelActive => _canvas != null && _canvas.gameObject.activeSelf;

        Canvas _canvas;
        Text _titleText;
        Text _bodyText;
        Text _optionsText;
        Text _hintText;

        /// <summary>当前可选事件列表。</summary>
        List<EventDef> _currentOptions = new();
        /// <summary>当前选中的事件（效果应用中）。</summary>
        EventDef _selectedEvent;
        /// <summary>是否在选矿模式。</summary>
        bool _pickOreMode;
        /// <summary>选矿模式下的矿石列表索引。</summary>
        List<int> _pickOreIndices = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Instance == null)
            {
                var go = new GameObject("[EventController]");
                var cmp = go.AddComponent<EventController>();
                DontDestroyOnLoad(go);
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildUI();
        }

        void Update()
        {
            if (_canvas == null || !_canvas.gameObject.activeSelf) return;

            // 数字键选择
            if (_pickOreMode)
            {
                HandlePickOreInput();
            }
            else if (_currentOptions.Count > 0)
            {
                HandleEventSelectionInput();
            }
            else if (_selectedEvent != null)
            {
                // 效果已应用，等待按键继续
                if (Input.anyKeyDown)
                {
                    FinishEvent();
                }
            }
        }

        /// <summary>由 SceneFlowDirector 调用：显示事件选择面板。</summary>
        public void ShowEvent(GameFlowState state)
        {
            var poolType = RunData.GetEventPoolType(state);
            var run = RunData.Ensure();

            // 生成事件选项
            if (run.PendingEventNames == null || run.PendingEventNames.Count == 0)
            {
                var events = WebGameData.GeneratePostBattleEvents(poolType);
                _currentOptions = events;
                run.PendingEventNames.Clear();
                foreach (var e in events) run.PendingEventNames.Add(e.Name);
                RunData.Save();
            }
            else
            {
                // 从已保存的事件名重建
                _currentOptions.Clear();
                foreach (var name in run.PendingEventNames)
                {
                    var ev = FindEventByName(name);
                    if (ev != null) _currentOptions.Add(ev);
                }
                if (_currentOptions.Count == 0)
                {
                    // fallback
                    _currentOptions = WebGameData.GeneratePostBattleEvents(poolType);
                }
            }

            _selectedEvent = null;
            _pickOreMode = false;
            ShowPanel(BuildEventSelectionText());
        }

        /// <summary>由 SceneFlowDirector 调用：显示 BOSS 改造三选一面。</summary>
        public void ShowBossRelicSelection()
        {
            var run = RunData.Ensure();
            _currentOptions.Clear();
            _selectedEvent = null;
            _pickOreMode = false;

            if (run.BossRelicOptions == null || run.BossRelicOptions.Count == 0)
            {
                // fallback: 生成
                var relics = WebGameData.PickBossRelics("gold_king_flagship");
                run.BossRelicOptions.Clear();
                foreach (var r in relics) run.BossRelicOptions.Add(r.DisplayName);
                RunData.Save();
            }

            ShowPanel(BuildBossRelicText());
        }

        // ===== UI 构建 =====

        void BuildUI()
        {
            var canvasGo = new GameObject("[EventPanel]");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32750;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasGo.AddComponent<GraphicRaycaster>();

            // 半透明背景
            var bgGo = new GameObject("BG");
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.05f, 0.03f, 0.02f, 0.92f);
            var bgRect = bgImg.rectTransform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // 内容容器
            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(canvasGo.transform, false);
            var contentRect = contentGo.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.15f, 0.1f);
            contentRect.anchorMax = new Vector2(0.85f, 0.9f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            // 标题
            _titleText = CreateText("Title", contentGo.transform, new Vector2(0, 0.85f), new Vector2(1, 1), 32);
            _titleText.alignment = TextAnchor.UpperCenter;
            _titleText.color = new Color(0.95f, 0.8f, 0.5f);

            // 选项正文
            _optionsText = CreateText("Options", contentGo.transform, new Vector2(0, 0.25f), new Vector2(1, 0.82f), 22);
            _optionsText.alignment = TextAnchor.UpperLeft;

            // 底部提示
            _hintText = CreateText("Hint", contentGo.transform, new Vector2(0, 0), new Vector2(1, 0.2f), 18);
            _hintText.alignment = TextAnchor.LowerCenter;
            _hintText.color = new Color(0.7f, 0.7f, 0.7f);

            _canvas.gameObject.SetActive(false);
        }

        Text CreateText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, int fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = Color.white;
            var rect = text.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(10, 5);
            rect.offsetMax = new Vector2(-10, -5);
            return text;
        }

        void ShowPanel(string text)
        {
            if (_canvas == null) BuildUI();
            _optionsText.text = text;
            _hintText.text = "按数字键选择 | ESC 跳过";
            _canvas.gameObject.SetActive(true);
        }

        void HidePanel()
        {
            if (_canvas != null)
                _canvas.gameObject.SetActive(false);
        }

        // ===== 文本构建 =====

        string BuildEventSelectionText()
        {
            var sb = new System.Text.StringBuilder();
            var run = RunData.Ensure();
            sb.AppendLine($"<size=36>航行事件</size>");
            sb.AppendLine($"银元: {run.Gold}  矿舱: {run.Deck.Count}块  改造: {run.Relics.Count}件");
            sb.AppendLine("");
            for (var i = 0; i < _currentOptions.Count; i++)
            {
                var ev = _currentOptions[i];
                sb.AppendLine($"<b>[{i + 1}] {ev.Name}</b> ({ev.Tier})");
                sb.AppendLine($"    {ev.Desc}");
                sb.AppendLine("");
            }
            return sb.ToString();
        }

        string BuildBossRelicText()
        {
            var sb = new System.Text.StringBuilder();
            var run = RunData.Ensure();
            sb.AppendLine($"<size=36>旗舰改造选择</size>");
            sb.AppendLine($"击败旗舰！选择一件改造：");
            sb.AppendLine("");
            for (var i = 0; i < run.BossRelicOptions.Count; i++)
            {
                var name = run.BossRelicOptions[i];
                var def = WebGameData.GetRelicByName(name);
                sb.AppendLine($"<b>[{i + 1}] {name}</b>");
                if (def != null) sb.AppendLine($"    {def.Desc}");
                sb.AppendLine("");
            }
            return sb.ToString();
        }

        string BuildPickOreText(string prompt)
        {
            var sb = new System.Text.StringBuilder();
            var run = RunData.Ensure();
            sb.AppendLine($"<size=32>{prompt}</size>");
            sb.AppendLine("");
            _pickOreIndices.Clear();
            for (var i = 0; i < run.Deck.Count && i < 12; i++)
            {
                var entry = run.Deck[i];
                sb.AppendLine($"[{i + 1}] {entry.OreId} (+{entry.PermanentBonus})");
                _pickOreIndices.Add(i);
            }
            sb.AppendLine("");
            sb.AppendLine("按数字键选择矿石");
            return sb.ToString();
        }

        // ===== 输入处理 =====

        void HandleEventSelectionInput()
        {
            for (var i = 0; i < _currentOptions.Count && i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                {
                    SelectEvent(i);
                    return;
                }
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                // 跳过事件
                FinishEvent();
            }
        }

        void HandlePickOreInput()
        {
            var run = RunData.Ensure();
            for (var i = 0; i < _pickOreIndices.Count && i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                {
                    var deckIdx = _pickOreIndices[i];
                    ApplyPickOreEffect(_selectedEvent, deckIdx);
                    return;
                }
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _pickOreMode = false;
                ShowPanel(BuildEventSelectionText());
            }
        }

        // ===== 事件选择与效果应用 =====

        void SelectEvent(int index)
        {
            if (index < 0 || index >= _currentOptions.Count) return;
            _selectedEvent = _currentOptions[index];
            var ev = _selectedEvent;

            // 从待选列表中移除已选
            var run = RunData.Ensure();
            run.PendingEventNames.Remove(ev.Name);
            RunData.Save();

            // 根据效果类型决定是否需要选矿
            if (NeedsOreSelection(ev.Effect))
            {
                _pickOreMode = true;
                ShowPanel(BuildPickOreText(ev.Name + " - 选择矿石"));
            }
            else
            {
                // 直接生效
                ApplyEventEffect(ev, -1);
            }
        }

        bool NeedsOreSelection(string effect)
        {
            return effect == "buff_card" || effect == "enchant_spread" || effect == "enchant_mighty"
                || effect == "duplicate_card" || effect == "transform_card";
        }

        void ApplyPickOreEffect(EventDef ev, int deckIndex)
        {
            _pickOreMode = false;
            ApplyEventEffect(ev, deckIndex);
        }

        void ApplyEventEffect(EventDef ev, int oreIndex)
        {
            var run = RunData.Ensure();
            var success = true;

            switch (ev.Effect)
            {
                case "gain_gold":
                    run.Gold += ev.ParamInt;
                    Debug.Log($"[Event] {ev.Name}: +{ev.ParamInt}银元（剩余{run.Gold}）");
                    break;

                case "random_strategy_level":
                    run.Gold += 1;
                    Debug.Log($"[Event] {ev.Name}: +1银元（叠牌已废弃）");
                    break;

                case "buff_card":
                    if (oreIndex >= 0 && oreIndex < run.Deck.Count)
                    {
                        run.Deck[oreIndex].PermanentBonus += ev.ParamInt;
                        Debug.Log($"[Event] {ev.Name}: 矿石{run.Deck[oreIndex].OreId} +{ev.ParamInt}强度");
                    }
                    break;

                case "self_blacksmith":
                    if (run.Gold >= ev.ParamInt)
                    {
                        run.Gold -= ev.ParamInt;
                        var slot = Random.Range(0, 3);
                        run.SlotUpgrades[slot]++;
                        Debug.Log($"[Event] {ev.Name}: -{ev.ParamInt}银元，铸造台{slot}+1倍率");
                    }
                    else
                    {
                        Debug.Log("[Event] 金币不足，效果无效");
                        success = false;
                    }
                    break;

                case "card_pick_three":
                    // 生成3个随机矿石供选择
                    var options = new List<string>();
                    for (var i = 0; i < 3; i++)
                        options.Add(PickRandomOreId());
                    _currentOptions.Clear();
                    for (var i = 0; i < options.Count; i++)
                    {
                        _currentOptions.Add(new EventDef
                        {
                            Name = options[i],
                            Desc = $"获得 {options[i]}",
                            Effect = "gain_specific_card",
                            ParamStr = options[i]
                        });
                    }
                    ShowPanel(BuildEventSelectionText());
                    return; // 不结束，进入二次选择

                case "free_remove_card":
                    if (oreIndex >= 0 && oreIndex < run.Deck.Count)
                    {
                        var removed = run.Deck[oreIndex].OreId;
                        run.RemoveOreAt(oreIndex);
                        Debug.Log($"[Event] {ev.Name}: 删除矿石{removed}");
                    }
                    else
                    {
                        // 进入选矿模式
                        _pickOreMode = true;
                        _selectedEvent = ev;
                        ShowPanel(BuildPickOreText(ev.Name + " - 选择要删除的矿石"));
                        return;
                    }
                    break;

                case "buy_random_relic":
                    if (run.Gold >= ev.ParamInt)
                    {
                        run.Gold -= ev.ParamInt;
                        // 随机一件非boss遗物
                        var pool = new List<RelicDef>();
                        foreach (var r in WebGameData.Relics)
                            if (r.Rarity != "boss" && !run.Relics.Contains(r.DisplayName))
                                pool.Add(r);
                        if (pool.Count > 0)
                        {
                            var picked = pool[Random.Range(0, pool.Count)];
                            run.AddRelic(picked.DisplayName);
                            Debug.Log($"[Event] {ev.Name}: -{ev.ParamInt}银元，获得{picked.DisplayName}");
                        }
                    }
                    else
                    {
                        Debug.Log("[Event] 金币不足");
                        success = false;
                    }
                    break;

                case "pick_rare_relic":
                    // 三选一中级遗物
                    _currentOptions.Clear();
                    var relicPool = new List<RelicDef>();
                    foreach (var r in WebGameData.Relics)
                        if (r.Rarity == "rare" && !run.Relics.Contains(r.DisplayName))
                            relicPool.Add(r);
                    for (var i = relicPool.Count - 1; i > 0; i--)
                    {
                        var j = Random.Range(0, i + 1);
                        (relicPool[i], relicPool[j]) = (relicPool[j], relicPool[i]);
                    }
                    for (var i = 0; i < Mathf.Min(3, relicPool.Count); i++)
                    {
                        var r = relicPool[i];
                        _currentOptions.Add(new EventDef
                        {
                            Name = r.DisplayName,
                            Desc = r.Desc,
                            Effect = "gain_relic",
                            ParamStr = r.DisplayName
                        });
                    }
                    ShowPanel(BuildEventSelectionText());
                    return;

                case "gain_relic":
                    if (!string.IsNullOrEmpty(ev.ParamStr))
                    {
                        run.AddRelic(ev.ParamStr);
                        Debug.Log($"[Event] 获得船体改造：{ev.ParamStr}");
                    }
                    break;

                case "gain_specific_card":
                    var oreId = !string.IsNullOrEmpty(ev.ParamStr) ? ev.ParamStr : ev.Name;
                    run.AddOre(oreId);
                    Debug.Log($"[Event] 获得矿石：{oreId}");
                    break;

                case "next_monster_hp_down":
                    run.NextMonsterHpPenalty = ev.ParamInt;
                    Debug.Log($"[Event] {ev.Name}: 下场敌舰-{ev.ParamInt}装甲");
                    break;

                case "next_battle_hearts_plus":
                    run.NextBattleHeartBonus = ev.ParamInt;
                    Debug.Log($"[Event] {ev.Name}: 下场备用锚+{ev.ParamInt}");
                    break;

                case "max_hearts_plus":
                    run.MaxHearts += 1;
                    Debug.Log($"[Event] {ev.Name}: 备用锚永久+1（当前{run.MaxHearts}）");
                    break;

                case "enchant_mighty":
                    if (oreIndex >= 0 && oreIndex < run.Deck.Count)
                    {
                        run.Deck[oreIndex].TraitsInt |= (int)OreTrait.Core;
                        Debug.Log($"[Event] {ev.Name}: 矿石获得熔核特性");
                    }
                    break;

                case "enchant_spread":
                    if (oreIndex >= 0 && oreIndex < run.Deck.Count)
                    {
                        run.Deck[oreIndex].TraitsInt |= (int)OreTrait.Debris;
                        Debug.Log($"[Event] {ev.Name}: 矿石获得碎屑特性");
                    }
                    break;

                case "duplicate_card":
                    if (oreIndex >= 0 && oreIndex < run.Deck.Count)
                    {
                        var src = run.Deck[oreIndex];
                        run.AddOre(src.OreId, src.PermanentBonus);
                        Debug.Log($"[Event] {ev.Name}: 复制矿石{src.OreId}");
                    }
                    break;

                case "transform_card":
                    if (oreIndex >= 0 && oreIndex < run.Deck.Count)
                    {
                        var newOre = PickRandomOreId();
                        run.Deck[oreIndex].OreId = newOre;
                        Debug.Log($"[Event] {ev.Name}: 矿石变为{newOre}");
                    }
                    break;

                case "grow_cards_twice":
                    foreach (var entry in run.Deck)
                        entry.PermanentBonus += 2;
                    Debug.Log($"[Event] {ev.Name}: 所有矿石+2强度");
                    break;

                case "fight_monster":
                case "fight_elite":
                    Debug.Log($"[Event] {ev.Name}: 遭遇战（占位，直接跳过）");
                    break;

                case "next_shop_system":
                    Debug.Log($"[Event] {ev.Name}: 下次精炼厂指定矿脉（占位）");
                    break;

                default:
                    Debug.Log($"[Event] {ev.Name}: 效果 {ev.Effect} 未实现");
                    break;
            }

            RunData.Save();

            // 显示结果
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"<size=32>{ev.Name}</size>");
            sb.AppendLine("");
            sb.AppendLine(ev.Desc);
            sb.AppendLine("");
            sb.AppendLine(success ? "效果已生效。" : "金币不足，效果无效。");
            sb.AppendLine("");
            sb.AppendLine("<size=20>按任意键继续...</size>");
            ShowPanel(sb.ToString());
        }

        void FinishEvent()
        {
            _selectedEvent = null;
            _currentOptions.Clear();
            _pickOreMode = false;
            HidePanel();

            // 推进主流程
            GameFlowController.Instance?.Advance();
        }

        // ===== 辅助 =====

        EventDef FindEventByName(string name)
        {
            foreach (var e in WebGameData.CommonEvents) if (e.Name == name) return e;
            foreach (var e in WebGameData.RareEvents) if (e.Name == name) return e;
            foreach (var e in WebGameData.LegendaryEvents) if (e.Name == name) return e;
            return null;
        }

        string PickRandomOreId()
        {
            // 从 OreCatalog 随机选一个
            var catalog = LoadOreCatalog();
            if (catalog == null || catalog.Entries.Count == 0) return "ore_chutie";
            return catalog.Entries[Random.Range(0, catalog.Entries.Count)].OreId;
        }

        OreCatalog _cachedCatalog;
        OreCatalog LoadOreCatalog()
        {
            if (_cachedCatalog != null) return _cachedCatalog;
#if UNITY_EDITOR
            _cachedCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<OreCatalog>(
                "Assets/ScriptableObjects/Data/OreCatalog.asset");
#endif
            return _cachedCatalog;
        }
    }
}
