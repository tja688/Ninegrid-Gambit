using System;
using System.Collections.Generic;
using QFramework;

namespace NineGrid.Core.Systems
{
    public sealed class Evt_ActionRejected
    {
        public Evt_ActionRejected(GameCommandKind command, string reason, SlotId slot, int cardUid)
        {
            Command = command;
            Reason = reason ?? string.Empty;
            Slot = slot;
            CardUid = cardUid;
        }

        public GameCommandKind Command { get; private set; }
        public string Reason { get; private set; }
        public SlotId Slot { get; private set; }
        public int CardUid { get; private set; }
    }

    /// <summary>
    /// ADR-0047：失控反应链被熔断遏制（深度/总量超限，后续分支丢弃、命令原子收尾）。
    /// 表现层订阅后转显式错误日志——熔断属内容 bug 的兜底，不是正常路径。
    /// </summary>
    public sealed class Evt_PipelineFaultContained
    {
        public Evt_PipelineFaultContained(string actionName, int depth, int resolvedThisRun)
        {
            ActionName = actionName ?? string.Empty;
            Depth = depth;
            ResolvedThisRun = resolvedThisRun;
        }

        public string ActionName { get; private set; }
        public int Depth { get; private set; }
        public int ResolvedThisRun { get; private set; }
    }

    public interface IActionPipelineSystem : ISystem
    {
        EventLog EventLog { get; }
        bool IsRunning { get; }
        int PendingCount { get; }
        void Enqueue(GameAction action);
        void PushReaction(GameAction action);
        int Execute(GameAction action);
        int RunToCompletion();
        void RejectCommand(GameCommandKind command, string reason, SlotId slot, int cardUid);
        void Clear();
    }

    public sealed class ActionPipelineSystem : AbstractSystem, IActionPipelineSystem
    {
        /// <summary>真实因果嵌套深度上限；超过视为失控触发环（ADR-0047）。</summary>
        private const int MaxReactionDepth = 64;
        /// <summary>单次 RunToCompletion 解算动作总量保险丝——防有限深度的宽度爆炸把主线程冻死。</summary>
        private const int MaxResolvedPerRun = 4096;
        /// <summary>每次 run 最多落多少条熔断诊断事件，防止排空残留动作时刷屏。</summary>
        private const int MaxFaultEventsPerRun = 8;

        private static readonly CoreGameEvent[] sNoEvents = new CoreGameEvent[0];
        private readonly Queue<GameAction> mQueue = new Queue<GameAction>();
        private readonly Stack<GameAction> mReactionStack = new Stack<GameAction>();
        private int mNextActionId = 1;
        private int mResolvedThisRun;
        private int mFaultEventsThisRun;

        public EventLog EventLog { get; private set; }
        public bool IsRunning { get; private set; }

        public int PendingCount
        {
            get { return mQueue.Count + mReactionStack.Count; }
        }

        protected override void OnInit()
        {
            EventLog = new EventLog();
            mQueue.Clear();
            mReactionStack.Clear();
            mNextActionId = 1;
            IsRunning = false;
        }

        public void Enqueue(GameAction action)
        {
            if (action == null)
            {
                throw new ArgumentNullException("action");
            }

            mQueue.Enqueue(action);
        }

        public void PushReaction(GameAction action)
        {
            if (action == null)
            {
                throw new ArgumentNullException("action");
            }

            mReactionStack.Push(action);
        }

        public int Execute(GameAction action)
        {
            Enqueue(action);
            return RunToCompletion();
        }

        public int RunToCompletion()
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("Action pipeline is already running.");
            }

            mResolvedThisRun = 0;
            mFaultEventsThisRun = 0;
            IsRunning = true;
            try
            {
                while (mReactionStack.Count > 0 || mQueue.Count > 0)
                {
                    var action = mReactionStack.Count > 0 ? mReactionStack.Pop() : mQueue.Dequeue();
                    ResolveAction(action, 0);
                }
            }
            finally
            {
                IsRunning = false;
            }

