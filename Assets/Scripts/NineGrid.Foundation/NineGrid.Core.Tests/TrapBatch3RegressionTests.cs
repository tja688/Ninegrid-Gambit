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
    /// 机关卡落地批次3：攻/甲图腾光环、恢复图腾/治疗泉脉冲回血、倒刺邻伤 + Help 治疗泉迁徙。
    /// </summary>
    public sealed class TrapBatch3RegressionTests
    {
        private static readonly SlotId sSlot1 = SlotId.Board(1);
        private static readonly SlotId sSlot2 = SlotId.Board(2);
        private static readonly SlotId sSlot3 = SlotId.Board(3);
        private static readonly SlotId sSlot4 = SlotId.Board(4);
        private static readonly SlotId sSlot6 = SlotId.Board(6);
        private static readonly SlotId sSlot7 = SlotId.Board(7);
        private static readonly SlotId sSlot8 = SlotId.Board(8);
        private static readonly SlotId sSlot9 = SlotId.Board(9);

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
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 42UL });
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
        public void Catalog_Batch3Traps_AreTrapOnDeckTrap_AndHelpHealingSpringGone()
        {
            AssertTrapMounted("trap.attack_totem", "trap.attack_totem.aura");
            AssertTrapMounted("trap.attack_totem", "trap.attack_totem.refresh");
            AssertTrapMounted("trap.attack_totem", "trap.attack_totem.aura_player");
            AssertTrapMounted("trap.attack_totem", "trap.attack_totem.refresh_player");
            AssertTrapMounted("trap.armor_totem", "trap.armor_totem.aura");
            AssertTrapMounted("trap.armor_totem", "trap.armor_totem.refresh");
            AssertTrapMounted("trap.armor_totem", "trap.armor_totem.aura_player");
            AssertTrapMounted("trap.armor_totem", "trap.armor_totem.refresh_player");
            AssertTrapMounted("trap.recovery_totem", "trap.recovery_totem.heal_on_move");
            AssertTrapMounted("trap.recovery_totem", "trap.recovery_totem.heal_player_on_move");
            AssertTrapMounted("trap.spike", "trap.spike.move");
            AssertTrapMounted("trap.spike", "trap.spike.move_player");
            AssertTrapMounted("trap.healing_spring", "trap.healing_spring.heal_on_move");

            Assert.IsFalse(mContent.Catalog.TryGetCard("help.healing_spring", out _), "help.healing_spring 应已删除");
        }

        [Test]
        public void AttackTotem_AdjacentMonsterAndPlayerGainAttack_LeaveAndTrapExcluded()
        {
            StartEmptyNode();
            PrepareAvatar(99, 5, 0);
            var registry = mArch.GetModel<CardRegistry>();
            var board = mArch.GetModel<BoardModel>();
            var avatar = registry.Get(board.AvatarUid.Value);

            // 格2 邻 Avatar(5) 与 格1；格9 不邻格2
            var nearUid = SpawnKind("monster.skull_head", CardKind.Monster, sSlot1, hp: 5);
            var farUid = SpawnKind("monster.melee_3", CardKind.Monster, sSlot9, hp: 5);
            var otherTrapUid = SpawnTrap("trap.revive_stone", sSlot3);
            registry.Get(nearUid).Stats.SetBase(StatId.Attack, 2);
            registry.Get(farUid).Stats.SetBase(StatId.Attack, 2);

            var totemUid = SpawnTrap("trap.attack_totem", sSlot2);
            Assert.Greater(
                mArch.GetSystem<IEffectSystem>().GetInstanceIdsByOwner(totemUid).Count,
                0,
                "攻击图腾应已激活效果实例");
            Assert.AreEqual(3, mStats.GetEffectiveInt(registry.Get(nearUid), StatId.Attack), "邻格真怪应攻+1");
            Assert.AreEqual(6, mStats.GetEffectiveInt(avatar, StatId.Attack), "邻玩家应攻+1");
            Assert.AreEqual(2, mStats.GetEffectiveInt(registry.Get(farUid), StatId.Attack), "非邻怪不加");
            Assert.AreEqual(
                0,
                mStats.GetEffectiveInt(registry.Get(otherTrapUid), StatId.Attack),
                "邻格机关不加攻");

            mPipeline.Enqueue(new MoveCardAction(nearUid, sSlot7, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.AreEqual(2, mStats.GetEffectiveInt(registry.Get(nearUid), StatId.Attack), "离开邻接后光环应失效");
            Assert.AreEqual(6, mStats.GetEffectiveInt(avatar, StatId.Attack), "玩家仍邻图腾应保持+1");
        }

        [Test]
        public void ArmorTotem_AdjacentMonsterAndPlayerGainArmor_LeaveExpires()
        {
            StartEmptyNode();
            PrepareAvatar(99, 0, 1);
            var registry = mArch.GetModel<CardRegistry>();
            var board = mArch.GetModel<BoardModel>();
            var avatar = registry.Get(board.AvatarUid.Value);

            var nearUid = SpawnKind("monster.skull_head", CardKind.Monster, sSlot1, hp: 5);
            registry.Get(nearUid).Stats.SetBase(StatId.Armor, 1);
            registry.Get(nearUid).Stats.SetBase(StatId.CurrentArmor, 1);

            SpawnTrap("trap.armor_totem", sSlot2);
            Assert.AreEqual(2, mStats.GetEffectiveInt(registry.Get(nearUid), StatId.Armor), "邻怪护甲应+1");
            Assert.AreEqual(2, mStats.GetEffectiveInt(avatar, StatId.Armor), "邻玩家护甲应+1");

            mPipeline.Enqueue(new MoveCardAction(nearUid, sSlot7, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.AreEqual(1, mStats.GetEffectiveInt(registry.Get(nearUid), StatId.Armor), "离开邻接后护甲光环应失效");
        }

        [Test]
        public void RecoveryTotem_MoveHealsAdjacentMonsterAndPlayer_NotTrap()
        {
            StartEmptyNode();
            PrepareAvatar(99, 0, 0);
            var registry = mArch.GetModel<CardRegistry>();
            var board = mArch.GetModel<BoardModel>();
            var avatar = registry.Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.Hp, 50);

            // 格4 正交邻中心5；格1/7 邻格4（用于怪/机关对照）
            var nearUid = SpawnKind("monster.skull_head", CardKind.Monster, sSlot1, hp: 8);
            registry.Get(nearUid).Stats.SetBase(StatId.Hp, 5);
            var otherTrapUid = SpawnTrap("trap.revive_stone", sSlot7);
            registry.Get(otherTrapUid).Stats.SetBase(StatId.Hp, 4);

            var totemUid = SpawnTrap("trap.recovery_totem", sSlot8);
            Assert.GreaterOrEqual(
                mArch.GetSystem<IEffectSystem>().GetInstanceIdsByOwner(totemUid).Count,
                2,
                "恢复图腾应挂 2 个效果实例");

            mPipeline.Enqueue(new MoveCardAction(totemUid, sSlot4, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            Assert.AreEqual(6, (int)registry.Get(nearUid).Stats.GetBase(StatId.Hp), "邻真怪应真回血1");
            Assert.AreEqual(4, (int)registry.Get(otherTrapUid).Stats.GetBase(StatId.Hp), "邻机关不应回血");
            Assert.AreEqual(51, (int)avatar.Stats.GetBase(StatId.Hp), "邻玩家应真回血1");
        }

        [Test]
        public void RecoveryTotem_BoardRotateLand_HealsAdjacentPlayer()
        {
            StartEmptyNode();
            PrepareAvatar(99, 0, 0);
            var registry = mArch.GetModel<CardRegistry>();
            var board = mArch.GetModel<BoardModel>();
            var avatar = registry.Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.Hp, 40);

            // 格1 对角不邻中心；顺时针旋转后落到格2，正交邻格5
            Assert.IsFalse(sSlot1.IsAdjacentTo(SlotId.Board(5)));
            Assert.IsTrue(sSlot2.IsAdjacentTo(SlotId.Board(5)));
            SpawnTrap("trap.recovery_totem", sSlot1);
            mPipeline.Enqueue(new RotateBoardClockwiseAction(true, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            Assert.AreEqual(41, (int)avatar.Stats.GetBase(StatId.Hp), "旋转落到邻接格应回血1");
        }

        [Test]
        public void Spike_MoveDamagesAdjacentMonsterAndPlayer_NotTrap()
        {
            StartEmptyNode();
            PrepareAvatar(99, 0, 0);
            var registry = mArch.GetModel<CardRegistry>();
            var board = mArch.GetModel<BoardModel>();
            var avatar = registry.Get(board.AvatarUid.Value);
            var hp0 = (int)avatar.Stats.GetBase(StatId.Hp);

            var nearUid = SpawnKind("monster.skull_head", CardKind.Monster, sSlot1, hp: 8);
            var near = registry.Get(nearUid);
            near.Stats.SetBase(StatId.Armor, 0);
            near.Stats.SetBase(StatId.CurrentArmor, 0);
            var otherTrapUid = SpawnTrap("trap.revive_stone", sSlot7);
            var spikeUid = SpawnTrap("trap.spike", sSlot8);

            mPipeline.Enqueue(new MoveCardAction(spikeUid, sSlot4, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            Assert.AreEqual(7, (int)near.Stats.GetBase(StatId.Hp), "邻真怪应受伤1");
            Assert.AreEqual(6, (int)registry.Get(otherTrapUid).Stats.GetBase(StatId.Hp), "邻机关不受伤");
            Assert.AreEqual(hp0 - 1, (int)avatar.Stats.GetBase(StatId.Hp), "邻玩家应受伤1");
        }

        private bool ContainsEffectTriggeredSince(int startIndex, int ownerUid)
        {
            var entries = mPipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].Type == CoreEventType.EffectTriggered && entries[i].CardUid == ownerUid)
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void HealingSpring_MoveHealsOnlyAdjacentPlayer()
        {
            StartEmptyNode();
            PrepareAvatar(99, 0, 0);
            var registry = mArch.GetModel<CardRegistry>();
            var board = mArch.GetModel<BoardModel>();
            var avatar = registry.Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.Hp, 40);

            var nearUid = SpawnKind("monster.skull_head", CardKind.Monster, sSlot4, hp: 8);
            registry.Get(nearUid).Stats.SetBase(StatId.Hp, 5);

            var springUid = SpawnTrap("trap.healing_spring", sSlot8);
            // 格8 不邻格5；先移一次确认不奶
            mPipeline.Enqueue(new MoveCardAction(springUid, sSlot9, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.AreEqual(40, (int)avatar.Stats.GetBase(StatId.Hp), "不邻玩家时不应回血");
            Assert.AreEqual(5, (int)registry.Get(nearUid).Stats.GetBase(StatId.Hp), "治疗泉不奶怪");

            // 格9→格6：格6 邻格5
            mPipeline.Enqueue(new MoveCardAction(springUid, sSlot6, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.AreEqual(41, (int)avatar.Stats.GetBase(StatId.Hp), "邻玩家应回血1");
            Assert.AreEqual(5, (int)registry.Get(nearUid).Stats.GetBase(StatId.Hp), "仍不奶怪");
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

        private int SpawnTrap(string defId, SlotId slot)
        {
            return SpawnKind(defId, CardKind.Trap, slot, hp: 6);
        }

        private int SpawnKind(string defId, CardKind kind, SlotId slot, int hp)
        {
            mPipeline.Enqueue(new SpawnCardAction(defId, kind, ZoneId.Board, slot, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            var uid = mArch.GetModel<BoardModel>().GetCardUid(slot);
            if (hp > 0 && kind != CardKind.HelpCard)
            {
                var card = mArch.GetModel<CardRegistry>().Get(uid);
                card.Stats.SetBase(StatId.MaxHp, hp);
                card.Stats.SetBase(StatId.Hp, hp);
            }

            return uid;
        }

        private void AssertTrapMounted(string trapId, string effectId)
        {
            Assert.IsTrue(mContent.Catalog.TryGetCard(trapId, out var trap), "missing " + trapId);
            Assert.AreEqual(CardKind.Trap, trap.Kind, trapId + " catalog kind");
            Assert.AreEqual("deck.trap", trap.DeckId, trapId + " deckId");
            CollectionAssert.Contains(trap.EffectIds, effectId, trapId + " should mount " + effectId);
            Assert.IsTrue(mContent.Catalog.TryGetEffect(effectId, out var fx), "missing effect " + effectId);
            Assert.AreEqual(ContentImplementationState.Implemented, fx.State, effectId);
            Assert.AreEqual(EffectContainerType.Trap, fx.ContainerType, effectId + " container");
        }
    }
}
