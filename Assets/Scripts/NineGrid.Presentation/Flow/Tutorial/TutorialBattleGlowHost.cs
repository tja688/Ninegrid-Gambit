using UnityEngine;

namespace NineGrid.Flow.Tutorial
{
    /// <summary>教学导演持有的范围荧光请求者（非场景 MonoBehaviour 宿主）。</summary>
    internal sealed class TutorialBattleGlowHost : MonoBehaviour
    {
        private static TutorialBattleGlowHost sInstance;

        public static TutorialBattleGlowHost Ensure()
        {
            if (sInstance != null)
            {
                return sInstance;
            }

            var go = new GameObject(nameof(TutorialBattleGlowHost));
            DontDestroyOnLoad(go);
            sInstance = go.AddComponent<TutorialBattleGlowHost>();
            return sInstance;
        }

        public static void DestroyIfExists()
        {
            if (sInstance == null)
            {
                return;
            }

            var go = sInstance.gameObject;
            sInstance = null;
            if (go != null)
            {
                Destroy(go);
            }
        }

        private void OnDestroy()
        {
            if (sInstance == this)
            {
                sInstance = null;
            }
        }
    }
}
