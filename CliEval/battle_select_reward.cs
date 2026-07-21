// SelectReward(0) or SkipHelpChoice.
var arch = NineGrid.Core.NineGridArchitecture.Current;
var phase = arch.GetSystem<NineGrid.Core.Systems.IPhaseSystem>();
var before = phase.CurrentPhase.ToString();
var sb = new System.Text.StringBuilder();
sb.Append("before=").Append(before).Append(" legal=");
foreach (var c in phase.LegalCommands) sb.Append(c).Append('|');

if (phase.CanExecute(NineGrid.Core.GameCommandKind.SelectReward))
{
    var result = arch.SendCommand(new NineGrid.Core.Commands.SelectRewardCommand(0));
    sb.Append(" did=SelectReward accepted=").Append(result != null && result.Accepted);
    sb.Append(" reason=").Append(result != null ? result.Reason : "");
}
else if (phase.CanExecute(NineGrid.Core.GameCommandKind.SkipHelpChoice))
{
    var result = arch.SendCommand(new NineGrid.Core.Commands.SkipHelpChoiceCommand());
    sb.Append(" did=SkipHelpChoice accepted=").Append(result != null && result.Accepted);
}
else
{
    sb.Append(" did=NONE");
}
sb.Append(" after=").Append(phase.CurrentPhase);
var loop = UnityEngine.Object.FindFirstObjectByType<NineGrid.Flow.MainGameLoopManagerSingleton>();
sb.Append(" loop=").Append(loop != null ? loop.State.ToString() : "null");
return sb.ToString();
