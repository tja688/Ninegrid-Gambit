using System;
using System.Collections.Generic;
using NineGrid.Core;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.FSM
{
    /// <summary>
    /// 道具 Drag 与场地 FSM 的薄协调层：广播 Drag 生命周期，门控场地常态点击。
    /// 临时路由：HandcardApplyZone 落点 + 场地点选目标（待 ItemTargetingSession 替换）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardItemInteractionCoordinator : MonoBehaviour, IController
    {
        public event Action<int> ItemDragBegan;
        public event Action ItemDragEnded;
        public event Action ApplyZoneSessionEnded;

        private readonly List<int> applyZoneSelectedTargets = new();

        private int applyZoneItemUid;
        private ItemUseProfile applyZoneProfile;
        private bool applyZoneSessionActive;

        public bool IsItemDragActive { get; private set; }
        public bool IsApplyZoneTargetingActive => applyZoneSessionActive;
        public int ApplyZoneItemUid => applyZoneItemUid;
        public ItemUseProfile ApplyZoneProfile => applyZoneProfile;
        public IReadOnlyList<int> ApplyZoneSelectedTargets => applyZoneSelectedTargets;

        public IArchitecture GetArchitecture()
        {
            return NineGridArchitecture.Interface;
        }

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

        public void BeginApplyZoneTargeting(int itemUid, ItemUseProfile profile)
        {
            applyZoneItemUid = itemUid;
            applyZoneProfile = profile;
            applyZoneSelectedTargets.Clear();
            applyZoneSessionActive = itemUid > 0;
        }

        public void CancelApplyZoneTargeting()
        {
            if (!applyZoneSessionActive)
            {
                return;
            }

            applyZoneItemUid = 0;
            applyZoneProfile = default;
            applyZoneSelectedTargets.Clear();
            applyZoneSessionActive = false;
            ApplyZoneSessionEnded?.Invoke();
        }

        public bool TryAddApplyZoneTarget(int targetUid, out bool readyToConfirm)
        {
            readyToConfirm = false;
            if (!applyZoneSessionActive || targetUid <= 0)
            {
                return false;
            }

            if (!ItemApplyZoneTargetRules.IsValidTarget(
                    GetArchitecture(),
                    applyZoneProfile,
                    targetUid,
                    applyZoneSelectedTargets))
            {
                return false;
            }

            applyZoneSelectedTargets.Add(targetUid);
            int required = Mathf.Max(1, applyZoneProfile.RequiredCount);
            readyToConfirm = applyZoneSelectedTargets.Count >= required;
            return true;
        }

        public void EndApplyZoneTargeting()
        {
            if (!applyZoneSessionActive)
            {
                return;
            }

            applyZoneItemUid = 0;
            applyZoneProfile = default;
            applyZoneSelectedTargets.Clear();
            applyZoneSessionActive = false;
            ApplyZoneSessionEnded?.Invoke();
        }
    }
}
