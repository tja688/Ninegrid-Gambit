using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 遗物/技能图标槽命中代理：挂在槽位节点，由 Manager ApplyDefIds 写入 DefId。
    /// 动态描述 TMP（DescriptionManagerSingleton）已退役；本代理不再写 HUD。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class ContentIconSlotHitProxy : MonoBehaviour
    {
        [Tooltip("运行时由 Relic/Skill Manager 写入；留空表示空槽。")]
        [SerializeField] private string defId;

        public string DefId
        {
            get => defId;
            set => defId = value ?? string.Empty;
        }
    }
}
