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
    /// 教学第四阶段全流程验证：
    /// 1. 开局格6飞刀，格8药水；
    /// 2. 拾取飞刀后在手里使用飞刀；
    /// 3. 再拾取药水；
    /// 4. 健壮性验证：即使过程中使用过道具，两张道具均拾取后依然能正确判定阶段4通过。
    /// </summary>
    public class TutorialPhase4FlowTests
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
        public void Phase4_PickupBothItems_Directly_Passes()
        {
            CreateAvatarOnBoard(SlotId.Board(5));
            mArch.SendCommand(new TutorialSetupPhaseCommand(4));

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var phase = mArch.GetSystem<IPhaseSystem>();
            var pipeline = mArch.GetSystem<IActionPipelineSystem>();

            var knifeUid = board.GetCardUid(SlotId.Board(6));
            var potionUid = board.GetCardUid(SlotId.Board(8));
            Assert.IsTrue(knifeUid > 0 && potionUid > 0, "阶段4开局应在格6与格8生成道具卡");

            Assert.IsFalse(ArePhase4ItemsPickedUp(board, registry, knifeUid, potionUid), "开局未拾取时不应通过");

            // 拾取飞刀（格6）
            Assert.IsTrue(phase.ApplyPickupItem(SlotId.Board(6)).Accepted, "拾取飞刀应被接受");
            Assert.IsFalse(ArePhase4ItemsPickedUp(board, registry, knifeUid, potionUid), "仅拾取飞刀时不应通过");

            // 拾取飞刀后棋盘旋转：格8药水转至格7（对角盲区）。再旋转一次进入正交范围（格4）
            pipeline.Execute(new RotateBoardClockwiseAction());
            Assert.AreEqual(potionUid, board.GetCardUid(SlotId.Board(4)), "药水应转入格4（与Avatar正交邻接）");
            Assert.IsTrue(phase.ApplyPickupItem(SlotId.Board(4)).Accepted, "拾取药水应被接受");

            Assert.IsTrue(ArePhase4ItemsPickedUp(board, registry, knifeUid, potionUid), "两张道具均拾取后应判定通过");
        }

        [Test]
        public void Phase4_PickupKnife_UseKnife_ThenPickupPotion_Passes()
        {
            CreateAvatarOnBoard(SlotId.Board(5));
            mArch.SendCommand(new TutorialSetupPhaseCommand(4));

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var phase = mArch.GetSystem<IPhaseSystem>();
            var pipeline = mArch.GetSystem<IActionPipelineSystem>();

            var knifeUid = board.GetCardUid(SlotId.Board(6));
            var potionUid = board.GetCardUid(SlotId.Board(8));

            // 1. 拾取飞刀
            Assert.IsTrue(phase.ApplyPickupItem(SlotId.Board(6)).Accepted, "拾取飞刀应被接受");
            Assert.IsTrue(registry.TryGet(knifeUid, out var knifeCard));
            Assert.AreEqual(ZoneId.ItemSlots, knifeCard.Zone.Value, "飞刀应进入道具槽");

            // 2. 玩家在手里使用飞刀（消耗掉飞刀）
            Assert.IsTrue(phase.ApplyUseItem(knifeUid, null, null).Accepted, "使用飞刀应被接受");
            Assert.AreNotEqual(ZoneId.Board, knifeCard.Zone.Value, "飞刀脱离棋盘");
            Assert.AreNotEqual(ZoneId.ItemSlots, knifeCard.Zone.Value, "飞刀已消耗");

            Assert.IsFalse(ArePhase4ItemsPickedUp(board, registry, knifeUid, potionUid), "飞刀用掉但药水仍在场上，不应通过");

            // 3. 旋转棋盘使药水进入可交互格4，并拾取药水
            pipeline.Execute(new RotateBoardClockwiseAction());
            Assert.AreEqual(potionUid, board.GetCardUid(SlotId.Board(4)), "药水应转入格4");
            Assert.IsTrue(phase.ApplyPickupItem(SlotId.Board(4)).Accepted, "拾取药水应被接受");

            // 4. 关键验证：即使飞刀已被消耗，只要两张道具卡都已被拾取，阶段4依然必须判定通过！
            Assert.IsTrue(ArePhase4ItemsPickedUp(board, registry, knifeUid, potionUid), "飞刀已用+药水拾取后必须判定通过（防止卡关）");
        }

        [Test]
        public void Phase4_PickupBoth_UseBoth_Passes()
        {
            CreateAvatarOnBoard(SlotId.Board(5));
            mArch.SendCommand(new TutorialSetupPhaseCommand(4));

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var phase = mArch.GetSystem<IPhaseSystem>();
            var pipeline = mArch.GetSystem<IActionPipelineSystem>();

            var knifeUid = board.GetCardUid(SlotId.Board(6));
            var potionUid = board.GetCardUid(SlotId.Board(8));

            // 拾取飞刀并使用
            Assert.IsTrue(phase.ApplyPickupItem(SlotId.Board(6)).Accepted);
            Assert.IsTrue(phase.ApplyUseItem(knifeUid, null, null).Accepted);

            // 旋转并拾取药水并使用
            pipeline.Execute(new RotateBoardClockwiseAction());
            Assert.AreEqual(potionUid, board.GetCardUid(SlotId.Board(4)));
            Assert.IsTrue(phase.ApplyPickupItem(SlotId.Board(4)).Accepted);
            Assert.IsTrue(phase.ApplyUseItem(potionUid, null, null).Accepted);

            // 两张道具均已拾取并消耗
            Assert.IsTrue(ArePhase4ItemsPickedUp(board, registry, knifeUid, potionUid), "两张道具卡均拾取并消耗后应判定通过");
        }

        private static bool ArePhase4ItemsPickedUp(BoardModel board, CardRegistry registry, int knifeUid, int potionUid)
        {
            if (knifeUid > 0 && potionUid > 0)
            {
                if (IsItemPickedUpFromBoard(registry, knifeUid) && IsItemPickedUpFromBoard(registry, potionUid))
                {
                    return true;
                }
            }

            if (!HasDefIdOnBoard(board, registry, TutorialContentIds.KnifeDefId)
                && !HasDefIdOnBoard(board, registry, TutorialContentIds.PotionDefId))
            {
                return true;
            }

            return false;
        }

        private static bool IsItemPickedUpFromBoard(CardRegistry registry, int uid)
        {
            if (uid <= 0)
            {
                return false;
            }

            if (!registry.TryGet(uid, out var card) || card == null)
            {
                return true;
            }

            return card.Zone.Value != ZoneId.Board;
        }

        private static bool HasDefIdOnBoard(BoardModel board, CardRegistry registry, string defId)
        {
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var uid = board.GetCardUid(SlotId.Board(i));
                if (uid > 0 && registry.TryGet(uid, out var card) && card != null)
                {
                    if (card.DefId == defId && card.Zone.Value == ZoneId.Board)
                    {
                        return true;
                    }
                }
            }

            return false;
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
    }
}
