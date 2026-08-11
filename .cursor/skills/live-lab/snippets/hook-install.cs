// LiveLab 钩子模板：把 StepDemo 全部替换成本次实验唯一名（如 StepAudio_v2）。
// 幂等：重跑本片段 = 先卸旧再挂新（调参数就是改常量后重装同 marker）。
// 卸载：跑 hook-uninstall.cs。
// 执行前先把本文件存到 Assets/Notes/LiveLab~/<日期>-<主题>/ 并写好头注释：
//   marker: LiveLab_StepDemo
//   意图: <一句话>
//   落地目标: <真实接线点文件路径与位置>
//   状态: 临时生效
string marker = "LiveLab_StepDemo";

// 1) 摘旧版本：编译后局部函数名形如 <Execute>g__LiveLab_StepDemo_Tick|0_0，
//    必须按子串 Contains 匹配，严禁 == 精确匹配（会漏删导致双触发）。
var existing = EditorApplication.update;
if (existing != null)
{
    foreach (var d in existing.GetInvocationList())
    {
        if (d.Method.Name.Contains(marker))
            EditorApplication.update -= (EditorApplication.CallbackFunction)d;
    }
}

// 2) 钩子私有状态放局部变量，被闭包捕获、随钩子存活。
double lastLogAt = 0;

// 3) 钩子本体：局部函数名必须含 marker；第一行必须挡非 Play 状态
//    （退出 Play 后钩子仍在编辑态被 tick）。
void LiveLab_StepDemo_Tick()
{
    if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;

    // —— 在这里写轮询 / 触发逻辑；日志一律带 [LiveLab] 前缀方便 read_console 过滤 ——
    // 示例：每 2 秒报一次心跳（装好后删掉，换成真实逻辑）
    if (EditorApplication.timeSinceStartup - lastLogAt > 2.0)
    {
        lastLogAt = EditorApplication.timeSinceStartup;
        Debug.Log("[LiveLab] " + marker + " alive");
    }
}

EditorApplication.update += LiveLab_StepDemo_Tick;
return "[LiveLab] installed: " + marker;
