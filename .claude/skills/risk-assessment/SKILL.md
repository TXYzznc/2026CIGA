---
name: risk-assessment
description: 使用定性和定量方法识别、分析并优先处理项目风险。制定缓解策略以降低影响并提高项目成功概率。
tags: project-risk-management, risk-assessment, risk-mitigation, risk-monitoring,
  qualitative-quantitative-analysis
tags_cn: 项目风险管理, 风险评估, 风险缓解策略, 风险监控, 定性定量分析
---

# 项目风险评估

## 概述

风险评估是一个系统性的流程，用于识别可能威胁项目成功的潜在因素，并制定缓解、规避或接受这些风险的策略。

## 适用场景

- 项目启动与规划阶段
- 重要里程碑或决策之前
- 引入新技术时
- 存在第三方依赖或集成需求时
- 组织架构或资源发生变化时
- 存在预算或时间限制时
- 涉及监管或合规相关问题时

## 操作指南

### 1. **风险识别方法**

```python
# Risk identification framework

class RiskIdentification:
    RISK_CATEGORIES = {
        'Technical': [
            'Technology maturity',
            'Integration complexity',
            'Performance requirements',
            'Security vulnerabilities',
            'Data integrity'
        ],
        'Resource': [
            'Team skill gaps',
            'Staff availability',
            'Budget constraints',
            'Equipment/infrastructure',
            'Vendor availability'
        ],
        'Schedule': [
            'Unrealistic deadlines',
            'Dependency delays',
            'Scope creep',
            'Approval delays',
            'Resource conflicts'
        ],
        'External': [
            'Regulatory changes',
            'Market conditions',
            'Vendor stability',
            'Political/economic factors',
            'Natural disasters'
        ],
        'Organizational': [
            'Stakeholder misalignment',
            'Priority changes',
            'Organizational restructuring',
            'Politics/conflicts',
            'Requirement changes'
        ]
    }

    @staticmethod
    def brainstorm_risks(project_context):
        """
        Facilitated brainstorming session to identify risks
        """
        risks = []
        for category, risk_types in RiskIdentification.RISK_CATEGORIES.items():
            for risk_type in risk_types:
                risks.append({
                    'category': category,
                    'description': risk_type,
                    'identified_by': [],
                    'probability': None,
                    'impact': None
                })

        return risks

    @staticmethod
    def analyze_assumptions_as_risks(assumptions):
        """
        Convert project assumptions into potential risks
        """
        assumption_risks = []
        for assumption in assumptions:
            assumption_risks.append({
                'risk_type': 'Assumption Violation',
                'description': f"Assumption '{assumption}' is invalid",
                'trigger': f"Evidence that {assumption} is false",
                'impact': 'High' if assumption.startswith('Critical') else 'Medium'
            })

        return assumption_risks
```

### 2. **风险分析矩阵**

```javascript
// Qualitative and quantitative risk analysis

class RiskAnalysis {
  constructor() {
    this.riskMatrix = [];
    this.priorityMap = [];
  }

  // Probability scale 1-5
  static PROBABILITY = {
    1: { name: 'Very Low', percentage: 0.1, color: 'Green' },
    2: { name: 'Low', percentage: 0.3, color: 'Green' },
    3: { name: 'Medium', percentage: 0.5, color: 'Yellow' },
    4: { name: 'High', percentage: 0.7, color: 'Orange' },
    5: { name: 'Very High', percentage: 0.9, color: 'Red' }
  };

  // Impact scale 1-5
  static IMPACT = {
    1: { name: 'Negligible', value: 1, scope: 'Minor inconvenience' },
    2: { name: 'Minor', value: 10, scope: 'Some delay or cost' },
    3: { name: 'Moderate', value: 100, scope: 'Significant delay or cost' },
    4: { name: 'Major', value: 1000, scope: 'Critical failure risk' },
    5: { name: 'Catastrophic', value: 10000, scope: 'Project cancellation' }
  };

  analyzeRisk(risk) {
    const probability = this.PROBABILITY[risk.probability];
    const impact = this.IMPACT[risk.impact];

    // Risk Score = Probability × Impact
    const riskScore = risk.probability * risk.impact;

    // Risk Exposure = Probability × Financial Impact
    const riskExposure = probability.percentage * impact.value;

    return {
      riskId: risk.id,
      riskScore,
      riskExposure,
      priority: this.calculatePriority(riskScore),
      severity: this.calculateSeverity(riskScore),
      mitigationUrgency: riskExposure > 100 ? 'Immediate' : 'Planned'
    };
  }

  calculatePriority(riskScore) {
    if (riskScore >= 16) return 'Critical';
    if (riskScore >= 12) return 'High';
    if (riskScore >= 6) return 'Medium';
    if (riskScore >= 2) return 'Low';
    return 'Very Low';
  }

  calculateSeverity(riskScore) {
    return {
      score: riskScore,
      rating: this.calculatePriority(riskScore),
      responseNeeded: riskScore >= 12
    };
  }

  // Risk Matrix
  createRiskMatrix(risks) {
    const matrix = {
      critical: [],
      high: [],
      medium: [],
      low: [],
      veryLow: []
    };

    risks.forEach(risk => {
      const analysis = this.analyzeRisk(risk);
      const priority = analysis.priority.toLowerCase();

      if (matrix[priority]) {
        matrix[priority].push({
          ...risk,
          ...analysis
        });
      }
    });

    return matrix;
  }
}
```

