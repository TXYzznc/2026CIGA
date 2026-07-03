# tools/playtest

Playtest 自动化产物目录。配套 SKILL：[.claude/skills/playtest-driver](../../.claude/skills/playtest-driver/SKILL.md)。

## 目录

- `reports/` — 每次 playtest 输出的 markdown 报告
  - `_TEMPLATE.md` — 报告模板（每次 playtest 复制此模板填充）
  - `YYYY-MM-DD-HHMM-<scenario>.md` — 实际报告文件

## 报告命名规范

```
YYYY-MM-DD-HHMM-<scenario>.md
```

- 日期时间：取测试启动时刻
- scenario：短横线分隔的小写英文短名，描述测试场景
  - 单功能：`e-key-triggers-skill` / `attack-mouse-click` / `wasd-movement`
  - 单界面：`main-menu-flow` / `settings-form-toggle` / `shop-buy-item`
  - 全量：`all-screens-summary`

## 触发 playtest 的方式

让 Claude 调用 `playtest-driver` SKILL。任一表述触发：

- "跑 playtest"
- "自动测试 <X 界面 / X 功能>"
- "模拟玩家 <操作>"
- "playtest 报告"

## 报告的 frontmatter 字段含义

| 字段 | 必填 | 取值 |
|---|---|---|
| `test_time` | ✅ | ISO 时间 `YYYY-MM-DD HH:MM` |
| `scenario` | ✅ | 与文件名 scenario 一致 |
| `result` | ✅ | `PASS` / `FAIL` / `PARTIAL` |
| `duration_sec` | ✅ | playtest 总耗时（秒） |
| `errors_found` | ✅ | console 抓到的 Error 数 |
| `warnings_found` | ✅ | console 抓到的 Warning 数 |

## 报告归档

- 每个独立的 playtest 输出独立报告
- 全量界面/功能跑完后，写一份 `YYYY-MM-DD-all-screens-summary.md` 链接到所有子报告
- **不**自动清理旧报告——历史记录有助于回溯回归
- 大型修订后建议手动归档到 `reports/archive/<YYYY-MM>/` 子目录
