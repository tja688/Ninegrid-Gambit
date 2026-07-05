#if UNITY_EDITOR
using System.Collections.Generic;
using NineGrid.GameFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 从 Fire Effect and Bullet 16x16 图集中提取右上角爆炸序列，并在 MainScene 放置小键盘7预览。
/// </summary>
public static class FireEffectExplosionSetup
{
    private const string TexturePath = "Assets/Arts/Images/Multiple/Fire Effect and Bullet 16x16.png";
    private const string ScenePath = "Assets/Scenes/MainScene.unity";
    private const string PreviewObjectName = "DebugExplosionFX";

    /// <summary>
    /// 图集右上角爆炸：先横向扩散 (15-19)，再收束消散 (20-23)。
    /// </summary>
    private static readonly int[] ExplosionFrameIndices =
    {
        15, 16, 17, 18, 19, 20, 21, 22, 23,
    };

    [MenuItem("NineGrid/Setup Explosion Debug FX Keypad7")]
    public static void SetupFromMenu()
    {
        SetupInMainScene();
    }

    public static void SetupInMainScene()
    {
        var frames = LoadExplosionFrames();
        if (frames.Count == 0)
        {
            Debug.LogError("[FireEffectExplosionSetup] 未能从图集加载爆炸帧。");
            return;
        }

        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        var preview = FindOrCreatePreviewObject();
        var renderer = preview.GetComponent<SpriteRenderer>();
        var animation = preview.GetComponent<OneShotSpriteAnimation>();

        renderer.sprite = frames[0];
        renderer.sortingOrder = 20;

        var serializedAnimation = new SerializedObject(animation);
        var framesProperty = serializedAnimation.FindProperty("frames");
        framesProperty.arraySize = frames.Count;
        for (var i = 0; i < frames.Count; i++)
        {
            framesProperty.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        }

        serializedAnimation.FindProperty("framesPerSecond").floatValue = 12f;
        serializedAnimation.FindProperty("playKey").intValue = (int)KeyCode.Keypad7;
        serializedAnimation.ApplyModifiedPropertiesWithoutUndo();

        preview.transform.position = new Vector3(0f, 1.5f, 0f);
        preview.transform.localScale = Vector3.one * 3f;
        preview.SetActive(true);

        EditorUtility.SetDirty(preview);
        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(animation);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[FireEffectExplosionSetup] 已在 MainScene 配置 " + PreviewObjectName +
                  "，共 " + frames.Count + " 帧，按小键盘7播放。");
    }

    private static List<Sprite> LoadExplosionFrames()
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath(TexturePath);
        var spriteByIndex = new Dictionary<int, Sprite>();
        const string prefix = "Fire Effect and Bullet 16x16_";

        for (var i = 0; i < assets.Length; i++)
        {
            if (!(assets[i] is Sprite sprite))
            {
                continue;
            }

            if (!sprite.name.StartsWith(prefix))
            {
                continue;
            }

            var suffix = sprite.name.Substring(prefix.Length);
            if (!int.TryParse(suffix, out var index))
            {
                continue;
            }

            spriteByIndex[index] = sprite;
        }

        var frames = new List<Sprite>(ExplosionFrameIndices.Length);
        for (var i = 0; i < ExplosionFrameIndices.Length; i++)
        {
            var index = ExplosionFrameIndices[i];
            if (!spriteByIndex.TryGetValue(index, out var sprite))
            {
                Debug.LogWarning("[FireEffectExplosionSetup] 缺少帧索引 " + index);
                continue;
            }

            frames.Add(sprite);
        }

        return frames;
    }

    private static GameObject FindOrCreatePreviewObject()
    {
        var existing = GameObject.Find(PreviewObjectName);
        if (existing != null)
        {
            EnsureComponents(existing);
            return existing;
        }

        var created = new GameObject(PreviewObjectName);
        EnsureComponents(created);
        return created;
    }

    private static void EnsureComponents(GameObject target)
    {
        if (target.GetComponent<SpriteRenderer>() == null)
        {
            target.AddComponent<SpriteRenderer>();
        }

        if (target.GetComponent<OneShotSpriteAnimation>() == null)
        {
            target.AddComponent<OneShotSpriteAnimation>();
        }
    }
}
#endif
