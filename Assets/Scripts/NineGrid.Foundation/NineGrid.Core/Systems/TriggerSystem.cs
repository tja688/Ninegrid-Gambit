using System;
using System.Collections.Generic;
using QFramework;

namespace NineGrid.Core.Systems
{
    public sealed class TriggerContext
    {
        public TriggerContext(
            TriggerPoint point,
            TriggerTiming timing,
            GameAction action,
            IReadOnlyList<CoreGameEvent> events,
            GameActionContext actionContext)
        {
            Point = point;
            Timing = timing;
            Action = action;
            Events = events;
            ActionContext = actionContext;
        }

        public TriggerPoint Point { get; private set; }
        public TriggerTiming Timing { get; private set; }
        public GameAction Action { get; private set; }
        public IReadOnlyList<CoreGameEvent> Events { get; private set; }
        public GameActionContext ActionContext { get; private set; }

        /// <summary>
        /// 同一触发批次内，碎片重组 (headless, skull) 仅允许合并一次。
        /// </summary>
        public bool TryReserveFragmentMerge(int leftUid, int rightUid)
        {
            if (leftUid == 0 || rightUid == 0)
            {
                return false;
            }

            if (mMergedFragmentPairs == null)
            {
                mMergedFragmentPairs = new HashSet<long>();
            }

            return mMergedFragmentPairs.Add(PackPair(leftUid, rightUid));
        }

        private HashSet<long> mMergedFragmentPairs;

        private static long PackPair(int leftUid, int rightUid)
        {
            var lo = leftUid < rightUid ? leftUid : rightUid;
            var hi = leftUid < rightUid ? rightUid : leftUid;
            return ((long)lo << 32) | (uint)hi;
        }
    }

    public interface ITriggerReaction
    {
        string Id { get; }
        IEnumerable<GameAction> React(TriggerContext context);
    }

    public sealed class DelegateTriggerReaction : ITriggerReaction
    {
        private readonly Func<TriggerContext, IEnumerable<GameAction>> mHandler;

        public DelegateTriggerReaction(string id, Func<TriggerContext, IEnumerable<GameAction>> handler)
        {
            Id = string.IsNullOrEmpty(id) ? "delegate" : id;
            mHandler = handler;
        }

        public string Id { get; private set; }

        public IEnumerable<GameAction> React(TriggerContext context)
        {
            return mHandler == null ? null : mHandler(context);
        }
    }

    public interface ITriggerSystem : ISystem
    {
        IUnRegister Register(TriggerPoint point, TriggerTiming timing, ITriggerReaction reaction);
        bool Unregister(TriggerPoint point, TriggerTiming timing, ITriggerReaction reaction);
        IReadOnlyList<GameAction> Dispatch(TriggerContext context);
        void Clear();
    }

    public sealed class TriggerSystem : AbstractSystem, ITriggerSystem
    {
        private readonly Dictionary<string, List<ITriggerReaction>> mReactions = new Dictionary<string, List<ITriggerReaction>>();

        protected override void OnInit()
        {
            Clear();
        }

        public IUnRegister Register(TriggerPoint point, TriggerTiming timing, ITriggerReaction reaction)
        {
            if (reaction == null)
            {
                throw new ArgumentNullException("reaction");
            }

            var key = BuildKey(point, timing);
            List<ITriggerReaction> list;
            if (!mReactions.TryGetValue(key, out list))
            {
                list = new List<ITriggerReaction>();
                mReactions.Add(key, list);
            }

            list.Add(reaction);
            return new CustomUnRegister(() => Unregister(point, timing, reaction));
        }

        public bool Unregister(TriggerPoint point, TriggerTiming timing, ITriggerReaction reaction)
        {
            if (reaction == null)
            {
                return false;
            }

            List<ITriggerReaction> list;
            return mReactions.TryGetValue(BuildKey(point, timing), out list) && list.Remove(reaction);
        }

        public IReadOnlyList<GameAction> Dispatch(TriggerContext context)
        {
            var result = new List<GameAction>();
            if (context == null)
            {
                return result;
            }

            List<ITriggerReaction> list;
            if (!mReactions.TryGetValue(BuildKey(context.Point, context.Timing), out list))
            {
                return result;
            }

            var snapshot = new List<ITriggerReaction>(list);
            for (var i = 0; i < snapshot.Count; i++)
            {
                var actions = snapshot[i].React(context);
                if (actions == null)
                {
                    continue;
                }

                foreach (var action in actions)
                {
                    if (action != null)
                    {
                        result.Add(action);
                    }
                }
            }

            return result;
        }

        public void Clear()
        {
            mReactions.Clear();
        }

        private static string BuildKey(TriggerPoint point, TriggerTiming timing)
        {
            return timing + ":" + point;
        }
    }
}
