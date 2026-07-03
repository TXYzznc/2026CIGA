---
name: quest-mission-design
description: 任务/Quest 系统设计——类型分层、状态图、分支条件、quest log 信息架构、验收 DoD 与奖励曲线。触发：quest、任务、mission、quest log、分支任务、objective、副本、剧情任务、支线、可重复任务、quest giver
tags: quest-design, mission, narrative, systems-design, level-designer
---

# Quest / Mission Design

## 何时使用
- 设计 RPG、开放世界、ARPG 的任务系统时
- Quest log / Journal 信息架构设计
- 主线分支、支线钩子、可重复任务（daily/weekly）规划
- 任务验收（DoD）与奖励曲线评审
- 任务卡死、超时、废弃机制定义

## 核心原则
1. **每个任务都是一段微型循环**：钩子（Hook）→ 旅程（Travel/Action）→ 高潮（Climax）→ 奖励（Resolution）。缺一不可。
2. **目标可见、动机可信、路径可达**：玩家任意时刻应能回答「我为什么要做」「我接下来去哪」。
3. **状态原子化**：任务的每个步骤必须可序列化为独立 flag，便于存档、断点续做、回溯校验。
4. **失败也是内容**：任务可以失败、超时、错过——但要在 quest log 中留下痕迹（Witcher 3「failed」标签），而不是无声消失。
5. **奖励 ≠ 唯一收益**：除经验/金币外，叙事推进、世界状态变化、NPC 关系才是「为什么再做一次」的根源。

## 关键模式

### 模式 A：任务类型分层
| 类型 | 定位 | 占比建议 | 案例 |
|---|---|---|---|
| Main（主线） | 强制推进剧情，不可错过 | 10-20% | Witcher 3 主线 |
| Side（支线） | 完整故事弧，可错过但有强叙事 | 30-40% | Witcher 3「血腥男爵」 |
| Branch / Faction（分支） | 阵营/选择驱动，互斥 | 10-20% | Fallout: NV 四阵营结局 |
| Repeatable（重复） | Daily/Weekly/Bounty | 10-20% | Destiny Bounty, FF14 Daily |
| Hidden / Emergent（隐藏） | 探索奖励、环境触发 | 5-10% | Elden Ring NPC 线 |

### 模式 B：状态机范式
每个 quest 用有限状态机表达：

```
[Inactive] → [Available] → [Active] → [InProgress: Step1] → [Step2] → ... → [Completed]
                                ↓                                              ↑
                            [Failed] ←——————————————— [Expired]            [Rewarded]
```

- **Available**：触发条件满足但未接（NPC 头顶 ! 显示）
- **Active**：已接，但未到第一个目标点
- **InProgress.Step{N}**：每个 objective 一个子状态
- **Completed**：所有 objective 完成，待返回
- **Rewarded**：领奖完成，正式归档
- **Failed / Expired**：失败 / 超时

### 模式 C：分支条件设计
分支由 **Flag + 状态查询** 驱动，避免硬编码：
- `flag.met_NPC_X = true`
- `world.faction_A_reputation >= 50`
- `quest.MQ03.choice == "spared"`
- `player.has_item(amulet_of_X)`

每个分支节点需有 **fallback 路径**：玩家错过 NPC 死亡了，任务应能继续而不是死锁。Disco Elysium / Witcher 3 都遵循「No Dead End」原则。

### 模式 D：Quest Log 信息架构
- **一句话目标**：「找到失踪的女儿」——电梯演讲级，玩家从菜单瞄一眼就懂
- **当前 Objective**：「前往诺维格瑞贫民窟询问 NPC」——具体动作
- **Journal 叙事段**：上一次推进时的玩家所做事情的复述（Witcher 3 风格，第一人称视角）
- **地图标记**：硬引导 vs 软引导可配置（Witcher 3 Death March 模式关闭精确标记）
- **失败/超时标签**：明确显示「失败」原因，避免玩家以为是 bug

