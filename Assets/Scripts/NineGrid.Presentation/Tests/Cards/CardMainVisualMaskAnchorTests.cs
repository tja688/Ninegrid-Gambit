using NUnit.Framework;
using NineGrid.Cards.Anim;
using NineGrid.Cards.Slots;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Presentation.Tests
{
    public sealed class CardMainVisualMaskAnchorTests
    {
        [Test]
        public void ComputeWorldPositionForAnchor_BottomCenter_AlignsBottomAndHorizontalCenter()
        {
            var mask = new Bounds(new Vector3(10f, 8f, 0f), new Vector3(4f, 6f, 0f));
            var content = new Bounds(new Vector3(1f, 2f, 0f), new Vector3(2f, 4f, 0f));
            var visualPos = Vector3.zero;

            var result = CardMainVisualMaskAnchor.ComputeWorldPositionForAnchor(
                visualPos,
                content,
                mask,
                CardMainVisualAnchorMode.BottomCenter);

            var delta = result - visualPos;
            var alignedContent = new Bounds(content.center + delta, content.size);
            Assert.AreEqual(mask.center.x, alignedContent.center.x, 0.0001f);
            Assert.AreEqual(mask.min.y, alignedContent.min.y, 0.0001f);
        }

        [Test]
        public void ComputeWorldPositionForAnchor_BoundsCenter_AlignsCenters()
        {
            var mask = new Bounds(new Vector3(5f, 5f, 0f), new Vector3(2f, 2f, 0f));
            var content = new Bounds(new Vector3(0f, 0f, 0f), new Vector3(1f, 3f, 0f));
            var visualPos = new Vector3(1f, -1f, 0f);

            var result = CardMainVisualMaskAnchor.ComputeWorldPositionForAnchor(
                visualPos,
                content,
                mask,
                CardMainVisualAnchorMode.BoundsCenter);

            var delta = result - visualPos;
            var alignedCenter = content.center + delta;
            Assert.AreEqual(mask.center.x, alignedCenter.x, 0.0001f);
            Assert.AreEqual(mask.center.y, alignedCenter.y, 0.0001f);
        }

        [Test]
        public void ApplyToRenderer_SwappingSprite_DoesNotChangeLocalPosition()
        {
            var root = new GameObject("root");
            var maskGo = new GameObject("mask");
            maskGo.transform.SetParent(root.transform, false);
            maskGo.transform.localPosition = new Vector3(0f, 2f, 0f);
            maskGo.transform.localScale = new Vector3(4f, 4f, 1f);

            var maskSr = maskGo.AddComponent<SpriteRenderer>();
            maskSr.sprite = CreateTestSprite(64, 64, Color.white);

            var anchor = maskGo.AddComponent<CardMainVisualMaskAnchor>();

            var visualGo = new GameObject("visual");
            visualGo.transform.SetParent(root.transform, false);
            var visualSr = visualGo.AddComponent<SpriteRenderer>();

            var spriteA = CreateTestSprite(32, 40, Color.red);
            var spriteB = CreateTestSprite(32, 28, Color.blue);

            CardMainVisualPlacement.ApplyToRenderer(
                visualSr,
                anchor,
                spriteA,
                uniformScale: 1f,
                offsetX: 0.1f,
                offsetY: -0.05f);

            var lockedLocal = visualGo.transform.localPosition;
            var lockedScale = visualGo.transform.localScale;

            visualSr.sprite = spriteB;
            Assert.AreEqual(lockedLocal, visualGo.transform.localPosition);
            Assert.AreEqual(lockedScale, visualGo.transform.localScale);

            Object.DestroyImmediate(root);
        }

        [Test]
        public void SyncMaskSortingTo_UsesSortingGroupLayer_NotChildLayer()
        {
            var root = new GameObject("chassis");
            var group = root.AddComponent<SortingGroup>();
            var uiLayerId = SortingLayer.NameToID("UI");
            if (uiLayerId != 0)
            {
                group.sortingLayerName = "UI";
            }
            else
            {
                group.sortingLayerName = "Default";
            }

            group.sortingOrder = 6;

            var maskGo = new GameObject(CardMainVisualMaskAnchor.NodeName);
            maskGo.transform.SetParent(root.transform, false);
            var spriteMask = maskGo.AddComponent<SpriteMask>();
            maskGo.AddComponent<SpriteRenderer>().sprite = CreateTestSprite(8, 8, Color.white);
            var anchor = maskGo.AddComponent<CardMainVisualMaskAnchor>();

            var visualGo = new GameObject("遗物主图标");
            visualGo.transform.SetParent(root.transform, false);
            var visual = visualGo.AddComponent<SpriteRenderer>();
            visual.sprite = CreateTestSprite(16, 16, Color.red);
            visual.sortingOrder = CardFaceSortingLayers.MainIcon;
            visual.sortingLayerID = SortingLayer.NameToID("Default");

            group.sortingOrder = 12;
            // 真实 BounceFan 路径：先传播子节点层，再 Sync。
            CardMainVisualMaskAnchor.PropagateSortingLayerFromGroup(group);
            anchor.SyncMaskSortingTo(visual);

            Assert.AreEqual(group.sortingLayerID, visual.sortingLayerID);
            Assert.AreEqual(group.sortingLayerID, spriteMask.frontSortingLayerID);
            Assert.AreEqual(group.sortingLayerID, spriteMask.backSortingLayerID);
            Assert.AreEqual(group.sortingLayerID, spriteMask.sortingLayerID);
            Assert.AreEqual(
                CardFaceSortingLayers.MainIcon + CardFaceSortingLayers.MainIconMaskFrontOffset,
                spriteMask.frontSortingOrder);
            Assert.AreEqual(
                CardFaceSortingLayers.MainIcon + CardFaceSortingLayers.MainIconMaskBackOffset,
                spriteMask.backSortingOrder);
            Assert.IsTrue(spriteMask.isCustomRangeActive);

            anchor.ApplyMaskInteraction(visual);
            Assert.AreEqual(SpriteMaskInteraction.VisibleInsideMask, visual.maskInteraction);

            Object.DestroyImmediate(root);
        }

        [Test]
        public void PropagateSortingLayerFromGroup_AlignsChildRenderersAndMasks()
        {
            var root = new GameObject("chassis");
            var group = root.AddComponent<SortingGroup>();
            var uiLayerId = SortingLayer.NameToID("UI");
            Assert.AreNotEqual(0, uiLayerId, "项目须有 UI Sorting Layer");
            group.sortingLayerName = "UI";

            var srGo = new GameObject("bg");
            srGo.transform.SetParent(root.transform, false);
            var sr = srGo.AddComponent<SpriteRenderer>();
            sr.sortingLayerID = SortingLayer.NameToID("Default");
            var sm = srGo.AddComponent<SpriteMask>();
            sm.sortingLayerID = SortingLayer.NameToID("Default");
            sm.isCustomRangeActive = true;
            sm.frontSortingLayerID = sm.sortingLayerID;
            sm.backSortingLayerID = sm.sortingLayerID;

            var meshGo = new GameObject("tmpLike");
            meshGo.transform.SetParent(root.transform, false);
            var mesh = meshGo.AddComponent<MeshRenderer>();
            mesh.sortingLayerID = SortingLayer.NameToID("Default");

            CardMainVisualMaskAnchor.PropagateSortingLayerFromGroup(group);

            Assert.AreEqual(group.sortingLayerID, sr.sortingLayerID);
            Assert.AreEqual(group.sortingLayerID, mesh.sortingLayerID);
            Assert.AreEqual(group.sortingLayerID, sm.sortingLayerID);
            Assert.AreEqual(group.sortingLayerID, sm.frontSortingLayerID);
            Assert.AreEqual(group.sortingLayerID, sm.backSortingLayerID);

            Object.DestroyImmediate(root);
        }

        [Test]
        public void MainIconMaskRange_DoesNotCoverFaceBackgroundOrder()
        {
            var back = CardFaceSortingLayers.MainIcon + CardFaceSortingLayers.MainIconMaskBackOffset;
            var front = CardFaceSortingLayers.MainIcon + CardFaceSortingLayers.MainIconMaskFrontOffset;
            Assert.Greater(back, CardFaceSortingLayers.FaceBackground);
            Assert.Greater(back, CardFaceSortingLayers.CardFrame);
            Assert.LessOrEqual(back, CardFaceSortingLayers.MainIcon);
            Assert.GreaterOrEqual(front, CardFaceSortingLayers.MainIcon);
        }

        [Test]
        public void EnsureFaceBackgroundHexMask_EnablesSelfMaskAndVisibleInside()
        {
            var face = new GameObject("遗物卡标准模版");
            var bg = new GameObject(CardMainVisualMaskAnchor.FaceBackgroundNodeName);
            bg.transform.SetParent(face.transform, false);
            var bgSr = bg.AddComponent<SpriteRenderer>();
            bgSr.sprite = CreateTestSprite(32, 48, Color.blue);
            bgSr.sortingOrder = CardFaceSortingLayers.FaceBackground;
            var hexMask = bg.AddComponent<SpriteMask>();
            hexMask.sprite = CreateTestSprite(32, 48, Color.white);
            hexMask.enabled = false;
            bgSr.maskInteraction = SpriteMaskInteraction.None;

            CardMainVisualMaskAnchor.EnsureFaceBackgroundHexMask(face.transform);

            Assert.IsTrue(hexMask.enabled);
            Assert.AreEqual(SpriteMaskInteraction.VisibleInsideMask, bgSr.maskInteraction);
            Assert.IsTrue(hexMask.isCustomRangeActive);
            Assert.AreEqual(
                CardFaceSortingLayers.FaceBackground + CardFaceSortingLayers.FaceBackgroundMaskFrontOffset,
                hexMask.frontSortingOrder);
            Assert.AreEqual(
                CardFaceSortingLayers.FaceBackground + CardFaceSortingLayers.FaceBackgroundMaskBackOffset,
                hexMask.backSortingOrder);

            Object.DestroyImmediate(face);
        }

        [Test]
        public void Anchoring_IsInvariant_UnderZeroScaleAncestor()
        {
            BuildRelicLikeHierarchy(out var wrapper, out var anchor, out var visualSr);
            try
            {
                wrapper.localScale = Vector3.one;
                CardMainVisualPlacement.ApplyToRenderer(
                    visualSr,
                    anchor,
                    visualSr.sprite,
                    uniformScale: 1f,
                    offsetX: 0f,
                    offsetY: 0f);
                var atScaleOne = visualSr.transform.localPosition;

                wrapper.localScale = Vector3.zero;
                CardMainVisualPlacement.ApplyToRenderer(
                    visualSr,
                    anchor,
                    visualSr.sprite,
                    uniformScale: 1f,
                    offsetX: 0f,
                    offsetY: 0f);
                var atScaleZero = visualSr.transform.localPosition;

                Assert.AreEqual(atScaleOne.x, atScaleZero.x, 0.0001f);
                Assert.AreEqual(atScaleOne.y, atScaleZero.y, 0.0001f);
                Assert.AreEqual(atScaleOne.z, atScaleZero.z, 0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(wrapper.gameObject);
            }
        }

        [Test]
        public void Anchoring_IsInvariant_UnderRotatedAncestor()
        {
            BuildRelicLikeHierarchy(out var wrapper, out var anchor, out var visualSr);
            try
            {
                wrapper.localScale = Vector3.one;
                wrapper.localRotation = Quaternion.identity;
                CardMainVisualPlacement.ApplyToRenderer(
                    visualSr,
                    anchor,
                    visualSr.sprite,
                    uniformScale: 1f,
                    offsetX: 0f,
                    offsetY: 0f);
                var withoutRotation = visualSr.transform.localPosition;

                wrapper.localRotation = Quaternion.Euler(0f, 0f, 18f);
                CardMainVisualPlacement.ApplyToRenderer(
                    visualSr,
                    anchor,
                    visualSr.sprite,
                    uniformScale: 1f,
                    offsetX: 0f,
                    offsetY: 0f);
                var withRotation = visualSr.transform.localPosition;

                Assert.AreEqual(withoutRotation.x, withRotation.x, 0.0001f);
                Assert.AreEqual(withoutRotation.y, withRotation.y, 0.0001f);
                Assert.AreEqual(withoutRotation.z, withRotation.z, 0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(wrapper.gameObject);
            }
        }

        [Test]
        public void MainIcon_StaysInsideMask_AfterRecommitAtZeroScale()
        {
            BuildRelicLikeHierarchy(out var wrapper, out var anchor, out var visualSr);
            try
            {
                wrapper.localScale = Vector3.one;
                CardMainVisualPlacement.ApplyToRenderer(
                    visualSr,
                    anchor,
                    visualSr.sprite,
                    uniformScale: 1f,
                    offsetX: 0f,
                    offsetY: 0f);

                wrapper.localScale = Vector3.zero;
                CardMainVisualPlacement.ApplyToRenderer(
                    visualSr,
                    anchor,
                    visualSr.sprite,
                    uniformScale: 1f,
                    offsetX: 0f,
                    offsetY: 0f);

                // 局部空间 AABB：图标内容须落在 Mask 内（遗物模板 x 偏移场景下旧算法会钉到原点外）。
                var parent = visualSr.transform.parent;
                var maskLocal = Matrix4x4.TRS(
                    anchor.transform.localPosition,
                    anchor.transform.localRotation,
                    anchor.transform.localScale);
                var maskBounds = TransformAabb(maskLocal, anchor.GetComponent<SpriteRenderer>().sprite.bounds);
                var visualLocal = Matrix4x4.TRS(
                    visualSr.transform.localPosition,
                    visualSr.transform.localRotation,
                    visualSr.transform.localScale);
                var contentBounds = TransformAabb(visualLocal, visualSr.sprite.bounds);

                Assert.GreaterOrEqual(contentBounds.min.x, maskBounds.min.x - 0.001f, "icon min.x inside mask");
                Assert.LessOrEqual(contentBounds.max.x, maskBounds.max.x + 0.001f, "icon max.x inside mask");
                Assert.GreaterOrEqual(contentBounds.min.y, maskBounds.min.y - 0.001f, "icon min.y inside mask");
                Assert.LessOrEqual(contentBounds.max.y, maskBounds.max.y + 0.001f, "icon max.y inside mask");
                Assert.AreSame(parent, anchor.transform.parent);
            }
            finally
            {
                Object.DestroyImmediate(wrapper.gameObject);
            }
        }

        /// <summary>
        /// 按遗物卡标准模版偏移：Mask (-1.2208, 0.4039) scale (1.904, 1.524)；图标 (-1.21875, 0)。
        /// </summary>
        private static void BuildRelicLikeHierarchy(
            out Transform wrapper,
            out CardMainVisualMaskAnchor anchor,
            out SpriteRenderer visualSr)
        {
            var wrapperGo = new GameObject("bounce_wrapper");
            wrapper = wrapperGo.transform;

            var face = new GameObject("face");
            face.transform.SetParent(wrapper, false);

            var maskGo = new GameObject(CardMainVisualMaskAnchor.NodeName);
            maskGo.transform.SetParent(face.transform, false);
            maskGo.transform.localPosition = new Vector3(-1.2208f, 0.4039f, 0f);
            maskGo.transform.localScale = new Vector3(1.904f, 1.524f, 1f);
            var maskSr = maskGo.AddComponent<SpriteRenderer>();
            maskSr.sprite = CreateTestSprite(64, 64, Color.white);
            maskGo.AddComponent<SpriteMask>().sprite = maskSr.sprite;
            anchor = maskGo.AddComponent<CardMainVisualMaskAnchor>();

            var visualGo = new GameObject("遗物主图标");
            visualGo.transform.SetParent(face.transform, false);
            visualGo.transform.localPosition = new Vector3(-1.21875f, 0f, 0f);
            visualSr = visualGo.AddComponent<SpriteRenderer>();
            visualSr.sprite = CreateTestSprite(32, 40, Color.red);
        }

        private static Bounds TransformAabb(Matrix4x4 matrix, Bounds localBounds)
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
                        var p = matrix.MultiplyPoint3x4(corner);
                        min = Vector3.Min(min, p);
                        max = Vector3.Max(max, p);
                    }
                }
            }

            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }

        private static Sprite CreateTestSprite(int width, int height, Color fill)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = fill;
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
