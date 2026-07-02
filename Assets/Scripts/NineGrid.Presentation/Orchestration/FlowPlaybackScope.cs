using System;
using NineGrid.Core;

namespace NineGrid.Presentation.Orchestration
{
    /// <summary>
    /// 批回放期间的只读上下文：Bindings 可从中取 Snapshot / ActorFactory 解析真实演员。
    /// </summary>
    public sealed class FlowPlaybackScope : IDisposable
    {
        public static FlowPlaybackScope Current { get; private set; }

        public CoreViewSnapshot Snapshot { get; }
        public TableNineActorFactory ActorFactory { get; }

        private FlowPlaybackScope(CoreViewSnapshot snapshot, TableNineActorFactory actorFactory)
        {
            Snapshot = snapshot;
            ActorFactory = actorFactory;
        }

        public static FlowPlaybackScope Push(CoreViewSnapshot snapshot, TableNineActorFactory actorFactory)
        {
            return new FlowPlaybackScope(snapshot, actorFactory);
        }

        public void Dispose()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        internal void Activate()
        {
            Current = this;
        }
    }
}
