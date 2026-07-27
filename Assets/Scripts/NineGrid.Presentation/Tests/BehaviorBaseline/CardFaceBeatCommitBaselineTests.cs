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
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NineGrid.Presentation.Tests.BehaviorBaseline
{
    /// <summary>
    /// #55/#56 主缝：Runtime 逐拍推进 → 已提交卡面投影。
    /// 攻击/反击：护甲在命中锚点提交；观察型加攻只在收尾锚点提交。
    /// 探索/用道具：表演通道完成后才消费 Impact/Settled（无帧级回调）。
    /// </summary>
    public sealed class CardFaceBeatCommitBaselineTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);
        private static readonly SlotId sFarCornerSlot = SlotId.Board(1);

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
        public void CounterPresent_ImpactThenSettled_ArmorBeforeObserverAttackOnCommittedFace()
        {
            NineGridArchitecture.ResetForTests();
            var architecture = NineGridArchitecture.Current;
            architecture.GetUtility<IConfigUtility>().Set(
                ContentConfigKeys.DefaultCatalog,
                TableNineContentCatalog.CreateDefault());
            InitialGameFactory.Create(architecture, new InitialGameOptions { Seed = 56UL });

            var phase = architecture.GetSystem<IPhaseSystem>();
            var pipeline = architecture.GetSystem<IActionPipelineSystem>();
            var sync = architecture.GetSystem<IPresentationSyncSystem>();
            var dispatcher = new CoreCommandDispatcher(architecture);

            Assert.IsTrue(phase.StartNode(CreateStoneLoverNode()).Accepted);
            PlaceSoleBoardCardAt(architecture, sAdjacentSlot);

            var board = architecture.GetModel<BoardModel>();
            var registry = architecture.GetModel<CardRegistry>();
            var avatar = registry.Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.Attack, 1);
            avatar.Stats.SetBase(StatId.CurrentArmor, 5);

            var monsterUid = board.GetCardUid(sAdjacentSlot);
            Assert.Greater(monsterUid, 0);
            var monster = registry.Get(monsterUid);
            var attackBefore = (int)monster.Stats.GetBase(StatId.Attack);
            Assert.AreEqual(2, attackBefore);

            var avatarArmorBefore = StatArmorUtility.GetCurrentArmor(avatar);
            Assert.Greater(avatarArmorBefore, 0);

            var monsterFace = mCardManager.SpawnView(
                monsterUid,
                defId: monster.DefId,
                kind: CardPresentationKind.Monster);
            monsterFace.CommitPresentation(new CardPresentationSnapshot
            {
                Kind = CardPresentationKind.Monster,
                DefId = monster.DefId,
                DisplayName = "石虾",
                Attack = attackBefore,
                Armor = StatArmorUtility.GetCurrentArmor(monster),
                Hp = (int)monster.Stats.GetBase(StatId.Hp),
                FaceUp = true,
            });

            var avatarFace = mCardManager.SpawnView(
                avatar.Uid,
                defId: avatar.DefId,
                kind: CardPresentationKind.Avatar);
            avatarFace.CommitPresentation(new CardPresentationSnapshot
            {
                Kind = CardPresentationKind.Avatar,
                DefId = avatar.DefId ?? string.Empty,
                Attack = (int)avatar.Stats.GetBase(StatId.Attack),
                Armor = avatarArmorBefore,
                Hp = (int)avatar.Stats.GetBase(StatId.Hp),
                FaceUp = true,
            });

            var hitPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var counterPresent = new ImpactReportingPresentChannel(ticksUntilComplete: 2);
            var factory = new AttackIntentScriptFactory(
                architecture,
                dispatcher,
                hitPresent,
                boardPresent,
                counterPresent);
            mRoot = new PresentationCompositionRoot();
            var runtime = mRoot.Install(factory);

            bool preview;
            Assert.IsTrue(runtime.TrySubmitIntent(
                new InputIntent(InputIntentKinds.Attack, sAdjacentSlot.Index),
                out preview));

            runtime.Tick(0.016f); // resolve hit
            runtime.Tick(0.016f); // present hit → Impact + Settled（怪物掉甲触发的观察型 +1 已上卡面）
            var attackAfterHitSettled = monsterFace.CommittedPresentation.Attack;
            Assert.AreEqual(attackBefore + 1, attackAfterHitSettled, "正面命中收尾后石头爱好者应已 +1");

            runtime.Tick(0.016f); // branch → counter
            runtime.Tick(0.016f); // resolve counter
            Assert.AreEqual(2, sync.ActiveBatchId);
            Assert.AreEqual(avatarArmorBefore, avatarFace.CommittedPresentation.Armor, "反击解算后、命中前 Avatar 护甲仍旧");
            Assert.AreEqual(attackAfterHitSettled, monsterFace.CommittedPresentation.Attack, "反击解算后、收尾前观察型加攻仍旧");

            var expectedAvatarArmor = FindLastRemainingArmor(pipeline, avatar.Uid);
            var expectedAttack = FindLastBaseStatResult(pipeline, monsterUid, StatId.Attack);
            Assert.Less(expectedAvatarArmor, avatarArmorBefore, "反击应掉 Avatar 护甲");
            Assert.AreEqual(attackAfterHitSettled + 1, expectedAttack, "石头爱好者应在反击掉甲后再 +1 攻");

            runtime.Tick(0.016f); // counter present tick 1 → Impact
            Assert.AreEqual(expectedAvatarArmor, avatarFace.CommittedPresentation.Armor, "反击命中锚点护甲应变");
            Assert.AreEqual(attackAfterHitSettled, monsterFace.CommittedPresentation.Attack, "反击命中锚点不得消费观察型加攻");

            runtime.Tick(0.016f); // counter present tick 2 → Settled
            Assert.AreEqual(expectedAttack, monsterFace.CommittedPresentation.Attack, "反击收尾锚点才提交观察型加攻");
            Assert.AreEqual(0, sync.ActiveBatchId);
        }

        [Test]
        public void UseItemPresent_Settled_CommitsHpAfterPresentNotAtResolve()
        {
            NineGridArchitecture.ResetForTests();
            var architecture = NineGridArchitecture.Current;
            architecture.GetUtility<IConfigUtility>().Set(
                ContentConfigKeys.DefaultCatalog,
                TableNineContentCatalog.CreateDefault());
            InitialGameFactory.Create(architecture, new InitialGameOptions { Seed = 5601UL });

            var phase = architecture.GetSystem<IPhaseSystem>();
            var pipeline = architecture.GetSystem<IActionPipelineSystem>();
            var sync = architecture.GetSystem<IPresentationSyncSystem>();
            var dispatcher = new CoreCommandDispatcher(architecture);

            Assert.IsTrue(phase.StartNode(CreateHighHpMonsterNode()).Accepted);
            PlaceSoleBoardCardAt(architecture, sAdjacentSlot);

            var board = architecture.GetModel<BoardModel>();
            var registry = architecture.GetModel<CardRegistry>();
            var monsterUid = board.GetCardUid(sAdjacentSlot);
            var monster = registry.Get(monsterUid);
            var hpBefore = (int)monster.Stats.GetBase(StatId.Hp);
            var armorBefore = StatArmorUtility.GetCurrentArmor(monster);

            var face = mCardManager.SpawnView(
                monsterUid,
                defId: monster.DefId,
                kind: CardPresentationKind.Monster);
            face.CommitPresentation(new CardPresentationSnapshot
            {
                Kind = CardPresentationKind.Monster,
                DefId = monster.DefId,
                DisplayName = "靶子",
                Attack = (int)monster.Stats.GetBase(StatId.Attack),
                Armor = armorBefore,
                Hp = hpBefore,
                FaceUp = true,
            });

            var knifeUid = SpawnHelpIntoItemSlots(architecture, pipeline, "help.throwing_knife");
            var usePresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var factory = new UseItemIntentScriptFactory(
                architecture,
                dispatcher,
                usePresent,
                boardPresent);
            mRoot = new PresentationCompositionRoot();
            var runtime = mRoot.Install(factory);

            bool preview;
            Assert.IsTrue(runtime.TrySubmitIntent(
                new InputIntent(InputIntentKinds.UseItem, knifeUid, new[] { monsterUid }, null),
                out preview));

            runtime.Tick(0.016f); // resolve use
            Assert.AreEqual(1, sync.ActiveBatchId);
            Assert.AreEqual(hpBefore, face.CommittedPresentation.Hp, "用道具解算后、表演前卡面血量仍旧");
            Assert.AreEqual(armorBefore, face.CommittedPresentation.Armor, "用道具解算后、表演前卡面护甲仍旧");

            var expectedHp = FindLastRemainingHp(pipeline, monsterUid);
            var expectedArmor = FindLastRemainingArmorOrDefault(pipeline, monsterUid, armorBefore);
            Assert.IsTrue(
                expectedHp >= 0 && expectedHp < hpBefore || expectedArmor < armorBefore,
                "飞刀本批应改血或甲");

            runtime.Tick(0.016f); // present use → Impact flush + Settled
            if (expectedHp >= 0)
            {
                Assert.AreEqual(expectedHp, face.CommittedPresentation.Hp, "用道具收尾后血量应按指令赋值");
            }

            Assert.AreEqual(expectedArmor, face.CommittedPresentation.Armor, "用道具收尾后护甲应按指令赋值");
            Assert.AreEqual(0, sync.ActiveBatchId);
        }

        [Test]
        public void FourScripts_ReportSettled_BeforeAcknowledge()
        {
            AssertScriptReportsSettled(
                "attack+counter",
                architecture =>
                {
                    Assert.IsTrue(architecture.GetSystem<IPhaseSystem>()
                        .StartNode(CreateHighHpMonsterNode()).Accepted);
                    PlaceSoleBoardCardAt(architecture, sAdjacentSlot);
                    var avatar = architecture.GetModel<CardRegistry>()
                        .Get(architecture.GetModel<BoardModel>().AvatarUid.Value);
                    avatar.Stats.SetBase(StatId.Attack, 1);
                },
                (architecture, dispatcher) => new AttackIntentScriptFactory(
                    architecture,
                    dispatcher,
                    new RecordingPresentChannel(1),
                    new RecordingPresentChannel(1),
                    new RecordingPresentChannel(1)),
                (runtime, architecture, settledCounts) =>
                {
                    runtime.TrySubmitIntent(
                        new InputIntent(InputIntentKinds.Attack, sAdjacentSlot.Index),
                        out _);
                    runtime.Tick(0.016f); // resolve hit
                    runtime.Tick(0.016f); // present hit → Settled
                    Assert.AreEqual(1, settledCounts[0], "攻击 Present 应报 Settled");
                    settledCounts[0] = 0;

                    runtime.Tick(0.016f); // branch
                    runtime.Tick(0.016f); // resolve counter
                    runtime.Tick(0.016f); // present counter → Settled
                    Assert.AreEqual(1, settledCounts[0], "反击 Present 应报 Settled");
                });

            AssertScriptReportsSettled(
                "explore",
                architecture =>
                {
                    Assert.IsTrue(architecture.GetSystem<IPhaseSystem>()
                        .StartNode(CreateHighHpMonsterNode()).Accepted);
                    PlaceSoleBoardCardAt(architecture, sFarCornerSlot);
                    Assert.IsTrue(architecture.GetModel<BoardModel>().IsEmpty(sAdjacentSlot));
                },
                (architecture, dispatcher) => new ExploreIntentScriptFactory(
                    architecture,
                    dispatcher,
                    new RecordingPresentChannel(1)),
                (runtime, architecture, settledCounts) =>
                {
                    runtime.TrySubmitIntent(
                        new InputIntent(InputIntentKinds.Explore, sAdjacentSlot.Index),
                        out _);
                    runtime.Tick(0.016f);
                    runtime.Tick(0.016f);
                    Assert.AreEqual(1, settledCounts[0], "探索 Present 应报 Settled");
                });

            AssertScriptReportsSettled(
                "useItem",
                architecture =>
                {
                    Assert.IsTrue(architecture.GetSystem<IPhaseSystem>()
                        .StartNode(CreateHighHpMonsterNode()).Accepted);
                    PlaceSoleBoardCardAt(architecture, sAdjacentSlot);
                },
                (architecture, dispatcher) => new UseItemIntentScriptFactory(
                    architecture,
                    dispatcher,
                    new RecordingPresentChannel(1),
                    new RecordingPresentChannel(1)),
                (runtime, architecture, settledCounts) =>
                {
                    var monsterUid = architecture.GetModel<BoardModel>().GetCardUid(sAdjacentSlot);
                    var knifeUid = SpawnHelpIntoItemSlots(
                        architecture,
                        architecture.GetSystem<IActionPipelineSystem>(),
                        "help.throwing_knife");
                    runtime.TrySubmitIntent(
                        new InputIntent(InputIntentKinds.UseItem, knifeUid, new[] { monsterUid }, null),
                        out _);
                    runtime.Tick(0.016f);
                    runtime.Tick(0.016f);
                    Assert.AreEqual(1, settledCounts[0], "用道具 Present 应报 Settled");
                });
        }

        private void AssertScriptReportsSettled(
            string label,
            System.Action<IArchitecture> arrange,
            System.Func<IArchitecture, CoreCommandDispatcher, IIntentScriptFactory> createFactory,
            System.Action<IPresentationRuntimeSystem, IArchitecture, int[]> drive)
        {
            if (mRoot != null)
            {
                mRoot.Shutdown(IntentClearReason.PhaseChange);
                mRoot = null;
            }

            NineGridArchitecture.ResetForTests();
            BattleBeatHook.Reset();
            var architecture = NineGridArchitecture.Current;
            architecture.GetUtility<IConfigUtility>().Set(
                ContentConfigKeys.DefaultCatalog,
                TableNineContentCatalog.CreateDefault());
            InitialGameFactory.Create(architecture, new InitialGameOptions { Seed = 5602UL });
            arrange(architecture);

            var dispatcher = new CoreCommandDispatcher(architecture);
            mRoot = new PresentationCompositionRoot();
            var runtime = mRoot.Install(createFactory(architecture, dispatcher));

            var settledCounts = new[] { 0 };
            var previous = BattleBeatHook.ReportBeat;
            BattleBeatHook.ReportBeat = beat =>
            {
                previous?.Invoke(beat);
                if (beat == PresentationBeat.Settled)
                {
                    settledCounts[0]++;
                }
            };

            try
            {
                drive(runtime, architecture, settledCounts);
            }
            catch (AssertionException ex)
            {
                throw new AssertionException("[" + label + "] " + ex.Message, ex);
            }
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

        [Test]
        public void SpawnCard_Settled_Commits_InstructionAbsolutes_NotJsonBirthValues()
        {
            // JSON / 出生展示值（模拟钉回）：攻 2 / 甲 0 / 血 4
            const int jsonBirthAttack = 2;
            const int jsonBirthArmor = 0;
            const int jsonBirthHp = 4;
            // 结算指令绝对值（与出生值不同）
            const int instructionAttack = 7;
            const int instructionArmor = 3;
            const int instructionHp = 99;

            var spawned = mCardManager.SpawnView(9057, "monster.beggar", kind: CardPresentationKind.Monster);
            spawned.CommitPresentation(new CardPresentationSnapshot
            {
                Kind = CardPresentationKind.Monster,
                DefId = "monster.beggar",
                DisplayName = "棕毛土狗",
                Attack = jsonBirthAttack,
                Armor = jsonBirthArmor,
                Hp = jsonBirthHp,
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

            NineGridArchitecture.ResetForTests();

            var spawnEvt = new CoreGameEvent(CoreEventType.CardSpawned, 57, "SpawnCard")
                .WithCard(spawned.Uid)
                .WithRemaining(instructionHp, instructionArmor)
                .WithResultValue(instructionAttack)
                .WithMessage("monster.beggar")
                .WithSource("monster.beggar", "test:#57");

            var batch = new PresentationBatch(
                57,
                new[]
                {
                    new PresentationInstruction(spawnEvt, PresentationEventMap.Get(CoreEventType.CardSpawned)),
                },
                snapshot: null);
            var scheduler = new BattleBeatScheduler(new CardFaceStatHandler());
            scheduler.OnBatchOpened(batch);

            Assert.AreEqual(jsonBirthAttack, spawned.CommittedPresentation.Attack, "Settled 前仍是出生展示值");
            Assert.AreEqual(jsonBirthHp, spawned.CommittedPresentation.Hp);

            scheduler.ReportBeat(PresentationBeat.Settled);

            Assert.AreEqual(instructionAttack, spawned.CommittedPresentation.Attack);
            Assert.AreEqual(instructionArmor, spawned.CommittedPresentation.Armor);
            Assert.AreEqual(instructionHp, spawned.CommittedPresentation.Hp);
            Assert.AreNotEqual(jsonBirthAttack, spawned.CommittedPresentation.Attack);
            Assert.AreNotEqual(jsonBirthHp, spawned.CommittedPresentation.Hp);
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

        private static NodeDeckOptions CreateHighHpMonsterNode()
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.test", CardKind.Monster)
            {
                MaxHp = 99,
                Attack = 1,
                Armor = 3,
                GoldReward = 0
            });
        }

        private static int SpawnHelpIntoItemSlots(
            IArchitecture architecture,
            IActionPipelineSystem pipeline,
            string defId)
        {
            pipeline.Enqueue(new SpawnCardAction(defId, CardKind.HelpCard, ZoneId.ItemSlots, SlotId.None, 1, "test"));
            Assert.Greater(pipeline.RunToCompletion(), 0);
            var deck = architecture.GetModel<DeckModel>();
            Assert.Greater(deck.ItemSlotUids.Count, 0);
            return deck.ItemSlotUids[deck.ItemSlotUids.Count - 1];
        }

        private static int FindLastRemainingArmorOrDefault(
            IActionPipelineSystem pipeline,
            int uid,
            int fallback)
        {
            var entries = pipeline.EventLog.Entries;
            var armor = fallback;
            var found = false;
            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Type == CoreEventType.ArmorChanged
                    && (e.CardUid == uid || e.TargetUid == uid))
                {
                    found = true;
                    armor = e.RemainingArmor;
                }
            }

            return found ? armor : fallback;
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
