using NineGrid.Core;
using TMPro;
using UnityEngine;

namespace NineGrid.Presentation.Visuals
{
    /// <summary>
    /// 玩家状态 HUD 视图绑定：HP / 护甲 / 金币 / 互动次数。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TableNineStatusPanelView : MonoBehaviour
    {
        [Header("Labels")]
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private TMP_Text armorText;
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private TMP_Text interactionText;

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

        public void ApplySnapshot(CoreViewSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            SetGold(snapshot.Coins);
            SetInteractionCount(snapshot.InteractionCount);

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
                case CoreEventType.GoldModified:
                    SetGold(evt.Amount);
                    break;
                case CoreEventType.InteractionChanged:
                    SetInteractionCount(evt.Amount);
                    break;
            }
        }

        private static bool IsAvatarCard(CoreGameEvent evt, CoreViewSnapshot snapshot)
        {
            return snapshot != null && evt != null && evt.CardUid == snapshot.AvatarUid;
        }
    }
}
