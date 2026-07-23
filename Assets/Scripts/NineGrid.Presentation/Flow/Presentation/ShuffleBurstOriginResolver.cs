using System;
using NineGrid.Cards;
using UnityEngine;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 炸牌/洗入原点挑选：优先死亡格锚点，禁止用 StageFieldDead 后的尸体 live transform（y-80 离屏）。
    /// </summary>
    public static class ShuffleBurstOriginResolver
    {
        /// <summary>
        /// <see cref="CardManagerSingleton.StageFieldDeadCorpseOffAnchor"/> 把尸体下移的偏移。
        /// </summary>
        public const float StagedCorpseYOffset = 80f;

        /// <summary>低于此 Y 视为不可用死亡原点（尸体离屏）。</summary>
        public const float UnusableOriginYThreshold = -40f;

        public static bool IsUsableLiveTrigger(ManagedCard trigger)
        {
            if (trigger?.Transform == null)
            {
                return false;
            }

            if (trigger.IsFieldDead || trigger.DisplayMode == CardDisplayMode.RemovedMode)
            {
                return false;
            }

            return trigger.Transform.position.y > UnusableOriginYThreshold;
        }

        /// <summary>
        /// 解析炸开/洗入世界原点。回调由调用方注入，便于 EditMode 单测。
        /// </summary>
        /// <param name="resolveBoardSlotWorld">FromBoardSlot → 格锚世界坐标（勿返回 staged 尸体位）。</param>
        /// <param name="tryGetLiveTrigger">TriggerCardUid → 仍可用的 live 触发卡；不可用时返回 null。</param>
        /// <param name="resolveFallbackCardWorld">Trigger 已死时的次级坐标（仍可能是尸体位，调用方应少依赖）。</param>
        public static bool TryResolveWorld(
            ShuffleIntoDeckPresentationEntry entry,
            Func<int, Vector3?> resolveBoardSlotWorld,
            Func<int, ManagedCard> tryGetLiveTrigger,
            Func<int, Vector3?> resolveFallbackCardWorld,
            out Vector3 world)
        {
            world = default;

            if (entry.FromBoardSlot > 0 && resolveBoardSlotWorld != null)
            {
                var slotPos = resolveBoardSlotWorld(entry.FromBoardSlot);
                if (slotPos.HasValue && slotPos.Value.y > UnusableOriginYThreshold)
                {
                    world = slotPos.Value;
                    return true;
                }
            }

            if (entry.TriggerCardUid > 0 && tryGetLiveTrigger != null)
            {
                var trigger = tryGetLiveTrigger(entry.TriggerCardUid);
                if (IsUsableLiveTrigger(trigger))
                {
                    world = trigger.Transform.position;
                    return true;
                }
            }

            if (entry.TriggerCardUid > 0 && resolveFallbackCardWorld != null)
            {
                var cardPos = resolveFallbackCardWorld(entry.TriggerCardUid);
                if (cardPos.HasValue && cardPos.Value.y > UnusableOriginYThreshold)
                {
                    world = cardPos.Value;
                    return true;
                }
            }

            return false;
        }
    }
}
