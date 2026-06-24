using System;

namespace NineGrid.Presentation.Diagnostics
{
    [Flags]
    public enum PresentationTraceChannel
    {
        None = 0,
        Flow = 1 << 0,
        Lock = 1 << 1,
        Batch = 1 << 2,
        Command = 1 << 3,
        Adaptor = 1 << 4,
        Fsm = 1 << 5,
        Actor = 1 << 6,
        Performance = 1 << 7,
        Overlay = 1 << 8,
        Watchdog = 1 << 9,

        Default = Flow | Lock | Batch | Command | Fsm | Overlay | Watchdog,
        All = Flow | Lock | Batch | Command | Adaptor | Fsm | Actor | Performance | Overlay | Watchdog,
    }
}
