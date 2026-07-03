---
name: testing-strategies
description: 为软件质量保证设计全面的测试策略。适用于规划测试覆盖范围、实施测试金字塔或搭建测试基础设施的场景。涵盖单元测试、集成测试、端到端（E2E）测试、测试驱动开发（TDD）以及测试最佳实践。
tags: test-strategy, unit-testing, integration-testing, e2e-testing, tdd-practices
platforms:
- Claude
- ChatGPT
- Gemini
tags_cn: 测试策略, 单元测试, 集成测试, E2E测试, TDD实践
---

# 测试策略


## 何时使用该技能

- **新项目**：制定测试策略
- **质量问题**：频繁出现漏洞
- **重构前**：搭建安全保障体系
- **搭建CI/CD**：自动化测试

## 操作指南

### 步骤1：理解测试金字塔

```
       /\
      /E2E\          ← 少量（速度慢、成本高）
     /______\
    /        \
   /Integration\    ← 中等数量
  /____________\
 /              \
/   Unit Tests   \  ← 大量（速度快、成本低）
/________________\
```

**比例指南**:
- 单元测试：70%
- 集成测试：20%
- E2E测试：10%

### 步骤2：单元测试策略

**Given-When-Then模式**:
```typescript
describe('calculateDiscount', () => {
  it('should apply 10% discount for orders over $100', () => {
    // Given: 给定场景
    const order = { total: 150, customerId: '123' };

    // When: 执行操作
    const discount = calculateDiscount(order);

    // Then: 验证结果
    expect(discount).toBe(15);
  });

  it('should not apply discount for orders under $100', () => {
    const order = { total: 50, customerId: '123' };
    const discount = calculateDiscount(order);
    expect(discount).toBe(0);
  });

  it('should throw error for invalid order', () => {
    const order = { total: -10, customerId: '123' };
    expect(() => calculateDiscount(order)).toThrow('Invalid order');
  });
});
```

**Mocking策略**:
```typescript
// 模拟外部依赖
jest.mock('../services/emailService');
import { sendEmail } from '../services/emailService';

describe('UserService', () => {
  it('should send welcome email on registration', async () => {
    // Arrange
    const mockSendEmail = sendEmail as jest.MockedFunction<typeof sendEmail>;
    mockSendEmail.mockResolvedValueOnce(true);

    // Act
    await userService.register({ email: 'test@example.com', password: 'pass' });

    // Assert
    expect(mockSendEmail).toHaveBeenCalledWith({
      to: 'test@example.com',
      subject: 'Welcome!',
      body: expect.any(String)
    });
  });
});
```

### 步骤3：集成测试

**API端点测试**:
```typescript
describe('POST /api/users', () => {
  beforeEach(async () => {
    await db.user.deleteMany();  // 清理数据库
  });

  it('should create user with valid data', async () => {
    const response = await request(app)
      .post('/api/users')
      .send({
        email: 'test@example.com',
        username: 'testuser',
        password: 'Password123!'
      });

    expect(response.status).toBe(201);
    expect(response.body.user).toMatchObject({
      email: 'test@example.com',
      username: 'testuser'
    });

    // 验证是否实际保存到数据库
    const user = await db.user.findUnique({ where: { email: 'test@example.com' } });
    expect(user).toBeTruthy();
  });

  it('should reject duplicate email', async () => {
    // 创建第一个用户
    await request(app)
      .post('/api/users')
      .send({ email: 'test@example.com', username: 'user1', password: 'Pass123!' });

    // 尝试创建重复用户
    const response = await request(app)
      .post('/api/users')
      .send({ email: 'test@example.com', username: 'user2', password: 'Pass123!' });

    expect(response.status).toBe(409);
  });
});
```

### 步骤4：E2E测试（Playwright）

