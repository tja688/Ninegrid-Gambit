#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NineGrid.Presentation.Visuals;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class StripSpriteAnimationBuilder
{
    private const string SourceFolder = "Assets/Arts/Images/Multiple";
    private const string ClipFolder = "Assets/Arts/Animations/StripSprites/Clips";
    private const string ControllerFolder = "Assets/Arts/Animations/StripSprites/Controllers";
    private const string CatalogPath = "Assets/Arts/Animations/StripSprites/StripSpriteVisualCatalog.asset";
    private const float DefaultFrameRate = 10f;

    private static readonly Regex StripNamePattern = new Regex(
        @"^(?<prefix>.+)_strip(?<count>\d+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [MenuItem("NineGrid/Build Strip Sprite Animations")]
    public static void BuildFromMenu()
    {
        BuildAll();
    }

    public static StripSpriteVisualCatalog BuildAll()
    {
        EnsureFolder(ClipFolder);
        EnsureFolder(ControllerFolder);

        var catalog = AssetDatabase.LoadAssetAtPath<StripSpriteVisualCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<StripSpriteVisualCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        var generatedEntries = new List<GeneratedEntry>();
        var textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { SourceFolder });

        foreach (var guid in textureGuids)
        {
            var texturePath = AssetDatabase.GUIDToAssetPath(guid);
            var textureName = Path.GetFileNameWithoutExtension(texturePath);
            if (!StripNamePattern.IsMatch(textureName))
            {
                continue;
            }

            var sprites = LoadOrderedSprites(texturePath, textureName);
            if (sprites.Count == 0)
            {
                Debug.LogWarning("[StripSpriteAnimationBuilder] No sprites found for " + texturePath);
                continue;
            }

            var clipPath = ClipFolder + "/" + textureName + "_idle.anim";
            var controllerPath = ControllerFolder + "/" + textureName + ".controller";
            var clip = CreateOrUpdateClip(clipPath, sprites, DefaultFrameRate);
            var controller = CreateOrUpdateController(controllerPath, clip);
            generatedEntries.Add(new GeneratedEntry(textureName, controller, sprites[0]));
        }

        generatedEntries.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));

        var serializedCatalog = new SerializedObject(catalog);
        var entriesProperty = serializedCatalog.FindProperty("entries");
        entriesProperty.ClearArray();
        for (var i = 0; i < generatedEntries.Count; i++)
        {
            entriesProperty.InsertArrayElementAtIndex(i);
            var element = entriesProperty.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("id").stringValue = generatedEntries[i].Id;
            element.FindPropertyRelative("displayName").stringValue = generatedEntries[i].Id;
            element.FindPropertyRelative("animatorController").objectReferenceValue =
                generatedEntries[i].Controller;
            element.FindPropertyRelative("previewSprite").objectReferenceValue =
                generatedEntries[i].Preview;
        }

        serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[StripSpriteAnimationBuilder] Built " + generatedEntries.Count + " strip sprite visuals.");
        return catalog;
    }

    private static List<Sprite> LoadOrderedSprites(string texturePath, string textureName)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
        var sprites = new List<Sprite>();
        for (var i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
            {
                sprites.Add(sprite);
            }
        }

        sprites.Sort((a, b) =>
        {
            var indexA = ParseFrameIndex(a.name, textureName);
            var indexB = ParseFrameIndex(b.name, textureName);
            return indexA.CompareTo(indexB);
        });

        return sprites;
    }

    private static int ParseFrameIndex(string spriteName, string textureName)
    {
        var prefix = textureName + "_";
        if (!spriteName.StartsWith(prefix))
        {
            return int.MaxValue;
        }

        var suffix = spriteName.Substring(prefix.Length);
        int index;
        return int.TryParse(suffix, out index) ? index : int.MaxValue;
    }

    private static AnimationClip CreateOrUpdateClip(string clipPath, IList<Sprite> sprites, float frameRate)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, clipPath);
        }

        clip.frameRate = frameRate;

        var binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite");
        var keyframes = new ObjectReferenceKeyframe[sprites.Count];
        for (var i = 0; i < sprites.Count; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i / frameRate,
                value = sprites[i]
            };
        }

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimatorController CreateOrUpdateController(string controllerPath, AnimationClip clip)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        }

        var stateMachine = controller.layers[0].stateMachine;
        AnimatorState idleState = null;

        for (var i = 0; i < stateMachine.states.Length; i++)
        {
            if (stateMachine.states[i].state.name == "Idle")
            {
                idleState = stateMachine.states[i].state;
                break;
            }
        }

        if (idleState == null)
        {
            idleState = stateMachine.AddState("Idle", new Vector3(300f, 0f, 0f));
        }

        idleState.motion = clip;
        stateMachine.defaultState = idleState;

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        var parts = folderPath.Split('/');
        var current = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private sealed class GeneratedEntry
    {
        public GeneratedEntry(string id, RuntimeAnimatorController controller, Sprite preview)
        {
            Id = id;
            Controller = controller;
            Preview = preview;
        }

        public string Id { get; }
        public RuntimeAnimatorController Controller { get; }
        public Sprite Preview { get; }
    }
}
#endif
