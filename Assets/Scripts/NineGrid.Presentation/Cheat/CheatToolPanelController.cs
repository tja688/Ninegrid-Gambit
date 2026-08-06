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
        private const string CloseButtonName = "关闭按钮";
        private const string ClearNodeButtonName = "一键清关选项";
        private const string AddCardButtonName = "战斗加卡选项";
        private const string CoinsButtonName = "无限金币选项";
        private const string HealButtonName = "作弊选项模板 (3)";
        private const string SecondLayerBlockerName = "第二层命中遮罩";
        private const string OptionFontAssetPath = "Assets/Arts/Fronts/DeYiHei/SmileySans-Oblique-3 SDF.asset";
        private const string OptionFontAssetName = "SmileySans-Oblique-3 SDF";
        private const int CoinsPerClick = 999;
        private const float OptionRowHeight = 36f;
        private const int SecondLayerBlockerHitSort = CheatToolPanelButton.HitSort + 1;

        private static CheatToolPanelController sInstance;

        private bool _bound;
        private Transform _firstLayer;
        private Transform _secondLayer;
        private BoxCollider2D _rootBlocker;

        private Canvas _secondCanvas;
        private TMP_InputField _inputField;
        private ScrollRect _scrollRect;
        private RectTransform _content;
        private TMP_FontAsset _font;
        private TMP_FontAsset _optionFont;

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
            // 对齐 QuickTest「-」(KeypadMinus) → CheatForceNodeVictoryCommand → TryForceNodeVictory。
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

            if (_firstLayer != null)
            {
                // 打开二级时关掉一级，避免 BoxCollider2D 与 WorldSpace UI 抢点。
                _firstLayer.gameObject.SetActive(false);
            }

            _secondLayer.gameObject.SetActive(true);
            _entries = null;

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

        public void CloseAddCardMenu()
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

            if (gameObject.activeSelf && _firstLayer != null)
            {
                _firstLayer.gameObject.SetActive(true);
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
            if (_firstLayer == null)
            {
                Debug.LogWarning("[CheatTool] 未找到「" + FirstLayerName + "」。");
            }

            BindHitButton(CloseButtonName, ClosePanel);
            BindHitButton(ClearNodeButtonName, ForceClearNode);
            BindHitButton(AddCardButtonName, OpenAddCardMenu);
            BindHitButton(CoinsButtonName, AddCoins);
            BindHitButton(HealButtonName, HealAvatarFull);

            if (_secondLayer == null)
            {
                Debug.LogWarning("[CheatTool] 未找到「" + SecondLayerName + "」。");
                return;
            }

            EnsureSecondLayerUiInteractable();
            EnsureSecondLayerPointerBlocker();

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

        /// <summary>
        /// 场景预置结构缺失时（EditMode 空物体），补齐与 MainScene 同名的最小层级，便于契约测试。
        /// </summary>
        private void EnsureMinimalHierarchyIfEmpty()
        {
            if (FindChild(FirstLayerName) != null && FindChild(SecondLayerName) != null)
            {
                return;
            }

            if (FindChild(FirstLayerName) == null)
            {
                var first = new GameObject(FirstLayerName).transform;
                first.SetParent(transform, false);
                CreateHitButtonStub(CloseButtonName, first);
                CreateHitButtonStub(ClearNodeButtonName, first);
                CreateHitButtonStub(AddCardButtonName, first);
                CreateHitButtonStub(CoinsButtonName, first);
                CreateHitButtonStub(HealButtonName, first);
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

        private void EnsureSecondLayerPointerBlocker()
        {
            var existing = FindChildRecursive(_secondLayer, SecondLayerBlockerName);
            Transform blocker;
            if (existing != null)
            {
                blocker = existing;
            }
            else
            {
                var go = new GameObject(SecondLayerBlockerName);
                blocker = go.transform;
                blocker.SetParent(_secondLayer, false);
                blocker.SetAsFirstSibling();
                blocker.localPosition = Vector3.zero;
                blocker.localScale = Vector3.one;
            }

            var box = blocker.GetComponent<BoxCollider2D>();
            if (box == null)
            {
                box = blocker.gameObject.AddComponent<BoxCollider2D>();
            }

            var rect = _secondLayer as RectTransform;
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
            var target = FindChild(objectName);
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
            placeholder.text = "输入卡名 / 卡组 / 技能…";
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
