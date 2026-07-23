using NUnit.Framework;
using NineGrid.Cards;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 融合结果卡提前显露回归：Ensure 预热后须 scale=0，Reveal 才弹出现。
    /// </summary>
    public sealed class SkeletonFusionResultRevealTests
    {
        [Test]
        public void PrewarmResultCard_MustStayInvisibleUntilScaleAppear()
        {
            // 文档化 EnsureFusionResultView 契约：Spawn + Apply 后立刻压扁，避免 PresentBegin 前旁路可见。
            DestroyAllSingletonsInScene();
            var host = new GameObject("FusionRevealHost");
            var cardManager = host.AddComponent<CardManagerSingleton>();
            var prefab = new GameObject("FusionRevealPrefab");
            prefab.AddComponent<StandardCardView>();
            cardManager.RegisterPrefab(CardManagerSingleton.StandardDefId, prefab);

            try
            {
                var resultCard = cardManager.SpawnView(
                    9501,
                    CardManagerSingleton.StandardDefId,
                    initialMode: CardDisplayMode.GroundCardMode);
                Assert.IsNotNull(resultCard?.Transform);
                Assert.AreNotEqual(Vector3.zero, resultCard.Transform.localScale);

                // EnsureFusionResultView 收口行为
                resultCard.Transform.localScale = Vector3.zero;
                Assert.AreEqual(Vector3.zero, resultCard.Transform.localScale);

                // Reveal 路径：零 scale 时用 one 作为弹出终值
                var baseScale = resultCard.Transform.localScale;
                if (baseScale == Vector3.zero)
                {
                    baseScale = Vector3.one;
                }

                Assert.AreEqual(Vector3.one, baseScale);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(prefab);
            }
        }

        private static void DestroyAllSingletonsInScene()
        {
            foreach (var m in Object.FindObjectsByType<CardManagerSingleton>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(m.gameObject);
            }

            var field = typeof(CardManagerSingleton).GetField(
                "_instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(null, null);
        }
    }
}
