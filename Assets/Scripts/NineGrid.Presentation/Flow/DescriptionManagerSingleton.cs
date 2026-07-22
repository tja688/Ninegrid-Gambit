using NineGrid.Cards;
using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Flow.Presentation;
using QFramework;
using TMPro;
using UnityEngine;
using NineGrid.Presentation;

namespace NineGrid.Flow
{
    /// <summary>
    /// 局内描述管理单例：合法 hover/drag 对象时，把 ContentVisual 描述写入 Card Info Text；
    /// 主流程选择悬停可写入 Notice Text。
    /// <para>
    /// 遗留 UI：Card Info Text 不再作为卡面基础描述权威（权威在卡面 <c>Basic_Description</c> 槽 +
    /// 投影 Commit）。本类可残存供选择/拖拽提示，但不得与卡面双写真相源。
    /// </para>
    /// </summary>
    public sealed class DescriptionManagerSingleton : MonoBehaviour
    {
        public const int MaxDescriptionChars = 72;
        private const string DefaultInfoRootName = "InGameInfo Text";
        private const string DefaultCardInfoTextName = "Card Info Text";
        private const string DefaultNoticeTextName = "Notice Text";


        [Tooltip("局内描述 TMP；留空则运行时在 InGameInfoText 下按名查找 Card Info Text。")]
        [SerializeField] private TextMeshProUGUI cardInfoText;

        [Tooltip("主流程选择悬停 / 公告 TMP；留空则运行时查找 TableNine Text Overlay UI/Notice Text。")]
        [SerializeField] private TextMeshProUGUI noticeText;

        private int _generation;
        private DescriptionShowRoute _activeRoute = DescriptionShowRoute.Hover;
        private string _activeDefId = string.Empty;
        private string _boardSelectPrompt = string.Empty;
        private int _noticeGeneration;
        private string _activeNoticeDefId = string.Empty;
        private ContentVisualCatalog _visualCatalog;
        private CardFrameStyleCatalog _frameStyleCatalog;
        private bool _visualsResolved;
        private IUnRegister _showEventUnRegister;
        private IUnRegister _showTextEventUnRegister;
        private IUnRegister _clearEventUnRegister;

        private void Awake()
        {
            EnsureBindings();
            RegisterDescriptionEvents();
            Clear();
        }

        private void OnDestroy()
        {
            UnregisterDescriptionEvents();
        }

        /// <summary>
        /// 显示 defId 对应描述；返回 generation，exit 时带同值 Clear 可防竞态。
        /// Drag 路由目前默认走与 Hover 相同的 ContentVisual 描述，后续可在此分支专属文案。
        /// Drag 优先于 Hover：拖拽中忽略 Hover 的 Show，避免被其它槽位悬停盖掉。
        /// </summary>
        public int Show(string defId, DescriptionShowRoute route = DescriptionShowRoute.Hover)
        {
            EnsureBindings();

            if (cardInfoText == null)
            {
                _generation++;
                return _generation;
            }

            // 拖拽 / 悬停占用时 BoardSelect 不得抢占
            if (route == DescriptionShowRoute.BoardSelect
                && (_activeRoute == DescriptionShowRoute.Drag || _activeRoute == DescriptionShowRoute.Hover))
            {
                return _generation;
            }

            // 拖拽描述占用中时，Hover 不得抢占；返回 -1 避免调用方 Clear(token) 误清 Drag
            if (route == DescriptionShowRoute.Hover
                && !string.IsNullOrEmpty(_activeDefId)
                && _activeRoute == DescriptionShowRoute.Drag)
            {
                return -1;
            }

            // 悬停占用时 BoardSelect 不得抢占（BoardSelect 仅作无 hover 回退）
            if (route == DescriptionShowRoute.BoardSelect
                && _activeRoute == DescriptionShowRoute.Hover
                && !string.IsNullOrEmpty(_activeDefId))
            {
                return _generation;
            }

            if (string.IsNullOrEmpty(defId))
            {
                return ClearActiveAndBump();
            }

            // 同 defId + 同路由已在展示时不 bump generation，避免每帧重申把外部 Clear(token) 弄失效
            if (_activeDefId == defId
                && _activeRoute == route
                && !string.IsNullOrEmpty(cardInfoText.text))
            {
                return _generation;
            }

            if (!TryResolveDescription(defId, route, out var description, out var isCardFaceBasicDescription))
            {
                return ClearActiveAndBump();
            }

            // #16：卡面基础描述权威在 Basic_Description Commit；Hover/Drag 不再把同源 ContentVisual
            // 文案写入 Card Info Text，避免与卡面双写真相。选择项 / BoardSelect / Notice 仍可写。
            if (isCardFaceBasicDescription
                && (route == DescriptionShowRoute.Hover || route == DescriptionShowRoute.Drag))
            {
                return ClearActiveAndBump();
            }

            _generation++;
            _activeDefId = defId;
            _activeRoute = route;
            cardInfoText.text = ClampDescription(description, MaxDescriptionChars);
            return _generation;
        }

