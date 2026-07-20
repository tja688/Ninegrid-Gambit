using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace NineGrid.VisualLook
{
    /// <summary>
    /// 把「全屏 PixelSnap + Overlay 文字逃逸」收敛为：
    /// - 世界默认走 PixelSnap + 扫描线（Base 相机）
    /// - NoPixelSnap 层（文字等）由 Overlay UICamera 渲染，不吃 snap
    /// - 文字扫描线默认由 TMP Scanline 材质补齐（与 Rig 参数同步）
    /// - 可选 useUnifiedScanline：关掉 TMP 扫描线，改由 Overlay 末相机统一 blit（需 Feature.overlayOwnsScanline，实验性）
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class TableNineLookRig : MonoBehaviour
    {
        const string NoSnapLayerName = "NoPixelSnap";

        [Header("Look Material")]
        [SerializeField]
        [Tooltip("手动装配：Assets/Arts/VisualProfiles/TableNineSelectiveLook.mat；控制 snap/scanline 参数。")]
        Material lookMaterial;

        [SerializeField]
        [Tooltip("兼容旧全屏材质；若 Selective 未就绪，可继续驱动 TableNinePixelSnap.mat。")]
        Material legacyPixelSnapMaterial;

        [SerializeField]
        [Tooltip("像素网格分辨率，默认与 PixelPerfectCamera 参考分辨率一致。")]
        Vector2 pixelResolution = new Vector2(960f, 540f);

        [SerializeField]
        [Tooltip("是否启用 UV Pixel Snap（对未受保护像素）。")]
        [Range(0f, 1f)]
        float pixelSnap = 1f;

        [SerializeField]
        [Tooltip("是否启用全局扫描线（写入 Look 材质；文字侧默认由 TMP 材质同步同一参数）。")]
        [Range(0f, 1f)]
        float scanlineEnabled = 1f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("扫描线强度。")]
        float scanlineIntensity = 0.3f;

        [SerializeField]
        [Range(1f, 8f)]
        [Tooltip("扫描线间距（像素行）。")]
        float scanlineSpacing = 2f;

        [Header("Camera Stack")]
        [SerializeField]
        [Tooltip("世界相机；留空则运行时自动取 Camera.main。")]
        Camera worldCamera;

        [SerializeField]
        [Tooltip("文字 Overlay 相机；留空则运行时自动查找/创建子物体 UICamera。")]
        Camera uiCamera;

        [SerializeField]
        [Tooltip("需要拉回相机域的 Canvas 根（如 大盘构型0-主菜单/Text Overlay UI）。")]
        Canvas[] livingTextCanvases;

        [SerializeField]
        [Tooltip("启用后：世界相机剔除 NoPixelSnap，UI 相机只渲染 NoPixelSnap，并挂到 camera stack。")]
        bool applyCameraStack = true;

        [SerializeField]
        [Tooltip("启用后：把 livingTextCanvases 转为 Screen Space Camera，并落到 NoPixelSnap 层。")]
        bool rebindLivingTextCanvases = true;

        [SerializeField]
        [Tooltip("实验：关掉 TMP 自带扫描线，改由 Overlay 末相机统一 blit。需 Feature.overlayOwnsScanline=true 且已验证，否则文字会丢扫描线。")]
        bool useUnifiedScanline = false;

        [SerializeField]
        [Tooltip("把 TMP 扫描线材质分辨率等参数同步到本 Rig（扫描线开关由 useUnifiedScanline 决定）。")]
        bool syncTmpScanlineMaterials = true;

        int _noSnapLayer = -1;

        void OnEnable()
        {
            EnsureReferences();
            ApplyLookParams();
            ApplyRig();
        }

        void OnValidate()
        {
            pixelResolution.x = Mathf.Max(1f, pixelResolution.x);
            pixelResolution.y = Mathf.Max(1f, pixelResolution.y);
            EnsureReferences();
            ApplyLookParams();
        }

        void Update()
        {
            ApplyLookParams();
        }

        [ContextMenu("Apply Look Rig Now")]
        public void ApplyRig()
        {
            EnsureReferences();
            if (_noSnapLayer < 0)
            {
                Debug.LogWarning("[TableNineLookRig] 缺少 NoPixelSnap 层，请先 add_layer。");
                return;
            }

            if (rebindLivingTextCanvases)
            {
                RebindCanvases();
            }

            if (applyCameraStack)
            {
                ApplyStackAndCulling();
            }

            ApplyLookParams();
            if (syncTmpScanlineMaterials)
            {
                SyncTmpMaterials();
            }
        }

        void EnsureReferences()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            _noSnapLayer = LayerMask.NameToLayer(NoSnapLayerName);

            if (uiCamera == null && worldCamera != null)
            {
                var existing = worldCamera.transform.Find("UICamera");
                if (existing != null)
                {
                    uiCamera = existing.GetComponent<Camera>();
                }
            }

            if (uiCamera == null && worldCamera != null && applyCameraStack)
            {
                var go = new GameObject("UICamera");
                go.transform.SetParent(worldCamera.transform, false);
                uiCamera = go.AddComponent<Camera>();
                go.AddComponent<UniversalAdditionalCameraData>();
            }
        }

        void RebindCanvases()
        {
            if (livingTextCanvases == null || uiCamera == null)
            {
                return;
            }

            foreach (var canvas in livingTextCanvases)
            {
                if (canvas == null)
                {
                    continue;
                }

                SetLayerRecursive(canvas.gameObject, _noSnapLayer);
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = uiCamera;
                canvas.planeDistance = 1f;

                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = pixelResolution;
                    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    scaler.matchWidthOrHeight = 1f;
                }
            }
        }

        void ApplyStackAndCulling()
        {
            if (worldCamera == null || uiCamera == null)
            {
                return;
            }

            var noSnapMask = 1 << _noSnapLayer;
            worldCamera.cullingMask &= ~noSnapMask;

            uiCamera.clearFlags = CameraClearFlags.Nothing;
            uiCamera.cullingMask = noSnapMask;
            uiCamera.orthographic = worldCamera.orthographic;
            uiCamera.orthographicSize = worldCamera.orthographicSize;
            uiCamera.nearClipPlane = worldCamera.nearClipPlane;
            uiCamera.farClipPlane = worldCamera.farClipPlane;
            uiCamera.depth = worldCamera.depth + 1f;
            uiCamera.allowHDR = worldCamera.allowHDR;
            uiCamera.allowMSAA = worldCamera.allowMSAA;

            var worldData = worldCamera.GetComponent<UniversalAdditionalCameraData>();
            var uiData = uiCamera.GetComponent<UniversalAdditionalCameraData>();
            if (worldData == null || uiData == null)
            {
                return;
            }

            uiData.renderType = CameraRenderType.Overlay;
            uiData.renderPostProcessing = false;
            uiData.renderShadows = false;

            if (uiCamera.GetComponent<TableNineFinalScanlineMarker>() == null)
            {
                uiCamera.gameObject.AddComponent<TableNineFinalScanlineMarker>();
            }

            if (!worldData.cameraStack.Contains(uiCamera))
            {
                worldData.cameraStack.Add(uiCamera);
            }
        }

        void ApplyLookParams()
        {
            ApplyToMaterial(lookMaterial);
            ApplyToMaterial(legacyPixelSnapMaterial);
        }

        void ApplyToMaterial(Material mat)
        {
            if (mat == null)
            {
                return;
            }

            if (mat.HasProperty("_PixelResolution"))
            {
                mat.SetVector("_PixelResolution", new Vector4(pixelResolution.x, pixelResolution.y, 0f, 0f));
            }

            if (mat.HasProperty("_PixelSnap"))
            {
                mat.SetFloat("_PixelSnap", pixelSnap);
            }

            if (mat.HasProperty("_ScanlineEnabled"))
            {
                mat.SetFloat("_ScanlineEnabled", scanlineEnabled);
            }

            if (mat.HasProperty("_ScanlineIntensity"))
            {
                mat.SetFloat("_ScanlineIntensity", scanlineIntensity);
            }

            if (mat.HasProperty("_ScanlineSpacing"))
            {
                mat.SetFloat("_ScanlineSpacing", scanlineSpacing);
            }
        }

        void SyncTmpMaterials()
        {
            if (livingTextCanvases == null)
            {
                return;
            }

            foreach (var canvas in livingTextCanvases)
            {
                if (canvas == null)
                {
                    continue;
                }

                var tmps = canvas.GetComponentsInChildren<TMP_Text>(true);
                foreach (var tmp in tmps)
                {
                    var mat = tmp.fontSharedMaterial;
                    if (mat == null)
                    {
                        continue;
                    }

                    if (mat.HasProperty("_PixelResolution"))
                    {
                        mat.SetVector("_PixelResolution", new Vector4(pixelResolution.x, pixelResolution.y, 0f, 0f));
                    }

                    // Unified post-stack blit owns scanlines; keep TMP materials off to avoid double.
                    if (mat.HasProperty("_ScanlineEnabled"))
                    {
                        mat.SetFloat("_ScanlineEnabled", useUnifiedScanline ? 0f : scanlineEnabled);
                    }

                    if (mat.HasProperty("_ScanlineIntensity"))
                    {
                        mat.SetFloat("_ScanlineIntensity", scanlineIntensity);
                    }

                    if (mat.HasProperty("_ScanlineSpacing"))
                    {
                        mat.SetFloat("_ScanlineSpacing", scanlineSpacing);
                    }
                }
            }
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            var t = go.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                SetLayerRecursive(t.GetChild(i).gameObject, layer);
            }
        }
    }
}
