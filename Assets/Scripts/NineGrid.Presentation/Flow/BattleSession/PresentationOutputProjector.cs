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
    /// 局内单向输出投影：伤害飘字、金币、Trigger 脉冲、拾取后旁路演出。
    /// 卡面数值提交改由 <see cref="BattleBeatScheduler"/> 在表演锚点消费结算指令；本类不再直读同步卡面攻防血。
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
        /// 按 DamageDealt 事件序分别飘字；无 popups 时回退主目标 DamageAmount（对齐 FieldBattle 强兜底）。
        /// </summary>
        public static void SpawnDamagePopups(
            CombatDamagePopup[] popups,
            ManagedCard fallbackVictim,
            int fallbackAmount)
        {
            var cardManager = CardEntityLifecycleHook.CardsOrNull();
            if (popups != null && popups.Length > 0)
            {
                for (var i = 0; i < popups.Length; i++)
                {
                    var popup = popups[i];
                    if (popup.Amount <= 0 || popup.TargetUid <= 0)
                    {
                        continue;
                    }

                    Vector3? pos = null;
                    if (cardManager != null
                        && cardManager.TryGet(popup.TargetUid, out var view)
                        && view?.Transform != null)
                    {
                        pos = view.Transform.position;
                    }
                    else if (fallbackVictim != null
                             && fallbackVictim.Uid == popup.TargetUid
                             && fallbackVictim.Transform != null)
                    {
                        pos = fallbackVictim.Transform.position;
                    }

                    if (pos.HasValue)
                    {
                        DamageNumberHook.RequestSpawn(pos.Value, popup.Amount);
                    }
                }

                return;
            }

            if (fallbackAmount > 0 && fallbackVictim?.Transform != null)
            {
                DamageNumberHook.RequestSpawn(fallbackVictim.Transform.position, fallbackAmount);
            }
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
        /// 扫描 EffectTriggered：经 TriggerPulseHub 发 FX/音效脉冲（发即完成、可降级）。
        /// 仅九宫格在场卡；卡组 / 手牌 / 已移除不播。不占主时间线控制权。
        /// 卡面数值不在此同步——观察型加攻等走收尾锚点。
        /// </summary>
        public static void PresentEffectTriggersFromEventLog(int startIndex)
        {
            if (startIndex < 0)
            {
                return;
            }

            var arch = NineGridArchitecture.Current;
            var entries = arch.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            if (startIndex >= entries.Count)
            {
                return;
            }

            var seen = new HashSet<int>();
            for (var i = startIndex; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Type != CoreEventType.EffectTriggered || e.CardUid <= 0)
                {
                    continue;
                }

                if (!IsCoreCardOnBoardForEffectPresentation(e.CardUid))
                {
                    continue;
                }

                if (!seen.Add(e.CardUid))
                {
                    continue;
                }

                var fxId = CardEffectTriggerPulseSink.IdForCard(e.CardUid);
                TriggerPulseHub.PulseFx(fxId);
                TriggerPulseHub.PulseAudio("sfx.effect." + e.CardUid.ToString());
            }
        }

        /// <summary>
        /// Pickup Command 写 Core 后的旁路演出（金币/触发/洗牌/HUD）；不承担规则写。
        /// </summary>
        public static void PresentPickupPostApplyEffects(int startIndex, int pickedUid)
        {
            PresentGoldGainsFromEventLog(startIndex, ResolveCardWorldPosition(pickedUid));
            PresentEffectTriggersFromEventLog(startIndex);
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

        private static bool IsCoreCardOnBoardForEffectPresentation(int uid)
        {
            if (uid <= 0)
            {
                return false;
            }

            var arch = NineGridArchitecture.Current;
            if (arch == null)
            {
                return false;
            }

            var registry = arch.GetModel<CardRegistry>();
            if (!registry.TryGet(uid, out var coreCard))
            {
                return false;
            }

            return coreCard.Zone.Value == ZoneId.Board;
        }
    }
}
