using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Presentation;
using QFramework;
using UnityEngine;

namespace NineGrid.Flow.InRoomBoard
{
    /// <summary>
    /// 商店/奖励房货架购领：Core 已直写 ItemSlots（ADR-0025），表现须把新卡从货架位接入手牌，
    /// 禁止只碎裂纯表现货架卡导致「逻辑有牌、手牌看不见」。
    /// </summary>
    public static class InRoomItemAcquirePresentation
    {
        /// <summary>
        /// 用 EventLog 自 startIndex 起的 CardSpawned，把货架纯表现卡换成 Core uid 视图并飞入手牌。
        /// 成功接手返回 true（调用方勿再 Release 货架卡）；无授予/失败返回 false。
        /// </summary>
        public static bool TryAcquireShelfHelpCardToHand(
            IArchitecture arch,
            int eventLogStart,
            ManagedCard shelfPresentationCard)
        {
            if (arch == null)
            {
                return false;
            }

            var spawnedUids = CollectItemSlotSpawnUidsSince(arch, eventLogStart);
            if (spawnedUids.Count == 0)
            {
                return false;
            }

            var cards = CardEntityLifecycleHook.CardsOrNull()
                        ?? Object.FindFirstObjectByType<CardManagerSingleton>();
            var hand = CardEntityLifecycleHook.HandOrNull()
                       ?? Object.FindFirstObjectByType<CardHandManagerSingleton>();
            if (cards == null || hand == null)
            {
                Debug.LogWarning("[InRoomItemAcquire] missing CardManager/Hand; cannot present acquire");
                return false;
            }

            var worldPos = shelfPresentationCard != null && shelfPresentationCard.Transform != null
                ? shelfPresentationCard.Transform.position
                : Vector3.zero;
            var worldRot = shelfPresentationCard != null && shelfPresentationCard.Transform != null
                ? shelfPresentationCard.Transform.rotation
                : Quaternion.identity;

            if (shelfPresentationCard != null)
            {
                cards.Release(shelfPresentationCard, "InRoom.AcquireReplaceShelf");
            }

            var presentedAny = false;
            for (var i = 0; i < spawnedUids.Count; i++)
            {
                var uid = spawnedUids[i];
                if (uid <= 0)
                {
                    continue;
                }

                if (hand.ContainsUid(uid))
                {
                    presentedAny = true;
                    continue;
                }

                ManagedCard view;
                if (cards.TryGet(uid, out view) && view != null)
                {
                    StripInRoomShelfProxies(view.GameObject);
                    presentedAny = true;
                    PullIntoHandAsync(hand, view).Forget();
                    continue;
                }

                if (!arch.GetModel<CardRegistry>().TryGet(uid, out var coreCard)
                    || coreCard == null
                    || string.IsNullOrEmpty(coreCard.DefId))
                {
                    Debug.LogWarning("[InRoomItemAcquire] Core card missing uid=" + uid);
                    continue;
                }

                var kind = CoreCardPresentationMapper.ResolvePresentationKind(uid, coreCard.DefId);
                view = cards.SpawnView(
                    uid,
                    coreCard.DefId,
                    parent: null,
                    CardDisplayMode.GroundCardMode,
                    kind);
                if (view?.Transform == null)
                {
                    Debug.LogWarning("[InRoomItemAcquire] SpawnView failed uid=" + uid);
                    continue;
                }

                // 从货架世界位起步；多张时叠一点避免完全重合。
                view.Transform.SetPositionAndRotation(
                    worldPos + new Vector3(0.05f * i, 0f, 0f),
                    worldRot);
                CoreCardPresentationMapper.ApplyToManagedCard(view);
                StripInRoomShelfProxies(view.GameObject);

                presentedAny = true;
                PullIntoHandAsync(hand, view).Forget();
            }

            return presentedAny;
        }

        public static List<int> CollectItemSlotSpawnUidsSince(IArchitecture arch, int startIndex)
        {
            var result = new List<int>(2);
            if (arch == null || startIndex < 0)
            {
                return result;
            }

            var entries = arch.GetSystem<IActionPipelineSystem>()?.EventLog?.Entries;
            if (entries == null || startIndex >= entries.Count)
            {
                return result;
            }

            var deck = arch.GetModel<DeckModel>();
            for (var i = startIndex; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Type != CoreEventType.CardSpawned || entry.CardUid <= 0)
                {
                    continue;
                }

                if (!IsRegisteredInItemSlots(deck, entry.CardUid))
                {
                    continue;
                }

                if (result.Contains(entry.CardUid))
                {
                    continue;
                }

                result.Add(entry.CardUid);
            }

            return result;
        }

        private static async UniTaskVoid PullIntoHandAsync(
            CardHandManagerSingleton hand,
            ManagedCard card)
        {
            if (hand == null || card == null)
            {
                return;
            }

            try
            {
                var ok = await hand.PullFromGroundAsync(card, skipBusyGuard: true);
                if (ok)
                {
                    return;
                }

                // 飞入被拒时就地贴入，避免 Core 已有牌而手牌永久缺视图。
                if (!hand.ContainsUid(card.Uid))
                {
                    hand.TryPlaceInHandImmediate(card, skipBusyGuard: true);
                }
            }
            catch (System.OperationCanceledException)
            {
            }
        }

        private static void StripInRoomShelfProxies(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            var shop = go.GetComponent<ShopBoard.ShopBoardHitProxy>();
            if (shop != null)
            {
                Object.Destroy(shop);
            }

            var reward = go.GetComponent<RewardBoard.RewardBoardHitProxy>();
            if (reward != null)
            {
                Object.Destroy(reward);
            }

            var tip = go.GetComponent<BoardBriefTip.BoardBriefTipHitProxy>();
            if (tip != null)
            {
                Object.Destroy(tip);
            }
        }

        private static bool IsRegisteredInItemSlots(DeckModel deck, int itemUid)
        {
            if (deck == null || itemUid <= 0)
            {
                return false;
            }

            var slots = deck.ItemSlotUids;
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i] == itemUid)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
