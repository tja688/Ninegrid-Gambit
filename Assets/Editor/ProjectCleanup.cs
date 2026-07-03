#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ProjectCleanup
{
    private static readonly string[] RootsToRemove =
    {
        "Actors",
        "Anchors",
        "Directors",
        "Panels",
        "CameraUICanvas",
        "TableNine Text Overlay UI",
        "TableNine Overlay UI"
    };

    [MenuItem("Tools/Project/Cleanup MainScene")]
    public static void CleanupMainScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "MainScene")
        {
            Debug.LogWarning("Load MainScene before running cleanup.");
            return;
        }

        foreach (string rootName in RootsToRemove)
        {
            DestroyRoot(rootName);
        }

        EnsureOverlayDemo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("MainScene cleanup complete.");
    }

    private static void DestroyRoot(string name)
    {
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (GameObject root in roots)
        {
            if (root != null && root.name == name)
            {
                Undo.DestroyObjectImmediate(root);
            }
        }
    }

    private static void EnsureOverlayDemo()
    {
        const string canvasName = "TableNine Overlay UI";
        GameObject canvasObject = GameObject.Find(canvasName);
        if (canvasObject == null)
        {
            canvasObject = new GameObject(canvasName, typeof(RectTransform));
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(480f, 270f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();
        }

        Transform existing = canvasObject.transform.Find("Overlay Text");
        if (existing != null)
            return;

        GameObject textObject = new GameObject("Overlay Text", typeof(RectTransform));
        textObject.transform.SetParent(canvasObject.transform, false);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/Arts/Fronts/正格点黑16_1.0.0/ZhengGeDianHei-16.asset");
        if (font == null)
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        }

        if (font != null)
            text.font = font;

        text.text = "Pixel Visual Demo";
        text.fontSize = 32;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(640f, 120f);

        System.Type binderType = System.Type.GetType(
            "NineGrid.Presentation.Visuals.TableNineTmpScanlineBinder, PixelVisuals");
        if (binderType != null)
        {
            Component binder = textObject.AddComponent(binderType);
            System.Type controllerType = System.Type.GetType(
                "NineGrid.Presentation.Visuals.TableNinePixelSnapController, PixelVisuals");
            Component controller = controllerType != null
                ? Object.FindFirstObjectByType(controllerType) as Component
                : null;

            Material bitmap = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Arts/VisualProfiles/TableNineTmpScanlineBitmap.mat");
            Material sdf = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Arts/VisualProfiles/TableNineTmpScanlineSdf.mat");

            SerializedObject so = new SerializedObject(binder);
            SetRef(so, "controller", controller);
            SetRef(so, "bitmapScanlinePreset", bitmap);
            SetRef(so, "sdfScanlinePreset", sdf);
            so.ApplyModifiedPropertiesWithoutUndo();
            binder.SendMessage("RebindNow", SendMessageOptions.DontRequireReceiver);
        }
    }

    private static void SetRef(SerializedObject so, string propertyName, Object value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }
}
#endif
