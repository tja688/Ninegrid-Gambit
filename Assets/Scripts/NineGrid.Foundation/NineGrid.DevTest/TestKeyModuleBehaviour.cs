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
    /// MonoBehaviour 模块基类：OnEnable 注册、OnDisable 注销。
    /// </summary>
    public abstract class TestKeyModuleBehaviour : MonoBehaviour
    {
        protected abstract string ModuleId { get; }

        protected virtual string DisplayName => ModuleId;

        protected abstract void ConfigureBindings(TestKeyRegistrationBuilder builder);

        protected virtual void OnEnable()
        {
            var builder = new TestKeyRegistrationBuilder();
            ConfigureBindings(builder);
            TestKeyManager.Instance.Register(ModuleId, builder.Build(), DisplayName);
        }

        protected virtual void OnDisable()
        {
            TestKeyManager.Instance.UnregisterModule(ModuleId);
        }

        protected void ActivateThisModule()
        {
            TestKeyManager.Instance.ActivateModule(ModuleId);
        }
    }
}

#endif
