using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// #101 / ADR-0023：场地面按世界点解析格号；运行时不写格位命中框 size / offset。
    /// </summary>
    public sealed class GroundFieldHitSurfaceTests
    {
        private GameObject _root;
        private GroundFieldHitSurface _surface;
        private readonly BoxCollider2D[] _colliders =
            new BoxCollider2D[GroundSlotTopology.MaxSlot + 1];

        [SetUp]
        public void SetUp()
        {
            PointerHitRegistry.ClearForTests();
            _root = new GameObject("GroundFieldHitSurfaceTests");
            _surface = _root.AddComponent<GroundFieldHitSurface>();

            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                var go = new GameObject("slot" + slot);
                go.transform.SetParent(_root.transform, false);
                // 3×3 网格，中心间距 5，避免邻格重叠。
                var row = (slot - 1) / 3;
                var col = (slot - 1) % 3;
                go.transform.position = new Vector3((col - 1) * 5f, (1 - row) * 5.5f, 0f);
                go.transform.localScale = new Vector3(2f, 2f, 2f);
                var box = go.AddComponent<BoxCollider2D>();
                box.size = new Vector2(1.625f, 2.0625f);
                box.offset = Vector2.zero;
                box.enabled = true;
                _colliders[slot] = box;
            }

            _surface.BindSlotColliders(_colliders);
        }

        [TearDown]
        public void TearDown()
        {
            PointerHitRegistry.ClearForTests();
            GroundFieldGeometryHook.Reset();
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        [Test]
        public void TryResolveSlotAtWorld_HitsAuthoredCollider_ReturnsSlot()
        {
            Assert.IsTrue(_surface.TryResolveSlotAtWorld(Vector2.zero, out var slot));
            Assert.AreEqual(5, slot);

            var slot1Center = (Vector2)_colliders[1].transform.position;
            Assert.IsTrue(_surface.TryResolveSlotAtWorld(slot1Center, out slot));
            Assert.AreEqual(1, slot);
        }

        [Test]
        public void TryResolveSlotAtWorld_GapBetweenSlots_Misses()
        {
            // slot5 与 slot6 之间的缝（世界 x≈2.5，均在框外）。
            Assert.IsFalse(_surface.TryResolveSlotAtWorld(new Vector2(2.5f, 0f), out var slot));
            Assert.AreEqual(0, slot);
        }

        [Test]
        public void BindSlotColliders_DoesNotMutateSizeOrOffset()
        {
            var authoredSize = new Vector2(1.625f, 2.0625f);
            var authoredOffset = new Vector2(0.1f, -0.2f);
            _colliders[3].size = authoredSize;
            _colliders[3].offset = authoredOffset;

            _surface.BindSlotColliders(_colliders);

            Assert.AreEqual(authoredSize, _colliders[3].size);
            Assert.AreEqual(authoredOffset, _colliders[3].offset);
        }

        [Test]
        public void GroundSlotHitProxy_Configure_DoesNotWriteSizeOrOffset()
        {
            var go = new GameObject("legacySlot");
            try
            {
                var box = go.AddComponent<BoxCollider2D>();
                box.size = new Vector2(1.625f, 2.0625f);
                box.offset = new Vector2(0.25f, 0.5f);
                var proxy = go.AddComponent<GroundSlotHitProxy>();
                proxy.Configure(4);
                Assert.AreEqual(new Vector2(1.625f, 2.0625f), box.size);
                Assert.AreEqual(new Vector2(0.25f, 0.5f), box.offset);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void HitTypePriority_IsDeclaredFieldSurface()
        {
            Assert.AreEqual(PointerHitSurfacePriorities.Field, _surface.HitTypePriority);
        }

        [Test]
        public void DisabledSlotCollider_IsNotResolved_Defensive()
        {
            // 生产路径九框恒开；此测仅锁定「禁用框不参与解析」的防护断言。
            _colliders[5].enabled = false;
            Assert.IsFalse(_surface.TryResolveSlotAtWorld(Vector2.zero, out _));
        }

        [Test]
        public void AllEnabledSlots_AreIndependentlyResolvable()
        {
            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                Assert.IsTrue(_colliders[slot].enabled, "slot " + slot + " 应恒开");
                var center = (Vector2)_colliders[slot].transform.position;
                Assert.IsTrue(_surface.TryResolveSlotAtWorld(center, out var resolved));
                Assert.AreEqual(slot, resolved);
            }
        }

        [Test]
        public void RefreshPointerHover_CrossSlotWithoutExit_SwitchesClaimant()
        {
            // 回归：单一场地面上跨格时 Router 不重入 Enter，须 RefreshPointerHover 切认领者。
            PointerHitRegistry.ClearForTests();
            GroundFieldGeometryHook.Reset();

            using (PresentationArchitectureFixture.CreateBare())
            {
                var fieldGo = new GameObject("Field_CrossSlotHover");
                var field = fieldGo.AddComponent<GroundFieldView>();
                var system = new GroundFieldGeometrySystem();
                NineGridArchitecture.Interface.RegisterSystem<IGroundFieldGeometrySystem>(system);
                system.Bind(field);
                GroundFieldGeometryHook.ResolveField = () => field;

                var enterA = 0;
                var exitA = 0;
                var enterB = 0;
                var exitB = 0;
                Assert.IsTrue(field.TryClaimSlot(
                    1,
                    new SlotClaimant(new object(), "A", () => { }, () => enterA++, () => exitA++)));
                Assert.IsTrue(field.TryClaimSlot(
                    3,
                    new SlotClaimant(new object(), "B", () => { }, () => enterB++, () => exitB++)));

                Assert.IsTrue(_surface.TryResolveSlotAtWorld(
                    (Vector2)_colliders[1].transform.position,
                    out _));
                _surface.HandlePointerEnter();
                Assert.AreEqual(1, enterA);
                Assert.AreEqual(0, enterB);

                Assert.IsTrue(_surface.TryResolveSlotAtWorld(
                    (Vector2)_colliders[3].transform.position,
                    out _));
                _surface.RefreshPointerHover();
                Assert.AreEqual(1, exitA, "跨格须先 Exit 旧认领者");
                Assert.AreEqual(1, enterB, "跨格须 Enter 新认领者");
                Assert.AreEqual(0, exitB);

                _surface.RefreshPointerHover();
                Assert.AreEqual(1, enterB, "同格同认领者不得重复 Enter");

                Object.DestroyImmediate(fieldGo);
                GroundFieldGeometryHook.Reset();
            }
        }

        [Test]
        public void RefreshPointerHover_ClaimReleasedUnderCursor_ClearsHover()
        {
            PointerHitRegistry.ClearForTests();
            GroundFieldGeometryHook.Reset();

            using (PresentationArchitectureFixture.CreateBare())
            {
                var fieldGo = new GameObject("Field_ClaimReleased");
                var field = fieldGo.AddComponent<GroundFieldView>();
                var system = new GroundFieldGeometrySystem();
                NineGridArchitecture.Interface.RegisterSystem<IGroundFieldGeometrySystem>(system);
                system.Bind(field);
                GroundFieldGeometryHook.ResolveField = () => field;

                var exitCount = 0;
                var owner = new object();
                Assert.IsTrue(field.TryClaimSlot(
                    5,
                    new SlotClaimant(owner, "tip", () => { }, null, () => exitCount++)));

                Assert.IsTrue(_surface.TryResolveSlotAtWorld(Vector2.zero, out _));
                _surface.HandlePointerEnter();
                Assert.IsTrue(field.ReleaseSlotClaim(5, owner));
                _surface.RefreshPointerHover();
                Assert.AreEqual(1, exitCount);

                Object.DestroyImmediate(fieldGo);
                GroundFieldGeometryHook.Reset();
            }
        }
    }
}
