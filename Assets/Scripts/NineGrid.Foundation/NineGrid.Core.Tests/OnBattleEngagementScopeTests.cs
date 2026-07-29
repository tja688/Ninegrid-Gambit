using NineGrid.Content;
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
    /// #78 / ADR-0012：OnBattle 仅在交战作用域内触发；非交战 DealDamage 不触发、不洗 UntilBattleEnds。
    /// Seam：IPhaseSystem / ActionPipeline（交战 Begin–End 与裸 DealDamage）。
    /// </summary>
    public sealed class OnBattleEngagementScopeTests
    {
        private const string BareOnBattleRelicJson =
            "{\"id\":\"relic.on_battle_scope.probe\",\"typeTag\":\"【类型遗物】\",\"containerType\":\"Relic\","
            + "\"kind\":\"Triggered\","
            + "\"requires\":[\"NoOwnerEntity\"],"
            + "\"trigger\":{\"atom\":\"OnBattle\"},"
            + "\"target\":{\"atom\":\"Player\"},"
            + "\"action\":{\"atom\":\"AddModifier\",\"stat\":\"Attack\",\"op\":\"Add\",\"value\":1,\"layer\":\"Temporary\",\"scope\":\"UntilBattleEnds\",\"source\":\"relic.on_battle_scope\"}}";

        private const string MonsterFilteredOnBattleJson =
            "{\"id\":\"relic.battle_hardened.scope\",\"typeTag\":\"【类型遗物】\",\"containerType\":\"Relic\","
            + "\"kind\":\"Triggered\","
            + "\"requires\":[\"NoOwnerEntity\"],"
            + "\"trigger\":{\"atom\":\"OnBattle\",\"sourceAction\":\"DealDamage\",\"targetKind\":\"Monster\",\"maxActionDepth\":0},"
            + "\"conditions\":[{\"atom\":\"EventFilterActorIsPlayer\",\"eventType\":\"DamageDealt\",\"targetKind\":\"Monster\"}],"
            + "\"target\":{\"atom\":\"Player\"},"
            + "\"action\":{\"atom\":\"AddModifier\",\"stat\":\"Attack\",\"op\":\"Add\",\"value\":2,\"layer\":\"Temporary\",\"scope\":\"UntilEnemyChanges\",\"source\":\"relic.battle_hardened.scope\"}}";

        private static readonly SlotId sPrimarySlot = SlotId.Board(2);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;
        private IEffectSystem mEffects;
        private IStatSystem mStats;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 78UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
            mEffects = mArch.GetSystem<IEffectSystem>();
            mStats = mArch.GetSystem<IStatSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void EngagementDamage_StillTriggersMonsterFilteredOnBattle()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 30, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sPrimarySlot);
            Activate(MonsterFilteredOnBattleJson, "relic.battle_hardened.scope");
            PrepareAvatarAttack(5);

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var avatarUid = board.AvatarUid.Value;
            var monsterUid = board.GetCardUid(sPrimarySlot);

            Assert.IsTrue(mPhase.ApplyCombatHit(avatarUid, monsterUid).Accepted);
            Assert.AreEqual(
                7,
                mStats.GetEffectiveInt(registry.Get(avatarUid), StatId.Attack),
                "交战路径上带 Monster 过滤的 OnBattle 仍应触发");
        }

        [Test]
        public void DealDamage_OutsideEngagement_DoesNotTriggerBareOnBattle()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 30, attack: 3)).Accepted);
            PlaceSoleBoardCardAt(sPrimarySlot);
            Activate(BareOnBattleRelicJson, "relic.on_battle_scope");
            PrepareAvatarAttack(5);

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var avatarUid = board.AvatarUid.Value;
            var monsterUid = board.GetCardUid(sPrimarySlot);
            var attackBefore = mStats.GetEffectiveInt(registry.Get(avatarUid), StatId.Attack);

            mPipeline.Enqueue(new DealDamageAction(monsterUid, avatarUid, 1));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            Assert.AreEqual(
                attackBefore,
                mStats.GetEffectiveInt(registry.Get(avatarUid), StatId.Attack),
                "交战作用域外的 DealDamage 不得触发 OnBattle");
            Assert.AreEqual(
                0,
                CountEffectTriggeredSince(0, "relic.on_battle_scope"),
                "非交战伤害不得产生 OnBattle 的 EffectTriggered");
        }

        [Test]
        public void DealDamage_InsideEngagement_TriggersBareOnBattle()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 30, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sPrimarySlot);
            Activate(BareOnBattleRelicJson, "relic.on_battle_scope");
            PrepareAvatarAttack(5);

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var avatarUid = board.AvatarUid.Value;
            var monsterUid = board.GetCardUid(sPrimarySlot);
            var attackBefore = mStats.GetEffectiveInt(registry.Get(avatarUid), StatId.Attack);

            mPipeline.Enqueue(new BeginPlayerMonsterEngagementAction(monsterUid));
            mPipeline.Enqueue(new DealDamageAction(avatarUid, monsterUid, 1));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            Assert.AreEqual(
                attackBefore + 1,
                mStats.GetEffectiveInt(registry.Get(avatarUid), StatId.Attack),
                "交战作用域内裸 OnBattle 应触发");
            Assert.Greater(CountEffectTriggeredSince(0, "relic.on_battle_scope"), 0);
        }

        [Test]
        public void UntilBattleEnds_NotClearedByNonEngagementDamage()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 30, attack: 3)).Accepted);
            PlaceSoleBoardCardAt(sPrimarySlot);
            PrepareAvatarAttack(5);

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var avatarUid = board.AvatarUid.Value;
            var monsterUid = board.GetCardUid(sPrimarySlot);
            var avatar = registry.Get(avatarUid);
            mStats.AddModifier(
                avatar,
                new StatModifier(
                    StatId.Attack,
                    ModifierOp.Add,
                    2f,
                    ModifierLayer.Temporary,
                    new ModifierSource("test.until_battle_ends"),
                    ModifierScope.UntilBattleEnds));
            Assert.AreEqual(7, mStats.GetEffectiveInt(registry.Get(avatarUid), StatId.Attack));

            // 非交战伤害：不开 Begin/End，模拟未来单向打击不得提前洗掉 UntilBattleEnds。
            mPipeline.Enqueue(new DealDamageAction(monsterUid, avatarUid, 1));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.AreEqual(
                7,
                mStats.GetEffectiveInt(registry.Get(avatarUid), StatId.Attack),
                "非交战 DealDamage 不得清掉 UntilBattleEnds");

            mPipeline.Enqueue(new EndBattleScopeCleanupAction());
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.AreEqual(
                5,
                mStats.GetEffectiveInt(registry.Get(avatarUid), StatId.Attack),
                "仅 EndBattleScopeCleanup 才清 UntilBattleEnds");
        }

        [Test]
        public void OnBattle_CountsOnlyEngagementHits_NotStrayDamage()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 40, attack: 3)).Accepted);
            PlaceSoleBoardCardAt(sPrimarySlot);
            Activate(BareOnBattleRelicJson, "relic.on_battle_scope");
            PrepareAvatarAttack(5);

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var avatarUid = board.AvatarUid.Value;
            var monsterUid = board.GetCardUid(sPrimarySlot);

            mPipeline.Enqueue(new BeginPlayerMonsterEngagementAction(monsterUid));
            mPipeline.Enqueue(new DealDamageAction(avatarUid, monsterUid, 1));
            mPipeline.Enqueue(new EndBattleScopeCleanupAction());
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            mPipeline.Enqueue(new BeginPlayerMonsterEngagementAction(monsterUid));
            mPipeline.Enqueue(new DealDamageAction(avatarUid, monsterUid, 1));
            mPipeline.Enqueue(new EndBattleScopeCleanupAction());
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            var afterTwoEngagements = CountEffectTriggeredSince(0, "relic.on_battle_scope");
            Assert.AreEqual(2, afterTwoEngagements, "两次交战应各计一次 OnBattle（「每战斗 N 次」只数交战）");

            mPipeline.Enqueue(new DealDamageAction(monsterUid, avatarUid, 1));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.AreEqual(
                2,
                CountEffectTriggeredSince(0, "relic.on_battle_scope"),
                "交战外伤害不得计入 OnBattle 次数");
        }

        [Test]
        public void ForceBattle_OutsideEngagement_OpensScopeAndTriggersOnBattle()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 30, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sPrimarySlot);
            Activate(BareOnBattleRelicJson, "relic.on_battle_scope");
            PrepareAvatarAttack(5);

            var board = mArch.GetModel<BoardModel>();
            var monsterUid = board.GetCardUid(sPrimarySlot);

            mPipeline.Enqueue(new ForceBattleAction(monsterUid));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            Assert.Greater(
                CountEffectTriggeredSince(0, "relic.on_battle_scope"),
                0,
                "独立 ForceBattle 应自开交战窗并触发 OnBattle（End 会清 UntilBattleEnds，故以 EffectTriggered 为准）");
            Assert.IsFalse(
                mArch.GetSystem<IBattleScopeSystem>().IsEngagementActive,
                "ForceBattle 收尾后交战窗应关闭");
        }

        private void Activate(string json, string sourceDefId)
        {
            var definition = mEffects.ParseJson(json);
            Assert.IsTrue(mEffects.Validate(definition).IsValid);
            mEffects.Activate(
                definition,
                new EffectOwner(EffectContainerType.Relic, sourceDefId, 0));
        }

        private static NodeDeckOptions CreateSingleMonsterNode(int hp, int attack)
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.test", CardKind.Monster) { MaxHp = hp, Attack = attack });
        }

        private void PrepareAvatarAttack(int attack)
        {
            var board = mArch.GetModel<BoardModel>();
            var avatar = mArch.GetModel<CardRegistry>().Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.MaxHp, 99);
            avatar.Stats.SetBase(StatId.Hp, 99);
            avatar.Stats.SetBase(StatId.Armor, 0);
            avatar.Stats.SetBase(StatId.Attack, attack);
        }

        private int PlaceSoleBoardCardAt(SlotId targetSlot)
        {
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                if (slot == board.AvatarSlot.Value)
                {
                    continue;
                }

                var uid = board.GetCardUid(slot);
                if (uid == 0)
                {
                    continue;
                }

                var card = registry.Get(uid);
                if (card.Slot.Value == targetSlot)
                {
                    return uid;
                }

                board.ClearSlot(card.Slot.Value);
                board.PlaceCard(card, targetSlot);
                return uid;
            }

            Assert.Fail("No board card to relocate.");
            return 0;
        }

        private int CountEffectTriggeredSince(int startIndex, string sourceDefId)
        {
            var entries = mPipeline.EventLog.Entries;
            var count = 0;
            for (var i = startIndex; i < entries.Count; i++)
            {
                var evt = entries[i];
                if (evt.Type == CoreEventType.EffectTriggered
                    && string.Equals(evt.SourceDefId, sourceDefId, System.StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
