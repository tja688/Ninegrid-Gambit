#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.DevTest
{
    /// <summary>
    /// 流式构建模块按键注册表。
    /// </summary>
    public sealed class TestKeyRegistrationBuilder
    {
        private readonly Dictionary<KeyCode, TestKeyBinding> _bindings = new();

        public TestKeyRegistrationBuilder Bind(KeyCode key, string label, Action callback)
        {
            _bindings[key] = new TestKeyBinding(label, callback);
            return this;
        }

        public TestKeyRegistrationBuilder Bind(KeyCode key, Action callback)
        {
            return Bind(key, key.ToString(), callback);
        }

        public IReadOnlyDictionary<KeyCode, TestKeyBinding> Build()
        {
            if (_bindings.Count == 0)
            {
                throw new InvalidOperationException("No test keys were registered.");
            }

            return new Dictionary<KeyCode, TestKeyBinding>(_bindings);
        }
    }

    /// <summary>
    /// MonoBehaviour 模块基类：OnEnable 向 SO 级联栈挂载回调，OnDisable 卸载。优先级由 TestKeyStackConfigSO 唯一决定。
    /// </summary>
    public abstract class TestKeyModuleBehaviour : MonoBehaviour
    {
        [Tooltip("关联的层配置 SO。留空时使用 ModuleId 作为动态层。")]
        [SerializeField] protected TestKeyLayerProfileSO layerProfile;

        protected abstract string ModuleId { get; }

        protected virtual string DisplayName => LayerProfile != null ? LayerProfile.DisplayName : ModuleId;

        protected virtual TestKeyLayerProfileSO LayerProfile => layerProfile;

        protected virtual string LayerId => LayerProfile != null ? LayerProfile.LayerId : ModuleId;

        protected abstract void ConfigureBindings(TestKeyRegistrationBuilder builder);

        protected virtual void OnEnable()
        {
            var builder = new TestKeyRegistrationBuilder();
            ConfigureBindings(builder);
            TestKeyManager.Instance.AttachLayer(
                LayerId,
                builder.Build(),
                DisplayName,
                LayerProfile);
        }

        protected virtual void OnDisable()
        {
            TestKeyManager.Instance.DetachLayer(LayerId);
        }
    }
}

#endif
