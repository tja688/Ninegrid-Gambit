using UnityEngine;

namespace NineGrid.Presentation.Interaction
{
    /// <summary>
    /// 卡演员运行时绑定：CardUid 与 Collider 输入中继。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TableNineActorBinding : MonoBehaviour
    {
        [SerializeField] private int cardUid;
        [SerializeField] private bool isHandItem;

        public int CardUid => cardUid;
        public bool IsHandItem => isHandItem;

        public void Bind(int uid, bool handItem = false)
        {
            cardUid = uid;
            isHandItem = handItem;
        }
    }
}
