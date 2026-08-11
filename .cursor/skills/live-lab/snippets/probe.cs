// LiveLab 探针：会话开局先跑一遍。只读，不改状态。
// 用法：整文件内容作为 execute_code 的 code 参数直接执行。
var sb = new System.Text.StringBuilder();
sb.AppendLine("time=" + DateTime.Now.ToString("HH:mm:ss"));
sb.AppendLine("isPlaying=" + EditorApplication.isPlaying + " paused=" + EditorApplication.isPaused);
sb.AppendLine("scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

// 残留 LiveLab 钩子（上次会话没卸干净就会出现在这里）
var upd = EditorApplication.update;
var hooks = new List<string>();
if (upd != null)
{
    foreach (var d in upd.GetInvocationList())
    {
        if (d.Method.Name.Contains("LiveLab_")) hooks.Add(d.Method.Name);
    }
}
sb.AppendLine("hooks=" + (hooks.Count == 0 ? "(none)" : string.Join(", ", hooks)));

// 跨片段登记表（detour 句柄等）
var reg = AppDomain.CurrentDomain.GetData("LiveLab.Registry") as Dictionary<string, object>;
sb.AppendLine("registry=" + (reg == null || reg.Count == 0 ? "(empty)" : string.Join(", ", reg.Keys)));

// 缝可用性
sb.AppendLine("monomod=" + (Type.GetType("MonoMod.RuntimeDetour.Hook, Locus.Detour") != null));
if (EditorApplication.isPlaying)
{
    var arch = NineGrid.Core.NineGridArchitecture.Interface;
    var audio = arch != null ? arch.GetSystem<NineGrid.Presentation.Systems.IAudioSystem>() : null;
    sb.AppendLine("audioSystem=" + (audio != null));
}
else
{
    sb.AppendLine("audioSystem=(need Play)");
}
return sb.ToString();
