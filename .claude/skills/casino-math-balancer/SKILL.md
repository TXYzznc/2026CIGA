---
name: casino-math-balancer
description: 计算并平衡赌场游戏的数学模型，包括赔率、RTP（玩家回报率）、庄家优势、方差以及派彩表。适用于设计投注机制、平衡元奖池系统、创建概率表、验证游戏经济数学模型，或确保公平且盈利的游戏机制。当涉及赌博数学计算、赔率核算、派彩平衡或RTP优化的需求时可使用。
tags: casino-game-math, rtp-calculation, house-edge-analysis, variance-management,
  game-economy-design
tags_cn: 赌场游戏数学, RTP计算, 庄家优势分析, 方差管理, 游戏经济设计
---

# 赌场游戏数学平衡工具

用于设计平衡、有吸引力且具备合理风险回报曲线的投注机制的数学框架。

## 核心指标

### Return to Player (RTP)

```
RTP = (Total Amount Returned to Players / Total Amount Wagered) × 100

Target ranges by game type:
- Casual/Social: 95-98% RTP (player-friendly)
- Balanced: 92-95% RTP (sustainable)  
- High-stakes: 88-92% RTP (house-favorable)
```

不同游戏类型的目标范围：
- 休闲/社交类：95-98% RTP（对玩家友好）
- 平衡类：92-95% RTP（可持续盈利）
- 高赌注类：88-92% RTP（对庄家有利）

### House Edge

```
House Edge = 100% - RTP

Example:
RTP = 96% → House Edge = 4%
For every 100 coins wagered, house keeps ~4 coins long-term
```

示例：
RTP = 96% → 庄家优势 = 4%
每投注100枚筹码，庄家长期来看会留存约4枚筹码

### 方差分类

| 方差等级 | 命中频率 | 最大赢额 | 游戏体验 |
|----------|---------------|---------|------------|
| 低 | >40% | 2-10倍 | 稳定的小额获胜 |
| 中 | 20-40% | 10-50倍 | 平衡的刺激感 |
| 高 | <20% | 50-500倍 | 罕见大额获胜 |

## 元奖池系统数学模型（Purria中的农场玩法）

### 奖池分类

| 奖池类型 | 概率范围 | 建议RTP | 说明 |
|-----|------------------|---------------|-------|
| 水奖池 | 45-65% 成功率 | 96% | 最稳定 |
| 日奖池 | 35-55% 成功率 | 94% | 中等方差 |
| 虫奖池 | 25-45% 成功率 | 92% | 高风险高回报 |
| 成长奖池 | 15-35% 成功率 | 90% | 头奖级玩法 |

### 投注等级计算

```typescript
interface BetCalculation {
  level: 'fold' | 'call' | 'all_in';
  amount: number;
  multiplier: number;
  potentialWin: number;
  expectedValue: number;
}

function calculateBet(
  coins: number, 
  level: BetLevel, 
  potSuccess: number,
  multiplier: number
): BetCalculation {
  const amounts = {
    fold: 0,
    call: Math.floor(coins * 0.1),
    all_in: coins
  };
  
  const amount = amounts[level];
  const potentialWin = Math.floor(amount * multiplier);
  const expectedValue = (potSuccess * potentialWin) - ((1 - potSuccess) * amount);
  
  return { level, amount, multiplier, potentialWin, expectedValue };
}
```

### 乘数平衡公式

```
Multiplier = (1 / Win_Probability) × RTP_Target

Example for 40% win rate at 94% RTP:
Multiplier = (1 / 0.40) × 0.94 = 2.35x

Payout table:
- Call bet (10%): Win = 2.35x stake
- All-in: Win = 2.35x stake (same multiplier, higher stakes)
```

示例：胜率40%且目标RTP为94%时：
乘数 = (1 / 0.40) × 0.94 = 2.35倍

