using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Presentation.Controllers;
using NineGrid.Presentation.Queries;
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.FieldGeometry
{
    /// <summary>
    /// V7 Seam1：IGroundFieldGeometrySystem 是场地几何注册的单一 QF 所有者。
    /// </summary>
    public sealed class GroundFieldGeometrySystemTests
    {
        private GroundFieldManagerSingleton _field;

        [SetUp]
        public void SetUp()
        {
            GroundFieldGeometryHook.Reset();
            DestroyControllers();
            DestroyAllFields();
            _field = new GameObject("GroundField_V7").AddComponent<GroundFieldManagerSingleton>();
        }

        [TearDown]
        public void TearDown()
        {
            GroundFieldGeometryHook.Reset();
            DestroyControllers();
            if (_field != null)
            {
                Object.DestroyImmediate(_field.gameObject);
                _field = null;
            }

            DestroyAllFields();
        }

        [Test]
        public void Bind_ExposesFieldWithoutSingletonLookup()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                var system = RegisterSystem();
                system.Bind(_field);

                Assert.IsTrue(system.IsBound);
                Assert.AreSame(_field, system.Field);
                Assert.IsFalse(system.IsFieldBusy);
                Assert.AreEqual(0, system.ActiveDealFlightCount);
            }
        }

        [Test]
        public void TryGetCardAtSlotQuery_ReadsThroughGeometrySystem()
        {
            using (var fixture = PresentationArchitectureFixture.CreateBare())
            {
                var system = RegisterSystem();
                system.Bind(_field);

                Assert.IsNull(fixture.Architecture.SendQuery(new TryGetCardAtSlotQuery(1)));
                Assert.IsFalse(fixture.Architecture.SendQuery(new IsFieldBusyQuery()));
                Assert.IsTrue(system.IsEmpty(1));
            }
        }

        [Test]
        public void Controller_BindField_WiresHookAndSystem()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                var host = new GameObject(nameof(GroundFieldGeometryController));
                var controller = host.AddComponent<GroundFieldGeometryController>();
                controller.BindField(_field);

                var system = NineGridArchitecture.Interface.GetSystem<IGroundFieldGeometrySystem>();
                Assert.IsNotNull(system);
                Assert.AreSame(_field, system.Field);
                Assert.AreSame(_field, GroundFieldGeometryHook.FieldOrNull());

                Object.DestroyImmediate(host);
            }
        }

        private static IGroundFieldGeometrySystem RegisterSystem()
        {
            var system = new GroundFieldGeometrySystem();
            NineGridArchitecture.Interface.RegisterSystem<IGroundFieldGeometrySystem>(system);
            return system;
        }

        private static void DestroyControllers()
        {
            var found = Object.FindObjectsByType<GroundFieldGeometryController>(FindObjectsSortMode.None);
            for (var i = 0; i < found.Length; i++)
            {
                if (found[i] != null)
                {
                    Object.DestroyImmediate(found[i].gameObject);
                }
            }
        }

        private static void DestroyAllFields()
        {
            var found = Object.FindObjectsByType<GroundFieldManagerSingleton>(FindObjectsSortMode.None);
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
