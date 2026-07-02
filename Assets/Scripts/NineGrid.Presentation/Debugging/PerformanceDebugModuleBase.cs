using System;
using NineGrid.Presentation.Contracts;
using UnityEngine;

namespace NineGrid.Presentation.Debugging
{
    public abstract class PerformanceDebugModuleBase<TComponent> : IPerformanceDebugModule
        where TComponent : Component
    {
        public abstract string Id { get; }
        public abstract string DisplayName { get; }
        public abstract PerformanceDebugCategory Category { get; }
        public Type RequiredComponentType => typeof(TComponent);
        public abstract PerformanceDebugSchema Schema { get; }

        public PerformanceDebugPlayResult Play(PerformanceDebugContext context, PerformanceDebugPayload payload)
        {
            TComponent module = context?.GetModule<TComponent>();
            if (module == null)
            {
                return PerformanceDebugPlayResult.Fail($"Missing component {typeof(TComponent).Name}");
            }

            try
            {
                return PlayModule(context, module, payload);
            }
            catch (Exception ex)
            {
                context?.Log?.Error($"{DisplayName}: {ex.Message}");
                return PerformanceDebugPlayResult.Fail(ex.Message);
            }
        }

        public void Stop(PerformanceDebugContext context)
        {
            TComponent module = context?.GetModule<TComponent>();
            if (module == null)
            {
                return;
            }

            StopModule(context, module);
        }

        public void Reset(PerformanceDebugContext context)
        {
            Stop(context);
            ResetModule(context, context?.GetModule<TComponent>());
        }

        public bool TryGetIsPlaying(PerformanceDebugContext context, out bool isPlaying)
        {
            isPlaying = false;
            TComponent module = context?.GetModule<TComponent>();
            if (module == null)
            {
                return false;
            }

            switch (module)
            {
                case IDirectedFlow flow:
                    isPlaying = flow.IsPlaying;
                    return true;
            }

            return TryGetCustomIsPlaying(module, out isPlaying);
        }

        public float TryGetExpectedDuration(PerformanceDebugContext context)
        {
            TComponent module = context?.GetModule<TComponent>();
            if (module == null)
            {
                return 0f;
            }

            switch (module)
            {
                case IDirectedFlow flow:
                    return flow.ExpectedDuration;
            }

            return TryGetCustomExpectedDuration(module);
        }

        protected abstract PerformanceDebugPlayResult PlayModule(
            PerformanceDebugContext context,
            TComponent module,
            PerformanceDebugPayload payload);

        protected virtual void StopModule(PerformanceDebugContext context, TComponent module)
        {
            switch (module)
            {
                case IDirectedFlow flow:
                    flow.StopAndRestore();
                    break;
                case ILocalCue cue:
                    cue.StopAndRestore();
                    break;
                case IInteractivePresenter presenter:
                    presenter.ForceReset();
                    break;
            }
        }

        protected virtual void ResetModule(PerformanceDebugContext context, TComponent module)
        {
        }

        protected virtual bool TryGetCustomIsPlaying(TComponent module, out bool isPlaying)
        {
            isPlaying = false;
            return false;
        }

        protected virtual float TryGetCustomExpectedDuration(TComponent module)
        {
            return 0f;
        }

        protected static PerformanceDebugSchema CreateSchema()
        {
            return new PerformanceDebugSchema();
        }

        protected static string DefaultContextField()
        {
            return PerformanceDebugContextPreset.BattlePair.ToString();
        }
    }
}
