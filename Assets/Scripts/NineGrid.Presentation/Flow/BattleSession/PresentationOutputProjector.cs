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
    /// 局内单向输出投影：金币、拾取后旁路演出、世界坐标解析。
    /// 卡面数值 / 伤害飘字 / FX 脉冲改由 <see cref="BattleBeatScheduler"/> 在表演锚点经处理器消费。
    /// </summary>
    public static class PresentationOutputProjector
    {
        private static UiPanelRouter _panelRouter;

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
        /// 扫描 EventLog 中的 GoldModified：经 Scheduler 广播单向表现事件，Binder 消费飞币/HUD。
        /// </summary>
        /// <param name="skipReason">若与事件 Message 相同则跳过（已由专用演出处理）。</param>
        public static void PresentGoldGainsFromEventLog(
            int startIndex,
            Vector3? originWorld = null,
            string skipReason = null)
        {
            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            if (arch == null)
            {
                return;
            }

            GoldGainPresentationBinder.EnsureInstalled();
            new GoldGainPresentationScheduler().PresentFromEventLog(
                arch,
                startIndex,
                originWorld,
                skipReason,
                ResolveCardWorldPosition);
        }

        /// <summary>
        /// Pickup Command 写 Core 后的旁路演出（金币/洗牌/HUD）；效果脉冲改经 Impact 装饰处理器。
        /// </summary>
        public static void PresentPickupPostApplyEffects(int startIndex, int pickedUid)
        {
            PresentGoldGainsFromEventLog(startIndex, ResolveCardWorldPosition(pickedUid));
            (NineGridArchitecture.Interface ?? NineGridArchitecture.Current)?
                .GetSystem<IBattleSessionSystem>()?
                .PresentShuffleIntoDeckFromEventLog(startIndex);
            PlayerInfoHudPresenter.TryGetInstance()?.SyncFromCore(animate: false);
        }

        /// <summary>
        /// 视觉对账（保留已提交数值，不从 Core 覆写攻防血）。
        /// Avatar 血甲 HUD 改由 <see cref="BattleBeatScheduler"/> 在消费 Avatar 数值指令同拍刷新。
        /// </summary>
        public static void SyncManagedCardPresentation(ManagedCard card)
        {
            CoreCardPresentationMapper.ApplyVisualsPreservingCommittedStats(card);
        }

        /// <summary>刷新 Avatar 调试文本；面板路由静态缓存，留空则场景查找。</summary>
        public static void UpdateAvatarDebugText()
        {
            if (_panelRouter == null)
            {
                _panelRouter = UnityEngine.Object.FindFirstObjectByType<UiPanelRouter>();
            }

            CoreCardPresentationMapper.UpdateAvatarDebugText(_panelRouter);
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
