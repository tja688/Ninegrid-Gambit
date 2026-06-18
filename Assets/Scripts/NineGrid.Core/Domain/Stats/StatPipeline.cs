using System;
using System.Collections.Generic;

namespace NineGrid.Core.Stats
{
    public sealed class StatPipeline
    {
        private static readonly ModifierLayer[] sLayerOrder =
        {
            ModifierLayer.Persistent,
            ModifierLayer.Conditional,
            ModifierLayer.Temporary
        };

        public float Evaluate(StatBlock block, StatId stat, StatEvaluationContext context)
        {
            if (block == null)
            {
                throw new ArgumentNullException("block");
            }

            var value = block.GetBase(stat);
            var modifiers = block.Modifiers;

            for (var i = 0; i < sLayerOrder.Length; i++)
            {
                value = ApplyLayer(value, stat, sLayerOrder[i], modifiers, context);
            }

            return value;
        }

        private static float ApplyLayer(
            float value,
            StatId stat,
            ModifierLayer layer,
            IReadOnlyList<StatModifier> modifiers,
            StatEvaluationContext context)
        {
            for (var i = 0; i < modifiers.Count; i++)
            {
                var modifier = modifiers[i];
                if (modifier.Stat != stat || modifier.Layer != layer || !modifier.IsActive(context))
                {
                    continue;
                }

                value = ApplyOp(value, modifier.Op, modifier.Value);
            }

            return value;
        }

        public static float ApplyOp(float current, ModifierOp op, float operand)
        {
            switch (op)
            {
                case ModifierOp.Add:
                    return current + operand;
                case ModifierOp.Multiply:
                    return current * operand;
                case ModifierOp.Override:
                    return operand;
                default:
                    throw new ArgumentOutOfRangeException("op", op, "Unsupported modifier op.");
            }
        }
    }
}
