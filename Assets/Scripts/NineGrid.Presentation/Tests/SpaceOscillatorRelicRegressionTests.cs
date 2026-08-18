using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// Issue #222 验收测试：
    /// 空间振荡器只在专属「换位」卡结算成功后、按换位后 Avatar 格做全向 2 伤。
    /// </summary>
    public class SpaceOscillatorRelicRegressionTests
    {
        private const string OscillatorDefId = "relic.space_oscillator";
        private const string PositionSwapDefId = "help.position_swap";
        private const string SwapCardDefId = "help.swap_card";
        private const string MonsterDefId = "monster.salamander";
        private const int OscillatorDamage = 2;

        private IArchitecture mArch;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Interface;
            mArch.GetModel<RunModel>().SetPhase(GamePhase.InteractionLoop);
            LoadRealCatalog();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void PositionSwap_EdgeSlot_DamagesOmniNeighborsIncludingCenter_ExcludesAvatar()
        {
            var avatar = CreateAvatarOnBoard(SlotId.Center);
            var swapTarget = CreateRealCardOnBoard(MonsterDefId, SlotId.Board(2));
            var n1 = CreateRealCardOnBoard(MonsterDefId, SlotId.Board(1));
            var n3 = CreateRealCardOnBoard(MonsterDefId, SlotId.Board(3));
            var far = CreateRealCardOnBoard(MonsterDefId, SlotId.Board(8));
            GrantOscillator();
            var swapCard = CreateItemCard(PositionSwapDefId);

            var avatarHpBefore = avatar.Stats.GetBase(StatId.Hp);
            var result = UseItem(swapCard.Uid, swapTarget.Uid);
            Assert.IsTrue(result.Accepted, "换位应结算成功");

            var board = mArch.GetModel<BoardModel>();
            Assert.AreEqual(SlotId.Board(2), board.AvatarSlot.Value, "Avatar 应落到边格 2");
            Assert.AreEqual(swapTarget.Uid, board.GetCardUid(SlotId.Center), "换位目标应到中心格");

            Assert.AreEqual(avatarHpBefore, avatar.Stats.GetBase(StatId.Hp), "不得打 Avatar");
            AssertOscillatorHit(n1, "边格原点应打到格 1");
            AssertOscillatorHit(n3, "边格原点应打到格 3");
            AssertOscillatorHit(swapTarget, "应打到中心格占用者");
            AssertOscillatorMiss(far, "格 8 不是格 2 的全向邻格");
        }

        [Test]
        public void PositionSwap_CornerSlot_CoversFewerNeighbors_StillHitsCenter()
        {
            var avatar = CreateAvatarOnBoard(SlotId.Center);
            var swapTarget = CreateRealCardOnBoard(MonsterDefId, SlotId.Board(1));
            var n2 = CreateRealCardOnBoard(MonsterDefId, SlotId.Board(2));
            var n4 = CreateRealCardOnBoard(MonsterDefId, SlotId.Board(4));
            var far3 = CreateRealCardOnBoard(MonsterDefId, SlotId.Board(3));
            GrantOscillator();
            var swapCard = CreateItemCard(PositionSwapDefId);

            var result = UseItem(swapCard.Uid, swapTarget.Uid);
            Assert.IsTrue(result.Accepted);

            Assert.AreEqual(SlotId.Board(1), mArch.GetModel<BoardModel>().AvatarSlot.Value);
            AssertOscillatorHit(n2, "角格原点应打到格 2");
            AssertOscillatorHit(n4, "角格原点应打到格 4");
            AssertOscillatorHit(swapTarget, "角格也应打到中心格占用者");
            AssertOscillatorMiss(far3, "格 3 不是格 1 的全向邻格（证明原点是换位后而非中心）");
        }

        [Test]
        public void HomingReturn_DoesNotDetonateOscillator()
        {
            CreateAvatarOnBoard(SlotId.Center);
            var swapTarget = CreateRealCardOnBoard(MonsterDefId, SlotId.Board(1));
            var n2 = CreateRealCardOnBoard(MonsterDefId, SlotId.Board(2));
            GrantOscillator();
            var swapCard = CreateItemCard(PositionSwapDefId);

            Assert.IsTrue(UseItem(swapCard.Uid, swapTarget.Uid).Accepted);
            var hitsAfterSwap = CountOscillatorHits();

            Run(new RotateBoardClockwiseAction(true));
            Run(new RotateBoardClockwiseAction(true));
            Run(new RotateBoardClockwiseAction(true));

            Assert.AreEqual(SlotId.Center, mArch.GetModel<BoardModel>().AvatarSlot.Value, "三次占格变化后应归位");
            Assert.AreEqual(hitsAfterSwap, CountOscillatorHits(), "归位与旋转带走不得再引爆空间振荡器");
            Assert.GreaterOrEqual(CountOscillatorHitsTo(n2.Uid), 1, "换位当时应对格 2 造成振荡器伤害");
        }

        [Test]
        public void ExistingSwapCard_DoesNotDetonateOscillator()
        {
            CreateAvatarOnBoard(SlotId.Center);
            var a = CreateRealCardOnBoard(MonsterDefId, SlotId.Board(1));
            var b = CreateRealCardOnBoard(MonsterDefId, SlotId.Board(3));
            GrantOscillator();
            var swapCard = CreateItemCard(SwapCardDefId);

            Assert.IsTrue(UseItem(swapCard.Uid, a.Uid, b.Uid).Accepted);
            Assert.AreEqual(0, CountOscillatorHits(), "现有「交换」不得引爆空间振荡器");
            AssertOscillatorMiss(a, "交换卡目标不应被振荡器打到");
            AssertOscillatorMiss(b, "交换卡目标不应被振荡器打到");
        }

        [Test]
        public void RotateCarry_DoesNotDetonateOscillator()
        {
            CreateAvatarOnBoard(SlotId.Center);
            var swapTarget = CreateRealCardOnBoard(MonsterDefId, SlotId.Board(1));
            GrantOscillator();
            var swapCard = CreateItemCard(PositionSwapDefId);

            Assert.IsTrue(UseItem(swapCard.Uid, swapTarget.Uid).Accepted);
            var hitsAfterSwap = CountOscillatorHits();

            Run(new RotateBoardClockwiseAction(true));
            Assert.AreEqual(SlotId.Board(2), mArch.GetModel<BoardModel>().AvatarSlot.Value, "旋转应带走离巢 Avatar");
            Assert.AreEqual(hitsAfterSwap, CountOscillatorHits(), "旋转带走不得引爆空间振荡器");
        }

        [Test]
        public void LeaveTrapDoor_StillBlocksOscillatorDamage()
        {
            CreateAvatarOnBoard(SlotId.Center);
            var swapTarget = CreateRealCardOnBoard(MonsterDefId, SlotId.Board(1));
            var door = CreateRealCardOnBoard("trap.leave", SlotId.Board(2));
            GrantOscillator();
            var swapCard = CreateItemCard(PositionSwapDefId);

            var doorHpBefore = door.Stats.GetBase(StatId.Hp);
            Assert.IsTrue(UseItem(swapCard.Uid, swapTarget.Uid).Accepted);

            Assert.AreEqual(doorHpBefore, door.Stats.GetBase(StatId.Hp), "离开机关的门应挡住遗物伤");
            Assert.AreEqual(0, CountOscillatorHitsTo(door.Uid), "门上不应出现振荡器伤害事件");
            AssertOscillatorHit(swapTarget, "中心占用者仍应受伤");
        }

        private void LoadRealCatalog()
        {
            var content = mArch.GetSystem<IContentSystem>();
            content.Load(NineGrid.Content.ContentCatalogBootstrap.Load());
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, content.Catalog);
            Assert.IsTrue(content.HasCatalog, "需要真实内容目录");
        }

        private void GrantOscillator()
        {
            Run(new GrantRelicAction(OscillatorDefId));
            Assert.Contains(
                OscillatorDefId,
                (System.Collections.ICollection)mArch.GetModel<PlayerModel>().RelicDefIds,
                "空间振荡器应已装备");
        }

        private CoreCommandResult UseItem(int itemUid, params int[] targets)
        {
            return mArch.GetSystem<IPhaseSystem>().ApplyUseItem(itemUid, targets, null);
        }

        private void Run(GameAction action)
        {
            mArch.GetSystem<IActionPipelineSystem>().Execute(action);
        }

        private CardInstance CreateAvatarOnBoard(SlotId slot)
        {
            var avatar = mArch.GetModel<CardRegistry>().Create("avatar.default", CardKind.Avatar);
            avatar.Stats.SetBase(StatId.MaxHp, 30);
            avatar.Stats.SetBase(StatId.Hp, 30);
            avatar.Stats.SetBase(StatId.Attack, 2);
            mArch.GetModel<BoardModel>().SetAvatar(avatar, slot);
            return avatar;
        }

        private CardInstance CreateRealCardOnBoard(string defId, SlotId slot)
        {
            var content = mArch.GetSystem<IContentSystem>();
            var registry = mArch.GetModel<CardRegistry>();
            var draft = content.CreateDraft(defId);
            Assert.AreNotEqual(CardKind.Unknown, draft.Kind, defId + " 应在内容目录中");
            var card = draft.Create(registry);
            content.ApplyContentToCard(card);
            mArch.GetModel<BoardModel>().PlaceCard(card, slot);
            return card;
        }

        private CardInstance CreateItemCard(string defId)
        {
            var content = mArch.GetSystem<IContentSystem>();
            var registry = mArch.GetModel<CardRegistry>();
            var draft = content.CreateDraft(defId);
            Assert.AreNotEqual(CardKind.Unknown, draft.Kind, defId + " 应在内容目录中");
            var card = draft.Create(registry);
            content.ApplyContentToCard(card);
            card.Zone.Value = ZoneId.ItemSlots;
            mArch.GetModel<DeckModel>().AddToItemSlots(card);
            return card;
        }

        private void AssertOscillatorHit(CardInstance card, string message)
        {
            Assert.GreaterOrEqual(CountOscillatorHitsTo(card.Uid), 1, message);
            Assert.AreEqual(
                card.Stats.GetBase(StatId.MaxHp) - OscillatorDamage,
                card.Stats.GetBase(StatId.Hp),
                message + "（应掉 " + OscillatorDamage + " 点血）");
        }

        private void AssertOscillatorMiss(CardInstance card, string message)
        {
            Assert.AreEqual(0, CountOscillatorHitsTo(card.Uid), message);
            Assert.AreEqual(card.Stats.GetBase(StatId.MaxHp), card.Stats.GetBase(StatId.Hp), message + "（血量应不变）");
        }

        private int CountOscillatorHits()
        {
            return CountOscillatorHitsTo(0);
        }

        private int CountOscillatorHitsTo(int targetUid)
        {
            var count = 0;
            var entries = mArch.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Type != CoreEventType.DamageDealt)
                {
                    continue;
                }

                if (!string.Equals(entry.SourceDefId, OscillatorDefId, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (targetUid != 0 && entry.TargetUid != targetUid)
                {
                    continue;
                }

                count++;
            }

            return count;
        }
    }
}
