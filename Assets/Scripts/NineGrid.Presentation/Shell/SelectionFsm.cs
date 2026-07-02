using System;
using UnityEngine;

namespace NineGrid.Presentation.Shell
{
  public enum SelectionState
  {
    Idle = 0,
    Hover,
    Confirming,
  }

  /// <summary>
  /// 外部选择 FSM：idle / hover / confirm；双路由黑盒由 <see cref="SelectionPresentation"/> 承载表演。
  /// General — 通关帮助卡三选一 / 道具选项 / 其他选项；RoomChoice — 房间二选一。
  /// </summary>
  public sealed class SelectionFsm
  {
    public event Action<int> OptionConfirmed;
    public event Action<int> OptionHovered;

    public SelectionChannel ActiveChannel { get; private set; } = SelectionChannel.None;
    public SelectionState State { get; private set; } = SelectionState.Idle;
    public int HoveredIndex { get; private set; } = -1;
    public bool InputLocked { get; set; }

    private SelectionPresentation presentation;

    public void BindPresentation(SelectionPresentation value)
    {
      presentation = value;
    }

    public void ActivateChannel(SelectionChannel channel)
    {
      if (ActiveChannel == channel)
      {
        return;
      }

      ForceReset();
      ActiveChannel = channel;
    }

    public void Deactivate()
    {
      ForceReset();
      ActiveChannel = SelectionChannel.None;
      InputLocked = false;
    }

    public void NotifyHover(int index)
    {
      if (InputLocked || ActiveChannel == SelectionChannel.None || index < 0)
      {
        return;
      }

      if (State == SelectionState.Confirming)
      {
        return;
      }

      HoveredIndex = index;
      State = SelectionState.Hover;
      PlayHover(index);
      OptionHovered?.Invoke(index);
    }

    public void NotifyHoverExit(int index)
    {
      if (InputLocked || ActiveChannel == SelectionChannel.None)
      {
        return;
      }

      if (State != SelectionState.Hover || HoveredIndex != index)
      {
        return;
      }

      HoveredIndex = -1;
      State = SelectionState.Idle;
      PlayReset();
    }

    public void NotifyConfirm(int index)
    {
      if (InputLocked || ActiveChannel == SelectionChannel.None || index < 0)
      {
        return;
      }

      if (State == SelectionState.Confirming)
      {
        return;
      }

      State = SelectionState.Confirming;
      HoveredIndex = index;
      OptionConfirmed?.Invoke(index);
    }

    public void ForceReset()
    {
      HoveredIndex = -1;
      State = SelectionState.Idle;
      PlayReset();
    }

    private void PlayHover(int index)
    {
      if (presentation == null)
      {
        return;
      }

      switch (ActiveChannel)
      {
        case SelectionChannel.General:
          presentation.PlayGeneralHover(index);
          break;
        case SelectionChannel.RoomChoice:
          presentation.PlayRoomHover(index);
          break;
      }
    }

    private void PlayReset()
    {
      if (presentation == null)
      {
        return;
      }

      presentation.PlayGeneralReset();
      presentation.PlayRoomReset();
    }
  }
}
