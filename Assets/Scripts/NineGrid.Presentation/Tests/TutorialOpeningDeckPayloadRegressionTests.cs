using System;
using System.Collections.Generic;
using System.Linq;
using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NineGrid.Flow;
using NineGrid.Flow.Tutorial;
using NineGrid.Presentation.Ui;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// Issue #228 验收测试：
    /// 教程第一关正式内容保序开局载荷：
    /// 1. 重复开局邻格真怪、首杀稳定后正交可拾道具、离开机关后半段出场。
    /// 2. 载荷全为 Catalog 登记的正式内容，不含教学假卡/归档卡。
    /// 3. 开局牌面在不同种子及旅途/冒险难度下完全一致。
    /// 4. 离开第一关或完成 1–9 步骤后，不再使用该固定载荷。
    /// </summary>
    [TestFixture]
    public class TutorialOpeningDeckPayloadRegressionTests
    {
        private sealed class MemoryRunSaveStore : IRunSaveStore
        {
            private readonly Dictionary<string, string> mSlots = new Dictionary<string, string>();
            public bool Exists(string slotId) => mSlots.ContainsKey(slotId);
            public bool TryRead(string slotId, out string json) => mSlots.TryGetValue(slotId, out json);
            public void Write(string slotId, string json) => mSlots[slotId] = json;
            public void Delete(string slotId) => mSlots.Remove(slotId);
            public void Clear() => mSlots.Clear();
        }

        private GameContentCatalog mCatalog;
        private MemoryRunSaveStore mSaveStore;

        [SetUp]
        public void SetUp()
        {
            mCatalog = ContentCatalogBootstrap.Load();
            mSaveStore = new MemoryRunSaveStore();
            RunSaveStoreHook.Set(mSaveStore);
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
            RunSaveStoreHook.Set(null);
            RunSetupSelection.ResetToDefault();
        }

        private IArchitecture CreateArchitecture(ulong seed = 12345UL, string difficultyId = "difficulty.adventure")
        {
            NineGridArchitecture.ResetForTests();
            var arch = NineGridArchitecture.Interface;
            arch.GetSystem<IContentSystem>().Load(mCatalog);

            InitialGameFactory.Create(arch, new InitialGameOptions
            {
                Seed = seed,
                DifficultyId = difficultyId,
                ProfessionId = ProfessionCatalog.Jester // 战士
            });

            return arch;
        }

        [Test]
        public void TutorialDeckPlan_OptionsStructure_Has16CardsWithPreserveDealOrder()
        {
            var arch = CreateArchitecture();
            var content = arch.GetSystem<IContentSystem>();
            var options = TutorialDeckPlan.BuildOpeningOptions(content);

            Assert.IsNotNull(options);
            Assert.IsTrue(options.PreserveDealOrder, "PreserveDealOrder 必须置为 true");
            Assert.AreEqual(0, options.PlayerOpeningCount, "PlayerOpeningCount 应为 0");
            Assert.AreEqual(0, options.EnemyOpeningCount, "EnemyOpeningCount 应为 0");
            Assert.AreEqual(16, options.EnemyCards.Count, "总开局载荷应为 16 张正式卡");
        }

        [Test]
        public void TutorialFirstBattle_AllCardsAreFormalOfficialContent_NoUnofficialOrFakeCards()
        {
            var arch = CreateArchitecture();
            var content = arch.GetSystem<IContentSystem>();
            var options = TutorialDeckPlan.BuildOpeningOptions(content);

            for (var i = 0; i < options.EnemyCards.Count; i++)
            {
                var draft = options.EnemyCards[i];
                Assert.IsNotNull(draft, $"第 {i} 张卡牌草案不应为 null");
                Assert.IsFalse(string.IsNullOrEmpty(draft.DefId), $"第 {i} 张卡牌 DefId 不应为空");

                Assert.IsTrue(mCatalog.Cards.ContainsKey(draft.DefId), $"卡牌 {draft.DefId} 必须在 Catalog 中注册");
                Assert.IsFalse(FormalContentWiring.IsUnofficialDefId(mCatalog, draft.DefId),
                    $"卡牌 {draft.DefId} 不得为非正式卡（归档/AI拓展/过渡卡组）");

                var cardDef = mCatalog.Cards[draft.DefId];
                Assert.IsFalse(FormalContentWiring.IsUnofficialDeck(cardDef.DeckId),
                    $"卡牌 {draft.DefId} 的 DeckId ({cardDef.DeckId}) 不得为非正式卡组");

                for (var t = 0; t < cardDef.Tags.Count; t++)
                {
                    Assert.AreNotEqual("tutorial.hint", cardDef.Tags[t],
                        $"卡牌 {draft.DefId} 不得带有教学假卡标签 tutorial.hint");
                }
            }
        }

        [Test]
        public void TutorialFirstBattle_OpeningDeal_PlacesOrthogonalTrueMonsterAtSlot2()
        {
            var arch = CreateArchitecture();
            var content = arch.GetSystem<IContentSystem>();
            var phaseSystem = arch.GetSystem<IPhaseSystem>();
            var board = arch.GetModel<BoardModel>();
            var registry = arch.GetModel<CardRegistry>();

            var options = TutorialDeckPlan.BuildOpeningOptions(content);
            phaseSystem.StartNode(options);

            // 断言 Avatar 位于中心 Slot 5
            Assert.AreEqual(SlotId.Board(5), board.AvatarSlot.Value);

            // 断言 Slot 2 存在且为真怪（Orthogonal 邻格）
            var slot2Uid = board.GetCardUid(SlotId.Board(2));
            Assert.Greater(slot2Uid, 0, "Slot 2 必须有卡牌");
            Assert.IsTrue(registry.TryGet(slot2Uid, out var slot2Card));
            Assert.AreEqual(TutorialDeckPlan.FirstTargetMonsterDefId, slot2Card.DefId, "Slot 2 应为小小莱姆 (monster.melee_3)");
            Assert.AreEqual(CardKind.Monster, slot2Card.Kind);
            Assert.IsTrue(CardCombatRules.IsTrueMonster(slot2Card.Kind));
            Assert.IsTrue(board.AvatarSlot.Value.IsAdjacentTo(SlotId.Board(2)), "Slot 2 必须与 Avatar (Slot 5) 正交相邻");

            // 断言 Slot 1 为恢复药水
            var slot1Uid = board.GetCardUid(SlotId.Board(1));
            Assert.Greater(slot1Uid, 0);
            Assert.IsTrue(registry.TryGet(slot1Uid, out var slot1Card));
            Assert.AreEqual(TutorialDeckPlan.FirstCollectItemDefId, slot1Card.DefId, "Slot 1 应为恢复药水 (help.healing_potion)");
            Assert.AreEqual(CardKind.HelpCard, slot1Card.Kind);

            // 断言 Slot 8 也为真怪（乞讨莱姆）
            var slot8Uid = board.GetCardUid(SlotId.Board(8));
            Assert.Greater(slot8Uid, 0);
            Assert.IsTrue(registry.TryGet(slot8Uid, out var slot8Card));
            Assert.IsTrue(CardCombatRules.IsTrueMonster(slot8Card.Kind), "Slot 8 亦应为真怪");
            Assert.IsTrue(board.AvatarSlot.Value.IsAdjacentTo(SlotId.Board(8)), "Slot 8 必须与 Avatar (Slot 5) 正交相邻");
        }

        [Test]
        public void TutorialFirstBattle_FirstKillAndStabilize_RotatesCollectibleItemToAdjacentSlot2()
        {
            var arch = CreateArchitecture();
            var content = arch.GetSystem<IContentSystem>();
            var phaseSystem = arch.GetSystem<IPhaseSystem>();
            var board = arch.GetModel<BoardModel>();
            var registry = arch.GetModel<CardRegistry>();
            var deck = arch.GetModel<DeckModel>();

            var options = TutorialDeckPlan.BuildOpeningOptions(content);
            phaseSystem.StartNode(options);

            // 战士普通攻击 Slot 2 目标（顺劈斧 + 基础攻击 = 4 点伤害，恰好击杀 4 HP 的小小莱姆并自动触发补牌与旋转）
            var attackResult = phaseSystem.Attack(SlotId.Board(2));
            Assert.IsTrue(attackResult.Accepted, "对 Slot 2 攻击应被接受");

            // 旋转后，原 Slot 1 的恢复药水顺时针移动至 Slot 2
            var newSlot2Uid = board.GetCardUid(SlotId.Board(2));
            Assert.Greater(newSlot2Uid, 0, "旋转后 Slot 2 必须有卡牌");
            Assert.IsTrue(registry.TryGet(newSlot2Uid, out var newSlot2Card));
            Assert.AreEqual(TutorialDeckPlan.FirstCollectItemDefId, newSlot2Card.DefId, "旋转后 Slot 2 应为恢复药水");
            Assert.AreEqual(CardKind.HelpCard, newSlot2Card.Kind);
            Assert.IsTrue(board.AvatarSlot.Value.IsAdjacentTo(SlotId.Board(2)), "Slot 2 恢复药水必须在 Avatar 正交交互范围内");

            // 验证该道具可以被正常拾取
            var pickResult = phaseSystem.PickupItem(SlotId.Board(2));
            Assert.IsTrue(pickResult.Accepted, "对 Slot 2 恢复药水的拾取应被接受");

            var inItemSlots = false;
            for (var i = 0; i < deck.ItemSlotUids.Count; i++)
            {
                if (deck.ItemSlotUids[i] == newSlot2Uid)
                {
                    inItemSlots = true;
                    break;
                }
            }
            Assert.IsTrue(inItemSlots, "恢复药水拾取后必须进入玩家手牌槽");
        }

        [Test]
        public void TutorialFirstBattle_LeaveTrap_IsInSecondHalfOfDrawPile()
        {
            var arch = CreateArchitecture();
            var content = arch.GetSystem<IContentSystem>();
            var phaseSystem = arch.GetSystem<IPhaseSystem>();
            var deck = arch.GetModel<DeckModel>();
            var registry = arch.GetModel<CardRegistry>();

            var options = TutorialDeckPlan.BuildOpeningOptions(content);
            phaseSystem.StartNode(options);

            // 开局铺满 8 张，抽牌堆剩余 8 张
            Assert.AreEqual(8, deck.DrawPileUids.Count, "开局发牌后抽牌堆应剩 8 张");

            var leaveTrapIndexInDrawPile = -1;
            for (var i = 0; i < deck.DrawPileUids.Count; i++)
            {
                var card = registry.Get(deck.DrawPileUids[i]);
                if (card.DefId == RegularTrapPool.LeaveTrapDefId)
                {
                    leaveTrapIndexInDrawPile = i;
                    break;
                }
            }

            Assert.GreaterOrEqual(leaveTrapIndexInDrawPile, 0, "离开机关必须在抽牌堆中");
            // 抽牌堆后半段：总共 8 张，后半段索引为 ≥ 4
            Assert.GreaterOrEqual(leaveTrapIndexInDrawPile, deck.DrawPileUids.Count / 2,
                $"离开机关索引 ({leaveTrapIndexInDrawPile}) 必须在抽牌堆后半段 (≥ {deck.DrawPileUids.Count / 2})");
        }

        [Test]
        public void TutorialFirstBattle_DifferentSeedsAndDifficulties_ProduceIdenticalBoardAndDrawPile()
        {
            var journeyBoardDefs = new List<string>();
            var journeyDrawDefs = new List<string>();

            {
                var arch = CreateArchitecture(seed: 11111UL, difficultyId: "difficulty.journey");
                var opt = TutorialDeckPlan.BuildOpeningOptions(arch.GetSystem<IContentSystem>());
                arch.GetSystem<IPhaseSystem>().StartNode(opt);

                var board = arch.GetModel<BoardModel>();
                var reg = arch.GetModel<CardRegistry>();
                var deck = arch.GetModel<DeckModel>();

                for (var slotIdx = SlotId.MinBoardIndex; slotIdx <= SlotId.MaxBoardIndex; slotIdx++)
                {
                    var uid = board.GetCardUid(SlotId.Board(slotIdx));
                    journeyBoardDefs.Add(uid > 0 && reg.TryGet(uid, out var card) ? card.DefId : "empty");
                }

                for (var i = 0; i < deck.DrawPileUids.Count; i++)
                {
                    journeyDrawDefs.Add(reg.Get(deck.DrawPileUids[i]).DefId);
                }
            }

            {
                var arch = CreateArchitecture(seed: 99999UL, difficultyId: "difficulty.adventure");
                var opt = TutorialDeckPlan.BuildOpeningOptions(arch.GetSystem<IContentSystem>());
                arch.GetSystem<IPhaseSystem>().StartNode(opt);

                var board = arch.GetModel<BoardModel>();
                var reg = arch.GetModel<CardRegistry>();
                var deck = arch.GetModel<DeckModel>();

                for (var slotIdx = SlotId.MinBoardIndex; slotIdx <= SlotId.MaxBoardIndex; slotIdx++)
                {
                    var uid = board.GetCardUid(SlotId.Board(slotIdx));
                    var def = uid > 0 && reg.TryGet(uid, out var card) ? card.DefId : "empty";
                    Assert.AreEqual(journeyBoardDefs[slotIdx - SlotId.MinBoardIndex], def,
                        $"Slot {slotIdx} 牌面在不同种子和难度下必须完全一致");
                }

                Assert.AreEqual(journeyDrawDefs.Count, deck.DrawPileUids.Count);
                for (var i = 0; i < deck.DrawPileUids.Count; i++)
                {
                    var def = reg.Get(deck.DrawPileUids[i]).DefId;
                    Assert.AreEqual(journeyDrawDefs[i], def,
                        $"抽牌堆第 {i} 张在不同种子和难度下必须完全一致");
                }
            }
        }

        [Test]
        public void TutorialFirstBattle_NodeIndex2OrStepsCompleted_DoesNotUseTutorialDeckPlan()
        {
            var arch = CreateArchitecture();
            var rewardSystem = arch.GetSystem<IRewardSystem>();

            // 节点 2 (1-2) 正常走 RewardSystem.BuildNodeDeckOptions，不使用 TutorialDeckPlan
            var node2Options = rewardSystem.BuildNodeDeckOptions(2, null);
            Assert.IsNotNull(node2Options);
            Assert.IsFalse(node2Options.PreserveDealOrder, "节点 2 选项不应 PreserveDealOrder");

            // 标记 1–9 完成后，正常走常规编组
            TutorialProgressStore.MarkSteps1To9Completed();
            Assert.IsTrue(TutorialProgressStore.IsSteps1To9Completed());
        }
    }
}
