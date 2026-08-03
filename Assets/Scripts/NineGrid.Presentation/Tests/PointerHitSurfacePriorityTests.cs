using System.Text.RegularExpressions;
using NineGrid.Flow;
using NineGrid.Presentation.Platform;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// #101 / ADR-0023：表面优先级显式互异；Router 同分报装配错误；覆层不再靠 TypePriority=100 抢。
    /// </summary>
    public sealed class PointerHitSurfacePriorityTests
    {
        private GameObject _routerGo;
        private GameObject _camGo;
        private PointerHitRouter _router;
        private FakePointerSource _pointer;
        private readonly System.Collections.Generic.List<GameObject> _targets =
            new System.Collections.Generic.List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            PointerHitRegistry.ClearForTests();
            WorldPointerUtility.ClearOverrideSource();
            _targets.Clear();

            _camGo = new GameObject("TestCamera");
            var cam = _camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            _camGo.tag = "MainCamera";

            _routerGo = new GameObject("PointerHitRouter");
            _router = _routerGo.AddComponent<PointerHitRouter>();

            _pointer = new FakePointerSource();
            WorldPointerUtility.SetOverrideSource(_pointer);
        }

        [TearDown]
        public void TearDown()
        {
            WorldPointerUtility.ClearOverrideSource();
            PointerHitRegistry.ClearForTests();
            for (var i = 0; i < _targets.Count; i++)
            {
                if (_targets[i] != null)
                {
                    Object.DestroyImmediate(_targets[i]);
                }
            }

            _targets.Clear();
            if (_routerGo != null)
            {
                Object.DestroyImmediate(_routerGo);
            }

            if (_camGo != null)
            {
                Object.DestroyImmediate(_camGo);
            }
        }

        [Test]
        public void DeclaredSurfacePriorities_AreDistinct()
        {
            Assert.IsTrue(PointerHitSurfacePriorityValidator.AreDeclaredSurfacePrioritiesDistinct());
            Assert.AreNotEqual(PointerHitSurfacePriorities.Field, PointerHitSurfacePriorities.Hand);
            Assert.AreNotEqual(PointerHitSurfacePriorities.Field, PointerHitSurfacePriorities.Overlay);
            Assert.AreNotEqual(PointerHitSurfacePriorities.Hand, PointerHitSurfacePriorities.Overlay);
        }

        [Test]
        public void OverlayPriority_IsBelowFieldAndHand()
        {
            Assert.Less(PointerHitSurfacePriorities.Overlay, PointerHitSurfacePriorities.Field);
            Assert.Less(PointerHitSurfacePriorities.Overlay, PointerHitSurfacePriorities.Hand);
        }

        [Test]
        public void FindDuplicateScorePairs_ReportsSameSortAndType()
        {
            var a = CreateTarget("A", Vector2.zero, sortOrder: 0, typePriority: 10);
            var b = CreateTarget("B", Vector2.zero, sortOrder: 0, typePriority: 10);
            var hits = PointerHitSurfacePriorityValidator.FindDuplicateScorePairs(
                new IPointerHitTarget[] { a, b });
            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(0, hits[0].HitSortOrder);
            Assert.AreEqual(10, hits[0].HitTypePriority);
        }

        [Test]
        public void Tick_SameSortAndType_LogsAssemblyError()
        {
            CreateTarget("First", Vector2.zero, sortOrder: 3, typePriority: 7);
            CreateTarget("Second", Vector2.zero, sortOrder: 3, typePriority: 7);

            LogAssert.Expect(LogType.Error, new Regex("装配错误"));
            _pointer.Screen = WorldToScreen(Vector3.zero);
            _router.Tick();
        }

        [Test]
        public void Tick_FieldBeatsOverlay_WhenBothOverlap()
        {
            var field = CreateTarget(
                "Field",
                Vector2.zero,
                sortOrder: 0,
                typePriority: PointerHitSurfacePriorities.Field);
            var overlay = CreateTarget(
                "Overlay",
                Vector2.zero,
                sortOrder: BattleUiDimmerOverlay.HitSort,
                typePriority: PointerHitSurfacePriorities.Overlay);

            _pointer.Screen = WorldToScreen(Vector3.zero);
            _router.Tick();
            Assert.AreEqual(1, field.EnterCount);
            Assert.AreEqual(0, overlay.EnterCount);
        }

        [Test]
        public void Overlay_DoesNotUseLegacyTypePriority100()
        {
            Assert.AreNotEqual(100, PointerHitSurfacePriorities.Overlay);
            Assert.AreEqual(0, BattleUiDimmerOverlay.HitSort);
            Assert.Less(BattleUiDimmerOverlay.HitSort, BattleUiDimmerOverlay.CloseHitSort);
        }

        private FakeHitTarget CreateTarget(string name, Vector2 pos, int sortOrder, int typePriority)
        {
            var go = new GameObject(name);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1.5f, 1.5f);
            var target = go.AddComponent<FakeHitTarget>();
            target.Configure(box, sortOrder, typePriority);
            PointerHitRegistry.Register(target);
            _targets.Add(go);
            return target;
        }

        private static Vector2 WorldToScreen(Vector3 world)
        {
            var cam = Camera.main;
            var screen = cam.WorldToScreenPoint(world);
            return new Vector2(screen.x, screen.y);
        }

        private sealed class FakePointerSource : WorldPointerUtility.IPointerSource
        {
            public Vector2 Screen;
            public bool Held;
            public bool PressPrimaryThisFrame;
            public bool ReleasePrimaryThisFrame;
            public bool PressSecondaryThisFrame;

            public bool TryGetScreenPosition(out Vector2 screen)
            {
                screen = Screen;
                return true;
            }

            public bool IsPrimaryHeld => Held;
            public bool WasPrimaryPressedThisFrame => PressPrimaryThisFrame;
            public bool WasPrimaryReleasedThisFrame => ReleasePrimaryThisFrame;
            public bool WasSecondaryPressedThisFrame => PressSecondaryThisFrame;
        }

        private sealed class FakeHitTarget : MonoBehaviour, IPointerHitTarget
        {
            private Collider2D _collider;
            private int _sort;
            private int _type;

            public int EnterCount { get; private set; }
            public int ExitCount { get; private set; }
            public int DownCount { get; private set; }

            public Collider2D HitCollider => _collider;
            public int HitSortOrder => _sort;
            public int HitTypePriority => _type;

            public void Configure(Collider2D collider, int sort, int type)
            {
                _collider = collider;
                _sort = sort;
                _type = type;
            }

            public void HandlePointerEnter() => EnterCount++;
            public void HandlePointerExit() => ExitCount++;
            public void HandlePointerDown() => DownCount++;
        }
    }
}
