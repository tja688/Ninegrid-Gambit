using NineGrid.Presentation.Shell;
using UnityEngine;

namespace NineGrid.Presentation.Interaction
{
    /// <summary>
    /// 选择 FSM 场景挂载点，内聚 <see cref="SelectionPresentation"/> 双路由黑盒。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SelectionPresentation))]
    public sealed class SelectionFsmOwner : MonoBehaviour
    {
        private SelectionFsm fsm;
        private SelectionPresentation presentation;

        public SelectionFsm Fsm => fsm;
        public SelectionPresentation Presentation => presentation;

        private void Awake()
        {
            presentation = GetComponent<SelectionPresentation>();
        }

        public void Bind(SelectionFsm selectionFsm)
        {
            fsm = selectionFsm;
            fsm?.BindPresentation(presentation);
        }
    }
}