        /// <summary>兼容旧调用：按 defId 走 Hover 路由。</summary>
        public int Show(string defId) => Show(defId, DescriptionShowRoute.Hover);

        /// <summary>展示原始文案（多选模式专属提示等）。</summary>
        public int ShowText(string text, DescriptionShowRoute route = DescriptionShowRoute.BoardSelect)
        {
            EnsureBindings();

            if (route == DescriptionShowRoute.BoardSelect)
            {
                _boardSelectPrompt = text ?? string.Empty;
            }

            if (cardInfoText == null)
            {
                _generation++;
                return _generation;
            }

            if (route == DescriptionShowRoute.BoardSelect)
            {
                if (_activeRoute == DescriptionShowRoute.Drag
                    || (_activeRoute == DescriptionShowRoute.Hover && !string.IsNullOrEmpty(_activeDefId)))
                {
                    return _generation;
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    return ClearActiveAndBump();
                }

                _generation++;
                _activeDefId = string.Empty;
                _activeRoute = route;
                cardInfoText.text = ClampDescription(text, MaxDescriptionChars);
                return _generation;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return ClearActiveAndBump();
            }

            _generation++;
            _activeDefId = string.Empty;
            _activeRoute = route;
            cardInfoText.text = ClampDescription(text, MaxDescriptionChars);
            return _generation;
        }

        /// <summary>
        /// 主流程选择悬停：写入 Notice Text（与 Card Info Text 独立 generation）。
        /// </summary>
        public int ShowOnNotice(string defId)
        {
            EnsureBindings();
            if (noticeText == null)
            {
                _noticeGeneration++;
                return _noticeGeneration;
            }

            if (string.IsNullOrEmpty(defId))
            {
                return ClearNoticeActiveAndBump();
            }

            if (_activeNoticeDefId == defId && !string.IsNullOrEmpty(noticeText.text))
            {
                return _noticeGeneration;
            }

            if (!TryResolveDescription(
                    defId,
                    DescriptionShowRoute.Hover,
                    out var description,
                    out _))
            {
                return ClearNoticeActiveAndBump();
            }

            _noticeGeneration++;
            _activeNoticeDefId = defId;
            noticeText.text = ClampDescription(description, MaxDescriptionChars);
            if (!noticeText.gameObject.activeSelf)
            {
                noticeText.gameObject.SetActive(true);
            }

            return _noticeGeneration;
        }

        public void ClearNotice(int generation)
        {
            if (generation != _noticeGeneration)
            {
                return;
            }

            EnsureBindings();
            _activeNoticeDefId = string.Empty;
            if (noticeText != null)
            {
                noticeText.text = string.Empty;
            }
        }

        public void Clear()
        {
            Clear(_generation);
            ClearNotice(_noticeGeneration);
        }

        public void Clear(int generation)
        {
            if (generation != _generation)
            {
                return;
            }

            EnsureBindings();
            _activeDefId = string.Empty;
            _activeRoute = DescriptionShowRoute.Hover;
            if (cardInfoText != null)
            {
                cardInfoText.text = string.Empty;
            }
        }

