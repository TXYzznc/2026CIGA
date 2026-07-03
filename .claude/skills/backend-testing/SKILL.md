---
name: backend-testing
description: 编写全面的后端测试，包括单元测试、集成测试和API测试。适用于测试REST API、数据库操作、认证流程或业务逻辑的场景。支持Jest、Pytest、Mocha等测试工具，以及测试策略、模拟和测试覆盖率相关内容。
tags: backend-testing, unit-testing, integration-testing, api-testing, test-coverage
platforms:
- Claude
- ChatGPT
- Gemini
tags_cn: 后端测试, 单元测试, 集成测试, API测试, 测试覆盖率
---

# 后端测试


## 何时使用此技能

以下是应触发此技能的具体场景：

- **新功能开发**：采用TDD（测试驱动开发）方式，先编写测试
- **新增API端点**：测试REST API的成功/失败场景
- **修复Bug**：添加测试以防止回归问题
- **重构之前**：编写确保原有功能正常的测试
- **配置CI/CD**：构建自动化测试流水线

## 输入格式 (Input Format)

需要从用户处获取的输入格式及必填/可选信息：

### 必填信息
- **框架**：Express、Django、FastAPI、Spring Boot等
- **测试工具**：Jest、Pytest、Mocha/Chai、JUnit等
- **测试目标**：API端点、业务逻辑、数据库操作等

### 可选信息
- **数据库**：PostgreSQL、MySQL、MongoDB（默认：内存数据库）
- **模拟库**：jest.mock、sinon、unittest.mock（默认：框架内置）
- **覆盖率目标**：80%、90%等（默认：80%）
- **E2E工具**：Supertest、TestClient、RestAssured（可选）

### 输入示例

```
请测试Express.js API的用户认证端点：
- 框架：Express + TypeScript
- 测试工具：Jest + Supertest
- 目标：POST /auth/register, POST /auth/login
- 数据库：PostgreSQL（测试用内存版）
- 覆盖率：90%以上
```

## Instructions

请严格按照以下步骤执行操作：

### Step 1: 测试环境配置

安装并配置测试框架及工具。

**操作内容**:
- 安装测试库
- 配置测试数据库（内存数据库或独立数据库）
- 分离环境变量 (.env.test)
- 配置jest.config.js或pytest.ini

**示例** (Node.js + Jest + Supertest):
```bash
npm install --save-dev jest ts-jest @types/jest supertest @types/supertest
```

**jest.config.js**:
```javascript
module.exports = {
  preset: 'ts-jest',
  testEnvironment: 'node',
  roots: ['<rootDir>/src'],
  testMatch: ['**/__tests__/**/*.test.ts'],
  collectCoverageFrom: [
    'src/**/*.ts',
    '!src/**/*.d.ts',
    '!src/__tests__/**'
  ],
  coverageThreshold: {
    global: {
      branches: 80,
      functions: 80,
      lines: 80,
      statements: 80
    }
  },
  setupFilesAfterEnv: ['<rootDir>/src/__tests__/setup.ts']
};
```

**setup.ts** (测试全局配置):
```typescript
import { db } from '../database';

// 每个测试前初始化数据库
beforeEach(async () => {
  await db.migrate.latest();
  await db.seed.run();
});

// 每个测试后清理
afterEach(async () => {
  await db.migrate.rollback();
});

// 所有测试完成后关闭连接
afterAll(async () => {
  await db.destroy();
});
```

### Step 2: 编写单元测试（业务逻辑）

编写单个函数/类的单元测试。

**操作内容**:
- 测试纯函数（无依赖）
- 通过模拟隔离依赖
- 测试边界场景（边界值、异常）
- 遵循AAA模式（Arrange-Act-Assert）

**判断标准**:
- 无外部依赖（数据库、API）→ 纯单元测试
- 有外部依赖→ 使用Mock/Stub
- 复杂逻辑→ 测试多种输入场景

