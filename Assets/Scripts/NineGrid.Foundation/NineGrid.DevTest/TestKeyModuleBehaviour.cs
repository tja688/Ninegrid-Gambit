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
    /// MonoBehaviour 模块基类：OnEnable 挂载到 SO 级联栈，OnDisable 卸载。
    /// 新挂载层默认追加到栈底（最高优先级）；同键冲突由栈底向上溢出解析。
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
                LayerProfile,
                appendToBottom: true);
        }

        protected virtual void OnDisable()
        {
            TestKeyManager.Instance.DetachLayer(LayerId);
        }

        /// <summary>
        /// 将本层移到栈底，成为最高优先级（等同把 SO 拖到底部）。
        /// </summary>
        protected void PromoteThisLayer()
        {
            TestKeyManager.Instance.PromoteLayerToTop(LayerId);
        }

        /// <summary>兼容旧 API。</summary>
        protected void ActivateThisModule() => PromoteThisLayer();
    }
}

#endif
