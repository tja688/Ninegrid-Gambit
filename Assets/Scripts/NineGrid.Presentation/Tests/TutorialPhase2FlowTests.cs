using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NineGrid.Flow.Tutorial;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 教学第二阶段全流程验证：
    /// 1. 开局格9假人，抽牌堆1假人；
    /// 2. 移动（空格交互）时挂起常规补牌，卡组假人不外溢，格9假人转入攻击范围（格8）；
    /// 3. 击杀格8假人，卡组假人点对点补入格8并随旋转移至盲区（格7），牌堆抽空；
    /// 4. 再次移动，格7假人转入攻击范围（格4）；
    /// 5. 击杀格4假人，阶段2顺利完成，场上与牌堆皆无多余假人。
    /// </summary>
    public class TutorialPhase2FlowTests
    {
        private IArchitecture mArch;

        [SetUp]
        public void SetUp()
        {
            EffectTemplateCatalog.Invalidate();
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Interface;
            mArch.GetModel<RunModel>().SetPhase(GamePhase.InteractionLoop);
            var content = mArch.GetSystem<IContentSystem>();
            content.Load(ContentCatalogBootstrap.Load());
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, content.Catalog);
            Assert.IsTrue(content.HasCatalog, "需要真实内容目录");
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void Phase2_FullFlow_MaintainsCorrectDummyCount_AndRefillOrder()
        {
            CreateAvatarOnBoard(SlotId.Board(5));
            var stab = mArch.GetSystem<IBoardStabilizationSystem>();
            stab.IsRefillSuspended = true;

            // 1. 设置阶段2场面：直摆格1、2提示卡 + 格9假人，抽牌堆1张假人
            mArch.SendCommand(new TutorialSetupPhaseCommand(2));

            var board = mArch.GetModel<BoardModel>();
            var deck = mArch.GetModel<DeckModel>();
            var registry = mArch.GetModel<CardRegistry>();

            Assert.AreEqual(1, deck.DrawPileUids.Count, "阶段2开局抽牌堆应有且仅有1张补位假人");
            Assert.IsFalse(board.IsEmpty(SlotId.Board(9)), "格9应有第一张假人");
            var dummy1Uid = board.GetCardUid(SlotId.Board(9));
            Assert.IsTrue(registry.TryGet(dummy1Uid, out var dummy1Card));
            Assert.AreEqual(TutorialContentIds.DummyTrapDefId, dummy1Card.DefId);

            Assert.IsFalse(IsPhase2Complete(board, deck, registry), "开局阶段2不应判定完成");

            // 2. 玩家首次探索移动（点击空格8）：验证常规补牌被阻止，牌堆仍为1，假人移至格8
            Assert.IsFalse(stab.NeedsRefill, "挂起状态下且无指定补位格时，NeedsRefill必须为false");

            var pipeline = mArch.GetSystem<IActionPipelineSystem>();
            pipeline.Execute(new RotateBoardClockwiseAction());

            Assert.AreEqual(1, deck.DrawPileUids.Count, "旋转后抽牌堆必须依然保留1张假人，不得被自动铺出");
            Assert.AreEqual(dummy1Uid, board.GetCardUid(SlotId.Board(8)), "第一张假人应顺时针旋转至格8（进入玩家攻击范围）");
            Assert.AreEqual(1, CountDummiesOnBoard(board, registry), "场上必须仅有1张假人");
            Assert.IsFalse(IsPhase2Complete(board, deck, registry), "首次移动后阶段2不应判定完成");

            // 3. 玩家攻击并击杀格8假人：
            // 造成伤害击杀 dummy1
            pipeline.Execute(new DealDamageAction(board.AvatarUid.Value, dummy1Uid, 999, "test", "testKill"));
            Assert.AreEqual(ZoneId.Graveyard, dummy1Card.Zone.Value, "第一张假人应进入墓地");

            // 击杀后设置点对点补位目标格8
            stab.PriorityRefillSlot = SlotId.Board(8);
            Assert.IsTrue(stab.NeedsRefill, "设置了目标格8后，NeedsRefill应为true以执行点对点补位");

            stab.ResolveNextSlice();

            Assert.AreEqual(0, deck.DrawPileUids.Count, "点对点补位后，抽牌堆应被抽空（0张）");
            Assert.IsFalse(board.IsEmpty(SlotId.Board(8)), "第二张假人应点对点补入格8");
            var dummy2Uid = board.GetCardUid(SlotId.Board(8));
            Assert.AreNotEqual(dummy1Uid, dummy2Uid, "补入的应为卡组中的第二张假人");
            Assert.IsFalse(stab.NeedsRefill, "点对点补位完成后，NeedsRefill应恢复为false");
            Assert.IsFalse(IsPhase2Complete(board, deck, registry), "假人1死后补入假人2，阶段2不应判定完成");

            // 击杀随附的顺时针旋转：格8第二张假人转至格7（攻击盲区）
            pipeline.Execute(new RotateBoardClockwiseAction());
            Assert.AreEqual(dummy2Uid, board.GetCardUid(SlotId.Board(7)), "第二张假人应随旋转移至格7（攻击盲区）");
            Assert.AreEqual(1, CountDummiesOnBoard(board, registry), "场上应依然仅有1张假人");
            Assert.IsFalse(IsPhase2Complete(board, deck, registry), "旋转至盲区后阶段2不应判定完成");

            // 4. 玩家二次探索移动：旋转盘面，格7第二张假人转至格4（进入攻击范围）
            pipeline.Execute(new RotateBoardClockwiseAction());
            Assert.AreEqual(dummy2Uid, board.GetCardUid(SlotId.Board(4)), "第二张假人应顺时针旋转至格4（进入玩家攻击范围）");
            Assert.AreEqual(0, deck.DrawPileUids.Count, "牌堆依然为0");
            Assert.IsFalse(IsPhase2Complete(board, deck, registry), "二次移动后阶段2不应判定完成");

            // 5. 玩家攻击并击杀格4第二张假人：
            pipeline.Execute(new DealDamageAction(board.AvatarUid.Value, dummy2Uid, 999, "test", "testKill2"));
            Assert.IsTrue(registry.TryGet(dummy2Uid, out var dummy2Card));
            Assert.AreEqual(ZoneId.Graveyard, dummy2Card.Zone.Value, "第二张假人应进入墓地");

            stab.PriorityRefillSlot = SlotId.Board(4);
            Assert.IsFalse(stab.NeedsRefill, "牌堆已空，即使设置了PriorityRefillSlot，NeedsRefill也应为false");

            Assert.AreEqual(0, CountDummiesOnBoard(board, registry), "场上不再有假人");
            Assert.AreEqual(0, deck.DrawPileUids.Count, "卡组不再有假人");
            Assert.IsTrue(IsPhase2Complete(board, deck, registry), "假人2击杀后阶段2必须判定完成（准备进入阶段3）");
        }

        private bool IsPhase2Complete(BoardModel board, DeckModel deck, CardRegistry registry)
        {
            if (deck.DrawPileUids.Count > 0)
            {
                return false;
            }

            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var uid = board.GetCardUid(SlotId.Board(i));
                if (uid > 0 && registry.TryGet(uid, out var card) && card != null)
                {
                    if (card.DefId == TutorialContentIds.DummyTrapDefId
                        && card.Zone.Value != ZoneId.Graveyard
                        && card.Zone.Value != ZoneId.Removed)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private CardInstance CreateAvatarOnBoard(SlotId slot)
        {
            var avatar = mArch.GetModel<CardRegistry>().Create("avatar.default", CardKind.Avatar);
            avatar.Stats.SetBase(StatId.MaxHp, 20);
            avatar.Stats.SetBase(StatId.Hp, 20);
            avatar.Stats.SetBase(StatId.Attack, 3);
            mArch.GetModel<BoardModel>().SetAvatar(avatar, slot);
            return avatar;
        }

        private int CountDummiesOnBoard(BoardModel board, CardRegistry registry)
        {
            var count = 0;
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var uid = board.GetCardUid(SlotId.Board(i));
                if (uid > 0 && registry.TryGet(uid, out var card) && card != null && card.DefId == TutorialContentIds.DummyTrapDefId)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
