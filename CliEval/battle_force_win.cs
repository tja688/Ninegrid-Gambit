// Cheat: set avatar atk/hp then force node victory.
var battle = UnityEngine.Object.FindFirstObjectByType<NineGrid.Flow.BattleSessionController>();
var loop = UnityEngine.Object.FindFirstObjectByType<NineGrid.Flow.GameFlowController>();
var arch = NineGrid.Core.NineGridArchitecture.Current;
var phaseBefore = arch.GetSystem<NineGrid.Core.Systems.IPhaseSystem>().CurrentPhase.ToString();
var hpOk = battle.TryCheatSetAvatarHp(99);
var atkOk = battle.TryCheatSetAvatarAttack(50);
var winOk = battle.TryCheatForceNodeVictory();
var phaseAfter = arch.GetSystem<NineGrid.Core.Systems.IPhaseSystem>().CurrentPhase.ToString();
var loopAfter = loop != null ? loop.State.ToString() : "null";
return "hpOk=" + hpOk + " atkOk=" + atkOk + " winOk=" + winOk
    + " phase " + phaseBefore + "->" + phaseAfter
    + " loop=" + loopAfter;
