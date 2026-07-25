using NUnit.Framework;
using NineGrid.Cards.Anim;
using UnityEngine;

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
