using NineGrid.Presentation.Shell;
using UnityEngine;

namespace NineGrid.Presentation.Interaction
{
    /// <summary>
    /// 将 Collider 鼠标事件转发给 <see cref="SelectionFsm"/>。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class SelectionOptionInputRelay : MonoBehaviour
    {
        [SerializeField] private int optionIndex;
        [SerializeField] private SelectionFsmOwner selectionOwner;

        public int OptionIndex
        {
            get => optionIndex;
            set => optionIndex = value;
        }

        public SelectionFsmOwner Owner
        {
            get => selectionOwner;
            set => selectionOwner = value;
        }

        private void OnMouseEnter()
        {
            ResolveFsm()?.NotifyHover(optionIndex);
        }

        private void OnMouseExit()
        {
            ResolveFsm()?.NotifyHoverExit(optionIndex);
        }

        private void OnMouseDown()
        {
            ResolveFsm()?.NotifyConfirm(optionIndex);
        }

        private SelectionFsm ResolveFsm()
        {
            if (selectionOwner != null && selectionOwner.Fsm != null)
            {
                return selectionOwner.Fsm;
            }

            return MainFlowDirector.Current?.SelectionFsm;
        }
    }
}
