using NineGrid.Flow;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 详述描述内联 sprite 代号解析：相邻多图标不得串到前一个标签。
    /// </summary>
    public sealed class CardInspectInlineSpriteHoverTests
    {
        private const string DualSpriteSource =
            "<sprite name=\"attack\">+<sprite name=\"armor\">";

        [Test]
        public void ParseSpriteName_FromFirstTag_ReturnsAttack()
        {
            Assert.IsTrue(
                CardInspectInlineSpriteHoverUtility.TryParseSpriteNameFromSource(
                    DualSpriteSource,
                    0,
                    out var code));
            Assert.AreEqual("attack", code);
        }

        [Test]
        public void ParseSpriteName_FromSecondTag_ReturnsArmor_NotFirst()
        {
            var secondTagIndex = DualSpriteSource.IndexOf(
                "<sprite name=\"armor\">",
                System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(secondTagIndex, 0);

            Assert.IsTrue(
                CardInspectInlineSpriteHoverUtility.TryParseSpriteNameFromSource(
                    DualSpriteSource,
                    secondTagIndex,
                    out var code));
            Assert.AreEqual("armor", code);
            Assert.AreNotEqual("attack", code);
        }

        [Test]
        public void ParseSpriteName_FromMiddleOfSecondTag_StillReturnsArmor()
        {
            var secondTagIndex = DualSpriteSource.IndexOf(
                "name=\"armor\"",
                System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(secondTagIndex, 0);

            Assert.IsTrue(
                CardInspectInlineSpriteHoverUtility.TryParseSpriteNameFromSource(
                    DualSpriteSource,
                    secondTagIndex,
                    out var code));
            Assert.AreEqual("armor", code);
        }

        [Test]
        public void ContainsPointInQuadWorld_InsideQuad_ReturnsTrue()
        {
            var bl = new UnityEngine.Vector3(0f, 0f, 0f);
            var tl = new UnityEngine.Vector3(0f, 1f, 0f);
            var tr = new UnityEngine.Vector3(1f, 1f, 0f);
            var br = new UnityEngine.Vector3(1f, 0f, 0f);
            var inside = new UnityEngine.Vector3(0.5f, 0.5f, 0f);

            Assert.IsTrue(
                CardInspectInlineSpriteHoverUtility.ContainsPointInQuadWorld(
                    inside,
                    bl,
                    tl,
                    tr,
                    br,
                    paddingWorld: 0f));
        }

        [Test]
        public void ContainsPointInQuadWorld_OutsideQuad_ReturnsFalse()
        {
            var bl = new UnityEngine.Vector3(0f, 0f, 0f);
            var tl = new UnityEngine.Vector3(0f, 1f, 0f);
            var tr = new UnityEngine.Vector3(1f, 1f, 0f);
            var br = new UnityEngine.Vector3(1f, 0f, 0f);
            var outside = new UnityEngine.Vector3(2f, 2f, 0f);

            Assert.IsFalse(
                CardInspectInlineSpriteHoverUtility.ContainsPointInQuadWorld(
                    outside,
                    bl,
                    tl,
                    tr,
                    br,
                    paddingWorld: 0f));
        }

        [Test]
        public void GlossaryRow_SetDefaultBodyColor_UsesConfiguredColorForUncoloredTerms()
        {
            var go = new UnityEngine.GameObject("TestRow");
            try
            {
                var tmp = go.AddComponent<TMPro.TextMeshPro>();
                tmp.color = UnityEngine.Color.white;
                var row = go.AddComponent<NineGrid.Cards.Presentation.CardInspectGlossaryRowView>();

                row.SetDefaultBodyColor(UnityEngine.Color.black);
                row.Bind("测试词条", "这是一段词条解释", hasColor: false, UnityEngine.Color.yellow);

                Assert.AreEqual(UnityEngine.Color.black, tmp.color);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void GlossaryRow_BindWithColor_UsesSpecifiedColor()
        {
            var go = new UnityEngine.GameObject("TestRow");
            try
            {
                var tmp = go.AddComponent<TMPro.TextMeshPro>();
                tmp.color = UnityEngine.Color.white;
                var row = go.AddComponent<NineGrid.Cards.Presentation.CardInspectGlossaryRowView>();

                row.SetDefaultBodyColor(UnityEngine.Color.black);
                row.Bind("测试词条", "这是一段词条解释", hasColor: true, UnityEngine.Color.red);

                Assert.AreEqual(UnityEngine.Color.red, tmp.color);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