**示例**（密码验证函数）:
```typescript
// src/utils/password.ts
export function validatePassword(password: string): { valid: boolean; errors: string[] } {
  const errors: string[] = [];

  if (password.length < 8) {
    errors.push('Password must be at least 8 characters');
  }

  if (!/[A-Z]/.test(password)) {
    errors.push('Password must contain uppercase letter');
  }

  if (!/[a-z]/.test(password)) {
    errors.push('Password must contain lowercase letter');
  }

  if (!/\d/.test(password)) {
    errors.push('Password must contain number');
  }

  if (!/[!@#$%^&*]/.test(password)) {
    errors.push('Password must contain special character');
  }

  return { valid: errors.length === 0, errors };
}

// src/__tests__/utils/password.test.ts
import { validatePassword } from '../../utils/password';

describe('validatePassword', () => {
  it('should accept valid password', () => {
    const result = validatePassword('Password123!');
    expect(result.valid).toBe(true);
    expect(result.errors).toHaveLength(0);
  });

  it('should reject password shorter than 8 characters', () => {
    const result = validatePassword('Pass1!');
    expect(result.valid).toBe(false);
    expect(result.errors).toContain('Password must be at least 8 characters');
  });

  it('should reject password without uppercase', () => {
    const result = validatePassword('password123!');
    expect(result.valid).toBe(false);
    expect(result.errors).toContain('Password must contain uppercase letter');
  });

  it('should reject password without lowercase', () => {
    const result = validatePassword('PASSWORD123!');
    expect(result.valid).toBe(false);
    expect(result.errors).toContain('Password must contain lowercase letter');
  });

  it('should reject password without number', () => {
    const result = validatePassword('Password!');
    expect(result.valid).toBe(false);
    expect(result.errors).toContain('Password must contain number');
  });

  it('should reject password without special character', () => {
    const result = validatePassword('Password123');
    expect(result.valid).toBe(false);
    expect(result.errors).toContain('Password must contain special character');
  });

  it('should return multiple errors for invalid password', () => {
    const result = validatePassword('pass');
    expect(result.valid).toBe(false);
    expect(result.errors.length).toBeGreaterThan(1);
  });
});
```

### Step 3: 集成测试（API端点）

编写API端点的集成测试。

**操作内容**:
- 测试HTTP请求/响应
- 成功场景（200、201）
- 失败场景（400、401、404、500）
- 认证/权限测试
- 输入验证测试

**检查项**:
- [x] 确认状态码
- [x] 验证响应体结构
- [x] 确认数据库状态变化
- [x] 验证错误信息

**示例** (Express.js + Supertest):
```typescript
// src/__tests__/api/auth.test.ts
import request from 'supertest';
import app from '../../app';
import { db } from '../../database';

describe('POST /auth/register', () => {
  it('should register new user successfully', async () => {
    const response = await request(app)
      .post('/api/auth/register')
      .send({
        email: 'test@example.com',
        username: 'testuser',
        password: 'Password123!'
      });

    expect(response.status).toBe(201);
    expect(response.body).toHaveProperty('user');
    expect(response.body).toHaveProperty('accessToken');
    expect(response.body.user.email).toBe('test@example.com');

    // 检查数据库是否已实际存储
    const user = await db.user.findUnique({ where: { email: 'test@example.com' } });
    expect(user).toBeTruthy();
    expect(user.username).toBe('testuser');
  });

  it('should reject duplicate email', async () => {
    // 创建第一个用户
    await request(app)
      .post('/api/auth/register')
      .send({
        email: 'test@example.com',
        username: 'user1',
        password: 'Password123!'
      });

    // 使用相同邮箱再次尝试
    const response = await request(app)
      .post('/api/auth/register')
      .send({
        email: 'test@example.com',
        username: 'user2',
        password: 'Password123!'
      });

    expect(response.status).toBe(409);
    expect(response.body.error).toContain('already exists');
  });

  it('should reject weak password', async () => {
    const response = await request(app)
      .post('/api/auth/register')
      .send({
        email: 'test@example.com',
        username: 'testuser',
        password: 'weak'
      });

    expect(response.status).toBe(400);
    expect(response.body.error).toBeDefined();
  });

  it('should reject missing fields', async () => {
    const response = await request(app)
      .post('/api/auth/register')
      .send({
        email: 'test@example.com'
        // 缺少username、password
      });

    expect(response.status).toBe(400);
  });
});

describe('POST /auth/login', () => {
  beforeEach(async () => {
    // 创建测试用户
    await request(app)
      .post('/api/auth/register')
      .send({
        email: 'test@example.com',
        username: 'testuser',
        password: 'Password123!'
      });
  });

  it('should login with valid credentials', async () => {
    const response = await request(app)
      .post('/api/auth/login')
      .send({
        email: 'test@example.com',
        password: 'Password123!'
      });

    expect(response.status).toBe(200);
    expect(response.body).toHaveProperty('accessToken');
    expect(response.body).toHaveProperty('refreshToken');
    expect(response.body.user.email).toBe('test@example.com');
  });

  it('should reject invalid password', async () => {
    const response = await request(app)
      .post('/api/auth/login')
      .send({
        email: 'test@example.com',
        password: 'WrongPassword123!'
      });

    expect(response.status).toBe(401);
    expect(response.body.error).toContain('Invalid credentials');
  });

  it('should reject non-existent user', async () => {
    const response = await request(app)
      .post('/api/auth/login')
      .send({
        email: 'nonexistent@example.com',
        password: 'Password123!'
      });

    expect(response.status).toBe(401);
  });
});
```

