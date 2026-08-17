using System;
using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NineGrid.Flow.Presentation;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// Opening 路径 <see cref="CardFaceGenerationBootstrap.ApplyFromEventLog"/> 须按序重放
    /// Impact 卡面数值（OnNodeStart GainArmor 等），不能只套 AvatarAppeared 的 Settled 绝对值。
    /// </summary>
    public class CardFaceGenerationBootstrapOpeningTests
    {
        private IArchitecture mArch;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Interface;
            var content = mArch.GetSystem<IContentSystem>();
            content.Load(NineGrid.Content.ContentCatalogBootstrap.Load());
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, content.Catalog);
            Assert.IsTrue(content.HasCatalog, "需要真实内容目录");
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void IsOpeningBootstrapStatEvent_IncludesArmorChanged()
        {
            var evt = new CoreGameEvent(CoreEventType.ArmorChanged, 1, "GainArmor")
                .WithCard(1)
                .WithTarget(1)
                .WithRemaining(10, 7);

            Assert.IsTrue(CardFaceGenerationBootstrap.IsOpeningBootstrapStatEvent(evt));
        }

        [Test]
        public void CompositeArmor_StartNode_BootstrapReplayEndsAtCurrentArmorNotAvatarAppeared()
        {
            var avatar = CreateAvatar(attack: 7, armor: 5);
            Run(new GrantRelicAction("relic.composite_armor"));

            var logStart = mArch.GetSystem<IActionPipelineSystem>().EventLog.Entries.Count;
            StartCombatNode();

            Assert.AreEqual(8, StatArmorUtility.GetCurrentArmor(avatar), "Core 当前甲应为 5+floor(7/2)=8");

            var entries = mArch.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            var replayed = ReplayOpeningBootstrapArmor(entries, logStart, avatar.Uid);

            Assert.AreEqual(8, replayed, "Opening 重放后卡面甲应跟 ArmorChanged(8)，不能停在 AvatarAppeared(5)");

            var legacyReplayed = ReplayLegacySettledOnlyArmor(entries, logStart, avatar.Uid);
            Assert.AreEqual(5, legacyReplayed, "旧逻辑（仅 Settled 生成类）会错误钉在 5——本回归锁此形态");
        }

        [Test]
        public void CompositeArmor_EventLog_ArmorChangedAfterAvatarAppeared_MatchesCurrentArmor()
        {
            var avatar = CreateAvatar(attack: 7, armor: 5);
            Run(new GrantRelicAction("relic.composite_armor"));
            StartCombatNode();

            var entries = mArch.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            var appearedIndex = -1;
            var armorChangedIndex = -1;
            CoreGameEvent armorChanged = null;
            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || e.CardUid != avatar.Uid && e.TargetUid != avatar.Uid)
                {
                    continue;
                }

                if (e.Type == CoreEventType.AvatarAppeared)
                {
                    appearedIndex = i;
                }
                else if (e.Type == CoreEventType.ArmorChanged
                    && e.SourceDefId == "relic.composite_armor")
                {
                    armorChangedIndex = i;
                    armorChanged = e;
                }
            }

            Assert.Greater(appearedIndex, -1, "应有 AvatarAppeared");
            Assert.Greater(armorChangedIndex, appearedIndex, "复合盔甲 GainArmor 须在亮相之后");
            Assert.NotNull(armorChanged);
            Assert.AreEqual(
                StatArmorUtility.GetCurrentArmor(avatar),
                armorChanged.RemainingArmor,
                "ArmorChanged 应携带与 Core 一致的当前甲绝对值");
        }

        /// <summary>模拟 ApplyFromEventLog 对甲的消费语义（与 CardFaceStatHandler.ApplyArmor 同构）。</summary>
        private static int ReplayOpeningBootstrapArmor(IReadOnlyList<CoreGameEvent> entries, int startIndex, int uid)
        {
            var armor = 0;
            var hasArmor = false;
            for (var i = startIndex; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                var isGeneration = IsGenerationFaceEventForTest(entry);
                var isOpeningStat = CardFaceGenerationBootstrap.IsOpeningBootstrapStatEvent(entry);
                if (!isGeneration && !isOpeningStat)
                {
                    continue;
                }

                if (entry.CardUid != uid && entry.TargetUid != uid)
                {
                    continue;
                }

                var map = PresentationEventMap.Get(entry.Type);
                if (map.Beat == PresentationBeat.None)
                {
                    continue;
                }

                if (isGeneration && map.Beat != PresentationBeat.Settled)
                {
                    continue;
                }

                switch (entry.Type)
                {
                    case CoreEventType.AvatarAppeared:
                    case CoreEventType.CardSpawned:
                    case CoreEventType.CardDealt:
                        armor = Math.Max(0, entry.RemainingArmor);
                        hasArmor = true;
                        break;
                    case CoreEventType.ArmorChanged:
                    case CoreEventType.HpChanged:
                    case CoreEventType.Healed:
                        armor = Math.Max(0, entry.RemainingArmor);
                        hasArmor = true;
                        break;
                    case CoreEventType.BaseStatModified when (StatId)entry.Amount == StatId.CurrentArmor:
                        armor = Math.Max(0, entry.ResultValue);
                        hasArmor = true;
                        break;
                }
            }

            return hasArmor ? armor : 0;
        }

        private static int ReplayLegacySettledOnlyArmor(IReadOnlyList<CoreGameEvent> entries, int startIndex, int uid)
        {
            var armor = 0;
            var hasArmor = false;
            for (var i = startIndex; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!IsGenerationFaceEventForTest(entry) || entry.CardUid != uid && entry.TargetUid != uid)
                {
                    continue;
                }

                var map = PresentationEventMap.Get(entry.Type);
                if (map.Beat != PresentationBeat.Settled)
                {
                    continue;
                }

                if (entry.Type == CoreEventType.AvatarAppeared
                    || entry.Type == CoreEventType.CardSpawned
                    || entry.Type == CoreEventType.CardDealt)
                {
                    armor = Math.Max(0, entry.RemainingArmor);
                    hasArmor = true;
                }
            }

            return hasArmor ? armor : 0;
        }

        private static bool IsGenerationFaceEventForTest(CoreGameEvent entry)
        {
            if (entry == null || (entry.CardUid <= 0 && entry.TargetUid <= 0))
            {
                return false;
            }

            return entry.Type == CoreEventType.CardSpawned
                || entry.Type == CoreEventType.CardDealt
                || entry.Type == CoreEventType.AvatarAppeared
                || entry.Type == CoreEventType.ActionCountdownChanged
                || entry.Type == CoreEventType.EffectCountdownChanged
                || entry.Type == CoreEventType.EffectCountdownCleared
                || entry.Type == CoreEventType.CardFaceChanged;
        }

        private CardInstance CreateAvatar(int attack, int armor)
        {
            var avatar = mArch.GetModel<CardRegistry>().Create("avatar.default", CardKind.Avatar);
            avatar.Stats.SetBase(StatId.MaxHp, 20);
            avatar.Stats.SetBase(StatId.Hp, 20);
            avatar.Stats.SetBase(StatId.Attack, attack);
            avatar.Stats.SetBase(StatId.Armor, armor);
            avatar.Stats.SetBase(StatId.CurrentArmor, armor);
            mArch.GetModel<BoardModel>().SetAvatar(avatar, SlotId.Board(5));
            return avatar;
        }

        private void StartCombatNode()
        {
            var run = mArch.GetModel<RunModel>();
            run.NodeIndex.Value = 0;
            run.SetPhase(GamePhase.NodeCompleted);
            var result = mArch.GetSystem<IPhaseSystem>().StartNode(null);
            Assert.IsTrue(result.Accepted, result.Reason);
        }

        private void Run(GameAction action)
        {
            mArch.GetSystem<IActionPipelineSystem>().Execute(action);
        }
    }
}
