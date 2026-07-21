var loop = UnityEngine.Object.FindFirstObjectByType<NineGrid.Flow.MainGameLoopManagerSingleton>();
if (loop == null) return "no loop";
loop.ReturnToMainMenu();
return "called ReturnToMainMenu state=" + loop.State;
