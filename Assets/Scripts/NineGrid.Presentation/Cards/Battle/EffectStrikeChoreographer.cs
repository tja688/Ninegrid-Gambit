using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation;
using NineGrid.Presentation.Systems;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 效果打击编排器（ADR-0050）：场上卡造成的伤害/破坏统一走「攻击动作 + 受击反馈」串行表演。
    /// 开批时构建 <see cref="EffectStrikePlan"/> 并把效果伤害指令移入排期器打击暂扣区；
    /// 消费点有三：① Drain 中受击者/打击者退场呈现前（PlayStrikesInvolving）；
    /// ② PresentStep 触发脉冲节拍之后（PlayAllPendingStrikes）；③ 排期器 Settled 兜底放行。
    /// 打击者不可解析时降级为普通冲刷（飘字/血甲照常，不丢失）。
    /// </summary>
    public static class EffectStrikeChoreographer
    {
        private static EffectStrikePlan sPlan;

        public static void Reset()
        {
            sPlan = null;
        }

        /// <summary>开批重建打击计划，并把计划内的 Impact 伤害指令移入排期器暂扣区。</summary>
        public static void OnBatchOpened(PresentationBatch batch)
        {
            WarnDroppedUnplayedGroups(batch);
            sPlan = EffectStrikePlan.Build(batch, IsStrikerPresentable);
            if (sPlan.HeldInstructions.Count > 0)
            {
                var held = sPlan.HeldInstructions;
                BattleBeatHook.NotifyHoldStrikeImpactWhere(held.Contains);
            }

            if (sPlan.HasWork)
            {
                Debug.Log(
                    "[EffectStrike] 打击计划 batchId=" + sPlan.BatchId
                    + " groups=" + sPlan.Groups.Count
                    + " held=" + sPlan.HeldInstructions.Count);
            }
        }

        /// <summary>
        /// 上一批仍有未播打击组即被新批覆盖：打击动作已静默丢失（纯移除组连兜底冲刷都不会留痕）。
        /// Warning 级进 Console 抓取轨，出现即代表某条呈现路径漏掉了打击消费点。
        /// </summary>
        private static void WarnDroppedUnplayedGroups(PresentationBatch nextBatch)
        {
            var previous = sPlan;
            if (previous == null || !previous.HasWork)
            {
                return;
            }

            for (var i = 0; i < previous.Groups.Count; i++)
            {
                var group = previous.Groups[i];
                if (group.Played)
                {
                    continue;
                }

                Debug.LogWarning(
                    "[EffectStrike] 打击组未播即被新批覆盖（攻击动作丢失）"
                    + " previousBatchId=" + previous.BatchId
                    + " nextBatchId=" + (nextBatch != null ? nextBatch.BatchId : 0)
                    + " striker=" + group.StrikerUid
                    + " victim=" + group.VictimUid
                    + " source=" + group.SourceDefId
                    + " victimRemoved=" + group.VictimRemoved);
            }
        }

        /// <summary>播完当批所有未消费打击组（串行，逐个「打一遍」）。</summary>
        public static async UniTask PlayAllPendingStrikesAsync(CancellationToken cancellationToken)
        {
            var plan = sPlan;
            if (plan == null || !plan.HasWork)
            {
                return;
            }

            while (plan.TryGetNextUnplayed(out var group))
            {
                await PlayGroupAsync(group, cancellationToken);
            }
        }

        /// <summary>
        /// 播涉及指定卡的打击组（作为打击者或受击者），在该卡退场/被移除呈现之前调用。
        /// </summary>
        public static async UniTask PlayStrikesInvolvingAsync(int cardUid, CancellationToken cancellationToken)
        {
            var plan = sPlan;
            if (plan == null || !plan.HasWork || cardUid <= 0)
            {
                return;
            }

            var buffer = new List<EffectStrikeGroup>(2);
            plan.CollectUnplayedInvolving(cardUid, buffer);
            for (var i = 0; i < buffer.Count; i++)
            {
                await PlayGroupAsync(buffer[i], cancellationToken);
            }
        }

        private static async UniTask PlayGroupAsync(EffectStrikeGroup group, CancellationToken cancellationToken)
        {
            group.Played = true;
            var flushed = false;

            void FlushGroup()
            {
                if (flushed)
                {
                    return;
                }

                flushed = true;
                if (group.Instructions.Count > 0)
                {
                    BattleBeatHook.NotifyFlushStrikeHeldWhere(group.ContainsInstruction);
                }
            }

            try
            {
                var battle = NineGridArchitecture.Interface?.GetSystem<IFieldBattlePresentationSystem>();
                var played = false;
                if (battle != null)
                {
                    played = await battle.PlayEffectStrikePresentAsync(
                        group.StrikerUid,
                        group.VictimUid,
                        group.VictimRemoved,
                        FlushGroup,
                        cancellationToken);
                }

                if (!played)
                {
                    // Warning 级进 Console 抓取轨：打击动作未播出（参与者视图/占格缺失或 rig 不可用）。
                    Debug.LogWarning(
                        "[EffectStrike] 打击组降级为普通冲刷 striker=" + group.StrikerUid
                        + " victim=" + group.VictimUid
                        + " source=" + group.SourceDefId
                        + " victimRemoved=" + group.VictimRemoved
                        + " " + DescribeParticipants(group));
                }
            }
            catch (OperationCanceledException)
            {
                FlushGroup();
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[EffectStrike] 打击组表演失败，降级冲刷 striker=" + group.StrikerUid
                    + " victim=" + group.VictimUid
                    + ": " + ex.Message);
            }
            finally
            {
                // 命中帧未触达（rig 缺失 / 提前取消）也保证本组指令放行，飘字/血甲不丢失。
                FlushGroup();
            }
        }

        /// <summary>打击者须是当前占格的场上卡（含 Avatar）；离场来源（手牌道具等）不入组。</summary>
        private static bool IsStrikerPresentable(int uid)
        {
            if (uid <= 0)
            {
                return false;
            }

            var geometry = NineGridArchitecture.Interface?.GetSystem<IGroundFieldGeometrySystem>();
            if (geometry == null)
            {
                return false;
            }

            // 主索引 → 双向表短暂不一致扫描 → 补牌飞行中（本批新发卡随后落格）。
            return geometry.TryGetSlotOf(uid, out _)
                || geometry.TryFindOccupiedSlotForUid(uid, out _)
                || geometry.IsDealInFlight(uid);
        }

        /// <summary>降级诊断辅助：报告参与者视图/占格现状，便于定位打击丢失原因。</summary>
        private static string DescribeParticipants(EffectStrikeGroup group)
        {
            var cards = NineGridArchitecture.Interface?.GetSystem<ICardEntityLifecycleSystem>();
            var geometry = NineGridArchitecture.Interface?.GetSystem<IGroundFieldGeometrySystem>();
            string Describe(int uid)
            {
                if (cards == null || geometry == null)
                {
                    return "?";
                }

                var hasView = cards.TryGet(uid, out var card) && card != null && card.Transform != null;
                var onSlot = geometry.TryGetSlotOf(uid, out var slot);
                return (hasView ? "view" : "noView")
                    + "/" + (onSlot ? ("slot" + slot) : "noSlot")
                    + (hasView && card.IsFieldDead ? "/fieldDead" : string.Empty);
            }

            return "strikerState=" + Describe(group.StrikerUid)
                + " victimState=" + Describe(group.VictimUid);
        }
    }
}
