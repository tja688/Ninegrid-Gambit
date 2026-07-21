var loop = UnityEngine.Object.FindFirstObjectByType<NineGrid.Flow.MainGameLoopManagerSingleton>();
var battle = UnityEngine.Object.FindFirstObjectByType<NineGrid.Flow.InBattleManagerSingleton>();
var hud = NineGrid.Flow.PlayerInfoHudPresenter.TryGetInstance();
var sb = new System.Text.StringBuilder();
sb.Append("loop=").Append(loop!=null).Append(" battle=").Append(battle!=null).Append(" hud=").Append(hud!=null);
if (loop != null) sb.Append(" state=").Append(loop.State);
var arch = NineGrid.Core.NineGridArchitecture.Current;
sb.Append(" arch=").Append(arch!=null);
return sb.ToString();
