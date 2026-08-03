using NineGrid.Cards;
using NineGrid.Flow;
using NineGrid.Flow.BoardBriefTip;
using NineGrid.Flow.RewardBoard;
using NineGrid.Flow.ShopBoard;
using NineGrid.Flow.TavernBoard;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Flow
{
    /// <summary>
    /// 简要解释悬停：场景面板绑定；落格代理经认领登记，不再自建命中优先级（#102）。
    /// </summary>
    public sealed class BoardBriefTipWiringTests
    {
        [TearDown]
        public void TearDown()
        {
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

        private static string WriteHover(BoardBriefTipPresenter presenter, string text)
        {
            presenter.ShowHover(text);
            return presenter.BodyTextOrNull != null ? presenter.BodyTextOrNull.text : null;
        }
    }
}