### Step 4: 认证/权限测试

测试JWT令牌及基于角色的访问控制。

**操作内容**:
- 验证无令牌访问时返回401
- 验证有效令牌可成功访问
- 测试过期令牌的处理
- 基于角色的权限测试

**示例**:
```typescript
describe('Protected Routes', () => {
  let accessToken: string;
  let adminToken: string;

  beforeEach(async () => {
    // 获取普通用户令牌
    const userResponse = await request(app)
      .post('/api/auth/register')
      .send({
        email: 'user@example.com',
        username: 'user',
        password: 'Password123!'
      });
    accessToken = userResponse.body.accessToken;

    // 获取管理员令牌
    const adminResponse = await request(app)
      .post('/api/auth/register')
      .send({
        email: 'admin@example.com',
        username: 'admin',
        password: 'Password123!'
      });
    // 在数据库中将角色改为'admin'
    await db.user.update({
      where: { email: 'admin@example.com' },
      data: { role: 'admin' }
    });
    // 重新登录获取新令牌
    const loginResponse = await request(app)
      .post('/api/auth/login')
      .send({
        email: 'admin@example.com',
        password: 'Password123!'
      });
    adminToken = loginResponse.body.accessToken;
  });

  describe('GET /api/auth/me', () => {
    it('should return current user with valid token', async () => {
      const response = await request(app)
        .get('/api/auth/me')
        .set('Authorization', `Bearer ${accessToken}`);

      expect(response.status).toBe(200);
      expect(response.body.user.email).toBe('user@example.com');
    });

    it('should reject request without token', async () => {
      const response = await request(app)
        .get('/api/auth/me');

      expect(response.status).toBe(401);
    });

    it('should reject request with invalid token', async () => {
      const response = await request(app)
        .get('/api/auth/me')
        .set('Authorization', 'Bearer invalid-token');

      expect(response.status).toBe(403);
    });
  });

  describe('DELETE /api/users/:id (Admin only)', () => {
    it('should allow admin to delete user', async () => {
      const targetUser = await db.user.findUnique({ where: { email: 'user@example.com' } });

      const response = await request(app)
        .delete(`/api/users/${targetUser.id}`)
        .set('Authorization', `Bearer ${adminToken}`);

      expect(response.status).toBe(200);
    });

    it('should forbid non-admin from deleting user', async () => {
      const targetUser = await db.user.findUnique({ where: { email: 'user@example.com' } });

      const response = await request(app)
        .delete(`/api/users/${targetUser.id}`)
        .set('Authorization', `Bearer ${accessToken}`);

      expect(response.status).toBe(403);
    });
  });
});
```

### Step 5: 模拟与测试隔离

通过模拟外部依赖实现测试隔离。

**操作内容**:
- 模拟外部API
- 模拟邮件发送
- 模拟文件系统
- 模拟时间相关函数

**示例**（模拟外部API）:
```typescript
// src/services/emailService.ts
export async function sendVerificationEmail(email: string, token: string): Promise<void> {
  const response = await fetch('https://api.sendgrid.com/v3/mail/send', {
    method: 'POST',
    headers: { 'Authorization': `Bearer ${process.env.SENDGRID_API_KEY}` },
    body: JSON.stringify({
      to: email,
      subject: 'Verify your email',
      html: `<a href="https://example.com/verify?token=${token}">Verify</a>`
    })
  });

  if (!response.ok) {
    throw new Error('Failed to send email');
  }
}

// src/__tests__/services/emailService.test.ts
import { sendVerificationEmail } from '../../services/emailService';

// 模拟fetch
global.fetch = jest.fn();

describe('sendVerificationEmail', () => {
  beforeEach(() => {
    (fetch as jest.Mock).mockClear();
  });

  it('should send email successfully', async () => {
    (fetch as jest.Mock).mockResolvedValueOnce({
      ok: true,
      status: 200
    });

    await expect(sendVerificationEmail('test@example.com', 'token123'))
      .resolves
      .toBeUndefined();

    expect(fetch).toHaveBeenCalledWith(
      'https://api.sendgrid.com/v3/mail/send',
      expect.objectContaining({
        method: 'POST'
      })
    );
  });

  it('should throw error if email sending fails', async () => {
    (fetch as jest.Mock).mockResolvedValueOnce({
      ok: false,
      status: 500
    });

    await expect(sendVerificationEmail('test@example.com', 'token123'))
      .rejects
      .toThrow('Failed to send email');
  });
});
```

