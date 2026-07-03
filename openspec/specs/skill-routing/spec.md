# skill-routing Specification

## Purpose
TBD - created by archiving change 02-skill-routing-unification. Update Purpose after archive.
## Requirements
### Requirement: SKILL 白名单为硬墙

agent MUST 仅调用其 `frontmatter.skills` 列出的 SKILL；列表外的 SKILL MUST 立即 `escalate_to: main`，由主对话决定是否调用 `find-skills` 后再委派。

#### Scenario: agent 调用白名单内 SKILL
- **GIVEN** `producer.md` 的 frontmatter.skills 含 `openspec`
- **WHEN** producer 在任务中调用 openspec
- **THEN** 直接执行，不需要升级

#### Scenario: agent 调用白名单外 SKILL
- **GIVEN** `client-lead.md` 的 frontmatter.skills 不含 `ai-art`
- **WHEN** client-lead 任务里出现需要 ai-art 的工作
- **THEN** 立即 escalate_to: main，禁止自行 find-skills 后调用

### Requirement: 跨 agent 共享 SKILL 显式登记

高频共享 SKILL MUST 显式列入每个相关 agent 的 `frontmatter.skills`，不依赖隐式继承。

#### Scenario: openspec 进 4 lead 白名单
- **GIVEN** producer / gd-lead / client-lead / net-lead 都是 lead 层
- **WHEN** 任意 lead 触发决策门槛需要落 spec
- **THEN** 其 frontmatter.skills 已含 `openspec`，可直接调用

#### Scenario: ai-art 进 3 art 白名单
- **GIVEN** art-director / art-2d / art-ui 三个 art-* agent
- **WHEN** 任意 art-* 需要写美术提示词
- **THEN** 其 frontmatter.skills 已含 `ai-art`，可直接调用

#### Scenario: deep-research 进 producer/gd-lead 白名单
- **GIVEN** producer 与 gd-lead 是项目研究主力
- **WHEN** 需要联网研究竞品/玩法
- **THEN** 两 agent 的 frontmatter.skills 已含 `deep-research`

### Requirement: agent prompt 措辞统一硬墙

7 个 lead/art-* agent 的 prompt 文案 MUST 删除「或 find-skills」式半墙措辞。

#### Scenario: grep 验收半墙
- **WHEN** 执行 `grep -r "或 find-skills" .claude/agents/`
- **THEN** 输出 0 行

### Requirement: gitnexus SKILL 体系退役

代码索引能力 MUST 由 `codebase-memory` MCP 承担；gitnexus SKILL、`.claude/GITNEXUS.md`、SKILLS_INDEX/CLAUDE/SKILL_MATRIX 中的所有引用 MUST 清除。

#### Scenario: 仓库全局无 gitnexus 引用
- **WHEN** 执行 `grep -ri "gitnexus\|GITNEXUS" .claude/` 排除「历史：」注释
- **THEN** 输出 0 行

#### Scenario: 代码分析路径指向 codebase-memory MCP
- **GIVEN** 用户需要分析代码结构 / 影响范围 / Bug 追踪
- **WHEN** 查阅 SKILLS_INDEX 快速匹配表
- **THEN** 应看到 `→ codebase-memory MCP（详见 CLAUDE.md §八）`，不再有 gitnexus 字样

### Requirement: deep-research 解锁

`deep-research/SKILL.md` 的 frontmatter MUST NOT 含 `user-invocable: false`，须允许 agent 直接调用。

#### Scenario: deep-research frontmatter 已解锁
- **WHEN** 读取 `.claude/skills/deep-research/SKILL.md` 前 20 行
- **THEN** 不含 `user-invocable: false`

