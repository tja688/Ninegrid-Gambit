#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System;
using System.Collections.Generic;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Flow;
using NineGrid.Flow.Diagnostics;
using NineGrid.Flow.Presentation;
using QFramework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace NineGrid.Presentation.Cheat
{
    /// <summary>
    /// 作弊工具面板控制器（F12 综合测试后门）。
    /// 优先接线 MainScene 预置的「作弊工具BG」（SpriteRenderer + BoxCollider2D 世界 UI）；
    /// 场景完全缺失时才做最小运行时兜底，方便 EditMode 契约测试。
    /// 仅 UNITY_EDITOR / DEVELOPMENT_BUILD 编译，正式包不含。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CheatToolPanelController : MonoBehaviour
    {
        public const string PanelRootName = "作弊工具BG";
        private const string FirstLayerName = "第一层主面板";
        private const string SecondLayerName = "第二层_添加卡菜单";
        private const string LogSecondLayerName = "第二层_log记录面板";
        private const string CloseButtonName = "关闭按钮";
        private const string ClearNodeButtonName = "一键清关选项";
        private const string CrossFloorButtonName = "一键跨层选项";
        private const string AddCardButtonName = "战斗加卡选项";
        private const string AddRelicButtonName = "添加遗物选项";
        private const string CoinsButtonName = "无限金币选项";
        private const string HealButtonName = "回复满血选项";
        private const string GodModeButtonName = "无敌模式选项";
        private const string LogRecordButtonName = "记录log选项";
        private const string LogNoticeTextName = "notice text";
        private const string LogSaveButtonName = "Button";
        private const string LogInputFieldName = "InputField (TMP)";
        private const string SecondLayerBlockerName = "第二层命中遮罩";
        private const string LogSecondLayerBlockerName = "log记录层命中遮罩";
        private const string OptionFontAssetPath = "Assets/Arts/Fronts/DeYiHei/SmileySans-Oblique-3 SDF.asset";
        private const string OptionFontAssetName = "SmileySans-Oblique-3 SDF";
        private const string CardSearchPlaceholder = "输入卡名 / 卡组 / 技能…";
        private const string RelicSearchPlaceholder = "输入遗物名 / 描述…";
        private const int CoinsPerClick = 999;
        private const float OptionRowHeight = 36f;
        private const int SecondLayerBlockerHitSort = CheatToolPanelButton.HitSort + 1;

        private enum SearchCatalogMode
        {
            Cards,
            Relics,
        }

        private static CheatToolPanelController sInstance;

        private bool _bound;
        private Transform _firstLayer;
        private Transform _secondLayer;
        private Transform _logLayer;
        private BoxCollider2D _rootBlocker;

        private Canvas _secondCanvas;
        private Canvas _logCanvas;
        private TMP_InputField _inputField;
        private TMP_InputField _logTagInput;
        private TMP_Text _logNoticeText;
        private Button _logSaveButton;
        private ScrollRect _scrollRect;
        private RectTransform _content;
        private TMP_FontAsset _font;
        private TMP_FontAsset _optionFont;
        private string _logNoticeDefault;

        private SearchCatalogMode _searchMode = SearchCatalogMode.Cards;
        private List<CheatToolCardSearchIndex.CardEntry> _cardEntries;
        private List<CheatToolRelicSearchIndex.RelicEntry> _relicEntries;

        private void Awake()
        {
            sInstance = this;
        }

        private void OnDestroy()
        {
            if (sInstance == this)
            {
                sInstance = null;
            }
        }

        private void Update()
        {
            if (!KeyboardUtility.GetKeyDown(KeyCode.Escape))
            {
                return;
            }

            if (_logLayer != null && _logLayer.gameObject.activeSelf)
            {
                CloseLogRecordMenu();
            }
            else if (_secondLayer != null && _secondLayer.gameObject.activeSelf)
            {
                CloseAddCardMenu();
            }
            else
            {
                ClosePanel();
            }
        }

        /// <summary>
        /// F12 宿主入口：先找已挂控制器的实例（含失活），再找场景预置「作弊工具BG」，最后才运行时兜底。
        /// </summary>
        public static void TryToggle()
        {
            if (sInstance != null)
            {
                sInstance.Toggle();
                return;
            }

            var controllers = Resources.FindObjectsOfTypeAll<CheatToolPanelController>();
            for (var i = 0; i < controllers.Length; i++)
            {
                var controller = controllers[i];
                if (controller == null || !IsSceneObject(controller.gameObject))
                {
                    continue;
                }

                controller.Toggle();
                return;
            }

            var root = FindScenePanelRoot();
            if (root != null)
            {
                var controller = root.GetComponent<CheatToolPanelController>();
                if (controller == null)
                {
                    controller = root.gameObject.AddComponent<CheatToolPanelController>();
                }

                controller.Toggle();
                return;
            }

            Debug.LogWarning(
                "[CheatTool] 场景中未找到预置「" + PanelRootName
                + "」。将创建运行时兜底面板（仅测试用）。");

            var go = new GameObject(PanelRootName);
            var fallback = go.AddComponent<CheatToolPanelController>();
            go.SetActive(false);
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(go);
            }

            fallback.Toggle();
        }

        public void Toggle()
        {
            if (gameObject.activeSelf)
            {
                ClosePanel();
            }
            else
            {
                OpenPanel();
            }
        }

        public void OpenPanel()
        {
            gameObject.SetActive(true);
            EnsureBindings();
            if (_firstLayer != null)
            {
                _firstLayer.gameObject.SetActive(true);
            }

            CloseAllSecondLayers();
            Debug.Log("[CheatTool] 作弊面板已打开（F12 切换 / Esc 关闭）。");
        }

        public void ClosePanel()
        {
            CloseAllSecondLayers();
            gameObject.SetActive(false);
        }

        private void CloseAllSecondLayers()
        {
            CloseAddCardMenu(restoreFirstLayer: false);
            CloseLogRecordMenu(restoreFirstLayer: true);
        }

        // ============ 一级菜单功能 ============

        private void ForceClearNode()
        {
            // 对齐 QuickTest「-」(KeypadMinus) → CheatForceNodeVictoryCommand → TryForceNodeVictory。
            if (!BattleSessionCheat.TryForceNodeVictory())
            {
                Debug.LogWarning("[CheatTool] 一键清关失败（仅战斗/结算阶段可用）。");
            }
        }

        private void ForceCrossFloor()
        {
            ClosePanel();
            if (!BattleSessionCheat.TryBeginCrossFloor())
            {
                Debug.LogWarning("[CheatTool] 一键跨层失败（需对局中且非第三层）。");
            }
        }

        private void AddCoins()
        {
            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            var player = arch?.GetModel<PlayerModel>();
            if (arch == null || player == null)
            {
                Debug.LogWarning("[CheatTool] 玩家模型未就绪，加金币失败。");
                return;
            }

            player.AddCoins(CoinsPerClick);
            Debug.Log("[CheatTool] 金币 +" + CoinsPerClick + " → " + player.Coins.Value);
        }

        private void HealAvatarFull()
        {
            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            if (arch == null)
            {
                Debug.LogWarning("[CheatTool] 架构未就绪，回复满血失败。");
                return;
            }

            var board = arch.GetModel<BoardModel>();
            var avatarUid = board != null ? board.AvatarUid.Value : 0;
            if (avatarUid <= 0
                || !arch.GetModel<CardRegistry>().TryGet(avatarUid, out var avatar))
            {
                Debug.LogWarning("[CheatTool] Avatar 不存在（需在战斗中），回复满血失败。");
                return;
            }

            var maxHp = (int)avatar.Stats.GetBase(StatId.MaxHp);
            if (maxHp <= 0 || !BattleSessionCheat.TrySetAvatarHp(maxHp))
            {
                Debug.LogWarning("[CheatTool] 回复满血失败。");
                return;
            }

            PlayerInfoHudPresenter.TryGetInstance()?.SyncFromCore(animate: false);
            Debug.Log("[CheatTool] 回复满血 → " + maxHp);
        }

        // ============ 二级菜单：战斗加卡 / 添加遗物（共用搜索面板） ============

        public void OpenAddCardMenu()
        {
            OpenSearchMenu(SearchCatalogMode.Cards);
        }

        public void OpenAddRelicMenu()
        {
            OpenSearchMenu(SearchCatalogMode.Relics);
        }

        private void OpenSearchMenu(SearchCatalogMode mode)
        {
            EnsureBindings();
            if (_secondLayer == null)
            {
                Debug.LogWarning("[CheatTool] 未找到「第二层_添加卡菜单」。");
                return;
            }

            CloseLogRecordMenu(restoreFirstLayer: false);

            if (_firstLayer != null)
            {
                // 打开二级时关掉一级，避免 BoxCollider2D 与 WorldSpace UI 抢点。
                _firstLayer.gameObject.SetActive(false);
            }

            _searchMode = mode;
            _secondLayer.gameObject.SetActive(true);
            _cardEntries = null;
            _relicEntries = null;
            ApplySearchPlaceholder();

            if (_inputField != null)
            {
                _inputField.text = string.Empty;
                RefreshResultList();
                _inputField.ActivateInputField();
                if (EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(_inputField.gameObject);
                }
            }
            else
            {
                RefreshResultList();
            }
        }

        private void ApplySearchPlaceholder()
        {
            if (_inputField == null || !(_inputField.placeholder is TMP_Text placeholder))
            {
                return;
            }

            placeholder.text = _searchMode == SearchCatalogMode.Relics
                ? RelicSearchPlaceholder
                : CardSearchPlaceholder;
        }

        public void CloseAddCardMenu()
        {
            CloseAddCardMenu(restoreFirstLayer: true);
        }

        private void CloseAddCardMenu(bool restoreFirstLayer)
        {
            if (_inputField != null)
            {
                _inputField.DeactivateInputField();
                _inputField.text = string.Empty;
                if (EventSystem.current != null
                    && EventSystem.current.currentSelectedGameObject == _inputField.gameObject)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }
            }

            ClearOptionRows();

            if (_secondLayer != null && _secondLayer.gameObject.activeSelf)
            {
                _secondLayer.gameObject.SetActive(false);
            }

            if (restoreFirstLayer)
            {
                RestoreFirstLayerIfNoSecondOpen();
            }
        }

        public void OpenLogRecordMenu()
        {
            EnsureBindings();
            if (_logLayer == null)
            {
                Debug.LogWarning("[CheatTool] 未找到「" + LogSecondLayerName + "」。");
                return;
            }

            CloseAddCardMenu(restoreFirstLayer: false);

            if (_firstLayer != null)
            {
                _firstLayer.gameObject.SetActive(false);
            }

            _logLayer.gameObject.SetActive(true);
            ResetLogNotice();

            if (_logTagInput != null)
            {
                _logTagInput.text = string.Empty;
                if (_logTagInput.placeholder is TMP_Text placeholder
                    && (string.IsNullOrWhiteSpace(placeholder.text)
                        || placeholder.text.StartsWith("Enter text", StringComparison.OrdinalIgnoreCase)))
                {
                    placeholder.text = "描述问题，例如：顺劈斧攻击异常";
                }

                _logTagInput.ActivateInputField();
                if (EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(_logTagInput.gameObject);
                }
            }
        }

        public void CloseLogRecordMenu()
        {
            CloseLogRecordMenu(restoreFirstLayer: true);
        }

        private void CloseLogRecordMenu(bool restoreFirstLayer)
        {
            if (_logTagInput != null)
            {
                _logTagInput.DeactivateInputField();
                if (EventSystem.current != null
                    && EventSystem.current.currentSelectedGameObject == _logTagInput.gameObject)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }
            }

            if (_logLayer != null && _logLayer.gameObject.activeSelf)
            {
                _logLayer.gameObject.SetActive(false);
            }

            if (restoreFirstLayer)
            {
                RestoreFirstLayerIfNoSecondOpen();
            }
        }

        private void RestoreFirstLayerIfNoSecondOpen()
        {
            if (!gameObject.activeSelf || _firstLayer == null)
            {
                return;
            }

            var addOpen = _secondLayer != null && _secondLayer.gameObject.activeSelf;
            var logOpen = _logLayer != null && _logLayer.gameObject.activeSelf;
            if (!addOpen && !logOpen)
            {
                _firstLayer.gameObject.SetActive(true);
            }
        }

        private void SaveLogSnapshot()
        {
            EnsureBindings();
            var tag = _logTagInput != null ? _logTagInput.text : string.Empty;
            if (string.IsNullOrWhiteSpace(tag))
            {
                SetLogNotice("请先填写问题 Tag（例如：顺劈斧攻击异常），再点保存。");
                return;
            }

            var result = DiagTraceManualSnapshot.Save(tag);
            SetLogNotice(result.Message);
            if (result.Success)
            {
                Debug.Log("[CheatTool] 手动 log 快照已保存：" + result.FolderPath);
            }
        }

        private void ResetLogNotice()
        {
            if (_logNoticeText == null)
            {
                return;
            }

            _logNoticeText.text = string.IsNullOrEmpty(_logNoticeDefault)
                ? string.Empty
                : _logNoticeDefault;
        }

        private void SetLogNotice(string message)
        {
            if (_logNoticeText != null)
            {
                _logNoticeText.text = message ?? string.Empty;
            }
        }

        private void TryAddCardToDeckTop(string defId)
        {
            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            if (arch == null)
            {
                Debug.LogWarning("[CheatTool] 架构未就绪，加卡失败。");
                return;
            }

            var phase = arch.GetSystem<IPhaseSystem>();
            var current = phase != null ? phase.CurrentPhase : GamePhase.None;
            if (current != GamePhase.BuildEnemyPool
                && current != GamePhase.ResetNode
                && current != GamePhase.DealOpeningCards
                && current != GamePhase.InteractionLoop)
            {
                Debug.LogWarning("[CheatTool] 加卡仅战斗中可用（当前 phase=" + current + "）。");
                return;
            }

            var content = arch.GetSystem<IContentSystem>();
            if (content == null
                || !content.HasCatalog
                || !content.Catalog.TryGetCard(defId, out var def))
            {
                Debug.LogWarning("[CheatTool] 找不到卡定义 " + defId + "。");
                return;
            }

            var pipeline = arch.GetSystem<IActionPipelineSystem>();
            if (pipeline == null || pipeline.EventLog == null)
            {
                Debug.LogWarning("[CheatTool] 动作管线未就绪，加卡失败。");
                return;
            }

            var start = pipeline.EventLog.Entries.Count;
            pipeline.Enqueue(new ShuffleIntoDrawPileAction(defId, def.Kind, 1, true, "CheatToolPanel"));
            pipeline.RunToCompletion();
            BattleBeatFlush.PresentEventLogSlice(arch, start);

            Debug.Log("[CheatTool] 已把「" + def.DisplayName + "」塞入卡组顶（defId=" + defId + "）。");
            CloseAddCardMenu();
        }

        private void TryAddRelicToInventory(string defId)
        {
            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            if (arch == null)
            {
                Debug.LogWarning("[CheatTool] 架构未就绪，加遗物失败。");
                return;
            }

            var player = arch.GetModel<PlayerModel>();
            if (player == null)
            {
                Debug.LogWarning("[CheatTool] 玩家模型未就绪，加遗物失败（需已开局）。");
                return;
            }

            var content = arch.GetSystem<IContentSystem>();
            if (content == null
                || !content.HasCatalog
                || !content.Catalog.TryGetRelic(defId, out var relic)
                || relic == null)
            {
                Debug.LogWarning("[CheatTool] 找不到遗物定义 " + defId + "。");
                return;
            }

            if (RelicDecks.IsArchive(relic.DeckId))
            {
                Debug.LogWarning("[CheatTool] 归档遗物不可添加：" + defId + "。");
                return;
            }

            if (OwnsRelic(player, defId))
            {
                Debug.LogWarning("[CheatTool] 已拥有遗物「" + relic.DisplayName + "」。");
                return;
            }

            if (player.IsRelicInventoryFull)
            {
                Debug.LogWarning("[CheatTool] 遗物栏已满（" + PlayerModel.MaxRelicSlots + "），请先丢弃。");
                return;
            }

            var pipeline = arch.GetSystem<IActionPipelineSystem>();
            if (pipeline == null || pipeline.EventLog == null)
            {
                Debug.LogWarning("[CheatTool] 动作管线未就绪，加遗物失败。");
                return;
            }

            var start = pipeline.EventLog.Entries.Count;
            pipeline.Enqueue(new GrantRelicAction(defId));
            pipeline.RunToCompletion();
            BattleBeatFlush.PresentEventLogSlice(arch, start);
            RelicHudHook.RequestWire();
            RelicHudHook.RequestSync();

            if (!OwnsRelic(player, defId))
            {
                Debug.LogWarning("[CheatTool] 加遗物未生效（defId=" + defId + "）。");
                return;
            }

            Debug.Log("[CheatTool] 已获得遗物「" + relic.DisplayName + "」（defId=" + defId + "）。");
            CloseAddCardMenu();
        }

        private static bool OwnsRelic(PlayerModel player, string defId)
        {
            if (player == null || string.IsNullOrEmpty(defId))
            {
                return false;
            }

            var relics = player.RelicDefIds;
            for (var i = 0; i < relics.Count; i++)
            {
                if (relics[i] == defId)
                {
                    return true;
                }
            }

            return false;
        }

        // ============ 接线 ============

        private void EnsureBindings()
        {
            if (_bound)
            {
                return;
            }

            _bound = true;
            EnsureMinimalHierarchyIfEmpty();
            EnsureEventSystem();
            EnsureRootBlocker();

            _firstLayer = FindChild(FirstLayerName);
            _secondLayer = FindChild(SecondLayerName);
            _logLayer = FindChild(LogSecondLayerName);
            if (_firstLayer == null)
            {
                Debug.LogWarning("[CheatTool] 未找到「" + FirstLayerName + "」。");
            }

            BindHitButton(CloseButtonName, ClosePanel);
            BindHitButton(ClearNodeButtonName, ForceClearNode);
            BindHitButton(CrossFloorButtonName, ForceCrossFloor);
            BindHitButton(AddCardButtonName, OpenAddCardMenu);
            BindHitButton(AddRelicButtonName, OpenAddRelicMenu);
            BindHitButton(CoinsButtonName, AddCoins);
            BindHitButton(HealButtonName, HealAvatarFull);
            BindHitButton(GodModeButtonName, CheatToolGodMode.Toggle);
            BindHitButton(LogRecordButtonName, OpenLogRecordMenu);

            if (_secondLayer != null)
            {
                EnsureSecondLayerUiInteractable();
                EnsureSecondLayerPointerBlocker(_secondLayer, SecondLayerBlockerName);

                _inputField = _secondLayer.GetComponentInChildren<TMP_InputField>(true);
                _scrollRect = _secondLayer.GetComponentInChildren<ScrollRect>(true);
                if (_scrollRect != null)
                {
                    _content = _scrollRect.content;
                    EnsureContentLayout();
                }

                if (_inputField != null)
                {
                    _inputField.onValueChanged.RemoveAllListeners();
                    _inputField.onValueChanged.AddListener(_ => RefreshResultList());
                }
                else
                {
                    Debug.LogWarning("[CheatTool] 未找到二级菜单输入框（InputField (TMP)）。");
                }

                if (_secondLayer.gameObject.activeSelf)
                {
                    _secondLayer.gameObject.SetActive(false);
                }
            }
            else
            {
                Debug.LogWarning("[CheatTool] 未找到「" + SecondLayerName + "」。");
            }

            BindLogRecordLayer();
        }

        private void BindLogRecordLayer()
        {
            if (_logLayer == null)
            {
                Debug.LogWarning("[CheatTool] 未找到「" + LogSecondLayerName + "」。");
                return;
            }

            EnsureLogLayerUiInteractable();
            EnsureSecondLayerPointerBlocker(_logLayer, LogSecondLayerBlockerName);

            var notice = FindChildRecursive(_logLayer, LogNoticeTextName);
            _logNoticeText = notice != null ? notice.GetComponent<TMP_Text>() : null;
            if (_logNoticeText != null)
            {
                _logNoticeDefault = _logNoticeText.text;
            }
            else
            {
                Debug.LogWarning("[CheatTool] 未找到 log 成功提醒文本「" + LogNoticeTextName + "」。");
            }

            var inputTf = FindChildRecursive(_logLayer, LogInputFieldName);
            _logTagInput = inputTf != null
                ? inputTf.GetComponent<TMP_InputField>()
                : _logLayer.GetComponentInChildren<TMP_InputField>(true);
            if (_logTagInput == null)
            {
                Debug.LogWarning("[CheatTool] 未找到 log 面板 Tag 输入框。");
            }

            var saveTf = FindChildRecursive(_logLayer, LogSaveButtonName);
            if (saveTf == null)
            {
                // 兼容场景若已改名为「保存按钮」
                saveTf = FindChildRecursive(_logLayer, "保存按钮");
            }

            _logSaveButton = saveTf != null
                ? saveTf.GetComponent<Button>()
                : _logLayer.GetComponentInChildren<Button>(true);
            if (_logSaveButton != null)
            {
                _logSaveButton.onClick.RemoveAllListeners();
                _logSaveButton.onClick.AddListener(SaveLogSnapshot);
            }
            else
            {
                Debug.LogWarning("[CheatTool] 未找到 log 面板保存按钮（Button / 保存按钮）。");
            }

            if (_logLayer.gameObject.activeSelf)
            {
                _logLayer.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 场景预置结构缺失时（EditMode 空物体），补齐与 MainScene 同名的最小层级，便于契约测试。
        /// </summary>
        private void EnsureMinimalHierarchyIfEmpty()
        {
            if (FindChild(FirstLayerName) != null
                && FindChild(SecondLayerName) != null
                && FindChild(LogSecondLayerName) != null)
            {
                return;
            }

            if (FindChild(FirstLayerName) == null)
            {
                var first = new GameObject(FirstLayerName).transform;
                first.SetParent(transform, false);
                CreateHitButtonStub(CloseButtonName, first);
                CreateHitButtonStub(ClearNodeButtonName, first);
                CreateHitButtonStub(CrossFloorButtonName, first);
                CreateHitButtonStub(AddCardButtonName, first);
                CreateHitButtonStub(AddRelicButtonName, first);
                CreateHitButtonStub(CoinsButtonName, first);
                CreateHitButtonStub(HealButtonName, first);
                CreateHitButtonStub(GodModeButtonName, first);
                CreateHitButtonStub(LogRecordButtonName, first);
            }
            else
            {
                var firstLayer = FindChild(FirstLayerName);
                if (firstLayer != null)
                {
                    if (FindChild(LogRecordButtonName) == null)
                    {
                        CreateHitButtonStub(LogRecordButtonName, firstLayer);
                    }

                    if (FindChild(AddRelicButtonName) == null)
                    {
                        CreateHitButtonStub(AddRelicButtonName, firstLayer);
                    }
                }
            }

            // 兼容旧自举名「作弊选项模板 (3)」：测试/旧场景若仍用旧名，补一个回复满血别名桩
            if (FindChild(HealButtonName) == null && FindChild("作弊选项模板 (3)") != null)
            {
                FindChild("作弊选项模板 (3)").name = HealButtonName;
            }

            if (FindChild(SecondLayerName) == null)
            {
                var secondGo = new GameObject(SecondLayerName, typeof(RectTransform), typeof(Canvas));
                var second = secondGo.GetComponent<RectTransform>();
                second.SetParent(transform, false);
                second.sizeDelta = new Vector2(3f, 3f);

                var canvas = secondGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = Camera.main;
                canvas.sortingOrder = 9999;

                BuildFallbackInputField(second);
                BuildFallbackScrollView(second);
                secondGo.SetActive(false);
            }

            if (FindChild(LogSecondLayerName) == null)
            {
                var logGo = new GameObject(LogSecondLayerName, typeof(RectTransform), typeof(Canvas));
                var logRect = logGo.GetComponent<RectTransform>();
                logRect.SetParent(transform, false);
                logRect.sizeDelta = new Vector2(3f, 3f);

                var logCanvas = logGo.GetComponent<Canvas>();
                logCanvas.renderMode = RenderMode.WorldSpace;
                logCanvas.worldCamera = Camera.main;
                logCanvas.sortingOrder = 9999;

                BuildFallbackLogNotice(logRect);
                BuildFallbackLogInputField(logRect);
                BuildFallbackLogSaveButton(logRect);
                logGo.SetActive(false);
            }
        }

        private void EnsureRootBlocker()
        {
            _rootBlocker = GetComponent<BoxCollider2D>();
            if (_rootBlocker == null)
            {
                _rootBlocker = gameObject.AddComponent<BoxCollider2D>();
            }

            var sprite = GetComponent<SpriteRenderer>();
            if (sprite != null && sprite.sprite != null)
            {
                _rootBlocker.size = sprite.size;
                _rootBlocker.offset = Vector2.zero;
            }
            else if (_rootBlocker.size == Vector2.zero)
            {
                _rootBlocker.size = new Vector2(4.7f, 5f);
            }

            _rootBlocker.isTrigger = false;

            var button = GetComponent<CheatToolPanelButton>();
            if (button == null)
            {
                button = gameObject.AddComponent<CheatToolPanelButton>();
            }

            // 点在面板空白处只吞点击，不关面板（关面板走关闭按钮 / Esc / F12）。
            button.Bind(() => { }, CheatToolPanelButton.HitSort - 1);
        }

        private void EnsureSecondLayerUiInteractable()
        {
            _secondCanvas = _secondLayer.GetComponent<Canvas>();
            if (_secondCanvas == null)
            {
                _secondCanvas = _secondLayer.gameObject.AddComponent<Canvas>();
                _secondCanvas.renderMode = RenderMode.WorldSpace;
            }

            if (_secondCanvas.renderMode == RenderMode.WorldSpace && _secondCanvas.worldCamera == null)
            {
                _secondCanvas.worldCamera = Camera.main;
            }

            if (_secondCanvas.GetComponent<GraphicRaycaster>() == null)
            {
                // 场景预置二级 Canvas 缺 GraphicRaycaster → 鼠标无法点中 InputField / ScrollView。
                _secondCanvas.gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        private void EnsureLogLayerUiInteractable()
        {
            if (_logLayer == null)
            {
                return;
            }

            _logCanvas = _logLayer.GetComponent<Canvas>();
            if (_logCanvas == null)
            {
                _logCanvas = _logLayer.gameObject.AddComponent<Canvas>();
                _logCanvas.renderMode = RenderMode.WorldSpace;
            }

            if (_logCanvas.renderMode == RenderMode.WorldSpace && _logCanvas.worldCamera == null)
            {
                _logCanvas.worldCamera = Camera.main;
            }

            if (_logCanvas.GetComponent<GraphicRaycaster>() == null)
            {
                _logCanvas.gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        private void EnsureSecondLayerPointerBlocker(Transform layer, string blockerName)
        {
            if (layer == null)
            {
                return;
            }

            var existing = FindChildRecursive(layer, blockerName);
            Transform blocker;
            if (existing != null)
            {
                blocker = existing;
            }
            else
            {
                var go = new GameObject(blockerName);
                blocker = go.transform;
                blocker.SetParent(layer, false);
                blocker.SetAsFirstSibling();
                blocker.localPosition = Vector3.zero;
                blocker.localScale = Vector3.one;
            }

            var box = blocker.GetComponent<BoxCollider2D>();
            if (box == null)
            {
                box = blocker.gameObject.AddComponent<BoxCollider2D>();
            }

            var rect = layer as RectTransform;
            if (rect != null)
            {
                box.size = rect.sizeDelta;
            }
            else if (box.size == Vector2.zero)
            {
                box.size = new Vector2(3f, 3f);
            }

            box.isTrigger = false;

            var button = blocker.GetComponent<CheatToolPanelButton>();
            if (button == null)
            {
                button = blocker.gameObject.AddComponent<CheatToolPanelButton>();
            }

            // 只吞世界指针，避免点穿到场地；真正关二级靠 Esc。不要在此 Close，
            // 否则与 InputField 同点会先被 PointerHit 关掉菜单。
            button.Bind(() => { }, SecondLayerBlockerHitSort);
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
            {
                return;
            }

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        private void BindHitButton(string objectName, Action onClick)
        {
            var target = FindButton(objectName);
            if (target == null)
            {
                Debug.LogWarning("[CheatTool] 未找到按钮「" + objectName + "」。");
                return;
            }

            if (target.GetComponent<BoxCollider2D>() == null)
            {
                target.gameObject.AddComponent<BoxCollider2D>().size = new Vector2(2.4f, 0.56f);
            }

            var button = target.GetComponent<CheatToolPanelButton>();
            if (button == null)
            {
                button = target.gameObject.AddComponent<CheatToolPanelButton>();
            }

            button.Bind(onClick);
        }

        private static void CreateHitButtonStub(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(2.4f, 0.56f);
        }

        private void BuildFallbackInputField(Transform parent)
        {
            var inputGo = new GameObject("InputField (TMP)", typeof(RectTransform), typeof(Image));
            var inputRect = inputGo.GetComponent<RectTransform>();
            inputRect.SetParent(parent, false);
            inputRect.sizeDelta = new Vector2(160f, 30f);
            inputRect.anchoredPosition = new Vector2(0f, 1.2f);

            var textArea = new GameObject("Text Area", typeof(RectTransform));
            var areaRect = textArea.GetComponent<RectTransform>();
            areaRect.SetParent(inputRect, false);
            Stretch(areaRect);

            var textGo = new GameObject("Text", typeof(RectTransform));
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.rectTransform.SetParent(areaRect, false);
            Stretch(text.rectTransform);
            text.font = ResolveFont();
            text.fontSize = 16f;
            text.raycastTarget = false;

            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform));
            var placeholder = placeholderGo.AddComponent<TextMeshProUGUI>();
            placeholder.rectTransform.SetParent(areaRect, false);
            Stretch(placeholder.rectTransform);
            placeholder.text = CardSearchPlaceholder;
            placeholder.font = ResolveFont();
            placeholder.fontSize = 16f;
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.color = new Color(0.6f, 0.6f, 0.65f);
            placeholder.raycastTarget = false;

            var input = inputGo.AddComponent<TMP_InputField>();
            input.targetGraphic = inputGo.GetComponent<Image>();
            input.textViewport = areaRect;
            input.textComponent = text;
            input.placeholder = placeholder;
        }

        private void BuildFallbackLogNotice(Transform parent)
        {
            var noticeGo = new GameObject(LogNoticeTextName, typeof(RectTransform));
            var notice = noticeGo.AddComponent<TextMeshProUGUI>();
            notice.rectTransform.SetParent(parent, false);
            notice.rectTransform.sizeDelta = new Vector2(160f, 40f);
            notice.rectTransform.anchoredPosition = new Vector2(0f, -1f);
            notice.font = ResolveFont();
            notice.fontSize = 14f;
            notice.alignment = TextAlignmentOptions.Center;
            notice.text = "提醒log成功保存的消息框";
            notice.raycastTarget = false;
        }

        private void BuildFallbackLogInputField(Transform parent)
        {
            var inputGo = new GameObject(LogInputFieldName, typeof(RectTransform), typeof(Image));
            var inputRect = inputGo.GetComponent<RectTransform>();
            inputRect.SetParent(parent, false);
            inputRect.sizeDelta = new Vector2(160f, 30f);
            inputRect.anchoredPosition = new Vector2(0f, 1.0f);

            var textArea = new GameObject("Text Area", typeof(RectTransform));
            var areaRect = textArea.GetComponent<RectTransform>();
            areaRect.SetParent(inputRect, false);
            Stretch(areaRect);

            var textGo = new GameObject("Text", typeof(RectTransform));
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.rectTransform.SetParent(areaRect, false);
            Stretch(text.rectTransform);
            text.font = ResolveFont();
            text.fontSize = 16f;
            text.raycastTarget = false;

            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform));
            var placeholder = placeholderGo.AddComponent<TextMeshProUGUI>();
            placeholder.rectTransform.SetParent(areaRect, false);
            Stretch(placeholder.rectTransform);
            placeholder.text = "描述问题，例如：顺劈斧攻击异常";
            placeholder.font = ResolveFont();
            placeholder.fontSize = 16f;
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.color = new Color(0.6f, 0.6f, 0.65f);
            placeholder.raycastTarget = false;

            var input = inputGo.AddComponent<TMP_InputField>();
            input.targetGraphic = inputGo.GetComponent<Image>();
            input.textViewport = areaRect;
            input.textComponent = text;
            input.placeholder = placeholder;
        }

        private void BuildFallbackLogSaveButton(Transform parent)
        {
            var buttonGo = new GameObject(LogSaveButtonName, typeof(RectTransform), typeof(Image));
            var buttonRect = buttonGo.GetComponent<RectTransform>();
            buttonRect.SetParent(parent, false);
            buttonRect.sizeDelta = new Vector2(120f, 30f);
            buttonRect.anchoredPosition = new Vector2(0f, 0.2f);

            var labelGo = new GameObject("Text (TMP)", typeof(RectTransform));
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.rectTransform.SetParent(buttonRect, false);
            Stretch(label.rectTransform);
            label.font = ResolveFont();
            label.fontSize = 16f;
            label.alignment = TextAlignmentOptions.Center;
            label.text = "记录log";
            label.raycastTarget = false;

            var button = buttonGo.AddComponent<Button>();
            button.targetGraphic = buttonGo.GetComponent<Image>();
        }

        private void BuildFallbackScrollView(Transform parent)
        {
            var scrollGo = new GameObject("Scroll View", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            var scrollRect = scrollGo.GetComponent<RectTransform>();
            scrollRect.SetParent(parent, false);
            scrollRect.sizeDelta = new Vector2(248f, 248f);
            scrollRect.anchoredPosition = new Vector2(0f, -0.3f);

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            var viewport = viewportGo.GetComponent<RectTransform>();
            viewport.SetParent(scrollRect, false);
            Stretch(viewport);
            scroll.viewport = viewport;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            var content = contentGo.GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, 300f);
            scroll.content = content;
        }

        private void EnsureContentLayout()
        {
            if (_content == null)
            {
                return;
            }

            var group = _content.GetComponent<VerticalLayoutGroup>();
            if (group == null)
            {
                group = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            group.spacing = 4f;
            group.padding = new RectOffset(4, 4, 4, 4);
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;

            var fitter = _content.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = _content.gameObject.AddComponent<ContentSizeFitter>();
            }

            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        private void RefreshResultList()
        {
            ClearOptionRows();
            if (_content == null)
            {
                return;
            }

            var query = _inputField != null ? _inputField.text : string.Empty;
            if (_searchMode == SearchCatalogMode.Relics)
            {
                if (_relicEntries == null)
                {
                    _relicEntries = BuildRelicEntries();
                }

                var matched = CheatToolRelicSearchIndex.Match(_relicEntries, query);
                for (var i = 0; i < matched.Count; i++)
                {
                    CreateRelicOptionRow(matched[i], i);
                }
            }
            else
            {
                if (_cardEntries == null)
                {
                    _cardEntries = BuildCardEntries();
                }

                var matched = CheatToolCardSearchIndex.Match(_cardEntries, query);
                for (var i = 0; i < matched.Count; i++)
                {
                    CreateCardOptionRow(matched[i], i);
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            if (_scrollRect != null)
            {
                _scrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private List<CheatToolCardSearchIndex.CardEntry> BuildCardEntries()
        {
            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            var content = arch?.GetSystem<IContentSystem>();
            if (arch == null || content == null || !content.HasCatalog)
            {
                Debug.LogWarning("[CheatTool] 内容目录未就绪，加卡搜索不可用。");
                return new List<CheatToolCardSearchIndex.CardEntry>();
            }

            return CheatToolCardSearchIndex.Build(content.Catalog, ResolveContentDescription);
        }

        private List<CheatToolRelicSearchIndex.RelicEntry> BuildRelicEntries()
        {
            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            var content = arch?.GetSystem<IContentSystem>();
            if (arch == null || content == null || !content.HasCatalog)
            {
                Debug.LogWarning("[CheatTool] 内容目录未就绪，遗物搜索不可用。");
                return new List<CheatToolRelicSearchIndex.RelicEntry>();
            }

            return CheatToolRelicSearchIndex.Build(content.Catalog, ResolveContentDescription);
        }

        private static string ResolveContentDescription(string defId)
        {
            if (CardPresentationConfigCatalog.TryGet(defId, out var dto) && dto != null)
            {
                return dto.description;
            }

            return null;
        }

        private void ClearOptionRows()
        {
            if (_content == null)
            {
                return;
            }

            for (var i = _content.childCount - 1; i >= 0; i--)
            {
                Destroy(_content.GetChild(i).gameObject);
            }
        }

        private void CreateCardOptionRow(CheatToolCardSearchIndex.CardEntry entry, int index)
        {
            CreateOptionRow(
                index,
                CheatToolCardSearchIndex.BuildOptionLabel(entry),
                () => TryAddCardToDeckTop(entry.DefId));
        }

        private void CreateRelicOptionRow(CheatToolRelicSearchIndex.RelicEntry entry, int index)
        {
            CreateOptionRow(
                index,
                CheatToolRelicSearchIndex.BuildOptionLabel(entry),
                () => TryAddRelicToInventory(entry.DefId));
        }

        private void CreateOptionRow(int index, string labelText, Action onClick)
        {
            var row = new GameObject("option_" + index, typeof(RectTransform));
            row.transform.SetParent(_content, false);
            row.AddComponent<LayoutElement>().preferredHeight = OptionRowHeight;

            var image = row.AddComponent<Image>();
            image.color = new Color(0.08f, 0.08f, 0.1f, 0.92f);
            image.raycastTarget = true;

            var button = row.AddComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }

            var label = new GameObject("label", typeof(RectTransform));
            label.transform.SetParent(row.transform, false);
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 0f);
            labelRect.offsetMax = new Vector2(-8f, 0f);

            var text = label.AddComponent<TextMeshProUGUI>();
            text.text = labelText ?? string.Empty;
            text.font = ResolveOptionFont();
            text.fontSize = 16f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
        }

        /// <summary>
        /// 二级 ScrollView 候选行字体：固定 SmileySans（与 MainScene 中文 UI 一致）。
        /// Editor 直读资产路径；Player 从已加载场景 TMP 引用或 FindObjectsOfTypeAll 解析。
        /// </summary>
        private TMP_FontAsset ResolveOptionFont()
        {
            if (_optionFont != null)
            {
                return _optionFont;
            }

#if UNITY_EDITOR
            _optionFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OptionFontAssetPath);
            if (_optionFont != null)
            {
                return _optionFont;
            }
#endif

            var texts = Resources.FindObjectsOfTypeAll<TMP_Text>();
            for (var i = 0; i < texts.Length; i++)
            {
                var font = texts[i] != null ? texts[i].font : null;
                if (font != null && font.name == OptionFontAssetName)
                {
                    _optionFont = font;
                    return _optionFont;
                }
            }

            var fontAssets = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            for (var i = 0; i < fontAssets.Length; i++)
            {
                var font = fontAssets[i];
                if (font != null && font.name == OptionFontAssetName)
                {
                    _optionFont = font;
                    return _optionFont;
                }
            }

            _optionFont = ResolveFont();
            return _optionFont;
        }

        private TMP_FontAsset ResolveFont()
        {
            if (_font != null)
            {
                return _font;
            }

            if (_inputField != null && _inputField.textComponent != null && _inputField.textComponent.font != null)
            {
                _font = _inputField.textComponent.font;
                return _font;
            }

            var existing = FindFirstObjectByType<TMP_Text>(FindObjectsInactive.Include);
            if (existing != null && existing.font != null)
            {
                _font = existing.font;
                return _font;
            }

            if (TMP_Settings.defaultFontAsset != null)
            {
                _font = TMP_Settings.defaultFontAsset;
                return _font;
            }

            _font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            return _font;
        }

        private Transform FindChild(string objectName)
        {
            return FindChildRecursive(transform, objectName);
        }

        /// <summary>场景按钮名偶带尾部空格，先精确再 trim 兼容。</summary>
        private Transform FindButton(string objectName)
        {
            var target = FindChild(objectName);
            if (target != null)
            {
                return target;
            }

            return FindChild(objectName + " ");
        }

        private static Transform FindChildRecursive(Transform root, string objectName)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == objectName)
                {
                    return child;
                }

                var found = FindChildRecursive(child, objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static Transform FindScenePanelRoot()
        {
            var all = Resources.FindObjectsOfTypeAll<Transform>();
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null || t.name != PanelRootName)
                {
                    continue;
                }

                if (!IsSceneObject(t.gameObject))
                {
                    continue;
                }

                return t;
            }

            return null;
        }

        private static bool IsSceneObject(GameObject go)
        {
            if (go == null || !go.scene.IsValid())
            {
                return false;
            }

            return (go.hideFlags & HideFlags.HideInHierarchy) == 0;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}

#endif