### 3. **风险应对规划**

```yaml
Risk Response Strategies:

Risk 1: Integration Delay with Third-Party API
  Probability: High (4/5)
  Impact: Major (4/5)
  Risk Score: 16 (Critical)

  Response Strategy: MITIGATION

  Actions:
    - Engage vendor early in planning (Week 1)
    - Develop fallback solution in parallel (Week 2-4)
    - Allocate 20% more development time (buffer)
    - Weekly sync with vendor team
    - Performance testing starts Month 2

  Owner: Technical Lead
  Budget Impact: +$15,000
  Timeline: 6 weeks vs. 4 weeks planned

---

Risk 2: Scope Creep from Stakeholders
  Probability: High (4/5)
  Impact: Moderate (3/5)
  Risk Score: 12 (High)

  Response Strategy: AVOIDANCE & MITIGATION

  Actions:
    - Establish change control process (Week 1)
    - Lock requirements for Phase 1 (Week 2)
    - Monthly scope review meetings
    - Create feature backlog for Phase 2
    - Strict change request evaluation criteria

  Owner: Project Manager
  Cost of Avoidance: 5 hours/week PM time
  Alternative: Accept 2-week timeline extension

---

Risk 3: Key Person Departure
  Probability: Medium (3/5)
  Impact: Major (4/5)
  Risk Score: 12 (High)

  Response Strategy: MITIGATION & CONTINGENCY

  Actions:
    - Knowledge transfer documentation (ongoing)
    - Cross-training second developer (Week 1)
    - Maintain up-to-date runbooks
    - Competitive salary review (HR)
    - Mentoring program setup

  Owner: HR Manager
  Contingency: Hire contractor within 1 week
  Estimated Cost: $20,000
```

### 4. **风险监控与控制**

```javascript
// Risk tracking and monitoring dashboard

class RiskMonitoring {
  constructor() {
    this.risks = [];
    this.triggers = [];
    this.escalations = [];
  }

  createRiskRegister(risks) {
    return risks.map((risk, index) => ({
      id: `RK-${String(index + 1).padStart(3, '0')}`,
      description: risk.description,
      category: risk.category,
      probability: risk.probability,
      impact: risk.impact,
      riskScore: risk.probability * risk.impact,
      responseStrategy: risk.strategy,
      owner: risk.owner,
      status: 'Active',
      triggers: risk.triggers,
      contingencyPlan: risk.contingency,
      createdDate: new Date(),
      lastReviewDate: new Date(),
      closeDate: null
    }));
  }

  identifyRiskTriggers(risk) {
    return {
      riskId: risk.id,
      triggers: [
        {
          trigger: 'Vendor communication delay >1 week',
          indicator: 'No response from vendor',
          escalationAction: 'Contact vendor PM, evaluate alternatives'
        },
        {
          trigger: 'Team member absence >3 days',
          indicator: 'Unplanned time off',
          escalationAction: 'Activate cross-training plan'
        },
        {
          trigger: 'Performance test fails baseline',
          indicator: 'Response time > 500ms',
          escalationAction: 'Emergency optimization sprint'
        }
      ],
      reviewFrequency: 'Weekly standup'
    };
  }

  monitorRisks(riskRegister) {
    const statusReport = {
      timestamp: new Date(),
      summary: {
        total: riskRegister.length,
        active: riskRegister.filter(r => r.status === 'Active').length,
        mitigated: riskRegister.filter(r => r.status === 'Mitigated').length,
        closed: riskRegister.filter(r => r.status === 'Closed').length
      },
      criticalRisks: riskRegister.filter(r => r.riskScore >= 16),
      highRisks: riskRegister.filter(r => r.riskScore >= 12 && r.riskScore < 16),
      triggeredRisks: riskRegister.filter(r => r.triggered === true)
    };

    return statusReport;
  }
}
```

## 最佳实践

### ✅ 建议
- 在项目规划早期识别风险
- 让不同角色的团队成员参与风险识别
- 尽可能量化风险影响
- 根据风险评分和暴露程度确定优先级
- 制定具体的缓解计划
- 明确风险负责人
- 定期监控风险触发条件
- 每月回顾并更新风险登记册
- 记录已发生风险的经验教训
- 向利益相关者透明沟通风险情况

### ❌ 禁忌
- 等到问题出现才开始识别风险
- 假设风险不会发生
- 对所有风险一视同仁
- 在没有明确触发条件的情况下制定缓解计划
- 忽视早期预警信号
- 将风险管理视为一次性活动
- 不为关键风险制定应急计划
- 向利益相关者隐瞒负面风险
- 试图消除所有风险（既不可能也不经济）
- 因风险发生而指责个人

## 风险管理小贴士

- 明确风险负责人能提升问责意识
- 定期回顾风险可避免意外情况
- 风险应对措施应具备成本效益
- 适度的风险容忍度是合理且必要的
- 文档化的风险更易于管理