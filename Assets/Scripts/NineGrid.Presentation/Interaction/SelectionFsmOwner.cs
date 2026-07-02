using NineGrid.Presentation.Shell;
using UnityEngine;

namespace NineGrid.Presentation.Interaction
{
    /// <summary>
    /// MonoBehaviour 包装，供场景挂载与序列化引用 <see cref="SelectionFsm"/>。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SelectionFsmOwner : MonoBehaviour
    {
        private SelectionFsm fsm;

        public SelectionFsm Fsm => fsm;

        public void Bind(SelectionFsm selectionFsm)
        {
            fsm = selectionFsm;
        }
    }
}
