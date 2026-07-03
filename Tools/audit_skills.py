#!/usr/bin/env python
"""
SKILL description 长度与触发词审计（11-skill-governance 防腐脚本）。

每月跑一次：
    python tools/audit_skills.py

输出：
- description 长度分布（avg/max/min）
- ❌ 极短（<60 字符）清单
- ❌ 极长（>250 字符）清单
- ⚠️ 缺触发词清单
- ✅ 持久化基线到 .claude/skills/_audit.json，下次跑可对比回归
"""
import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
SKILLS_DIR = ROOT / ".claude" / "skills"
AUDIT_FILE = SKILLS_DIR / "_audit.json"

# 阈值（与 SKILL_MATRIX §六 对齐）
MIN_LEN = 60
MAX_LEN = 250
AVG_MIN = 120
AVG_MAX = 180

# 触发词关键字（中英双语）
TRIGGER_KEYWORDS = (
    "触发", "Trigger", "trigger", "适用", "When to use", "when to use",
    "覆盖", "Covers", "覆盖：", "❌ 不适用",
)


def scan_skills():
    results = []
    for skill_dir in sorted(SKILLS_DIR.iterdir()):
        if not skill_dir.is_dir():
            continue
        md = skill_dir / "SKILL.md"
        if not md.exists():
            results.append({"name": skill_dir.name, "error": "missing SKILL.md"})
            continue
        text = md.read_text(encoding="utf-8", errors="replace")
        fm = re.match(r"^---\n(.*?)\n---", text, re.S)
        if not fm:
            results.append({"name": skill_dir.name, "error": "no frontmatter"})
            continue
        block = fm.group(1)
        sub = re.search(r"^description:\s*(.*?)(?=^\w[\w\-]*:|\Z)", block, re.M | re.S)
        desc = sub.group(1).strip() if sub else ""
        # YAML 多行折叠去除缩进换行
        desc_flat = re.sub(r"\s+", " ", desc).strip()
        has_trigger = any(kw in desc_flat for kw in TRIGGER_KEYWORDS)
        results.append({
            "name": skill_dir.name,
            "desc_len": len(desc_flat),
            "has_trigger": has_trigger,
        })
    return results


def load_baseline():
    if not AUDIT_FILE.exists():
        return None
    try:
        return json.loads(AUDIT_FILE.read_text(encoding="utf-8"))
    except Exception:
        return None


def save_baseline(results):
    AUDIT_FILE.write_text(
        json.dumps(results, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )


def report(results, baseline):
    valid = [r for r in results if "desc_len" in r]
    lens = [r["desc_len"] for r in valid]
    avg = sum(lens) / len(lens) if lens else 0
    n = len(valid)

    print(f"# SKILL description 审计报告")
    print()
    print(f"## 总览")
    print(f"  SKILL 总数：{n}")
    print(f"  平均长度：{avg:.0f}  (阈值 {AVG_MIN}-{AVG_MAX})")
    print(f"  最大长度：{max(lens)}  (阈值 ≤{MAX_LEN})")
    print(f"  最小长度：{min(lens)}  (阈值 ≥{MIN_LEN})")
    print()

    # 与基线对比
    if baseline:
        old_valid = [r for r in baseline if "desc_len" in r]
        old_avg = sum(r["desc_len"] for r in old_valid) / len(old_valid) if old_valid else 0
        delta = avg - old_avg
        arrow = "↑" if delta > 0 else ("↓" if delta < 0 else "→")
        print(f"## 回归检查（vs 上次基线）")
        print(f"  平均长度变化：{old_avg:.0f} → {avg:.0f}  {arrow} {abs(delta):.1f}")
        if delta > 10:
            print(f"  [WARN] 平均长度回涨 > 10 字符，需关注")
        print()

    # 极短违规
    too_short = [r for r in valid if r["desc_len"] < MIN_LEN]
    print(f"## [FAIL] 极短 description（<{MIN_LEN} 字符，{len(too_short)} 个）")
    if too_short:
        for r in sorted(too_short, key=lambda x: x["desc_len"]):
            print(f"  {r['desc_len']:>4d}  {r['name']}")
    else:
        print(f"  [OK] 无违规")
    print()

    # 极长违规
    too_long = [r for r in valid if r["desc_len"] > MAX_LEN]
    print(f"## [FAIL] 极长 description（>{MAX_LEN} 字符，{len(too_long)} 个）")
    if too_long:
        for r in sorted(too_long, key=lambda x: -x["desc_len"]):
            print(f"  {r['desc_len']:>4d}  {r['name']}")
    else:
        print(f"  [OK] 无违规")
    print()

    # 缺触发词
    no_trigger = [r for r in valid if not r["has_trigger"]]
    print(f"## [WARN] 缺触发词关键字（{len(no_trigger)} 个）")
    if no_trigger:
        for r in no_trigger:
            print(f"  - {r['name']}")
    else:
        print(f"  [OK] 全部带触发词")
    print()

    print(f"基线已保存到 {AUDIT_FILE.relative_to(ROOT)}")


def main():
    baseline = load_baseline()
    results = scan_skills()
    report(results, baseline)
    save_baseline(results)


if __name__ == "__main__":
    main()
