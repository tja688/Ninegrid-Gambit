#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.DevTest
{
    /// <summary>
    /// 开发测试按键管理器：维护全局生效表与模块注册历史，支持抢占与手动激活恢复。
    /// </summary>
    public sealed class TestKeyManager
    {
        private static TestKeyManager _instance;

        private readonly Dictionary<KeyCode, ActiveTestKeyBinding> _activeBindings = new();
        private readonly Dictionary<string, TestKeyModuleData> _modules = new(StringComparer.Ordinal);
        private readonly List<string> _moduleOrder = new();

        public static TestKeyManager Instance => _instance ??= new TestKeyManager();

        public event Action Changed;

        public IReadOnlyDictionary<KeyCode, ActiveTestKeyBinding> ActiveBindings => _activeBindings;

        public IReadOnlyDictionary<string, TestKeyModuleData> Modules => _modules;

        public IReadOnlyList<string> ModuleOrder => _moduleOrder;

        /// <summary>
        /// 注册或更新模块按键。新注册的键会直接写入全局表，抢占其他模块的冲突键。
        /// </summary>
        public void Register(string moduleId, IReadOnlyDictionary<KeyCode, TestKeyBinding> bindings, string displayName = null)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
            {
                throw new ArgumentException("Module id is required.", nameof(moduleId));
            }

            if (bindings == null || bindings.Count == 0)
            {
                throw new ArgumentException("At least one binding is required.", nameof(bindings));
            }

            var moduleData = new TestKeyModuleData(moduleId, displayName, bindings);
            var isNewModule = !_modules.ContainsKey(moduleId);
            _modules[moduleId] = moduleData;

            if (isNewModule)
            {
                _moduleOrder.Add(moduleId);
            }

            ApplyModuleBindingsToGlobal(moduleId, moduleData.Bindings);
            RaiseChanged();
        }

        /// <summary>
        /// 手动激活指定模块：将其历史按键重新写入全局表，覆盖冲突键。
        /// 未声明的键保持当前拥有者不变。
        /// </summary>
        public bool ActivateModule(string moduleId)
        {
            if (!_modules.TryGetValue(moduleId, out var moduleData))
            {
                return false;
            }

            ApplyModuleBindingsToGlobal(moduleId, moduleData.Bindings);
            RaiseChanged();
            return true;
        }

        /// <summary>
        /// 注销模块并按注册顺序重建全局表（同键后注册者优先）。
        /// </summary>
        public bool UnregisterModule(string moduleId)
        {
            if (!_modules.Remove(moduleId))
            {
                return false;
            }

            _moduleOrder.Remove(moduleId);
            RebuildActiveBindings();
            RaiseChanged();
            return true;
        }

        /// <summary>
        /// 查询某键当前由哪个模块拥有。
        /// </summary>
        public bool TryGetOwner(KeyCode key, out string moduleId)
        {
            if (_activeBindings.TryGetValue(key, out var binding))
            {
                moduleId = binding.ModuleId;
                return true;
            }

            moduleId = null;
            return false;
        }

        /// <summary>
        /// 查询模块声明过但当前未生效的按键。
        /// </summary>
        public IReadOnlyList<KeyCode> GetInactiveKeys(string moduleId)
        {
            if (!_modules.TryGetValue(moduleId, out var moduleData))
            {
                return Array.Empty<KeyCode>();
            }

            var inactive = new List<KeyCode>();
            foreach (var key in moduleData.Bindings.Keys)
            {
                if (!_activeBindings.TryGetValue(key, out var active) || active.ModuleId != moduleId)
                {
                    inactive.Add(key);
                }
            }

            return inactive;
        }

        /// <summary>
        /// 轮询输入并触发当前全局表中的按键回调。
        /// </summary>
        public void PollInput()
        {
            if (_activeBindings.Count == 0)
            {
                return;
            }

            foreach (var pair in _activeBindings)
            {
                if (Input.GetKeyDown(pair.Key))
                {
                    pair.Value.Invoke();
                }
            }
        }

        /// <summary>
        /// 清空全部模块与全局表，仅用于测试或域重载。
        /// </summary>
        public void Reset()
        {
            _activeBindings.Clear();
            _modules.Clear();
            _moduleOrder.Clear();
            RaiseChanged();
        }

#if UNITY_EDITOR
        /// <summary>
        /// 仅用于 EditMode 测试重置单例。
        /// </summary>
        public static void ResetSingletonForTests()
        {
            _instance = null;
        }
#endif

        private void ApplyModuleBindingsToGlobal(string moduleId, IReadOnlyDictionary<KeyCode, TestKeyBinding> bindings)
        {
            foreach (var pair in bindings)
            {
                _activeBindings[pair.Key] = new ActiveTestKeyBinding(moduleId, pair.Key, pair.Value);
            }
        }

        private void RebuildActiveBindings()
        {
            _activeBindings.Clear();

            foreach (var moduleId in _moduleOrder)
            {
                if (!_modules.TryGetValue(moduleId, out var moduleData))
                {
                    continue;
                }

                ApplyModuleBindingsToGlobal(moduleId, moduleData.Bindings);
            }
        }

        private void RaiseChanged()
        {
            Changed?.Invoke();
        }
    }
}

#endif
