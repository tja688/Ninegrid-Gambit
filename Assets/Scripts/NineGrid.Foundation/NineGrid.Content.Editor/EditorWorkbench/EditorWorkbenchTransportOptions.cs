#if UNITY_EDITOR
namespace NineGrid.Content.Editor
{
    /// <summary>#195 参数化 token/端口区间、静态页根目录与协议版本。</summary>
    public sealed class EditorWorkbenchTransportOptions
    {
        public string TokenSessionKey { get; set; }

        public string PortSessionKey { get; set; }

        public int MinPort { get; set; }

        public int MaxPort { get; set; }

        public int ProtocolVersion { get; set; } = 1;

        /// <summary>相对项目根目录的静态页目录（含 index.html / styles.css / app.js）。</summary>
        public string WebRootRelativePath { get; set; }

        /// <summary>端口绑定失败等日志/异常中的可读名称。</summary>
        public string DisplayName { get; set; }
    }
}
#endif
