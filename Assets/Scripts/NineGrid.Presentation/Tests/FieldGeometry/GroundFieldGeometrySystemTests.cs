using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Presentation.Commands;
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
        public void Bind_OwnsGeometryWithoutExposingViewAsField()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                var system = RegisterSystem();
                system.Bind(_field);

                Assert.IsTrue(system.IsBound);
                Assert.IsFalse(system.IsFieldBusy);
                Assert.AreEqual(0, system.ActiveDealFlightCount);
                Assert.IsTrue(system.IsEmpty(1));
                Assert.IsTrue(system.TryRegisterCardAtSlot(2, 9001));
                Assert.IsTrue(system.TryGetSlotOf(9001, out var slot));
                Assert.AreEqual(2, slot);
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
        public void OccupancyCommands_PlaceAndVacateThroughSystem()
        {
            using (var fixture = PresentationArchitectureFixture.CreateBare())
            {
                var system = RegisterSystem();
                system.Bind(_field);

                // 无 ManagedCard 时 Place 失败；Vacate 在空格返回 false。
                Assert.IsFalse(fixture.Architecture.SendCommand(new VacateGroundOccupancyCommand(3)));
                Assert.IsTrue(system.TryRegisterCardAtSlot(3, 9002));
                Assert.IsTrue(fixture.Architecture.SendCommand(new VacateGroundOccupancyCommand(3, skipBusyGuard: true)));
                Assert.IsTrue(system.IsEmpty(3));

                var snapshot = fixture.Architecture.SendQuery(new GroundFieldSnapshotQuery());
                Assert.IsNotNull(snapshot);
                Assert.AreEqual(0, snapshot.OccupiedCount);
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
                Assert.IsTrue(system.IsBound);
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
