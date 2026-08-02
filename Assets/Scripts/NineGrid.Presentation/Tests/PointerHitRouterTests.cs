using NineGrid.Flow;
using NineGrid.Presentation.Platform;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    public sealed class PointerHitRouterTests
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
        public void Tick_SynthesizesEnterExitAndDownEdges()
        {
            var a = CreateTarget("A", new Vector2(0f, 0f), sortOrder: 1, typePriority: 1);
            var b = CreateTarget("B", new Vector2(2f, 0f), sortOrder: 1, typePriority: 1);

            _pointer.Screen = WorldToScreen(new Vector3(0f, 0f, 0f));
            _router.Tick();
            Assert.AreEqual(1, a.EnterCount);
            Assert.AreEqual(0, a.ExitCount);
            Assert.AreEqual(0, a.DownCount);

            _pointer.Screen = WorldToScreen(new Vector3(2f, 0f, 0f));
            _router.Tick();
            Assert.AreEqual(1, a.ExitCount);
            Assert.AreEqual(1, b.EnterCount);

            _pointer.PressPrimaryThisFrame = true;
            _router.Tick();
            Assert.AreEqual(1, b.DownCount);
            Assert.AreEqual(0, a.DownCount);
        }

        [Test]
        public void Tick_PrefersHigherSortThenTypePriority()
        {
            CreateTarget("LowSort", new Vector2(0f, 0f), sortOrder: 1, typePriority: 99);
            var highSort = CreateTarget("HighSort", new Vector2(0f, 0f), sortOrder: 5, typePriority: 1);

            _pointer.Screen = WorldToScreen(new Vector3(0f, 0f, 0f));
            _router.Tick();
            Assert.AreEqual(1, highSort.EnterCount);

            PointerHitRegistry.ClearForTests();
            _router.ResetHoverStateForTests();

            var lowType = CreateTarget("LowType", new Vector2(0f, 0f), sortOrder: 3, typePriority: 1);
            var highType = CreateTarget("HighType", new Vector2(0f, 0f), sortOrder: 3, typePriority: 50);
            _router.Tick();
            Assert.AreEqual(1, highType.EnterCount);
            Assert.AreEqual(0, lowType.EnterCount);
        }

        [Test]
        public void Tick_SecondaryOnRelicSlot_DiscardsEvenUnderHigherSortOverlay()
        {
            var discarded = new System.Collections.Generic.List<string>();
            RelicHudHook.TryDiscardRelic = defId =>
            {
                discarded.Add(defId);
                return true;
            };

            try
            {
                var relicGo = new GameObject("RelicSlot");
                relicGo.SetActive(false);
                relicGo.transform.position = Vector3.zero;
                var box = relicGo.AddComponent<BoxCollider2D>();
                box.size = new Vector2(1.5f, 1.5f);
                var proxy = relicGo.AddComponent<ContentIconSlotHitProxy>();
                proxy.DefId = "relic.wood_shield";
                relicGo.SetActive(true);
                PointerHitRegistry.Register(proxy);
                _targets.Add(relicGo);

                CreateTarget("Dimmer", Vector2.zero, sortOrder: BattleUiDimmerOverlay.HitSort, typePriority: 100);

                var registered = false;
                for (var i = 0; i < PointerHitRegistry.All.Count; i++)
                {
                    if (ReferenceEquals(PointerHitRegistry.All[i], proxy))
                    {
                        registered = true;
                        break;
                    }
                }

                Assert.IsTrue(registered, "Relic proxy must be registered before secondary tick.");

                _pointer.Screen = WorldToScreen(Vector3.zero);
                _pointer.PressSecondaryThisFrame = true;
                _router.Tick();

                Assert.AreEqual(1, discarded.Count, "Expected discard under dimmer; registryCount=" + PointerHitRegistry.All.Count);
                Assert.AreEqual("relic.wood_shield", discarded[0]);
            }
            finally
            {
                RelicHudHook.TryDiscardRelic = null;
            }
        }

        [Test]
        public void MitigationInfo_AndAdrMarker_Exist()
        {
            Assert.AreEqual("0006", WindowsHighPollingMouseMitigationInfo.AdrId);
            Assert.AreEqual("UUM-142550", WindowsHighPollingMouseMitigationInfo.UnityIssueId);
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
