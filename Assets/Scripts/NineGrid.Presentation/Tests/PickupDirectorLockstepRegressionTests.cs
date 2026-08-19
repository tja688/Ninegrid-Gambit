using System.Collections.Generic;
using NineGrid.Cards;
using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Setup;
using NineGrid.Presentation.Systems;
using NUnit.Framework;
using QFramework;
using System.Linq;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 根因 = 拾取走 <c>PhaseSystem.ApplyPickupItem</c> 单体路径——Core 命令内同步结算
    /// 整个互动链（含敌方齐射 <c>DealDamage</c>），没有任何表演批次 / Counter rig，
    /// 观感就是「瞬间掉血、无攻击表演编排」；教程钉死莱姆卡组 + 强制拾取，必在第 4 次互动触发。
    /// 修复 = 拾取交 PresentationDirector 锁步剧本（<c>ApplyPickupCard</c> 分拍 +
    /// 互动计数/补牌/旋转/敌方行动逐批表演），并让外部租约可嵌套（教程卡点与拾取动画共用一把锁）。
    /// </summary>
    public class PickupDirectorLockstepRegressionTests
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
            BattleBeatHook.Reset();
            FlipPlaybackCoordinator.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            BattleBeatHook.Reset();
            FlipPlaybackCoordinator.Reset();
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void ApplyPickupCard_IsCardOnly_NoRotationNoCountdownTick()
        {
            var registry = mArch.GetModel<CardRegistry>();
            var board = mArch.GetModel<BoardModel>();
            var deck = mArch.GetModel<DeckModel>();

            var avatar = CreateAvatar(SlotId.Board(5));
            var slime = SpawnOnBoard("monster.melee_3", CardKind.Monster, SlotId.Board(6), "test.pickupCardOnly");
            slime.Counters.Set(CoreCounterKeys.AttackPatternCountdown, 2);
            var potion = SpawnOnBoard("help.healing_potion", CardKind.HelpCard, SlotId.Board(2), "test.pickupCardOnly");

            var phase = mArch.GetSystem<IPhaseSystem>();
            var result = phase.ApplyPickupCard(SlotId.Board(2));
            Assert.IsTrue(result.Accepted, "拾取应被接受");
            Assert.AreEqual(ZoneId.ItemSlots, potion.Zone.Value, "拾取后应进入道具卡格");
            Assert.IsTrue(deck.ItemSlotUids.Contains(potion.Uid), "道具卡格应登记该卡");

            // 分拍入口不得自行旋转 / 推进敌方倒计时：互动链由导演逐批表演。
            Assert.AreEqual(SlotId.Board(6), slime.Slot.Value, "拾取分拍不得旋转盘面");
            Assert.AreEqual(2, slime.Counters.Get(CoreCounterKeys.AttackPatternCountdown), "拾取分拍不得推进行动倒计时");
            Assert.AreEqual(ZoneId.Avatar, avatar.Zone.Value);
            Assert.IsTrue(board.IsEmpty(SlotId.Board(2)), "拾取后原格应清空（由导演稳定化补位）");
            _ = registry;
        }

        [Test]
        public void PickupIntent_EnemyVolleyPlaysThroughDirectorCounterChannel()
        {
            var deck = mArch.GetModel<DeckModel>();

            var avatar = CreateAvatar(SlotId.Board(5));
            // 莱姆在格 3（对角）：拾取后顺时针旋转 3→6（正交），敌方行动可开火。
            var slime = SpawnOnBoard("monster.melee_3", CardKind.Monster, SlotId.Board(3), "test.pickupLockstep");
            slime.Counters.Set(CoreCounterKeys.AttackPatternCountdown, 1);
            var potion = SpawnOnBoard("help.healing_potion", CardKind.HelpCard, SlotId.Board(2), "test.pickupLockstep");

            var boardChannel = new RecordingBoardChannel();
            var counterChannel = new RecordingCounterChannel();
            var pickedSlots = new List<int>();
            var dispatcher = new CoreCommandDispatcher(mArch);
            var factory = new PickupIntentScriptFactory(
                mArch,
                dispatcher,
                boardChannel,
                (startIndex, boardSlot, result) => boardChannel.Enqueue(result),
                onItemPickedUp: pickedSlots.Add,
                counterPresentChannel: counterChannel,
                onCounterBatchProjected: (startIndex, slot, uid, result) => counterChannel.Enqueue(slot, uid, result));

            var root = new PresentationCompositionRoot();
            var runtime = root.Install(factory);

            var accepted = runtime.TrySubmitIntent(new InputIntent(InputIntentKinds.Pickup, 2), out _);
            Assert.IsTrue(accepted, "拾取意图应被导演接纳");

            TickUntilIdle(runtime);

            Assert.IsFalse(runtime.MainlineBusy.Value, "拾取剧本应完整跑完");
            Assert.AreEqual(1, counterChannel.BeginCount, "齐射应恰有一次开火表演（Counter 通道）");
            Assert.GreaterOrEqual(boardChannel.BeginCount, 4, "拾取/计数/旋转/收尾盘面批均应表演");
            Assert.AreEqual(new[] { 2 }, pickedSlots.ToArray(), "收尾回调（教程 Step7）应在互动链表演完后触发一次");
            Assert.AreEqual(ZoneId.ItemSlots, potion.Zone.Value, "拾取卡应进入道具卡格");
            Assert.IsTrue(deck.ItemSlotUids.Contains(potion.Uid), "道具卡格应登记该卡");
            Assert.AreEqual(10 - 1, (int)avatar.Stats.GetBase(StatId.Hp), "齐射伤害应经 Counter 表演后恰为莱姆攻击力（1）");
        }

        [Test]
        public void ExternalHold_NestsAndReleasesIncrementally()
        {
            var director = new PresentationDirector(new NoOpIntentScriptFactory());
            Assert.IsFalse(director.HasExternalHold);

            Assert.IsTrue(director.TryBeginExternalHold("outer"), "首次租约应成功");
            Assert.IsTrue(director.HasExternalHold);
            Assert.IsTrue(director.TryBeginExternalHold("inner"), "嵌套租约应成功而非拒绝");
            Assert.IsTrue(director.HasExternalHold, "嵌套释放前不得放行主线");

            director.EndExternalHold("inner");
            Assert.IsTrue(director.HasExternalHold, "内层释放后外层租约仍应持有");

            director.EndExternalHold("outer");
            Assert.IsFalse(director.HasExternalHold, "全部释放后主线才放行");
        }

        // ==================== 基建 ====================

        private void TickUntilIdle(IPresentationRuntimeSystem runtime)
        {
            for (var i = 0; i < 5000 && runtime.MainlineBusy.Value; i++)
            {
                runtime.Tick(0.016f);
            }
        }

        private CardInstance CreateAvatar(SlotId slot)
        {
            var avatar = mArch.GetModel<CardRegistry>().Create("avatar.default", CardKind.Avatar);
            avatar.Stats.SetBase(StatId.MaxHp, 10);
            avatar.Stats.SetBase(StatId.Hp, 10);
            avatar.Stats.SetBase(StatId.Attack, 4);
            mArch.GetModel<BoardModel>().SetAvatar(avatar, slot);
            return avatar;
        }

        private CardInstance SpawnOnBoard(string defId, CardKind kind, SlotId slot, string cause)
        {
            mArch.GetSystem<IActionPipelineSystem>().Execute(
                new SpawnCardAction(defId, kind, ZoneId.Board, slot, 1, cause));
            var uid = mArch.GetModel<BoardModel>().GetCardUid(slot);
            Assert.Greater(uid, 0, defId + " 应落到 " + slot);
            return mArch.GetModel<CardRegistry>().Get(uid);
        }

        /// <summary>记录 Begin 次数的盘面通道（即时完成，无场景依赖）。</summary>
        private sealed class RecordingBoardChannel : IPresentChannel
        {
            public int BeginCount;

            public int ActiveBatchId { get; private set; }

            public bool IsComplete => true;

            public void Begin(int batchId)
            {
                ActiveBatchId = batchId;
                BeginCount++;
            }

            public void Tick(float deltaTime)
            {
            }

            public void Enqueue(PostKillBoardPresentationResult result)
            {
            }
        }

        /// <summary>记录 Begin 次数的反击通道（即时完成，无场景依赖）。</summary>
        private sealed class RecordingCounterChannel : IPresentChannel
        {
            public int BeginCount;

            public int ActiveBatchId { get; private set; }

            public bool IsComplete => true;

            public void Begin(int batchId)
            {
                ActiveBatchId = batchId;
                BeginCount++;
            }

            public void Tick(float deltaTime)
            {
            }

            public void Enqueue(int attackerSlot, int attackerUid, PostKillBoardPresentationResult result)
            {
            }
        }

        private sealed class NoOpIntentScriptFactory : IIntentScriptFactory
        {
            public void BuildScript(InputIntent intent, BattleTimeline timeline)
            {
            }
        }
    }
}
