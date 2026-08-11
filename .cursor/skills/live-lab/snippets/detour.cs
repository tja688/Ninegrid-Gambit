// LiveLab 方法重定向模板（通道 3，最深）：MonoMod detour 改写现有方法行为。
// 先跑 probe.cs 确认 monomod=True。示例把 AudioSystem.RequestCue 包一层日志；
// 实际使用时替换目标方法与替身签名，并把 RequestCueDemo 换成唯一 marker。
// 卸载：hook-uninstall.cs 会 Dispose 登记表对象即还原原方法。
string key = "LiveLab_RequestCueDemo_detour";

var reg = AppDomain.CurrentDomain.GetData("LiveLab.Registry") as Dictionary<string, object>;
if (reg == null)
{
    reg = new Dictionary<string, object>();
    AppDomain.CurrentDomain.SetData("LiveLab.Registry", reg);
}
object old;
if (reg.TryGetValue(key, out old))
{
    var d = old as IDisposable;
    if (d != null) d.Dispose();
    reg.Remove(key);
}

// 目标方法（实例方法示例）
var target = typeof(NineGrid.Presentation.Systems.AudioSystem).GetMethod(
    "RequestCue", BindingFlags.Instance | BindingFlags.Public);
if (target == null) return "[LiveLab] 目标方法未找到";

// 替身：首参 orig 委托（原实现），第二参 this，其后为原方法参数。
NineGrid.Presentation.Systems.AudioCueResult Replacement(
    Func<NineGrid.Presentation.Systems.AudioSystem,
         NineGrid.Content.Audio.AudioCueRequest,
         NineGrid.Presentation.Systems.AudioCueResult> orig,
    NineGrid.Presentation.Systems.AudioSystem self,
    NineGrid.Content.Audio.AudioCueRequest request)
{
    Debug.Log("[LiveLab] RequestCue: " + request.CueId + " from " + request.DiagnosticSource);
    return orig(self, request);
}

var replacement = new Func<
    Func<NineGrid.Presentation.Systems.AudioSystem,
         NineGrid.Content.Audio.AudioCueRequest,
         NineGrid.Presentation.Systems.AudioCueResult>,
    NineGrid.Presentation.Systems.AudioSystem,
    NineGrid.Content.Audio.AudioCueRequest,
    NineGrid.Presentation.Systems.AudioCueResult>(Replacement);

// Hook 对象必须保活（被 GC 会自动还原），登记进 Registry 统一管理。
var hook = new MonoMod.RuntimeDetour.Hook(target, replacement);
reg[key] = hook;
return "[LiveLab] detour installed: " + key;