派彩表：
- 跟注（10%筹码）：获胜可得2.35倍投注额
- 全押：获胜可得2.35倍投注额（乘数相同，投注额更高）

## 概率表

### 标准模板

```markdown
| Outcome | Probability | Payout | Contribution to RTP |
|---------|-------------|--------|---------------------|
| Win     | P%          | Mx     | P × M              |
| Push    | Q%          | 1x     | Q × 1              |
| Lose    | R%          | 0x     | 0                  |
| TOTAL   | 100%        | -      | RTP%               |
```

### 示例：元奖池结算

```markdown
| Pot State | Probability | Payout | RTP Contribution |
|-----------|-------------|--------|------------------|
| ≥80%      | 15%         | 3.0x   | 45%             |
| 50-79%    | 35%         | 1.8x   | 63%             |
| 20-49%    | 30%         | 0.5x   | 15%             |
| <20%      | 20%         | 0x     | 0%              |
| TOTAL     | 100%        | -      | 123% → adjust   |

Adjustment needed: Scale payouts by 0.94/1.23 = 0.764
New payouts: 2.29x, 1.38x, 0.38x, 0x → RTP ≈ 94%
```

需要调整：将派彩额乘以0.94/1.23 = 0.764
新派彩额：2.29倍、1.38倍、0.38倍、0倍 → RTP ≈ 94%

## 平衡调节手段

### 可调参数

| 调节项 | 对玩家的影响 | 对收益的影响 |
|-------|-------------------|-------------------|
| ↑ 基础胜率 | 提升玩家参与度 | ↓ 庄家优势 |
| ↑ 最大乘数 | 提升刺激感 | 方差风险增加 |
| ↑ 等级阈值 | 大额获胜难度提升 | ↑ 庄家优势 |
| ↓ 最低投注额 | 提升游戏可及性 | ↓ 单注收益 |

### 单局游戏经济模型

```
Target metrics per session:
- Average session: 10-15 bets
- Net outcome: -5% to +20% of starting bankroll
- "Near miss" rate: 15-20% (engagement driver)
- Big win frequency: 1 in 20-50 sessions
```

单局游戏目标指标：
- 平均单局投注次数：10-15次
- 净结果：初始筹码的-5%至+20%
- "差一点获胜"概率：15-20%（提升参与度的关键）
- 大额获胜频率：每20-50局出现一次

## 模拟验证

### 蒙特卡洛模拟模板

```typescript
function simulateSession(
  startingCoins: number,
  betsPerSession: number,
  betSize: number,
  winProb: number,
  winMultiplier: number
): SimulationResult {
  let coins = startingCoins;
  let wins = 0;
  
  for (let i = 0; i < betsPerSession; i++) {
    coins -= betSize;
    if (Math.random() < winProb) {
      coins += betSize * winMultiplier;
      wins++;
    }
  }
  
  return {
    finalCoins: coins,
    netChange: coins - startingCoins,
    winRate: wins / betsPerSession,
    rtp: (coins + (betsPerSession * betSize) - startingCoins) / (betsPerSession * betSize)
  };
}

// Run 10,000 sessions to validate RTP converges to target
```

// 运行10,000局模拟以验证RTP是否收敛至目标值

## 风险预警清单

在上线任何投注机制前，请检查：

- [ ] 已计算RTP且处于目标范围内
- [ ] 已分类方差等级且符合目标受众
- [ ] 随机数生成器（RNG）无可利用的规律
- [ ] 单局最大损失已设置上限
- [ ] 连胜次数不超过5次（无冷却）
- [ ] 连败触发怜悯机制
- [ ] 游戏经济不会随时间膨胀
- [ ] 已通过模拟验证数学模型（运行10,000+局）

## 法律注意事项

对于社交赌场/非赌博类游戏：
- 不可进行真实货币兑换
- 虚拟货币需明确标注
- 建议披露赔率
- 需设置年龄限制
- 需标注“仅供娱乐”的提示语