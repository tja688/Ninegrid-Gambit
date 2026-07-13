using System;
using NineGrid.Core.Systems;

namespace NineGrid.Core.Effects
{
    /// <summary>
    /// 无头骷髅 / 骷髅头重组：同一 TriggerContext 批次内，相邻对只触发一次合并。
    /// </summary>
    internal static class FragmentRecombineDedup
    {
        private const string RecombineHeadEffectId = "skill.recombine_head.move";
        private const string RecombineBodyEffectId = "skill.recombine_body.move";
        private const string SkullHeadDefId = "monster.skull_head";
        private const string HeadlessDefId = "monster.headless_skeleton";

        public static bool Passes(EffectInstance instance, EffectRuntimeContext runtime)
        {
            if (instance?.Definition == null || runtime?.TriggerContext == null)
            {
                return true;
            }

            var effectId = instance.Definition.Id;
            string partnerDefId;
            if (string.Equals(effectId, RecombineHeadEffectId, StringComparison.Ordinal))
            {
                partnerDefId = SkullHeadDefId;
            }
            else if (string.Equals(effectId, RecombineBodyEffectId, StringComparison.Ordinal))
            {
                partnerDefId = HeadlessDefId;
            }
            else
            {
                return true;
            }

            var ownerUid = runtime.OwnerUid;
            if (ownerUid == 0)
            {
                return false;
            }

            var partnerUid = AdjacentHasCardEffectCondition.ResolveAdjacentCardUid(runtime, "Self", partnerDefId);
            if (partnerUid == 0)
            {
                return false;
            }

            return runtime.TriggerContext.TryReserveFragmentMerge(ownerUid, partnerUid);
        }
    }
}