```typescript
import { test, expect } from '@playwright/test';

test.describe('User Registration Flow', () => {
  test('should complete full registration process', async ({ page }) => {
    // 1. 访问首页
    await page.goto('http://localhost:3000');

    // 2. 点击注册按钮
    await page.click('text=Sign Up');

    // 3. 填写表单
    await page.fill('input[name="email"]', 'test@example.com');
    await page.fill('input[name="username"]', 'testuser');
    await page.fill('input[name="password"]', 'Password123!');

    // 4. 提交表单
    await page.click('button[type="submit"]');

    // 5. 验证成功消息
    await expect(page.locator('text=Welcome')).toBeVisible();

    // 6. 验证重定向到仪表盘
    await expect(page).toHaveURL('http://localhost:3000/dashboard');

    // 7. 验证用户信息显示
    await expect(page.locator('text=testuser')).toBeVisible();
  });

  test('should show error for invalid email', async ({ page }) => {
    await page.goto('http://localhost:3000/signup');
    await page.fill('input[name="email"]', 'invalid-email');
    await page.fill('input[name="password"]', 'Password123!');
    await page.click('button[type="submit"]');

    await expect(page.locator('text=Invalid email')).toBeVisible();
  });
});
```

### 步骤5：测试驱动开发（TDD）

**红-绿-重构循环**:

```typescript
// 1. RED：编写失败的测试
describe('isPalindrome', () => {
  it('should return true for palindrome', () => {
    expect(isPalindrome('racecar')).toBe(true);
  });
});

// 2. GREEN：编写让测试通过的最简代码
function isPalindrome(str: string): boolean {
  return str === str.split('').reverse().join('');
}

// 3. REFACTOR：优化代码
function isPalindrome(str: string): boolean {
  const cleaned = str.toLowerCase().replace(/[^a-z0-9]/g, '');
  return cleaned === cleaned.split('').reverse().join('');
}

// 4. 添加更多测试用例
it('should ignore case and spaces', () => {
  expect(isPalindrome('A man a plan a canal Panama')).toBe(true);
});

it('should return false for non-palindrome', () => {
  expect(isPalindrome('hello')).toBe(false);
});
```

## 输出格式

### 测试策略文档

```markdown
## Testing Strategy

### 覆盖目标
- 单元测试：80%
- 集成测试：60%
- E2E测试：核心用户流程

### 测试执行
- 单元测试：每次提交（本地 + CI）
- 集成测试：每次PR
- E2E测试：部署前

### 工具
- 单元测试：Jest
- 集成测试：Supertest
- E2E测试：Playwright
- 覆盖率：Istanbul/nyc

### CI/CD集成
- GitHub Actions：在PR上运行所有测试
- 覆盖率低于80%则构建失败
- 在预发布环境运行E2E测试
```

## 约束条件

### 必须遵守的规则（MUST）

1. **测试隔离**：每个测试必须独立
2. **快速反馈**：单元测试需快速完成（<1分钟）
3. **确定性**：相同输入必须得到相同结果

### 禁止事项（MUST NOT）

1. **测试依赖**：禁止测试A依赖测试B的执行结果
2. **生产数据库**：禁止在测试中使用生产数据库
3. **睡眠/超时**：避免使用基于时间的测试

## 最佳实践

1. **AAA模式**：Arrange-Act-Assert（准备-执行-验证）
2. **测试命名**：采用“should ... when ...”格式
3. **边界用例**：测试边界值、null、空值等场景
4. **正常+异常路径**：覆盖成功和失败场景

## 参考资料

- [Test Pyramid](https://martinfowler.com/articles/practical-test-pyramid.html)
- [Jest](https://jestjs.io/)
- [Playwright](https://playwright.dev/)
- [Testing Best Practices](https://github.com/goldbergyoni/javascript-testing-best-practices)

## 元数据

### 版本
- **当前版本**：1.0.0
- **最后更新**：2025-01-01
- **兼容平台**：Claude, ChatGPT, Gemini

### 相关技能
- [backend-testing](../../backend/testing/SKILL.md)
- [code-review](../code-review/SKILL.md)

### 标签
`#testing` `#test-strategy` `#TDD` `#unit-test` `#integration-test` `#E2E` `#code-quality`

## 示例

### 示例1：基础用法
<!-- 在此添加示例内容 -->

### 示例2：高级用法
<!-- 在此添加高级示例内容 -->
