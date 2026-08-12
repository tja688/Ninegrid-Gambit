using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Flow.Tutorial
{
    /// <summary>
    /// 教学关卡导演：主线空闲时巡检 Core 状态，推进四波受控发牌。
    /// 波次切换作为真时间线脚本挂主线（清场批 → 补发批 → 盘面稳定化），
    /// 复用既有 Resolve/Present/ack 锁步与发牌表演；切换期间输入互斥自然生效。
    /// </summary>
    /// <remarks>
    /// 波次剧本：
    /// 1. 八张教学机关卡铺场讲基础互动；击破两张 → 换波。
    /// 2. 七张机关 + 一只教学怪：讲攻击模式 / 行动计数 / 右键详情；击破怪 → 换波（机关无限补位）。
    /// 3. 五张机关 + 飞刀 + 恢复药水 + 练习靶怪：讲拾取与使用；两件道具都用掉 → 换波。
    /// 4. 七张机关 + 离开机关：讲清关 / 变卖 / 层主房；击破离开机关走 Core 正常清关收口。
    /// </remarks>
    public sealed class TutorialBattleDirector
    {
        private const int WaveOne = 1;
        private const int WaveTwo = 2;
        private const int WaveThree = 3;
        private const int WaveFour = 4;

        private readonly IArchitecture mArchitecture;
        private readonly IBattleSessionSystem mSession;
        private readonly CoreCommandDispatcher mDispatcher;
        private readonly IPresentChannel mBoardChannel;
        private readonly BoardStabilizationScheduler mStabilization = new BoardStabilizationScheduler();
        private readonly HashSet<int> mWaveUids = new HashSet<int>();

        private int mWave = WaveOne;
        private bool mWaveScanned;
        private bool mTransitionQueued;
        private int mMonsterUid;
        private int mKnifeUid;
        private int mPotionUid;
        private CancellationTokenSource mCts;

        private TutorialBattleDirector(IArchitecture architecture)
        {
            mArchitecture = architecture;
            mSession = BattleSessionSystem.EnsureRegistered(architecture);
            // 独立批号段，避免与生产 Dispatcher 撞号（sync 同时只开一批，纯保险）。
            mDispatcher = new CoreCommandDispatcher(architecture, 700_000);
            mBoardChannel = new QueuedBoardPresentChannel(
                (result, token) => mSession.DrainPostKillBoardAsync(result, token),
                mSession.EnsurePresentationToken);
        }

        public static TutorialBattleDirector StartNew(CancellationToken ct)
        {
            var architecture = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            var director = new TutorialBattleDirector(architecture);
            director.mCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            director.RunAsync(director.mCts.Token).Forget();
            return director;
        }

        public void Stop()
        {
            if (mCts == null)
            {
                return;
            }

            mCts.Cancel();
            mCts.Dispose();
            mCts = null;
        }

        private async UniTaskVoid RunAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    Tick();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogError("[Tutorial] 教学导演异常：" + ex);
            }
        }

        private void Tick()
        {
            var phase = mArchitecture.GetSystem<IPhaseSystem>();
            if (phase == null || phase.CurrentPhase != GamePhase.InteractionLoop)
            {
                return;
            }

            var runtime = mArchitecture.GetSystem<IPresentationRuntimeSystem>();
            var sync = mArchitecture.GetSystem<IPresentationSyncSystem>();
            if (runtime == null
                || sync == null
                || runtime.MainlineBusy.Value
                || sync.ActiveBatchId > 0
                || mTransitionQueued)
            {
                return;
            }

            if (!mWaveScanned)
            {
                ScanWave();
            }

            switch (mWave)
            {
                case WaveOne:
                    if (CountDeadWaveCards() >= 2)
                    {
                        EnqueueWaveTransition(WaveTwo);
                    }

                    break;
                case WaveTwo:
                    if (IsDeadOrGone(mMonsterUid))
                    {
                        EnqueueWaveTransition(WaveThree);
                        break;
                    }

                    EnsureFillerStock();
                    break;
                case WaveThree:
                    if (IsDeadOrGone(mKnifeUid) && IsDeadOrGone(mPotionUid))
                    {
                        EnqueueWaveTransition(WaveFour);
                        break;
                    }

                    EnsureFillerStock();
                    EnsureKnifeTargetAvailable();
                    break;
                case WaveFour:
                    // 击破离开机关 → Core 正常清关收口；导演无事可做。
                    break;
            }
        }

        /// <summary>波次发牌落定后：登记本波教学卡 uid 与特殊卡（怪物 / 飞刀 / 药水）。</summary>
        private void ScanWave()
        {
            mWaveUids.Clear();
            mMonsterUid = 0;
            if (mWave == WaveOne)
            {
                mKnifeUid = 0;
                mPotionUid = 0;
            }

            var board = mArchitecture.GetModel<BoardModel>();
            var deck = mArchitecture.GetModel<DeckModel>();
            var registry = mArchitecture.GetModel<CardRegistry>();

            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                RegisterWaveCard(registry, board.GetCardUid(SlotId.Board(i)));
            }

            var pile = deck.DrawPileUids;
            for (var i = 0; i < pile.Count; i++)
            {
                RegisterWaveCard(registry, pile[i]);
            }

            mWaveScanned = true;
            Debug.Log(
                $"[Tutorial] 第{mWave}波就位：教学卡 {mWaveUids.Count} 张"
                + (mMonsterUid > 0 ? $" 怪物#{mMonsterUid}" : string.Empty)
                + (mKnifeUid > 0 ? $" 飞刀#{mKnifeUid}" : string.Empty)
                + (mPotionUid > 0 ? $" 药水#{mPotionUid}" : string.Empty));
        }

        private void RegisterWaveCard(CardRegistry registry, int uid)
        {
            if (uid <= 0 || !registry.TryGet(uid, out var card) || card == null)
            {
                return;
            }

            var defId = card.DefId ?? string.Empty;
            if (defId.StartsWith(TutorialDeckPlan.TutorialTrapPrefix, StringComparison.Ordinal)
                || defId == TutorialDeckPlan.LeaveTrapDefId)
            {
                mWaveUids.Add(uid);
                return;
            }

            if (defId == TutorialDeckPlan.MonsterDefId)
            {
                mWaveUids.Add(uid);
                if (!IsDeadOrGone(uid))
                {
                    mMonsterUid = uid;
                }

                return;
            }

            if (defId == TutorialDeckPlan.KnifeDefId)
            {
                mKnifeUid = uid;
                return;
            }

            if (defId == TutorialDeckPlan.PotionDefId)
            {
                mPotionUid = uid;
            }
        }

        private int CountDeadWaveCards()
        {
            var count = 0;
            foreach (var uid in mWaveUids)
            {
                if (IsDeadOrGone(uid))
                {
                    count++;
                }
            }

            return count;
        }

        private bool IsDeadOrGone(int uid)
        {
            if (uid <= 0)
            {
                return true;
            }

            var registry = mArchitecture.GetModel<CardRegistry>();
            if (!registry.TryGet(uid, out var card) || card == null)
            {
                return true;
            }

            var zone = card.Zone.Value;
            return zone == ZoneId.Graveyard || zone == ZoneId.Removed;
        }

        /// <summary>第二 / 三波：机关无限补位——抽牌堆空了就补一张教学补位机关。</summary>
        private void EnsureFillerStock()
        {
            var deck = mArchitecture.GetModel<DeckModel>();
            if (deck.DrawPileUids.Count > 0)
            {
                return;
            }

            InjectTopUnlockstep(TutorialDeckPlan.FillerDefId, CardKind.Trap, "tutorialRefill");
        }

        /// <summary>第三波：飞刀还没用而场上/牌堆都没活怪时，补一只练习靶，避免飞刀无目标卡死。</summary>
        private void EnsureKnifeTargetAvailable()
        {
            if (IsDeadOrGone(mKnifeUid))
            {
                return;
            }

            if (FindAliveMonster() > 0)
            {
                return;
            }

            InjectTopUnlockstep(TutorialDeckPlan.MonsterDefId, CardKind.Monster, "tutorialKnifeTarget");
        }

        private int FindAliveMonster()
        {
            var board = mArchitecture.GetModel<BoardModel>();
            var deck = mArchitecture.GetModel<DeckModel>();
            var registry = mArchitecture.GetModel<CardRegistry>();

            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var uid = board.GetCardUid(SlotId.Board(i));
                if (IsAliveMonster(registry, uid))
                {
                    return uid;
                }
            }

            var pile = deck.DrawPileUids;
            for (var i = 0; i < pile.Count; i++)
            {
                if (IsAliveMonster(registry, pile[i]))
                {
                    return pile[i];
                }
            }

            return 0;
        }

        private bool IsAliveMonster(CardRegistry registry, int uid)
        {
            return uid > 0
                   && registry.TryGet(uid, out var card)
                   && card != null
                   && card.Kind == CardKind.Monster
                   && !IsDeadOrGone(uid);
        }

        /// <summary>非锁步顶插一张卡（对齐作弊面板战斗加卡范式：直跑管线 + 事件切片冲刷）。</summary>
        private void InjectTopUnlockstep(string defId, CardKind kind, string cause)
        {
            var pipeline = mArchitecture.GetSystem<IActionPipelineSystem>();
            if (pipeline?.EventLog == null)
            {
                return;
            }

            var start = pipeline.EventLog.Entries.Count;
            pipeline.Enqueue(new ShuffleIntoDrawPileAction(defId, kind, 1, top: true, cause: cause));
            pipeline.RunToCompletion();
            BattleBeatFlush.PresentEventLogSlice(mArchitecture, start);
            Debug.Log($"[Tutorial] 顶插 {defId}（{cause}）");
        }

        /// <summary>
        /// 换波脚本挂主线：清场批（Present 移除）→ 补发批（Present 洗入）→ 盘面稳定化（真发牌表演）。
        /// </summary>
        private void EnqueueWaveTransition(int nextWave)
        {
            var runtime = mArchitecture.GetSystem<IPresentationRuntimeSystem>();
            var sync = mArchitecture.GetSystem<IPresentationSyncSystem>();
            if (runtime == null || sync == null)
            {
                return;
            }

            mTransitionQueued = true;
            mWave = nextWave;
            mWaveScanned = false;
            var pileOrder = TutorialDeckPlan.GetWavePileOrder(nextWave);
            Debug.Log($"[Tutorial] 换波 → 第{nextWave}波（补发 {pileOrder.Count} 张）");

            runtime.MutateMainline(timeline =>
            {
                var clearGate = PresentationSyncBatchGate.FromSync(
                    sync,
                    () => ResolveAndProject(() => mDispatcher.Send(new TutorialClearWaveCommand())),
                    slice: "TutorialWaveClear");
                timeline.Enqueue(new ResolveBatchStep(clearGate));
                timeline.Enqueue(new PresentStep(clearGate, mBoardChannel, channelName: "TutorialWaveClear"));

                var injectGate = PresentationSyncBatchGate.FromSync(
                    sync,
                    () => ResolveAndProject(() => mDispatcher.Send(new TutorialInjectWaveCommand(pileOrder))),
                    slice: "TutorialWaveInject");
                timeline.Enqueue(new ResolveBatchStep(injectGate));
                timeline.Enqueue(new PresentStep(injectGate, mBoardChannel, channelName: "TutorialWaveInject"));

                mStabilization.Append(
                    timeline,
                    mArchitecture,
                    mDispatcher,
                    mBoardChannel,
                    ResolveAvatarSlotIndex(),
                    mSession.OnExploreBatchProjected,
                    onStable: _ => mTransitionQueued = false);
            });
        }

        private CoreCommandDispatchResult ResolveAndProject(Func<CoreCommandDispatchResult> resolve)
        {
            var pipeline = mArchitecture.GetSystem<IActionPipelineSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            var dispatch = resolve();
            if (dispatch == null || !dispatch.Accepted)
            {
                return dispatch;
            }

            mSession.OnExploreBatchProjected(
                startIndex,
                ResolveAvatarSlotIndex(),
                IntentBatchProjection.Build(mArchitecture, pipeline, startIndex));
            return dispatch;
        }

        private int ResolveAvatarSlotIndex()
        {
            var board = mArchitecture.GetModel<BoardModel>();
            var slot = board.AvatarSlot.Value;
            return slot.IsBoardSlot ? slot.Index : 5;
        }
    }
}
