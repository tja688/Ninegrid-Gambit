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

        [Header("数值动态缩放（1x~2x）")]
        [Tooltip("缩放区间下界：飘字数值 ≤ 此值显示 1 倍。")]
        [SerializeField] private float scaleFromNumber = 1f;

        [Tooltip("缩放区间上界：飘字数值 ≥ 此值显示 2 倍（满倍封顶）。")]
        [SerializeField] private float scaleToNumber = 25f;

        [Tooltip("最小数字（1 倍）的可见时长（秒）。")]
        [SerializeField] private float baseLifetime = 0.8f;

        [Tooltip("满倍数字（2 倍）的可见时长（秒），随倍率线性插值。")]
        [SerializeField] private float maxLifetime = 1.6f;

        [Tooltip("治疗飘字颜色。")]
        [SerializeField] private Color healColor = new Color(0.35f, 1f, 0.4f, 1f);

        [Tooltip("伤害飘字颜色：每次生成显式重设，防止治疗色经对象池残留。")]
        [SerializeField] private Color damageColor = Color.white;

        [Header("拆分飘字（血量/护甲伤害）")]
        [Tooltip("拆分模式血量伤害颜色（#CB3834）。")]
        [SerializeField] private Color hpDamageColor = new Color(0.796f, 0.22f, 0.204f, 1f);

        [Tooltip("拆分模式护甲伤害颜色（#5E7E74）。")]
        [SerializeField] private Color armorDamageColor = new Color(0.369f, 0.494f, 0.455f, 1f);

        [Tooltip("拆分飘字缩放区间上界：拆分数值普遍小于总伤害，单独收窄上界让曲线仍可触达满倍。")]
        [SerializeField] private float splitScaleToNumber = 12f;

        [Tooltip("拆分飘字最小数字（1 倍）的可见时长（秒）。")]
        [SerializeField] private float splitBaseLifetime = 0.9f;

        [Tooltip("拆分飘字满倍数字（2 倍）的可见时长（秒）。")]
        [SerializeField] private float splitMaxLifetime = 1.7f;

        [Tooltip("拆分飘字位置随机抖动半径（世界单位）：血/甲两字同命中避免完全重叠。")]
        [SerializeField] private float splitJitterRadius = 0.35f;

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

            SpawnAtWorldPosition(e.WorldPosition, e.Amount, e.Kind);
        }

        /// <summary>
        /// 在指定世界坐标弹出伤害数字，并按数值应用动态缩放（1x~2x）与可见时长。
        /// 区间依据内容数值分布设定（见 <see cref="scaleToNumber"/>）：日常单发伤害/治疗集中在 1~10，
        /// 精英/Boss 与成长叠伤后可达 15~25，故 25 封顶 2 倍，确保一局内可触达满倍。
        /// 拆分（血/甲）飘字走独立的 <see cref="splitScaleToNumber"/> 区间并施加随机抖动。
        /// </summary>
        public DamageNumber SpawnAtWorldPosition(
            Vector3 worldPosition,
            float number,
            DamageNumberKind kind = DamageNumberKind.Damage)
        {
            if (!TryResolvePrefab(out var prefab))
            {
                return null;
            }

            if (IsSplitKind(kind) && splitJitterRadius > 0f)
            {
                var jitter = (Vector2)Random.insideUnitCircle * splitJitterRadius;
                worldPosition.x += jitter.x;
                worldPosition.y += jitter.y;
            }

            worldPosition.z = spawnWorldZ;
            var popup = prefab.Spawn(worldPosition, number);
            ApplyDynamicPresentation(popup, number, kind);
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
            if (!WorldPointerUtility.TryGetPointerScreen(out var screen))
            {
                return null;
            }

            return SpawnAtWorldPosition(ScreenToWorld(screen), number);
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

        /// <summary>
        /// 应用动态表现：数值在 [scaleFromNumber, scaleToNumber] 内线性映射 1x~2x，
        /// 可见时长同步线性插值 [baseLifetime, maxLifetime]，并显式重设颜色（防池残留）。
        /// 拆分（血/甲）飘字使用独立的收窄区间与稍长时长，补偿拆分数值变小导致的曲线退化。
        /// Spawn 返回后同步设置即生效：插件在下一帧 Restart()/Start() 才把 lifetime 拷入
        /// currentLifetime，UpdateText 每帧按 number 重算 numberScale（DamageNumbersPro 约定）。
        /// </summary>
        private void ApplyDynamicPresentation(DamageNumber popup, float number, DamageNumberKind kind)
        {
            if (popup == null)
            {
                return;
            }

            var isSplit = IsSplitKind(kind);
            var fromNumber = scaleFromNumber;
            var toNumber = isSplit ? splitScaleToNumber : scaleToNumber;
            var baseLife = isSplit ? splitBaseLifetime : baseLifetime;
            var maxLife = isSplit ? splitMaxLifetime : maxLifetime;
            var span = Mathf.Max(0f, toNumber - fromNumber);
            var normalized = span > 0f
                ? Mathf.Clamp01((number - fromNumber) / span)
                : 1f;

            popup.enableScaleByNumber = true;
            popup.scaleByNumberSettings = new ScaleByNumberSettings
            {
                fromNumber = fromNumber,
                toNumber = toNumber,
                fromScale = 1f,
                toScale = 2f
            };

            popup.lifetime = Mathf.Lerp(baseLife, maxLife, normalized);
            popup.SetColor(ResolveColor(kind));
        }

        private static bool IsSplitKind(DamageNumberKind kind)
        {
            return kind == DamageNumberKind.HpDamage || kind == DamageNumberKind.ArmorDamage;
        }

        private Color ResolveColor(DamageNumberKind kind)
        {
            switch (kind)
            {
                case DamageNumberKind.Heal:
                    return healColor;
                case DamageNumberKind.HpDamage:
                    return hpDamageColor;
                case DamageNumberKind.ArmorDamage:
                    return armorDamageColor;
                default:
                    return damageColor;
            }
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
