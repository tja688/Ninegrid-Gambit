#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System;
using System.Collections.Generic;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Flow;
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
    /// 作弊工具面板控制器（F12 综合测试后门，运行时全量自举，无需场景预置）。
    /// 职责：
    /// 1) 一键清关（对齐 QuickTest「-」的 CheatForceNodeVictory 内部实现）；
    /// 2) 战斗加卡（二级菜单搜索 + 卡组顶插入，仅战斗中可用，仅真实落地三类卡）；
    /// 3) 无限金币（+999）；4) 回复满血。
    /// 面板本身（Canvas / EventSystem / 一级菜单 / 二级菜单输入框与滚动列表）全部在首次打开时
    /// 运行时创建；若场景已提供同名结构（作弊工具BG 等），则复用并只做接线。
    /// 仅 UNITY_EDITOR / DEVELOPMENT_BUILD 编译，正式包不含。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CheatToolPanelController : MonoBehaviour
    {
        private const string FirstLayerName = "第一层主面板";
        private const string SecondLayerName = "第二层_添加卡菜单";
        private const string BlockerName = "背景遮罩";
        private const string CloseButtonName = "关闭按钮";
        private const string ClearNodeButtonName = "一键清关选项";
        private const string AddCardButtonName = "战斗加卡选项";
        private const string CoinsButtonName = "无限金币选项";
        private const string HealButtonName = "作弊选项模板 (3)";
        private const int CoinsPerClick = 999;
        private const float OptionRowHeight = 36f;
        private const int RootSortOrder = 1000;
        private const int SecondLayerSortOrder = 10;

        private static CheatToolPanelController sInstance;

        private bool _bound;
        private Transform _firstLayer;
        private Transform _secondLayer;

        private Canvas _secondCanvas;
        private TMP_InputField _inputField;
        private ScrollRect _scrollRect;
        private RectTransform _content;
        private TMP_FontAsset _font;

        private List<CheatToolCardSearchIndex.CardEntry> _entries;

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

            if (_secondLayer != null && _secondLayer.gameObject.activeSelf)
            {
                CloseAddCardMenu();
            }
            else
            {
                ClosePanel();
            }
        }

        /// <summary>
        /// F12 宿主入口：面板未初始化（inactive）时也能找到实例；场景完全没有时运行时创建。
        /// </summary>
        public static void TryToggle()
        {
            if (sInstance != null)
            {
                sInstance.Toggle();
                return;
            }

            var all = Resources.FindObjectsOfTypeAll<CheatToolPanelController>();
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i] != null)
                {
                    all[i].Toggle();
                    return;
                }
            }

            var go = new GameObject("作弊工具BG");
            var controller = go.AddComponent<CheatToolPanelController>();
            go.SetActive(false);
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(go);
            }

            // 不依赖 sInstance：EditMode 下 AddComponent 不会触发 Awake。
            controller.Toggle();
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

            CloseAddCardMenu();
            Debug.Log("[CheatTool] 作弊面板已打开（F12 切换 / Esc 关闭）。");
        }

        public void ClosePanel()
        {
            CloseAddCardMenu();
            gameObject.SetActive(false);
        }

        // ============ 一级菜单功能 ============

        private void ForceClearNode()
        {
            if (!BattleSessionCheat.TryForceNodeVictory())
            {
                Debug.LogWarning("[CheatTool] 一键清关失败（仅战斗/结算阶段可用）。");
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

        // ============ 二级菜单：战斗加卡 ============

        public void OpenAddCardMenu()
        {
            EnsureBindings();
            if (_secondLayer == null)
            {
                Debug.LogWarning("[CheatTool] 未找到「第二层_添加卡菜单」。");
                return;
            }

            _secondLayer.gameObject.SetActive(true);
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
        }

        public void CloseAddCardMenu()
        {
            if (_secondLayer != null && _secondLayer.gameObject.activeSelf)
            {
                _secondLayer.gameObject.SetActive(false);
            }

            if (_inputField != null)
            {
                _inputField.text = string.Empty;
            }

            ClearOptionRows();
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
        }

        // ============ 自举 ============

        private void EnsureBindings()
        {
            if (_bound)
            {
                return;
            }

            _bound = true;
            EnsurePanelHierarchy();

            _firstLayer = FindChild(FirstLayerName);
            _secondLayer = FindChild(SecondLayerName);
            if (_firstLayer == null)
            {
                Debug.LogWarning("[CheatTool] 未找到「" + FirstLayerName + "」。");
            }

            BindButton(CloseButtonName, "关闭", ClosePanel);
            BindButton(ClearNodeButtonName, "一键清关（跳过战斗）", ForceClearNode);
            BindButton(AddCardButtonName, "战斗加卡…", OpenAddCardMenu);
            BindButton(CoinsButtonName, "无限金币 +999", AddCoins);
            BindButton(HealButtonName, "回复满血", HealAvatarFull);

            if (_secondLayer == null)
            {
                Debug.LogWarning("[CheatTool] 未找到「" + SecondLayerName + "」。");
                return;
            }

            _secondCanvas = _secondLayer.GetComponent<Canvas>();
            if (_secondCanvas != null && _secondCanvas.GetComponent<GraphicRaycaster>() == null)
            {
                // 场景 Canvas 缺 GraphicRaycaster，鼠标无法选中输入框 / ScrollView —— 运行时补齐。
                _secondCanvas.gameObject.AddComponent<GraphicRaycaster>();
            }

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
        }

        /// <summary>
        /// 保证面板结构完整：根 Canvas / EventSystem / 遮罩 / 一层 / 二层 缺则运行时创建。
        /// 场景已提供同名物体时复用。
        /// </summary>
        private void EnsurePanelHierarchy()
        {
            EnsureRootCanvas();
            EnsureEventSystem();
            if (FindChild(FirstLayerName) == null)
            {
                BuildFirstLayer();
            }

            if (FindChild(SecondLayerName) == null)
            {
                BuildSecondLayer();
            }
        }

        private void EnsureRootCanvas()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = RootSortOrder;

            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            if (FindChild(BlockerName) == null)
            {
                var blocker = CreateRect(BlockerName, transform);
                Stretch(blocker);
                var image = blocker.gameObject.AddComponent<Image>();
                image.color = new Color(0f, 0f, 0f, 0.55f);
                image.raycastTarget = true;
            }
        }

        private void EnsureEventSystem()
        {
            // EventSystem.current 在 EditMode 下不保证被赋值，按场景搜索判存在。
            if (FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
            {
                return;
            }

            // New Input System only：EventSystem 须配 InputSystemUIInputModule 才能收 uGUI 点击。
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        private void BuildFirstLayer()
        {
            var layer = CreateRect(FirstLayerName, transform);
            AnchorCenter(layer, new Vector2(430f, 420f));
            var image = layer.gameObject.AddComponent<Image>();
            image.color = new Color(0.10f, 0.10f, 0.13f, 0.96f);
            image.raycastTarget = true;

            var group = layer.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = 8f;
            group.padding = new RectOffset(14, 14, 14, 14);
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;

            var fitter = layer.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var title = CreateLabel("标题", "作弊工具  （F12 开关 / Esc 关闭）", layer);
            title.fontSize = 20f;
            title.color = new Color(0.95f, 0.85f, 0.45f);
            title.alignment = TextAlignmentOptions.Center;
            title.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 34f);

            CreateButtonObject(CloseButtonName, "关闭", layer);
            CreateButtonObject(ClearNodeButtonName, "一键清关（跳过战斗）", layer);
            CreateButtonObject(AddCardButtonName, "战斗加卡…", layer);
            CreateButtonObject(CoinsButtonName, "无限金币 +999", layer);
            CreateButtonObject(HealButtonName, "回复满血", layer);
        }

        private void BuildSecondLayer()
        {
            var layer = CreateRect(SecondLayerName, transform);
            var canvas = layer.gameObject.AddComponent<Canvas>();
            canvas.sortingOrder = SecondLayerSortOrder;
            layer.gameObject.AddComponent<GraphicRaycaster>();

            var dim = CreateRect("第二层遮罩", layer);
            Stretch(dim);
            var dimImage = dim.gameObject.AddComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 0.6f);
            dimImage.raycastTarget = true;

            var window = CreateRect("窗口", layer);
            AnchorCenter(window, new Vector2(600f, 720f));
            var windowImage = window.gameObject.AddComponent<Image>();
            windowImage.color = new Color(0.09f, 0.09f, 0.11f, 0.97f);
            windowImage.raycastTarget = true;

            var title = CreateLabel("标题", "战斗加卡（搜索卡名 / 卡组 / 技能）", window);
            StretchTop(title.rectTransform, 40f, 16f);
            title.fontSize = 18f;
            title.color = new Color(0.95f, 0.85f, 0.45f);
            title.alignment = TextAlignmentOptions.MidlineLeft;

            BuildInputField(window);

            var scrollGo = CreateRect("Scroll View", window);
            StretchBottom(scrollGo, 16f, 104f);
            var scroll = scrollGo.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            var viewport = CreateRect("Viewport", scrollGo);
            Stretch(viewport);
            viewport.gameObject.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.08f, 1f);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;

            var content = CreateRect("Content", viewport);
            Stretch(content);
            scroll.content = content;
        }

        private void BuildInputField(Transform window)
        {
            var inputGo = CreateRect("InputField (TMP)", window);
            StretchTop(inputGo, 48f, 52f);
            var background = inputGo.gameObject.AddComponent<Image>();
            background.color = new Color(0.14f, 0.14f, 0.17f, 0.98f);
            background.raycastTarget = true;

            var textArea = CreateRect("Text Area", inputGo);
            StretchInset(textArea, 8f);
            textArea.gameObject.AddComponent<RectMask2D>();

            var placeholder = CreateLabel("Placeholder", "输入卡名 / 卡组 / 技能…", textArea);
            Stretch(placeholder.rectTransform);
            placeholder.fontSize = 16f;
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.color = new Color(0.6f, 0.6f, 0.65f, 1f);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;

            var text = CreateLabel("Text", string.Empty, textArea);
            Stretch(text.rectTransform);
            text.fontSize = 16f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;

            var input = inputGo.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = background;
            input.textViewport = textArea;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.selectionColor = new Color(0.3f, 0.6f, 1f, 0.5f);
            input.characterLimit = 64;
        }

        private void BindButton(string objectName, string label, Action onClick)
        {
            var target = FindChild(objectName);
            if (target == null)
            {
                target = CreateButtonObject(
                    objectName,
                    label,
                    _firstLayer != null ? _firstLayer : transform);
            }

            var button = target.GetComponent<Button>();
            if (button == null)
            {
                var image = target.GetComponent<Image>();
                if (image == null)
                {
                    image = target.gameObject.AddComponent<Image>();
                    image.color = new Color(0.22f, 0.22f, 0.26f, 1f);
                }

                button = target.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick());
        }

        private Transform CreateButtonObject(string name, string label, Transform parent)
        {
            var row = CreateRect(name, parent);
            var image = row.gameObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.22f, 0.26f, 1f);
            image.raycastTarget = true;
            row.gameObject.AddComponent<Button>().targetGraphic = image;

            var text = CreateLabel("标签", label, row);
            StretchInset(text.rectTransform, 12f);
            text.fontSize = 18f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;

            var element = row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 52f;
            return row;
        }

        private TMP_Text CreateLabel(string name, string content, Transform parent)
        {
            var go = CreateRect(name, parent);
            var text = go.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = content ?? string.Empty;
            text.font = ResolveFont();
            text.fontSize = 16f;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void StretchInset(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static void StretchTop(RectTransform rect, float height, float topInset)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(16f, -height - topInset);
            rect.offsetMax = new Vector2(-16f, -topInset);
        }

        private static void StretchBottom(RectTransform rect, float inset, float topGap)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(16f, 16f + inset);
            rect.offsetMax = new Vector2(-16f, -topGap);
        }

        private static void AnchorCenter(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
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

            if (_entries == null)
            {
                _entries = BuildEntries();
            }

            var query = _inputField != null ? _inputField.text : string.Empty;
            var matched = CheatToolCardSearchIndex.Match(_entries, query);
            for (var i = 0; i < matched.Count; i++)
            {
                CreateOptionRow(matched[i], i);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            if (_scrollRect != null)
            {
                _scrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private List<CheatToolCardSearchIndex.CardEntry> BuildEntries()
        {
            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            var content = arch?.GetSystem<IContentSystem>();
            if (arch == null || content == null || !content.HasCatalog)
            {
                Debug.LogWarning("[CheatTool] 内容目录未就绪，加卡搜索不可用。");
                return new List<CheatToolCardSearchIndex.CardEntry>();
            }

            return CheatToolCardSearchIndex.Build(content.Catalog, ResolveCardDescription);
        }

        private static string ResolveCardDescription(string defId)
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

        private void CreateOptionRow(CheatToolCardSearchIndex.CardEntry entry, int index)
        {
            var row = new GameObject("option_" + index, typeof(RectTransform));
            row.transform.SetParent(_content, false);
            row.AddComponent<LayoutElement>().preferredHeight = OptionRowHeight;

            var image = row.AddComponent<Image>();
            image.color = new Color(0.08f, 0.08f, 0.1f, 0.92f);
            image.raycastTarget = true;

            var button = row.AddComponent<Button>();
            button.targetGraphic = image;
            var defId = entry.DefId;
            button.onClick.AddListener(() => TryAddCardToDeckTop(defId));

            var label = new GameObject("label", typeof(RectTransform));
            label.transform.SetParent(row.transform, false);
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 0f);
            labelRect.offsetMax = new Vector2(-8f, 0f);

            var text = label.AddComponent<TextMeshProUGUI>();
            text.text = CheatToolCardSearchIndex.BuildOptionLabel(entry);
            text.font = ResolveFont();
            text.fontSize = 16f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
        }

        /// <summary>
        /// 字体解析链：场景中已有 TMP 文本（通常带中文字体）→ TMP 全局默认 → Resources 兜底。
        /// 面板是运行时创建，先于任何自有文本，故从场景全局找字体而非自身子级。
        /// </summary>
        private TMP_FontAsset ResolveFont()
        {
            if (_font != null)
            {
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
    }
}

#endif
