#if UNITY_EDITOR
namespace NineGrid.Content.Editor
{
    internal sealed class VfxWorkbenchHost : IEditorWorkbenchHost
    {
        private readonly VfxWorkbenchEditorState state;

        public VfxWorkbenchHost(VfxWorkbenchEditorState state)
        {
            this.state = state;
        }

        public int ProtocolVersion => VfxWorkbenchServer.ProtocolVersion;

        public long LocalRevision => state.LocalRevision;

        public void Initialize()
        {
            state.InitializeFromDisk();
        }

        public void OnBeforeAssemblyReload()
        {
            state.OnBeforeAssemblyReload();
        }

        public void TickObservation()
        {
            state.TickRuntimeObservation();
        }

        public bool TryDispatchCommand(string command, string payloadJson, out object payload, out string error)
        {
            return state.TryDispatchCommand(command, payloadJson, out payload, out error);
        }

        public object BuildSnapshotPayload()
        {
            return state.BuildSnapshotPayload();
        }
    }
}
#endif
