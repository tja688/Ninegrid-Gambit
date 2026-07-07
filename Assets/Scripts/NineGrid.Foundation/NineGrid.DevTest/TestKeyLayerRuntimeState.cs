#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.DevTest
{
    /// <summary>
    /// 运行时层状态：保存层元数据与实际回调表。
    /// </summary>
    public sealed class TestKeyLayerRuntimeState
    {
        public TestKeyLayerRuntimeState(string layerId, string displayName, TestKeyLayerProfileSO profile)
        {
            LayerId = layerId ?? throw new ArgumentNullException(nameof(layerId));
            Profile = profile;
            DisplayName = ResolveDisplayName(layerId, displayName, profile);
            Bindings = new Dictionary<KeyCode, TestKeyBinding>();
        }

        public string LayerId { get; }

        public string DisplayName { get; }

        public TestKeyLayerProfileSO Profile { get; }

        public IReadOnlyDictionary<KeyCode, TestKeyBinding> Bindings { get; private set; }

        public TestKeyLayerRuntimeState WithBindings(IReadOnlyDictionary<KeyCode, TestKeyBinding> bindings)
        {
            Bindings = CopyBindings(bindings);
            return this;
        }

        public TestKeyLayerRuntimeState WithDisplayName(string displayName, TestKeyLayerProfileSO profile)
        {
            return new TestKeyLayerRuntimeState(LayerId, displayName, profile ?? Profile)
                .WithBindings(Bindings);
        }

        private static string ResolveDisplayName(string layerId, string displayName, TestKeyLayerProfileSO profile)
        {
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }

            if (profile != null && !string.IsNullOrWhiteSpace(profile.DisplayName))
            {
                return profile.DisplayName;
            }

            return layerId;
        }

        private static Dictionary<KeyCode, TestKeyBinding> CopyBindings(IReadOnlyDictionary<KeyCode, TestKeyBinding> bindings)
        {
            var copy = new Dictionary<KeyCode, TestKeyBinding>(bindings.Count);
            foreach (var pair in bindings)
            {
                copy[pair.Key] = pair.Value;
            }

            return copy;
        }
    }
}

#endif