## Output format

定义结果必须遵循的格式：

### 基本结构

```
项目/
├── src/
│   ├── __tests__/
│   │   ├── setup.ts                 # 测试全局配置
│   │   ├── utils/
│   │   │   └── password.test.ts     # 单元测试
│   │   ├── services/
│   │   │   └── emailService.test.ts
│   │   └── api/
│   │       ├── auth.test.ts         # 集成测试
│   │       └── users.test.ts
│   └── ...
├── jest.config.js
└── package.json
```

### 测试执行脚本 (package.json)

```json
{
  "scripts": {
    "test": "jest",
    "test:watch": "jest --watch",
    "test:coverage": "jest --coverage",
    "test:ci": "jest --ci --coverage --maxWorkers=2"
  }
}
```

### 覆盖率报告

```bash
$ npm run test:coverage

--------------------------|---------|----------|---------|---------|
File                      | % Stmts | % Branch | % Funcs | % Lines |
--------------------------|---------|----------|---------|---------|
All files                 |   92.5  |   88.3   |   95.2  |   92.8  |
 auth/                    |   95.0  |   90.0   |  100.0  |   95.0  |
  middleware.ts           |   95.0  |   90.0   |  100.0  |   95.0  |
  routes.ts               |   95.0  |   90.0   |  100.0  |   95.0  |
 utils/                   |   90.0  |   85.0   |   90.0  |   90.0  |
  password.ts             |   90.0  |   85.0   |   90.0  |   90.0  |
--------------------------|---------|----------|---------|---------|
```

## 约束规则

以下是必须遵守的规则和禁止事项：

### 必须遵守的规则 (MUST)

1. **测试隔离**：每个测试必须能够独立执行
   - 使用beforeEach/afterEach初始化状态
   - 不依赖测试执行顺序

2. **清晰的测试名称**：从测试名称中应能明确了解其验证内容
   - ✅ 'should reject duplicate email'（应拒绝重复邮箱）
   - ❌ 'test1'（无意义名称）

3. **AAA模式**：采用Arrange（准备）- Act（执行）- Assert（验证）结构
   - 提升可读性
   - 明确测试意图

### 禁止事项 (MUST NOT)

1. **禁止使用生产数据库**：测试需使用独立数据库或内存数据库
   - 避免实际数据丢失风险
   - 确保测试隔离

2. **禁止调用真实外部API**：外部服务需使用模拟（Mock）
   - 消除网络依赖
   - 提升测试速度
   - 降低成本

3. **禁止滥用Sleep/Timeout**：基于时间的测试需使用假计时器
   - 使用jest.useFakeTimers()
   - 避免测试速度下降

### 安全规则

- **禁止硬编码敏感信息**：测试代码中同样禁止硬编码API密钥、密码等
- **分离环境变量**：使用.env.test文件

## 示例

### 示例1：Python FastAPI测试（Pytest）

**场景**: 测试FastAPI REST API

**用户请求**:
```
请用Pytest测试FastAPI开发的用户API。
```

