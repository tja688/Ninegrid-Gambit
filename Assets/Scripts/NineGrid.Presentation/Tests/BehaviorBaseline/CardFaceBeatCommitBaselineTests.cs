using NineGrid.Cards;
using NineGrid.Cards.Presentation;
using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Content;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Setup;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NineGrid.Presentation.Tests.BehaviorBaseline
{
    /// <summary>
    /// #55 主缝：Runtime 逐拍推进 → 已提交卡面投影。
    /// 护甲在命中锚点提交；观察型加攻只在收尾锚点提交。
    /// </summary>
    public sealed class CardFaceBeatCommitBaselineTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);

        private GameObject mManagerGo;
        private CardManagerSingleton mCardManager;
        private GameObject mChassis;
        private GameObject mMonsterFace;
        private PresentationCompositionRoot mRoot;

        [SetUp]
        public void SetUp()
        {
            DestroyAllCardManagers();
            BattleBeatHook.Reset();

            mManagerGo = new GameObject("CardManager_CardFaceBeatTest");
            mCardManager = mManagerGo.AddComponent<CardManagerSingleton>();
            mChassis = CreateMinimalChassis("Chassis_CardFaceBeat");
            mMonsterFace = CreateMinimalMonsterFace("MonsterFace_CardFaceBeat");
            mCardManager.ConfigureChassisAndFaces(
                mChassis,
                mMonsterFace,
                mMonsterFace,
                mMonsterFace,
                mMonsterFace);
            CardEntityLifecycleHook.RequestWire(mCardManager, hand: null, deck: null);
        }

        [TearDown]
        public void TearDown()
        {
            if (mRoot != null)
            {
                mRoot.Shutdown(IntentClearReason.PhaseChange);
                mRoot = null;
            }

            BattleBeatHook.Reset();
            CardEntityLifecycleHook.Reset();

            if (mManagerGo != null)
            {
                Object.DestroyImmediate(mManagerGo);
                mManagerGo = null;
            }

            if (mChassis != null)
            {
                Object.DestroyImmediate(mChassis);
                mChassis = null;
            }

            if (mMonsterFace != null)
            {
                Object.DestroyImmediate(mMonsterFace);
                mMonsterFace = null;
            }

            DestroyAllCardManagers();
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void AttackPresent_ImpactThenSettled_ArmorBeforeObserverAttackOnCommittedFace()
        {
            NineGridArchitecture.ResetForTests();
            var architecture = NineGridArchitecture.Current;
            architecture.GetUtility<IConfigUtility>().Set(
                ContentConfigKeys.DefaultCatalog,
                TableNineContentCatalog.CreateDefault());
            InitialGameFactory.Create(architecture, new InitialGameOptions { Seed = 55UL });

            var phase = architecture.GetSystem<IPhaseSystem>();
            var pipeline = architecture.GetSystem<IActionPipelineSystem>();
            var sync = architecture.GetSystem<IPresentationSyncSystem>();
            var dispatcher = new CoreCommandDispatcher(architecture);

            Assert.IsTrue(phase.StartNode(CreateStoneLoverNode()).Accepted);
            PlaceSoleBoardCardAt(architecture, sAdjacentSlot);

            var board = architecture.GetModel<BoardModel>();
            var registry = architecture.GetModel<CardRegistry>();
            var avatar = registry.Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.Attack, 3);

            var monsterUid = board.GetCardUid(sAdjacentSlot);
            Assert.Greater(monsterUid, 0);
            var monster = registry.Get(monsterUid);
            var armorBefore = StatArmorUtility.GetCurrentArmor(monster);
            var attackBefore = (int)monster.Stats.GetBase(StatId.Attack);
            Assert.Greater(armorBefore, 0, "石虾应有护甲以便命中掉甲");
            Assert.AreEqual(2, attackBefore);

            var face = mCardManager.SpawnView(
                monsterUid,
                defId: monster.DefId,
                kind: CardPresentationKind.Monster);
            face.CommitPresentation(new CardPresentationSnapshot
            {
                Kind = CardPresentationKind.Monster,
                DefId = monster.DefId,
                DisplayName = "石虾",
                Attack = attackBefore,
                Armor = armorBefore,
                Hp = (int)monster.Stats.GetBase(StatId.Hp),
                FaceUp = true,
            });

            var hitPresent = new ImpactReportingPresentChannel(ticksUntilComplete: 2);
            var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var factory = new AttackIntentScriptFactory(
                architecture,
                dispatcher,
                hitPresent,
                boardPresent);
            mRoot = new PresentationCompositionRoot();
            var runtime = mRoot.Install(factory);

            bool preview;
            Assert.IsTrue(runtime.TrySubmitIntent(
                new InputIntent(InputIntentKinds.Attack, sAdjacentSlot.Index),
                out preview));

            runtime.Tick(0.016f); // Resolve CombatHit → OpenBatch → OnBatchOpened
            Assert.AreEqual(1, sync.ActiveBatchId);
            Assert.AreEqual(armorBefore, face.CommittedPresentation.Armor, "解算后、命中前卡面护甲仍是旧值");
            Assert.AreEqual(attackBefore, face.CommittedPresentation.Attack, "解算后、命中前卡面攻击仍是旧值");

            var expectedArmor = FindLastRemainingArmor(pipeline, monsterUid);
            var expectedAttack = FindLastBaseStatResult(pipeline, monsterUid, StatId.Attack);
            Assert.Less(expectedArmor, armorBefore, "本批应掉甲");
            Assert.AreEqual(attackBefore + 1, expectedAttack, "石头爱好者本批应 +1 攻");

            runtime.Tick(0.016f); // Present tick 1 → Impact
            Assert.AreEqual(expectedArmor, face.CommittedPresentation.Armor, "命中锚点护甲应变");
            Assert.AreEqual(attackBefore, face.CommittedPresentation.Attack, "命中锚点不得消费观察型加攻");
            var expectedHp = FindLastRemainingHp(pipeline, monsterUid);
            if (expectedHp >= 0)
            {
                Assert.AreEqual(expectedHp, face.CommittedPresentation.Hp, "命中锚点血量应按指令绝对值赋值");
            }

            runtime.Tick(0.016f); // Present tick 2 → Settled → ack
            Assert.AreEqual(expectedArmor, face.CommittedPresentation.Armor);
            Assert.AreEqual(expectedAttack, face.CommittedPresentation.Attack, "收尾锚点才提交观察型加攻");
            Assert.AreEqual(0, sync.ActiveBatchId);
        }

        [Test]
        public void CardFaceStatHandler_DoesNotTouch_CardRegistry_Or_StatSystem()
        {
            var spawned = mCardManager.SpawnView(9001, "monster.guard", kind: CardPresentationKind.Monster);
            spawned.CommitPresentation(new CardPresentationSnapshot
            {
                Kind = CardPresentationKind.Monster,
                DefId = "monster.guard",
                Attack = 8,
                Armor = 4,
                Hp = 10,
                FaceUp = true,
            });

            CardEntityLifecycleHook.TryGet = (int uid, out ManagedCard found) =>
            {
                if (uid == spawned.Uid)
                {
                    found = spawned;
                    return true;
                }

                found = null;
                return false;
            };

            var handler = new CardFaceStatHandler();
            var armorEvt = new CoreGameEvent(CoreEventType.ArmorChanged, 1, "test")
                .WithCard(spawned.Uid)
                .WithTarget(spawned.Uid)
                .WithRemaining(10, 1);
            var attackEvt = new CoreGameEvent(CoreEventType.BaseStatModified, 1, "test")
                .WithCard(spawned.Uid)
                .WithTarget(spawned.Uid)
                .WithAmount((int)StatId.Attack)
                .WithDelta(1)
                .WithResultValue(9);

            // 不注册 Architecture / CardRegistry：若 handler 回头读内核会抛。
            NineGridArchitecture.ResetForTests();

            handler.Apply(new PresentationInstruction(armorEvt, PresentationEventMap.Get(CoreEventType.ArmorChanged)));
            handler.Apply(new PresentationInstruction(attackEvt, PresentationEventMap.Get(CoreEventType.BaseStatModified)));

            Assert.AreEqual(1, spawned.CommittedPresentation.Armor);
            Assert.AreEqual(9, spawned.CommittedPresentation.Attack);
            Assert.AreEqual(10, spawned.CommittedPresentation.Hp);
        }

        private static NodeDeckOptions CreateStoneLoverNode()
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.stone_shrimp", CardKind.Monster)
            {
                MaxHp = 10,
                Attack = 2,
                Armor = 5,
                GoldReward = 1
            }.AddEffect("skill.stone_lover.armor_lost"));
        }

        private static void PlaceSoleBoardCardAt(IArchitecture architecture, SlotId targetSlot)
        {
            var board = architecture.GetModel<BoardModel>();
            var registry = architecture.GetModel<CardRegistry>();
            CardInstance sole = null;
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

                Assert.IsNull(sole);
                sole = registry.Get(uid);
            }

            Assert.IsNotNull(sole);
            if (sole.Slot.Value == targetSlot)
            {
                return;
            }

            board.ClearSlot(sole.Slot.Value);
            board.PlaceCard(sole, targetSlot);
        }

        private static int FindLastRemainingArmor(IActionPipelineSystem pipeline, int uid)
        {
            var entries = pipeline.EventLog.Entries;
            var armor = -1;
            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Type == CoreEventType.ArmorChanged
                    && (e.CardUid == uid || e.TargetUid == uid))
                {
                    armor = e.RemainingArmor;
                }
            }

            Assert.GreaterOrEqual(armor, 0, "应有 ArmorChanged");
            return armor;
        }

        /// <returns>若本批无 HpChanged 则 -1。</returns>
        private static int FindLastRemainingHp(IActionPipelineSystem pipeline, int uid)
        {
            var entries = pipeline.EventLog.Entries;
            var hp = -1;
            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if ((e.Type == CoreEventType.HpChanged || e.Type == CoreEventType.Healed)
                    && (e.CardUid == uid || e.TargetUid == uid))
                {
                    hp = e.RemainingHp;
                }
            }

            return hp;
        }

        private static int FindLastBaseStatResult(IActionPipelineSystem pipeline, int uid, StatId stat)
        {
            var entries = pipeline.EventLog.Entries;
            var found = false;
            var value = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Type == CoreEventType.BaseStatModified
                    && (e.CardUid == uid || e.TargetUid == uid)
                    && e.Amount == (int)stat)
                {
                    found = true;
                    value = e.ResultValue;
                }
            }

            Assert.IsTrue(found, "应有 BaseStatModified");
            return value;
        }

        private static GameObject CreateMinimalChassis(string name)
        {
            var root = new GameObject(name);
            root.AddComponent<StandardCardView>();
            var facePivot = new GameObject("FacePivot");
            facePivot.transform.SetParent(root.transform, false);
            return root;
        }

        private static GameObject CreateMinimalMonsterFace(string name)
        {
            return new GameObject(name);
        }

        private static void DestroyAllCardManagers()
        {
            var managers = Object.FindObjectsByType<CardManagerSingleton>(FindObjectsSortMode.None);
            for (var i = 0; i < managers.Length; i++)
            {
                if (managers[i] != null)
                {
                    Object.DestroyImmediate(managers[i].gameObject);
                }
            }
        }

        /// <summary>第 1 拍报 Impact，第 N 拍完成——模拟攻击命中帧后再收势。</summary>
        private sealed class ImpactReportingPresentChannel : IPresentChannel
        {
            private readonly int mTicksUntilComplete;
            private int mTicks;
            private bool mBegan;
            private bool mImpactReported;

            public ImpactReportingPresentChannel(int ticksUntilComplete)
            {
                mTicksUntilComplete = ticksUntilComplete;
            }

            public int ActiveBatchId { get; private set; }

            public bool IsComplete
            {
                get { return mBegan && mTicks >= mTicksUntilComplete; }
            }

            public void Begin(int batchId)
            {
                mBegan = true;
                mTicks = 0;
                mImpactReported = false;
                ActiveBatchId = batchId;
            }

            public void Tick(float deltaTime)
            {
                if (!mBegan)
                {
                    return;
                }

                mTicks++;
                if (!mImpactReported && mTicks >= 1)
                {
                    mImpactReported = true;
                    BattleBeatHook.NotifyBeat(PresentationBeat.Impact);
                }
            }
        }
    }
}
