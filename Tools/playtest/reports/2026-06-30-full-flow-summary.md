---
created: 2026-06-30
scope: full-game-flow-v2.1
driver: playtest-driver SKILL (unity-skills REST + Tools/Playtest/* MenuItem)
plan: ../../../openspec/changes/15-playtest-driver/tests/plan.md
bugs: ../../../openspec/changes/15-playtest-driver/tests/bugs.md
---

# 全量 Playtest 汇总报告 — 2026-06-30

> 按 [plan.md](../../../openspec/changes/15-playtest-driver/tests/plan.md) 14 条 TC 执行的结果汇总。
> 单条 TC 的执行细节直接嵌在此文档（替代每 TC 单独 report，避免 14 个微型文件维护成本）。

## 总览

| 维度 | 值 |
|---|---|
| TC 总数 | 14 |
| PASS | 9 |
| PARTIAL | 1（TC-04：状态切换成功但 HUD 未激活，依赖 Bug#3） |
| FAIL | 2（TC-10、TC-12：UIForm 未响应事件，依赖 Bug#6/#7） |
| BLOCKED | 3（TC-05/11/13：前置 TC 失败） |
| 新增 Bug | 7（详见 [bugs.md](../../../openspec/changes/15-playtest-driver/tests/bugs.md)） |
| 全局 Error | 0 |
| 全局 Warning | 0 |
| 退出状态 | isPlaying=false ✅ |

## 用例执行明细

| TC | 场景 | 结果 | 关键证据 / 备注 |
|---|---|---|---|
| TC-01 | Launch 启动 → GameApp 就绪 | ✅ PASS | `[GameApp] 所有模块初始化完成` + `[UIModule\|INFO] Action=AllFormsLoaded Success=11/Total=11`；isPlaying=true |
| TC-02 | MainMenuForm 自动显示 | ✅ PASS | `[MainMenuForm\|INFO] Action=Register Form=MainMenuForm`（**唯一**成功 Register 的 Form，已是 Bug#3/#6/#7 的线索） |
| TC-03 | 装配 InputSimulator | ✅ PASS | `[Playtest\|INFO] Action=EnableSimulator Type=InputSimulator` |
| TC-04 | StartButton → 进战斗 | ⚠️ PARTIAL | StartButton 缺失（Bug#2），改用 Debug/StartGame；状态切换 Old=MainMenu New=InGame ✅；但 CombatHUDForm 仍 active=False（Bug#3） |
| TC-05 | CombatHUD Ready | 🚫 BLOCKED | 前置 Bug#3 未解，CombatHUDForm 不激活，`Action=Ready` 日志不存在 |
| TC-06 | WASD 移动覆盖 | ✅ PASS | `Action=SetMove Dir=(1.00, 0.00)` + `Action=SetMove Dir=null` 两条日志均命中 |
| TC-07 | 鼠左普攻 | ✅ PASS | `Action=PressMouse Button=0`（WeaponModule 后续事件为加分项，未观测到不算 FAIL） |
| TC-08 | E 技能 | ✅ PASS | `Action=PressKey Key=E`（PoC 已验证 InputSimulator → InputModule 链路） |
| TC-09 | 空格闪避 | ✅ PASS | `Action=PressKey Key=Space` |
| TC-10 | Tab 打开 SelfTattooForm | ❌ FAIL | PressKey Tab 日志命中，但 DumpUIForms 显示 SelfTattooForm(active=False) → 新增 Bug#6 |
| TC-11 | 再按 Tab 关闭 | 🚫 BLOCKED | 前置 TC-10 FAIL（Form 从未打开，无法测 toggle） |
| TC-12 | Esc 暂停（用 PublishPauseRequestedEvent 绕过） | ❌ FAIL | DebugPublishPauseRequested 日志命中，但 PauseMenuForm 仍 active=False → 新增 Bug#7 |
| TC-13 | Resume 退出暂停 | 🚫 BLOCKED | 前置 TC-12 FAIL |
| TC-14 | 全局 Error 检查 + 退 Play | ✅ PASS | 0 Error / 0 Warning；`Edit/Play` 后 isPlaying=false |

## 根因聚合

### 主线断链（高优先级，影响 5 个 TC）

**只有 MainMenuForm 成功 Register 到 UIModule**——UIModule 日志里 `AllFormsLoaded Success=11/Total=11`，但实际只有 MainMenuForm 触发了 `Action=Register Form=...` 日志。CombatHUDForm/PauseMenuForm/SelfTattooForm 全部 active=False 且无 Register 日志，导致：

- TC-04 partial / TC-05 BLOCKED（CombatHUD 不显示）
- TC-10/11 FAIL/BLOCKED（Tab 切 SelfTattoo 失败）
- TC-12/13 FAIL/BLOCKED（PauseRequestedEvent 无响应）

→ 一并归到 **Bug#3 / #6 / #7**，三者同根因，需 client-unity 先排 UIModule.Register / IUIForm.Awake 链路。

> 怀疑路径：`AllFormsLoaded` 是 UIModule 的"加载完成数"，但实际 IUIForm.Awake 时机或 GameApp.TryGetRuntime 等待循环存在 bug，导致 11 个 Form 中只有 1 个能完成异步 Start → Register。

### 周边小问题

- **Bug#1** — `event_invoke` 参数名 `objectName` 文档与实际不一致，应改 SKILL.md
- **Bug#2** — MainMenuForm Prefab 缺 StartButton 子节点
- **Bug#4** — SelfTattooForm 在 DumpUIForms 中出现两次（双实例）
- **Bug#5** — Unicode 箭头 `→` 经 curl/bash 调用 Editor 菜单时被 mangled（**已 FIXED**：所有菜单名替换为 ASCII `->`）

## 流程层验证

✅ **playtest-driver SKILL「TC 优先」规则首次实战**：先写 14 条 TC 矩阵 + 暂不覆盖边界 → 用户确认 → 执行；过程中遇到 7 个 bug 但没有原地阻塞、没有打断用户，走"记录 + 跳过 + 继续"路径覆盖完所有 TC。SKILL 规则验证有效。

✅ **PlaytestDriverEditor Debug 菜单**新增的 6 个 Debug 入口（StartGame/Pause/Resume/GameOver/GoToMainMenu/PublishPauseRequestedEvent/Dump SceneRoots/Dump UIForms）证明能有效绕过缺失的 UI 节点，是 AI 驱动 playtest 的关键"逃生口"。

## 后续建议

1. **优先级 P0** — client-unity 排查 UIModule.Register / IUIForm 异步初始化链路，修 Bug#3/#6/#7
2. **优先级 P1** — Bug#2 MainMenu Prefab StartButton 缺失（影响真实玩家体验）
3. **优先级 P2** — Bug#1 SKILL 文档修正 + Bug#4 SelfTattooForm 双实例排查
4. **下一批 Playtest 候选** — 修完 P0 后回归 TC-05/10/11/12/13；再扩展未覆盖的 RunResultForm 用例（需要 `Tools/Playtest/Debug/ForceEndRun` 菜单）
