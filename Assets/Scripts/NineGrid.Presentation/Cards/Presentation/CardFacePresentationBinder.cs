using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using NineGrid.Cards;
using NineGrid.Cards.Anim;
using NineGrid.Cards.Slots;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using TMPro;
using UnityEngine;

namespace NineGrid.Cards.Presentation
{
    /// <summary>
    /// L4 卡面 Kind Binder：按投影 Kind 路由消费图标/名字/数值/基础描述；未绑字段忽略。
    /// 朝向：Commit 时按 FaceUp 显隐 front/back；翻牌动画由 FlipPresenter 驱动。
    /// 基础描述静态组装（含 `[SlotCode]`→真实图标），不随数值 Commit 重算跳动。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardFacePresentationBinder : MonoBehaviour, ICardFaceBinder
    {
        private const float StatIconPulsePeak = 1.1f;
        private const float StatIconPulseOutDuration = 0.08f;
        private const float StatIconPulseInDuration = 0.14f;
        private const string StatIconPulseTweenIdPrefix = "CardFaceStatIconPulse.";

        private bool _committedFaceUp = true;
        private Dictionary<string, Sprite> _templateDefaults;
        private string _templateBasicDescription;
        private bool _hasTemplateBasicDescription;
        private float _descriptionTemplateFontSize = -1f;
        private string _lastBasicDescriptionSource;
        private string _lastIconFingerprint;
        private TMP_SpriteAsset _descriptionSpriteAsset;
        private Dictionary<string, int> _lastNumericValues;
        private readonly Dictionary<string, Tween> _statIconTweens = new Dictionary<string, Tween>();
        private readonly Dictionary<string, Vector3> _statIconBaseScales = new Dictionary<string, Vector3>();
        private static CardFaceSlotRegistrySO _defaultRegistry;
        private static CardFaceDescriptionInlineIconStyleSO _cachedInlineIconStyle;
        private static CardFaceDescriptionInlineIconStyleSO _inlineIconStyleOverride;
        private static CardFaceDescriptionIconCatalogSO _cachedIconCatalog;
        private static CardFaceDescriptionIconCatalogSO _iconCatalogOverride;

        /// <summary>最近一次 Commit 的朝向镜像。</summary>
        public bool CommittedFaceUp => _committedFaceUp;

        private void Awake()
        {
            CaptureTemplateDefaultsIfNeeded();
        }

        private void OnDestroy()
        {
            KillAllStatIconPulses();
            ReleaseDescriptionSpriteAsset();
        }

        public void ApplyPresentation(CardPresentationSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            CaptureTemplateDefaultsIfNeeded();
            ApplyMainIcon(snapshot.MainIcon);
            ApplyDirectSprites(snapshot);
            ApplyRarityVariants(snapshot.Rarity);
            // 遗物等六边框模板：矩形背景须吃 Mask_hexagon，否则四角漏出卡框。
            CardMainVisualMaskAnchor.EnsureFaceBackgroundHexMask(transform);
            ApplyName(snapshot.DisplayName);
            ApplyStats(snapshot);
            ApplyRhythmIconMatrix(snapshot);
            ApplyRhythmCountIcon(snapshot);
            ApplyBasicDescription(snapshot);
            ApplyFaceOrientation(snapshot.FaceUp);
            TryPlayIdleOrStatic(snapshot);
        }

        /// <summary>供 Mapper 合成详情时读取词条表（可无）。</summary>
        public static CardFaceDescriptionIconCatalogSO PeekDescriptionIconCatalog()
        {
            return GetIconCatalog();
        }

        private void TryPlayIdleOrStatic(CardPresentationSnapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.DefId))
            {
                return;
            }

            if (!CardPresentationConfigCatalog.TryGet(snapshot.DefId, out var config) || config == null)
            {
                return;
            }

            var player = GetComponent<CardSpriteAnimPlayer>();
            if (player == null)
            {
                player = gameObject.AddComponent<CardSpriteAnimPlayer>();
            }

