#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.DevTest
{
    /// <summary>
    /// 开发测试按键管理器：基于 SO 级联栈，自栈底（列表末项）向上溢出解析按键归属。
    /// </summary>
    public sealed class TestKeyManager
    {
        private static TestKeyManager _instance;

        private readonly Dictionary<KeyCode, ActiveTestKeyBinding> _activeBindings = new();
        private readonly Dictionary<string, TestKeyLayerRuntimeState> _layers = new(StringComparer.Ordinal);
        private readonly List<string> _stackOrder = new();
        private readonly HashSet<string> _configLayerIds = new(StringComparer.Ordinal);

        private TestKeyStackConfigSO _stackConfig;

        public static TestKeyManager Instance => _instance ??= new TestKeyManager();

        public event Action Changed;

        public TestKeyStackConfigSO StackConfig => _stackConfig;

        public IReadOnlyDictionary<KeyCode, ActiveTestKeyBinding> ActiveBindings => _activeBindings;

        public IReadOnlyList<string> StackOrder => _stackOrder;

        public IReadOnlyDictionary<string, TestKeyLayerRuntimeState> Layers => _layers;

        /// <summary>
        /// 载入 SO 级联栈，初始化层顺序（列表末项 = 最高优先级）。
        /// </summary>
        public void SetStack(TestKeyStackConfigSO stackConfig)
        {
            _stackConfig = stackConfig;
            _stackOrder.Clear();
            _configLayerIds.Clear();

            if (stackConfig?.Layers != null)
            {
                for (var i = 0; i < stackConfig.Layers.Count; i++)
                {
                    var profile = stackConfig.Layers[i];
                    if (profile == null || string.IsNullOrWhiteSpace(profile.LayerId))
                    {
                        continue;
                    }

                    EnsureLayerState(profile.LayerId, profile.DisplayName, profile);
                    if (!_stackOrder.Contains(profile.LayerId))
                    {
                        _stackOrder.Add(profile.LayerId);
                    }

                    _configLayerIds.Add(profile.LayerId);
                }
            }

            RebuildCascade();
        }

        /// <summary>
        /// 挂载一层运行时回调。新层默认追加到栈底（最高优先级）。
        /// </summary>
        public void AttachLayer(
            string layerId,
            IReadOnlyDictionary<KeyCode, TestKeyBinding> bindings,
            string displayName = null,
            TestKeyLayerProfileSO profile = null,
            bool appendToBottom = true)
        {
            if (string.IsNullOrWhiteSpace(layerId))
            {
                throw new ArgumentException("Layer id is required.", nameof(layerId));
            }

            if (bindings == null || bindings.Count == 0)
            {
                throw new ArgumentException("At least one binding is required.", nameof(bindings));
            }

            EnsureLayerState(layerId, displayName, profile);
            _layers[layerId] = _layers[layerId].WithBindings(bindings);

            if (appendToBottom)
            {
                MoveLayerToBottom(layerId);
            }
            else if (!_stackOrder.Contains(layerId))
            {
                _stackOrder.Insert(0, layerId);
            }

            RebuildCascade();
        }

        /// <summary>
        /// 卸载一层运行时回调。动态层会同时移出栈。
        /// </summary>
        public void DetachLayer(string layerId)
        {
            if (string.IsNullOrWhiteSpace(layerId) || !_layers.ContainsKey(layerId))
            {
                return;
            }

            _layers.Remove(layerId);

            if (!_configLayerIds.Contains(layerId))
            {
                _stackOrder.Remove(layerId);
            }

            RebuildCascade();
        }

        /// <summary>
        /// 将指定层移到栈底（列表末项），使其成为最高优先级。
        /// </summary>
        public bool PromoteLayerToTop(string layerId)
        {
            if (string.IsNullOrWhiteSpace(layerId) || !_stackOrder.Contains(layerId))
            {
                return false;
            }

            MoveLayerToBottom(layerId);

#if UNITY_EDITOR
            if (_stackConfig != null && _configLayerIds.Contains(layerId))
            {
                _stackConfig.PromoteLayerById(layerId);
            }
#endif

            RebuildCascade();
            return true;
        }

        /// <summary>
        /// 兼容旧 API：等同于 PromoteLayerToTop。
        /// </summary>
        public bool ActivateModule(string moduleId) => PromoteLayerToTop(moduleId);

        /// <summary>
        /// 兼容旧 API：挂载层并追加到栈底。
        /// </summary>
        public void Register(string moduleId, IReadOnlyDictionary<KeyCode, TestKeyBinding> bindings, string displayName = null)
        {
            AttachLayer(moduleId, bindings, displayName, profile: null, appendToBottom: true);
        }

        /// <summary>
        /// 兼容旧 API：卸载层。
        /// </summary>
        public bool UnregisterModule(string moduleId)
        {
            if (!_layers.ContainsKey(moduleId))
            {
                return false;
            }

            DetachLayer(moduleId);
            return true;
        }

        public bool TryGetOwner(KeyCode key, out string layerId)
        {
            if (_activeBindings.TryGetValue(key, out var binding))
            {
                layerId = binding.LayerId;
                return true;
            }

            layerId = null;
            return false;
        }

        public IReadOnlyList<KeyCode> GetActiveKeysForLayer(string layerId)
        {
            var result = new List<KeyCode>();
            foreach (var pair in _activeBindings)
            {
                if (pair.Value.LayerId == layerId)
                {
                    result.Add(pair.Key);
                }
            }

            return result;
        }

        public IReadOnlyList<KeyCode> GetOverflowKeysForLayer(string layerId)
        {
            var result = new List<KeyCode>();
            if (!_layers.TryGetValue(layerId, out var state))
            {
                return result;
            }

            foreach (var pair in state.Bindings)
            {
                if (_activeBindings.TryGetValue(pair.Key, out var active) && active.LayerId != layerId)
                {
                    result.Add(pair.Key);
                }
            }

            return result;
        }

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

        public void Reset()
        {
            _activeBindings.Clear();
            _layers.Clear();
            _stackOrder.Clear();
            _configLayerIds.Clear();
            _stackConfig = null;
            RaiseChanged();
        }

#if UNITY_EDITOR
        public static void ResetSingletonForTests()
        {
            _instance = null;
        }
#endif

        private void EnsureLayerState(string layerId, string displayName, TestKeyLayerProfileSO profile)
        {
            if (_layers.TryGetValue(layerId, out var existing))
            {
                _layers[layerId] = existing.WithDisplayName(displayName, profile);
                return;
            }

            _layers[layerId] = new TestKeyLayerRuntimeState(layerId, displayName, profile);
        }

        private void MoveLayerToBottom(string layerId)
        {
            _stackOrder.Remove(layerId);
            _stackOrder.Add(layerId);
        }

        private void RebuildCascade()
        {
            _activeBindings.Clear();
            var claimed = new HashSet<KeyCode>();

            for (var i = _stackOrder.Count - 1; i >= 0; i--)
            {
                var layerId = _stackOrder[i];
                if (!_layers.TryGetValue(layerId, out var state))
                {
                    continue;
                }

                foreach (var pair in state.Bindings)
                {
                    if (!claimed.Add(pair.Key))
                    {
                        continue;
                    }

                    _activeBindings[pair.Key] = new ActiveTestKeyBinding(layerId, pair.Key, pair.Value);
                }
            }

            RaiseChanged();
        }

        private void RaiseChanged()
        {
            Changed?.Invoke();
        }
    }
}

#endif
