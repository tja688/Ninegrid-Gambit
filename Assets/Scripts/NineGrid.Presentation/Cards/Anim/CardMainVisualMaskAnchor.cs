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

        /// <summary>
        /// 将 visual 中心对齐到 Mask 中心；可选叠加 contentBounds 修正（世界空间内容包围盒）。
        /// </summary>
        public Vector3 GetSuggestedLocalPosition(Transform visual, Bounds? contentBounds = null)
        {
            if (visual == null)
            {
                return Vector3.zero;
            }

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
        /// 编辑器 PreviewRenderUtility 往往不跑 URP 2D Mask 模板，导致 VisibleInsideMask 全灭。
        /// 预览实例上关掉 Mask 交互，仅保留定位；Play/运行时仍走真实裁剪。
        /// </summary>
        public static void DisableMaskingForEditorPreview(Transform faceRoot)
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
            var renderers = group.GetComponentsInChildren<SpriteRenderer>(true);
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
