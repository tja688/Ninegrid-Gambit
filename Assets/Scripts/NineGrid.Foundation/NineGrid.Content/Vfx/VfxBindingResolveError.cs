namespace NineGrid.Content.Vfx
{
  public enum VfxBindingResolveCode
  {
    Found = 0,
    NotFound = 1,
    Ambiguous = 2,
    EmptyIdentity = 3,
  }

  public sealed class VfxBindingResolveError
  {
    public VfxBindingResolveCode Code { get; set; }
    public string Message { get; set; }
    public string IdentityId { get; set; }
    public string BindingKey { get; set; }
    public string ConflictingBindingKey { get; set; }
  }
}
