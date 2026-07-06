#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.DevTest
{
    /// <summary>
    /// 模块注册历史：保存某模块声明过的全部按键，不受抢占影响。
    /// </summary>
    public sealed class TestKeyModuleData
    {
        internal TestKeyModuleData(string moduleId, string displayName, IReadOnlyDictionary<KeyCode, TestKeyBinding> bindings)
        {
            ModuleId = moduleId ?? throw new ArgumentNullException(nameof(moduleId));
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? moduleId : displayName;
            Bindings = CopyBindings(bindings);
        }

        public string ModuleId { get; }

        public string DisplayName { get; }

        public IReadOnlyDictionary<KeyCode, TestKeyBinding> Bindings { get; }

        internal TestKeyModuleData WithBindings(IReadOnlyDictionary<KeyCode, TestKeyBinding> bindings)
        {
            return new TestKeyModuleData(ModuleId, DisplayName, bindings);
        }

        private static Dictionary<KeyCode, TestKeyBinding> CopyBindings(IReadOnlyDictionary<KeyCode, TestKeyBinding> bindings)
        {
            if (bindings == null)
            {
                throw new ArgumentNullException(nameof(bindings));
            }

            var copy = new Dictionary<KeyCode, TestKeyBinding>(bindings.Count);
            foreach (var pair in bindings)
            {
                if (pair.Value.Callback == null)
                {
                    throw new ArgumentException($"Key {pair.Key} has null callback.", nameof(bindings));
                }

                copy[pair.Key] = pair.Value;
            }

            return copy;
        }
    }
}

#endif
