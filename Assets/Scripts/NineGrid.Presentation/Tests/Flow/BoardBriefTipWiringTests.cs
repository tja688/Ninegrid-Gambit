using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NineGrid.Cards;
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
    /// 简要解释悬停：场景面板绑定 + 命中优先级高于场地卡底盘。
    /// </summary>
    public sealed class BoardBriefTipWiringTests
    {
        private static readonly string PresentationRoot = Path.GetFullPath(
            Path.Combine(Application.dataPath, "Scripts", "NineGrid.Presentation"));

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
        public void ConsumerBoardHitProxies_OutrankGroundCardHitProxy()
        {
            Assert.Greater(ReadTypePriority(typeof(BoardBriefTipHitProxy)), 30);
            Assert.Greater(ReadTypePriority(typeof(ShopBoardHitProxy)), 30);
            Assert.Greater(ReadTypePriority(typeof(TavernBoardHitProxy)), 30);
            Assert.Greater(ReadTypePriority(typeof(RewardBoardHitProxy)), 30);
        }

        [Test]
        public void ProductionSources_DocumentHitPriorityAboveGroundCard()
        {
            var shop = File.ReadAllText(Path.Combine(
                PresentationRoot, "Flow", "ShopBoard", "ShopBoardHitProxy.cs"));
            Assert.IsTrue(
                Regex.IsMatch(shop, @"TypePriority\s*=\s*4\d"),
                "ShopBoardHitProxy 须高于 GroundCardHitProxy(30)");
        }

        private static string WriteHover(BoardBriefTipPresenter presenter, string text)
        {
            presenter.ShowHover(text);
            return presenter.BodyTextOrNull != null ? presenter.BodyTextOrNull.text : null;
        }

        private static int ReadTypePriority(Type proxyType)
        {
            var field = proxyType.GetField(
                "TypePriority",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, proxyType.Name + " missing TypePriority");
            return (int)field.GetValue(null);
        }
    }
}
