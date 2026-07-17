using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.LivingUI.Unity
{
    [DefaultExecutionOrder(-300)]
    [DisallowMultipleComponent]
    public sealed class LivingUiSceneLayoutSource : MonoBehaviour
    {
        [Serializable]
        private struct LayoutRootBinding
        {
            [Tooltip("该构型的运行时标识。")]
            public LivingUiLayoutId LayoutId;

            [Tooltip("构型样板根；留空时运行时按内置 UITestSence 根名称自动查找，找不到会报错。")]
            public Transform Root;
        }

        [Tooltip("七组构型样板；数组留空或根引用留空时，运行时会按 UITestSence 的构型根名称自动查找。")]
        [SerializeField] private LayoutRootBinding[] layouts;

        private readonly Dictionary<LivingUiLayoutId, LivingUiLayout> _snapshots = new();
        private readonly Dictionary<int, SpriteRenderer> _carriers = new();

        public IReadOnlyDictionary<LivingUiLayoutId, LivingUiLayout> Snapshots
        {
            get
            {
                EnsureCaptured();
                return _snapshots;
            }
        }

        public IReadOnlyDictionary<int, SpriteRenderer> Carriers
        {
            get
            {
                EnsureCaptured();
                return _carriers;
            }
        }

        private void Awake()
        {
            Capture();
        }

        [ContextMenu("重新抓取构型样板")]
        public void Capture()
        {
            _snapshots.Clear();
            _carriers.Clear();

            var bindings = ResolveBindings();
            HashSet<int> expectedIds = null;
            foreach (var binding in bindings)
            {
                if (binding.Root == null)
                {
                    throw new InvalidOperationException($"未找到构型样板 {binding.LayoutId} 的根对象。");
                }

                var terminals = CaptureTerminals(binding.Root);
                expectedIds ??= new HashSet<int>(terminals.Keys);
                if (!expectedIds.SetEquals(terminals.Keys))
                {
                    throw new InvalidOperationException($"构型 {binding.LayoutId} 的载体身份集合与首个构型不一致。");
                }

                _snapshots.Add(binding.LayoutId, new LivingUiLayout(binding.LayoutId, terminals));
            }

            var carrierRoot = FindBindingRoot(bindings, LivingUiLayoutId.MainMenu);
            for (var carrierId = 1; carrierId <= 12; carrierId++)
            {
                var child = carrierRoot.Find(carrierId.ToString());
                var renderer = child != null ? child.GetComponent<SpriteRenderer>() : null;
                if (renderer == null)
                {
                    throw new InvalidOperationException($"固定载体根缺少 SpriteRenderer 载体 {carrierId}。");
                }

                _carriers.Add(carrierId, renderer);
            }

            foreach (var binding in bindings)
            {
                binding.Root.gameObject.SetActive(binding.LayoutId == LivingUiLayoutId.MainMenu);
            }
        }

        public LivingUiLayout GetLayout(LivingUiLayoutId layoutId)
        {
            EnsureCaptured();
            return _snapshots[layoutId];
        }

        private void EnsureCaptured()
        {
            if (_snapshots.Count == 0 || _carriers.Count == 0)
            {
                Capture();
            }
        }

        private LayoutRootBinding[] ResolveBindings()
        {
            var defaults = CreateDefaultBindings();
            if (layouts == null || layouts.Length == 0)
            {
                layouts = defaults;
                return layouts;
            }

            for (var index = 0; index < layouts.Length; index++)
            {
                if (layouts[index].Root != null) continue;
                for (var defaultIndex = 0; defaultIndex < defaults.Length; defaultIndex++)
                {
                    if (defaults[defaultIndex].LayoutId != layouts[index].LayoutId) continue;
                    layouts[index].Root = defaults[defaultIndex].Root;
                    break;
                }
            }

            return layouts;
        }

        private static LayoutRootBinding[] CreateDefaultBindings()
        {
            return new[]
            {
                Binding(LivingUiLayoutId.MainMenu, "大盘构型0-主菜单"),
                Binding(LivingUiLayoutId.CharacterChoice, "大盘构型1-人物选择"),
                Binding(LivingUiLayoutId.Battle, "大盘构型2-核心战斗面板"),
                Binding(LivingUiLayoutId.RewardChoice, "大盘构型2-1-选择奖励"),
                Binding(LivingUiLayoutId.DeckPreview, "大盘构型2-2-打开卡组视图"),
                Binding(LivingUiLayoutId.Room, "大盘构型3-房间基础面板"),
                Binding(LivingUiLayoutId.Route, "大盘构型4-路线展示"),
            };
        }

        private static LayoutRootBinding Binding(LivingUiLayoutId id, string rootName)
        {
            var gameObject = GameObject.Find(rootName);
            if (gameObject == null)
            {
                var allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
                for (var index = 0; index < allTransforms.Length; index++)
                {
                    var candidate = allTransforms[index];
                    if (candidate.gameObject.scene.IsValid() && candidate.name == rootName)
                    {
                        gameObject = candidate.gameObject;
                        break;
                    }
                }
            }

            return new LayoutRootBinding { LayoutId = id, Root = gameObject != null ? gameObject.transform : null };
        }

        private static Dictionary<int, LivingUiTerminal> CaptureTerminals(Transform root)
        {
            var terminals = new Dictionary<int, LivingUiTerminal>(12);
            for (var carrierId = 1; carrierId <= 12; carrierId++)
            {
                var child = root.Find(carrierId.ToString());
                var renderer = child != null ? child.GetComponent<SpriteRenderer>() : null;
                if (renderer == null)
                {
                    throw new InvalidOperationException($"构型样板 {root.name} 缺少 SpriteRenderer 载体 {carrierId}。");
                }

                terminals.Add(carrierId, new LivingUiTerminal(
                    carrierId,
                    child.position,
                    renderer.size,
                    renderer.sortingLayerID,
                    renderer.sortingOrder));
            }

            return terminals;
        }

        private static Transform FindBindingRoot(LayoutRootBinding[] bindings, LivingUiLayoutId id)
        {
            for (var index = 0; index < bindings.Length; index++)
            {
                if (bindings[index].LayoutId == id) return bindings[index].Root;
            }

            throw new InvalidOperationException($"缺少构型绑定 {id}。");
        }
    }
}
