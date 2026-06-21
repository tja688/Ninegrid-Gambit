# agents.md

## 项目介绍

本项目是一个 `Unity 6.3 LTS` urp 管线开发的 2D 卡牌像素风 Roguelike 游戏，名为 **TableNine**。核心玩法为九宫格棋盘驱动的卡牌战斗。

## 技术框架

- 项目框架采用 `QFramework`。
- 项目本地代码入口在：`Assets/Scripts`。

## 设计文档

- 游戏设计案统一存放在 `Assets/Docs` 目录下，当某些效果实现、游戏设计细节需要理解时可以进行查阅、参考。
- 游戏开发进行中过程性笔记：`Assets/Notes`，根据自己需求选择性阅读。如果开发过程中用户要求落地笔记、汇报等需求，统一落地在此目录。
- 游戏开发架构设计文档见：Assets/Notes/九宫牌局权威顶层架构设计.md ，想要总览游戏架构组织设计理念可以阅读此文档、

## 项目规则

- 开发本项目前，请优先阅读 `rules.md`，其中包含架构规范以及文件保护规则等全局性约束。
- 当前处于原型开发期，唯一游戏场景：`Assets/Scenes/MainScene.unity`
- 项目有 Unity MCP，在落地实现时注意了解功能并辅助使用，如果发现无法使用再回退文件操作形式开发。
- 请在完成代码落地后主动触发unity mcp 的unity 刷新功能并阅读Console，这可以暴露编译代码的错误并帮助你进行修复。
- 项目已经初始化 codegraph，可以使用codegraph macp来进行代码查询，使用提醒：它擅长“符号/类/方法/调用关系/影响面”，不擅长一次性查询混合了批次号、pending ID、设计文档、测试名、Luban 数据的宽泛任务。查询不理想时，先把问题拆成明确符号或代码区域，例如 `UseItemAction`、`EffectAtomLibrary`、`TableNineContentCatalog`、`P6ContentLandingTests`；查具体 pending ID、中文设计文本、JSON 行、计划笔记时优先用 `rg` 精确检索。代码刚改完且 codegraph 可能未同步时，以文件读取和 Unity 编译/测试结果为准。

## 协作需求

- 每次完成任务后，将本次改动**全量提交**至 git，提交信息应简洁、准确地概括改动内容与目的。
