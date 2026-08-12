using System;
using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 卡面投影统一对账缝回归（ADR-0045）。
    /// 锁死的 bug 形态：改「有效攻/当前甲」的源头（遗物、光环、乘区、拓扑变更、规则卸载……）
    /// 必须逐处手工补发卡面提交事件，漏一处 → 卡面显示与实际结算对不上。
    /// 正确语义：Core 在每个动作边界自动 diff-emit 卡面绝对值提交事件（紧邻因果动作、
    /// 沿用 Settled 锚点），表现层维持只从指令赋值的哑消费者；任何新增数值源零手工接线。
    /// 核心不变量：按表现层消费语义重放事件日志得到的卡面值 == 结算同源 oracle 重算值。
    /// </summary>
    public class CardFaceReconciliationRegressionTests
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

        // ---------- 动态加攻：邻接光环随拓扑变更 ----------

        [Test]
        public void AdjacencyAura_SwapAndKill_FaceAttackTracksOracle()
        {
            var avatar = CreateAvatarOnBoard(hp: 20, attack: 2);
            var totem = CreateMonsterOnBoard("monster.test.totem", 4, hp: 5, attack: 0);
            var buddy = CreateMonsterOnBoard("monster.test.buddy", 1, hp: 5, attack: 2);
            SeedFaces(avatar, totem, buddy);

            // 邻接光环：buddy 邻接 totem 时攻 +3（条件型 Permanent，条件随拓扑翻转）。
            Run(new AddStatModifierAction(
                buddy.Uid,
                StatId.Attack,
                ModifierOp.Add,
                3f,
                ModifierLayer.Conditional,
                ModifierScope.Permanent,
                "test.aura.totem",
                "test.totem",
                new SourceAdjacentToUidCondition(totem.Uid, buddy.Uid)));

            AssertFacesMatchOracle("光环挂载后");
            Assert.AreEqual(5, FaceOf(buddy.Uid).Attack, "1 与 4 邻接：光环生效，卡面攻 2+3");

            // 换位到不相邻（1↔9 与 4 不邻接）：条件翻转，卡面必须自动回落。
            Run(new SwapBoardSlotsAction(SlotId.Board(1), SlotId.Board(9)));
            AssertFacesMatchOracle("换位到不相邻后");
            Assert.AreEqual(2, FaceOf(buddy.Uid).Attack, "9 与 4 不邻接：光环失效，卡面攻回 2");

            // 换回相邻，再击杀光环宿主：卡面必须随宿主离场回落（旧补丁地狱高发路径）。
            Run(new SwapBoardSlotsAction(SlotId.Board(9), SlotId.Board(1)));
            AssertFacesMatchOracle("换回相邻后");
            Assert.AreEqual(5, FaceOf(buddy.Uid).Attack, "换回相邻：光环再生效");

            Run(new KillAction(avatar.Uid, totem.Uid));
            AssertFacesMatchOracle("击杀光环宿主后");
            Assert.AreEqual(2, FaceOf(buddy.Uid).Attack, "宿主离场：邻接条件不成立，卡面攻回 2");
        }

        [Test]
        public void AdjacencyAura_Rotate_FaceAttackTracksOracle()
        {
            var avatar = CreateAvatarOnBoard(hp: 20, attack: 2);
            var totem = CreateMonsterOnBoard("monster.test.totem", 4, hp: 5, attack: 0);
            var buddy = CreateMonsterOnBoard("monster.test.buddy", 1, hp: 5, attack: 2);
            SeedFaces(avatar, totem, buddy);

            Run(new AddStatModifierAction(
                buddy.Uid,
                StatId.Attack,
                ModifierOp.Add,
                3f,
                ModifierLayer.Conditional,
                ModifierScope.Permanent,
                "test.aura.totem",
                "test.totem",
                new SourceAdjacentToUidCondition(totem.Uid, buddy.Uid)));
            Assert.AreEqual(5, FaceOf(buddy.Uid).Attack, "旋转前 1/4 邻接生效");

            // 顺时针一格：1→2、4→1，邻接关系保持（1、2 相邻），换个不敏感断言：口径恒等即可。
            Run(new RotateBoardClockwiseAction(true));
            AssertFacesMatchOracle("旋转后");
        }

        // ---------- EnemyAttackDelta 规则装卸（含历史泄漏路径 RemoveRuleModifiersBySource） ----------

        [Test]
        public void EnemyAttackDeltaRule_GrantAndSilentRemove_MonsterFaceMatchesSettlement()
        {
            var avatar = CreateAvatarOnBoard(hp: 20, attack: 2);
            var m1 = CreateMonsterOnBoard("monster.test.orc", 4, hp: 8, attack: 3);
            var m2 = CreateMonsterOnBoard("monster.test.slime", 6, hp: 8, attack: 1);
            SeedFaces(avatar, m1, m2);

            // 全场怪物攻 -1（龙鳞甲形态），经 AddRuleModifierAction 授予。
            Run(new AddRuleModifierAction(
                0,
                RuleId.EnemyAttackDelta,
                ModifierOp.Add,
                -1f,
                ModifierLayer.Persistent,
                ModifierScope.Permanent,
                "test.relic.scale"));

            AssertFacesMatchOracle("规则授予后");
            Assert.AreEqual(2, FaceOf(m1.Uid).Attack, "3-1：卡面与结算同口径");
            Assert.AreEqual(0, FaceOf(m2.Uid).Attack, "1-1：钳到 0");

            // 按 Source 静默卸载：旧机制下这里无任何补扫（泄漏路径），卡面会永久停在 -1。
            Run(new RemoveRuleModifiersBySourceAction("test.relic.scale"));
            AssertFacesMatchOracle("规则静默卸载后");
            Assert.AreEqual(3, FaceOf(m1.Uid).Attack, "卸载后卡面攻必须自动回 3");
            Assert.AreEqual(1, FaceOf(m2.Uid).Attack, "卸载后卡面攻必须自动回 1");
        }

        // ---------- 动态护甲：卡面甲=当前甲；基础甲 HUD 轨不串扰 ----------

        [Test]
        public void ArmorTracks_FaceUsesCurrentArmor_BaseArmorStaysOffFace()
        {
            var avatar = CreateAvatarOnBoard(hp: 20, attack: 2);
            var monster = CreateMonsterOnBoard("monster.test.turtle", 4, hp: 8, attack: 1);
            monster.Stats.SetBase(StatId.Armor, 2);
            StatArmorUtility.SetCurrentArmor(monster, 2);
            SeedFaces(avatar, monster);

            Run(new GainArmorAction(monster.Uid, 3, "test.armor", "gain"));
            AssertFacesMatchOracle("怪物加当前甲后");
            Assert.AreEqual(5, FaceOf(monster.Uid).Armor, "卡面甲=当前甲 2+3");

            // 玩家基础甲 +2：卡面甲经 ArmorChanged 同步；BaseStatModified(Armor) 只驱动 HUD、
            // 重放模拟器（与 CardFaceStatHandler 同构）不得用它写卡面。
            Run(new ModifyBaseStatAction(avatar.Uid, StatId.Armor, 2, "test.baseArmor"));
            AssertFacesMatchOracle("玩家基础甲提升后");
            Assert.AreEqual(
                StatArmorUtility.GetCurrentArmor(avatar),
                FaceOf(avatar.Uid).Armor,
                "玩家卡面甲=当前甲（基础甲变化经 ArmorChanged 耦合）");
        }

        // ---------- DamageMultiplier 一次性乘区：投影与消耗回退 ----------

        [Test]
        public void DamageMultiplierOnce_ProjectsOnFace_ThenRollsBackAfterConsumption()
        {
            var avatar = CreateAvatarOnBoard(hp: 20, attack: 3);
            var monster = CreateMonsterOnBoard("monster.test.dummy", 4, hp: 12, attack: 0);
            SeedFaces(avatar, monster);
            Assert.AreEqual(3, FaceOf(avatar.Uid).Attack, "初始卡面攻=有效攻");

            // 暴力卡形态：玩家下一次 DealDamage ×2（Once）。
            Run(new AddRuleModifierAction(
                avatar.Uid,
                false,
                avatar.Uid,
                CardKind.Monster,
                RuleId.DamageMultiplier,
                ModifierOp.Multiply,
                2f,
                ModifierLayer.Temporary,
                ModifierScope.Once,
                "test.brutality"));

            AssertFacesMatchOracle("乘区授予后");
            Assert.AreEqual(6, FaceOf(avatar.Uid).Attack, "卡面攻=下一击投影 3×2");

            var hit = mArch.GetSystem<IPhaseSystem>().ApplyCombatHit(avatar.Uid, monster.Uid);
            Assert.IsTrue(hit.Accepted, "ApplyCombatHit 应被接受");

            Assert.AreEqual(6, (int)monster.Stats.GetBase(StatId.Hp), "实际结算 12-6：与投影一致");
            AssertFacesMatchOracle("乘区消耗后");
            Assert.AreEqual(3, FaceOf(avatar.Uid).Attack, "Once 消耗后卡面攻自动回退 3");
        }

        // ---------- 行动倒计时：计数器旁改自动补投影 ----------

        [Test]
        public void ActionCountdown_SilentCounterMutation_IsReconciled()
        {
            var avatar = CreateAvatarOnBoard(hp: 20, attack: 2);
            var monster = CreateMonsterOnBoard("monster.test.rhythm", 4, hp: 8, attack: 2);
            monster.AttackPattern = AttackPattern.OrthogonalMelee;
            monster.RhythmSource = CardRhythmSource.Action;
            monster.RhythmPeriod = 3;
            monster.Counters.Set(CoreCounterKeys.AttackPatternCountdown, 3);
            SeedFaces(avatar, monster);
            Assert.AreEqual(3, FaceOf(monster.Uid).ActionCount, "发牌事件携带倒计时 3");

            // 直改计数器且不发事件（模拟提速类效果漏发）：对账缝须在下一个动作边界补投影。
            Run(new MutateCountdownSilentlyAction(monster.Uid, 1));
            Assert.AreEqual(1, FaceOf(monster.Uid).ActionCount, "旁改倒计时须被自动补投影为 1");
        }

        // ---------- 时序：提交事件紧邻因果动作、位于其事件之后 ----------

        [Test]
        public void ReconcileCommit_IsInsideCausalActionWindow_AfterItsOwnEvents()
        {
            var avatar = CreateAvatarOnBoard(hp: 20, attack: 2);
            var totem = CreateMonsterOnBoard("monster.test.totem", 4, hp: 5, attack: 0);
            var buddy = CreateMonsterOnBoard("monster.test.buddy", 1, hp: 5, attack: 2);
            SeedFaces(avatar, totem, buddy);
            Run(new AddStatModifierAction(
                buddy.Uid,
                StatId.Attack,
                ModifierOp.Add,
                3f,
                ModifierLayer.Conditional,
                ModifierScope.Permanent,
                "test.aura.totem",
                "test.totem",
                new SourceAdjacentToUidCondition(totem.Uid, buddy.Uid)));

            var before = Events().Count;
            Run(new SwapBoardSlotsAction(SlotId.Board(1), SlotId.Board(9)));

            var entries = Events();
            var commitIndex = -1;
            for (var i = before; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Type == CoreEventType.BaseStatModified
                    && e.CardUid == buddy.Uid
                    && e.Message == CardFaceReconciliation.CauseFaceReconcile)
                {
                    commitIndex = i;
                    break;
                }
            }

            Assert.GreaterOrEqual(commitIndex, 0, "换位翻转光环应产生对账提交事件");
            var actionId = entries[commitIndex].ActionId;

            var swappedIndex = -1;
            var finishedIndex = -1;
            for (var i = before; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.ActionId == actionId && e.Type == CoreEventType.CardSwapped)
                {
                    swappedIndex = i;
                }

                if (e.ActionId == actionId && e.Type == CoreEventType.ActionFinished)
                {
                    finishedIndex = i;
                }
            }

            Assert.GreaterOrEqual(swappedIndex, 0, "同 ActionId 应有 CardSwapped");
            Assert.Greater(commitIndex, swappedIndex, "提交事件必须位于因果动作自身事件之后");
            Assert.Greater(finishedIndex, commitIndex, "提交事件必须在因果动作窗口（ActionFinished 前）内");
        }

        // ---------- 不变量：随机动作风暴后全场 卡面重放值 == oracle ----------

        [Test]
        public void RandomActionStorm_ReplayedFacesAlwaysMatchOracle()
        {
            var avatar = CreateAvatarOnBoard(hp: 60, attack: 2);
            var m1 = CreateMonsterOnBoard("monster.test.a", 1, hp: 30, attack: 2);
            var m2 = CreateMonsterOnBoard("monster.test.b", 4, hp: 30, attack: 1);
            var m3 = CreateMonsterOnBoard("monster.test.c", 9, hp: 30, attack: 3);
            SeedFaces(avatar, m1, m2, m3);
            var monsters = new[] { m1, m2, m3 };

            var rng = new Random(20260812);
            var ruleSources = new List<string>();
            for (var step = 0; step < 120; step++)
            {
                var uidPool = monsters[rng.Next(monsters.Length)].Uid;
                switch (rng.Next(9))
                {
                    case 0:
                        Run(new RotateBoardClockwiseAction(rng.Next(2) == 0));
                        break;
                    case 1:
                        Run(new SwapBoardSlotsAction(
                            SlotId.Board(BoardIndexExcludingAvatar(rng)),
                            SlotId.Board(BoardIndexExcludingAvatar(rng))));
                        break;
                    case 2:
                        Run(new AddStatModifierAction(
                            rng.Next(2) == 0 ? avatar.Uid : uidPool,
                            StatId.Attack,
                            ModifierOp.Add,
                            rng.Next(-2, 4),
                            ModifierLayer.Persistent,
                            ModifierScope.Permanent,
                            "test.storm.stat." + step));
                        break;
                    case 3:
                        Run(new AddStatModifierAction(
                            uidPool,
                            StatId.Attack,
                            ModifierOp.Add,
                            rng.Next(1, 4),
                            ModifierLayer.Conditional,
                            ModifierScope.Permanent,
                            "test.storm.aura." + step,
                            null,
                            new SourceAdjacentToUidCondition(
                                monsters[rng.Next(monsters.Length)].Uid,
                                uidPool)));
                        break;
                    case 4:
                        var source = "test.storm.rule." + step;
                        ruleSources.Add(source);
                        Run(new AddRuleModifierAction(
                            0,
                            RuleId.EnemyAttackDelta,
                            ModifierOp.Add,
                            rng.Next(-2, 3),
                            ModifierLayer.Persistent,
                            ModifierScope.Permanent,
                            source));
                        break;
                    case 5:
                        if (ruleSources.Count > 0)
                        {
                            var removeAt = rng.Next(ruleSources.Count);
                            Run(new RemoveRuleModifiersBySourceAction(ruleSources[removeAt]));
                            ruleSources.RemoveAt(removeAt);
                        }

                        break;
                    case 6:
                        Run(new ModifyBaseStatAction(
                            rng.Next(2) == 0 ? avatar.Uid : uidPool,
                            rng.Next(3) == 0 ? StatId.Armor : StatId.Attack,
                            rng.Next(-1, 3),
                            "test.storm.base." + step));
                        break;
                    case 7:
                        Run(new GainArmorAction(uidPool, rng.Next(0, 3), "test.storm.armor." + step));
                        break;
                    case 8:
                        Run(new DealDamageAction(avatar.Uid, uidPool, rng.Next(0, 4)));
                        break;
                }

                AssertFacesMatchOracle("随机风暴第 " + step + " 步后");
            }
        }

        // ==================== 基建 ====================

        /// <summary>发牌路径同构的播种动作：经 AddWithFaceAbsolutes 写入生成绝对值。</summary>
        private sealed class SeedFaceAction : GameAction
        {
            private readonly int mUid;
            private readonly bool mIsAvatar;

            public SeedFaceAction(int uid, bool isAvatar)
            {
                mUid = uid;
                mIsAvatar = isAvatar;
            }

            public override string ActionName { get { return "TestSeedFace"; } }

            public override GameActionResult Apply(GameActionContext context)
            {
                var card = context.GetModel<CardRegistry>().Get(mUid);
                var eventType = mIsAvatar ? CoreEventType.AvatarAppeared : CoreEventType.CardDealt;
                return new GameActionResult().AddWithFaceAbsolutes(
                    context,
                    card,
                    new CoreGameEvent(eventType, context.ActionId, ActionName).WithCard(mUid));
            }
        }

        /// <summary>模拟「改倒计时计数器但漏发事件」的效果原子。</summary>
        private sealed class MutateCountdownSilentlyAction : GameAction
        {
            private readonly int mUid;
            private readonly int mValue;

            public MutateCountdownSilentlyAction(int uid, int value)
            {
                mUid = uid;
                mValue = value;
            }

            public override string ActionName { get { return "TestMutateCountdown"; } }

            public override GameActionResult Apply(GameActionContext context)
            {
                context.GetModel<CardRegistry>().Get(mUid)
                    .Counters.Set(CoreCounterKeys.AttackPatternCountdown, mValue);
                return GameActionResult.Empty;
            }
        }

        /// <summary>与 CardFaceStatHandler 同构的卡面重放值（表现层哑消费者视角）。</summary>
        private sealed class SimulatedFace
        {
            public int Attack;
            public int Armor;
            public int Hp;
            public int ActionCount;
        }

        private CardInstance CreateAvatarOnBoard(int hp, int attack)
        {
            var registry = mArch.GetModel<CardRegistry>();
            var avatar = registry.Create("avatar.default", CardKind.Avatar);
            avatar.Stats.SetBase(StatId.MaxHp, hp);
            avatar.Stats.SetBase(StatId.Hp, hp);
            avatar.Stats.SetBase(StatId.Attack, attack);
            mArch.GetModel<BoardModel>().SetAvatar(avatar, SlotId.Board(5));
            return avatar;
        }

        private CardInstance CreateMonsterOnBoard(string defId, int slotIndex, int hp, int attack)
        {
            var registry = mArch.GetModel<CardRegistry>();
            var monster = registry.Create(defId, CardKind.Monster);
            monster.Stats.SetBase(StatId.MaxHp, hp);
            monster.Stats.SetBase(StatId.Hp, hp);
            monster.Stats.SetBase(StatId.Attack, attack);
            mArch.GetModel<BoardModel>().PlaceCard(monster, SlotId.Board(slotIndex));
            return monster;
        }

        private void SeedFaces(CardInstance avatar, params CardInstance[] monsters)
        {
            var pipeline = mArch.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new SeedFaceAction(avatar.Uid, isAvatar: true));
            for (var i = 0; i < monsters.Length; i++)
            {
                pipeline.Enqueue(new SeedFaceAction(monsters[i].Uid, isAvatar: false));
            }

            pipeline.RunToCompletion();
        }

        private void Run(GameAction action)
        {
            mArch.GetSystem<IActionPipelineSystem>().Execute(action);
        }

        private IReadOnlyList<CoreGameEvent> Events()
        {
            return mArch.GetSystem<IActionPipelineSystem>().EventLog.Entries;
        }

        private static int BoardIndexExcludingAvatar(Random rng)
        {
            // Avatar 固定 Board(5)：随机换位避开，避免撞 BoardModel 占位断言。
            var index = rng.Next(1, 9);
            return index >= 5 ? index + 1 : index;
        }

        /// <summary>
        /// 按 CardFaceStatHandler 消费语义重放事件日志：卡面值只来自指令绝对值。
        /// 改 CardFaceStatHandler / CardFaceReconciliation 的消费语义须同步改这里。
        /// </summary>
        private Dictionary<int, SimulatedFace> ReplayFaces()
        {
            var faces = new Dictionary<int, SimulatedFace>();
            var entries = Events();
            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                var uid = e.CardUid > 0 ? e.CardUid : e.TargetUid;
                if (uid <= 0)
                {
                    continue;
                }

                switch (e.Type)
                {
                    case CoreEventType.HpChanged:
                    case CoreEventType.Healed:
                        Face(faces, uid).Hp = Math.Max(0, e.RemainingHp);
                        Face(faces, uid).Armor = Math.Max(0, e.RemainingArmor);
                        break;
                    case CoreEventType.ArmorChanged:
                        Face(faces, uid).Armor = Math.Max(0, e.RemainingArmor);
                        break;
                    case CoreEventType.CardDealt:
                    case CoreEventType.CardSpawned:
                    case CoreEventType.AvatarAppeared:
                        var spawned = Face(faces, uid);
                        spawned.Attack = Math.Max(0, e.ResultValue);
                        spawned.Armor = Math.Max(0, e.RemainingArmor);
                        spawned.Hp = Math.Max(0, e.RemainingHp);
                        break;
                    case CoreEventType.CardKilled:
                        Face(faces, uid).Hp = Math.Max(0, e.RemainingHp);
                        break;
                    case CoreEventType.ActionCountdownChanged:
                        Face(faces, uid).ActionCount = Math.Max(0, e.ResultValue);
                        break;
                    case CoreEventType.BaseStatModified:
                        switch ((StatId)e.Amount)
                        {
                            case StatId.Attack:
                                Face(faces, uid).Attack = Math.Max(0, e.ResultValue);
                                break;
                            case StatId.CurrentArmor:
                                Face(faces, uid).Armor = Math.Max(0, e.ResultValue);
                                break;
                            case StatId.Hp:
                                Face(faces, uid).Hp = Math.Max(0, e.ResultValue);
                                break;
                            case StatId.MaxHp:
                                Face(faces, uid).Hp = Math.Max(0, e.RemainingHp);
                                break;

                            // StatId.Armor：HUD 轨，不写卡面（ADR-0005）。
                        }

                        break;
                }
            }

            return faces;
        }

        private static SimulatedFace Face(Dictionary<int, SimulatedFace> faces, int uid)
        {
            SimulatedFace face;
            if (!faces.TryGetValue(uid, out face))
            {
                face = new SimulatedFace();
                faces[uid] = face;
            }

            return face;
        }

        private SimulatedFace FaceOf(int uid)
        {
            var faces = ReplayFaces();
            Assert.IsTrue(faces.ContainsKey(uid), "uid=" + uid + " 应有卡面重放值（先 SeedFaces）");
            return faces[uid];
        }

        /// <summary>
        /// 核心不变量（ADR-0045）：盘面所有实体 + Avatar 的「事件重放卡面值」==「结算同源 oracle」。
        /// </summary>
        private void AssertFacesMatchOracle(string when)
        {
            var faces = ReplayFaces();
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var stats = mArch.GetSystem<IStatSystem>();
            var context = new GameActionContext(
                mArch,
                mArch.GetSystem<IActionPipelineSystem>().EventLog,
                0,
                0);

            var avatarUid = board.AvatarUid.Value;
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var uid = board.GetCardUid(SlotId.Board(i));
                if (uid <= 0 || uid == avatarUid || !faces.ContainsKey(uid))
                {
                    continue;
                }

                CardInstance card;
                if (!registry.TryGet(uid, out card) || card == null)
                {
                    continue;
                }

                var face = faces[uid];
                Assert.AreEqual(
                    CardFaceEventValues.GetFaceAttack(stats, card),
                    face.Attack,
                    when + "：uid=" + uid + " 卡面攻必须等于结算口径有效攻");
                Assert.AreEqual(
                    Math.Max(0, StatArmorUtility.GetCurrentArmor(card)),
                    face.Armor,
                    when + "：uid=" + uid + " 卡面甲必须等于当前护甲");
                Assert.AreEqual(
                    CardFaceReconciliation.GetFaceHp(card),
                    face.Hp,
                    when + "：uid=" + uid + " 卡面血必须等于结算口径血量");
                if (CardRhythmRules.HasActiveRhythm(card))
                {
                    Assert.AreEqual(
                        Math.Max(0, card.Counters.Get(CoreCounterKeys.AttackPatternCountdown)),
                        face.ActionCount,
                        when + "：uid=" + uid + " 行动倒计时必须等于计数器");
                }
            }

            CardInstance avatar;
            if (avatarUid > 0 && registry.TryGet(avatarUid, out avatar) && faces.ContainsKey(avatarUid))
            {
                var face = faces[avatarUid];
                Assert.AreEqual(
                    CardFaceEventValues.GetProjectedPlayerBattleAttack(context, avatar),
                    face.Attack,
                    when + "：玩家卡面攻必须等于下一击投影口径");
                Assert.AreEqual(
                    Math.Max(0, StatArmorUtility.GetCurrentArmor(avatar)),
                    face.Armor,
                    when + "：玩家卡面甲必须等于当前护甲");
                Assert.AreEqual(
                    CardFaceReconciliation.GetFaceHp(avatar),
                    face.Hp,
                    when + "：玩家卡面血必须等于结算口径血量");
            }
        }
    }
}
