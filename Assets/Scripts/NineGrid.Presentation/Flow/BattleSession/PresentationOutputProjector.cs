using System;
using System.Collections.Generic;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation;
using NineGrid.Presentation.Systems;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 局内单向输出投影：拾取后旁路演出、世界坐标解析。
    /// 卡面数值 / 伤害飘字 / FX 脉冲 / 金币 / Avatar HUD 改由 <see cref="BattleBeatScheduler"/> 在表演锚点经处理器消费。
    /// </summary>
    public static class PresentationOutputProjector
    {
        public static CombatDamagePopup[] CollectDamagePopups(
            IReadOnlyList<CoreGameEvent> entries,
            int startIndex)
        {
            if (entries == null || startIndex >= entries.Count)
            {
                return Array.Empty<CombatDamagePopup>();
            }

            var popups = new List<CombatDamagePopup>(4);
            for (var i = Math.Max(0, startIndex); i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Type == CoreEventType.DamageDealt && e.Amount > 0 && e.TargetUid > 0)
                {
                    popups.Add(new CombatDamagePopup { TargetUid = e.TargetUid, Amount = e.Amount });
                }
            }

            return popups.Count > 0 ? popups.ToArray() : Array.Empty<CombatDamagePopup>();
        }

        /// <summary>
        /// Pickup Command 写 Core 后的旁路演出：经统一 FlushBeats 消费金币/HUD；洗牌仍旁路。
        /// </summary>
        public static void PresentPickupPostApplyEffects(int startIndex, int pickedUid)
        {
            _ = pickedUid;
            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            BattleBeatFlush.PresentEventLogSlice(arch, startIndex);
            arch?.GetSystem<IBattleSessionSystem>()?.PresentShuffleIntoDeckFromEventLog(startIndex);
        }

        /// <summary>
        /// 视觉对账（保留已提交数值，不从 Core 覆写攻防血）。
        /// Avatar 血甲 HUD 由 <see cref="PlayerInfoHudBeatHandler"/> 在 Impact 用指令刷新。
        /// </summary>
        public static void SyncManagedCardPresentation(ManagedCard card)
        {
            CoreCardPresentationMapper.ApplyVisualsPreservingCommittedStats(card);
        }

        public static Vector3? ResolveCardWorldPosition(int cardUid)
        {
            if (cardUid <= 0)
            {
                return null;
            }

            var cards = CardEntityLifecycleHook.CardsOrNull();
            if (cards != null
                && cards.TryGet(cardUid, out var view)
                && view?.Transform != null)
            {
                return view.Transform.position;
            }

            return null;
        }

        public static Vector3? ResolveBoardSlotWorldPosition(int groundSlot)
        {
            var field = GroundFieldGeometryHook.FieldOrNull();
            if (field == null)
            {
                return null;
            }

            if (field.TryGetCardAt(groundSlot, out var card) && card?.Transform != null)
            {
                return card.Transform.position;
            }

            // 空格探求：尽量用格锚点；无则交给 GoldFx 默认屏幕中心。
            var anchor = field.GetGroundAnchor(groundSlot);
            if (anchor != null)
            {
                return anchor.position;
            }

            return null;
        }
    }
}
