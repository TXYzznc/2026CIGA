---
name: task-estimation
description: 使用多种技术准确估算软件开发任务。适用于冲刺规划、路线图制定或项目时间线规划场景。支持Story Points、T恤尺寸估算、计划扑克以及估算最佳实践。
tags: task-estimation, agile-practices, sprint-planning, story-points, planning-poker
platforms:
- Claude
- ChatGPT
- Gemini
tags_cn: 任务估算, 敏捷实践, Sprint规划, Story Points, 计划扑克
---

# 任务估算


## 何时使用该技能

- **Sprint Planning**: 确定要纳入冲刺的任务
- **Roadmap 制定**: 制定长期规划
- **资源规划**: 估算团队规模及日程

## 操作指南

### Step 1: Story Points (相对估算)

**Fibonacci 序列**: 1, 2, 3, 5, 8, 13, 21

```markdown
## Story Point 标准

### 1 Point (极小)
- 示例: 文本修改、常量值调整
- 时间: 1-2小时
- 复杂度: 极低
- 风险: 无

### 2 Points (小)
- 示例: 简单的Bug修复、添加日志
- 时间: 2-4小时
- 复杂度: 低
- 风险: 低

### 3 Points (中)
- 示例: 简单CRUD API端点
- 时间: 4-8小时
- 复杂度: 中等
- 风险: 低

### 5 Points (中-大)
- 示例: 复杂表单实现、认证中间件
- 时间: 1-2天
- 复杂度: 中等
- 风险: 中等

### 8 Points (大)
- 示例: 新功能（前端+后端）
- 时间: 2-3天
- 复杂度: 高
- 风险: 中等

### 13 Points (极大)
- 示例: 支付系统集成
- 时间: 1周
- 复杂度: 极高
- 风险: 高
- **建议**: 拆分为更小的任务

### 21+ Points (史诗级)
- **必须**: 务必拆分为更小的用户故事
```

### Step 2: Planning Poker

**流程**:
1. 产品负责人讲解用户故事
2. 团队成员提问
3. 各自选择卡片（1, 2, 3, 5, 8, 13）
4. 同时公开卡片
5. 最高分/最低分持有者说明理由
6. 重新投票
7. 达成共识

**示例**:
```
Story: "用户可上传个人资料照片"

团队成员A: 3 points (前端实现简单)
团队成员B: 5 points (需要图片缩放)
团队成员C: 8 points (需考虑S3上传、安全问题)

讨论:
- 使用图片处理库
- S3已配置完成
- 需要进行文件大小验证

重新投票 → 达成5 points的共识
```

### Step 3: T-Shirt Sizing (快速估算)

```markdown
## T-Shirt 尺寸

- **XS**: 1-2 Story Points (1小时以内)
- **S**: 2-3 Story Points (半天)
- **M**: 5 Story Points (1-2天)
- **L**: 8 Story Points (1周)
- **XL**: 13+ Story Points (需要拆分)

**适用场景**:
- 初期产品待办事项整理
- 大致路线图制定
- 快速优先级排序
```

### Step 4: 考虑风险与不确定性

**估算调整**:
```typescript
interface TaskEstimate {
  baseEstimate: number;      // 基础估算
  risk: 'low' | 'medium' | 'high';
  uncertainty: number;        // 0-1
  finalEstimate: number;      // 调整后的估算
}

function adjustEstimate(estimate: TaskEstimate): number {
  let buffer = 1.0;

  // 风险缓冲
  if (estimate.risk === 'medium') buffer *= 1.3;
  if (estimate.risk === 'high') buffer *= 1.5;

  // 不确定性缓冲
  buffer *= (1 + estimate.uncertainty);

  return Math.ceil(estimate.baseEstimate * buffer);
}

// 示例
const task = {
  baseEstimate: 5,
  risk: 'medium',
  uncertainty: 0.2  // 20% 不确定性
};

const final = adjustEstimate(task);  // 5 * 1.3 * 1.2 = 7.8 → 8 points
```

## 输出格式

### 估算文档模板

```markdown
## Task: [任务名称]

### Description
[任务内容说明]

### Acceptance Criteria
- [ ] 标准1
- [ ] 标准2
- [ ] 标准3

### Estimation
- **Story Points**: 5
- **T-Shirt Size**: M
- **Estimated Time**: 1-2 days

### Breakdown
- Frontend UI: 2 points
- API Endpoint: 2 points
- Testing: 1 point

### Risks
- API响应速度不确定（中等风险）
- 依赖外部库（低风险）

### Dependencies
- 需先完成用户认证

### Notes
- 需与UX团队讨论设计方案
```

## 约束条件

### 必须遵守的规则（MUST）

1. **相对估算**: 使用相对复杂度而非绝对时间
2. **团队共识**: 需团队全体达成共识而非个人决定
3. **参考历史数据**: 基于团队速度（Velocity）制定计划

### 禁止事项（MUST NOT）

1. **给个人施压**: 估算不等于承诺
2. **过度细化估算**: 13+ points的任务必须拆分
3. **将估算作为截止日期**: 估算≠保证

## 最佳实践

1. **拆分任务**: 大任务拆分为小任务
2. **参考历史任务**: 参考过往类似任务
3. **预留缓冲时间**: 应对意外情况

## 参考资料

- [Scrum Guide](https://scrumguides.org/)
- [Planning Poker](https://www.planningpoker.com/)
- [Story Points](https://www.atlassian.com/agile/project-management/estimation)

## 元数据

### 版本
- **当前版本**: 1.0.0
- **最后更新**: 2025-01-01
- **兼容平台**: Claude, ChatGPT, Gemini

### 标签
`#estimation` `#agile` `#story-points` `#planning-poker` `#sprint-planning` `#project-management`

## 示例

### 示例1: 基础用法
<!-- 在此添加示例内容 -->

### 示例2: 高级用法
<!-- 在此添加高级示例内容 -->
