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
        private GroundFieldManagerSingleton _field;
        private FieldBattleManagerSingleton _battle;

        [SetUp]
        public void SetUp()
        {
            GroundFieldGeometryHook.Reset();
            FieldBattlePresentationHook.Reset();
            DestroyControllers();
            DestroyManagers();

            _field = new GameObject("GroundField_NoInstance").AddComponent<GroundFieldManagerSingleton>();
            _battle = new GameObject("FieldBattle_NoInstance").AddComponent<FieldBattleManagerSingleton>();
        }

        [TearDown]
        public void TearDown()
        {
            GroundFieldGeometryHook.Reset();
            FieldBattlePresentationHook.Reset();
            DestroyControllers();
            DestroyManagers();
            ResetStaticInstances();
        }

        [Test]
        public void HookResolvers_SurviveClearedSingletonInstances()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                GroundFieldGeometryHook.RequestWire(_field);
                FieldBattlePresentationHook.RequestWire(_battle);
                ResetStaticInstances();

                Assert.IsNull(GetStaticInstance(typeof(GroundFieldManagerSingleton), "_instance"));
                Assert.IsNull(GetStaticInstance(typeof(FieldBattleManagerSingleton), "_instance"));

                Assert.AreSame(_field, GroundFieldGeometryHook.FieldOrNull());
                Assert.AreSame(_battle, FieldBattlePresentationHook.BattleOrNull());
            }
        }

        private void DestroyManagers()
        {
            DestroyAllOfType<GroundFieldManagerSingleton>();
            DestroyAllOfType<FieldBattleManagerSingleton>();
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

        private static void ResetStaticInstances()
        {
            SetStaticInstance(typeof(GroundFieldManagerSingleton), "_instance", null);
            SetStaticInstance(typeof(FieldBattleManagerSingleton), "_instance", null);
        }

        private static object GetStaticInstance(System.Type type, string fieldName)
        {
            var field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
            return field?.GetValue(null);
        }

        private static void SetStaticInstance(System.Type type, string fieldName, object value)
        {
            var field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
            field?.SetValue(null, value);
        }
    }
}
