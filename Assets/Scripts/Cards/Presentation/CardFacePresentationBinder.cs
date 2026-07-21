using System.Collections.Generic;
using NineGrid.Cards.Slots;
using UnityEngine;

namespace NineGrid.Cards.Presentation
{
    /// <summary>
    /// L4 卡面 Kind Binder：按投影 Kind 路由消费图标/名字/数值；未绑字段忽略。
    /// 朝向：本波恒正面（front 显 / back 隐），存储 FaceUp 供后续演出循迹。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardFacePresentationBinder : MonoBehaviour, ICardFaceBinder
    {
        private bool _committedFaceUp = true;
        private Dictionary<string, Sprite> _templateDefaults;

        /// <summary>最近一次 Commit 的朝向镜像（供后续翻牌专题读取；本波不驱动演出）。</summary>
        public bool CommittedFaceUp => _committedFaceUp;

        private void Awake()
        {
            CaptureTemplateDefaultsIfNeeded();
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
            ApplyFaceOrientation(snapshot.FaceUp);
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
            TryApplySprite(CardFaceSlotCodes.FaceBackground, snapshot.FaceBackground);
            TryApplySprite(CardFaceSlotCodes.BackBorder, snapshot.BackBorder);
            TryApplySprite(CardFaceSlotCodes.BackShirt, snapshot.BackShirt);
            TryApplySprite(CardFaceSlotCodes.BackLogo, snapshot.BackLogo);
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
