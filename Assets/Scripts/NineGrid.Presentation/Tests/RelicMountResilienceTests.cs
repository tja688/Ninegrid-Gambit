using System;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 遗物挂载韧性回归（对照 corelog-20260812-124012「选了遗物、整局哑火」事故）：
    /// 1) 管线内任意动作 Apply 异常不再炸穿命令（ADR-0047 补遗）——兄弟动作照常、遗物照常挂载；
    /// 2) 自愈重挂做到装配级——部分装配丢失单独补挂，不重复已存活装配；
    /// 3) Modifier 空挂死实例（挂载时目标缺位）被自愈检测并重挂；
    /// 4) 木甲系 MaxHp 修饰遗物挂载时回等量血（healOnAttach，与 ModifyBaseStat 局内约定对齐）；
    /// 5) 挂载审计事件（RelicEffectMountAudited）在授予路径落盘。
    /// </summary>
    public class RelicMountResilienceTests
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

        // ==================== 1. Apply 异常遏制 ====================

        [Test]
        public void ApplyFault_BeforeGrantRelic_DoesNotBlastCommand()
        {
            CreateAvatar();
            var pipeline = mArch.GetSystem<IActionPipelineSystem>();

            // 事故形态重演：同一命令的 FollowUp 链里，遗物授予之前有动作炸了。
            // 修复前：异常上抛炸穿整条命令，GrantRelic 永远不会执行（或半途中断）。
            Assert.DoesNotThrow(
                () => pipeline.Execute(new FollowUpHostAction(
                    new FaultingAction(),
                    new GrantRelicAction("relic.composite_armor"))),
                "动作 Apply 异常必须被熔断遏制，不得炸穿命令");

            Assert.IsFalse(pipeline.IsRunning, "命令必须原子收尾");
            Assert.Contains(
                "relic.composite_armor",
                (System.Collections.ICollection)mArch.GetModel<PlayerModel>().RelicDefIds,
                "兄弟动作不受牵连：遗物应照常入栏");
            Assert.IsTrue(
                HasActiveEffectInstanceFrom("relic.composite_armor"),
                "兄弟动作不受牵连：遗物效果实例应照常挂载");
            Assert.IsTrue(
                HasEventOfType(CoreEventType.PipelineFaultContained),
                "Apply 异常必须落 PipelineFaultContained 诊断事件");
        }

        [Test]
        public void ApplyFault_KeepsEventLogPaired()
        {
            CreateAvatar();
            var pipeline = mArch.GetSystem<IActionPipelineSystem>();
            pipeline.Execute(new FaultingAction());

            var started = 0;
            var finished = 0;
            var entries = pipeline.EventLog.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].Type == CoreEventType.ActionStarted)
                {
                    started++;
                }
                else if (entries[i].Type == CoreEventType.ActionFinished)
                {
                    finished++;
                }
            }

            Assert.AreEqual(started, finished, "异常遏制后 Started/Finished 必须保持配对");
        }

        // ==================== 2. 装配级自愈 ====================

        [Test]
        public void PartialAssemblyLoss_SelfHealsOnlyMissingAssembly()
        {
            var avatar = CreateAvatar();
            Run(new GrantRelicAction("relic.blood_regen"));

            var stats = mArch.GetSystem<IStatSystem>();
            var maxHpAfterGrant = stats.GetEffectiveInt(avatar, StatId.MaxHp);
            Assert.AreEqual(24, maxHpAfterGrant, "血色再生 max_hp 装配（+4）应已生效");

            // 只杀掉 battle 装配，max_hp 装配保持存活。
            DeactivateInstances("relic.blood_regen", "relic.blood_regen.battle");
            Assert.IsFalse(
                HasEffectInstance("relic.blood_regen", "relic.blood_regen.battle"),
                "坏档前提：battle 装配实例已缺失");
            Assert.IsTrue(
                HasEffectInstance("relic.blood_regen", "relic.blood_regen.max_hp"),
                "坏档前提：max_hp 装配实例仍存活");

            StartCombatNode();

            Assert.IsTrue(
                HasEffectInstance("relic.blood_regen", "relic.blood_regen.battle"),
                "StartNode 自愈应补挂缺失的 battle 装配");
            Assert.AreEqual(
                24,
                stats.GetEffectiveInt(avatar, StatId.MaxHp),
                "已存活的 max_hp 装配不得被重复挂载（上限翻倍即为重复）");
        }

        // ==================== 3. Modifier 空挂自愈 ====================

        [Test]
        public void DeadModifierMount_SelfHealsOnStartNode()
        {
            // 无 Avatar 时授予：Modifier 目标解析为空 → 实例存在但零修饰符（空挂死实例）。
            Run(new GrantRelicAction("relic.wood_armor"));
            Assert.IsTrue(
                HasActiveEffectInstanceFrom("relic.wood_armor"),
                "空挂前提：实例已创建");

            var avatar = CreateAvatar();
            var stats = mArch.GetSystem<IStatSystem>();
            Assert.AreEqual(
                20,
                stats.GetEffectiveInt(avatar, StatId.MaxHp),
                "空挂前提：死实例的修饰符没有挂到任何卡上（有效上限仍为基础值）");

            StartCombatNode();

            Assert.AreEqual(
                22,
                stats.GetEffectiveInt(avatar, StatId.MaxHp),
                "StartNode 自愈应检测空挂死实例并重挂：木甲 +2 上限生效（20 → 22）");
        }

        // ==================== 4. MaxHp 修饰遗物回血 ====================

        [Test]
        public void WoodArmor_ModifierRelic_HealsEqualAmountOnAttach()
        {
            var avatar = CreateAvatar(hp: 15);
            Run(new GrantRelicAction("relic.wood_armor"));

            var stats = mArch.GetSystem<IStatSystem>();
            Assert.AreEqual(
                22,
                stats.GetEffectiveInt(avatar, StatId.MaxHp),
                "木甲应加 2 点有效血量上限（20 → 22）");
            Assert.AreEqual(
                17,
                (int)Math.Round(avatar.Stats.GetBase(StatId.Hp)),
                "加上限须回等量当前血（15 → 17，局内统一约定）");
        }

        [Test]
        public void WoodArmor_AtFullHp_HealClampsToNewCap()
        {
            var avatar = CreateAvatar(hp: 20);
            Run(new GrantRelicAction("relic.wood_armor"));

            Assert.AreEqual(
                22,
                (int)Math.Round(avatar.Stats.GetBase(StatId.Hp)),
                "满血获得木甲：回血填满新上限（20 → 22）");
        }

        [Test]
        public void VitalityAmulet_OnActivate_AddsMaxHpAndHeals()
        {
            var avatar = CreateAvatar(hp: 20);
            Run(new GrantRelicAction("relic.vitality_amulet"));

            Assert.AreEqual(
                26,
                (int)Math.Round(avatar.Stats.GetBase(StatId.MaxHp)),
                "活力护符应加 6 点基础血量上限（20 → 26）");
            Assert.AreEqual(
                26,
                (int)Math.Round(avatar.Stats.GetBase(StatId.Hp)),
                "加上限须回等量当前血（20 → 26）");
        }

        [Test]
        public void DiscardWoodArmor_ClampsHpToNewCap()
        {
            var avatar = CreateAvatar(hp: 20);
            Run(new GrantRelicAction("relic.wood_armor"));
            Assert.AreEqual(22, (int)Math.Round(avatar.Stats.GetBase(StatId.Hp)), "前提：获得木甲后 22/22");

            Run(new DiscardRelicAction("relic.wood_armor"));

            var stats = mArch.GetSystem<IStatSystem>();
            Assert.AreEqual(
                20,
                stats.GetEffectiveInt(avatar, StatId.MaxHp),
                "丢弃木甲后有效上限回落（22 → 20）");
            Assert.AreEqual(
                20,
                (int)Math.Round(avatar.Stats.GetBase(StatId.Hp)),
                "丢弃后当前血应钳到新上限（22 → 20）");
        }

        // ==================== 5. 挂载审计事件 ====================

        [Test]
        public void GrantRelic_EmitsMountAuditEvent()
        {
            CreateAvatar();
            Run(new GrantRelicAction("relic.composite_armor"));

            CoreGameEvent audit = null;
            var entries = mArch.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].Type == CoreEventType.RelicEffectMountAudited)
                {
                    audit = entries[i];
                }
            }

            Assert.IsNotNull(audit, "授予路径必须落挂载审计事件");
            Assert.AreEqual("relic.composite_armor", audit.SourceDefId, "审计事件应携带遗物 defId");
            Assert.Greater(audit.Amount, 0, "复合盔甲应至少挂载 1 个效果实例（mounted>0）");
            StringAssert.Contains("mounted=", audit.Message, "审计事件 message 应携带四数核对明细");
        }

        // ==================== 6. 条件修饰符：分母口径 + 激活态审计 ====================

        [Test]
        public void BloodViolence_HpBelowUsesEffectiveMaxHp()
        {
            var avatar = CreateAvatar(hp: 20);
            Run(new GrantRelicAction("relic.blood_violence"));

            var stats = mArch.GetSystem<IStatSystem>();
            Assert.AreEqual(
                24,
                stats.GetEffectiveInt(avatar, StatId.MaxHp),
                "血液暴力应加 4 点有效血量上限（20 → 24）");
            Assert.AreEqual(
                5,
                stats.GetEffectiveInt(avatar, StatId.Attack),
                "满血时低血攻击加成不应激活");

            // 11/24 < 50%（有效上限口径应触发）；11/20 = 55%（旧基础上限口径会漏触发——回归点）。
            avatar.Stats.SetBase(StatId.Hp, 11);
            Assert.AreEqual(
                7,
                stats.GetEffectiveInt(avatar, StatId.Attack),
                "血量低于有效上限 50% 时玩家攻击 +2（分母须含遗物自身的 MaxHp 修饰）");

            // 恰等于 50% 不触发：「低于」为严格小于。
            avatar.Stats.SetBase(StatId.Hp, 12);
            Assert.AreEqual(
                5,
                stats.GetEffectiveInt(avatar, StatId.Attack),
                "恰为 50% 不应触发（低于＝严格小于）");
        }

        [Test]
        public void ConditionalModifier_AuditsInitialStateAndFlip()
        {
            var avatar = CreateAvatar(hp: 20);
            Run(new GrantRelicAction("relic.blood_violence"));

            Assert.IsTrue(
                HasConditionalAudit(ConditionalModifierAudit.CauseInitial, active: 0),
                "授予后应落首见采样事件（active=0）");

            avatar.Stats.SetBase(StatId.Hp, 5);
            Run(new HealAction(avatar.Uid, avatar.Uid, 0));

            Assert.IsTrue(
                HasConditionalAudit(ConditionalModifierAudit.CauseFlip, active: 1),
                "低血后的下一个动作边界应落翻转采样事件（active=1）");
        }

        private bool HasConditionalAudit(string route, int active)
        {
            var entries = mArch.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Type == CoreEventType.ConditionalModifierAudited
                    && entry.Cause == route
                    && entry.ResultValue == active)
                {
                    return true;
                }
            }

            return false;
        }

        // ==================== 基建 ====================

        private CardInstance CreateAvatar(int hp = 20)
        {
            var avatar = mArch.GetModel<CardRegistry>().Create("avatar.default", CardKind.Avatar);
            avatar.Stats.SetBase(StatId.MaxHp, 20);
            avatar.Stats.SetBase(StatId.Hp, hp);
            avatar.Stats.SetBase(StatId.Attack, 5);
            avatar.Stats.SetBase(StatId.Armor, 5);
            avatar.Stats.SetBase(StatId.CurrentArmor, 5);
            mArch.GetModel<BoardModel>().SetAvatar(avatar, SlotId.Board(5));
            return avatar;
        }

        private void StartCombatNode()
        {
            var run = mArch.GetModel<RunModel>();
            run.NodeIndex.Value = 0;
            run.SetPhase(GamePhase.NodeCompleted);
            var result = mArch.GetSystem<IPhaseSystem>().StartNode(null);
            Assert.IsTrue(result.Accepted, "StartNode 应被接受: " + result.Reason);
        }

        private void Run(GameAction action)
        {
            mArch.GetSystem<IActionPipelineSystem>().Execute(action);
        }

        private bool HasActiveEffectInstanceFrom(string relicDefId)
        {
            var instances = mArch.GetSystem<IEffectSystem>().Instances;
            for (var i = 0; i < instances.Count; i++)
            {
                var owner = instances[i].Owner;
                if (owner != null && owner.SourceDefId == relicDefId)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasEffectInstance(string relicDefId, string effectDefinitionId)
        {
            var instances = mArch.GetSystem<IEffectSystem>().Instances;
            for (var i = 0; i < instances.Count; i++)
            {
                var owner = instances[i].Owner;
                if (owner != null
                    && owner.SourceDefId == relicDefId
                    && instances[i].Definition != null
                    && instances[i].Definition.Id == effectDefinitionId)
                {
                    return true;
                }
            }

            return false;
        }

        private void DeactivateInstances(string relicDefId, string effectDefinitionId)
        {
            var effects = mArch.GetSystem<IEffectSystem>();
            var instances = effects.Instances;
            for (var i = 0; i < instances.Count; i++)
            {
                var owner = instances[i].Owner;
                if (owner != null
                    && owner.SourceDefId == relicDefId
                    && instances[i].Definition != null
                    && instances[i].Definition.Id == effectDefinitionId)
                {
                    effects.Deactivate(instances[i].InstanceId);
                }
            }
        }

        private bool HasEventOfType(CoreEventType type)
        {
            var entries = mArch.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].Type == type)
                {
                    return true;
                }
            }

            return false;
        }

        // ==================== 人造动作 ====================

        /// <summary>Apply 必炸的动作：验证 ADR-0047 补遗（Apply 异常遏制）。</summary>
        private sealed class FaultingAction : GameAction
        {
            public override string ActionName { get { return "TestFaultingAction"; } }

            public override GameActionResult Apply(GameActionContext context)
            {
                throw new InvalidOperationException("test-induced apply fault");
            }
        }

        /// <summary>把给定动作作为 FollowUp 链发出的宿主动作（重演命令内因果链）。</summary>
        private sealed class FollowUpHostAction : GameAction
        {
            private readonly GameAction[] mFollowUps;

            public FollowUpHostAction(params GameAction[] followUps)
            {
                mFollowUps = followUps ?? Array.Empty<GameAction>();
            }

            public override string ActionName { get { return "TestFollowUpHost"; } }

            public override GameActionResult Apply(GameActionContext context)
            {
                var result = new GameActionResult();
                for (var i = 0; i < mFollowUps.Length; i++)
                {
                    result.AddFollowUp(mFollowUps[i]);
                }

                return result;
            }
        }
    }
}
