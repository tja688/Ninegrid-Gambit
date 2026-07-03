using NineGrid.Core;
using TMPro;
using UnityEngine;

namespace NineGrid.Presentation.Visuals
{
    /// <summary>
    /// 玩家状态 HUD 视图：金币/互动次数由 Model 直连；HP/护甲由战斗 Flow + Stat 事件投影驱动。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TableNineStatusPanelView : MonoBehaviour
    {
        [Header("Labels")]
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private TMP_Text armorText;
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private TMP_Text interactionText;

        public void TryWireFromHierarchy()
        {
            if (hpText == null)
            {
                hpText = FindTmpChild("HpText");
            }

            if (armorText == null)
            {
                armorText = FindTmpChild("ArmorText");
            }

            if (goldText == null)
            {
                goldText = FindTmpChild("GoldText");
            }

            if (interactionText == null)
            {
                interactionText = FindTmpChild("InteractionText");
            }
        }

        public void SetHp(int hp)
        {
            if (hpText != null)
            {
                hpText.text = hp.ToString();
            }
        }

        public void SetArmor(int armor)
        {
            if (armorText != null)
            {
                armorText.text = armor.ToString();
            }
        }

        public void SetGold(int gold)
        {
            if (goldText != null)
            {
                goldText.text = gold.ToString();
            }
        }

        public void SetInteractionCount(int count)
        {
            if (interactionText != null)
            {
                interactionText.text = count.ToString();
            }
        }

        /// <summary>
        /// 开局/按需：仅同步玩家化身 HP/护甲，不含金币与互动次数。
        /// </summary>
        public void ApplyAvatarCombatStats(CoreViewSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            BoardSlotView avatarSlot = snapshot.GetSlot(snapshot.AvatarSlot);
            if (avatarSlot != null && avatarSlot.CardUid == snapshot.AvatarUid)
            {
                SetHp(avatarSlot.Hp);
                SetArmor(avatarSlot.Armor);
            }
        }

        public void ApplyEvent(CoreGameEvent evt, CoreViewSnapshot snapshot)
        {
            if (evt == null)
            {
                return;
            }

            switch (evt.Type)
            {
                case CoreEventType.HpChanged:
                case CoreEventType.Healed:
                    if (IsAvatarCard(evt, snapshot))
                    {
                        SetHp(evt.RemainingHp);
                    }

                    break;
                case CoreEventType.ArmorChanged:
                    if (IsAvatarCard(evt, snapshot))
                    {
                        SetArmor(evt.RemainingArmor);
                    }

                    break;
            }
        }

        private TMP_Text FindTmpChild(string childName)
        {
            Transform child = transform.Find(childName);
            return child != null ? child.GetComponentInChildren<TMP_Text>(true) : null;
        }

        private static bool IsAvatarCard(CoreGameEvent evt, CoreViewSnapshot snapshot)
        {
            return snapshot != null && evt != null && evt.CardUid == snapshot.AvatarUid;
        }
    }
}