### 模式 E：Quest Giver 设计
- **视觉识别**：头顶 `!`（可接）/ `?`（可交）/ 颜色区分类型（金=主线、银=支线）
- **位置可发现**：放置在玩家自然路径上（城镇枢纽、新区域入口）
- **再访价值**：完成后该 NPC 仍应有对话、动画、小奖励，避免「空气化」
- **避免 quest hub 堆叠**：一个 NPC 一次性给 5 个任务会让玩家麻木——拆分到不同地标

### 模式 F：DoD（Definition of Done）验收
每个 quest 必须可回答：
- [ ] 接取条件、前置任务是否明确？
- [ ] 每个 objective 是否有 fail-safe（玩家走丢能找回）？
- [ ] 是否支持存档中断后续？（关闭游戏再开能继续）
- [ ] 奖励是否记录在玩家成就/经济曲线表内？
- [ ] 文案是否过本地化（避免硬编码字符串）？
- [ ] 是否有 cinematic / 高潮时刻？（不只是「跑腿」）

### 模式 G：奖励曲线
| Quest 类型 | XP 占比 | 金币占比 | 独家掉落 | 叙事推进 |
|---|---|---|---|---|
| Main | 25-35% | 15-20% | 必有 | 必有 |
| Side | 15-25% | 25-35% | 高概率 | 强 |
| Branch | 20-30% | 20-30% | 必有（互斥） | 改变世界 |
| Repeatable | 5-10% | 30-40% | 低概率/碎片 | 无 |

奖励应**前低后高**：开头给小钩子（一把武器图样），高潮给大爆点（橙装/解锁新地图）。Hades 每次 Boss 战后的 Boon 选择就是教科书。

## 常见坑
- **坑：任务卡死无法继续**。原因：关键 NPC 死亡 / 物品被卖。修法：每个 critical NPC 标记为 essential / 关键物品标记不可丢弃，或提供 fallback NPC。
- **坑：玩家不知道下一步去哪**。原因：objective 文案太模糊（「调查那个地方」）。修法：动词+对象+方位，例如「前往 X 北门询问铁匠 Y」。
- **坑：支线比主线还精彩，主线显得寡淡**。原因：主线被工作量挤压。修法：保证主线节点都有「记忆点」——cinematic、Boss、世界变化。
- **坑：可重复任务变成苦工**。原因：奖励曲线没有递减。修法：每日任务给货币、每周任务给装备碎片、月度给保底箱——让重复有目的。
- **坑：分支选项玩家看不到后果**。原因：选项文案过短。修法：选项后展示 NPC 反应预览或道德取向 icon（Mass Effect Paragon/Renegade）。
- **坑：任务超时被默默废弃**。原因：玩家完全没收到通知。修法：超时前 push 一次 quest log 高亮 + 屏幕通知，超时后保留「failed」记录。
- **坑：quest log 太长无法阅读**。原因：所有任务平铺。修法：按区域 / 章节 / 类型分组，已完成的折叠归档。

## 输出形式

### 1. Quest 设计单（每个任务一张）
```
Quest ID：SQ_Novigrad_07
名称：The Missing Witcher
类型：Side / Branch
前置：MQ03 完成 + 与 NPC_Lambert 对话
预计时长：35-50 分钟
核心循环：接取 → 调查酒馆 → 战斗 → 选择 → 返回
Objectives（线性 + 状态机）：
  1. 与 NPC_X 对话               flag: started
  2. 在 Map_Tavern 调查 3 个线索    flag: clue_1, clue_2, clue_3
  3. 击败 Boss_Wraith             flag: boss_defeated
  4. 选择：拯救 / 处决             flag: choice_save | choice_kill
  5. 返回 NPC_X 复命                flag: rewarded
分支后果：
  - 选择拯救 → 解锁 SQ_Witcher_Reunion
  - 选择处决 → world.NPC_Lambert.dies = true
奖励：800 XP / 350 Crown / 银剑「Witcher's Edge」/ 解锁地图 X
DoD 检查：☐ 文案 ☐ 本地化 ☐ 存档 ☐ Fail-safe ☐ Cinematic
```

### 2. Quest 状态图（mermaid 或表格）
状态、转换条件、副作用全部列出，供程序对齐实现。

### 3. Quest 总览表（横向规划）
按章节 × 类型分布，便于检查节奏密度，避免某区域全是跑腿。
