# rules.md

## 架构使用规范

QFramework 系统设计架构分为四层及其规则，此规则仅起到基础使用指导作用，具体落地以项目实际情况为准：

### 表现层（ViewController）

IController 接口，负责接收输入和状态变化时的表现。一般情况下，MonoBehaviour 均为表现层。

- 可以获取 System
- 可以获取 Model
- 可以发送 Command
- 可以监听 Event

### 系统层（System）

ISystem 接口，帮助 IController 承担一部分逻辑，在多个表现层共享的逻辑，比如计时系统、商城系统、成就系统等。

- 可以获取 System
- 可以获取 Model
- 可以监听 Event
- 可以发送 Event

### 数据层（Model）

IModel 接口，负责数据的定义、数据的增删查改方法的提供。

- 可以获取 Utility
- 可以发送 Event

### 工具层（Utility）

IUtility 接口，负责提供基础设施，比如存储方法、序列化方法、网络连接方法、蓝牙方法、SDK、框架继承等。啥都干不了，可以集成第三方库，或者封装 API。

### Command

除了四个层级，还有一个核心概念——Command。

- 可以获取 System
- 可以获取 Model
- 可以发送 Event
- 可以发送 Command

### 层级规则

- IController 更改 ISystem、IModel 的状态必须用 Command
- ISystem、IModel 状态发生变更后通知 IController 必须用事件或 BindableProperty
- IController 可以获取 ISystem、IModel 对象来进行数据查询
- ICommand 不能有状态
- 上层可以直接获取下层，下层不能获取上层对象
- 下层向上层通信用事件
- 上层向下层通信用方法调用（只是做查询，状态变更用 Command），IController 的交互逻辑为特别情况，只能用 Command

---

## 文件保护规则

- **严禁改动 .unity 文件**：在任何情况、以任何理由直接修改 `.unity` 后缀的场景文件。相关改动需求应通过 Unity MCP 进行落地，不得手动或通过文本方式编辑 `.unity` 文件。

## 其他
- AbstractCommand / AbstractSystem / IController 中禁止裸调架构能力方法**：在 `AbstractCommand.OnExecute()`、`AbstractSystem.OnInit()`、`IController` 实现类等上下文中，访问 Model、System、Command、Event、Query 时，必须通过 `this.` 调用 QFramework 扩展方法（如 `this.GetModel<T>()`、`this.SendCommand()`、`this.SendEvent()`）。