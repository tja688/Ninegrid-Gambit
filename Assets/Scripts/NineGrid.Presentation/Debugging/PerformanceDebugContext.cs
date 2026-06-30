using UnityEngine;

namespace NineGrid.Presentation.Debugging
{
    public sealed class PerformanceDebugContext
    {
        public PerformanceDebugHarness Harness { get; set; }
        public PerformanceDebugViewRegistry Registry { get; set; }
        public DebugActorFactory ActorFactory { get; set; }
        public PerformanceDebugLogBuffer Log { get; set; }
        public Camera MainCamera { get; set; }

        public T GetModule<T>() where T : Component
        {
            return Harness != null ? Harness.GetModule<T>() : null;
        }

        public Transform ResolveActor(string actorId)
        {
            return Registry != null ? Registry.ResolveActor(actorId) : null;
        }

        public Transform ResolveAnchor(string anchorId)
        {
            return Registry != null ? Registry.ResolveAnchor(anchorId) : null;
        }
    }
}
