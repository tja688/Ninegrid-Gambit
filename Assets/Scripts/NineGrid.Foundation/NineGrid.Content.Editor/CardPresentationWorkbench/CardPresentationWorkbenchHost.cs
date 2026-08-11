#if UNITY_EDITOR
using System.Collections.Specialized;

namespace NineGrid.Content.Editor
{
    /// <summary>卡面表现域宿主：桥接 <see cref="CardPresentationWorkbenchEditorState"/>，transport 不感知卡面语义。</summary>
    internal sealed class CardPresentationWorkbenchHost : IEditorWorkbenchHost, IEditorWorkbenchAssetHost
    {
        private readonly CardPresentationWorkbenchEditorState state;

        public CardPresentationWorkbenchHost(CardPresentationWorkbenchEditorState state)
        {
            this.state = state;
        }

        public int ProtocolVersion => CardPresentationWorkbenchServer.ProtocolVersion;

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
            state.TickObservation();
        }

        public bool TryDispatchCommand(string command, string payloadJson, out object payload, out string error)
        {
            return state.TryDispatchCommand(command, payloadJson, out payload, out error);
        }

        public object BuildSnapshotPayload()
        {
            return state.BuildSnapshotPayload();
        }

        public bool TryGetAsset(NameValueCollection query, out byte[] bytes, out string contentType, out string error)
        {
            return state.TryGetAsset(query, out bytes, out contentType, out error);
        }
    }
}
#endif
