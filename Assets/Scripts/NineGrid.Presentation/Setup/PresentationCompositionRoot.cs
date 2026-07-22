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

        /// <summary>测试夹具：直接注入剧本工厂。</summary>
        public IPresentationRuntimeSystem Install(
            IIntentScriptFactory scriptFactory,
            IUiPickPreviewSink uiPickPreview = null,
            ITimelineDiagnosticSink timelineDiagnostics = null)
        {
            return InstallCore(scriptFactory, uiPickPreview, timelineDiagnostics, bindings: null);
        }

        /// <summary>生产入口：由场景绑定构造 Channel / Factory / Director。</summary>
        public IPresentationRuntimeSystem Install(PresentationSceneBindings bindings)
        {
            if (bindings == null)
            {
                throw new ArgumentNullException("bindings");
            }

            var architecture = NineGridArchitecture.Interface;
            var inBattle = bindings.InBattle;
            var dispatcher = new CoreCommandDispatcher(architecture);

            BoardPresentDrainHook.RequestWire(inBattle.DrainPostKillBoardForDirector);
            var explorePresentChannel = new QueuedBoardPresentChannel(
                BoardPresentDrainHook.RequestDrain,
                inBattle.EnsurePresentationTokenForDirector);
            var attackBoardPresentChannel = new QueuedBoardPresentChannel(
                BoardPresentDrainHook.RequestDrain,
                inBattle.EnsurePresentationTokenForDirector);

            var battlePresentation = architecture.GetSystem<IFieldBattlePresentationSystem>();
            var attackHitPresentChannel = new CombatAttackPresentChannel(
                (slot, uid, result, token) =>
                    battlePresentation != null
                        ? battlePresentation.PlayDirectorAttackHitPresentAsync(slot, uid, result, token)
                        : UniTask.CompletedTask,
                inBattle.EnsurePresentationTokenForDirector);
            var attackCounterPresentChannel = new CombatCounterPresentChannel(
                (slot, uid, result, token) =>
                    battlePresentation != null
                        ? battlePresentation.PlayDirectorCounterPresentAsync(slot, uid, result, token)
                        : UniTask.CompletedTask,
                inBattle.EnsurePresentationTokenForDirector);
            var useItemBoardPresentChannel = new QueuedBoardPresentChannel(
                BoardPresentDrainHook.RequestDrain,
                inBattle.EnsurePresentationTokenForDirector);
            var useItemPresentChannel = new UseItemPresentChannel(
                inBattle.PlayDirectorUseItemPresentForDirector,
                inBattle.EnsurePresentationTokenForDirector);

            inBattle.BindPresentChannels(
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
                inBattle.OnExploreBatchProjectedForDirector);
            var attackFactory = new AttackIntentScriptFactory(
                architecture,
                dispatcher,
                attackHitPresentChannel,
                attackBoardPresentChannel,
                attackCounterPresentChannel,
                inBattle.OnAttackHitBatchProjectedForDirector,
                inBattle.OnAttackBoardBatchProjectedForDirector,
                inBattle.OnAttackCounterBatchProjectedForDirector);
            var useItemFactory = new UseItemIntentScriptFactory(
                architecture,
                dispatcher,
                useItemPresentChannel,
                useItemBoardPresentChannel,
                inBattle.OnUseItemBatchProjectedForDirector,
                inBattle.OnUseItemBoardBatchProjectedForDirector,
                inBattle.OnUseItemResolvedWithoutKillForDirector);

            TriggerPulseOutputHook.RequestConfigureProduction();

            var scriptFactory = new RoutingIntentScriptFactory(
                exploreFactory,
                attackFactory,
                useItemFactory);

            return InstallCore(
                scriptFactory,
                uiPickPreview: null,
                timelineDiagnostics: DirectorTrace.TimelineSink,
                bindings);
        }

        public void Shutdown(IntentClearReason reason)
        {
            if (mRuntime == null)
            {
                return;
            }

            if (mBindings != null)
            {
                TriggerPulseOutputHook.RequestReset();
                mBindings.InBattle.ClearPresentChannels();
            }

            mRuntime.Stop(reason);
            mRuntime = null;
            mBindings = null;
        }

        private IPresentationRuntimeSystem InstallCore(
            IIntentScriptFactory scriptFactory,
            IUiPickPreviewSink uiPickPreview,
            ITimelineDiagnosticSink timelineDiagnostics,
            PresentationSceneBindings bindings)
        {
            if (mRuntime != null)
            {
                throw new InvalidOperationException("Presentation composition root is already installed.");
            }

            var architecture = NineGridArchitecture.Interface;
            PresentationInputStateSystem.EnsureRegistered(architecture);

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

            existing.Start(scriptFactory, uiPickPreview, timelineDiagnostics);
            mRuntime = existing;
            mBindings = bindings;
            return mRuntime;
        }
    }
}
