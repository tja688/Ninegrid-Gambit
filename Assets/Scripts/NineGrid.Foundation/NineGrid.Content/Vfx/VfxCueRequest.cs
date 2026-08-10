using System;

namespace NineGrid.Content.Vfx
{
  public readonly struct VfxCueRequest
  {
    public VfxCueRequest(
        string cueId,
        string diagnosticSource,
        string cardDefId,
        string skillId,
        string roomId,
        string itemDefId,
        string contentId,
        int diagnosticCardUid = 0)
    {
      CueId = cueId ?? string.Empty;
      DiagnosticSource = diagnosticSource ?? string.Empty;
      CardDefId = cardDefId ?? string.Empty;
      SkillId = skillId ?? string.Empty;
      RoomId = roomId ?? string.Empty;
      ItemDefId = itemDefId ?? string.Empty;
      ContentId = contentId ?? string.Empty;
      DiagnosticCardUid = diagnosticCardUid > 0 ? diagnosticCardUid : 0;
    }

    public string CueId { get; }
    public string DiagnosticSource { get; }
    public string CardDefId { get; }
    public string SkillId { get; }
    public string RoomId { get; }
    public string ItemDefId { get; }
    public string ContentId { get; }
    public int DiagnosticCardUid { get; }

    public static VfxCueRequest Simple(string cueId, string diagnosticSource)
    {
      return new VfxCueRequest(
          cueId,
          diagnosticSource,
          string.Empty,
          string.Empty,
          string.Empty,
          string.Empty,
          string.Empty);
    }
  }
}
