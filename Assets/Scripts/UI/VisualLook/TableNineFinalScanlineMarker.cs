using UnityEngine;

namespace NineGrid.VisualLook
{
    /// <summary>
    /// Marks the Overlay camera that should run the final full-frame scanline blit
    /// after the camera stack composites world + NoPixelSnap text.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TableNineFinalScanlineMarker : MonoBehaviour
    {
    }
}
