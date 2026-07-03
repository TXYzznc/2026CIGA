#!/usr/bin/env python
"""
聚合 .claude/skills/_usage.log 输出 SKILL / Agent / MCP 使用频次报告。

用法：
    python tools/audit_skill_usage.py             # 全部历史
    python tools/audit_skill_usage.py --days 30   # 最近 30 天
"""
import argparse
import datetime
from collections import Counter
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
LOG = ROOT / ".claude" / "skills" / "_usage.log"
SKILLS_DIR = ROOT / ".claude" / "skills"
AGENTS_DIR = ROOT / ".claude" / "agents"


def parse_log(days: int | None):
    if not LOG.exists():
        return []
    cutoff = None
    if days:
        cutoff = datetime.datetime.now() - datetime.timedelta(days=days)
    rows = []
    for line in LOG.read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        parts = line.split("\t")
        if len(parts) < 3:
            continue
        ts_str, kind, name = parts[0], parts[1], parts[2]
        if cutoff:
            try:
                ts = datetime.datetime.fromisoformat(ts_str)
                if ts < cutoff:
                    continue
            except ValueError:
                pass
        rows.append((ts_str, kind, name))
    return rows


def report(rows, days):
    title_suffix = f"（最近 {days} 天）" if days else "（全部历史）"
    print(f"# Tool 使用频次报告{title_suffix}")
    print()

    by_kind: dict[str, Counter] = {"Skill": Counter(), "Agent": Counter(), "MCP": Counter()}
    for _, kind, name in rows:
        if kind in by_kind:
            by_kind[kind][name] += 1

    for kind, counts in by_kind.items():
        print(f"## {kind} 调用频次")
        if not counts:
            print("  （无记录）")
            print()
            continue
        for name, n in counts.most_common(20):
            print(f"  {n:>4d}  {name}")
        print()

    # 0 次召回的 SKILL（候选淘汰）
    all_skills = {p.name for p in SKILLS_DIR.iterdir() if p.is_dir()}
    used = set(by_kind["Skill"].keys())
    zero = sorted(all_skills - used)
    print(f"## 0 次召回的 SKILL（{len(zero)} 个 / 共 {len(all_skills)}）")
    for s in zero:
        print(f"  - {s}")
    print()

    # 0 次召回的 Agent
    if AGENTS_DIR.exists():
        all_agents = {p.stem for p in AGENTS_DIR.glob("*.md")}
        used_a = set(by_kind["Agent"].keys())
        zero_a = sorted(all_agents - used_a)
        print(f"## 0 次召回的 Agent（{len(zero_a)} 个 / 共 {len(all_agents)}）")
        for s in zero_a:
            print(f"  - {s}")
        print()

    # 总览
    print("## 总览")
    print(f"  日志条数：{len(rows)}")
    if rows:
        print(f"  时间范围：{rows[0][0]} ~ {rows[-1][0]}")
    print(f"  Skill 调用：{sum(by_kind['Skill'].values())}（{len(by_kind['Skill'])} 个不同 SKILL）")
    print(f"  Agent 调用：{sum(by_kind['Agent'].values())}（{len(by_kind['Agent'])} 个不同 Agent）")
    print(f"  MCP 调用：{sum(by_kind['MCP'].values())}（{len(by_kind['MCP'])} 个不同 MCP tool）")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--days", type=int, default=None, help="只统计最近 N 天")
    args = parser.parse_args()
    rows = parse_log(args.days)
    report(rows, args.days)


if __name__ == "__main__":
    main()