**最终结果**:
```python
# tests/conftest.py
import pytest
from fastapi.testclient import TestClient
from sqlalchemy import create_engine
from sqlalchemy.orm import sessionmaker

from app.main import app
from app.database import Base, get_db

# 测试用内存SQLite
SQLALCHEMY_DATABASE_URL = "sqlite:///./test.db"
engine = create_engine(SQLALCHEMY_DATABASE_URL, connect_args={"check_same_thread": False})
TestingSessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)

@pytest.fixture(scope="function")
def db_session():
    Base.metadata.create_all(bind=engine)
    db = TestingSessionLocal()
    try:
        yield db
    finally:
        db.close()
        Base.metadata.drop_all(bind=engine)

@pytest.fixture(scope="function")
def client(db_session):
    def override_get_db():
        try:
            yield db_session
        finally:
            db_session.close()

    app.dependency_overrides[get_db] = override_get_db
    yield TestClient(app)
    app.dependency_overrides.clear()

# tests/test_auth.py
def test_register_user_success(client):
    response = client.post("/auth/register", json={
        "email": "test@example.com",
        "username": "testuser",
        "password": "Password123!"
    })

    assert response.status_code == 201
    assert "access_token" in response.json()
    assert response.json()["user"]["email"] == "test@example.com"

def test_register_duplicate_email(client):
    # 创建第一个用户
    client.post("/auth/register", json={
        "email": "test@example.com",
        "username": "user1",
        "password": "Password123!"
    })

    # 使用重复邮箱
    response = client.post("/auth/register", json={
        "email": "test@example.com",
        "username": "user2",
        "password": "Password123!"
    })

    assert response.status_code == 409
    assert "already exists" in response.json()["detail"]

def test_login_success(client):
    # 注册用户
    client.post("/auth/register", json={
        "email": "test@example.com",
        "username": "testuser",
        "password": "Password123!"
    })

    # 登录
    response = client.post("/auth/login", json={
        "email": "test@example.com",
        "password": "Password123!"
    })

    assert response.status_code == 200
    assert "access_token" in response.json()

def test_protected_route_without_token(client):
    response = client.get("/auth/me")
    assert response.status_code == 401

def test_protected_route_with_token(client):
    # 注册并获取令牌
    register_response = client.post("/auth/register", json={
        "email": "test@example.com",
        "username": "testuser",
        "password": "Password123!"
    })
    token = register_response.json()["access_token"]

    # 访问受保护路由
    response = client.get("/auth/me", headers={
        "Authorization": f"Bearer {token}"
    })

    assert response.status_code == 200
    assert response.json()["email"] == "test@example.com"
```

## 最佳实践

### 提升质量

1. **TDD（测试驱动开发）**: 先编写测试再开发代码
   - 明确需求
   - 优化设计
   - 自然实现高覆盖率

2. **Given-When-Then模式**: 采用BDD风格编写测试
   ```typescript
   it('should return 404 when user not found', async () => {
     // Given: 不存在的用户ID
     const nonExistentId = 'non-existent-uuid';

     // When: 尝试查询该用户
     const response = await request(app).get(`/users/${nonExistentId}`);

     // Then: 返回404响应
     expect(response.status).toBe(404);
   });
   ```

3. **测试夹具**: 使用可复用的测试数据
   ```typescript
   const validUser = {
     email: 'test@example.com',
     username: 'testuser',
     password: 'Password123!'
   };
   ```

### 提升效率

- **并行执行**: 使用Jest的`--maxWorkers`选项提升测试速度
- **快照测试**: 保存UI组件或JSON响应的快照
- **覆盖率阈值**: 在jest.config.js中强制设置最低覆盖率

## 常见问题 (Common Issues)

### 问题1：测试间状态共享导致的失败

**症状**: 单独执行测试成功，但全部执行时失败

**原因**: 未使用beforeEach/afterEach导致数据库状态共享

**解决方法**:
```typescript
beforeEach(async () => {
  await db.migrate.rollback();
  await db.migrate.latest();
});
```

### 问题2: "Jest did not exit one second after the test run"

**症状**: 测试完成后进程未退出

**原因**: 数据库连接、服务器等资源未清理

**解决方法**:
```typescript
afterAll(async () => {
  await db.destroy();
  await server.close();
});
```

### 问题3: 异步测试超时

**症状**: "Timeout - Async callback was not invoked"

**原因**: 遗漏async/await或未处理Promise

**解决方法**:
```typescript
// ❌ 不良示例
it('should work', () => {
  request(app).get('/users');  // 未处理Promise
});

// ✅ 良好示例
it('should work', async () => {
  await request(app).get('/users');
});
```

## 参考资料

### 官方文档
- [Jest Documentation](https://jestjs.io/docs/getting-started)
- [Pytest Documentation](https://docs.pytest.org/)
- [Supertest GitHub](https://github.com/visionmedia/supertest)

### 学习资料
- [Testing JavaScript with Kent C. Dodds](https://testingjavascript.com/)
- [Test-Driven Development by Example (Kent Beck)](https://www.amazon.com/Test-Driven-Development-Kent-Beck/dp/0321146530)

### 工具
- [Istanbul/nyc](https://istanbul.js.org/) - 代码覆盖率
- [nock](https://github.com/nock/nock) - HTTP模拟
- [faker.js](https://fakerjs.dev/) - 测试数据生成

## 元数据

### 版本
- **当前版本**: 1.0.0
- **最后更新**: 2025-01-01
- **兼容平台**: Claude、ChatGPT、Gemini

### 相关技能
- [api-design](../api-design/SKILL.md): 与API协同设计测试
- [authentication-setup](../authentication/SKILL.md): 认证系统测试

### 标签
`#testing` `#backend` `#Jest` `#Pytest` `#unit-test` `#integration-test` `#TDD` `#API-test`