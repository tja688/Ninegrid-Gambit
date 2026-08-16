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
    /// 暴力卡（help.brutality_card）回归：使用后玩家下一次对怪交战伤害 ×2（DamageMultiplier
    /// Once，条件＝动作 DealDamage / 施法者玩家 / 目标怪物 / 来源为空＝玩家直接交战攻击）。
    /// 策划报告 bug：捡道具卡或使用道具卡也会把攻击力移除（乘区被吞）。
    /// 根因：遗物等带 defId 来源的玩家施法伤害（如 junk_launcher「使用道具卡时对随机怪物
    /// 造成 2 点伤害」）同样满足旧条件（仅排除 help.*），把 ×2 当成自己的下一击消费掉。
    /// 修复：新增 requireEmptySource 条件，乘区只被玩家「普通交战攻击」（来源为空）消费。
    /// 未消费时随战斗结束（ClearNodeTransientModifiers 清 Once）复原。
    /// </summary>
    public class BrutalityCardMultiplierRegressionTests
    {
        private IArchitecture mArch;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Interface;
            mArch.GetModel<RunModel>().SetPhase(GamePhase.InteractionLoop);
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void BrutalityMultiplier_SurvivesUsingOtherHelpCard_WithJunkLauncherMounted()
        {
            LoadRealCatalog();
            CreateAvatarOnBoard(SlotId.Board(5));
            CreateRealCardOnBoard("monster.salamander", SlotId.Board(4));
            Run(new GrantRelicAction("relic.junk_launcher"));

            var brutality = CreateItemCard("help.brutality_card");
            var potion = CreateItemCard("help.healing_potion");

            var phase = mArch.GetSystem<IPhaseSystem>();
            Assert.IsTrue(phase.ApplyUseItem(brutality.Uid, null, null).Accepted, "使用暴力卡应被接受");
            Assert.AreEqual(
                1,
                CountDamageMultiplierOnce(),
                "暴力卡使用后应挂上 ×2 乘区");

            Assert.IsTrue(phase.ApplyUseItem(potion.Uid, null, null).Accepted, "使用回复药水应被接受");
            Assert.AreEqual(
                1,
                CountDamageMultiplierOnce(),
                "使用道具卡（回复药水）不得吞掉暴力卡 ×2——乘区只应被玩家普通交战攻击消费");
        }

        [Test]
        public void BrutalityMultiplier_SurvivesPickingUpHelpCard()
        {
            LoadRealCatalog();
            CreateAvatarOnBoard(SlotId.Board(5));
            CreateRealCardOnBoard("monster.salamander", SlotId.Board(4));

            var brutality = CreateItemCard("help.brutality_card");
            var pickup = CreateRealCardOnBoard("help.gold_card", SlotId.Board(2));

            var phase = mArch.GetSystem<IPhaseSystem>();
            Assert.IsTrue(phase.ApplyUseItem(brutality.Uid, null, null).Accepted, "使用暴力卡应被接受");
            Assert.AreEqual(
                1,
                CountDamageMultiplierOnce(),
                "暴力卡使用后应挂上 ×2 乘区");

            Assert.IsTrue(phase.ApplyPickupItem(SlotId.Board(2)).Accepted, "捡起道具卡应被接受");
            Assert.AreEqual(
                1,
                CountDamageMultiplierOnce(),
                "捡道具卡不得吞掉暴力卡 ×2——乘区只应被玩家普通交战攻击消费");
        }

        [Test]
        public void BrutalityMultiplier_IsConsumedByDirectAttack_AndDoublesDamage()
        {
            LoadRealCatalog();
            var avatar = CreateAvatarOnBoard(SlotId.Board(5));
            var monster = CreateRealCardOnBoard("monster.salamander", SlotId.Board(4));
            var brutality = CreateItemCard("help.brutality_card");

            var phase = mArch.GetSystem<IPhaseSystem>();
            Assert.IsTrue(phase.ApplyUseItem(brutality.Uid, null, null).Accepted, "使用暴力卡应被接受");
            Assert.AreEqual(1, CountDamageMultiplierOnce(), "暴力卡使用后应挂上 ×2 乘区");

            var hit = phase.ApplyCombatHit(avatar.Uid, monster.Uid);
            Assert.IsTrue(hit.Accepted, "玩家直接交战攻击应被接受");
            Assert.AreEqual(0, CountDamageMultiplierOnce(), "玩家直接交战攻击应消费 ×2 乘区");

            var dealt = FindDamageDealtTo(monster.Uid);
            Assert.IsNotNull(dealt, "应产生对怪物的伤害事件");
            Assert.AreEqual(4, dealt.Amount, "2 攻 ×2 = 4 伤害");
        }

        // ==================== 基建 ====================

        private void LoadRealCatalog()
        {
            var content = mArch.GetSystem<IContentSystem>();
            content.Load(NineGrid.Content.ContentCatalogBootstrap.Load());
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, content.Catalog);
            Assert.IsTrue(content.HasCatalog, "需要真实内容目录");
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

        private void Run(GameAction action)
        {
            mArch.GetSystem<IActionPipelineSystem>().Execute(action);
        }

        private int CountDamageMultiplierOnce()
        {
            var modifiers = mArch.GetSystem<IStatSystem>().RuleModifiers.Modifiers;
            var count = 0;
            for (var i = 0; i < modifiers.Count; i++)
            {
                var modifier = modifiers[i];
                if (modifier.Rule == RuleId.DamageMultiplier && modifier.Scope == ModifierScope.Once)
                {
                    count++;
                }
            }

            return count;
        }

        private CoreGameEvent FindDamageDealtTo(int targetUid)
        {
            var entries = mArch.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                if (entry.Type == CoreEventType.DamageDealt && entry.TargetUid == targetUid)
                {
                    return entry;
                }
            }

            return null;
        }
    }
}