        /// <summary>仅当当前展示路由匹配时清空，避免 hover/drag 互相踩。</summary>
        public void ClearRoute(DescriptionShowRoute route)
        {
            if (_activeRoute != route)
            {
                return;
            }

            if (route == DescriptionShowRoute.BoardSelect)
            {
                _boardSelectPrompt = string.Empty;
            }

            Clear(_generation);
            TryRestoreBoardSelectPrompt();
        }

        private void TryRestoreBoardSelectPrompt()
        {
            if (string.IsNullOrWhiteSpace(_boardSelectPrompt)
                || !PresentationInputGates.BoardSelectModeActive
                || cardInfoText == null)
            {
                return;
            }

            if (_activeRoute == DescriptionShowRoute.Drag
                || (_activeRoute == DescriptionShowRoute.Hover && !string.IsNullOrEmpty(_activeDefId)))
            {
                return;
            }

            _generation++;
            _activeDefId = string.Empty;
            _activeRoute = DescriptionShowRoute.BoardSelect;
            cardInfoText.text = ClampDescription(_boardSelectPrompt, MaxDescriptionChars);
        }

        private int ClearActiveAndBump()
        {
            _generation++;
            _activeDefId = string.Empty;
            if (cardInfoText != null)
            {
                cardInfoText.text = string.Empty;
            }

            return _generation;
        }

        private int ClearNoticeActiveAndBump()
        {
            _noticeGeneration++;
            _activeNoticeDefId = string.Empty;
            if (noticeText != null)
            {
                noticeText.text = string.Empty;
            }

            return _noticeGeneration;
        }

        private void RegisterDescriptionEvents()
        {
            UnregisterDescriptionEvents();
            var arch = NineGridArchitecture.Interface;
            if (arch == null)
            {
                return;
            }

            _showEventUnRegister = arch.RegisterEvent<DescriptionShowRequested>(OnDescriptionShowRequested);
            _showTextEventUnRegister = arch.RegisterEvent<DescriptionShowTextRequested>(
                OnDescriptionShowTextRequested);
            _clearEventUnRegister = arch.RegisterEvent<DescriptionClearRequested>(
                OnDescriptionClearRequested);
        }

        private void UnregisterDescriptionEvents()
        {
            _showEventUnRegister?.UnRegister();
            _showTextEventUnRegister?.UnRegister();
            _clearEventUnRegister?.UnRegister();
            _showEventUnRegister = null;
            _showTextEventUnRegister = null;
            _clearEventUnRegister = null;
        }

        private void OnDescriptionShowRequested(DescriptionShowRequested e) => Show(e.DefId, e.Route);

        private void OnDescriptionShowTextRequested(DescriptionShowTextRequested e) =>
            ShowText(e.Text, e.Route);

        private void OnDescriptionClearRequested(DescriptionClearRequested e) => ClearRoute(e.Route);

        private void EnsureBindings()
        {
            if (cardInfoText == null)
            {
                var root = GameObject.Find(DefaultInfoRootName);
                if (root == null)
                {
                    var overlay = GameObject.Find("TableNine Text Overlay UI");
                    if (overlay != null)
                    {
                        var t = overlay.transform.Find(DefaultInfoRootName);
                        if (t != null)
                        {
                            root = t.gameObject;
                        }
                    }
                }

                if (root != null)
                {
                    var child = root.transform.Find(DefaultCardInfoTextName);
                    if (child != null)
                    {
                        cardInfoText = child.GetComponent<TextMeshProUGUI>();
                    }

                    if (cardInfoText == null)
                    {
                        cardInfoText = root.GetComponentInChildren<TextMeshProUGUI>(true);
                        if (cardInfoText != null && cardInfoText.gameObject.name != DefaultCardInfoTextName)
                        {
                            // 避免误绑到 PlayerInfoText 子节点；仅接受具名 Card Info Text
                            cardInfoText = null;
                        }
                    }
                }
            }

            if (noticeText == null)
            {
                var noticeGo = GameObject.Find(DefaultNoticeTextName);
                if (noticeGo == null)
                {
                    var overlay = GameObject.Find("TableNine Text Overlay UI");
                    if (overlay != null)
                    {
                        var t = overlay.transform.Find(DefaultNoticeTextName);
                        if (t != null)
                        {
                            noticeGo = t.gameObject;
                        }
                    }
                }

                if (noticeGo == null)
                {
                    var all = Resources.FindObjectsOfTypeAll<Transform>();
                    for (var i = 0; i < all.Length; i++)
                    {
                        var tr = all[i];
                        if (tr != null
                            && tr.name == DefaultNoticeTextName
                            && tr.gameObject.scene.IsValid())
                        {
                            noticeGo = tr.gameObject;
                            break;
                        }
                    }
                }

                if (noticeGo != null)
                {
                    noticeText = noticeGo.GetComponent<TextMeshProUGUI>();
                }
            }
        }

