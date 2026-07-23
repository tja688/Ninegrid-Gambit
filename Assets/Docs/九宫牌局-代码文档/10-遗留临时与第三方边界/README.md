# 10 · 遗留、临时与第三方边界

> 本页标注**清理候选**与**不可随意改动的外部边界**。判断仅来自目录名、asmdef、代码注释与引用关系。

---

## 临时程序集

### `NineGrid.TemporaryTest`

| 项 | 事实 |
|----|------|
| 路径 | `Assets/Scripts/Temporary Test/` |
| asmdef | `NineGrid.TemporaryTest`；引用 `NineGrid.Flow` |
| 源码 | 仅 `UITestLivingFormChoiceTest.cs` |
| 职责（类注释） | UITest 专用：Bounce 扇形形态选择验收；由 `UITestBootstrap` 转发小键盘 1 |
| 接口 | 实现 `NineGrid.Flow.IUITestKeyConsumer`（Flow 侧注释写明：实现放 Temporary，避免 Flow 反向引用） |
| 清理含义 | 名称即 Temporary；验收完成后可整体移除程序集，保留接口在 Flow 或一并删 Bootstrap 分支 |

---

## 第三方 / 插件边界（`Assets/Plugins`）

| 目录 | 被自研程序集引用的证据 |
|------|------------------------|
| **QFramework**（`Assets/QFramework`） | Core / Flow / DevTest asmdef |
| **UniTask** | Cards / Flow / DevTest |
| **Demigiant / DOTween** | Cards、Flow 预编译 `DOTween.dll`；另有 `DOTweenTimeline` 插件目录 |
| **DamageNumbersPro** | Flow、DevTest |
| **Sirenix (Odin)** | Cards 预编译 `Sirenix.OdinInspector.Attributes.dll` |
| **Luban.Runtime** | Content asmdef（具体包路径以工程 Package/Plugins 为准） |
| UniRx | 插件存在；主链 asmdef **未**直接列出（是否历史残留需全库引用扫描后决定） |
| Easy Save 3 | 插件存在；主链 asmdef 未列出 |
| Febucci / Feel / Feel 1 | 插件存在；LivingUI Editor 有 `LivingUiFeelWindow` 文件名关联 |
| TextMesh Pro | Flow、VisualLook 引用 |

**边界规则（事实约束）**：重构自研层时，默认不改 Plugins / QFramework 源码；只改引用方式与自研适配层。

---

## QFramework 边界

| 项 | 事实 |
|----|------|
| 位置 | `Assets/QFramework/Framework` + `Toolkits` |
| 唯一 Architecture | `NineGrid.Core.NineGridArchitecture : Architecture<NineGridArchitecture>` |
| 使用层 | Core（注册 Model/System/Utility）；Flow/DevTest 通过 `QFramework` using 取 `IArchitecture` / 发送命令 |
| Cards / Content / LivingUI / VisualLook | asmdef **不**引用 QFramework |

---

## 体量异常（重构时优先盯梢）

以下来自文件行数/命名，非价值判断：

| 信号 | 位置 |
|------|------|
| 超大 MonoBehaviour | `Flow/BattleSessionController.cs`（数千行量级；类注释称 #11 硬切后编排出口为 PresentationDirector，本类仍为局内桥） |
| 生成代码区 | `NineGrid.Content/Generated/Luban/**` — 勿手改，走 Luban 生成 |
| 双 Feel 目录 | `Plugins/Feel` 与 `Plugins/Feel 1` |

---

## 相关

- [程序集与依赖](../00-总览与路由/程序集与依赖.md)  
- [跨层契约](../09-跨层契约与依赖/README.md)  
