---
name: algo-rank-trueskill
description: 为多人及团队型竞技排名实现TrueSkill评分系统。当用户需要对团队游戏中的玩家进行评分、处理多人（非1v1）对局，或构建具备不确定性追踪的匹配系统时使用该技能——即使用户提及‘团队评分系统’、‘多人排名’或‘匹配评分’也适用。
metadata:
  category: WP-44 排名演算法
  tags:
  - ranking
  - trueskill
  - matchmaking
  - bayesian-rating
tags: trueskill-rating, multiplayer-ranking, matchmaking-system, bayesian-inference,
  skill-uncertainty
tags_cn: TrueSkill评分系统, 多人竞技排名, 匹配系统构建, 贝叶斯推理, 技能不确定性追踪
---

# TrueSkill评分系统

## 概述

TrueSkill（微软研究院提出）将每位玩家的技能建模为高斯分布N(μ, σ²)，其中μ为预估技能值，σ为不确定性。支持团队和多人模式（不仅限于1v1）。保守评分为μ - 3σ。随着对局次数增加，不确定性σ会降低。通过消息传递的方式采用贝叶斯推理。

## 使用场景

**触发条件：**
- 对团队类或多人（3名及以上参与者）游戏中的玩家进行评分
- 构建平衡对局质量的匹配系统
- 需要在技能评分之外获取不确定性估计时

**不适用场景：**
- 无需不确定性的简单1v1排名（Elo算法更简便）
- 非竞技类排名（如产品评分——应使用Wilson Score）

## 算法流程

```
IRON LAW: Skill Rating Has TWO Components — Mean AND Uncertainty
TrueSkill represents skill as N(μ, σ²). New players have high σ
(uncertain). After many games, σ shrinks (confident). The conservative
rating μ - 3σ ensures players are ranked by their LIKELY MINIMUM
skill, not their estimated average. Never use μ alone for ranking.
```

### 阶段1：输入验证
初始化：μ₀ = 25，σ₀ = 25/3（默认值）。收集包含团队组成和排名结果的对局数据。
**校验点**：对局结果有效，团队组成明确。

### 阶段2：核心算法
1. 针对每一场对局，根据团队技能分布计算预期结果
2. 对比实际结果与预期结果
3. 使用贝叶斯更新法更新每位玩家的(μ, σ)：
   - μ会向实际表现方向偏移（获胜者上升，失败者下降）
   - σ会降低（观察到结果后不确定性减少）
   - 更新幅度与σ成正比（不确定性高的玩家评分变化更大）
4. 保守排名 = μ - 3σ

### 阶段3：验证
检查：活跃玩家的σ随时间推移逐渐降低。按保守评分排名靠前的玩家获胜次数高于预期。对局质量指标（平局概率）合理。
**校验点**：评分系统生成的排名符合直觉，σ趋于收敛。

### 阶段4：输出
返回包含不确定性范围的玩家评分。

## 输出格式

```json
{
  "ratings": [{"player": "P1", "mu": 32.5, "sigma": 2.1, "conservative": 26.2, "games_played": 50}],
  "metadata": {"initial_mu": 25, "initial_sigma": 8.33, "beta": 4.17, "tau": 0.083}
}
```

## 示例

### 输入输出样例
**输入**：团队[A(25,8.3), B(25,8.3)]击败团队[C(25,8.3), D(25,8.3)]
**预期结果**：A、B的μ值上升约2-3分，σ值下降约0.5。C、D的μ值下降，σ值也下降。保守评分相应调整。

### 边缘情况
| 输入 | 预期结果 | 原因 |
|-------|----------|-----|
| 新手玩家 vs 资深玩家 | 新手玩家的μ值变化更大 | σ值越高（不确定性越强），评分更新幅度越大 |
| 1v1对局 | 退化为类Elo算法的表现 | TrueSkill在1v1场景下会简化为简单模式 |
| 自由对战（8名玩家） | 所有玩家两两对比 | 原生支持多人模式，与Elo算法不同 |

## 注意事项

- **计算成本**：因子图中的消息传递比Elo算法更耗时。对于数百万玩家规模的场景，需使用近似方法（如EP截断）。
- **团队技能聚合**：TrueSkill将个体高斯分布求和得到团队技能。这假设玩家技能相互独立——对于配合熟练的团队，其技能相关性未被充分建模。
- **动态技能**：σ只会降低。如果玩家的技能确实发生变化（提升或下降），需添加一个小的漂移项τ，随时间逐步增大σ。
- **部分参与对局**：若玩家中途加入或提前退出，其贡献难以界定。需使用部分参与权重扩展方案。
- **专利状态**：TrueSkill由微软申请专利（2024年已过期）。TrueSkill 2增加了更多功能，但需注意许可问题。

## 参考资料

- 关于TrueSkill因子图推导，请参阅`references/factor-graph.md`
- 关于对局匹配质量指标，请参阅`references/matchmaking.md`