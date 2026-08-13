using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Flow.BoardBriefTip;
using NineGrid.Flow.Diagnostics;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Flow
{
    internal sealed partial class BattleSessionExecutor
    {
        public async UniTask PresentRewardChoiceFromCoreAsync(bool hoverOnNotice = false)
        {
            var selector = Selector ?? UnityEngine.Object.FindFirstObjectByType<SelectorManagerSingleton>();
            if (selector == null)
            {
                Debug.LogWarning("[BattleSession] 奖励相位但无 SelectorManager。");
                return;
            }

            var arch = NineGridArchitecture.Current;
            var pending = arch.GetModel<PendingChoiceModel>();
            var options = pending.RewardOptions;
            if (options == null || options.Count == 0)
            {
                Debug.LogWarning("[BattleSession] 奖励选项为空。");
                return;
            }

            var defIds = new string[options.Count];
            for (var i = 0; i < options.Count; i++)
            {
                defIds[i] = options[i].DefId;
            }

            AbortBoardSelectIfActive("reward-choice");
            PresentationInputGates.SetChoiceOverlay(true);
            var dimmerHeld = BattleUiDimmerOverlay.TryAcquire("reward-choice");
            try
            {
                var pipeline = arch.GetSystem<IActionPipelineSystem>();
                var phaseSystem = arch.GetSystem<IPhaseSystem>();
                CoreCommandResult result;
                string chosenDefId;
                string choiceKind;
                int pickIndex;
                int startIndex;
                string phaseBefore = phaseSystem.CurrentPhase.ToString();

                // 点选即推进 Core；若 Intake/门禁拒收则重开 Bounce，避免孤儿 Pending。
                while (true)
                {
                    var pick = await WaitBouncePickAsync(
                        selector,
                        defIds,
                        allowEscapeSkip: true,
                        hoverOnNotice: hoverOnNotice);
                    if (pick.Cancelled)
                    {
                        return;
                    }

                    startIndex = pipeline.EventLog.Entries.Count;
                    phaseBefore = phaseSystem.CurrentPhase.ToString();
                    pickIndex = pick.Index;
                    if (pick.SkipRequested || pick.Index < 0)
                    {
                        chosenDefId = string.Empty;
                        choiceKind = "skip";
                        result = SubmitSkipHelpChoice(phaseSystem);
                        if (!result.Accepted)
                        {
                            Debug.LogWarning($"[BattleSession] SkipHelpChoice 被拒: {result.Reason}");
                            RecordRewardChosenFlow(
                                choiceKind,
                                chosenDefId,
                                -1,
                                phaseBefore,
                                phaseSystem.CurrentPhase.ToString(),
                                accepted: false,
                                reason: result.Reason);
                            continue;
                        }

                        break;
                    }

                    chosenDefId = pick.Index >= 0 && pick.Index < defIds.Length
                        ? defIds[pick.Index]
                        : string.Empty;
                    choiceKind = "select";
                    result = SubmitSelectReward(phaseSystem, pick.Index);
                    if (!result.Accepted)
                    {
                        Debug.LogWarning($"[BattleSession] SelectReward 被拒: {result.Reason}");
                        if (!string.IsNullOrEmpty(result.Reason))
                        {
                            BoardBriefTipPresenter.EnsureExists().ShowNotice(result.Reason);
                        }

                        RecordRewardChosenFlow(
                            choiceKind,
                            chosenDefId,
                            pick.Index,
                            phaseBefore,
                            phaseSystem.CurrentPhase.ToString(),
                            accepted: false,
                            reason: result.Reason);
                        continue;
                    }

                    break;
                }

                RecordRewardChosenFlow(
                    choiceKind,
                    chosenDefId,
                    pickIndex,
                    phaseBefore,
                    phaseSystem.CurrentPhase.ToString(),
                    accepted: true,
                    reason: string.Empty);

                // 选完关闭覆盖层；Drain 期间由导演主线租约挡输入（外层已持锁则复用）。
                var acquiredDrainLock = PresentationInputGates.TryBeginExternalHold("RewardDrain");
                PresentationInputGates.SetChoiceOverlay(false);
                if (dimmerHeld)
                {
                    BattleUiDimmerOverlay.Release("reward-choice");
                    dimmerHeld = false;
                }
                try
                {
                    CoreBatchProjectionCoordinator.FillBoardDeltaFromEventLog(pipeline, startIndex, out var moves, out var deals, out _, out var removedUids, out var rewardSteps);
                    var phase = phaseSystem.CurrentPhase;
                    var boardDelta = new PostKillBoardPresentationResult
                    {
                        Accepted = true,
                        Steps = rewardSteps ?? Array.Empty<BoardPresentationStep>(),
                        Moves = moves ?? Array.Empty<PostKillCardMove>(),
                        Deals = deals ?? Array.Empty<PostKillCardDeal>(),
                        RemovedUids = removedUids ?? Array.Empty<int>(),
                        DamagePopups = Array.Empty<CombatDamagePopup>(),
                        NodeClearedOrRewardPhase = NodeSettlementReadiness.IsPostClearPhase(
                            phase,
                            arch.GetSystem<IDeckSystem>().IsNodeCleared()),
                        AvatarDefeated = phase == GamePhase.Defeat,
                    };

                    var hasBoardDrain =
                        (boardDelta.Steps != null && boardDelta.Steps.Length > 0)
                        || (boardDelta.Moves != null && boardDelta.Moves.Length > 0)
                        || (boardDelta.Deals != null && boardDelta.Deals.Length > 0)
                        || (boardDelta.RemovedUids != null && boardDelta.RemovedUids.Length > 0);

                    if (hasBoardDrain)
                    {
                        // ADR-0050 补记「先开批后 Drain」：与拾取切片同构——OpenBatch（打击计划/暂扣）
                        // → Drain 前冲非触发 Impact（ADR-0018 保飘字坐标）→ Drain（含移除打击）
                        // → 触发脉冲 → 剩余打击 → 其余 Impact/Settled。
                        await BattleBeatFlush.PresentEventLogSliceWithStrikesAsync(
                            NineGridArchitecture.Current,
                            startIndex,
                            async _ =>
                            {
                                BoardPlayer.PresentShuffleIntoDeckFromEventLog(startIndex);
                                await DrainPostKillBoardAsync(boardDelta, EnsurePresentationToken());
                            },
                            flushNonTriggerImpactBeforeDrain: true);
                    }
                    else
                    {
                        BattleBeatFlush.PresentEventLogSlice(NineGridArchitecture.Current, startIndex);
                        BoardPlayer.PresentShuffleIntoDeckFromEventLog(startIndex);
                        // 空 delta：对账已提交投影（不直读最新 Core 抢刷卡面）。
                        CoreCardPresentationMapper.SyncAllSpawnedCards();
                        // #10：空 delta 不再 soft Sync。
                    }

                    RefreshPersistentInBattleUi(animate: false);

                    if (boardDelta.AvatarDefeated)
                    {
                        EnsureBattleEndedIfAvatarDefeated(
                            boardDelta, EnsurePresentationToken());
                        return;
                    }

                    // 整局通关：回主菜单。RoomChoice / NodeCompleted 交主循环继续，不在此宣告胜利。
                    if (phase == GamePhase.Victory)
                    {
                        RaiseBattleEnded(victory: true);
                    }
                }
                finally
                {
                    if (acquiredDrainLock)
                    {
                        PresentationInputGates.EndExternalHold("RewardDrain");
                    }
                }
            }
            finally
            {
                PresentationInputGates.SetChoiceOverlay(false);
                if (dimmerHeld)
                {
                    BattleUiDimmerOverlay.Release("reward-choice");
                }
            }
        }

        private static CoreCommandResult SubmitSelectReward(IPhaseSystem phaseSystem, int optionIndex)
        {
            EnsureRewardChoiceHookWired();
            if (RewardChoiceCoreHook.SelectReward != null)
            {
                return RewardChoiceCoreHook.SelectReward(optionIndex);
            }

            return phaseSystem.SelectReward(optionIndex);
        }

        private static CoreCommandResult SubmitSkipHelpChoice(IPhaseSystem phaseSystem)
        {
            EnsureRewardChoiceHookWired();
            if (RewardChoiceCoreHook.SkipHelpChoice != null)
            {
                return RewardChoiceCoreHook.SkipHelpChoice();
            }

            return phaseSystem.SkipHelpChoice();
        }

        private static void RecordRewardChosenFlow(
            string choiceKind,
            string chosenDefId,
            int index,
            string phaseBefore,
            string phaseAfter,
            bool accepted,
            string reason)
        {
            try
            {
                FlowTraceRecorder.Record(
                    FlowTraceCategory.CoreGate,
                    FlowTraceNames.RewardChosen,
                    new Dictionary<string, string>
                    {
                        { "kind", choiceKind ?? string.Empty },
                        { "defId", chosenDefId ?? string.Empty },
                        { "index", index.ToString() },
                        { "reason", reason ?? string.Empty },
                    },
                    phaseBefore: phaseBefore,
                    phaseAfter: phaseAfter,
                    accepted: accepted);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BattleSession] FlowTrace RewardChosen: " + ex.Message);
            }
        }

        private readonly struct BouncePickResult
        {
            public readonly int Index;
            public readonly bool SkipRequested;
            public readonly bool Cancelled;

            public BouncePickResult(int index, bool skipRequested, bool cancelled)
            {
                Index = index;
                SkipRequested = skipRequested;
                Cancelled = cancelled;
            }
        }

        /// <summary>
        /// 等玩家点选（onPicked），不等退场动画（onFinished），避免 fallDuration≈5s 锁死输入。
        /// </summary>
        private static async UniTask<BouncePickResult> WaitBouncePickAsync(
            SelectorManagerSingleton selector,
            IReadOnlyList<string> optionDefIds,
            bool allowEscapeSkip,
            bool hoverOnNotice = false)
        {
            var picked = -1;
            var pickedDone = false;
            selector.BeginBounceChoice(
                optionDefIds,
                (index, _) =>
                {
                    picked = index;
                    pickedDone = true;
                },
                onFinished: null,
                hoverOnNotice: hoverOnNotice);

            while (!pickedDone)
            {
                if (allowEscapeSkip && KeyboardUtility.GetKeyDown(KeyCode.Escape))
                {
                    selector.HideChoice();
                    return new BouncePickResult(-1, skipRequested: true, cancelled: false);
                }

                await UniTask.Yield();
            }

            return new BouncePickResult(picked, skipRequested: false, cancelled: false);
        }

        private const string UnusedHelpCardsGoldReason = "unusedHelpCards";
        private const float UnusedHelpCardVanishDuration = 0.18f;
        private const float UnusedHelpCardStaggerSeconds = 0.07f;

        /// <summary>
        /// 对局结束（通关判定）当拍启动的残留帮助卡结算演出：场上/手牌/卡组中的剩余帮助卡
        /// 逐张错峰并行退场并飞币，不阻塞主循环推进奖励三选一/房间选择。
        /// 结算集合在进入时快照，之后（奖励三选一）新获得的帮助卡视图不会被误清。
        /// </summary>
        public async UniTask PresentUnusedHelpCardSettlementFromEventLogAsync(
            int startIndex,
            CancellationToken cancellationToken = default)
        {
                        var arch = NineGridArchitecture.Current;
            if (arch == null)
            {
                return;
            }

            var entries = arch.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            var unusedDelta = 0;
            var unusedAfter = 0;
            if (entries != null)
            {
                for (var i = Math.Max(0, startIndex); i < entries.Count; i++)
                {
                    var e = entries[i];
                    if (e.Type != CoreEventType.GoldModified
                        || e.Delta <= 0
                        || !string.Equals(e.Message, UnusedHelpCardsGoldReason, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    unusedDelta = e.Delta;
                    unusedAfter = e.Amount;
                    break;
                }
            }

            // 快照本拍应结算的帮助卡（内核区域 + 表现侧场牌/牌堆/拖拽视图；不含道具卡格手牌）。
            var settledUids = CollectUnusedHelpCardUidsFromCore();
            AppendPresentationHelpCardUids(settledUids);
            var sweepSet = new HashSet<int>(settledUids);
            if (Cards != null)
            {
                foreach (var card in Cards.EnumerateCards())
                {
                    if (card != null
                        && IsPresentationHelpCard(card)
                        && card.DisplayMode != CardDisplayMode.RemovedMode
                        && card.DisplayMode != CardDisplayMode.HandCardMode)
                    {
                        sweepSet.Add(card.Uid);
                    }
                }
            }

            if (unusedDelta > 0)
            {
                await PresentUnusedHelpCardsToGoldAsync(
                    settledUids,
                    unusedDelta,
                    unusedAfter,
                    cancellationToken);
                GoldGainPresentationBinder.RecordGoldChangedFlow(
                    FlowTraceNames.GoldGained,
                    unusedDelta,
                    unusedAfter,
                    UnusedHelpCardsGoldReason,
                    string.Empty,
                    "ModifyGold");
            }

            // 兜底清掉快照集合中仍存活的帮助卡视图（不含道具卡格），避免下一关前幽灵牌。
            await SweepRemainingHelpCardViewsAsync(sweepSet, cancellationToken);
        }

        private async UniTask PresentUnusedHelpCardsToGoldAsync(
            List<int> uids,
            int totalDelta,
            int amountAfter,
            CancellationToken cancellationToken)
        {
            var goldPerCard = ResolveUnusedHelpCardGoldPerCard();
            if (goldPerCard <= 0 && uids.Count > 0)
            {
                goldPerCard = Math.Max(1, totalDelta / uids.Count);
            }

            var snapshots = new List<(ManagedCard card, Vector3 origin)>(uids.Count);
            if (Cards != null)
            {
                for (var i = 0; i < uids.Count; i++)
                {
                    if (!Cards.TryGet(uids[i], out var card)
                        || card?.Transform == null
                        || card.DisplayMode == CardDisplayMode.RemovedMode)
                    {
                        continue;
                    }

                    snapshots.Add((card, card.Transform.position));
                }
            }

            // 卸占位后并行退场飞币；道具卡格（手牌）按 ADR-0025 保留，不 ClearHand。
            for (var i = 0; i < snapshots.Count; i++)
            {
                var card = snapshots[i].card;
                Field?.TryClearOccupancyForUid(card.Uid, skipBusyGuard: true);
                Deck?.TryDetachByUid(card.Uid, out _);
            }

            var before = Math.Max(0, amountAfter - totalDelta);
            var credited = 0;

            // 并行结算：每张卡仅错峰起始（轻微先后顺序），不再串行等待上一张退场完成。
            var tasks = new List<UniTask>(snapshots.Count);
            for (var i = 0; i < snapshots.Count; i++)
            {
                var (card, origin) = snapshots[i];
                var remaining = totalDelta - credited;
                var slice = remaining <= 0
                    ? 0
                    : Math.Min(goldPerCard > 0 ? goldPerCard : remaining, remaining);
                credited += slice;
                tasks.Add(VanishHelpCardWithGoldAsync(
                    card,
                    origin,
                    slice,
                    before + credited,
                    UnusedHelpCardStaggerSeconds * i,
                    cancellationToken));
            }

            await UniTask.WhenAll(tasks);

            if (credited < totalDelta)
            {
                GoldGainPresentationBinder.PresentGainVisual(
                    totalDelta - credited,
                    amountAfter,
                    ResolveDeckGoldOriginWorld());
            }
        }

        private async UniTask VanishHelpCardWithGoldAsync(
            ManagedCard card,
            Vector3 origin,
            int goldSlice,
            int goldTargetAfterSlice,
            float startDelaySeconds,
            CancellationToken cancellationToken)
        {
            if (startDelaySeconds > 0f)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(startDelaySeconds),
                    cancellationToken: cancellationToken);
            }

            if (goldSlice > 0)
            {
                // 保留卸视图前 origin 快照，改发 gold-flight VFX（#199）。
                GoldGainPresentationBinder.PresentGainVisual(goldSlice, goldTargetAfterSlice, origin);
            }

            await VanishAndReleaseHelpCardAsync(card, cancellationToken);
        }

        private async UniTask SweepRemainingHelpCardViewsAsync(
            HashSet<int> restrictToUids,
            CancellationToken cancellationToken)
        {
            // 道具卡格（手牌）跨清关保留；只清结算快照内的场牌/牌堆等视图。
            if (Cards == null)
            {
                return;
            }

            var leftovers = new List<ManagedCard>();
            foreach (var card in Cards.EnumerateCards())
            {
                if (card == null || !IsPresentationHelpCard(card))
                {
                    continue;
                }

                if (card.DisplayMode == CardDisplayMode.RemovedMode
                    || card.DisplayMode == CardDisplayMode.HandCardMode
                    || card.Transform == null)
                {
                    continue;
                }

                // 只清结算快照内的卡：奖励三选一新获得的帮助卡视图不受影响。
                if (restrictToUids != null && !restrictToUids.Contains(card.Uid))
                {
                    continue;
                }

                leftovers.Add(card);
            }

            for (var i = 0; i < leftovers.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var card = leftovers[i];
                Field?.TryClearOccupancyForUid(card.Uid, skipBusyGuard: true);
                Deck?.TryDetachByUid(card.Uid, out _);
                await VanishAndReleaseHelpCardAsync(card, cancellationToken);
            }
        }

        private async UniTask VanishAndReleaseHelpCardAsync(
            ManagedCard card,
            CancellationToken cancellationToken)
        {
            if (card == null)
            {
                return;
            }

            var uid = card.Uid;
            if (Cards == null || !Cards.TryGet(uid, out card))
            {
                return;
            }

            if (card.Transform == null)
            {
                Cards.Release(uid, "HelpCard.VanishNoTransform");
                return;
            }

            Cards?.SetDisplayMode(card, CardDisplayMode.RemovedMode);
            CardDeckTween.KillMotion(card.Transform);

            var transform = card.Transform;
            Tween tween = null;
            try
            {
                var completed = false;
                tween = transform
                    .DOScale(Vector3.zero, UnusedHelpCardVanishDuration)
                    .SetEase(Ease.InBack)
                    .SetLink(transform.gameObject, LinkBehaviour.KillOnDestroy)
                    .OnComplete(() => completed = true)
                    .OnKill(() => completed = true);

                while (!completed)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // 取消路径仍 Release，避免幽灵视图。
            }
            finally
            {
                if (tween != null && tween.IsActive())
                {
                    tween.Kill();
                }

                CardOpacityUtility.ClearCache(uid);
                Cards?.Release(uid, "HelpCard.VanishComplete");
            }
        }

        private static List<int> CollectUnusedHelpCardUidsFromCore()
        {
            var result = new List<int>();
            var arch = NineGridArchitecture.Current;
            if (arch == null)
            {
                return result;
            }

            var registry = arch.GetModel<CardRegistry>();
            var board = arch.GetModel<BoardModel>();
            var deck = arch.GetModel<DeckModel>();
            var seen = new HashSet<int>();

            AddHelpCardUids(registry, deck.DrawPileUids, seen, result);
            AddHelpCardUids(registry, deck.PlayerCardPoolUids, seen, result);
            // ADR-0025 / #107：道具卡格不参与清关兑金演出。
            foreach (var uid in board.BoardCardUids())
            {
                AddHelpCardUid(registry, uid, seen, result);
            }

            return result;
        }

        private void AppendPresentationHelpCardUids(List<int> uids)
        {
            if (uids == null || Cards == null)
            {
                return;
            }

            var seen = new HashSet<int>(uids);
            foreach (var card in Cards.EnumerateCards())
            {
                if (card == null || !IsPresentationHelpCard(card) || !seen.Add(card.Uid))
                {
                    continue;
                }

                // 手牌即道具卡格视图，清关保留。
                if (card.DisplayMode == CardDisplayMode.GroundCardMode
                    || card.DisplayMode == CardDisplayMode.CardDeckMode
                    || card.DisplayMode == CardDisplayMode.DragCardMode)
                {
                    uids.Add(card.Uid);
                }
            }
        }

        private static void AddHelpCardUids(
            CardRegistry registry,
            IReadOnlyList<int> source,
            HashSet<int> seen,
            List<int> result)
        {
            if (source == null)
            {
                return;
            }

            for (var i = 0; i < source.Count; i++)
            {
                AddHelpCardUid(registry, source[i], seen, result);
            }
        }

        private static void AddHelpCardUid(
            CardRegistry registry,
            int uid,
            HashSet<int> seen,
            List<int> result)
        {
            if (uid == 0 || registry == null || !seen.Add(uid))
            {
                return;
            }

            if (!registry.TryGet(uid, out var card) || card.Kind != CardKind.HelpCard)
            {
                seen.Remove(uid);
                return;
            }

            result.Add(uid);
        }

        private static bool IsPresentationHelpCard(ManagedCard card)
        {
            if (card == null)
            {
                return false;
            }

            if (card.CoreKind == CardPresentationKind.HelpCard)
            {
                return true;
            }

            return !string.IsNullOrEmpty(card.DefId)
                && card.DefId.StartsWith("help.", StringComparison.OrdinalIgnoreCase);
        }

        private static int ResolveUnusedHelpCardGoldPerCard()
        {
            var arch = NineGridArchitecture.Current;
            if (arch == null)
            {
                return 0;
            }

            var content = arch.GetSystem<IContentSystem>();
            content?.TryReloadFromConfig();
            if (content == null || !content.HasCatalog)
            {
                return 0;
            }

            return Math.Max(0, content.Catalog.Economy.UnusedHelpCardGold);
        }

        private Vector3? ResolveDeckGoldOriginWorld()
        {
            if (Cards == null)
            {
                return null;
            }

            foreach (var card in Cards.EnumerateCards())
            {
                if (card?.Transform != null && card.DisplayMode == CardDisplayMode.CardDeckMode)
                {
                    return card.Transform.position;
                }
            }

            return null;
        }

    }
}
