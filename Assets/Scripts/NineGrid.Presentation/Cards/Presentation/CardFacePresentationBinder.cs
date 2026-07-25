using System.Collections.Generic;
using System.Text;
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
        private bool _committedFaceUp = true;
        private Dictionary<string, Sprite> _templateDefaults;
        private string _templateBasicDescription;
        private bool _hasTemplateBasicDescription;
        private string _lastBasicDescriptionSource;
        private string _lastIconFingerprint;
        private TMP_SpriteAsset _descriptionSpriteAsset;
        private static CardFaceSlotRegistrySO _defaultRegistry;
        private static CardFaceDescriptionInlineIconStyleSO _cachedInlineIconStyle;
        private static CardFaceDescriptionInlineIconStyleSO _inlineIconStyleOverride;

        /// <summary>最近一次 Commit 的朝向镜像（供后续翻牌专题读取；本波不驱动演出）。</summary>
        public bool CommittedFaceUp => _committedFaceUp;

        private void Awake()
        {
            CaptureTemplateDefaultsIfNeeded();
        }

        private void OnDestroy()
        {
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
            // 空描述 = 未接线：保留模板自带文案，不把旧 ContentVisual 长文案盖上来。
            var source = string.IsNullOrEmpty(snapshot.BasicDescription)
                ? (_hasTemplateBasicDescription ? _templateBasicDescription : string.Empty)
                : snapshot.BasicDescription;
            var fingerprint = BuildIconFingerprint(assembled);
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
                GetDefaultRegistry());

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

        private static string BuildIconFingerprint(IReadOnlyDictionary<string, Sprite> assembled)
        {
            if (assembled == null || assembled.Count == 0)
            {
                return string.Empty;
            }

            var codes = new List<string>(assembled.Keys);
            codes.Sort(System.StringComparer.Ordinal);
            var sb = new StringBuilder(codes.Count * 24);
            for (var i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                sb.Append(code).Append('=');
                if (assembled.TryGetValue(code, out var sprite) && sprite != null)
                {
                    sb.Append(sprite.GetInstanceID());
                }

                sb.Append(';');
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

        /// <summary>编辑器专页可注入未落盘草稿 style；传 null 清除覆盖。</summary>
        public static void SetInlineIconStyleOverride(CardFaceDescriptionInlineIconStyleSO style)
        {
            _inlineIconStyleOverride = style;
        }

        /// <summary>编辑器改 style 资产后清缓存，下次重新 Load。</summary>
        public static void InvalidateInlineIconStyleCache()
        {
            _cachedInlineIconStyle = null;
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

            text.text = Mathf.Max(0, value).ToString();
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
