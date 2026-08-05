---
id: kd_3a74ea45-5a41-469f-ab3f-c1eac8dc519d
type: memory
path: engineering-skills-setup.md
title: engineering-skills-setup
inheritInjectMode: true
summaryEnabled: true
commandEnabled: false
readOnly: false
inheritAiConfig: true
createdAt: 1785896646433
updatedAt: 1785896646434
---

# engineering-skills-setup

## Summary
Matt Pocock engineering skills 已作为 Locus App skill 包安装（17 个），本仓库依赖文件已齐备，可直接使用，勿重复跑 setup。

<!-- locus:body:start -->
- 来源：C:/Users/jinji/GitTools/skills/skills/engineering，已全部导入为 App 级 skill 包（%APPDATA%/locus/skills/<id>/，schema locus.skill.v1），所有项目可用。
- 9 个仅斜杠命令触发（disable-model-invocation，与源 frontmatter 一致）：/ask-matt /grill-with-docs /triage /improve-codebase-architecture /setup-matt-pocock-skills /to-spec /to-tickets /implement /wayfinder。
- 8 个模型自动触发：/code-review /codebase-design /diagnosing-bugs /domain-modeling /prototype /research /resolving-merge-conflicts /tdd。
- 本仓库依赖已就绪：docs/agents/{issue-tracker,domain,triage-labels,unity-cli}.md、AGENTS.md 含 "## Agent skills" 段、CONTEXT.md、docs/adr/、docs/code-map/。docs/agents/*.md 缺失时才需要跑 /setup-matt-pocock-skills。
- 源 skills 的 agents/*.yaml（Claude Code/Codex 专用）已跳过；SKILL.md 中 "sub-agent" / "Agent tool" / "general-purpose subagent" 等措辞在 Locus 对应 task 工具（subagent_type dev/explorer）。
- 不迁移 Cursor 环境：模型（Grok 绑定 Cursor 账号）与 cli-config/hooks/ticket-runner 不可复用；DeepSeek API key 在 Claude Code settings 里，Locus custom_providers 已有 DeepSeek provider 但 apiKey 为空（用户已明确不迁移）。
<!-- locus:body:end -->
