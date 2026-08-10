#if UNITY_EDITOR
namespace NineGrid.Content.Editor
{
    /// <summary>#195 Audio 域宿主：桥接 <see cref="AudioWorkbenchEditorState"/>，transport 不引用 Audio 语义类型。</summary>
    internal sealed class AudioWorkbenchHost : IEditorWorkbenchHost
    {
        private readonly AudioWorkbenchEditorState state;

        public AudioWorkbenchHost(AudioWorkbenchEditorState state)
        {
            this.state = state;
        }

        public int ProtocolVersion => AudioWorkbenchServer.ProtocolVersion;

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
