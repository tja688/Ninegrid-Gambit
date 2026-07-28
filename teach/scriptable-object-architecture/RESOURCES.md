# ScriptableObject 架构 Resources

## Knowledge

- [Talk: Unite Austin 2017 — Game Architecture with Scriptable Objects — Ryan Hipple](https://www.youtube.com/watch?v=raQ3iHhE_Kk)
  SO 架构的权威源头：把 SO 当模块间「胶水」（共享变量、事件频道、Runtime Sets、系统资产），明确反对 Singleton 当默认共享。Use for: Field / Event Channel 的原始动机。
- [Blog: My Unite 2017 Talk — Ryan Hipple](http://www.roboryantron.com/2017/10/unite-2017-game-architecture-with.html)
  讲座配套文字 + 运行时值勿写回磁盘的注意点。Use for: 快速回顾与「runtime value reset」坑。
- [Repo: roboryantron/Unite2017](https://github.com/roboryantron/Unite2017)
  讲座极简样例：`GameEvent`、变量 SO、RuntimeSet。Use for: 对照作者扩展出的 Rules Model / State Manager 时，先看「经典三件套」长什么样。
- [Docs: Unity Manual — ScriptableObject](https://docs.unity3d.com/Manual/class-ScriptableObject.html)
  官方定义：可序列化资产、可被多处引用、无 GameObject/Transform。Use for: 「配置 SO」基线与「不是场景对象」边界。
- [Repo: docs/code-map/presentation.md](../../docs/code-map/presentation.md)
  NineGrid 表现层现状：CompositionRoot、Controllers、IntentIntake、读写约定。Use for: 对比课的「你们怎么玩」。
- [ADR-0004: IntentIntake 两轴门禁](../../docs/adr/0004-input-intake-two-axis-gating.md)
  输入唯一收口不变量。Use for: 对照 SO Rules Model + Bridge 多入口共用规则中心。

## Wisdom (Communities)

- [Unity Discussions — ScriptableObject](https://discussions.unity.com/)
  官方论坛；搜 ScriptableObject architecture / event channel。Use for: 真实项目踩坑（重复 asset、Play Mode 脏值）。
- 用户偏好：默认不强推 Discord/Reddit 社群；若你明确想找人对练再补。

## Gaps

- 用户提供的「SO玩法.md」是二次 AI 总结，不是原作者一手材料；讲「作者真实项目」时以该总结为线索，模式命名以 Hipple 经典 + 总结中的扩展层（Rules Model / State Manager / Bridge）为准，避免假装见过原仓库源码。
- 尚缺原教程作者的公开仓库链接；若你之后补上，应升格进 Knowledge。
