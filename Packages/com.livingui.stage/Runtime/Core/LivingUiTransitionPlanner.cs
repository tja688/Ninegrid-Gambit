using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.LivingUI
{
    public readonly struct LivingUiMotionProgram
    {
        public LivingUiMotionProgram(
            int carrierId,
            LivingUiCarrierState source,
            LivingUiTerminal target,
            float startOffset,
            float duration,
            bool expelled,
            QuinticMotion positionX,
            QuinticMotion positionY,
            BoundedScalarMotion width,
            BoundedScalarMotion height)
        {
            CarrierId = carrierId;
            Source = source;
            Target = target;
            StartOffset = startOffset;
            Duration = duration;
            IsExpelled = expelled;
            PositionX = positionX;
            PositionY = positionY;
            Width = width;
            Height = height;
        }

        public int CarrierId { get; }
        public LivingUiCarrierState Source { get; }
        public LivingUiTerminal Target { get; }
        public float StartOffset { get; }
        public float Duration { get; }
        public bool IsExpelled { get; }
        public QuinticMotion PositionX { get; }
        public QuinticMotion PositionY { get; }
        public BoundedScalarMotion Width { get; }
        public BoundedScalarMotion Height { get; }
        public float EndTime => StartOffset + Duration;

        public LivingUiCarrierState Sample(float planTime)
        {
            if (planTime <= StartOffset)
            {
                return Source;
            }

            var localTime = Mathf.Min(planTime - StartOffset, Duration);
            var position = new Vector2(PositionX.EvaluatePosition(localTime), PositionY.EvaluatePosition(localTime));
            var velocity = new Vector2(PositionX.EvaluateVelocity(localTime), PositionY.EvaluateVelocity(localTime));
            var size = IsExpelled
                ? Source.Size
                : new Vector2(Width.EvaluatePosition(localTime), Height.EvaluatePosition(localTime));
            var sizeVelocity = IsExpelled
                ? Vector2.zero
                : new Vector2(Width.EvaluateVelocity(localTime), Height.EvaluateVelocity(localTime));
            return new LivingUiCarrierState(CarrierId, position, velocity, size, sizeVelocity);
        }
    }

    public sealed class LivingUiTransitionPlan
    {
        private readonly IReadOnlyDictionary<int, LivingUiMotionProgram> _programs;

        public LivingUiTransitionPlan(
            int generation,
            LivingUiLayoutId targetLayout,
            IReadOnlyDictionary<int, LivingUiMotionProgram> programs,
            float makespan)
        {
            Generation = generation;
            TargetLayout = targetLayout;
            _programs = programs;
            Makespan = makespan;
        }

        public int Generation { get; }
        public LivingUiLayoutId TargetLayout { get; }
        public IReadOnlyDictionary<int, LivingUiMotionProgram> Programs => _programs;
        public float Makespan { get; }
        public LivingUiMotionProgram GetProgram(int carrierId) => _programs[carrierId];
    }

    public sealed class TransitionPlanner : ILivingUiTransitionPlanner
    {
        private int _generation;

        public LivingUiTransitionPlan Plan(
            IReadOnlyList<LivingUiCarrierState> liveStates,
            LivingUiLayout target,
            Rect stageBounds,
            LivingUiTransitionStyle style,
            IReadOnlyDictionary<int, Vector2> carrierFlowFloorOverrides = null)
        {
            if (liveStates == null) throw new ArgumentNullException(nameof(liveStates));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (style == null) throw new ArgumentNullException(nameof(style));

            var generation = ++_generation;
            var programs = new Dictionary<int, LivingUiMotionProgram>(liveStates.Count);
            var makespan = 0f;

            var easing = LivingUiEasing.GetEasing(style.EasingType, style.CustomEasingCurve);
            var easingDerivative = LivingUiEasing.GetDerivative(style.EasingType, style.CustomEasingCurve);
            var styleFloor = new Vector2(style.FlowWidthFloor, style.FlowHeightFloor);

            for (var index = 0; index < liveStates.Count; index++)
            {
                var source = liveStates[index];
                var terminal = target.GetTerminal(source.CarrierId);
                var targetRect = RectFromCenter(terminal.Position, terminal.Size);
                var sourceRect = RectFromCenter(source.Position, source.Size);
                var expelled = !targetRect.Overlaps(stageBounds);
                var entering = !sourceRect.Overlaps(stageBounds) && !expelled;
                var distance = Vector2.Distance(source.Position, terminal.Position);
                var duration = style.BaseDuration
                    + Mathf.Min(style.MaximumDistanceAddition, distance * style.DistanceSecondsPerUnit);
                var startOffset = ResolveStartOffset(source, terminal, stageBounds, style, expelled, entering);
                var targetSize = expelled ? source.Size : terminal.Size;
                var widthMaximum = Mathf.Max(source.Size.x, targetSize.x) * style.SizeCeilingMultiplier;
                var heightMaximum = Mathf.Max(source.Size.y, targetSize.y) * style.SizeCeilingMultiplier;
                var carrierFloor = LivingUiContentProjector.ResolveCarrierFlowFloor(
                    styleFloor, carrierFlowFloorOverrides, source.CarrierId);
                var width = BoundedScalarMotion.Create(
                    source.Size.x,
                    expelled ? 0f : source.SizeVelocity.x,
                    targetSize.x,
                    duration,
                    Mathf.Min(carrierFloor.x, Mathf.Min(source.Size.x, targetSize.x)),
                    widthMaximum,
                    easing,
                    easingDerivative);
                var height = BoundedScalarMotion.Create(
                    source.Size.y,
                    expelled ? 0f : source.SizeVelocity.y,
                    targetSize.y,
                    duration,
                    Mathf.Min(carrierFloor.y, Mathf.Min(source.Size.y, targetSize.y)),
                    heightMaximum,
                    easing,
                    easingDerivative);
                var program = new LivingUiMotionProgram(
                    source.CarrierId,
                    source,
                    terminal,
                    startOffset,
                    duration,
                    expelled,
                    QuinticMotion.Create(source.Position.x, source.Velocity.x, terminal.Position.x, duration, easing, easingDerivative),
                    QuinticMotion.Create(source.Position.y, source.Velocity.y, terminal.Position.y, duration, easing, easingDerivative),
                    width,
                    height);
                programs.Add(source.CarrierId, program);
                makespan = Mathf.Max(makespan, program.EndTime);
            }

            return new LivingUiTransitionPlan(generation, target.Id, programs, makespan);
        }

        private static float ResolveStartOffset(
            LivingUiCarrierState source,
            LivingUiTerminal target,
            Rect stageBounds,
            LivingUiTransitionStyle style,
            bool expelled,
            bool entering)
        {
            if (source.Velocity.sqrMagnitude > 0.0001f || source.SizeVelocity.sqrMagnitude > 0.0001f)
            {
                return 0f;
            }

            var normalizedX = Mathf.InverseLerp(stageBounds.xMin, stageBounds.xMax, target.Position.x);
            var spatialCanon = normalizedX * style.CanonSpan;
            if (expelled) return spatialCanon;
            if (entering) return style.ExpelledLead + style.EnteringDelay + spatialCanon;
            return style.ExpelledLead + spatialCanon;
        }

        private static Rect RectFromCenter(Vector2 center, Vector2 size)
        {
            return new Rect(center - size * 0.5f, size);
        }
    }
}
