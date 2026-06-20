#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class TableNinePixelSnapInstaller
{
    private const string RootFolder = "Assets/Arts/VisualProfiles";
    private const string ShaderPath = RootFolder + "/TableNinePixelSnap.shader";
    private const string MaterialPath = RootFolder + "/TableNinePixelSnap.mat";
    private const string RendererDataPath = "Assets/Settings/Renderer2D.asset";
    private const string FeatureName = "TableNine Pixel Snap Post";
    private const string ControllerTypeName = "NineGrid.Presentation.Visuals.TableNinePixelSnapController, NineGrid.Presentation";

    [MenuItem("Tools/TableNine/Install Pixel Snap Post")]
    public static void Install()
    {
        EnsureFolder("Assets/Arts", "VisualProfiles");
        AssetDatabase.ImportAsset(ShaderPath, ImportAssetOptions.ForceUpdate);

        Material postMaterial = EnsurePostMaterial();
        EnsureFullScreenFeature(postMaterial);
        EnsureSceneController(postMaterial);
        ConfigureMainCamera();

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("Installed TableNine pixel snap post process.");
    }

    [InitializeOnLoadMethod]
    private static void EnsureRendererFeatureActiveOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            TryReactivateRendererFeature(logWhenFixed: true);
        };
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

        material.shader = shader;
        material.SetVector("_PixelResolution", new Vector4(320f, 180f, 0f, 0f));
        material.SetFloat("_PixelSnap", 1f);
        material.SetFloat("_ScanlineEnabled", 1f);
        material.SetFloat("_ScanlineIntensity", 0.12f);
        material.SetFloat("_ScanlineSpacing", 2f);
        EditorUtility.SetDirty(material);
        return material;
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
        foreach (ScriptableRendererFeature rendererFeature in rendererData.rendererFeatures)
        {
            if (rendererFeature != null && rendererFeature.name == FeatureName)
            {
                feature = rendererFeature as FullScreenPassRendererFeature;
                break;
            }
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

    private static void EnsureSceneController(Material material)
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
        SetObject(serializedObject, "postProcessMaterial", material);
        SetVector2Int(serializedObject, "internalResolution", new Vector2Int(320, 180));
        SetBool(serializedObject, "pixelSnap", true);
        SetBool(serializedObject, "scanlines", true);
        SetFloat(serializedObject, "scanlineIntensity", 0.12f);
        SetFloat(serializedObject, "scanlineSpacing", 2f);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        controller.SendMessage("ApplyNow", SendMessageOptions.DontRequireReceiver);
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
            SetInt(serializedObject, "m_RefResolutionX", 320);
            SetInt(serializedObject, "m_RefResolutionY", 180);
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
