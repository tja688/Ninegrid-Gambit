using System.Collections.Generic;
using System.Reflection;
using NineGrid.Cards;
using NineGrid.Content.Vfx;
using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 顺劈斧测试弹道不得泄露进正式局：c4fa6ef 把 PulseFromTo 挂在 ShowDamage 上，
    /// QuickTest 检查只用来换预设，非 QuickTest 仍会命中 vfx.projectile.relic 默认金色流光。
    /// </summary>
    public sealed class QuickTestCleaveAxeProjectileLeakTests
    {
        private IArchitecture mArch;
        private GameObject mCardManagerGo;
        private GameObject mViewGo;
        private RecordingVfxSink mVfx;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Interface;
            GameFlowShellSystem.EnsureRegistered(mArch);
            QuickTestProjectileEffectState.Reset();

            mVfx = new RecordingVfxSink();
            TriggerPulseHub.Configure(
                NullTriggerPulseSink.Instance,
                NullTriggerPulseSink.Instance,
                mVfx);

            DamageNumberHook.Spawn = (_, __, ___) => { };

            mViewGo = new GameObject("TestCardView");
            mViewGo.transform.position = new Vector3(1f, 2f, 0f);
            var view = mViewGo.AddComponent<StandardCardView>();

            mCardManagerGo = new GameObject("TestCardManager");
            var manager = mCardManagerGo.AddComponent<CardManagerSingleton>();
            var dict = (Dictionary<int, ManagedCard>)typeof(CardManagerSingleton)
                .GetField("_cardsByUid", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(manager);
            dict[7] = new ManagedCard(7, "monster.demo", view);

            CardEntityLifecycleHook.ResolveCards = () => manager;
            CardEntityLifecycleHook.TryGet = manager.TryGet;
        }

        [TearDown]
        public void TearDown()
        {
            DamageNumberHook.Spawn = null;
            CardEntityLifecycleHook.Reset();
            TriggerPulseHub.ResetToNull();
            QuickTestProjectileEffectState.Reset();
            if (mViewGo != null)
            {
                Object.DestroyImmediate(mViewGo);
            }

            if (mCardManagerGo != null)
            {
                Object.DestroyImmediate(mCardManagerGo);
            }

            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void FormalPlay_CleaveAxeDamage_DoesNotPulseRelicProjectile()
        {
            var shell = GameFlowShellSystem.EnsureRegistered(mArch);
            shell.ApplyRunMode(false);
            Assert.IsFalse(shell.IsQuickTestMode);

            Assert.IsTrue(new DamageFloaterBeatHandler().TryApply(BuildCleaveDamage()));

            Assert.AreEqual(
                0,
                CountRelicProjectiles(),
                "正式局顺劈伤害不得发射 vfx.projectile.relic（含默认金色流光回退）");
        }

        [Test]
        public void TutorialMode_CleaveAxeDamage_DoesNotPulseRelicProjectile()
        {
            var shell = GameFlowShellSystem.EnsureRegistered(mArch);
            shell.ApplyRunMode(false);
            shell.ApplyTutorialMode(true);

            Assert.IsTrue(new DamageFloaterBeatHandler().TryApply(BuildCleaveDamage()));

            Assert.AreEqual(0, CountRelicProjectiles(), "教程顺劈伤害不得发射测试弹道");
        }

        [Test]
        public void QuickTest_CleaveAxeDamage_PulsesSelectedProjectilePreset()
        {
            var shell = GameFlowShellSystem.EnsureRegistered(mArch);
            shell.ApplyRunMode(true);
            Assert.IsTrue(shell.IsQuickTestMode);
            Assert.IsTrue(QuickTestProjectileEffectState.TrySetActiveIndex(3, out var presetId, out _));
            Assert.AreEqual(VfxProjectilePresetIds.VoidOrb, presetId);

            Assert.IsTrue(new DamageFloaterBeatHandler().TryApply(BuildCleaveDamage()));

            var relicPulses = CollectRelicProjectiles();
            Assert.AreEqual(1, relicPulses.Count, "QuickTest 顺劈应发射一条遗物弹道");
            Assert.AreEqual(VfxProjectilePresetIds.VoidOrb, relicPulses[0].SkillId);
            Assert.AreEqual("relic.rotten_cleave_axe", relicPulses[0].CardDefId);
        }

        [Test]
        public void TryGetActivePresetForQuickTest_ReturnsFalse_WhenNotQuickTest()
        {
            var shell = GameFlowShellSystem.EnsureRegistered(mArch);
            shell.ApplyRunMode(false);

            Assert.IsFalse(QuickTestProjectileEffectState.TryGetActivePresetForQuickTest(out var presetId));
            Assert.IsNull(presetId);
        }

        private static PresentationInstruction BuildCleaveDamage()
        {
            PresentationEventMap.TryGet(CoreEventType.DamageDealt, out var map);
            var gameEvent = new CoreGameEvent(CoreEventType.DamageDealt, 1, "DealDamage")
                .WithTarget(7)
                .WithAmount(1)
                .WithDamageSplit(0, 1)
                .WithSource("relic.rotten_cleave_axe", "relic.rotten_cleave_axe.cleave");
            return new PresentationInstruction(gameEvent, map);
        }

        private int CountRelicProjectiles()
        {
            return CollectRelicProjectiles().Count;
        }

        private List<VfxCueRequest> CollectRelicProjectiles()
        {
            var matched = new List<VfxCueRequest>();
            for (var i = 0; i < mVfx.Requests.Count; i++)
            {
                if (mVfx.Requests[i].CueId == ProjectileVfxCues.Relic)
                {
                    matched.Add(mVfx.Requests[i]);
                }
            }

            return matched;
        }

        private sealed class RecordingVfxSink : IVfxCuePulseSink
        {
            public readonly List<VfxCueRequest> Requests = new List<VfxCueRequest>();

            public VfxCueResult Pulse(VfxCueRequest request, VfxSpatialContext spatialContext = default)
            {
                Requests.Add(request);
                return new VfxCueResult
                {
                    Outcome = VfxCueOutcome.Played,
                    CueId = request.CueId,
                    PresentationPlan = VfxPresentationPlan.None,
                };
            }
        }
    }
}
