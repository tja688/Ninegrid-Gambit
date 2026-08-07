using System;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Flow.Diagnostics;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;

namespace NineGrid.Presentation.Setup
{
    public sealed class PresentationCompositionRoot
    {
        private IPresentationRuntimeSystem mRuntime;
        private PresentationSceneBindings mBindings;
        private BattleBeatScheduler mBeatScheduler;
        private IUnRegister mBatchOpenedUnRegister;

        /// <summary>测试夹具：直接注入剧本工厂。</summary>
        public IPresentationRuntimeSystem Install(
            IIntentScriptFactory scriptFactory,
            IUiPickPreviewSink uiPickPreview = null,
            ITimelineDiagnosticSink timelineDiagnostics = null,
            IBufferedIntentLegality bufferedIntentLegality = null)
        {
            return InstallCore(
                scriptFactory, uiPickPreview, timelineDiagnostics, bindings: null, bufferedIntentLegality);
        }

        /// <summary>生产入口：由场景绑定构造 Channel / Factory / Director。</summary>
        public IPresentationRuntimeSystem Install(PresentationSceneBindings bindings)
        {
            if (bindings == null)
            {
                throw new ArgumentNullException("bindings");
            }

            var architecture = NineGridArchitecture.Interface;
            var session = BattleSessionSystem.EnsureRegistered(architecture);
            if (bindings.InBattle != null)
            {
                session.Bind(bindings.InBattle);
            }

            var dispatcher = new CoreCommandDispatcher(architecture);

            var explorePresentChannel = new QueuedBoardPresentChannel(
                session.DrainPostKillBoardAsync,
                session.EnsurePresentationToken);
            var attackBoardPresentChannel = new QueuedBoardPresentChannel(
                session.DrainPostKillBoardAsync,
                session.EnsurePresentationToken);

            var battlePresentation = architecture.GetSystem<IFieldBattlePresentationSystem>();
            var attackHitPresentChannel = new CombatAttackPresentChannel(
                async (slot, uid, result, token) =>
                {
                    try
                    {
                        if (battlePresentation != null)
                        {
                            await battlePresentation.PlayDirectorAttackHitPresentAsync(
                                slot, uid, result, token);
                        }
                    }
                    finally
                    {
                        session.EnsureBattleEndedIfAvatarDefeated(result, token);
                    }
                },
                session.EnsurePresentationToken);
            var attackCounterPresentChannel = new CombatCounterPresentChannel(
                async (slot, uid, result, token) =>
                {
                    try
                    {
                        if (battlePresentation != null)
                        {
                            await battlePresentation.PlayDirectorCounterPresentAsync(
                                slot, uid, result, token);
                        }
                    }
                    finally
                    {
                        session.EnsureBattleEndedIfAvatarDefeated(result, token);
                    }
                },
                session.EnsurePresentationToken);
            var useItemBoardPresentChannel = new QueuedBoardPresentChannel(
                session.DrainPostKillBoardAsync,
                session.EnsurePresentationToken);
            var useItemPresentChannel = new UseItemPresentChannel(
                session.PlayDirectorUseItemPresentAsync,
                session.EnsurePresentationToken);

            session.BindPresentChannels(
                explorePresentChannel,
                attackHitPresentChannel,
                attackCounterPresentChannel,
                attackBoardPresentChannel,
                useItemPresentChannel,
                useItemBoardPresentChannel);

            var exploreFactory = new ExploreIntentScriptFactory(
                architecture,
                dispatcher,
                explorePresentChannel,
                session.OnExploreBatchProjected,
                counterPresentChannel: attackCounterPresentChannel,
                onCounterBatchProjected: session.OnAttackCounterBatchProjected);
            var revealFaceFactory = new RevealFaceIntentScriptFactory(
                architecture,
                dispatcher,
                explorePresentChannel,
                session.OnExploreBatchProjected,
                counterPresentChannel: attackCounterPresentChannel,
                onCounterBatchProjected: session.OnAttackCounterBatchProjected);
            var attackFactory = new AttackIntentScriptFactory(
                architecture,
                dispatcher,
                attackHitPresentChannel,
                attackBoardPresentChannel,
                attackCounterPresentChannel,
                session.OnAttackHitBatchProjected,
                session.OnAttackBoardBatchProjected,
                session.OnAttackCounterBatchProjected);
            var useItemFactory = new UseItemIntentScriptFactory(
                architecture,
                dispatcher,
                useItemPresentChannel,
                useItemBoardPresentChannel,
                session.OnUseItemBatchProjected,
                session.OnUseItemBoardBatchProjected,
                session.OnUseItemResolvedWithoutKill);

            TriggerPulseOutputHook.RequestConfigureProduction();

            var scriptFactory = new RoutingIntentScriptFactory(
                exploreFactory,
                revealFaceFactory,
                attackFactory,
                useItemFactory,
                new NonCombatUseItemIntentScriptFactory(architecture),
                new PickupIntentScriptFactory(architecture),
                new RecycleItemIntentScriptFactory(architecture),
                new BoardWalkIntentScriptFactory(architecture));

            return InstallCore(
                scriptFactory,
                uiPickPreview: null,
                timelineDiagnostics: DirectorTrace.TimelineSink,
                bindings,
                new BoardBufferedIntentLegality(architecture));
        }

