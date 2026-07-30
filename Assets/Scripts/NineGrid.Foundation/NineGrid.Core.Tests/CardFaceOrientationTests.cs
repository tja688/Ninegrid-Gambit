using System.Collections.Generic;
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
    /// ADR-0016：牌面朝向权威、Flip/Reveal 事件、背面双向惰性、FaceDownTick、ActiveWhileFaceDown。
    /// </summary>
    public sealed class CardFaceOrientationTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;
        private IEffectSystem mEffects;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(
                ContentConfigKeys.DefaultCatalog,
                ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 42UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
            mEffects = mArch.GetSystem<IEffectSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void FlipCardAction_TogglesFaceUp_AndEmitsCardFaceChanged()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 5, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            var card = GetSoleBoardCard();
            Assert.IsTrue(card.FaceUp);

            var startIndex = mPipeline.EventLog.Entries.Count;
            mPipeline.Enqueue(new FlipCardAction(card.Uid));
            mPipeline.RunToCompletion();

            Assert.IsFalse(card.FaceUp);
            var faceEvt = FindLast(startIndex, CoreEventType.CardFaceChanged);
            Assert.IsNotNull(faceEvt);
            Assert.AreEqual(card.Uid, faceEvt.CardUid);
            Assert.AreEqual(0, faceEvt.ResultValue);
        }

        [Test]
        public void RevealFace_OnlyAcceptsFaceDownAdjacent()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 5, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            var card = GetSoleBoardCard();
            Assert.IsFalse(mPhase.RevealFace(sAdjacentSlot).Accepted, "Already face-up");

            card.FaceUp = false;
            Assert.IsTrue(mPhase.RevealFace(sAdjacentSlot).Accepted);
            Assert.IsTrue(card.FaceUp);
        }

        [Test]
        public void FaceOrientationAtoms_AndActiveWhileFaceDownToken_AreRegistered()
        {
            Assert.IsTrue(mEffects.AtomRegistry.Triggers.ContainsKey("OnFlip"));
            Assert.IsTrue(mEffects.AtomRegistry.Actions.ContainsKey("Flip"));
            Assert.IsTrue(mEffects.AtomRegistry.Conditions.ContainsKey("IsFaceUp"));
            Assert.IsTrue(EffectRequiresValidator.IsKnownToken(EffectRequireTokens.ActiveWhileFaceDown));
            Assert.IsTrue(EffectRequireTokens.HasActiveWhileFaceDown(
                mEffects.ParseJson(
                    @"{""id"":""test.flip.exempt"",""kind"":""Triggered"",""containerType"":""MonsterSkill"","
                    + @"""requires"":[""HasOwnerEntity"",""CardZone:Board"",""ActiveWhileFaceDown""],"
                    + @"""trigger"":{""atom"":""OnFlip""},"
                    + @"""target"":{""atom"":""Self""},"
                    + @"""action"":{""atom"":""Flip""}}")));
        }

        [Test]
        public void Attack_RejectsFaceDownTarget()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 5, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            var card = GetSoleBoardCard();
            card.FaceUp = false;

            var result = mPhase.Attack(sAdjacentSlot);
            Assert.IsFalse(result.Accepted);
            StringAssert.Contains("face-down", result.Reason);
        }

        [Test]
        public void RegisterEnemyAction_FaceDown_DoesNotTickAttackCountdown_OrEnterRoster()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyNode()).Accepted);
            var faceDownUid = SpawnOrthogonalMelee(sAdjacentSlot, hp: 5, attack: 3, countdown: 2);
            var faceUpUid = SpawnOrthogonalMelee(SlotId.Board(4), hp: 5, attack: 1, countdown: 2);
            Registry().Get(faceDownUid).FaceUp = false;

            Assert.IsTrue(mPhase.RegisterEnemyActionPhase().Accepted);

            Assert.AreEqual(2, Countdown(faceDownUid), "背面应冻结攻击倒计时");
            Assert.AreEqual(1, Countdown(faceUpUid), "正面仍 −1");
            CollectionAssert.DoesNotContain(mPhase.PendingEnemyActionUids, faceDownUid);
            CollectionAssert.DoesNotContain(mPhase.PendingEnemyActionUids, faceUpUid);
        }

        [Test]
        public void ResolveNext_FaceDown_DoesNotDamage_OrResetCountdown()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyNode()).Accepted);
            var monsterUid = SpawnOrthogonalMelee(sAdjacentSlot, hp: 5, attack: 3, countdown: 1);
            PrepareAvatar(hp: 20, armor: 0, attack: 0);
            var avatarHpBefore = AvatarHp();

            // 先正面报名进名单，再翻到背面，模拟齐射中途翻面。
            Assert.IsTrue(mPhase.RegisterEnemyActionPhase().Accepted);
            CollectionAssert.AreEqual(new[] { monsterUid }, mPhase.PendingEnemyActionUids);
            Registry().Get(monsterUid).FaceUp = false;

            Assert.IsTrue(mPhase.ResolveNextEnemyAction().Accepted);

            Assert.AreEqual(avatarHpBefore, AvatarHp(), "背面不得开火");
            Assert.AreEqual(0, Countdown(monsterUid), "背面结算不得 Reset 倒计时");
        }

        [Test]
        public void DealDamage_SkipsFaceDownTarget()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 8, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            var card = GetSoleBoardCard();
            card.FaceUp = false;
            var hpBefore = (int)card.Stats.GetBase(StatId.Hp);
            var avatarUid = mArch.GetModel<BoardModel>().AvatarUid.Value;

            mPipeline.Enqueue(new DealDamageAction(avatarUid, card.Uid, 4));
            mPipeline.RunToCompletion();

            Assert.AreEqual(hpBefore, (int)card.Stats.GetBase(StatId.Hp), "DealDamage 不得击中背面");
        }

        [Test]
        public void Bomb_AllMonsters_SkipsFaceDown()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyNode()).Accepted);
            var downUid = SpawnOrthogonalMelee(sAdjacentSlot, hp: 8, attack: 0, countdown: 3);
            var upUid = SpawnOrthogonalMelee(SlotId.Board(4), hp: 8, attack: 0, countdown: 3);
            Registry().Get(downUid).FaceUp = false;
            var downHp = (int)Registry().Get(downUid).Stats.GetBase(StatId.Hp);
            var upHp = (int)Registry().Get(upUid).Stats.GetBase(StatId.Hp);

            mPipeline.Enqueue(new SpawnCardAction(
                "help.bomb",
                CardKind.HelpCard,
                ZoneId.ItemSlots,
                SlotId.None,
                1,
                "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            var bombUid = mArch.GetModel<DeckModel>().ItemSlotUids[
                mArch.GetModel<DeckModel>().ItemSlotUids.Count - 1];

            Assert.IsTrue(mPhase.ApplyUseItem(bombUid, null, null).Accepted, "爆弹 UseItem 应接受");

            Assert.AreEqual(downHp, (int)Registry().Get(downUid).Stats.GetBase(StatId.Hp), "爆弹不得打背面");
            Assert.AreEqual(upHp - 4, (int)Registry().Get(upUid).Stats.GetBase(StatId.Hp), "正面仍受伤");
        }

        [Test]
        public void FaceDownTick_ViaEnemyRegister_DecrementsAndCollects()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyNode()).Accepted);
            var uid = SpawnOrthogonalMelee(sAdjacentSlot, hp: 5, attack: 0, countdown: 5);
            var card = Registry().Get(uid);
            card.FaceUp = false;
            FaceDownTickCounters.Register(card, "skill.delayed_flip", 2);
            var attackBefore = Countdown(uid);

            Assert.IsTrue(mPhase.RegisterEnemyActionPhase().Accepted);
            Assert.AreEqual(attackBefore, Countdown(uid), "背面攻击倒计时冻结");
            Assert.AreEqual(1, FaceDownTickRemaining(card, "skill.delayed_flip"));

            Assert.IsTrue(mPhase.RegisterEnemyActionPhase().Accepted);
            Assert.AreEqual(0, FaceDownTickRemaining(card, "skill.delayed_flip"));

            var entries = new List<FaceDownTickEntry>();
            FaceDownTickCounters.Collect(card, entries);
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("skill.delayed_flip", entries[0].RegistrationId);
            Assert.AreEqual(0, entries[0].Remaining);

            var fired = new List<FaceDownTickFire>();
            FaceDownTickCounters.TickFaceDownBoard(
                mArch.GetModel<BoardModel>(),
                Registry(),
                fired);
            Assert.AreEqual(0, fired.Count, "已为 0 不再重复 fire");
        }

        [Test]
        public void FaceDownTick_FaceUp_DoesNotTick_IndependentOfAttackCountdown()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyNode()).Accepted);
            var uid = SpawnOrthogonalMelee(sAdjacentSlot, hp: 5, attack: 0, countdown: 5);
            var card = Registry().Get(uid);
            FaceDownTickCounters.Register(card, "flip.back.in.2", 2);

            Assert.IsTrue(mPhase.RegisterEnemyActionPhase().Accepted);
            Assert.AreEqual(2, FaceDownTickRemaining(card, "flip.back.in.2"), "正面不推进背面 Tick");
            Assert.AreEqual(4, Countdown(uid), "正面攻击倒计时仍 −1");
        }

        [Test]
        public void FaceDownTick_FireList_OnFinalTick()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyNode()).Accepted);
            var uid = SpawnOrthogonalMelee(sAdjacentSlot, hp: 5, attack: 0, countdown: 5);
            var card = Registry().Get(uid);
            card.FaceUp = false;
            FaceDownTickCounters.Register(card, "once", 1);

            var fired = new List<FaceDownTickFire>();
            FaceDownTickCounters.TickFaceDownBoard(
                mArch.GetModel<BoardModel>(),
                Registry(),
                fired);
            Assert.AreEqual(1, fired.Count);
            Assert.AreEqual(uid, fired[0].CardUid);
            Assert.AreEqual("once", fired[0].RegistrationId);
            Assert.AreEqual(0, fired[0].RemainingAfter);
        }

        private static NodeDeckOptions CreateSingleMonsterNode(int hp, int attack)
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.test", CardKind.Monster) { MaxHp = hp, Attack = attack });
        }

        private static NodeDeckOptions CreateEmptyNode()
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            };
        }

        private int SpawnOrthogonalMelee(SlotId slot, int hp, int attack, int countdown)
        {
            var draft = new CardDraft("monster.test.facedown", CardKind.Monster)
            {
                MaxHp = hp,
                Attack = attack,
                AttackPattern = AttackPattern.OrthogonalMelee,
                ActionFrequency = 3
            };
            var card = draft.Create(Registry());
            card.Counters.Set(CoreCounterKeys.AttackPatternCountdown, countdown);
            Board().PlaceCard(card, slot);
            return card.Uid;
        }

        private void PrepareAvatar(int hp, int armor, int attack)
        {
            var avatar = Registry().Get(Board().AvatarUid.Value);
            avatar.Stats.SetBase(StatId.Hp, hp);
            avatar.Stats.SetBase(StatId.MaxHp, hp);
            avatar.Stats.SetBase(StatId.Attack, attack);
            StatArmorUtility.SetCurrentArmor(avatar, armor);
        }

        private int AvatarHp()
        {
            return (int)Registry().Get(Board().AvatarUid.Value).Stats.GetBase(StatId.Hp);
        }

        private int Countdown(int uid)
        {
            return Registry().Get(uid).Counters.Get(CoreCounterKeys.AttackPatternCountdown);
        }

        private static int FaceDownTickRemaining(CardInstance card, string registrationId)
        {
            Assert.IsTrue(FaceDownTickCounters.TryGetRemaining(card, registrationId, out var remaining));
            return remaining;
        }

        private CardRegistry Registry()
        {
            return mArch.GetModel<CardRegistry>();
        }

        private BoardModel Board()
        {
            return mArch.GetModel<BoardModel>();
        }

        private void PlaceSoleBoardCardAt(SlotId targetSlot)
        {
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
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

                Assert.IsNull(sole, "Expected at most one non-avatar board card for relocate helper.");
                sole = registry.Get(uid);
            }

            Assert.IsNotNull(sole, "No board card to relocate.");
            if (sole.Slot.Value == targetSlot)
            {
                return;
            }

            board.ClearSlot(sole.Slot.Value);
            board.PlaceCard(sole, targetSlot);
        }

        private CardInstance GetSoleBoardCard()
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
                if (uid != 0)
                {
                    return registry.Get(uid);
                }
            }

            Assert.Fail("No board card");
            return null;
        }

        private CoreGameEvent FindLast(int startIndex, CoreEventType type)
        {
            var entries = mPipeline.EventLog.Entries;
            CoreGameEvent found = null;
            for (var i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].Type == type)
                {
                    found = entries[i];
                }
            }

            return found;
        }
    }
}
