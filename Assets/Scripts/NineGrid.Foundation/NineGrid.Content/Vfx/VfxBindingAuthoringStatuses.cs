using System;

namespace NineGrid.Content.Vfx
{
  public static class VfxBindingAuthoringStatuses
  {
    public const string AiDraft = "aiDraft";
    public const string HumanConfirmed = "humanConfirmed";

    public static bool IsHumanConfirmed(string status)
    {
      return string.Equals(status, HumanConfirmed, StringComparison.Ordinal);
    }

    public static bool IsAiDraft(string status)
    {
      return string.IsNullOrWhiteSpace(status)
          || string.Equals(status, AiDraft, StringComparison.Ordinal);
    }
  }
}
