using UnityEngine;

namespace NineGrid.DevTest.Flow
{
    /// <summary>
    /// MainScene 的 DevTest 组件运行时安装宿主（#126）：仅在 Editor / Development Build 下把
    /// 按键模块与 QuickTest 入口 AddComponent 到导演宿主上；Release 下为空壳。
    /// 场景不再序列化任何 #if UNITY_EDITOR || DEVELOPMENT_BUILD 专属类型，从而避免
    /// Release Player 产生 Missing Script。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DevTestSceneInstaller : MonoBehaviour
    {
        [Header("DevTest 安装目标（仅 Editor / Development Build 生效）")]
        [SerializeField] private GameObject cardDeckDirector;
        [SerializeField] private GameObject groundFieldManager;
        [SerializeField] private GameObject cardHandDirector;
        [SerializeField] private GameObject inBattleDirector;
        [SerializeField] private GameObject selectorManager;
        [SerializeField] private GameObject mainGameLoopDirector;

        private void Awake()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Install<NineGrid.DevTest.Cards.CardDeckManagerDevKeys>(cardDeckDirector);
            Install<NineGrid.DevTest.Cards.GroundFieldManagerDevKeys>(groundFieldManager);
            Install<NineGrid.DevTest.Cards.CardHandManagerDevKeys>(cardHandDirector);
            Install<Flow.InBattleManagerDevKeys>(inBattleDirector);
            Install<Flow.DamageNumberManagerDevKeys>(inBattleDirector);
            Install<Flow.GoldGainFxManagerDevKeys>(inBattleDirector);
            Install<Flow.SelectorManagerDevKeys>(selectorManager);
            Install<Flow.MainGameLoopManagerDevKeys>(mainGameLoopDirector);
            Install<QuickTestEntryInputHandler>(mainGameLoopDirector);
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void Install<T>(GameObject target) where T : Component
        {
            if (target == null || target.GetComponent<T>() != null)
            {
                return;
            }

            target.AddComponent<T>();
        }
#endif
    }
}
