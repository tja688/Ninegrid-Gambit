namespace NineGrid.Content.Vfx
{
  public static class VfxBindingKey
  {
    private const char Separator = '\u001f';

    public static string Compose(
        string identityId,
        string selectorCardDefId,
        string selectorSkillId,
        string selectorRoomId,
        string selectorItemDefId,
        string selectorContentId)
    {
      return string.Join(
          Separator.ToString(),
          identityId ?? string.Empty,
          selectorCardDefId ?? string.Empty,
          selectorSkillId ?? string.Empty,
          selectorRoomId ?? string.Empty,
          selectorItemDefId ?? string.Empty,
          selectorContentId ?? string.Empty);
    }
  }
}
