using System;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Flow.BoardBriefTip;
using QFramework;
using UnityEngine;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// UseItem 被 Core 权威门禁拒绝（如遗物栏满拒开宝箱，ADR-0027 addendum / #143）时的
    /// 表现自愈收口：提示 + 拒绝音效 + 把仍在道具卡格中的卡视图归还手牌。
    /// 战斗（UseItemIntentScriptFactory）与非战斗（NonCombatUseItemIntentScriptFactory）
    /// 的拒收路径共用，避免拖放路径已让卡视图消失后留下「内核有卡、画面没卡」的幽灵。
    /// </summary>
    public static class RejectedUseItemRecovery
    {
        /// <summary>该拒收是否属于「遗物栏满 + 开宝箱」组合（决定用宝箱专属拒绝音）。</summary>
        public static bool IsChestRelicFullRejection(IArchitecture arch, string defId)
        {
            if (arch == null || string.IsNullOrEmpty(defId))
            {
                return false;
            }

            var player = arch.GetModel<PlayerModel>();
            if (player == null || !player.IsRelicInventoryFull)
            {
                return false;
            }

            return ChestUseRelicPoolRule.IsChestUseOfferingRelicPool(
                arch.GetSystem<IContentSystem>(),
                defId);
        }

        /// <summary>提示 + 音效。chestRelicFull=true 用宝箱专属拒绝音，否则通用拒绝音。</summary>
        public static void SurfaceRejection(string rejectReason, string cardDefId, bool chestRelicFull)
        {
            if (!string.IsNullOrEmpty(rejectReason))
            {
                BoardBriefTipPresenter.EnsureExists().ShowNotice(rejectReason);
            }

            var cueId = chestRelicFull
                ? InteractionAudioCues.ChestOpenReject
                : InteractionAudioCues.UiReject;
            if (string.IsNullOrEmpty(cardDefId))
            {
                InteractionAudioCues.Pulse(cueId, "RejectedUseItemRecovery.SurfaceRejection");
            }
            else
            {
                InteractionAudioCues.PulseCard(cueId, "RejectedUseItemRecovery.SurfaceRejection", cardDefId);
            }
        }

        /// <summary>
        /// 若 Core 仍把该卡持在道具卡格、而表现视图已被拖放路径释放，则重生手牌视图并拉回手牌。
        /// 先等 VanishCardAfterApplyAsync 完全释放旧视图，避免重复 SpawnView（uid 冲突）。
        /// </summary>
        public static async UniTask RestoreItemViewToHandAsync(IArchitecture arch, int itemUid, string defId)
        {
            if (arch == null || itemUid <= 0)
            {
                return;
            }

            var registry = arch.GetModel<CardRegistry>();
            CardInstance coreCard;
            if (!registry.TryGet(itemUid, out coreCard)
                || coreCard == null
                || coreCard.Zone.Value != ZoneId.ItemSlots)
            {
                return;
            }

            var deck = arch.GetModel<DeckModel>();
            var inItemSlots = false;
            for (var i = 0; i < deck.ItemSlotUids.Count; i++)
            {
                if (deck.ItemSlotUids[i] == itemUid)
                {
                    inItemSlots = true;
                    break;
                }
            }

            if (!inItemSlots)
            {
                return;
            }

            var cards = CardEntityLifecycleHook.CardsOrNull();
            var hand = CardEntityLifecycleHook.HandOrNull();
            if (cards == null || hand == null)
            {
                return;
            }

            // 视图可能处于三种状态：
            // ① 已回手（如重复拒收/旧路径）→ 无需恢复；
            // ② 拖放退场中（RemovedMode 播放中）或待退场（DragCardMode）→ 等释放后重生；
            // ③ 已被释放 → 直接重生。
            ManagedCard existing;
            if (cards.TryGet(itemUid, out existing) && existing != null)
            {
                if (existing.DisplayMode == CardDisplayMode.HandCardMode
                    && hand.ContainsUid(itemUid))
                {
                    return;
                }

                var waited = 0f;
                while (cards.TryGet(itemUid, out _))
                {
                    if (waited > RejectedItemRestoreWaitSeconds)
                    {
                        Debug.LogWarning(
                            "[RejectedUseItemRecovery] 等待旧视图释放超时 uid=" + itemUid);
                        return;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update);
                    waited += Time.unscaledDeltaTime;
                }
            }

            // 等手牌自身忙碌/拖拽结束，避免在进场途中抢槽。
            var waitedIdle = 0f;
            while (hand.IsSelfBusy || hand.IsDragging)
            {
                if (waitedIdle > RejectedItemRestoreWaitSeconds)
                {
                    Debug.LogWarning(
                        "[RejectedUseItemRecovery] 等待手牌空闲超时 uid=" + itemUid);
                    return;
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
                waitedIdle += Time.unscaledDeltaTime;
            }

            var resolvedDefId = string.IsNullOrEmpty(defId) ? coreCard.DefId : defId;
            var view = cards.SpawnView(
                itemUid,
                resolvedDefId,
                initialMode: CardDisplayMode.HandCardMode,
                kind: CoreCardPresentationMapper.ResolvePresentationKind(itemUid, resolvedDefId));
            if (view == null)
            {
                Debug.LogWarning(
                    "[RejectedUseItemRecovery] 恢复手牌视图失败 uid=" + itemUid);
                return;
            }

            CoreCardPresentationMapper.ApplyToManagedCard(view);
            CardFaceGenerationBootstrap.ApplyFaceHistoryForUid(arch, itemUid);
            var restored = await hand.PullFromGroundAsync(view, skipBusyGuard: true);
            if (!restored)
            {
                Debug.LogWarning(
                    "[RejectedUseItemRecovery] PullFromGroundAsync 失败 uid=" + itemUid);
                cards.Release(view, "RejectedUseItemRecovery.RestoreFailed");
            }
        }

        private const float RejectedItemRestoreWaitSeconds = 5f;
    }
}
