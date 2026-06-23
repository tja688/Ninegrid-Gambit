using System;
using NineGrid.Core;
using NineGrid.Presentation.Visuals;
using UnityEngine;

namespace NineGrid.Presentation.Performance
{
  /// <summary>
  /// 玩家面板更新表演占位：当前为即时数值同步，后续替换为 DOTween 动效黑盒。
  /// </summary>
  [DisallowMultipleComponent]
  public sealed class StatusPanelUpdatePerformance : MonoBehaviour
  {
    [SerializeField] private TableNineStatusPanelView panelView;

    public bool IsPlaying => false;

    public float TotalDuration => 0f;

    public void Play(CoreGameEvent evt, CoreViewSnapshot snapshot, Action onComplete = null)
    {
      EnsureReferences();
      if (panelView != null)
      {
        panelView.ApplyEvent(evt, snapshot);
      }

      onComplete?.Invoke();
    }

    public void AlignFromSnapshot(CoreViewSnapshot snapshot)
    {
      EnsureReferences();
      if (panelView != null)
      {
        panelView.ApplySnapshot(snapshot);
      }
    }

    private void EnsureReferences()
    {
      if (panelView == null)
      {
        panelView = GetComponentInChildren<TableNineStatusPanelView>(true);
      }
    }
  }
}
