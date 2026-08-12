using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Flow.Tutorial
{
    /// <summary>
    /// 教学关卡导演：逐帧巡检 Core 状态，推进四波受控发牌。
    /// 波次切换作为真时间线脚本挂主线（清场批 → 补发批 → 盘面稳定化），
    /// 复用既有 Resolve/Present/ack 锁步与发牌表演。
    /// 换波条件一满足（哪怕击杀表演还在播）即挂 Opening 输入门，
    /// 直到新波发牌表演完成、ScanWave 登记完毕才解锁——堵住
    /// 「表演收尾 → 换波脚本入队」之间的玩家输入窗口。
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
        private readonly QueuedBoardPresentChannel mBoardChannel;
        private readonly BoardStabilizationScheduler mStabilization = new BoardStabilizationScheduler();
        private readonly HashSet<int> mWaveUids = new HashSet<int>();

        private int mWave = WaveOne;
        private bool mWaveScanned;
        private bool mTransitionQueued;
        private bool mInputHeld;
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
            ReleasePlayerInput();
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
                // 清关 / 战败等相位切换后由后续流程接管输入，不粘住教学输入门。
                ReleasePlayerInput();
                return;
            }

            var runtime = mArchitecture.GetSystem<IPresentationRuntimeSystem>();
            var sync = mArchitecture.GetSystem<IPresentationSyncSystem>();
            if (runtime == null || sync == null || mTransitionQueued)
            {
                return;
            }

            // 换波条件按 Core 真相先行判定（击杀批一解算即为真，无需等表演收尾）：
            // 满足即挂输入门，再等主线与批次真正空闲后把换波脚本挂上主线。
            if (mWaveScanned)
            {
                var nextWave = NextWaveIfCompleted();
                if (nextWave > 0)
                {
                    HoldPlayerInput();
                    if (!runtime.MainlineBusy.Value && sync.ActiveBatchId == 0)
                    {
                        EnqueueWaveTransition(nextWave);
                    }

                    return;
                }
            }

            if (runtime.MainlineBusy.Value || sync.ActiveBatchId > 0)
            {
                return;
            }

            if (!mWaveScanned)
            {
                ScanWave();
                // 新波登记完毕才把换波期间锁住的输入还给玩家。
                ReleasePlayerInput();
                return;
            }

            switch (mWave)
            {
                case WaveTwo:
                    EnsureFillerStock();
                    break;
                case WaveThree:
                    EnsureFillerStock();
                    EnsureKnifeTargetAvailable();
                    break;
            }
        }

        /// <summary>本波完成条件（Core 真相）：满足返回下一波号，否则 0。第四波走 Core 正常清关收口。</summary>
        private int NextWaveIfCompleted()
        {
            switch (mWave)
            {
                case WaveOne:
                    return CountDeadWaveCards() >= 2 ? WaveTwo : 0;
                case WaveTwo:
                    return IsDeadOrGone(mMonsterUid) ? WaveThree : 0;
                case WaveThree:
                    return IsDeadOrGone(mKnifeUid) && IsDeadOrGone(mPotionUid) ? WaveFour : 0;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// 换波期输入管控：复用 Opening 输入门（发牌表演期输入归 Opening，棋盘意图一律被拒）。
        /// 幂等重挂，防止外部 ResetGates 把门清掉。
        /// </summary>
        private void HoldPlayerInput()
        {
            if (!mInputHeld)
            {
                mInputHeld = true;
                Debug.Log("[Tutorial] 换波就绪：锁定玩家输入，等待表演完成。");
            }

            if (!PresentationInputGates.OpeningPresentationActive)
            {
                PresentationInputGates.SetOpening(true);
            }
        }

        private void ReleasePlayerInput()
        {
            if (!mInputHeld)
            {
                return;
            }

            mInputHeld = false;
            if (PresentationInputGates.OpeningPresentationActive)
            {
                PresentationInputGates.SetOpening(false);
            }

            Debug.Log("[Tutorial] 新波就位：解锁玩家输入。");
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
                    OnTutorialBatchProjected,
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

            OnTutorialBatchProjected(
                startIndex,
                ResolveAvatarSlotIndex(),
                IntentBatchProjection.Build(mArchitecture, pipeline, startIndex));
            return dispatch;
        }

        /// <summary>
        /// 投影必须投递导演私有通道——PresentStep 消费的就是它。
        /// 若走 mSession.OnExploreBatchProjected 会投进生产 Explore 通道，
        /// 无人消费 → 换波在 Core 落地但零表演（旧卡影子留场、新卡虚空发出）。
        /// </summary>
        private void OnTutorialBatchProjected(
            int startIndex,
            int boardSlot,
            PostKillBoardPresentationResult result)
        {
            mBoardChannel.Enqueue(result);
            // 补发批的「洗入卡组」飞行表演与生产链同构。
            mSession.PresentShuffleIntoDeckFromEventLog(startIndex);
        }

        private int ResolveAvatarSlotIndex()
        {
            var board = mArchitecture.GetModel<BoardModel>();
            var slot = board.AvatarSlot.Value;
            return slot.IsBoardSlot ? slot.Index : 5;
        }
    }
}
