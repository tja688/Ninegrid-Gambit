#if UNITY_EDITOR
using System.Collections.Specialized;

namespace NineGrid.Content.Editor
{
    /// <summary>
    /// 工作台可选二进制资产契约：宿主实现后，transport 暴露 <c>GET /api/asset?...</c>（Bearer 鉴权），
    /// 用于向页面提供精灵缩略图 / 预览渲染等 PNG 载荷。
    /// </summary>
    public interface IEditorWorkbenchAssetHost
    {
        bool TryGetAsset(NameValueCollection query, out byte[] bytes, out string contentType, out string error);
    }
}
#endif
