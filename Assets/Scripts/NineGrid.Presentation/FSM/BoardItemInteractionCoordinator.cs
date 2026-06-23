using System;
using UnityEngine;

namespace NineGrid.Presentation.FSM
{
    /// <summary>
    /// 道具 Drag 与场地 FSM 的薄协调层：广播 Drag 生命周期，门控场地常态点击。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardItemInteractionCoordinator : MonoBehaviour
    {
        public event Action<int> ItemDragBegan;
        public event Action ItemDragEnded;

        public bool IsItemDragActive { get; private set; }

        public void NotifyDragBegan(int itemUid)
        {
            if (IsItemDragActive)
            {
                return;
            }

            IsItemDragActive = true;
            ItemDragBegan?.Invoke(itemUid);
        }

        public void NotifyDragEnded()
        {
            if (!IsItemDragActive)
            {
                return;
            }

            IsItemDragActive = false;
            ItemDragEnded?.Invoke();
        }
    }
}