            player.BindConfig(config);
            player.PlayIdleOrStatic();
        }

        public void ApplyFaceOrientation(bool faceUp)
        {
            // Core：实例级牌面朝向权威。
            // 表现投影：仅 Commit 镜像（本字段）。
            // 卡面：消费已提交朝向；front/back 显隐跟随 faceUp。
            _committedFaceUp = faceUp;

            var front = FindFrontRoot();
            var back = FindBackRoot();
            if (front != null)
            {
                front.gameObject.SetActive(faceUp);
            }

            if (back != null)
            {
                back.gameObject.SetActive(!faceUp);
            }
        }

        /// <summary>只更新 Commit 镜像，不立刻切 front/back（翻牌动画路径）。</summary>
        public void RecordCommittedFaceUp(bool faceUp)
        {
            _committedFaceUp = faceUp;
        }

        private void CaptureTemplateDefaultsIfNeeded()
        {
            if (_templateDefaults != null)
            {
                return;
            }

            _templateDefaults = CardFaceSlotNodeMap.CaptureTemplateDefaults(transform);
            _hasTemplateBasicDescription = CardFaceSlotNodeMap.TryReadText(
                transform,
                CardFaceSlotCodes.BasicDescription,
                out _templateBasicDescription);
        }

        private void ApplyMainIcon(Sprite contentIcon)
        {
            if (!CardFaceSlotNodeMap.TryFindRenderer(
                    transform,
                    CardFaceSlotCodes.MainIcon,
                    out var renderer))
            {
                return;
            }

            Sprite templateDefault = null;
            _templateDefaults?.TryGetValue(CardFaceSlotCodes.MainIcon, out templateDefault);
            var resolved = contentIcon != null ? contentIcon : templateDefault;
            if (resolved != null)
            {
                renderer.sprite = resolved;
                renderer.enabled = true;
            }
        }

        private void ApplyDirectSprites(CardPresentationSnapshot snapshot)
        {
            // null = 保留卡面模板兜底，不覆盖。
            TryApplySprite(CardFaceSlotCodes.FaceBackground, snapshot.FaceBackground);
            TryApplySprite(CardFaceSlotCodes.BackBorder, snapshot.BackBorder);
            TryApplySprite(CardFaceSlotCodes.BackShirt, snapshot.BackShirt);
            TryApplySprite(CardFaceSlotCodes.BackLogo, snapshot.BackLogo);
            TryApplySprite(CardFaceSlotCodes.CardFrame, snapshot.CardFrame);
            TryApplySprite(CardFaceSlotCodes.Banner, snapshot.Banner);
        }

        /// <summary>
        /// 稀有度变体切换：卡面模板「卡框 / 横幅」节点下若存在「白 / 蓝 / 金」子变体
        /// （当前仅遗物卡标准模版），按内容稀有度只保留匹配的一个；
        /// 「副Icon」节点下若存在「高常规 / 中常规 / 低常规 / 特殊」档位子对象
        /// （当前仅道具卡标准模版），按道具卡稀有度四档切换（ADR-0033 修订）。
        /// 模板无变体子节点时 no-op（其余 Kind 模板不受影响）。
        /// </summary>
        private void ApplyRarityVariants(NineGrid.Core.Content.ContentRarity rarity)
        {
            ApplyRarityVariant(CardFaceSlotCodes.CardFrame, rarity);
            ApplyRarityVariant(CardFaceSlotCodes.Banner, rarity);
            ApplySubIconRarityVariant(rarity);
        }

        private const string SubIconRootName = "副Icon";
        private const string SubIconHighRegularName = "高常规";
        private const string SubIconMidRegularName = "中常规";
        private const string SubIconLowRegularName = "低常规";
        private const string SubIconSpecialName = "特殊";

        /// <summary>
        /// 道具卡副图标档位切换（ADR-0033 修订）：白=高常规 / 蓝=中常规 / 金=低常规 / 红=特殊。
        /// 模板无「副Icon」节点时 no-op；稀有度 None（未声明）时整组隐藏，不留错误档位。
        /// </summary>
        private void ApplySubIconRarityVariant(NineGrid.Core.Content.ContentRarity rarity)
        {
            var subIconRoot = FindDeepByName(transform, SubIconRootName);
            if (subIconRoot == null)
            {
                return;
            }

            string targetName;
            switch (rarity)
            {
                case NineGrid.Core.Content.ContentRarity.White:
                    targetName = SubIconHighRegularName;
                    break;
                case NineGrid.Core.Content.ContentRarity.Blue:
                    targetName = SubIconMidRegularName;
                    break;
                case NineGrid.Core.Content.ContentRarity.Gold:
                    targetName = SubIconLowRegularName;
                    break;
                case NineGrid.Core.Content.ContentRarity.Red:
                    targetName = SubIconSpecialName;
                    break;
                default:
                    targetName = null;
                    break;
            }

            var matchedAny = false;
            for (var i = 0; i < subIconRoot.childCount; i++)
            {
                var child = subIconRoot.GetChild(i);
                switch (child.name)
                {
                    case SubIconHighRegularName:
                    case SubIconMidRegularName:
                    case SubIconLowRegularName:
                    case SubIconSpecialName:
                        var match = targetName != null && child.name == targetName;
                        child.gameObject.SetActive(match);
                        matchedAny |= match;
                        break;
                }
            }

            subIconRoot.gameObject.SetActive(matchedAny);
        }

        private static Transform FindDeepByName(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindDeepByName(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private void ApplyRarityVariant(string slotCode, NineGrid.Core.Content.ContentRarity rarity)
        {
            if (!CardFaceSlotNodeMap.TryFindRenderer(transform, slotCode, out var renderer)
                || renderer == null)
            {
                return;
            }

            var parent = renderer.transform;
            Transform white = null;
            Transform blue = null;
            Transform gold = null;
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                switch (child.name)
                {
                    case "白":
                        white = child;
                        break;
                    case "蓝":
                        blue = child;
                        break;
                    case "金":
                        gold = child;
                        break;
                }
            }

            if (white == null && blue == null && gold == null)
            {
                return;
            }

            // Red（独特）暂无专属卡框/横幅美术：兜底用金档；None/未知兜底白档。
            var target = rarity switch
            {
                NineGrid.Core.Content.ContentRarity.Blue => blue,
                NineGrid.Core.Content.ContentRarity.Gold => gold,
                NineGrid.Core.Content.ContentRarity.Red => gold,
                _ => white,
            };
            if (target == null)
            {
                target = white != null ? white : (blue != null ? blue : gold);
            }

            if (white != null)
            {
                white.gameObject.SetActive(white == target);
            }

            if (blue != null)
            {
                blue.gameObject.SetActive(blue == target);
            }

            if (gold != null)
            {
                gold.gameObject.SetActive(gold == target);
            }
        }

        private void TryApplySprite(string slotCode, Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            if (CardFaceSlotNodeMap.TryFindRenderer(transform, slotCode, out var renderer))
            {
                renderer.sprite = sprite;
                renderer.enabled = true;
            }
        }

        private void ApplyName(string displayName)
        {
            if (!CardFaceSlotNodeMap.TryFindText(transform, CardFaceSlotCodes.Name, out var text))
            {
                return;
            }

            text.text = displayName ?? string.Empty;
        }

        private void ApplyBasicDescription(CardPresentationSnapshot snapshot)
        {
            if (!CardFaceSlotNodeMap.TryFindText(
                    transform,
                    CardFaceSlotCodes.BasicDescription,
                    out var text))
            {
                return;
            }

            var assembled = BuildAssembledIcons(snapshot);
            var catalog = GetIconCatalog();
            // 空描述 = 未接线：保留模板自带文案，不把旧 ContentVisual 长文案盖上来。
            var source = string.IsNullOrEmpty(snapshot.BasicDescription)
                ? (_hasTemplateBasicDescription ? _templateBasicDescription : string.Empty)
                : snapshot.BasicDescription;
            if (snapshot.Kind == CardPresentationKind.HelpCard
                && !string.IsNullOrWhiteSpace(snapshot.DefId)
                && CardPresentationConfigCatalog.TryGet(snapshot.DefId, out var dto)
                && dto != null)
            {
                var liveDescription = HelpCardMagnitudeOverlay.ProjectHelpCardDescription(
                    dto.description,
                    dto.effectAssemblies);
                if (!string.IsNullOrWhiteSpace(liveDescription))
                {
                    source = liveDescription;
                }
            }
            var fingerprint = BuildIconFingerprint(assembled, catalog, source);
            if (source == _lastBasicDescriptionSource
                && fingerprint == _lastIconFingerprint)
            {
                return;
            }

            _lastBasicDescriptionSource = source;
            _lastIconFingerprint = fingerprint;

            if (string.IsNullOrEmpty(snapshot.BasicDescription) && _hasTemplateBasicDescription)
            {
                ReleaseDescriptionSpriteAsset();
                text.spriteAsset = null;
                text.text = _templateBasicDescription ?? string.Empty;
                return;
            }

            var composed = CardFaceDescriptionComposer.Compose(
                source,
                assembled,
                GetDefaultRegistry(),
                catalog);

            ReleaseDescriptionSpriteAsset();
            if (composed.Icons.Count > 0)
            {
                _descriptionSpriteAsset = CardFaceDescriptionSpriteAssetBuilder.Build(
                    composed.Icons,
                    GetInlineIconStyle());
                text.spriteAsset = _descriptionSpriteAsset;
            }
            else
            {
                text.spriteAsset = null;
            }

            EnsureDescriptionAutoFit(text);
            text.text = composed.TmpRichText;
        }

        /// <summary>
        /// 溢出防护（ADR-0046 §5）：英文等译文可比中文长，描述槽开 TMP autosize，
        /// 上限锁模板原字号（中文短文案视觉不变），下限 60% 防长译文垂直溢出。
        /// </summary>
        private void EnsureDescriptionAutoFit(TMP_Text text)
        {
            if (_descriptionTemplateFontSize <= 0f)
            {
                _descriptionTemplateFontSize = text.fontSize;
            }

            if (_descriptionTemplateFontSize <= 0f)
            {
                return;
            }

            text.enableAutoSizing = true;
            text.fontSizeMax = _descriptionTemplateFontSize;
            text.fontSizeMin = _descriptionTemplateFontSize * 0.6f;
        }

        private Dictionary<string, Sprite> BuildAssembledIcons(CardPresentationSnapshot snapshot)
        {
            var map = new Dictionary<string, Sprite>();

            Sprite templateMain = null;
            _templateDefaults?.TryGetValue(CardFaceSlotCodes.MainIcon, out templateMain);
            var main = snapshot.MainIcon != null ? snapshot.MainIcon : templateMain;
            if (main != null)
            {
                map[CardFaceSlotCodes.MainIcon] = main;
            }

            Sprite action = null;
            _templateDefaults?.TryGetValue(CardFaceSlotCodes.ActionIcon, out action);
            if (action != null)
            {
                map[CardFaceSlotCodes.ActionIcon] = action;
            }

            Sprite sync = null;
            _templateDefaults?.TryGetValue(CardFaceSlotCodes.SyncRhythmIcon, out sync);
            if (sync != null)
            {
                map[CardFaceSlotCodes.SyncRhythmIcon] = sync;
            }

            return map;
        }

        /// <summary>
        /// ADR-0038 图标矩阵：攻击模式槽 / 同步子图标显隐。
        /// 临时接线：有攻击模式时按 <see cref="CardFaceAttackPatternIconResolver"/> 换三档近战图标；
        /// 节奏源行动计数图标、完整 Catalog 待后续票。
        /// </summary>
        private void ApplyRhythmIconMatrix(CardPresentationSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Kind != CardPresentationKind.Monster)
            {
                return;
            }

            var hasPattern = NineGrid.Core.AttackPatternRules.ParticipatesInEnemyAction(snapshot.AttackPattern);
            var hasSync = snapshot.HasSyncRhythmSkills;

            // 攻击模式槽：有模式用模式图；仅同步则升格同步图；皆无则隐藏。
            if (CardFaceSlotNodeMap.TryFindRenderer(transform, CardFaceSlotCodes.ActionIcon, out var patternRenderer)
                && patternRenderer != null)
            {
                Sprite patternSprite = null;
                _templateDefaults?.TryGetValue(CardFaceSlotCodes.ActionIcon, out patternSprite);
                Sprite syncSprite = null;
                _templateDefaults?.TryGetValue(CardFaceSlotCodes.SyncRhythmIcon, out syncSprite);

                if (hasPattern)
                {
                    if (!CardFaceAttackPatternIconResolver.TryGet(snapshot.AttackPattern, out var resolvedPatternIcon))
                    {
                        resolvedPatternIcon = patternSprite;
                    }

                    if (resolvedPatternIcon != null)
                    {
                        patternRenderer.sprite = resolvedPatternIcon;
                    }

                    patternRenderer.enabled = true;
                    patternRenderer.gameObject.SetActive(true);
                }
                else if (hasSync)
                {
                    if (syncSprite != null)
                    {
                        patternRenderer.sprite = syncSprite;
                    }
                    else if (patternSprite != null)
                    {
                        patternRenderer.sprite = patternSprite;
                    }

                    patternRenderer.enabled = true;
                    patternRenderer.gameObject.SetActive(true);
                }
                else
                {
                    patternRenderer.enabled = false;
                    patternRenderer.gameObject.SetActive(false);
                }
            }

            // 子图标：仅「攻击+同步」时开。
            if (CardFaceSlotNodeMap.TryFindRenderer(transform, CardFaceSlotCodes.SyncRhythmIcon, out var syncRenderer)
                && syncRenderer != null)
            {
                var showChild = hasPattern && hasSync;
                syncRenderer.enabled = showChild;
                syncRenderer.gameObject.SetActive(showChild);
            }
        }

        /// <summary>
        /// 倒计时类型图标：机关模板默认移动计数，怪物模板默认行动计数。
        /// 有活跃节奏时按 JSON <c>rhythmSource</c> 换成对应词条 Sprite。
        /// </summary>
        private void ApplyRhythmCountIcon(CardPresentationSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.HasActiveRhythm)
            {
                return;
            }

            if (!CardFaceSlotNodeMap.TryFindRendererByNodeNames(
                    transform,
                    CardFaceIconGlossaryTargets.RhythmCountIconNodeNames,
                    out var renderer)
                || renderer == null)
            {
                return;
            }

            var term = CardRhythmRules.TokenAction;
            var iconCode = "action";
            if (!string.IsNullOrWhiteSpace(snapshot.DefId)
                && CardPresentationConfigCatalog.TryGet(snapshot.DefId.Trim(), out var dto)
                && dto != null
                && CardRhythmRules.TryParse(dto.rhythmSource, out var source)
                && source == CardRhythmSource.Move)
            {
                term = CardRhythmRules.TokenMove;
                iconCode = "move";
            }

            var catalog = PeekDescriptionIconCatalog();
            CardFaceDescriptionIconCatalogSO.Entry entry = null;
            if (catalog != null)
            {
                catalog.TryGetByDisplayName(term, out entry);
                if ((entry == null || entry.sprite == null)
                    && catalog.TryGetByCode(iconCode, out var byCode))
                {
                    entry = byCode;
                }
            }

            if (entry == null || entry.sprite == null)
            {
                return;
            }

            renderer.sprite = entry.sprite;
            renderer.enabled = true;
            renderer.gameObject.SetActive(true);
        }

        private void SetNumericSlotVisible(string slotCode, bool visible)
        {
            if (!CardFaceSlotNodeMap.TryFindText(transform, slotCode, out var text) || text == null)
            {
                return;
            }

            text.gameObject.SetActive(visible);
        }

        private static string BuildIconFingerprint(
            IReadOnlyDictionary<string, Sprite> assembled,
            CardFaceDescriptionIconCatalogSO catalog,
            string sourceDescription)
        {
            var codeSet = new HashSet<string>(System.StringComparer.Ordinal);
            if (assembled != null)
            {
                foreach (var key in assembled.Keys)
                {
                    codeSet.Add(key);
                }
            }

            if (catalog?.Entries != null)
            {
                for (var i = 0; i < catalog.Entries.Count; i++)
                {
                    var entry = catalog.Entries[i];
                    if (entry == null
                        || string.IsNullOrEmpty(entry.code)
                        || CardFaceDescriptionIconCatalogSO.IsReservedAssemblySlotCode(entry.code))
                    {
                        continue;
                    }

                    codeSet.Add(entry.code);
                }
            }

            var codes = new List<string>(codeSet);
            codes.Sort(System.StringComparer.Ordinal);
            var sb = new StringBuilder(codes.Count * 32 + 32);
            sb.Append("src=").Append(sourceDescription ?? string.Empty).Append('|');

            for (var i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                sb.Append(code).Append('=');
                Sprite sprite = null;
                if (assembled != null)
                {
                    assembled.TryGetValue(code, out sprite);
                }

                if (sprite == null && catalog != null)
                {
                    catalog.TryGet(code, out sprite);
                }

                if (sprite != null)
                {
                    sb.Append(sprite.GetInstanceID());
                }

                sb.Append(';');
            }

            if (catalog != null)
            {
                sb.Append("|catalog=").Append(catalog.GetInstanceID());
            }

            var style = GetInlineIconStyle();
            if (style != null)
            {
                sb.Append("|style=").Append(style.GetInstanceID());
                for (var i = 0; i < codes.Count; i++)
                {
                    style.Resolve(codes[i], out var bx, out var by, out var scale);
                    sb.Append(codes[i]).Append(':')
                        .Append(bx.ToString("R")).Append(',')
                        .Append(by.ToString("R")).Append(',')
                        .Append(scale.ToString("R")).Append(';');
                }
            }

            return sb.ToString();
        }

        private void ReleaseDescriptionSpriteAsset()
        {
            if (_descriptionSpriteAsset == null)
            {
                return;
            }

            CardFaceDescriptionSpriteAssetBuilder.DestroyBuilt(_descriptionSpriteAsset);
            _descriptionSpriteAsset = null;
        }

        private static CardFaceSlotRegistrySO GetDefaultRegistry()
        {
            if (_defaultRegistry == null)
            {
                _defaultRegistry = ScriptableObject.CreateInstance<CardFaceSlotRegistrySO>();
                _defaultRegistry.hideFlags = HideFlags.HideAndDontSave;
                _defaultRegistry.ApplyDefaultCatalog();
            }

            return _defaultRegistry;
        }

        private static CardFaceDescriptionInlineIconStyleSO GetInlineIconStyle()
        {
            if (_inlineIconStyleOverride != null)
            {
                return _inlineIconStyleOverride;
            }

            if (_cachedInlineIconStyle != null)
            {
                return _cachedInlineIconStyle;
            }

            _cachedInlineIconStyle = CardChassisPaths.LoadAsset<CardFaceDescriptionInlineIconStyleSO>(
                CardChassisPaths.DescriptionInlineIconStyleAsset);
            return _cachedInlineIconStyle;
        }

        private static CardFaceDescriptionIconCatalogSO GetIconCatalog()
        {
            if (_iconCatalogOverride != null)
            {
                return _iconCatalogOverride;
            }

            if (_cachedIconCatalog != null)
            {
                return _cachedIconCatalog;
            }

            _cachedIconCatalog = CardChassisPaths.LoadAsset<CardFaceDescriptionIconCatalogSO>(
                CardChassisPaths.DescriptionIconCatalogAsset);
            return _cachedIconCatalog;
        }

        /// <summary>编辑器专页可注入未落盘草稿 style；传 null 清除覆盖。</summary>
        public static void SetInlineIconStyleOverride(CardFaceDescriptionInlineIconStyleSO style)
        {
            _inlineIconStyleOverride = style;
        }

        /// <summary>编辑器专页可注入未落盘草稿 catalog；传 null 清除覆盖。</summary>
        public static void SetDescriptionIconCatalogOverride(CardFaceDescriptionIconCatalogSO catalog)
        {
            _iconCatalogOverride = catalog;
        }

        /// <summary>编辑器改 style 资产后清缓存，下次重新 Load。</summary>
        public static void InvalidateInlineIconStyleCache()
        {
            _cachedInlineIconStyle = null;
        }

        /// <summary>编辑器改 catalog 资产后清缓存，下次重新 Load。</summary>
        public static void InvalidateDescriptionIconCatalogCache()
        {
            _cachedIconCatalog = null;
        }

        private void ApplyStats(CardPresentationSnapshot snapshot)
        {
            switch (snapshot.Kind)
            {
                case CardPresentationKind.Avatar:
                    // 玩家卡：攻击 + 护甲；不绑血量到卡面。
                    SetNumeric(CardFaceSlotCodes.Attack, snapshot.Attack);
                    SetNumeric(CardFaceSlotCodes.Armor, snapshot.Armor);
                    break;

                case CardPresentationKind.Monster:
                    SetNumeric(CardFaceSlotCodes.Attack, snapshot.Attack);
                    SetNumeric(CardFaceSlotCodes.Armor, snapshot.Armor);
                    SetNumeric(CardFaceSlotCodes.Hp, snapshot.Hp);
                    // 行动计数经 UpdateActionCount 指令 Commit；无活跃节奏时隐藏/写 0。
                    if (snapshot.HasActiveRhythm)
                    {
                        SetNumeric(CardFaceSlotCodes.ActionCount, snapshot.ActionCount);
                        SetNumericSlotVisible(CardFaceSlotCodes.ActionCount, true);
                    }
                    else
                    {
                        SetNumeric(CardFaceSlotCodes.ActionCount, 0);
                        SetNumericSlotVisible(CardFaceSlotCodes.ActionCount, false);
                    }

                    break;

                case CardPresentationKind.Trap:
                    SetNumeric(CardFaceSlotCodes.Hp, snapshot.Hp);
                    if (snapshot.HasActiveRhythm || snapshot.ShowActionCount)
                    {
                        SetNumeric(CardFaceSlotCodes.ActionCount, snapshot.ActionCount);
                        SetNumericSlotVisible(CardFaceSlotCodes.ActionCount, true);
                    }
                    else
                    {
                        SetNumeric(CardFaceSlotCodes.ActionCount, 0);
                        SetNumericSlotVisible(CardFaceSlotCodes.ActionCount, false);
                    }

                    break;

                default:
                    // 道具 / 遗物：名字与主图标为主；数值槽有节点才写（通常无）。
                    break;
            }
        }

        private void SetNumeric(string slotCode, int value)
        {
            if (!CardFaceSlotNodeMap.TryFindText(transform, slotCode, out var text))
            {
                return;
            }

            var clamped = Mathf.Max(0, value);
            var hadPrevious = false;
            var previous = 0;
            if (_lastNumericValues != null
                && _lastNumericValues.TryGetValue(slotCode, out previous))
            {
                hadPrevious = true;
            }

            text.text = clamped.ToString();

            if (_lastNumericValues == null)
            {
                _lastNumericValues = new Dictionary<string, int>();
            }

            _lastNumericValues[slotCode] = clamped;

            // 首次写入 / 同值重 Commit：不脉冲（Spawn、Bootstrap、视觉重刷）。
            if (hadPrevious && previous != clamped)
            {
                TryPulseCompanionIcon(slotCode);
            }
        }

        private void TryPulseCompanionIcon(string numericSlotCode)
        {
            if (!CardFaceSlotNodeMap.TryFindCompanionIcon(transform, numericSlotCode, out var icon)
                || icon == null)
            {
                return;
            }

            if (_statIconTweens.TryGetValue(numericSlotCode, out var existing)
                && existing != null
                && existing.IsActive())
            {
                existing.Kill(complete: false);
            }

            if (!_statIconBaseScales.TryGetValue(numericSlotCode, out var baseScale))
            {
                baseScale = icon.localScale;
                _statIconBaseScales[numericSlotCode] = baseScale;
            }
            else
            {
                icon.localScale = baseScale;
            }

            var peak = baseScale * Mathf.Max(1.01f, StatIconPulsePeak);
            var tweenId = StatIconPulseTweenIdPrefix + numericSlotCode;
            var seq = DOTween.Sequence()
                .SetId(tweenId)
                .SetUpdate(true)
                .SetLink(icon.gameObject, LinkBehaviour.KillOnDestroy);
            seq.Append(icon.DOScale(peak, StatIconPulseOutDuration).SetEase(Ease.OutQuad));
            seq.Append(icon.DOScale(baseScale, StatIconPulseInDuration).SetEase(Ease.OutQuad));
            seq.OnKill(() =>
            {
                if (icon != null && _statIconBaseScales.TryGetValue(numericSlotCode, out var restore))
                {
                    icon.localScale = restore;
                }

                _statIconTweens.Remove(numericSlotCode);
            });
            _statIconTweens[numericSlotCode] = seq;
        }

        private void KillAllStatIconPulses()
        {
            if (_statIconTweens.Count == 0)
            {
                return;
            }

            var keys = new List<string>(_statIconTweens.Keys);
            for (var i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                if (!_statIconTweens.TryGetValue(key, out var tween) || tween == null)
                {
                    continue;
                }

                if (tween.IsActive())
                {
                    tween.Kill(complete: false);
                }
            }

            _statIconTweens.Clear();
        }

        private Transform FindFrontRoot()
        {
            return FindChildIgnoreCase(transform, "front")
                   ?? FindChildIgnoreCase(transform, "Front");
        }

        private Transform FindBackRoot()
        {
            return FindChildIgnoreCase(transform, "back")
                   ?? FindChildIgnoreCase(transform, "Back");
        }

        private static Transform FindChildIgnoreCase(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child != null
                    && string.Equals(child.name, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }

            return null;
        }
    }
}
