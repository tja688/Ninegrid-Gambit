using System;
using System.Collections.Generic;
using NineGrid.Presentation.Contracts;
using NineGrid.Presentation.Debugging;
using NineGrid.Presentation.Shared;

namespace NineGrid.Presentation.Debugging.Modules
{
    public sealed class BatchFixtureDebugModule : IPerformanceDebugModule
    {
        private readonly BatchFixtureEntry fixture;

        public BatchFixtureDebugModule(BatchFixtureEntry fixture)
        {
            this.fixture = fixture;
            Schema = BuildSchema(fixture);
        }

        public string Id => fixture.Id;
        public string DisplayName => fixture.DisplayName;
        public PerformanceDebugCategory Category => PerformanceDebugCategory.Batch;
        public Type RequiredComponentType => null;
        public PerformanceDebugSchema Schema { get; }

        public PerformanceDebugPlayResult Play(PerformanceDebugContext context, PerformanceDebugPayload payload)
        {
            if (context?.Harness?.BatchRunner == null)
            {
                return PerformanceDebugPlayResult.Fail("Batch runner not ready.");
            }

            context.Harness.BatchRunner.Play(fixture, payload);
            return PerformanceDebugPlayResult.Ok(0f);
        }

        public void Stop(PerformanceDebugContext context)
        {
            context?.Harness?.BatchRunner?.Stop();
        }

        public void Reset(PerformanceDebugContext context)
        {
            Stop(context);
        }

        public bool TryGetIsPlaying(PerformanceDebugContext context, out bool isPlaying)
        {
            isPlaying = context?.Harness?.BatchRunner?.IsPlaying ?? false;
            return context?.Harness?.BatchRunner != null;
        }

        public float TryGetExpectedDuration(PerformanceDebugContext context)
        {
            return 0f;
        }

        private static PerformanceDebugSchema BuildSchema(BatchFixtureEntry fixture)
        {
            if (fixture.Id == PresentationBatchFixtureLibrary.HelpCardChain.Id)
            {
                return new PerformanceDebugSchema()
                    .Add(PerformanceDebugPayloadKeys.ContextPreset, "Context", PerformanceDebugParamKind.ContextPreset,
                        fixture.Preset.ToString(),
                        PerformanceDebugSchemaFactory.EnumNames<PerformanceDebugContextPreset>())
                    .Add(PerformanceDebugPayloadKeys.ActorUid, "Card Uid", PerformanceDebugParamKind.Int, "1")
                    .Add(PerformanceDebugPayloadKeys.EffectId, "Effect Id", PerformanceDebugParamKind.String, "relic.chain");
            }

            if (fixture.Id == PresentationBatchFixtureLibrary.OpeningDeal.Id)
            {
                return new PerformanceDebugSchema()
                    .Add(PerformanceDebugPayloadKeys.ContextPreset, "Context", PerformanceDebugParamKind.ContextPreset,
                        fixture.Preset.ToString(),
                        PerformanceDebugSchemaFactory.EnumNames<PerformanceDebugContextPreset>())
                    .Add(PerformanceDebugPayloadKeys.ToSlot, "First Deal Slot", PerformanceDebugParamKind.Enum, "2",
                        PerformanceDebugSchemaFactory.BoardSlotOptions);
            }

            if (fixture.Id == PresentationBatchFixtureLibrary.AttackKillRotateFill.Id)
            {
                return AppendMoveAndRotateFields(PerformanceDebugSchemaFactory.BattleEventSchema(fixture.Preset));
            }

            return PerformanceDebugSchemaFactory.BattleEventSchema(fixture.Preset);
        }

        private static PerformanceDebugSchema AppendMoveAndRotateFields(PerformanceDebugSchema baseSchema)
        {
            var schema = new PerformanceDebugSchema();
            IReadOnlyList<PerformanceDebugFieldDef> fields = baseSchema.Fields;
            for (var i = 0; i < fields.Count; i++)
            {
                PerformanceDebugFieldDef field = fields[i];
                schema.Add(field.Key, field.Label, field.Kind, field.DefaultValue, field.EnumOptions);
            }

            schema.Add(PerformanceDebugPayloadKeys.FromSlot, "Move From Slot", PerformanceDebugParamKind.Enum, "1",
                PerformanceDebugSchemaFactory.BoardSlotOptions);
            schema.Add(PerformanceDebugPayloadKeys.ToSlot, "Move To Slot", PerformanceDebugParamKind.Enum, "2",
                PerformanceDebugSchemaFactory.BoardSlotOptions);
            schema.Add(PerformanceDebugPayloadKeys.RotateAmount, "Rotate Amount", PerformanceDebugParamKind.Int, "1");
            return schema;
        }
    }
}
