namespace NineGrid.Presentation.Shell
{
    /// <summary>
    /// 非 Harness 时弱同步 RunModel.Phase → MainFlowScreen（首版占位）。
    /// </summary>
    [UnityEngine.DisallowMultipleComponent]
    public sealed class GamePhaseFlowShellProjection : UnityEngine.MonoBehaviour
    {
        [UnityEngine.SerializeField] private bool enabledProjection;
        [UnityEngine.SerializeField] private MainFlowDirector director;

        public bool EnabledProjection
        {
            get => enabledProjection;
            set => enabledProjection = value;
        }

        public void Bind(MainFlowDirector flowDirector)
        {
            director = flowDirector;
        }
    }
}
