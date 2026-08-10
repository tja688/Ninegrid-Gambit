namespace NineGrid.Content.Vfx
{
  public readonly struct VfxStateRequest
  {
    public VfxStateRequest(
        string stateId,
        string diagnosticSource,
        string cardDefId,
        string skillId,
        string roomId,
        string itemDefId,
        string contentId,
        int diagnosticOwnerUid = 0)
    {
      StateId = stateId ?? string.Empty;
      DiagnosticSource = diagnosticSource ?? string.Empty;
      CardDefId = cardDefId ?? string.Empty;
      SkillId = skillId ?? string.Empty;
      RoomId = roomId ?? string.Empty;
      ItemDefId = itemDefId ?? string.Empty;
      ContentId = contentId ?? string.Empty;
      DiagnosticOwnerUid = diagnosticOwnerUid > 0 ? diagnosticOwnerUid : 0;
    }

    public string StateId { get; }
    public string DiagnosticSource { get; }
    public string CardDefId { get; }
    public string SkillId { get; }
    public string RoomId { get; }
    public string ItemDefId { get; }
    public string ContentId { get; }
    public int DiagnosticOwnerUid { get; }
  }
}
