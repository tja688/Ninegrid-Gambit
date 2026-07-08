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
        /// 载入 SO 级联栈，初始化层顺序（列表末项 = 最高优先级）。运行时优先级的唯一权威来源。
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
        /// 挂载一层运行时回调。不改变 SO 已定义的栈顺序；未在 SO 中声明的动态层默认插入栈顶（最低优先级）。
        /// </summary>
        public void AttachLayer(
            string layerId,
            IReadOnlyDictionary<KeyCode, TestKeyBinding> bindings,
            string displayName = null,
            TestKeyLayerProfileSO profile = null)
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

            if (!_stackOrder.Contains(layerId))
            {
                if (_configLayerIds.Contains(layerId))
                {
                    _stackOrder.Add(layerId);
                }
                else
                {
                    _stackOrder.Insert(0, layerId);
                    Debug.LogWarning(
                        $"[TestKeyManager] 层「{layerId}」未在 TestKeyStackConfigSO 中声明，已以最低优先级挂载。请将该层加入 SO 以控制优先级。");
                }
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

#if UNITY_EDITOR
        /// <summary>
        /// 将指定层写入 SO 栈底（最高优先级）并重新载入。仅编辑器下调试使用；运行时优先级以 SO 为准。
        /// </summary>
        public bool PromoteLayerToTop(string layerId)
        {
            if (_stackConfig == null || string.IsNullOrWhiteSpace(layerId))
            {
                return false;
            }

            _stackConfig.PromoteLayerById(layerId);
            UnityEditor.EditorUtility.SetDirty(_stackConfig);
            SetStack(_stackConfig);
            return _stackOrder.Count > 0 && _stackOrder[^1] == layerId;
        }
#endif

        /// <summary>
        /// 兼容旧 API：挂载层，不改变 SO 栈顺序。
        /// </summary>
        public void Register(string moduleId, IReadOnlyDictionary<KeyCode, TestKeyBinding> bindings, string displayName = null)
        {
            AttachLayer(moduleId, bindings, displayName, profile: null);
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

        /// <summary>
        /// 列出所有已挂载层的测试动作（含小键盘溢出项）。顺序与级联栈一致。
        /// </summary>
        public IReadOnlyList<TestKeyRegisteredAction> GetAllRegisteredActions()
        {
            var result = new List<TestKeyRegisteredAction>();
            var visited = new HashSet<string>(StringComparer.Ordinal);

            AppendLayerActions(result, visited, _stackOrder);

            foreach (var pair in _layers)
            {
                if (visited.Add(pair.Key))
                {
                    AppendLayerActions(result, pair.Key, pair.Value);
                }
            }

            return result;
        }

        /// <summary>
        /// 直接触发指定层的测试动作，不受小键盘级联与键位冲突限制。
        /// </summary>
        public bool TryInvokeRegisteredAction(string layerId, KeyCode key)
        {
            if (!_layers.TryGetValue(layerId, out var state))
            {
                return false;
            }

            if (!state.Bindings.TryGetValue(key, out var binding))
            {
                return false;
            }

            try
            {
                binding.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }

        public void PollInput()
        {
            if (_activeBindings.Count == 0)
            {
                return;
            }

            // 回调可能触发 Attach/Detach 并修改 _activeBindings，先快照再轮询。
            var snapshot = new List<KeyValuePair<KeyCode, ActiveTestKeyBinding>>(_activeBindings.Count);
            foreach (var pair in _activeBindings)
            {
                snapshot.Add(pair);
            }

            for (var i = 0; i < snapshot.Count; i++)
            {
                var pair = snapshot[i];
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

        private void AppendLayerActions(
            List<TestKeyRegisteredAction> result,
            HashSet<string> visited,
            IReadOnlyList<string> layerIds)
        {
            for (var i = 0; i < layerIds.Count; i++)
            {
                var layerId = layerIds[i];
                if (!visited.Add(layerId))
                {
                    continue;
                }

                if (_layers.TryGetValue(layerId, out var state))
                {
                    AppendLayerActions(result, layerId, state);
                }
            }
        }

        private void AppendLayerActions(
            List<TestKeyRegisteredAction> result,
            string layerId,
            TestKeyLayerRuntimeState state)
        {
            foreach (var pair in state.Bindings)
            {
                var isActive = _activeBindings.TryGetValue(pair.Key, out var active) && active.LayerId == layerId;
                result.Add(new TestKeyRegisteredAction(
                    layerId,
                    state.DisplayName,
                    pair.Key,
                    pair.Value.Label,
                    isActive));
            }
        }
    }
}

#endif
