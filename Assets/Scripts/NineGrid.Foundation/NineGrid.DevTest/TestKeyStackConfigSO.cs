#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.DevTest
{
    /// <summary>
    /// 测试按键级联栈配置。列表从上到下优先级递增；<b>最末项为最高优先级</b>，同键仅由最末层持有，其余层溢出。
    /// </summary>
    [CreateAssetMenu(fileName = "TestKeyStack", menuName = "NineGrid/DevTest/Test Key Stack")]
    public sealed class TestKeyStackConfigSO : ScriptableObject
    {
        [Tooltip("级联层列表：上方低优先级，下方高优先级。手动将某层拖到底部即可置顶激活。")]
        [SerializeField] private List<TestKeyLayerProfileSO> layers = new();

        public IReadOnlyList<TestKeyLayerProfileSO> Layers => layers;

        public void PromoteLayer(TestKeyLayerProfileSO profile)
        {
            if (profile == null || layers == null)
            {
                return;
            }

            var index = layers.IndexOf(profile);
            if (index < 0)
            {
                layers.Add(profile);
            }
            else
            {
                layers.RemoveAt(index);
                layers.Add(profile);
            }
        }

        public void PromoteLayerById(string layerId)
        {
            if (string.IsNullOrWhiteSpace(layerId) || layers == null)
            {
                return;
            }

            for (var i = 0; i < layers.Count; i++)
            {
                if (layers[i] != null && layers[i].LayerId == layerId)
                {
                    var profile = layers[i];
                    layers.RemoveAt(i);
                    layers.Add(profile);
                    return;
                }
            }
        }

        /// <summary>
        /// 将层注册到栈底（最高优先级）。层不在列表中时追加；已存在时移到底部。
        /// </summary>
        public void RegisterLayerAsHighestPriority(TestKeyLayerProfileSO profile) => PromoteLayer(profile);
    }
}

#endif