            return mResolvedThisRun;
        }

        public void RejectCommand(GameCommandKind command, string reason, SlotId slot, int cardUid)
        {
            var gameEvent = new CoreGameEvent(CoreEventType.ActionRejected, 0, "Command")
                .WithAmount((int)command)
                .WithCard(cardUid)
                .WithSlots(slot, slot)
                .WithMessage(reason);
            EventLog.Append(gameEvent);
            this.SendEvent(new Evt_ActionRejected(command, reason, slot, cardUid));

            var context = new GameActionContext(((IBelongToArchitecture)this).GetArchitecture(), EventLog, 0, 0);
            var triggerContext = new TriggerContext(TriggerPoint.OnActionRejected, TriggerTiming.Post, null, new[] { gameEvent }, context);
            ResolveTriggeredActions(this.GetSystem<ITriggerSystem>().Dispatch(triggerContext), 1);
        }

        public void Clear()
        {
            mQueue.Clear();
            mReactionStack.Clear();
            EventLog.Clear();
            mNextActionId = 1;
            // 卡面投影账本随事件日志一起复位（扫描游标与日志长度锁步，ADR-0045）。
            this.GetModel<CardFaceLedgerModel>().Reset();
        }

        private void ResolveAction(GameAction action, int depth)
        {
            // ADR-0047 熔断遏制：失控触发环（深度超限）或宽度爆炸（总量超限）不再抛异常——
            // 抛异常会把命令炸穿在半途：已 Apply 的动作留在 Core、批次不开、表现层永远收不到
            // 该命令事实（盘面/遗物/点击全面分叉）。改为丢弃该分支并落诊断事件，命令原子收尾；
            // 失控环的「根治」在内容层（效果 DSL 的 cause 防环标记），这里只保证不把局玩死。
            if (depth > MaxReactionDepth || mResolvedThisRun >= MaxResolvedPerRun)
            {
                ContainPipelineFault(action, depth);
                return;
            }

            mResolvedThisRun++;
            var actionId = mNextActionId++;
            var context = new GameActionContext(((IBelongToArchitecture)this).GetArchitecture(), EventLog, actionId, depth);
            EventLog.Append(new CoreGameEvent(CoreEventType.ActionStarted, actionId, action.ActionName));

            DispatchTriggers(action, TriggerTiming.Pre, action.GetPreTriggerPoints(context), sNoEvents, context, depth);

            var result = action.Apply(context) ?? GameActionResult.Empty;
            EventLog.AppendRange(result.Events);
            // 统一对账缝（ADR-0045）：动作自身事件入日志后立即 diff-emit 卡面提交，
            // 使提交事件紧邻因果动作；触发器 / FollowUp 各自结算时再各对账一次。
            CardFaceReconciliation.ReconcileAfterAction(context, EventLog, action.ActionName);
            DispatchTriggers(action, TriggerTiming.Post, action.GetPostTriggerPoints(context, result.Events), result.Events, context, depth);
            ResolveTriggeredActions(result.FollowUpActions, depth + 1);

            EventLog.Append(new CoreGameEvent(CoreEventType.ActionFinished, actionId, action.ActionName));
        }

        private void ContainPipelineFault(GameAction action, int depth)
        {
            if (mFaultEventsThisRun >= MaxFaultEventsPerRun)
            {
                return;
            }

            mFaultEventsThisRun++;
            var reason = depth > MaxReactionDepth
                ? "Reaction chain exceeded max depth " + MaxReactionDepth
                : "Run exceeded max resolved actions " + MaxResolvedPerRun;
            var actionName = action != null ? action.ActionName : "<null>";
            EventLog.Append(new CoreGameEvent(CoreEventType.PipelineFaultContained, 0, actionName)
                .WithAmount(depth)
                .WithMessage(reason + "; dropped " + actionName + " (resolved=" + mResolvedThisRun + ")"));
            this.SendEvent(new Evt_PipelineFaultContained(actionName, depth, mResolvedThisRun));
        }

        private void DispatchTriggers(
            GameAction action,
            TriggerTiming timing,
            IEnumerable<TriggerPoint> points,
            IReadOnlyList<CoreGameEvent> events,
            GameActionContext context,
            int depth)
        {
            if (points == null)
            {
                return;
            }

            var triggerSystem = this.GetSystem<ITriggerSystem>();
            foreach (var point in points)
            {
                var triggerContext = new TriggerContext(point, timing, action, events, context);
                ResolveTriggeredActions(triggerSystem.Dispatch(triggerContext), depth + 1);
            }
        }

        private void ResolveTriggeredActions(IReadOnlyList<GameAction> actions, int depth)
        {
            if (actions == null || actions.Count == 0)
            {
                return;
            }

            // 只排干本次压入的动作：栈是共享的，若排到空为止，内层调用会把外层
            // 尚未处理的兄弟动作偷走并按内层深度嵌套结算——一串有限但较长的合法
            // 连锁（劈砍+击杀奖励+转盘+敌方行动）深度会被线性放大，误撞 64 深度
            // 保护并把异常炸出命令中途（Core/表现盘面永久分叉、流程卡死）。
            // 深度只应反映真实因果嵌套；真失控循环仍由 ResolveAction 的上限捕获。
            var baseline = mReactionStack.Count;
            for (var i = actions.Count - 1; i >= 0; i--)
            {
                PushReaction(actions[i]);
            }

            while (mReactionStack.Count > baseline)
            {
                ResolveAction(mReactionStack.Pop(), depth);
            }
        }
    }
}
