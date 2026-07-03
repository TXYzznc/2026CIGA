#!/usr/bin/env python
"""
Tool usage logger — Claude Code PreToolUse hook。

捕获三类 tool 调用：
- Skill        → 从 tool_input.skill 取 SKILL 名
- Agent        → 从 tool_input.subagent_type 取 agent 名
- mcp__*       → 直接用 tool_name 作为 MCP 名

写到 .claude/skills/_usage.log（TSV 格式）。
失败时静默退出，绝不阻塞工具调用。
"""
import sys
import json
import datetime
from pathlib import Path


def main():
    try:
        data = json.load(sys.stdin)
    except Exception:
        return

    try:
        tool_name = data.get("tool_name", "")
        tool_input = data.get("tool_input", {}) or {}
        session = (data.get("session_id") or "")[:8]

        if tool_name == "Skill":
            kind = "Skill"
            name = tool_input.get("skill") or "unknown"
        elif tool_name == "Agent":
            kind = "Agent"
            name = tool_input.get("subagent_type") or "general-purpose"
        elif tool_name.startswith("mcp__"):
            kind = "MCP"
            name = tool_name
        else:
            return

        root = Path(__file__).resolve().parent.parent
        log = root / ".claude" / "skills" / "_usage.log"
        log.parent.mkdir(parents=True, exist_ok=True)

        ts = datetime.datetime.now().isoformat(timespec="seconds")
        with log.open("a", encoding="utf-8") as f:
            f.write(f"{ts}\t{kind}\t{name}\t{session}\n")
    except Exception:
        # 任何异常都不能 block 工具调用
        pass


if __name__ == "__main__":
    main()
