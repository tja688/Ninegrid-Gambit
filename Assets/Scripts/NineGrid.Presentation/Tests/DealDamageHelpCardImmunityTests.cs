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
    /// ADR-0017：道具卡等非可交战 Kind 对 DealDamage 免疫，避免 HP=0 误走 KillIfDead。
    /// </summary>
    public sealed class DealDamageHelpCardImmunityTests
    {
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
        public void DealDamage_OnBoardHelpCard_DoesNotKillOrRemove()
        {
            var avatar = CreateAvatarOnBoard(SlotId.Center);
            var helpCard = CreateHelpCardOnBoard("help.common_chest_card", SlotId.Board(2));

            Run(new DealDamageAction(avatar.Uid, helpCard.Uid, 2, "test.damage"));

            Assert.AreEqual(ZoneId.Board, helpCard.Zone.Value, "道具卡应仍在盘面");
            Assert.AreEqual(helpCard.Uid, mArch.GetModel<BoardModel>().GetCardUid(SlotId.Board(2)));
            Assert.AreEqual(0, CountDamageDealtTo(helpCard.Uid), "不得发出 DamageDealt");
            Assert.AreEqual(0, CountKills(helpCard.Uid), "不得 Kill 道具卡");
        }

        private void LoadRealCatalog()
        {
            var content = mArch.GetSystem<IContentSystem>();
            content.Load(NineGrid.Content.ContentCatalogBootstrap.Load());
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, content.Catalog);
            Assert.IsTrue(content.HasCatalog, "需要真实内容目录");
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
            mArch.GetModel<BoardModel>().SetAvatar(avatar, slot);
            return avatar;
        }

        private CardInstance CreateHelpCardOnBoard(string defId, SlotId slot)
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

        private int CountDamageDealtTo(int targetUid)
        {
            var count = 0;
            var entries = mArch.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Type == CoreEventType.DamageDealt && entry.TargetUid == targetUid)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountKills(int targetUid)
        {
            var count = 0;
            var entries = mArch.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Type == CoreEventType.CardKilled && entry.TargetUid == targetUid)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
