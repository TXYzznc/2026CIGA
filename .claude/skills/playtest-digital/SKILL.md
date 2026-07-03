---
name: playtest-digital
description: 数字游戏 playtest 全流程方法论，从招募到观察、问卷、访谈到迭代决策。触发：playtest、玩家测试、用户测试、focus group、think-aloud、SUS、NPS、可用性测试、玩家访谈
tags: playtest, ux-research, usability, sus, nps, qa
---

# 数字游戏 Playtest 方法论

## 何时使用
- 关键 milestone 前验证核心机制是否「fun」
- 新手引导设计完毕，需要测试 drop-off 点
- 上线前一周做 polish playtest，捕捉 friction
- 调整难度曲线、UI 改版后量化前后差异
- 商业化点位调整，评估玩家反感度

## 核心组件
- **类型**：Kleenex / One-on-one / Focus Group / Blind / Remote
- **平台**：UserTesting.com、PlaytestCloud、Steam Playtest、TestFlight、自建 Discord
- **录制**：OBS / Loom / 内置 Replay；移动端用 reflector + Zoom
- **问卷**：SUS（系统可用性）+ NPS（推荐意愿）+ 自定义 Likert
- **分析**：Miro / FigJam affinity diagram；Notion / Airtable 跟踪

## 类型对照

| 类型 | 人数 | 用途 | 成本 | 何时用 |
|---|---|---|---|---|
| Kleenex Test | 1-2 | 5min 第一印象 | 极低 | 每周迭代 |
| One-on-one | 1+主持人 | 深度行为 + 访谈 | 中 | 核心机制验证 |
| Focus Group | 4-8 | 群体讨论 | 高 | 题材/IP/美术方向 |
| Blind Test | 5-10 | 看玩家能否自己上手 | 中 | 引导验证 |
| Remote Async | 20+ | 大样本量化 | 低（平台） | A/B 比较 |
| Public Playtest | 100+ | 真实环境数据 | 低 | beta 前 |

## 关键流程

### 流程 A：招募
- **人数**：定性测试 5-8 人即可发现 80% 可用性问题（Nielsen 法则）；定量 ≥ 30
- **画像**：和目标受众一致——重度 / 轻度 / 非品类玩家分层
- **奖励**：1 小时 session 给 $30-50 美元 / Steam 充值卡，或免费 DLC
- **NDA**：alpha/beta 强制签电子 NDA（DocuSign）；公测可省
- **筛选问卷**：5 题筛掉 bot 和不符画像，含「最近 30 天玩过什么游戏」开放题

### 流程 B：Session 结构（60min one-on-one）
```
0-5min  破冰 + 背景问卷（玩什么游戏、玩多久）
5-10min 介绍 think-aloud 规则：「请把脑子里想的说出来」
10-45min 自由游玩（主持人 silent 观察，仅在卡死 90s+ 提示）
45-55min Post-test 访谈（见流程 D）
55-60min 发放问卷 SUS + NPS + 自定义
```

### 流程 C：观察记录（Observation Sheet）
不要事后凭记忆，**同步打字**到 sheet：

| Time | Player Action | Player Quote | Observer Note | Severity |
|---|---|---|---|---|
| 02:15 | 在主菜单徘徊 18s | "这个开始按钮在哪？" | START 按钮颜色对比度低 | High |
| 05:30 | 跳过教程 | "我以为我会自己学" | 教程跳过率需埋点 | Medium |

**Severity**：
- **Critical**：阻塞，玩家放弃
- **High**：明显挫败但能继续
- **Friction**：轻微犹豫
- **Nice-to-have**：建议但不影响

### 流程 D：Post-test Interview 引导词
开放式问题，避免诱导：
- "整体感受如何？" （不要问「好玩吗」）
- "印象最深的瞬间是什么？为什么？"
- "有没有哪个时刻你感到困惑/沮丧？"
- "如果给一个朋友推荐，你会怎么形容这个游戏？"（NPS 验证）
- "有什么时刻让你想关掉游戏？"
- "你会愿意为它付多少钱？" （仅商业化测试问）

