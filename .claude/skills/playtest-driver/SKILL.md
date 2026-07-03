---
name: playtest-driver
description: 在 Unity Editor 中自动驱动 playtest：控制 Play Mode、触发项目输入入口或 UI 事件、读取日志并生成测试报告。模板不预设具体输入框架。
tags: playtest, unity-editor-automation, input-simulation, e2e-testing, test-report
---

# playtest-driver

用于让 AI 在 Unity Editor 中按测试用例扮演玩家：启动 Play Mode、执行输入或 UI 操作、读取日志、输出 markdown 测试报告。

## 原则

- 先确认当前项目真实存在的输入入口、UI 事件入口或测试注入工具。
- 模板不预设 `InputModule`、`GameApp` 或任何启动器。
- 不直接改业务状态，除非项目测试规范明确允许。
- 每条测试用例都要有操作步骤、预期结果、实际结果和 PASS/FAIL。

## 常用命令

```bash
python .claude/skills/unity-skills/scripts/unity_skills.py console_clear
python .claude/skills/unity-skills/scripts/unity_skills.py editor_get_state
python .claude/skills/unity-skills/scripts/unity_skills.py editor_play
python .claude/skills/unity-skills/scripts/unity_skills.py console_get_logs limit=50
```

如果项目提供 `Tools/Playtest/*` 菜单或测试专用事件，可以通过 `editor_execute_menu` 或 `event_invoke` 调用；参数含中文时使用 `--stdin-json`。

## 报告位置

建议输出到：

```text
Tools/playtest/reports/YYYY-MM-DD-HHMM-<scope>.md
```

## 边界

- 单元测试 / EditMode / PlayMode 测试：优先使用 `uloop-run-tests`。
- Web E2E：使用 `playwright`。
- 性能压测：使用对应性能测试工具。
