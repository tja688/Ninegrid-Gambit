using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Flow.BoardBriefTip;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.Flow
{
    public sealed class BoardBriefTipCopyTests
    {
        [Test]
        public void ForRoom_UsesDisplayName_AndInjectCountWhenPresent()
        {
            var bare = new RoomDefinition(RoomKind.Shop, "商店");
            Assert.AreEqual("商店", BoardBriefTipCopy.ForRoom(bare));

            var withInject = new RoomDefinition(RoomKind.Fountain, "恢复房")
                .AddOpeningInject(new RoomInjectDeclaration
                {
                    Side = RoomInjectSide.Player,
                    SourceKind = RoomInjectSourceKind.FixedCard,
                    CardDefId = "help.food_card",
                    Count = 1
                });
            Assert.AreEqual("恢复房：开局注入 1 项", BoardBriefTipCopy.ForRoom(withInject));
        }

        [Test]
        public void ForNavigation_UsesFixedCopy()
        {
            Assert.AreEqual("离开本房", BoardBriefTipCopy.ForNavigation(NavigationKind.Leave));
            Assert.AreEqual("前往下一层", BoardBriefTipCopy.ForNavigation(NavigationKind.GoDown));
            Assert.AreEqual(string.Empty, BoardBriefTipCopy.ForNavigation(NavigationKind.None));
        }

        [Test]
        public void ForOptionOrShelf_JoinsBriefAndPrice()
        {
            Assert.AreEqual("刷新货架 · 10 金币", BoardBriefTipCopy.ForOptionOrShelf("刷新货架", 10));
            Assert.AreEqual("10 金币", BoardBriefTipCopy.ForOptionOrShelf(null, 10));
            Assert.AreEqual("恢复药水", BoardBriefTipCopy.ForOptionOrShelf("恢复药水", null));
            Assert.AreEqual(string.Empty, BoardBriefTipCopy.ForOptionOrShelf("  ", null));
        }

        [Test]
        public void FormatFloorHint_UsesDisplayNode()
        {
            Assert.AreEqual("第 1 层 · 节点 1", BoardBriefTipCopy.FormatFloorHint(1, 0));
            Assert.AreEqual("第 2 层 · 节点 8", BoardBriefTipCopy.FormatFloorHint(2, 7));
            Assert.AreEqual("第 3 层 · 节点 5", BoardBriefTipCopy.FormatFloorHint(3, 4));
        }

        [Test]
        public void ForContentId_RoutesNavAndRoom()
        {
            Assert.AreEqual("离开本房", BoardBriefTipCopy.ForContentId("Leave"));
            Assert.AreEqual("前往下一层", BoardBriefTipCopy.ForContentId("GoDown"));
            Assert.AreEqual("返回上一层", BoardBriefTipCopy.ForContentId("GoUp"));

            Assert.AreEqual(
                "商店",
                BoardBriefTipCopy.ForContentId(
                    "Shop",
                    kind => kind == RoomKind.Shop ? new RoomDefinition(RoomKind.Shop, "商店") : null));
        }
    }

    public sealed class BoardBriefTipSessionTests
    {
        [Test]
        public void Hover_ShowThenClear_Empties()
        {
            var session = new BoardBriefTipSession();
            var gen = session.ShowHover("商店");
            Assert.AreEqual("商店", session.DisplayText);
            Assert.IsTrue(session.IsVisible);

            session.ClearHover(gen);
            Assert.AreEqual(string.Empty, session.DisplayText);
            Assert.IsFalse(session.IsVisible);
        }

        [Test]
        public void StaleClearHover_DoesNotWipeNewer()
        {
            var session = new BoardBriefTipSession();
            var old = session.ShowHover("旧");
            session.ShowHover("新");
            session.ClearHover(old);
            Assert.AreEqual("新", session.DisplayText);
        }

        [Test]
        public void Notice_OverridesHover_UntilCleared()
        {
            var session = new BoardBriefTipSession();
            session.ShowHover("悬停");
            var noticeGen = session.ShowNotice("胜利");
            Assert.AreEqual("胜利", session.DisplayText);

            session.ShowHover("应被盖住");
            Assert.AreEqual("胜利", session.DisplayText);

            session.ClearNotice(noticeGen);
            Assert.AreEqual("应被盖住", session.DisplayText);
        }

        [Test]
        public void HardClear_WipesHoverAndNotice()
        {
            var session = new BoardBriefTipSession();
            session.ShowHover("悬停");
            session.ShowNotice("金币不足");
            Assert.AreEqual("金币不足", session.DisplayText);

            session.HardClear();
            Assert.AreEqual(string.Empty, session.DisplayText);
            Assert.IsFalse(session.IsVisible);
            Assert.IsFalse(session.HasNotice);
        }
    }
}