**禁问**：
- 「你喜欢这个功能吗」（leading）
- 「你觉得我们应该改 X 吗」（让玩家做设计）

### 流程 E：问卷模板
**SUS（10 题，1-5 Likert）**，标准计算：奇数题(score-1) + 偶数题(5-score)，乘 2.5，满分 100。**> 68 = 通过**。

简化版（5 题足够）：
1. 我想经常玩这个游戏
2. 我觉得游戏不必要地复杂
3. 我觉得游戏容易上手
4. 我需要有人指导才能玩
5. 我觉得游戏的各种功能整合得很好

**NPS**：「0-10 分，你向朋友推荐这个游戏的意愿是？」
- 9-10 = Promoter / 7-8 = Passive / 0-6 = Detractor
- NPS = %Promoter - %Detractor，**> 30 健康，> 50 优秀**

### 流程 F：关键指标
- **Time-to-first-fail (TTFF)**：玩家首次遭遇明显挫败的时间
- **Time-to-first-fun (TTFF2)**：玩家首次表现出明显愉悦
- **Drop-off Point**：标注流程图上玩家退出位置
- **Tutorial Completion Rate**：完成新手引导比例
- **Retention Proxy**：「明天还想玩吗」自评（无法做长留存时的代理）
- **Frown/Smile Count**：观察期间情绪表征次数

### 流程 G：结果分类与迭代
按 Severity 分类后用 RICE 排序（Reach × Impact × Confidence ÷ Effort）：
- 全部 Critical 必须修
- High 修 80%
- Friction 修高 Reach 的
- Nice-to-have 进 backlog

**迭代节奏**：每 2 周一次 small playtest（3-5 人），每月一次大 playtest（10-15 人）。

## 常见坑
- **样本偏差**：朋友/同事 playtest 友善度爆表，结果误导；必须外部招募
- **主持人诱导**：玩家卡住时忍不住提示，污染数据；规则是 **90 秒沉默**才介入
- **only-vocal-bias**：会表达的玩家观点权重过高，安静的玩家也要追问
- **过度自报**：玩家说「我觉得这里要 X」≠ 真需求；看行为 > 听陈述
- **NDA 漏洞**：未签 NDA 玩家把 alpha 截图发推；用电子 NDA + 水印构建
- **录屏未授权**：欧盟 GDPR 必须明确同意录屏，分开授权
- **单次 session 太长**：> 90min 玩家疲劳数据失真，分两次
- **测试时段不分层**：只测周末玩家，工作日通勤玩家场景没覆盖

## 模板/产物

### Playtest Plan 模板
```markdown
# Playtest Plan: [Build vX.Y]
## 目标
- 验证 [新手引导改版] 是否降低 Tutorial Drop-off
- 收集对 [新角色 Lina] 的第一印象

## 假设
- H1: 新引导 Drop-off 从 35% 降到 < 20%
- H2: Lina 角色 NPS ≥ 40

## 方法
- 类型: One-on-one Remote (Zoom)
- 人数: 8 人（4 老玩家 + 4 新玩家）
- 时长: 60 min
- 招募平台: PlaytestCloud
- 奖励: $40 Amazon gift card

## 任务清单
1. 完成新手引导（observe drop-off）
2. 自由游玩 20min（observe friction）
3. 抽到 Lina 角色用 5min（observe first impression）

## 成功标准
- Tutorial 完成率 ≥ 75%
- 至少 6/8 玩家提到「想再玩」
- SUS ≥ 70
```

### 观察 Sheet（实时填）
| T+ | Action | Quote | Note | Sev |
|---|---|---|---|---|
| | | | | |

### Post-Playtest Report 结构
1. **TL;DR**（3 句话结论）
2. **关键发现**（按 Severity 排）
3. **量化结果**（SUS、NPS、完成率）
4. **代表性视频片段**（30s 内）
5. **行动项**（owner + 截止日期）
6. **下次 playtest 计划**
