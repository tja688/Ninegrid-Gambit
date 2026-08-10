using UnityEngine;

namespace NineGrid.Content.Vfx
{
    /// <summary>向附着型特效提供受控视觉域；#197 完整能力协商。</summary>
    public interface IVfxDomainHost
    {
        bool IsAvailable { get; }

        /// <summary>实例应挂接的父级；不得要求播放器改写共享相机、Canvas 或卡级 SortingGroup。</summary>
        Transform AttachmentParent { get; }

        /// <summary>发射时世界坐标 → 父级局部坐标。</summary>
        bool TryWorldToLocal(Vector3 worldPosition, out Vector3 localPosition);

        /// <summary>可选跟随锚；非 null 时实例应挂在其下以随宿主运动。</summary>
        bool TryGetFollowTarget(out Transform followTarget);

        /// <summary>允许写入的排序层与 order 区间。</summary>
        bool TryGetSortingBounds(out VfxSortingBounds bounds);

        /// <summary>可选裁剪遮罩；播放器只读，不修改遮罩组件。</summary>
        SpriteMask Mask { get; }
    }
}
