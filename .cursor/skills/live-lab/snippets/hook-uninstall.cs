// LiveLab 卸载：filter 为空 = 卸载全部 LiveLab 钩子与登记对象；
// 只卸某个实验就填 marker 子串（如 "StepDemo"）。会话收尾必须全卸一次。
string filter = "";

int removed = 0;
var list = EditorApplication.update;
if (list != null)
{
    foreach (var d in list.GetInvocationList())
    {
        string n = d.Method.Name;
        if (!n.Contains("LiveLab_")) continue;
        if (filter.Length > 0 && !n.Contains(filter)) continue;
        EditorApplication.update -= (EditorApplication.CallbackFunction)d;
        removed++;
    }
}

// 登记表里的 detour 句柄等：Dispose 即还原
var reg = AppDomain.CurrentDomain.GetData("LiveLab.Registry") as Dictionary<string, object>;
if (reg != null)
{
    foreach (var key in reg.Keys.ToList())
    {
        if (filter.Length > 0 && !key.Contains(filter)) continue;
        var disp = reg[key] as IDisposable;
        if (disp != null) disp.Dispose();
        reg.Remove(key);
        removed++;
    }
}
return "[LiveLab] removed: " + removed + (filter.Length > 0 ? " (filter=" + filter + ")" : " (all)");
