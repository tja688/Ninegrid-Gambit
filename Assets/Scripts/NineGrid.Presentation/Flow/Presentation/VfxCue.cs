using System;

namespace NineGrid.Flow.Presentation
{
  [Flags]
  public enum VfxCueContexts
  {
    None = 0,
    CardDefId = 1 << 0,
    SkillId = 1 << 1,
    RoomId = 1 << 2,
    ItemDefId = 1 << 3,
    ContentId = 1 << 4,
  }

  [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
  public sealed class VfxCueAttribute : Attribute
  {
    public VfxCueAttribute(
        string cueId,
        string note,
        string module,
        string authoritativeEmitter,
        VfxCueContexts allowedContexts)
    {
      CueId = cueId ?? string.Empty;
      Note = note ?? string.Empty;
      Module = module ?? string.Empty;
      AuthoritativeEmitter = authoritativeEmitter ?? string.Empty;
      AllowedContexts = allowedContexts;
    }

    public string CueId { get; }
    public string Note { get; }
    public string Module { get; }
    public string AuthoritativeEmitter { get; }
    public VfxCueContexts AllowedContexts { get; }
  }

  [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
  public sealed class PersistentVfxStateAttribute : Attribute
  {
    public PersistentVfxStateAttribute(
        string stateId,
        string note,
        string module,
        string authoritativeEmitter,
        VfxCueContexts allowedContexts)
    {
      StateId = stateId ?? string.Empty;
      Note = note ?? string.Empty;
      Module = module ?? string.Empty;
      AuthoritativeEmitter = authoritativeEmitter ?? string.Empty;
      AllowedContexts = allowedContexts;
    }

    public string StateId { get; }
    public string Note { get; }
    public string Module { get; }
    public string AuthoritativeEmitter { get; }
    public VfxCueContexts AllowedContexts { get; }
  }
}
