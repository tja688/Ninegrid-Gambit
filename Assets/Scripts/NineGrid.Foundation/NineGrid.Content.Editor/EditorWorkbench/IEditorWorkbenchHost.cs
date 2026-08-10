#if UNITY_EDITOR
namespace NineGrid.Content.Editor
{
    /// <summary>
    /// #195 工作台 loopback 传输的宿主契约：revision envelope、命令与 snapshot payload 由具体域实现。
    /// </summary>
    public interface IEditorWorkbenchHost
    {
        int ProtocolVersion { get; }

        long LocalRevision { get; }

        void Initialize();

        void OnBeforeAssemblyReload();

        void TickObservation();

        bool TryDispatchCommand(string command, string payloadJson, out object payload, out string error);

        object BuildSnapshotPayload();
    }
}
#endif
