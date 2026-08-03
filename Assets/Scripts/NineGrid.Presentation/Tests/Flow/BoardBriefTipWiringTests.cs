using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Flow.BoardBriefTip;
using NineGrid.Flow.RewardBoard;
using NineGrid.Flow.ShopBoard;
using NineGrid.Flow.TavernBoard;
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Flow
{
    /// <summary>
    /// 简要解释悬停：场景面板绑定；落格代理经认领登记；悬停与点击同源（#102+#103）。
    /// </summary>
    public sealed class BoardBriefTipWiringTests
    {
        [TearDown]
        public void TearDown()
        {
            PointerHitRegistry.ClearForTests();
            GroundFieldGeometryHook.Reset();
            var panels = UnityEngine.Object.FindObjectsByType<BoardBriefTipPresenter>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var i = 0; i < panels.Length; i++)
            {
                if (panels[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(panels[i].gameObject);
                }
            }
        }

        [Test]
        public void EnsureExists_PrefersNamedScenePanel_OverOrphanFallback()
        {
            var orphan = new GameObject(nameof(BoardBriefTipPresenter));
            orphan.AddComponent<BoardBriefTipPresenter>();

            var panel = new GameObject(BoardBriefTipPresenter.PanelObjectName);
            var tmpGo = new GameObject("标准世界文字");
            tmpGo.transform.SetParent(panel.transform, false);
            tmpGo.AddComponent<TextMeshPro>();

            var presenter = BoardBriefTipPresenter.EnsureExists();
            Assert.AreSame(panel, presenter.gameObject);
            Assert.IsNotNull(presenter.BodyTextOrNull);
            Assert.AreEqual("PROBE", WriteHover(presenter, "PROBE"));
        }

        [Test]
        public void BoardHitProxies_AreNotPointerHitTargets()
        {
            Assert.IsFalse(typeof(IPointerHitTarget).IsAssignableFrom(typeof(BoardBriefTipHitProxy)));
            Assert.IsFalse(typeof(IPointerHitTarget).IsAssignableFrom(typeof(ShopBoardHitProxy)));
            Assert.IsFalse(typeof(IPointerHitTarget).IsAssignableFrom(typeof(TavernBoardHitProxy)));
            Assert.IsFalse(typeof(IPointerHitTarget).IsAssignableFrom(typeof(RewardBoardHitProxy)));
            Assert.IsFalse(typeof(IPointerHitTarget).IsAssignableFrom(typeof(GroundCardHitProxy)));
        }

        [Test]
        public void FieldSurface_HoverTipAndActivate_ShareSameClaimant()
        {
            PointerHitRegistry.ClearForTests();
            GroundFieldGeometryHook.Reset();

            using (PresentationArchitectureFixture.CreateBare())
            {
                var fieldGo = new GameObject("Field_Hover同源");
                var field = fieldGo.AddComponent<GroundFieldView>();
                var system = new GroundFieldGeometrySystem();
                NineGridArchitecture.Interface.RegisterSystem<IGroundFieldGeometrySystem>(system);
                system.Bind(field);
                GroundFieldGeometryHook.ResolveField = () => field;

                var surfaceGo = new GameObject("Surface");
                var surface = surfaceGo.AddComponent<GroundFieldHitSurface>();
                var slotGo = new GameObject("slot5");
                slotGo.transform.SetParent(surfaceGo.transform, false);
                slotGo.transform.position = Vector3.zero;
                var box = slotGo.AddComponent<BoxCollider2D>();
                box.size = new Vector2(2f, 2f);
                var colliders = new BoxCollider2D[GroundSlotTopology.MaxSlot + 1];
                colliders[5] = box;
                surface.BindSlotColliders(colliders);

                var panel = new GameObject(BoardBriefTipPresenter.PanelObjectName);
                var tmpGo = new GameObject("标准世界文字");
                tmpGo.transform.SetParent(panel.transform, false);
                tmpGo.AddComponent<TextMeshPro>();

                var activateCount = 0;
                Assert.IsTrue(field.TryClaimSlot(
                    5,
                    new SlotClaimant(new object(), "同源文案", () => activateCount++)));

                Assert.IsTrue(surface.TryResolveSlotAtWorld(Vector2.zero, out var slot));
                Assert.AreEqual(5, slot);
                surface.HandlePointerEnter();
                Assert.AreEqual("同源文案", BoardBriefTipPresenter.EnsureExists().BodyTextOrNull.text);

                surface.HandlePointerDown();
                Assert.AreEqual(1, activateCount, "点击须激活同一认领者");

                surface.HandlePointerExit();
                Object.DestroyImmediate(fieldGo);
                Object.DestroyImmediate(surfaceGo);
                Object.DestroyImmediate(panel);
            }
        }

        private static string WriteHover(BoardBriefTipPresenter presenter, string text)
        {
            presenter.ShowHover(text);
            return presenter.BodyTextOrNull != null ? presenter.BodyTextOrNull.text : null;
        }
    }
}
