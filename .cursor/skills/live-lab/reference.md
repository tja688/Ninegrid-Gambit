# Live Lab 深层参考

主文见 [SKILL.md](SKILL.md)。本文是按需查阅的机制细节与坑目录。

## execute_code 包装解剖

- 片段被包进 `public static class MCPDynamicCode { public static object Execute() { <片段> } }`，用 Roslyn（可用时，C# 12；否则 CodeDom，C# 6）内存编译，载入当前 AppDomain 后反射调用。
- 自动 using 只有六个：`System` / `System.Collections.Generic` / `System.Linq` / `System.Reflection` / `UnityEngine` / `UnityEditor`。**方法体内不能写 using 指令**——其余命名空间一律全限定（如 `NineGrid.Presentation.Systems.AudioSystem`）。
- 必须所有路径 `return`（惯例结尾 `return "ok";`），返回值序列化回 agent。
- 每次调用产出一个**新**匿名程序集，永不卸载；上一片段定义的局部函数/闭包对下一片段不可见。
- `safety_checks` 默认按子串拦 `File.Delete`、`Process.Start`、`while(true)`、`AssetDatabase.DeleteAsset` 等；确认误伤后可 `safety_checks: false`。
- history（`get_history` / `replay`）存内存，域重载即丢——不要当持久痕迹用。

## 生命周期表

| 事件 | 注入程序集/钩子 | 项目类型 static | 场景/运行时对象 | 磁盘文件 |
|------|----------------|----------------|----------------|---------|
| 退出 Play | **存活**（编辑态继续 tick！） | 保留（小心脏 static） | 丢 | 保留 |
| 进 Play（本项目默认域重载开） | 蒸发 | 重置 | 重建 | 保留 |
| recompile | 蒸发 | 重置 | — | 保留 |

推论：钩子体第一行永远 `if (!EditorApplication.isPlaying) return;`；会话收尾必须卸载；想跨 Play 存活的东西只有磁盘文件。

## 跨片段状态与钩子识别

- **钩子识别**：局部函数编译名形如 `<Execute>g__LiveLab_Xxx_Tick|0_0`——卸载按 `Method.Name.Contains(marker)` 子串匹配，**严禁 `==` 精确匹配**（音频台账踩坑 S1-02：漏删导致双触发）。
- **跨片段对象**（detour 句柄等需要保活且可寻回的东西）：`AppDomain.CurrentDomain.SetData("LiveLab.Registry", dict)` 存一个 `Dictionary<string, object>`。它与注入程序集同生命周期（域重载同归于尽），语义正好一致。
- 小段字符串可用 `SessionState`（域重载不丢、关编辑器丢）。

## 坑目录（实战验证，编号对应音频台账条目）

1. **整包替换**：`ApplyWorkbenchCatalog` 是整 catalog 替换——永远维护一份 live JSON 全量应用，别攒多个局部补丁互相冲掉（S1-03）。
2. **manifest 门禁**：新音频素材必须登记 `audio_manifest.json` 且 `AudioAssetManifestLoader.ClearCache()`，否则播放 Adapter 直接拒播（S1-04 / S1-05）。
3. **最短间隔吞音**：连发音的 `minimumIntervalSeconds` 必须小于错峰间隔，否则后续声被吞（S1-04）。
4. **改全局运行参数先记原值**：如 mixer 先 `GetFloat` 记录再 `SetFloat`，恢复靠记录值（S1-01）。
5. **落地沿选择**：轮询的触发沿要选稳定信号（如格位变化），别用瞬时布尔（`IsHopping` 连跳会漏拍）（S1-02）。
6. **「没声」先量响度**：素材试听无感先比较 rms 响度差再补 `volumeDb`，不要急着换素材（S2-01）。

## 方法重定向（MonoMod detour）

- `Packages/com.farlocus.locus/Editor/Detour/Locus.Detour.dll` 随域加载，片段可直接 `new MonoMod.RuntimeDetour.Hook(原方法, 替身委托)`。先跑 probe 确认 `monomod=True`。
- Hook 对象**必须保活且可寻回**：登记进 AppDomain Registry；卸载时 `Dispose()` 即还原原方法。
- 替身签名：首参可以是 orig 委托（调用原实现），之后 this（实例方法时）+ 原参数。模板见 [snippets/detour.cs](snippets/detour.cs)。
- 这是最深的通道：行为漂移风险最大，验收满意后当场把片段标 `待落地`，不过夜。
- 认知备注：Locus 桌面 App 自带方法体级热重载（compile-server sidecar + detour，直接改真实 .cs 让运行中游戏生效），那是人的工具，不经 Cursor 驱动；本 skill 不依赖它。

## 为什么痕迹阶梯长这样

- execute_code history 域重载即蒸发 → 不可靠。
- 台账（人话）可靠但间接：落地 AI 要凭描述重写代码。
- 片段文件 = 痕迹即代码：可重放恢复体验、可照抄翻译落地；与台账互补（台账管状态与人话，片段管精确行为）。
- 数据缝 live JSON = 痕迹即成品：格式与正式文件相同，落地退化成 diff 合并——最原生的一档，所以通道阶梯排第一。