        private bool TryResolveDescription(
            string defId,
            DescriptionShowRoute route,
            out string description,
            out bool isCardFaceBasicDescription)
        {
            description = string.Empty;
            isCardFaceBasicDescription = false;

            // Drag 专属描述路由预留：当前与 Hover 相同，后续可在此分支替换文案来源。
            _ = route;

            if (TryResolveChoiceOptionDescription(defId, out description))
            {
                return true;
            }

            EnsureVisualsLoaded();
            CoreCardPresentationMapper.EnsureContentCatalogLoaded();

            var arch = NineGridArchitecture.Current;
            if (arch == null || _visualCatalog == null)
            {
                return false;
            }

            var content = arch.GetSystem<IContentSystem>();
            if (!content.HasCatalog)
            {
                return false;
            }

            if (!ContentVisualResolver.TryResolve(
                    defId,
                    content.Catalog,
                    _visualCatalog,
                    _frameStyleCatalog,
                    spriteProvider: null,
                    out var resolved))
            {
                return false;
            }

            description = resolved.Description ?? string.Empty;
            // Avatar 悬停追加血量/遗物是 HUD 提示，不是卡面基础描述权威；其余 ContentVisual 描述归卡面。
            var avatarEnriched = TryAppendAvatarRuntimeDescription(defId, content.Catalog, ref description);
            if (string.IsNullOrWhiteSpace(description))
            {
                return false;
            }

            isCardFaceBasicDescription = !avatarEnriched;
            return true;
        }

        /// <summary>
        /// 场地玩家卡悬停：在 ContentVisual 描述后追加当前血量上限与持有技能名。
        /// </summary>
        private static bool TryAppendAvatarRuntimeDescription(
            string defId,
            GameContentCatalog catalog,
            ref string description)
        {
            var arch = NineGridArchitecture.Current;
            if (arch == null)
            {
                return false;
            }

            var board = arch.GetModel<BoardModel>();
            var avatarUid = board.AvatarUid != null ? board.AvatarUid.Value : 0;
            if (avatarUid <= 0 || !arch.GetModel<CardRegistry>().TryGet(avatarUid, out var avatar))
            {
                return false;
            }

            if (!string.Equals(avatar.DefId, defId, System.StringComparison.Ordinal))
            {
                return false;
            }

            var stats = arch.GetSystem<IStatSystem>();
            var maxHp = Mathf.Max(0, stats.GetEffectiveInt(avatar, StatId.MaxHp));
            var player = arch.GetModel<PlayerModel>();
            var relicNames = ResolveRelicDisplayNames(player.RelicDefIds, catalog);

            var builder = new System.Text.StringBuilder();
            if (!string.IsNullOrWhiteSpace(description))
            {
                builder.Append(description.Trim());
            }

            if (maxHp > 0)
            {
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append("血量上限").Append(maxHp);
            }

            if (relicNames.Count > 0)
            {
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append("遗物:");
                for (var i = 0; i < relicNames.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append('、');
                    }

                    builder.Append(relicNames[i]);
                }
            }

            if (builder.Length == 0)
            {
                return false;
            }

            description = builder.ToString();
            return true;
        }

        private static System.Collections.Generic.List<string> ResolveRelicDisplayNames(
            System.Collections.Generic.IReadOnlyList<string> relicDefIds,
            GameContentCatalog catalog)
        {
            var names = new System.Collections.Generic.List<string>();
            if (relicDefIds == null || catalog == null)
            {
                return names;
            }

            for (var i = 0; i < relicDefIds.Count; i++)
            {
                var relicId = relicDefIds[i];
                if (string.IsNullOrEmpty(relicId))
                {
                    continue;
                }

                if (catalog.Relics.TryGetValue(relicId, out var relic)
                    && !string.IsNullOrWhiteSpace(relic.DisplayName))
                {
                    names.Add(relic.DisplayName);
                }
                else
                {
                    names.Add(relicId);
                }
            }

            return names;
        }

