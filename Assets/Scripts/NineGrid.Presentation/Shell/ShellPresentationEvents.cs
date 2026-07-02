using System;

namespace NineGrid.Presentation.Shell
{
    public readonly struct Evt_RunOutcomeAnnounced
    {
        public Evt_RunOutcomeAnnounced(RunOutcome outcome)
        {
            Outcome = outcome;
        }

        public RunOutcome Outcome { get; }
    }

    /// <summary>
    /// 表现层 Shell 事件总线（不依赖 Core 架构）。
    /// </summary>
    public static class ShellPresentationEvents
    {
        public static event Action<Evt_RunOutcomeAnnounced> RunOutcomeAnnounced;

        internal static void RaiseRunOutcome(RunOutcome outcome)
        {
            RunOutcomeAnnounced?.Invoke(new Evt_RunOutcomeAnnounced(outcome));
        }
    }
}
