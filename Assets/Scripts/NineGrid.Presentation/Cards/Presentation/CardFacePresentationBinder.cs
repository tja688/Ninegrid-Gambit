using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using NineGrid.Cards;
using NineGrid.Cards.Anim;
using NineGrid.Cards.Slots;
using NineGrid.Content.CardPresentation;
using TMPro;
using UnityEngine;

namespace NineGrid.Cards.Presentation
{
    /// <summary>
    /// L4 卡面 Kind Binder：按投影 Kind 路由消费图标/名字/数值/基础描述；未绑字段忽略。
    /// 朝向：本波恒正面（front 显 / back 隐），存储 FaceUp 供后续演出循迹。
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

        /// <summary>最近一次 Commit 的朝向镜像（供后续翻牌专题读取；本波不驱动演出）。</summary>
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
            ApplyName(snapshot.DisplayName);
            ApplyStats(snapshot);
            ApplyBasicDescription(snapshot);
            ApplyFaceOrientation(snapshot.FaceUp);
            TryPlayIdleOrStatic(snapshot);
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
            // Core：实例级牌面朝向权威（本波可不落地状态机）。
            // 表现投影：仅 Commit 镜像（本字段）。
            // 卡面：只消费已提交朝向；本波恒正面，无翻转演出 / 无 DisplayMode→朝向通道。
            _committedFaceUp = faceUp;

            var front = FindFrontRoot();
            var back = FindBackRoot();
            if (front != null)
            {
                front.gameObject.SetActive(true);
            }

            if (back != null)
            {
                back.gameObject.SetActive(false);
            }
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

            text.text = composed.TmpRichText;
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

            return map;
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

#if UNITY_EDITOR
            _cachedInlineIconStyle = UnityEditor.AssetDatabase.LoadAssetAtPath<CardFaceDescriptionInlineIconStyleSO>(
                CardChassisPaths.DescriptionInlineIconStyleAsset);
#endif
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

#if UNITY_EDITOR
            _cachedIconCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<CardFaceDescriptionIconCatalogSO>(
                CardChassisPaths.DescriptionIconCatalogAsset);
#endif
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
                    // 行动计数首波未接线 → 缺省 0。
                    SetNumeric(CardFaceSlotCodes.ActionCount, snapshot.ActionCount);
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
