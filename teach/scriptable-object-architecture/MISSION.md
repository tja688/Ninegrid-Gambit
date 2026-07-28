# Mission: 用对比法读懂 ScriptableObject 玩法

## Why
你已经深度掌握 TableNine（NineGrid）的组织方式，却卡在「网上那套完整 SO 玩法」——隐约觉得像松散 MVC + 可拖拽配置，但说不清它到底在解什么问题、和你们项目哪里同构。目标是把 SO 玩法变成可迁移的架构词汇：以后看到 Field / Event Channel / Rules Model / Bridge，能立刻映射到你们的 IntentIntake、CompositionRoot、Core，并判断该不该迁、迁哪一块。

## Success looks like
- 能用一句话区分「职责拆分」与「用 SO 资产做接线」——前者两边都有，后者才是 SO 玩法的独特之处
- 能把 SO 总结里的每种模式，点名对应到 NineGrid 里的具体角色（含「你们已经在用 SO 的地方」）
- 面对「要不要把某块改成 SO」时，能说出收益、代价，以及和 ADR（尤其 IntentIntake / 禁业务静态 Sink）是否冲突

## Constraints
- 对比式学习：始终以你对本仓库的认知为锚，不另起空中楼阁 demo 工程
- 中文为主；标识符与官方术语保留英文
- 按模块逐课推进；每课一个可检索的硬钉子
- 知识以 RESOURCES 与仓库 code-map / ADR 为据，不以参数化记忆硬讲作者截图里的私有类名

## Out of scope
- 把 NineGrid 整体重写成 SO 架构（教学不推动大规模迁移）
- Unity 编辑器扩展 / 自定义 Inspector 深挖
- 一般 C# MVC / MVP 教科书史
