using DamageNumbersPro;
using NineGrid.Core;
using NineGrid.Flow.Presentation;
using QFramework;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Flow
{
    /// <summary>
    /// 伤害数字弹出统一管理器：封装 DamageNumbersPro 的 2D 世界空间 Spawn，并强制 Main 图层排序。
    /// </summary>
    public sealed class DamageNumberManagerSingleton : MonoBehaviour
    {
        private const string DefaultSortingLayerName = "Main";
        private const int DefaultSortingOrder = 5;

        private IUnRegister _damageEventUnRegister;

        [Tooltip("用于 Spawn 的伤害数字预制体（DamageNumberMesh，2D 世界空间）。留空时无法生成。")]
        [SerializeField] private DamageNumber defaultPrefab;

        [Tooltip("留空则使用 Camera.main；也可手动拖入主相机。")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("生成点世界 Z；正交 2D 默认 0，与场景 Sprite 同平面。")]
        [SerializeField] private float spawnWorldZ;

        [Tooltip("所有弹出数字使用的 Sorting Layer 名称。")]
        [SerializeField] private string sortingLayerName = DefaultSortingLayerName;

        [Tooltip("弹出数字的 sortingOrder，需 ≥5 以免被 Main 背景遮挡。")]
        [SerializeField] private int sortingOrder = DefaultSortingOrder;

        [Tooltip("SpawnRandomAtScreenCenter 时的随机伤害下限（含）。")]
        [SerializeField] private int randomMin = 1;

        [Tooltip("SpawnRandomAtScreenCenter 时的随机伤害上限（含）。")]
        [SerializeField] private int randomMax = 999;

        private void Awake()
        {
            ResolveCamera();
            PrewarmDefaultPrefab();
            RegisterDamageEvents();
        }

        private void OnDestroy()
        {
            UnregisterDamageEvents();
        }

        private void RegisterDamageEvents()
        {
            UnregisterDamageEvents();
            var arch = NineGridArchitecture.Interface;
            if (arch == null)
            {
                return;
            }

            _damageEventUnRegister = arch.RegisterEvent<DamageNumberRequested>(OnDamageNumberRequested);
        }

        private void UnregisterDamageEvents()
        {
            _damageEventUnRegister?.UnRegister();
            _damageEventUnRegister = null;
        }

        private void OnDamageNumberRequested(DamageNumberRequested e)
        {
            if (e.Amount <= 0)
            {
                return;
            }

            SpawnAtWorldPosition(e.WorldPosition, e.Amount);
        }

        /// <summary>
        /// 在指定世界坐标弹出伤害数字。
        /// </summary>
        public DamageNumber SpawnAtWorldPosition(Vector3 worldPosition, float number)
        {
            if (!TryResolvePrefab(out var prefab))
            {
                return null;
            }

            worldPosition.z = spawnWorldZ;
            var popup = prefab.Spawn(worldPosition, number);
            ApplySorting(popup);
            return popup;
        }

        /// <summary>
        /// 在屏幕中心对应的世界坐标弹出伤害数字。
        /// </summary>
        public DamageNumber SpawnAtScreenCenter(float number)
        {
            return SpawnAtWorldPosition(GetScreenCenterWorldPosition(), number);
        }

        /// <summary>
        /// 在屏幕中心弹出 [randomMin, randomMax] 范围内的随机整数伤害。
        /// </summary>
        public DamageNumber SpawnRandomAtScreenCenter()
        {
            var min = Mathf.Min(randomMin, randomMax);
            var max = Mathf.Max(randomMin, randomMax);
            var number = Random.Range(min, max + 1);
            return SpawnAtScreenCenter(number);
        }

        /// <summary>
        /// 在当前鼠标屏幕位置对应的世界坐标弹出伤害数字。
        /// </summary>
        public DamageNumber SpawnAtMousePosition(float number)
        {
            return SpawnAtWorldPosition(ScreenToWorld(Input.mousePosition), number);
        }

        /// <summary>
        /// 在鼠标位置弹出 [randomMin, randomMax] 范围内的随机整数伤害。
        /// </summary>
        public DamageNumber SpawnRandomAtMousePosition()
        {
            var min = Mathf.Min(randomMin, randomMax);
            var max = Mathf.Max(randomMin, randomMax);
            var number = Random.Range(min, max + 1);
            return SpawnAtMousePosition(number);
        }

        private bool TryResolvePrefab(out DamageNumber prefab)
        {
            prefab = defaultPrefab;
            if (prefab != null)
            {
                return true;
            }

            Debug.LogWarning("[DamageNumberManager] 未配置 defaultPrefab，无法生成伤害数字。");
            return false;
        }

        private void ResolveCamera()
        {
            if (targetCamera != null)
            {
                return;
            }

            targetCamera = Camera.main;
        }

        private void PrewarmDefaultPrefab()
        {
            if (defaultPrefab == null || !defaultPrefab.enablePooling)
            {
                return;
            }

            defaultPrefab.PrewarmPool();
        }

        private Vector3 GetScreenCenterWorldPosition()
        {
            var screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            return ScreenToWorld(screenCenter);
        }

        private Vector3 ScreenToWorld(Vector2 screenPosition)
        {
            ResolveCamera();
            if (targetCamera == null)
            {
                Debug.LogWarning("[DamageNumberManager] 未找到相机，回退到屏幕坐标。");
                return new Vector3(screenPosition.x, screenPosition.y, spawnWorldZ);
            }

            var screenPoint = new Vector3(screenPosition.x, screenPosition.y, GetScreenPointDepth());
            var world = targetCamera.ScreenToWorldPoint(screenPoint);
            world.z = spawnWorldZ;
            return world;
        }

        private float GetScreenPointDepth()
        {
            if (targetCamera.orthographic)
            {
                return Mathf.Abs(targetCamera.transform.position.z - spawnWorldZ);
            }

            return targetCamera.nearClipPlane;
        }

        private void ApplySorting(DamageNumber popup)
        {
            if (popup == null)
            {
                return;
            }

            var sortingLayerId = SortingLayer.NameToID(sortingLayerName);
            if (sortingLayerId == 0 && sortingLayerName != "Default")
            {
                Debug.LogWarning($"[DamageNumberManager] 未找到 Sorting Layer「{sortingLayerName}」，保持预制体默认排序。");
            }

            foreach (var sortingGroup in popup.GetComponentsInChildren<SortingGroup>(true))
            {
                sortingGroup.sortingLayerID = sortingLayerId;
                sortingGroup.sortingOrder = sortingOrder;
            }

            foreach (var renderer in popup.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sortingLayerID = sortingLayerId;
                renderer.sortingOrder = sortingOrder;
            }
        }
    }
}
