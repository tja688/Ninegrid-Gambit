#if UNITY_EDITOR
using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TableNinePixelSnapInstaller
{
    private const string RootFolder = "Assets/Arts/VisualProfiles";
    private const string ShaderPath = RootFolder + "/TableNinePixelSnap.shader";
    private const string TmpSdfShaderPath = RootFolder + "/TableNine_TMP_SDF_Scanline Overlay.shader";
    private const string TmpBitmapShaderPath = RootFolder + "/TableNine_TMP_Bitmap_Scanline Overlay.shader";
    private const string MaterialPath = RootFolder + "/TableNinePixelSnap.mat";
    private const string TmpBitmapMaterialPath = RootFolder + "/TableNineTmpScanlineBitmap.mat";
    private const string TmpSdfMaterialPath = RootFolder + "/TableNineTmpScanlineSdf.mat";
    private const string LegacyTmpMaterialPath = RootFolder + "/TableNineTmpScanline.mat";
    private const string RendererDataPath = "Assets/Settings/Renderer2D.asset";
    private const string FeatureName = "TableNine Pixel Snap Post";
    private const string OverlayCanvasName = "TableNine Overlay UI";
    private const string ControllerTypeName = "NineGrid.Presentation.Visuals.TableNinePixelSnapController, NineGrid.Presentation";
    private const string BinderTypeName = "NineGrid.Presentation.Visuals.TableNineTmpScanlineBinder, NineGrid.Presentation";

    private static readonly Vector2Int InternalResolution = new(480, 270);

    [MenuItem("Tools/TableNine/Install Pixel Snap Post")]
    public static void Install()
    {
        EnsureFolder("Assets/Arts", "VisualProfiles");
        AssetDatabase.ImportAsset(ShaderPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(TmpSdfShaderPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(TmpBitmapShaderPath, ImportAssetOptions.ForceUpdate);

        Material postMaterial = EnsurePostMaterial();
        Material tmpBitmapMaterial = EnsureTmpScanlineMaterial(TmpBitmapMaterialPath, TmpBitmapShaderPath, "TableNineTmpScanlineBitmap");
        Material tmpSdfMaterial = EnsureTmpScanlineMaterial(TmpSdfMaterialPath, TmpSdfShaderPath, "TableNineTmpScanlineSdf");
        EnsureFullScreenFeature(postMaterial);
        EnsureSceneController(postMaterial, tmpBitmapMaterial, tmpSdfMaterial);
        EnsureOverlayTextCanvas(tmpBitmapMaterial, tmpSdfMaterial);
        ConfigureMainCamera();

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("Installed TableNine pixel snap post process with overlay TMP scanlines.");
    }

    [InitializeOnLoadMethod]
    private static void EnsureRendererFeatureActiveOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            TryMigrateRendererFeature();
            TryReactivateRendererFeature(logWhenFixed: true);
        };
    }

    private static void TryMigrateRendererFeature()
    {
        ScriptableRendererData rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererDataPath);
        if (rendererData == null)
            return;

        bool hasLegacyCustomFeature = false;
        foreach (ScriptableRendererFeature rendererFeature in rendererData.rendererFeatures)
        {
            if (rendererFeature == null || rendererFeature.name != FeatureName)
                continue;

            if (rendererFeature is not FullScreenPassRendererFeature)
            {
                hasLegacyCustomFeature = true;
                break;
            }
        }

        if (!hasLegacyCustomFeature)
            return;

        Material postMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (postMaterial == null)
            return;

        EnsureFullScreenFeature(postMaterial);
        AssetDatabase.SaveAssets();
        Debug.Log("Migrated TableNine pixel snap renderer feature back to FullScreenPass.");
    }

    private static bool TryReactivateRendererFeature(bool logWhenFixed)
    {
        ScriptableRendererData rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererDataPath);
        if (rendererData == null)
            return false;

        foreach (ScriptableRendererFeature rendererFeature in rendererData.rendererFeatures)
        {
            if (rendererFeature == null || rendererFeature.name != FeatureName)
                continue;

            if (rendererFeature.isActive)
                return false;

            rendererFeature.SetActive(true);
            EditorUtility.SetDirty(rendererFeature);
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();

            if (logWhenFixed)
            {
                Debug.LogWarning("Re-enabled inactive TableNine pixel snap renderer feature after editor load.");
            }

            return true;
        }

        return false;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static Material EnsurePostMaterial()
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null)
        {
            throw new InvalidOperationException("Missing shader at " + ShaderPath);
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader)
            {
                name = "TableNinePixelSnap"
            };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        ApplyPostMaterialDefaults(material, shader);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material EnsureTmpScanlineMaterial(string materialPath, string shaderPath, string materialName)
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        if (shader == null)
        {
            throw new InvalidOperationException("Missing shader at " + shaderPath);
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null && materialPath == TmpBitmapMaterialPath)
        {
            Material legacyMaterial = AssetDatabase.LoadAssetAtPath<Material>(LegacyTmpMaterialPath);
            if (legacyMaterial != null)
            {
                AssetDatabase.CopyAsset(LegacyTmpMaterialPath, materialPath);
                material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            }
        }

        if (material == null)
        {
            material = new Material(shader)
            {
                name = materialName
            };
            AssetDatabase.CreateAsset(material, materialPath);
        }

        material.shader = shader;
        ApplyScanlineMaterialDefaults(material);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ApplyPostMaterialDefaults(Material material, Shader shader)
    {
        material.shader = shader;
        ApplyScanlineMaterialDefaults(material);
        material.SetFloat("_PixelSnap", 1f);
    }

    private static void ApplyScanlineMaterialDefaults(Material material)
    {
        material.SetVector("_PixelResolution", new Vector4(InternalResolution.x, InternalResolution.y, 0f, 0f));
        material.SetFloat("_ScanlineEnabled", 1f);
        material.SetFloat("_ScanlineIntensity", 0.12f);
        material.SetFloat("_ScanlineSpacing", 2f);
    }

    private static void EnsureFullScreenFeature(Material material)
    {
        ScriptableRendererData rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererDataPath);
        if (rendererData == null)
        {
            Debug.LogWarning("Renderer data not found at " + RendererDataPath);
            return;
        }

        FullScreenPassRendererFeature feature = null;
        for (int i = rendererData.rendererFeatures.Count - 1; i >= 0; i--)
        {
            ScriptableRendererFeature rendererFeature = rendererData.rendererFeatures[i];
            if (rendererFeature == null)
            {
                rendererData.rendererFeatures.RemoveAt(i);
                continue;
            }

            if (rendererFeature.name != FeatureName)
                continue;

            if (rendererFeature is FullScreenPassRendererFeature fullScreenFeature)
            {
                if (feature == null)
                {
                    feature = fullScreenFeature;
                    continue;
                }

                rendererData.rendererFeatures.RemoveAt(i);
                ScriptableObject.DestroyImmediate(rendererFeature, true);
                continue;
            }

            rendererData.rendererFeatures.RemoveAt(i);
            ScriptableObject.DestroyImmediate(rendererFeature, true);
        }

        if (feature == null)
        {
            feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            feature.name = FeatureName;
            AssetDatabase.AddObjectToAsset(feature, rendererData);
            rendererData.rendererFeatures.Add(feature);
        }

        feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
        feature.requirements = ScriptableRenderPassInput.None;
        feature.fetchColorBuffer = true;
        feature.bindDepthStencilAttachment = false;
        feature.passMaterial = material;
        feature.passIndex = 0;
        feature.SetActive(true);
        feature.Create();

        EditorUtility.SetDirty(feature);
        EditorUtility.SetDirty(rendererData);
    }

    private static void EnsureSceneController(Material postMaterial, Material tmpBitmapMaterial, Material tmpSdfMaterial)
    {
        Type controllerType = Type.GetType(ControllerTypeName);
        if (controllerType == null)
        {
            Debug.LogWarning("Controller type not compiled yet: " + ControllerTypeName);
            return;
        }

        GameObject controllerObject = GameObject.Find("TableNine Post Processing");
        if (controllerObject == null)
        {
            controllerObject = GameObject.Find("TableNine Pixel Snap");
        }

        if (controllerObject == null)
        {
            controllerObject = new GameObject("TableNine Post Processing");
            Undo.RegisterCreatedObjectUndo(controllerObject, "Create TableNine Post Processing");
        }

        Component controller = controllerObject.GetComponent(controllerType);
        if (controller == null)
        {
            controller = controllerObject.AddComponent(controllerType);
        }

        SerializedObject serializedObject = new SerializedObject(controller);
        SetObject(serializedObject, "postProcessMaterial", postMaterial);
        SetObject(serializedObject, "tmpScanlineMaterial", tmpBitmapMaterial);
        SetObject(serializedObject, "tmpBitmapScanlineMaterial", tmpBitmapMaterial);
        SetObject(serializedObject, "tmpSdfScanlineMaterial", tmpSdfMaterial);
        SetVector2Int(serializedObject, "internalResolution", InternalResolution);
        SetBool(serializedObject, "pixelSnap", true);
        SetBool(serializedObject, "scanlines", true);
        SetFloat(serializedObject, "scanlineIntensity", 0.12f);
        SetFloat(serializedObject, "scanlineSpacing", 2f);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        controller.SendMessage("ApplyNow", SendMessageOptions.DontRequireReceiver);
    }

    private static void EnsureOverlayTextCanvas(Material tmpBitmapMaterial, Material tmpSdfMaterial)
    {
        GameObject canvasObject = GameObject.Find(OverlayCanvasName);
        if (canvasObject == null)
        {
            canvasObject = new GameObject(OverlayCanvasName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create TableNine Overlay UI");

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(InternalResolution.x, InternalResolution.y);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();
        }
        else
        {
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.pixelPerfect = false;
            }
        }

        Type binderType = Type.GetType(BinderTypeName);
        Type controllerType = Type.GetType(ControllerTypeName);
        Component controllerComponent = controllerType != null
            ? UnityEngine.Object.FindFirstObjectByType(controllerType) as Component
            : null;

        TextMeshProUGUI[] overlayTexts = canvasObject.GetComponentsInChildren<TextMeshProUGUI>(true);
        TMP_FontAsset defaultFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/Arts/Fronts/正格点黑16_1.0.0/ZhengGeDianHei-16.asset");
        if (defaultFont == null)
        {
            defaultFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        }

        if (overlayTexts.Length == 0)
        {
            GameObject textObject = new GameObject("Overlay Text", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(textObject, "Create TableNine Overlay Text");
            textObject.transform.SetParent(canvasObject.transform, false);

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            if (defaultFont != null)
            {
                text.font = defaultFont;
            }

            text.text = "TableNine Overlay Text";
            text.fontSize = 32;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;

            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = new Vector2(640f, 240f);

            overlayTexts = new[] { text };
        }

        foreach (TextMeshProUGUI text in overlayTexts)
        {
            if (text == null)
                continue;

            if (text.font == null && defaultFont != null)
            {
                text.font = defaultFont;
            }

            if (binderType != null)
            {
                Component binder = text.GetComponent(binderType);
                if (binder == null)
                {
                    binder = text.gameObject.AddComponent(binderType);
                }

                SerializedObject serializedBinder = new SerializedObject(binder);
                if (controllerComponent != null)
                {
                    SetObject(serializedBinder, "controller", controllerComponent);
                }

                SetObject(serializedBinder, "bitmapScanlinePreset", tmpBitmapMaterial);
                SetObject(serializedBinder, "sdfScanlinePreset", tmpSdfMaterial);
                serializedBinder.ApplyModifiedPropertiesWithoutUndo();
                binder.SendMessage("RebindNow", SendMessageOptions.DontRequireReceiver);
            }
            else
            {
                Material preset = tmpBitmapMaterial;
                if (text.font != null && text.font.material != null && text.font.material.shader != null
                    && !text.font.material.shader.name.Contains("Bitmap"))
                {
                    preset = tmpSdfMaterial;
                }

                text.fontSharedMaterial = preset;
            }

            text.isOrthographic = true;
            EditorUtility.SetDirty(text);
        }

        EditorUtility.SetDirty(canvasObject);
    }

    private static void ConfigureMainCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;

        Component[] components = camera.GetComponents<Component>();
        foreach (Component component in components)
        {
            if (component == null || component.GetType().Name != "PixelPerfectCamera")
                continue;

            SerializedObject serializedObject = new SerializedObject(component);
            SetInt(serializedObject, "m_AssetsPPU", 32);
            SetInt(serializedObject, "m_RefResolutionX", InternalResolution.x);
            SetInt(serializedObject, "m_RefResolutionY", InternalResolution.y);
            SetBool(serializedObject, "m_UpscaleRT", true);
            SetBool(serializedObject, "m_PixelSnapping", false);
            SetBool(serializedObject, "m_CropFrameX", false);
            SetBool(serializedObject, "m_CropFrameY", false);
            SetBool(serializedObject, "m_StretchFill", false);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(component);
        }

        EditorUtility.SetDirty(camera);
    }

    private static void SetObject(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = value;
        }
    }

    private static void SetInt(SerializedObject serializedObject, string propertyName, int value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.intValue = value;
        }
    }

    private static void SetVector2Int(SerializedObject serializedObject, string propertyName, Vector2Int value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.vector2IntValue = value;
        }
    }

    private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
        }
    }
}
#endif
