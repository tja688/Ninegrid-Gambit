using UnityEngine;

namespace NineGrid.DevTest.Cards
{
    /// <summary>
    /// 卡牌底盘预制体的 DevTest 运行时安装宿主（#126）：Editor / Development Build 下为每个
    /// 实例补装 StandardCardViewDevKeys；Release 下为空壳。预制体不再序列化 #if 专属类型，
    /// 避免 Release 生成的卡牌实例产生 Missing Script。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DevTestStandardCardInstaller : MonoBehaviour
    {
        private void Awake()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (GetComponent<StandardCardViewDevKeys>() == null)
            {
                gameObject.AddComponent<StandardCardViewDevKeys>();
            }
#endif
        }
    }
}
