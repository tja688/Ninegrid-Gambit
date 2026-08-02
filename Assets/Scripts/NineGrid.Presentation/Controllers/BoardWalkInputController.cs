using System;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Presentation.Commands;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Controllers
{
    /// <summary>
    /// 非战斗跳格输入：经 <see cref="BoardWalkInputHook"/> 承接，发 QF Command。
    /// </summary>
    public sealed class BoardWalkInputController : PresentationController
    {
        private Func<int, bool> mSubmitHandler;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterInstallHook()
        {
            BoardWalkInputHook.WireController = WireToField;
            BoardWalkInputHook.IsEnabled = () =>
            {
                var walk = NineGridArchitecture.Interface?.GetSystem<NineGrid.Flow.Presentation.IAvatarWalkSystem>();
                return walk != null && walk.IsEnabled;
            };
        }

        protected override void OnBind()
        {
            InstallSubmitHandler();
        }

        protected override void OnUnbind()
        {
            ClearSubmitHandler();
        }

        public bool HandleEmptySlotClicked(int groundSlot)
        {
            return this.SendCommand(new SubmitBoardWalkIntentCommand(groundSlot));
        }

        private void InstallSubmitHandler()
        {
            mSubmitHandler = HandleEmptySlotClicked;
            BoardWalkInputHook.TrySubmitBoardWalk = mSubmitHandler;
        }

        private void ClearSubmitHandler()
        {
            if (mSubmitHandler != null && BoardWalkInputHook.TrySubmitBoardWalk == mSubmitHandler)
            {
                BoardWalkInputHook.TrySubmitBoardWalk = null;
            }

            mSubmitHandler = null;
        }

        private static void WireToField(GroundFieldView field)
        {
            if (field == null)
            {
                return;
            }

            var existing = field.GetComponent<BoardWalkInputController>();
            if (existing == null)
            {
                existing = UnityEngine.Object.FindObjectOfType<BoardWalkInputController>();
            }

            if (existing == null)
            {
                var host = new GameObject(nameof(BoardWalkInputController));
                host.transform.SetParent(field.transform, false);
                existing = host.AddComponent<BoardWalkInputController>();
            }

            existing.InstallSubmitHandler();
        }
    }
}
