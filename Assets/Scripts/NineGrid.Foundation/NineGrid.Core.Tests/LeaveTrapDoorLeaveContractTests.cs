using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #111 / ADR-0026：离开机关内容 + 门/离开技能契约。
    /// 缝：Catalog 装配；门挡非交战伤与直接移除、放行交战玩家出手；离开击破置清关向标志；无赏金。
    /// 本票不改写 <see cref="IDeckSystem.IsNodeCleared"/>（留给 #113）。
    /// </summary>
    public sealed class LeaveTrapDoorLeaveContractTests
    {
        private static readonly SlotId sSlot2 = SlotId.Board(2);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;
        private IContentSystem mContent;
        private IStatSystem mStats;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            EffectTemplateCatalog.Invalidate();
            CardPresentationConfigCatalog.Invalidate();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 111UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
            mContent = mArch.GetSystem<IContentSystem>();
            mStats = mArch.GetSystem<IStatSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            EffectTemplateCatalog.Invalidate();
            CardPresentationConfigCatalog.Invalidate();
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void Catalog_LeaveTrap_MountsDoorAndLeave()
        {
            Assert.IsTrue(mContent.Catalog.TryGetCard("trap.leave", out var trap), "missing trap.leave");
            Assert.AreEqual(CardKind.Trap, trap.Kind);
            Assert.AreEqual("deck.trap", trap.DeckId);
            CollectionAssert.Contains(trap.EffectIds, "trap.leave.door");
            CollectionAssert.Contains(trap.EffectIds, "trap.leave.leave");

            Assert.IsTrue(mContent.Catalog.TryGetEffect("trap.leave.door", out var door));
            Assert.AreEqual(ContentImplementationState.Implemented, door.State);
            Assert.AreEqual(EffectContainerType.Trap, door.ContainerType);

            Assert.IsTrue(mContent.Catalog.TryGetEffect("trap.leave.leave", out var leave));
            Assert.AreEqual(ContentImplementationState.Implemented, leave.State);
            Assert.AreEqual(EffectContainerType.Trap, leave.ContainerType);
        }

        [Test]
        public void Door_BlocksNonEngagementDamage_AndDirectRemove()
        {
            StartEmptyNode();
            PrepareAvatar(99, 5, 0);
            var leaveUid = SpawnLeaveTrap(hp: 6);
            var registry = mArch.GetModel<CardRegistry>();
            var board = mArch.GetModel<BoardModel>();
            var avatarUid = board.AvatarUid.Value;

            Assert.Greater(
                mStats.EvaluateRule(RuleId.DoorProtection, 0f, mStats.CreateContext(registry.Get(leaveUid))),
                0f,
                "门技能应挂 DoorProtection");

            mPipeline.Enqueue(new DealDamageAction(avatarUid, leaveUid, 99));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.AreEqual(6, (int)registry.Get(leaveUid).Stats.GetBase(StatId.Hp), "非交战伤害不得拆门");
            Assert.AreEqual(ZoneId.Board, registry.Get(leaveUid).Zone.Value);

            mPipeline.Enqueue(new RemoveCardAction(leaveUid, ZoneId.Removed, "test.direct_remove"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.AreEqual(ZoneId.Board, registry.Get(leaveUid).Zone.Value, "直接移除不得拆门");
            Assert.AreEqual(sSlot2, registry.Get(leaveUid).Slot.Value);
        }

        [Test]
        public void Door_AllowsEngagementPlayerDamage()
        {
            StartEmptyNode();
            PrepareAvatar(99, 3, 0);
            var leaveUid = SpawnLeaveTrap(hp: 6);
            var registry = mArch.GetModel<CardRegistry>();
            var board = mArch.GetModel<BoardModel>();

            Assert.IsTrue(mPhase.ApplyCombatHit(board.AvatarUid.Value, leaveUid).Accepted);
            Assert.AreEqual(3, (int)registry.Get(leaveUid).Stats.GetBase(StatId.Hp), "交战玩家出手应可造成伤害");
            Assert.AreEqual(ZoneId.Board, registry.Get(leaveUid).Zone.Value);
        }

        [Test]
        public void Door_DoesNotBlockFlipOrShuffleIntoDrawPile()
        {
            StartEmptyNode();
            PrepareAvatar(99, 0, 0);
            var leaveUid = SpawnLeaveTrap(hp: 6);
            var registry = mArch.GetModel<CardRegistry>();
            var battle = mArch.GetModel<BattleContextModel>();

            Assert.IsTrue(registry.Get(leaveUid).FaceUp);
            mPipeline.Enqueue(new FlipCardAction(leaveUid));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.IsFalse(registry.Get(leaveUid).FaceUp, "门不得挡翻面");

            mPipeline.Enqueue(new FlipCardAction(leaveUid));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.IsTrue(registry.Get(leaveUid).FaceUp);

            mPipeline.Enqueue(new ShuffleCardIntoDrawPileAction(leaveUid, top: true, cause: "test.shuffle"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.AreEqual(ZoneId.DrawPile, registry.Get(leaveUid).Zone.Value, "门不得挡洗回卡组");
            Assert.IsFalse(battle.IsLeaveTrapBroken, "洗回不是击破，离开不得置清关标志");
        }

        [Test]
        public void Leave_BreakByCombat_MarksClearOrientedFlag_WithoutGold()
        {
            StartEmptyNode();
            PrepareAvatar(99, 99, 0);
            var leaveUid = SpawnLeaveTrap(hp: 1);
            // 场上留一只真怪：旧 IsNodeCleared（真怪清零）仍为 false，证明本票未切换全局清关条件。
            mPipeline.Enqueue(new SpawnCardAction("monster.skull_head", CardKind.Monster, ZoneId.Board, SlotId.Board(8), 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            var monsterUid = mArch.GetModel<BoardModel>().GetCardUid(SlotId.Board(8));
            var monster = mArch.GetModel<CardRegistry>().Get(monsterUid);
            monster.Stats.SetBase(StatId.MaxHp, 20);
            monster.Stats.SetBase(StatId.Hp, 20);
            monster.Stats.SetBase(StatId.Attack, 0);

            var player = mArch.GetModel<PlayerModel>();
            var coinsBefore = player.Coins.Value;
            var board = mArch.GetModel<BoardModel>();
            var battle = mArch.GetModel<BattleContextModel>();
            var registry = mArch.GetModel<CardRegistry>();

            Assert.IsFalse(battle.IsLeaveTrapBroken);
            Assert.IsFalse(mArch.GetSystem<IDeckSystem>().IsNodeCleared(), "有真怪存活时旧清关条件应为 false");
            Assert.IsTrue(mPhase.ApplyCombatHit(board.AvatarUid.Value, leaveUid).Accepted);

            Assert.IsTrue(battle.IsLeaveTrapBroken, "击破离开机关应置清关向标志");
            Assert.AreNotEqual(ZoneId.Board, registry.Get(leaveUid).Zone.Value, "击破后应离场");
            Assert.AreEqual(coinsBefore, player.Coins.Value, "击破离开机关无击杀赏金");
            Assert.AreEqual(ZoneId.Board, registry.Get(monsterUid).Zone.Value, "真怪应仍在场");
            Assert.IsFalse(mArch.GetSystem<IDeckSystem>().IsNodeCleared(), "本票不切换全局清关条件");
        }

        private void StartEmptyNode()
        {
            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            }).Accepted);
        }

        private void PrepareAvatar(int hp, int attack, int armor)
        {
            var board = mArch.GetModel<BoardModel>();
            var avatar = mArch.GetModel<CardRegistry>().Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.MaxHp, hp);
            avatar.Stats.SetBase(StatId.Hp, hp);
            avatar.Stats.SetBase(StatId.Attack, attack);
            avatar.Stats.SetBase(StatId.Armor, armor);
            avatar.Stats.SetBase(StatId.CurrentArmor, armor);
        }

        private int SpawnLeaveTrap(int hp)
        {
            mPipeline.Enqueue(new SpawnCardAction("trap.leave", CardKind.Trap, ZoneId.Board, sSlot2, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            var uid = mArch.GetModel<BoardModel>().GetCardUid(sSlot2);
            var card = mArch.GetModel<CardRegistry>().Get(uid);
            card.Stats.SetBase(StatId.MaxHp, hp);
            card.Stats.SetBase(StatId.Hp, hp);
            Assert.Greater(
                mArch.GetSystem<IEffectSystem>().GetInstanceIdsByOwner(uid).Count,
                0,
                "离开机关应已激活效果实例");
            return uid;
        }
    }
}
