using System;
using UnityEngine;

namespace NineGrid.Presentation.Systems.Vfx
{
    public interface IVfxMaterialFrameLoader
    {
        bool TryLoadFrames(string materialKey, out Sprite[] frames, out string failureReason);
    }

    public sealed class DefaultVfxMaterialFrameLoader : IVfxMaterialFrameLoader
    {
        public bool TryLoadFrames(string materialKey, out Sprite[] frames, out string failureReason)
        {
            return NineGrid.Content.Vfx.VfxMaterialFrameSource.TryLoadFrames(
                materialKey,
                out frames,
                out failureReason);
        }
    }
}
