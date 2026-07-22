namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 时间线换步诊断（非逐帧）。生产接 DirectorTrace；EditMode 可注入录制 sink。
    /// </summary>
    public interface ITimelineDiagnosticSink
    {
        void StepEnter(string step, string lane);

        void StepExit(string step, string lane);
    }
}
