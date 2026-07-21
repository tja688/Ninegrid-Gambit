QFramework架构使用规范

QFramework系统设计架构分为四层及其规则：

表现层：ViewController层。IController接口，负责接收输入和状态变化时的表现，一般情况下，MonoBehaviour 均为表现层

可以获取System

可以获取Model

可以发送Command

可以监听Event

系统层：System层。ISystem接口，帮助IController承担一部分逻辑，在多个表现层共享的逻辑，比如计时系统、商城系统、成就系统等

可以获取System

可以获取Model

可以监听Event

可以发送Event

数据层：Model层。IModel接口，负责数据的定义、数据的增删查改方法的提供

可以获取Utility

可以发送Event

工具层：Utility层。IUtility接口，负责提供基础设施，比如存储方法、序列化方法、网络连接方法、蓝牙方法、SDK、框架继承等。啥都干不了，可以集成第三方库，或者封装API

除了四个层级，还有一个核心概念——Command

可以获取System

可以获取Model

可以发送Event

可以发送Command

层级规则：

IController 更改 ISystem、IModel 的状态必须用Command

ISystem、IModel状态发生变更后通知IController必须用事件或BindableProperty

IController可以获取ISystem、IModel对象来进行数据查询

ICommand不能有状态

上层可以直接获取下层，下层不能获取上层对象

下层向上层通信用事件

上层向下层通信用方法调用（只是做查询，状态变更用Command），IController的交互逻辑为特别情况，只能用Command