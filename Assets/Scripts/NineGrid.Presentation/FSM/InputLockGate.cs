using System;
using NineGrid.Core;
using NineGrid.Presentation.Diagnostics;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.FSM
{
    /// <summary>
    /// 镜像 <see cref="IPresentationSyncSystem.IsInputLocked"/>，向交互 FSM 广播 Watching 状态（观演期拒绝一切指针交互，含 Hover）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InputLockGate : MonoBehaviour, IController
    {
        public event Action<bool> OnWatchingChanged;

        public bool IsWatching { get; private set; }

        public IArchitecture GetArchitecture()
        {
            return NineGridArchitecture.Interface;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            bool locked = this.GetSystem<IPresentationSyncSystem>().IsInputLocked;
            if (locked != IsWatching)
            {
                IsWatching = locked;
                PresentationTrace.Log(
                    PresentationTraceChannel.Lock,
                    PresentationTraceLevel.Info,
                    locked ? "WATCHING_ON" : "WATCHING_OFF");
                OnWatchingChanged?.Invoke(IsWatching);
            }
        }

        private void Update()
        {
            bool locked = this.GetSystem<IPresentationSyncSystem>().IsInputLocked;
            if (locked == IsWatching)
            {
                return;
            }

            IsWatching = locked;
            PresentationTrace.Log(
                PresentationTraceChannel.Lock,
                PresentationTraceLevel.Info,
                locked ? "WATCHING_ON" : "WATCHING_OFF");
            OnWatchingChanged?.Invoke(IsWatching);
        }
    }
}
