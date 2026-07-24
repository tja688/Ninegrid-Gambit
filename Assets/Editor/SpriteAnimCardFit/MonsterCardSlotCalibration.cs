#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 卡面合法怪物显示区标定：把抽象比对线框（slotW×slotH px）对齐到真实怪物卡上的世界矩形。
/// </summary>
[Serializable]
public sealed class MonsterCardSlotCalibration
{
    public const string PrefsKey = "TableNine.MonsterCardSlotCalibration.V1";

    /// <summary>比对/catalog 用的像素宽（默认 45）。</summary>
    public int slotW = 45;

    /// <summary>比对/catalog 用的像素高（默认 66）。</summary>
    public int slotH = 66;

    /// <summary>真实卡面上合法区世界宽度（默认贴近「背景」0.45）。</summary>
    public float worldW = 0.45f;

    /// <summary>真实卡面上合法区世界高度（默认贴近「背景」0.66）。</summary>
    public float worldH = 0.66f;

    /// <summary>相对 Face 根（或标定参考点）的中心偏移；Y 向上。</summary>
    public float offsetX;

    /// <summary>相对 Face 根的中心偏移；怪物卡「背景」默认约 +0.252。</summary>
    public float offsetY = 0.252f;

    public float slackPx = 4f;

    public static MonsterCardSlotCalibration CreateDefault()
    {
        return new MonsterCardSlotCalibration();
    }

    public Bounds ToWorldBounds(Transform faceRoot)
    {
        if (faceRoot == null)
            return new Bounds(new Vector3(offsetX, offsetY, 0f), new Vector3(worldW, worldH, 0.01f));

        Vector3 center = faceRoot.TransformPoint(new Vector3(offsetX, offsetY, 0f));
        Vector3 size = faceRoot.TransformVector(new Vector3(Mathf.Max(0.01f, worldW), Mathf.Max(0.01f, worldH), 0.01f));
        return new Bounds(center, new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), 0.01f));
    }

    public void SnapFromRenderer(SpriteRenderer renderer, Transform faceRoot)
    {
        if (renderer == null || faceRoot == null)
            return;

        Bounds b = renderer.bounds;
        Vector3 localCenter = faceRoot.InverseTransformPoint(b.center);
        offsetX = localCenter.x;
        offsetY = localCenter.y;
        worldW = Mathf.Max(0.01f, b.size.x);
        worldH = Mathf.Max(0.01f, b.size.y);

        if (renderer.sprite != null)
        {
            var rect = renderer.sprite.rect;
            if (rect.width > 1f && rect.height > 1f)
            {
                slotW = Mathf.RoundToInt(rect.width);
                slotH = Mathf.RoundToInt(rect.height);
            }
        }
    }

    public static MonsterCardSlotCalibration Load()
    {
        string json = EditorPrefs.GetString(PrefsKey, string.Empty);
        if (string.IsNullOrEmpty(json))
            return CreateDefault();

        try
        {
            var c = JsonUtility.FromJson<MonsterCardSlotCalibration>(json);
            return c ?? CreateDefault();
        }
        catch
        {
            return CreateDefault();
        }
    }

    public void Save()
    {
        EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(this));
    }
}
#endif
