using NUnit.Framework;
using NineGrid.Flow;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Cards
{
    /// <summary>
    /// 右键详述常规面板占位按道具卡原点布局；遗物卡面预制体内容偏左时须补偿。
    /// </summary>
    public sealed class CardInspectLiveFaceAlignTests
    {
        [Test]
        public void AlignLiveFace_RelicLikeFrameOffset_RecentersFrameToOrigin()
        {
            var face = new GameObject("face");
            var frame = new GameObject("卡框");
            try
            {
                frame.transform.SetParent(face.transform, false);
                frame.transform.localPosition = new Vector3(-1.21875f, 0.0625f, 0f);

                CardInspectOverlayPresenter.AlignLiveFaceToPlaceholderOrigin(face.transform);

                Assert.AreEqual(
                    new Vector3(1.21875f, -0.0625f, 0f),
                    face.transform.localPosition);
                Assert.AreEqual(
                    Vector3.zero,
                    face.transform.localPosition + frame.transform.localPosition,
                    "卡框应落在占位本地原点");
            }
            finally
            {
                Object.DestroyImmediate(face);
            }
        }

        [Test]
        public void AlignLiveFace_ItemLikeFrameAtOrigin_StaysAtZero()
        {
            var face = new GameObject("face");
            var frame = new GameObject("卡框");
            try
            {
                frame.transform.SetParent(face.transform, false);
                frame.transform.localPosition = Vector3.zero;

                CardInspectOverlayPresenter.AlignLiveFaceToPlaceholderOrigin(face.transform);

                Assert.AreEqual(Vector3.zero, face.transform.localPosition);
            }
            finally
            {
                Object.DestroyImmediate(face);
            }
        }

        [Test]
        public void AlignLiveFace_BorderNameFallback_Recenters()
        {
            var face = new GameObject("face");
            var frame = new GameObject("Card_Border_rectangle_bronze");
            try
            {
                frame.transform.SetParent(face.transform, false);
                frame.transform.localPosition = new Vector3(-1.21875f, 0.0625f, 0f);

                CardInspectOverlayPresenter.AlignLiveFaceToPlaceholderOrigin(face.transform);

                Assert.AreEqual(
                    Vector3.zero,
                    face.transform.localPosition + frame.transform.localPosition);
            }
            finally
            {
                Object.DestroyImmediate(face);
            }
        }
    }
}
