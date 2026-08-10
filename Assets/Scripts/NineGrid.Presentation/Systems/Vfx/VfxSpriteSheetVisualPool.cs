using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Presentation.Systems.Vfx
{
    internal sealed class VfxSpriteSheetVisualPool
    {
        private readonly Transform mPoolRoot;
        private readonly Stack<VfxSpriteSheetVisual> mIdle = new Stack<VfxSpriteSheetVisual>();

        public VfxSpriteSheetVisualPool()
        {
            var host = new GameObject("VfxSpriteSheetPool");
            Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            mPoolRoot = host.transform;
        }

        public VfxSpriteSheetVisual Acquire()
        {
            if (mIdle.Count > 0)
            {
                var reused = mIdle.Pop();
                reused.gameObject.SetActive(true);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                VfxSpriteSheetPoolDiagnostics.RecordReuse(reused.name);
#endif
                return reused;
            }

            var go = new GameObject("VfxSpriteSheetInstance");
            go.hideFlags = HideFlags.HideAndDontSave;
            var visual = go.AddComponent<VfxSpriteSheetVisual>();
            visual.EnsureRenderer();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            VfxSpriteSheetPoolDiagnostics.RecordCreated(visual.name);
#endif
            return visual;
        }

        public void Release(VfxSpriteSheetVisual visual)
        {
            if (visual == null)
            {
                return;
            }

            visual.ResetVisual();
            visual.transform.SetParent(mPoolRoot, false);
            visual.gameObject.SetActive(false);
            mIdle.Push(visual);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            VfxSpriteSheetPoolDiagnostics.RecordRelease(visual.name);
#endif
        }

        public void DestroyAll()
        {
            while (mIdle.Count > 0)
            {
                var visual = mIdle.Pop();
                if (visual != null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    VfxSpriteSheetPoolDiagnostics.RecordDestroyed(visual.name);
#endif
                    Object.Destroy(visual.gameObject);
                }
            }
        }
    }
}
