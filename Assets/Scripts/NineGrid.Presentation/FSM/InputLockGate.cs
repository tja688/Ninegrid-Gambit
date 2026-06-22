using System;
using NineGrid.Core;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.FSM
{
    /// <summary>
    /// 镜像 <see cref="IPresentationSyncSystem.IsInputLocked"/>，向交互 FSM 广播 Watching 状态。
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
            OnWatchingChanged?.Invoke(IsWatching);
        }
    }
}
