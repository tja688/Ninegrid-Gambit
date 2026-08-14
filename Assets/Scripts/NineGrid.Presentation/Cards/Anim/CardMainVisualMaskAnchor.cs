using NineGrid.Cards.Slots;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Cards.Anim
{
    /// <summary>
    /// 卡面「主视图Mask」锚点：提供世界包围盒、建议本地居中、以及 SpriteMask 交互。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardMainVisualMaskAnchor : MonoBehaviour
    {
        public const string NodeName = "主视图Mask";
        public const string FaceBackgroundNodeName = "背景图";

        [SerializeField]
        private SpriteMask spriteMask;

        [SerializeField]
        private SpriteRenderer maskRenderer;

        private void Awake()
        {
            CacheRefsIfNeeded();
        }

        public Bounds GetWorldBounds()
        {
            CacheRefsIfNeeded();

            // SpriteMask.bounds 在 Renderer 禁用时仍可用；优先它，避免依赖已隐藏的调试 SpriteRenderer。
            if (spriteMask != null && spriteMask.sprite != null)
            {
                return spriteMask.bounds;
            }

            if (maskRenderer != null && maskRenderer.sprite != null)
            {
                return maskRenderer.bounds;
            }

            return new Bounds(transform.position, Vector3.one);
        }

        /// <summary>
        /// 按锚点模式将 visual（需已赋参考 sprite 与 scale）摆到 Mask 内；返回父节点下 localPosition。
        /// 定位在祖先变换无关的局部空间完成，避免 BounceFan 入场 scale=0 / 扇形旋转破坏世界空间锚定。
        /// </summary>
        public Vector3 GetAnchoredLocalPosition(
            SpriteRenderer visual,
            CardMainVisualAnchorMode mode = CardMainVisualAnchorMode.BottomCenter)
        {
            if (visual == null)
            {
                return Vector3.zero;
            }

            if (mode == CardMainVisualAnchorMode.TransformOrigin)
            {
                return GetSuggestedLocalPosition(visual.transform);
            }

            if (visual.sprite == null)
            {
                return GetSuggestedLocalPosition(visual.transform);
            }

            if (TryGetAnchoredLocalPositionInLocalSpace(visual, mode, out var local))
            {
                return local;
            }

            return GetAnchoredLocalPositionWorldFallback(visual, mode);
        }

        /// <summary>
        /// 将 visual 中心对齐到 Mask 中心；可选叠加 contentBounds 修正。
        /// 优先局部空间；退化时回退世界空间。
        /// </summary>
        public Vector3 GetSuggestedLocalPosition(Transform visual, Bounds? contentBounds = null)
        {
            if (visual == null)
            {
                return Vector3.zero;
            }

            if (TryGetSuggestedLocalPositionInLocalSpace(visual, contentBounds, out var local))
            {
                return local;
            }

            return GetSuggestedLocalPositionWorldFallback(visual, contentBounds);
        }

        /// <summary>
        /// 纯数学：移动 visual 原点后，content 包围盒按 mode 与 mask 对齐。
        /// </summary>
        public static Vector3 ComputeWorldPositionForAnchor(
            Vector3 visualWorldPosition,
            Bounds contentWorldBounds,
            Bounds maskWorldBounds,
            CardMainVisualAnchorMode mode)
        {
            Vector3 delta;
            switch (mode)
            {
                case CardMainVisualAnchorMode.BoundsCenter:
                    delta = maskWorldBounds.center - contentWorldBounds.center;
                    break;
                case CardMainVisualAnchorMode.BottomCenter:
                    delta.x = maskWorldBounds.center.x - contentWorldBounds.center.x;
                    delta.y = maskWorldBounds.min.y - contentWorldBounds.min.y;
                    delta.z = maskWorldBounds.center.z - contentWorldBounds.center.z;
                    break;
                default:
                    delta = Vector3.zero;
                    break;
            }

            return visualWorldPosition + delta;
        }

        /// <summary>
        /// 在父节点局部空间内，将已赋 sprite/scale 的 visual 按锚点模式对齐到给定 mask 包围盒。
        /// 供无 MonoBehaviour Mask（如战前预览槽框）复用卡面 BottomCenter 语义。
        /// </summary>
        public static Vector3 GetAnchoredLocalPositionInParentSpace(
            SpriteRenderer visual,
            Bounds maskInParent,
            CardMainVisualAnchorMode mode = CardMainVisualAnchorMode.BottomCenter)
        {
            if (visual == null)
            {
                return Vector3.zero;
            }

            if (mode == CardMainVisualAnchorMode.TransformOrigin || visual.sprite == null)
            {
                return visual.transform.localPosition;
            }

            var visualLocal = Matrix4x4.TRS(
                visual.transform.localPosition,
                visual.transform.localRotation,
                visual.transform.localScale);
            var contentInParent = TransformBounds(visualLocal, visual.sprite.bounds);
            return ComputeWorldPositionForAnchor(
                visual.transform.localPosition,
                contentInParent,
                maskInParent,
                mode);
        }

        public void ApplyMaskInteraction(SpriteRenderer visual)
        {
            if (visual == null)
            {
                return;
            }

            CacheRefsIfNeeded();
            if (spriteMask == null)
            {
                return;
            }

            // URP / SortingGroup：Mask 必须与被裁剪 Sprite 同 Sorting Layer，
            // 并用 Custom Range 罩住其 order，否则 VisibleInsideMask 会整段消失。
            SyncMaskSortingTo(visual);
            visual.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }

        /// <summary>
        /// 关掉卡面子树全部 SpriteMask 与 VisibleInsideMask 交互。
        /// 检视面板 / 编辑器预览在 UI SortingGroup 下若保留场地 Mask 会把整卡裁没。
        /// </summary>
        public static void DisableMasking(Transform faceRoot)
        {
            if (faceRoot == null)
            {
                return;
            }

            var masks = faceRoot.GetComponentsInChildren<SpriteMask>(true);
            for (var i = 0; i < masks.Length; i++)
            {
                if (masks[i] != null)
                {
                    masks[i].enabled = false;
                }
            }

            var renderers = faceRoot.GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var sr = renderers[i];
                if (sr != null && sr.maskInteraction != SpriteMaskInteraction.None)
                {
                    sr.maskInteraction = SpriteMaskInteraction.None;
                }
            }
        }

        /// <summary>
        /// 编辑器 PreviewRenderUtility 往往不跑 URP 2D Mask 模板，导致 VisibleInsideMask 全灭。
        /// 预览实例上关掉 Mask 交互，仅保留定位；Play/运行时仍走真实裁剪。
        /// </summary>
        public static void DisableMaskingForEditorPreview(Transform faceRoot)
        {
            DisableMasking(faceRoot);
        }

        public void SyncMaskSortingTo(SpriteRenderer visual)
        {
            if (visual == null)
            {
                return;
            }

            CacheRefsIfNeeded();
            if (spriteMask == null)
            {
                return;
            }

            ApplyMaskSortingRange(
                spriteMask,
                visual,
                CardFaceSortingLayers.MainIconMaskBackOffset,
                CardFaceSortingLayers.MainIconMaskFrontOffset);
        }

        /// <summary>
        /// URP + SortingGroup：Mask custom range 比对的是子 Renderer/Mask 上的
        /// sortingLayerID 属性，不是 SG 的「有效绘制层」。
        /// BounceFan 等把 SG 切到 UI 后，必须把子节点层 ID 一并改成同一层，
        /// 再 Sync Mask；只改 Mask 或只改 SG 都会导致 VisibleInsideMask 整段消失。
        /// </summary>
        public static void PropagateSortingLayerFromGroup(SortingGroup group)
        {
            if (group == null)
            {
                return;
            }

            var layerId = group.sortingLayerID;
            // Sprite + TMP MeshRenderer：子节点 sortingLayerID 须与 SG 同层，
            // 否则 SpriteMask custom range / 部分相机排序会把字或图标裁没。
            var renderers = group.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].sortingLayerID = layerId;
                }
            }

            var masks = group.GetComponentsInChildren<SpriteMask>(true);
            for (var i = 0; i < masks.Length; i++)
            {
                var mask = masks[i];
                if (mask == null)
                {
                    continue;
                }

                mask.sortingLayerID = layerId;
                if (mask.isCustomRangeActive)
                {
                    mask.frontSortingLayerID = layerId;
                    mask.backSortingLayerID = layerId;
                }
            }
        }

        /// <summary>
        /// SortingGroup 改层/改序后：先对齐子节点层，再重同步所有 VisibleInsideMask。
        /// </summary>
        public static void ResyncAllVisibleInsideMasks(Transform root)
        {
            if (root == null)
            {
                return;
            }

            var group = root.GetComponent<SortingGroup>();
            if (group == null)
            {
                group = root.GetComponentInParent<SortingGroup>();
            }

            if (group != null)
            {
                PropagateSortingLayerFromGroup(group);
            }

            var mainAnchor = FindOrAdd(root);
            var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var visual = renderers[i];
                if (visual == null
                    || visual.maskInteraction != SpriteMaskInteraction.VisibleInsideMask)
                {
                    continue;
                }

                var selfMask = visual.GetComponent<SpriteMask>();
                if (selfMask != null && selfMask.enabled)
                {
                    ApplyMaskSortingRange(
                        selfMask,
                        visual,
                        CardFaceSortingLayers.FaceBackgroundMaskBackOffset,
                        CardFaceSortingLayers.FaceBackgroundMaskFrontOffset);
                    continue;
                }

                if (mainAnchor != null)
                {
                    mainAnchor.SyncMaskSortingTo(visual);
                }
            }
        }

        /// <summary>
        /// 启用「背景图」上的六边 SpriteMask，并让背景 VisibleInsideMask。
        /// 主视图 Mask 的 custom range 不得覆盖 FaceBackground order（见 SortingLayers 偏移）。
        /// 调用前若卡在 SortingGroup 下，应先 <see cref="PropagateSortingLayerFromGroup"/>。
        /// </summary>
        public static void EnsureFaceBackgroundHexMask(Transform faceRoot)
        {
            if (faceRoot == null)
            {
                return;
            }

            SpriteRenderer bgRenderer = null;
            var bgNode = FindDeep(faceRoot, FaceBackgroundNodeName);
            if (bgNode != null)
            {
                bgRenderer = bgNode.GetComponent<SpriteRenderer>();
            }

            if (bgRenderer == null
                && CardFaceSlotNodeMap.TryFindRenderer(
                    faceRoot,
                    CardFaceSlotCodes.FaceBackground,
                    out var resolved))
            {
                bgRenderer = resolved;
            }

            if (bgRenderer == null)
            {
                return;
            }

            var hexMask = bgRenderer.GetComponent<SpriteMask>();
            if (hexMask == null || hexMask.sprite == null)
            {
                // 无专用 Mask sprite 时不瞎启用，避免整卡被矩形自裁。
                return;
            }

            var group = bgRenderer.GetComponentInParent<SortingGroup>();
            if (group != null)
            {
                PropagateSortingLayerFromGroup(group);
            }

            hexMask.enabled = true;
            ApplyMaskSortingRange(
                hexMask,
                bgRenderer,
                CardFaceSortingLayers.FaceBackgroundMaskBackOffset,
                CardFaceSortingLayers.FaceBackgroundMaskFrontOffset);
            bgRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }

        /// <summary>
        /// Mask custom range 使用子 Renderer 上的 sortingLayerID（调用方须已与 SG 对齐）。
        /// </summary>
        public static void ApplyMaskSortingRange(
            SpriteMask mask,
            SpriteRenderer visual,
            int backOrderOffset,
            int frontOrderOffset)
        {
            if (mask == null || visual == null)
            {
                return;
            }

            ResolveEffectiveSorting(visual, out var layerId, out var order);
            mask.frontSortingLayerID = layerId;
            mask.backSortingLayerID = layerId;
            mask.frontSortingOrder = order + frontOrderOffset;
            mask.backSortingOrder = order + backOrderOffset;
            mask.isCustomRangeActive = true;
            mask.sortingLayerID = layerId;
            mask.sortingOrder = order;
        }

        public static void ResolveEffectiveSorting(
            SpriteRenderer visual,
            out int sortingLayerId,
            out int sortingOrder)
        {
            sortingLayerId = visual != null ? visual.sortingLayerID : 0;
            sortingOrder = visual != null ? visual.sortingOrder : 0;
            if (visual == null)
            {
                return;
            }

            // 若子节点尚未被 Propagate，回退到父 SG 层，避免写错 range。
            var group = visual.GetComponentInParent<SortingGroup>();
            if (group != null && visual.sortingLayerID != group.sortingLayerID)
            {
                sortingLayerId = group.sortingLayerID;
            }
        }

        public static CardMainVisualMaskAnchor FindOrAdd(Transform faceRoot)
        {
            if (faceRoot == null)
            {
                return null;
            }

            var existing = faceRoot.GetComponentInChildren<CardMainVisualMaskAnchor>(true);
            if (existing != null)
            {
                return existing;
            }

            var node = FindDeep(faceRoot, NodeName);
            if (node == null)
            {
                return null;
            }

            var anchor = node.GetComponent<CardMainVisualMaskAnchor>();
            if (anchor == null)
            {
                anchor = node.gameObject.AddComponent<CardMainVisualMaskAnchor>();
            }

            return anchor;
        }

        private void CacheRefsIfNeeded()
        {
            if (spriteMask == null)
            {
                spriteMask = GetComponent<SpriteMask>();
                if (spriteMask == null)
                {
                    spriteMask = GetComponentInChildren<SpriteMask>(true);
                }
            }

            if (maskRenderer == null)
            {
                maskRenderer = GetComponent<SpriteRenderer>();
                if (maskRenderer == null)
                {
                    maskRenderer = GetComponentInChildren<SpriteRenderer>(true);
                }
            }
        }

        private bool TryGetAnchoredLocalPositionInLocalSpace(
            SpriteRenderer visual,
            CardMainVisualAnchorMode mode,
            out Vector3 local)
        {
            local = Vector3.zero;
            var parent = visual.transform.parent;
            if (parent == null || !TryGetMaskSprite(out var maskSprite))
            {
                return false;
            }

            if (!TryGetLocalToLocalMatrix(transform, parent, out var maskToParent))
            {
                return false;
            }

            var maskInParent = TransformBounds(maskToParent, maskSprite.bounds);
            local = GetAnchoredLocalPositionInParentSpace(visual, maskInParent, mode);
            return true;
        }

        private bool TryGetSuggestedLocalPositionInLocalSpace(
            Transform visual,
            Bounds? contentBounds,
            out Vector3 local)
        {
            local = Vector3.zero;
            var parent = visual.parent;
            if (parent == null || !TryGetMaskSprite(out var maskSprite))
            {
                return false;
            }

            if (!TryGetLocalToLocalMatrix(transform, parent, out var maskToParent))
            {
                return false;
            }

            var maskInParent = TransformBounds(maskToParent, maskSprite.bounds);
            if (contentBounds.HasValue)
            {
                // contentBounds 约定为世界空间；无局部换算时走退化路径。
                return false;
            }

            local = maskInParent.center;
            return true;
        }

        private Vector3 GetAnchoredLocalPositionWorldFallback(
            SpriteRenderer visual,
            CardMainVisualAnchorMode mode)
        {
            var maskBounds = GetWorldBounds();
            var targetWorld = ComputeWorldPositionForAnchor(
                visual.transform.position,
                visual.bounds,
                maskBounds,
                mode);

            var parent = visual.transform.parent;
            if (parent == null)
            {
                return targetWorld;
            }

            return parent.InverseTransformPoint(targetWorld);
        }

        private Vector3 GetSuggestedLocalPositionWorldFallback(
            Transform visual,
            Bounds? contentBounds)
        {
            if (contentBounds.HasValue)
            {
                var maskBounds = GetWorldBounds();
                var targetWorld = ComputeWorldPositionForAnchor(
                    visual.position,
                    contentBounds.Value,
                    maskBounds,
                    CardMainVisualAnchorMode.BoundsCenter);

                var parent = visual.parent;
                if (parent == null)
                {
                    return targetWorld;
                }

                return parent.InverseTransformPoint(targetWorld);
            }

            var maskBoundsCenter = GetWorldBounds();
            var targetWorldOrigin = maskBoundsCenter.center;
            var parentTransform = visual.parent;
            if (parentTransform == null)
            {
                return targetWorldOrigin;
            }

            return parentTransform.InverseTransformPoint(targetWorldOrigin);
        }

        private bool TryGetMaskSprite(out Sprite maskSprite)
        {
            CacheRefsIfNeeded();
            if (spriteMask != null && spriteMask.sprite != null)
            {
                maskSprite = spriteMask.sprite;
                return true;
            }

            if (maskRenderer != null && maskRenderer.sprite != null)
            {
                maskSprite = maskRenderer.sprite;
                return true;
            }

            maskSprite = null;
            return false;
        }

        /// <summary>
        /// 将 <paramref name="node"/> 局部空间点变换到 <paramref name="target"/> 局部空间。
        /// 只累乘两侧 local TRS，不碰 localToWorld / InverseTransformPoint，故共同祖先之上的
        /// scale=0 / 旋转不影响结果。
        /// </summary>
        private static bool TryGetLocalToLocalMatrix(
            Transform node,
            Transform target,
            out Matrix4x4 matrix)
        {
            matrix = Matrix4x4.identity;
            if (node == null || target == null)
            {
                return false;
            }

            if (node == target)
            {
                return true;
            }

            if (!TryFindCommonAncestor(node, target, out var ancestor))
            {
                return false;
            }

            var nodeSide = AccumulateLocalToAncestor(node, ancestor);
            var targetSide = AccumulateLocalToAncestor(target, ancestor);
            if (!targetSide.ValidTRS())
            {
                // 共同祖先之下若已有退化 scale，仍不应走到这里；保守拒绝。
                return false;
            }

            matrix = targetSide.inverse * nodeSide;
            return true;
        }

        private static bool TryFindCommonAncestor(
            Transform a,
            Transform b,
            out Transform ancestor)
        {
            ancestor = null;
            var seen = new System.Collections.Generic.HashSet<Transform>();
            for (var t = a; t != null; t = t.parent)
            {
                seen.Add(t);
            }

            for (var t = b; t != null; t = t.parent)
            {
                if (seen.Contains(t))
                {
                    ancestor = t;
                    return true;
                }
            }

            return false;
        }

        private static Matrix4x4 AccumulateLocalToAncestor(Transform node, Transform ancestor)
        {
            var result = Matrix4x4.identity;
            for (var t = node; t != null && t != ancestor; t = t.parent)
            {
                var local = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale);
                result = local * result;
            }

            return result;
        }

        private static Bounds TransformBounds(Matrix4x4 matrix, Bounds localBounds)
        {
            var center = localBounds.center;
            var extents = localBounds.extents;
            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (var ix = -1; ix <= 1; ix += 2)
            {
                for (var iy = -1; iy <= 1; iy += 2)
                {
                    for (var iz = -1; iz <= 1; iz += 2)
                    {
                        var corner = center + new Vector3(extents.x * ix, extents.y * iy, extents.z * iz);
                        var world = matrix.MultiplyPoint3x4(corner);
                        min = Vector3.Min(min, world);
                        max = Vector3.Max(max, world);
                    }
                }
            }

            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
