# 开课时的直觉：松散 MVC + 可拖拽配置

用户已能感到 SO 玩法在做「输入 / 规则 / 状态 / 事件 / 表现」拆分，并把它类比为不够标准的 MVC；同时抓住了 Inspector 拖拽接线这一表层优点。这对后续总图课很关键：第一课要钉死——职责拆分两边都有，「用资产当通信节点」才是 SO 玩法相对 NineGrid（组合根 + QF）的真正差异。

## Implications
- 不要从「什么是 MVC」开讲；要从「同一条点击路径，两边接线落在哪里」开讲。
- 用户对本仓库深度高：对比锚点优先用 IntentIntake、CompositionRoot、PresentationSceneBindings、CardEffectSO。