        public void Shutdown(IntentClearReason reason)
        {
            if (mRuntime == null)
            {
                return;
            }

            TeardownBattleBeatScheduler();

            if (mBindings != null)
            {
                TriggerPulseOutputHook.RequestResetFx();
                var session = NineGridArchitecture.Interface?.GetSystem<IBattleSessionSystem>();
                session?.ClearPresentChannels();
            }

            mRuntime.Stop(reason);
            mRuntime = null;
            mBindings = null;
        }

        private IPresentationRuntimeSystem InstallCore(
            IIntentScriptFactory scriptFactory,
            IUiPickPreviewSink uiPickPreview,
            ITimelineDiagnosticSink timelineDiagnostics,
            PresentationSceneBindings bindings,
            IBufferedIntentLegality bufferedIntentLegality)
        {
            if (mRuntime != null)
            {
                throw new InvalidOperationException("Presentation composition root is already installed.");
            }

            var architecture = NineGridArchitecture.Interface;
            PresentationInputStateSystem.EnsureRegistered(architecture);
            IntentIntakeSystem.EnsureRegistered(architecture);
            BattleSessionSystem.EnsureRegistered(architecture);
            ChoicePresentationSystem.EnsureRegistered(architecture);
            BoardSelectionSystem.EnsureRegistered(architecture);
            GameFlowShellSystem.EnsureRegistered(architecture);
            AvatarWalkSystem.EnsureRegistered(architecture);
            InstallBattleBeatScheduler(architecture);

            var existing = architecture.GetSystem<IPresentationRuntimeSystem>();
            if (existing != null && existing.IsStarted)
            {
                // 冲突时不接管既有运行时：保持 mRuntime 为 null，避免丢弃一个
                // 并非本组合根创建、仍在运行的 System 引用。
                throw new InvalidOperationException("Presentation runtime is already registered and started.");
            }

            if (existing == null)
            {
                existing = new PresentationRuntimeSystem();
                architecture.RegisterSystem(existing);
            }

            existing.Start(scriptFactory, uiPickPreview, timelineDiagnostics, bufferedIntentLegality);
            mRuntime = existing;
            mBindings = bindings;
            return mRuntime;
        }

        private void InstallBattleBeatScheduler(IArchitecture architecture)
        {
            TeardownBattleBeatScheduler();
            // 装饰处理器（飘字 / FX / 金币 / Avatar HUD）不占主线 ack；OfferReward 由 CardFaceStatHandler 消费。
            // PlayerInfoHud 对 Avatar 血甲只旁路写 HUD 并 return false，留给 CardFace 认领。
            // DamageFloater 须在 CardFaceStat 之前：Healed 的 UpdateHp 由其旁路飘绿字（return false），
            // 否则 UpdateHp 已被 CardFaceStat 认领，治疗飘字将永远收不到指令。
            mBeatScheduler = new BattleBeatScheduler(
                new PlayerInfoHudBeatHandler(),
                new DamageFloaterBeatHandler(),
                new CardFaceStatHandler(),
                new CardFaceFlipBeatHandler(),
                new EffectTriggerPulseBeatHandler(),
                new GoldGainBeatHandler());
            FlipPlaybackCoordinator.Reset();
            BattleBeatHook.OnBatchOpened = mBeatScheduler.OnBatchOpened;
            BattleBeatHook.ReportBeat = mBeatScheduler.ReportBeat;
            BattleBeatHook.PresentStandalone = mBeatScheduler.PresentStandalone;
            BattleBeatHook.FlushUpdateFaceUp = mBeatScheduler.FlushUpdateFaceUp;
            BattleBeatHook.FlushImpactExcept = mBeatScheduler.FlushImpactExcept;
            if (architecture != null)
            {
                mBatchOpenedUnRegister = architecture.RegisterEvent<Evt_PresentationBatchOpened>(e =>
                {
                    if (e != null)
                    {
                        BattleBeatHook.NotifyBatchOpened(e.Batch);
                    }
                });
            }
        }

        private void TeardownBattleBeatScheduler()
        {
            mBatchOpenedUnRegister?.UnRegister();
            mBatchOpenedUnRegister = null;
            BattleBeatHook.Reset();
            FlipPlaybackCoordinator.Reset();
            mBeatScheduler = null;
        }
    }
}
