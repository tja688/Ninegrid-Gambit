using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.LivingUI.Unity
{
    [DefaultExecutionOrder(-300)]
    [DisallowMultipleComponent]
    public sealed class LivingUiSceneLayoutSource : MonoBehaviour
    {
        public const string LiveRootName = "大盘";

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

        [Tooltip("运行时权威大盘根；留空时按名称「大盘」查找。")]
        [SerializeField] private Transform liveRoot;

        private readonly Dictionary<LivingUiLayoutId, LivingUiLayout> _snapshots = new();
        private readonly Dictionary<int, SpriteRenderer> _carriers = new();
        private readonly Dictionary<int, CarrierView> _carrierViews = new();
        private readonly List<LivingUiContentBinding> _contentBindings = new();

        public Transform LiveRoot
        {
            get
            {
                EnsureCaptured();
                return liveRoot;
            }
        }

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

        public IReadOnlyDictionary<int, CarrierView> CarrierViews
        {
            get
            {
                EnsureCaptured();
                return _carrierViews;
            }
        }

        public IReadOnlyList<LivingUiContentBinding> ContentBindings
        {
            get
            {
                EnsureCaptured();
                return _contentBindings;
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
            _carrierViews.Clear();
            _contentBindings.Clear();

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

            var carrierRoot = ResolveLiveRoot();
            liveRoot = carrierRoot;
            for (var carrierId = 1; carrierId <= 12; carrierId++)
            {
                var child = carrierRoot.Find(carrierId.ToString());
                var renderer = child != null ? child.GetComponent<SpriteRenderer>() : null;
                if (renderer == null)
                {
                    throw new InvalidOperationException($"固定载体根「{LiveRootName}」缺少 SpriteRenderer 载体 {carrierId}。");
                }

                _carriers.Add(carrierId, renderer);
                var view = child.GetComponent<CarrierView>();
                if (view == null) view = child.gameObject.AddComponent<CarrierView>();
                view.EnsureWired();
                _carrierViews.Add(carrierId, view);
            }

            CaptureContentBindings(carrierRoot);

            // 全部构型降级为蓝图：运行时关闭；仅大盘保持激活。
            foreach (var binding in bindings)
            {
                binding.Root.gameObject.SetActive(false);
            }

            carrierRoot.gameObject.SetActive(true);

            var contentController = GetComponent<LivingUiContentController>();
            if (contentController != null)
            {
                var blueprintList = new List<(LivingUiLayoutId LayoutId, Transform Root)>(bindings.Length);
                for (var i = 0; i < bindings.Length; i++)
                {
                    blueprintList.Add((bindings[i].LayoutId, bindings[i].Root));
                }

                contentController.RebuildByNamePresence(carrierRoot, blueprintList);
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

        private Transform ResolveLiveRoot()
        {
            if (liveRoot != null) return liveRoot;
            var found = FindSceneRoot(LiveRootName);
            if (found == null)
            {
                throw new InvalidOperationException($"未找到运行时权威根「{LiveRootName}」。");
            }

            return found;
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
                Binding(LivingUiLayoutId.RewardChoice, "大盘构型3-选择奖励"),
                Binding(LivingUiLayoutId.DeckPreview, "大盘构型4-打开卡组视图"),
                Binding(LivingUiLayoutId.Room, "大盘构型5-房间基础面板"),
                Binding(LivingUiLayoutId.Route, "大盘构型6-预备待定"),
            };
        }

        private static LayoutRootBinding Binding(LivingUiLayoutId id, string rootName)
        {
            return new LayoutRootBinding { LayoutId = id, Root = FindSceneRoot(rootName) };
        }

        private static Transform FindSceneRoot(string rootName)
        {
            var gameObject = GameObject.Find(rootName);
            if (gameObject != null) return gameObject.transform;

            var allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (var index = 0; index < allTransforms.Length; index++)
            {
                var candidate = allTransforms[index];
                if (candidate.gameObject.scene.IsValid()
                    && candidate.parent == null
                    && candidate.name == rootName)
                {
                    return candidate;
                }
            }

            return null;
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

        private void CaptureContentBindings(Transform carrierRoot)
        {
            var markers = carrierRoot.GetComponentsInChildren<LivingUiContentMarker>(true);
            for (var i = 0; i < markers.Length; i++)
            {
                _contentBindings.Add(markers[i].ToBinding());
            }
        }
    }
}