        /// <summary>
        /// 属性提升 / 房间 RoomKind 等非 ContentVisual 选项的描述回退。
        /// </summary>
        private static bool TryResolveChoiceOptionDescription(string defId, out string description)
        {
            description = string.Empty;
            if (string.IsNullOrWhiteSpace(defId))
            {
                return false;
            }

            var key = defId.Trim();
            switch (key)
            {
                case "Attack":
                    description = "攻击+1";
                    return true;
                case "Armor":
                    description = "护甲+1";
                    return true;
                case "Hp":
                    description = "血量上限与当前血量+2";
                    return true;
                case "room_left":
                    description = "左侧房间";
                    return true;
                case "room_right":
                    description = "右侧房间";
                    return true;
            }

            if (TryResolveRoomKindDescription(key, out description))
            {
                return true;
            }

            return false;
        }

        private static bool TryResolveRoomKindDescription(string key, out string description)
        {
            description = string.Empty;
            if (!System.Enum.TryParse(key, ignoreCase: true, out RoomKind roomKind)
                || roomKind == RoomKind.None)
            {
                return false;
            }

            CoreCardPresentationMapper.EnsureContentCatalogLoaded();
            var arch = NineGridArchitecture.Current;
            if (arch != null)
            {
                var content = arch.GetSystem<IContentSystem>();
                if (content != null
                    && content.HasCatalog
                    && content.Catalog.Rewards.TryGetRoom(roomKind, out var room)
                    && !string.IsNullOrWhiteSpace(room.DisplayName))
                {
                    description = BuildRoomDescription(room);
                    return true;
                }
            }

            description = roomKind switch
            {
                RoomKind.Shop => "商店：挑选帮助卡",
                RoomKind.Gold => "金币房：获得金币",
                RoomKind.Treasure => "宝箱房：挑选遗物",
                RoomKind.Fountain => "温泉房：提升血量上限并回满",
                RoomKind.Tavern => "酒馆",
                RoomKind.Event => "事件房",
                RoomKind.Battle => "战斗房",
                RoomKind.Elite => "精英房",
                RoomKind.Boss => "Boss 房",
                _ => roomKind.ToString(),
            };
            return true;
        }

        private static string BuildRoomDescription(RoomDefinition room)
        {
            if (room.ShopOfferCount > 0)
            {
                return $"{room.DisplayName}：挑选帮助卡";
            }

            if (room.GoldDelta != 0)
            {
                return $"{room.DisplayName}：金币{(room.GoldDelta > 0 ? "+" : string.Empty)}{room.GoldDelta}";
            }

            if (room.MaxHpDelta != 0 || room.HealToFull)
            {
                var parts = new System.Collections.Generic.List<string>(2);
                if (room.MaxHpDelta != 0)
                {
                    parts.Add($"血量上限+{room.MaxHpDelta}");
                }

                if (room.HealToFull)
                {
                    parts.Add("回满血");
                }

                return $"{room.DisplayName}：{string.Join("，", parts)}";
            }

            if (!string.IsNullOrEmpty(room.RewardPoolId))
            {
                return $"{room.DisplayName}：挑选奖励";
            }

            return room.DisplayName;
        }

        private void EnsureVisualsLoaded()
        {
            if (_visualsResolved)
            {
                return;
            }

            _visualsResolved = true;
            ContentVisualBootstrap.TryLoad(
                ContentVisualBootstrap.ResolveLubanDataDirectory(),
                out _visualCatalog,
                out _frameStyleCatalog);
        }

        /// <summary>
        /// 限 MaxChars；换行符（\n / \r）计入长度。
        /// </summary>
        internal static string ClampDescription(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text) || maxChars <= 0)
            {
                return string.Empty;
            }

            if (text.Length <= maxChars)
            {
                return text;
            }

            return text.Substring(0, maxChars);
        }
    }
}
