using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Presentation.Commands;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Controllers
{
    /// <summary>
    /// 房间选择 / 进入 Controller：经 Hook 承接 MainGameLoop，发 QF Command。
    /// </summary>
    public sealed class RoomChoiceInputController : PresentationController
    {
        private System.Func<int, CoreCommandResult> mSelectHandler;
        private System.Func<CoreCommandResult> mEnterHandler;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterInstallHook()
        {
            RoomChoiceCoreHook.WireController = Wire;
            RoomChoiceCoreHook.SelectRoom = null;
            RoomChoiceCoreHook.EnterRoom = null;
        }

        protected override void OnBind()
        {
            InstallHandlers();
        }

        protected override void OnUnbind()
        {
            ClearHandlers();
        }

        public CoreCommandResult HandleSelectRoom(int optionIndex)
        {
            return this.SendCommand(new SubmitSelectRoomCommand(optionIndex));
        }

        public CoreCommandResult HandleEnterRoom()
        {
            return this.SendCommand(new SubmitEnterRoomCommand());
        }

        private void InstallHandlers()
        {
            mSelectHandler = HandleSelectRoom;
            mEnterHandler = HandleEnterRoom;
            RoomChoiceCoreHook.SelectRoom = mSelectHandler;
            RoomChoiceCoreHook.EnterRoom = mEnterHandler;
        }

        private void ClearHandlers()
        {
            if (mSelectHandler != null && RoomChoiceCoreHook.SelectRoom == mSelectHandler)
            {
                RoomChoiceCoreHook.SelectRoom = null;
            }

            if (mEnterHandler != null && RoomChoiceCoreHook.EnterRoom == mEnterHandler)
            {
                RoomChoiceCoreHook.EnterRoom = null;
            }

            mSelectHandler = null;
            mEnterHandler = null;
        }

        private static void Wire()
        {
            var existing = Object.FindObjectOfType<RoomChoiceInputController>();
            if (existing == null)
            {
                var host = new GameObject(nameof(RoomChoiceInputController));
                existing = host.AddComponent<RoomChoiceInputController>();
            }

            existing.InstallHandlers();
        }
    }
}
