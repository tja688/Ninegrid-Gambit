namespace NineGrid.Content.Vfx
{
    /// <summary>向附着型特效提供受控视觉域；#197 扩展完整能力协商。</summary>
    public interface IVfxDomainHost
    {
        bool IsAvailable { get; }
    }
}
