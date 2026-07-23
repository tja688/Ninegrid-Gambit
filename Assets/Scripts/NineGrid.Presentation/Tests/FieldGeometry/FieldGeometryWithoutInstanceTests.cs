using System.Reflection;
using NineGrid.Cards;
using NineGrid.Presentation.Controllers;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.FieldGeometry
{
    /// <summary>
    /// V7 Seam3：Field/Battle Hook 解析在清掉静态 Instance 后仍可用。
    /// </summary>
    public sealed class FieldGeometryWithoutInstanceTests
    {
        private GroundFieldView _field;
        private FieldBattleView _battle;

        [SetUp]
        public void SetUp()
        {
            GroundFieldGeometryHook.Reset();
            FieldBattlePresentationHook.Reset();
            DestroyControllers();
            DestroyManagers();

            _field = new GameObject("GroundField_NoInstance").AddComponent<GroundFieldView>();
            _battle = new GameObject("FieldBattle_NoInstance").AddComponent<FieldBattleView>();
        }

        [TearDown]
        public void TearDown()
        {
            GroundFieldGeometryHook.Reset();
            FieldBattlePresentationHook.Reset();
            DestroyControllers();
            DestroyManagers();
        }

        [Test]
        public void HookResolvers_WorkWithoutStaticInstanceFields()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                Assert.IsNull(
                    typeof(GroundFieldView).GetField(
                        "_instance", BindingFlags.Static | BindingFlags.NonPublic));
                Assert.IsNull(
                    typeof(FieldBattleView).GetField(
                        "_instance", BindingFlags.Static | BindingFlags.NonPublic));

                GroundFieldGeometryHook.RequestWire(_field);
                FieldBattlePresentationHook.RequestWire(_battle);

                Assert.AreSame(_field, GroundFieldGeometryHook.FieldOrNull());
                Assert.AreSame(_battle, FieldBattlePresentationHook.BattleOrNull());
            }
        }

        private void DestroyManagers()
        {
            DestroyAllOfType<GroundFieldView>();
            DestroyAllOfType<FieldBattleView>();
            _field = null;
            _battle = null;
        }

        private static void DestroyControllers()
        {
            DestroyAllOfType<GroundFieldGeometryController>();
            DestroyAllOfType<FieldBattlePresentationController>();
        }

        private static void DestroyAllOfType<T>() where T : MonoBehaviour
        {
            var found = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            for (var i = 0; i < found.Length; i++)
            {
                if (found[i] != null)
                {
                    Object.DestroyImmediate(found[i].gameObject);
                }
            }
        }
    }
}
