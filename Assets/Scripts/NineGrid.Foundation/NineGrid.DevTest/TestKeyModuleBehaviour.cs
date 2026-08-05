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

        protected virtual TestKeyLayerProfileSO LayerProfile
        {
            get
            {
                if (layerProfile != null)
                {
                    return layerProfile;
                }

                if (_runtimeProfile == null)
                {
                    _runtimeProfile = ResolveProfileByLayerId(ModuleId);
                }

                return _runtimeProfile;
            }
        }

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

        private TestKeyLayerProfileSO _runtimeProfile;
        private static TestKeyLayerProfileSO[] _profileCache;

        /// <summary>
        /// 运行时安装（DevTestSceneInstaller AddComponent）时无序列化 profile，按 LayerId 从
        /// Resources/DevTest 回填，保持级联栈显示名与元数据一致。
        /// </summary>
        private static TestKeyLayerProfileSO ResolveProfileByLayerId(string layerId)
        {
            if (_profileCache == null)
            {
                _profileCache = Resources.LoadAll<TestKeyLayerProfileSO>("DevTest");
            }

            for (var i = 0; i < _profileCache.Length; i++)
            {
                if (_profileCache[i] != null && _profileCache[i].LayerId == layerId)
                {
                    return _profileCache[i];
                }
            }

            return null;
        }
    }
}

#endif
