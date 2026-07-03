---
test_time: YYYY-MM-DD HH:MM
scenario: <场景短名，如 e-key-triggers-skill / main-menu-flow / settings-form-toggle>
result: PASS  # PASS / FAIL / PARTIAL
duration_sec: 0
errors_found: 0
warnings_found: 0
---

# Playtest Report — <scenario>

## 概要

- **测试目标**：一句话写清验证什么
- **测试结果**：✅ PASS / ❌ FAIL / ⚠️ PARTIAL
- **关键发现**：1-3 条要点（命中预期日志 / 发现的异常 / 性能观察）
- **耗时**：N 秒

## 测试流程

按 STEP 顺序列实际执行：

1. STEP 1 准备：`editor_stop` + `console_clear` → OK
2. STEP 2 启动 Play：`editor_play` → 等就绪 3.2s → 命中 "GameReadyEvent"
3. STEP 3 装配 simulator：`uloop` 返回 "simulator ready"
4. STEP 4 注入循环：
   - 注入 `PressKey(KeyCode.E)` → sleep 0.5s → 日志命中 `[SkillModule|INFO]`
   - 注入 `PressMouse(0)` → sleep 0.3s → 日志命中 `[CombatModule|INFO] Action=Attack`
5. STEP 5 收尾：`console_get_logs filter=Error` 0 条 → `editor_stop`

## 遇到的问题

按严重度从高到低列：

- **[ERROR]** 模块/位置 → 现象 → 影响 → 建议
- **[WARN]** 模块/位置 → 现象 → 影响 → 建议
- **[OBSERVATION]** 非错误但值得记录

无问题时：「无」

## 关键日志摘录

```
[SkillModule|INFO] Action=SkillCast SkillId=fireball Cost=10
[CombatModule|INFO] Action=DamageDealt Target=enemy_01 Damage=42
```

## 后续

- [ ] 待跟进的 bug / 改进项
- [ ] 下一轮测试关注点
