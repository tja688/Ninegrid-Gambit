using System.IO;
using NineGrid.Core.Commands;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Core.Tests.Simulation;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    public sealed class P8HeadlessSimulationTests
    {
        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void AvatarFatalDamageSwitchesToDefeatAndLocksCommands()
        {
            InitialGameFactory.Create(NineGridArchitecture.Current);
            var architecture = NineGridArchitecture.Current;
            var board = architecture.GetModel<BoardModel>();
            var avatarUid = board.AvatarUid.Value;

            architecture.GetSystem<IActionPipelineSystem>().Execute(new DealDamageAction(0, avatarUid, 999));

            var phase = architecture.GetSystem<IPhaseSystem>();
            Assert.AreEqual(GamePhase.Defeat, phase.CurrentPhase);
            Assert.IsFalse(phase.CanExecute(GameCommandKind.StartNode));
            Assert.IsFalse(phase.CanExecute(GameCommandKind.Attack));
        }

        [Test]
        public void EnteringRoomAfterLastBossNodeSetsVictory()
        {
            InitialGameFactory.Create(NineGridArchitecture.Current);
            P5CatalogTestSupport.RegisterCatalog(NineGridArchitecture.Current);
            var architecture = NineGridArchitecture.Current;
            var run = architecture.GetModel<RunModel>();
            var pending = architecture.GetModel<PendingChoiceModel>();

            for (var i = 0; i < 26; i++)
            {
                run.AdvanceNode();
            }

            Assert.AreEqual(3, run.Floor.Value);
            Assert.AreEqual(8, run.NodeIndex.Value);
            run.SetPhase(GamePhase.RoomEvent);
            pending.OfferRooms(new[] { RoomKind.Gold });
            pending.SelectRoom(RoomKind.Gold);

            var result = architecture.SendCommand(new EnterRoomCommand());

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(GamePhase.Victory, run.Phase.Value);
            Assert.AreEqual(3, run.Floor.Value);
            Assert.AreEqual(RunModel.NodesPerFloor, run.NodeIndex.Value);
            Assert.IsFalse(architecture.GetSystem<IPhaseSystem>().CanExecute(GameCommandKind.StartNode));
        }

        [Test]
        public void HeadlessInvariantReportsRuntimeEffectOwnedByRemovedCard()
        {
            InitialGameFactory.Create(NineGridArchitecture.Current);
            var architecture = NineGridArchitecture.Current;
            var registry = architecture.GetModel<CardRegistry>();
            var effectSystem = architecture.GetSystem<IEffectSystem>();
            var removed = registry.Create("monster.leak.owner", CardKind.Monster);
            removed.Zone.Value = ZoneId.Removed;

            var definition = effectSystem.ParseJson(
                "{"
                + "\"id\":\"test.runtime.leak\","
                + "\"typeTag\":\"【类型怪物技能】\","
                + "\"containerType\":\"MonsterSkill\","
                + "\"kind\":\"Triggered\","
                + "\"trigger\":{\"atom\":\"OnUseHelpCard\"},"
                + "\"target\":{\"atom\":\"Player\"},"
                + "\"action\":{\"atom\":\"GainArmor\",\"amount\":1}"
                + "}");
            effectSystem.Activate(definition, new EffectOwner(EffectContainerType.MonsterSkill, "test.runtime.leak", removed.Uid));

            var checker = new HeadlessInvariantChecker(architecture.GetModel<PlayerModel>().Coins.Value);
            checker.CheckNewEvents(architecture);

            Assert.AreEqual(1, checker.Issues.Count);
            StringAssert.Contains("Runtime effect leak", checker.Issues[0].Message);
            StringAssert.Contains(removed.Uid.ToString(), checker.Issues[0].Message);
        }

        [Test]
        public void HeadlessCampaignRunsFullFlowAndWritesTranscript()
        {
            var result = new HeadlessCampaignDriver().Run(new HeadlessSimulationOptions
            {
                ContentSeed = 7UL,
                AgentSeed = 1707UL,
                CatalogOverride = CreateSmokeCatalog(),
                AvatarMaxHp = 100,
                AvatarAttack = 99,
                AvatarRecovery = 1,
                MaxCommandsPerNode = 80,
                MaxTotalCommands = 4000,
                NoProgressCommandLimit = 40,
                TranscriptDirectory = "Logs/sim/tests",
                WriteTranscript = true
            });

            Assert.AreEqual(HeadlessRunOutcome.Victory, result.Outcome, result.StopReason);
            Assert.AreEqual(GamePhase.Victory, result.FinalPhase);
            Assert.AreEqual(3, result.FinalFloor);
            Assert.AreEqual(RunModel.NodesPerFloor, result.FinalNodeIndex);
            Assert.AreEqual(0, result.InvariantIssues.Count);
            Assert.AreEqual(3, result.DeckSelections.Count);
            Assert.Greater(result.Commands.Count, 0);
            Assert.Greater(result.MonsterKills, 0);
            Assert.IsTrue(File.Exists(result.TranscriptPath), result.TranscriptPath);
            Assert.IsTrue(result.Transcript.Contains("Outcome: Victory"));
        }

        [Test]
        public void HeadlessBatchRunnerExportsSequentialMetrics()
        {
            var result = new HeadlessBatchRunner().Run(new HeadlessBatchOptions
            {
                StartContentSeed = 20UL,
                StartAgentSeed = 2020UL,
                Count = 2,
                OutputDirectory = "Logs/sim/tests",
                SimulationOptions = new HeadlessSimulationOptions
                {
                    AvatarMaxHp = 10000,
                    AvatarAttack = 99,
                    MaxTotalCommands = 5,
                    WriteTranscript = false
                }
            });

            Assert.AreEqual(2, result.Runs.Count);
            Assert.IsTrue(File.Exists(result.CsvPath), result.CsvPath);
            Assert.IsTrue(File.Exists(result.JsonPath), result.JsonPath);
        }

        private static GameContentCatalog CreateSmokeCatalog()
        {
            var catalog = new GameContentCatalog();
            AddSmokeDeck(catalog, "deck.smoke.weak", MonsterDeckKind.WeakElite, "monster.smoke.weak");
            AddSmokeDeck(catalog, "deck.smoke.strong", MonsterDeckKind.StrongElite, "monster.smoke.strong");
            AddSmokeDeck(catalog, "deck.smoke.boss", MonsterDeckKind.Boss, "monster.smoke.boss");

            catalog.AddCard(new CardContentDefinition("help.smoke", "Smoke Help", CardKind.HelpCard));
            catalog.Rewards
                .AddPool(new RewardPoolDefinition("help.choice", 1)
                    .Add("help.smoke", CardKind.HelpCard, 1))
                .AddRoom(new RoomDefinition(RoomKind.Gold, "Gold") { Weight = 1 });

            for (var node = 1; node <= RunModel.NodesPerFloor; node++)
            {
                catalog.Rewards.AddNodeRule(new NodeDeckRule
                {
                    NodeIndex = node,
                    TotalMonsterCount = 1,
                    Level1Min = 1,
                    Level1Max = 1,
                    DeckKind = node <= 3
                        ? MonsterDeckKind.WeakElite
                        : node <= 6 ? MonsterDeckKind.StrongElite : MonsterDeckKind.Boss
                });
            }

            return catalog;
        }

        private static void AddSmokeDeck(
            GameContentCatalog catalog,
            string deckId,
            MonsterDeckKind kind,
            string monsterDefId)
        {
            var deck = new MonsterDeckDefinition(deckId, deckId, kind);
            deck.AddMonster(monsterDefId);
            catalog.AddMonsterDeck(deck);
            catalog.AddCard(new CardContentDefinition(monsterDefId, monsterDefId, CardKind.Monster)
                .WithStats(1, 0, 0)
                .WithLevel(1)
                .InDeck(deckId));
        }
    }
}
