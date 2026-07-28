# NOTES

## 用户偏好
- 对比式学习：用 NineGrid 深度认知迁移到 SO 玩法
- 按模块拆课，一次学一块
- 输入材料：桌面 `SO玩法.md`（AI 对网上教程的总结）

## 课程路线（拟定 8 课）
1. **总图对照** — 职责相同，接线不同（本课）
2. **配置 SO** — 你们已经在用（CardEffectSO / Encounter / SlotRegistry…）
3. **Field SO** — 共享运行时事实 ↔ InputState / BindableProperty / Director 状态
4. **Event Channel** — 「发生了一件事」↔ struct Event / Hook / Core Event
5. **Rules Model** — 业务规则中心 ↔ Core + Query + IntentIntake
6. **Bridge** — 场景缝 ↔ Controller / HitProxy / Hook
7. **State Manager** — 状态栈 / 取消 ↔ 所有权轴 + GameFlow 相位
8. **优缺点与迁移判断** — 何时该 SO、何时绝不要

## 开课前直觉（待验证）
- 「松散 MVC + 可拖拽可视化」——部分对：有职责拆分与 Inspector 接线；「松散」易低估两边的纪律性。
