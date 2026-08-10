#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;

namespace NineGrid.Presentation.Systems.Vfx
{
    /// <summary>精灵表池 create / reuse / release / destroy 事实；#194 将接入完整诊断。</summary>
    public static class VfxSpriteSheetPoolDiagnostics
    {
        public static int CreatedCount { get; private set; }
        public static int ReuseCount { get; private set; }
        public static int ReleaseCount { get; private set; }
        public static int DestroyedCount { get; private set; }

        private static readonly List<string> sFacts = new List<string>(64);

        public static IReadOnlyList<string> Facts => sFacts;

        public static void RecordCreated(string detail)
        {
            CreatedCount++;
            Append("create", detail);
        }

        public static void RecordReuse(string detail)
        {
            ReuseCount++;
            Append("reuse", detail);
        }

        public static void RecordRelease(string detail)
        {
            ReleaseCount++;
            Append("release", detail);
        }

        public static void RecordDestroyed(string detail)
        {
            DestroyedCount++;
            Append("destroy", detail);
        }

        public static void ResetForTests()
        {
            CreatedCount = 0;
            ReuseCount = 0;
            ReleaseCount = 0;
            DestroyedCount = 0;
            sFacts.Clear();
        }

        private static void Append(string kind, string detail)
        {
            if (sFacts.Count >= 256)
            {
                sFacts.RemoveAt(0);
            }

            sFacts.Add(kind + ":" + detail);
        }
    }
}
#endif
