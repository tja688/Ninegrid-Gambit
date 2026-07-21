using Unity.Pipeline.HotReload;
using UnityEngine;
using NineGrid.TemporaryTest;

/// <summary>
/// Hot-reload override file — compiled only by reload_file_override (keep outside Assets asmdefs).
/// Do not redeclare StartRunHoverScale.
/// </summary>
public static class StartRunHoverScaleOverrides
{
    [HotReloadOverrideMethod("StartRunHoverScale.OnMouseEnter")]
    public static void TweakedOnMouseEnter(StartRunHoverScale instance)
    {
        instance.hovering = true;
        var factor = 2.0f;
        instance.transform.localScale = instance.baseScale * factor;
        Debug.Log("[StartRunHoverScale] Enter OVERRIDE v3 factor=" + factor);
    }

    [HotReloadOverrideMethod("StartRunHoverScale.OnMouseExit")]
    public static void TweakedOnMouseExit(StartRunHoverScale instance)
    {
        instance.hovering = false;
        instance.transform.localScale = instance.baseScale;
        Debug.Log("[StartRunHoverScale] Exit OVERRIDE v2");
    }
}
