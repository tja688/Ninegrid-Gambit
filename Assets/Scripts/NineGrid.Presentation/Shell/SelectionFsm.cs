using System;
using NineGrid.Presentation.Interaction;
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
  /// 外部选择 FSM：idle / hover / confirm；与主流程解耦。
  /// </summary>
  public sealed class SelectionFsm
  {
    public event Action<int> OptionConfirmed;
    public event Action<int> OptionHovered;

    public SelectionChannel ActiveChannel { get; private set; } = SelectionChannel.None;
    public SelectionState State { get; private set; } = SelectionState.Idle;
    public int HoveredIndex { get; private set; } = -1;
    public bool InputLocked { get; set; }

    private SelectionOptionHoverPresenter generalHoverPresenter;
    private RoomChoiseOptionHoverPresenter roomHoverPresenter;

    public void BindPresenters(
      SelectionOptionHoverPresenter general,
      RoomChoiseOptionHoverPresenter room)
    {
      generalHoverPresenter = general;
      roomHoverPresenter = room;
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
      switch (ActiveChannel)
      {
        case SelectionChannel.General:
          generalHoverPresenter?.PlayHover(index);
          break;
        case SelectionChannel.RoomChoice:
          roomHoverPresenter?.PlayHover(index);
          break;
      }
    }

    private void PlayReset()
    {
      generalHoverPresenter?.PlayReset();
      roomHoverPresenter?.PlayReset();
    }
  }
}
