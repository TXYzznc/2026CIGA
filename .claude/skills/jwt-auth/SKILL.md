---
name: jwt-auth
description: 实现具备刷新令牌轮转、安全存储和自动续期功能的安全JWT认证机制。适用于为SPA、移动应用或需要带刷新能力的无状态认证的API构建认证系统。
license: MIT
compatibility: TypeScript/JavaScript, Python
metadata:
  category: auth
  time: 4h
  source: drift-masterguide
tags: jwt-authentication, refresh-token-rotation, stateless-auth, token-management,
  backend-security
tags_cn: JWT认证, 刷新令牌轮转, 无状态认证, 令牌管理, 后端安全
---

# 带刷新令牌轮转的JWT认证

具备自动令牌刷新功能的安全无状态认证方案。

## 适用场景

- 为SPA或移动应用构建认证系统
- 为API实现无状态认证
- 无需重新登录即可自动刷新令牌
- 实现类OAuth的令牌流程

## 令牌架构

```
┌─────────────────────────────────────────────────────┐
│                   Access Token                       │
│  - Short-lived (15 min)                             │
│  - Contains user claims                             │
│  - Sent with every request                          │
│  - Stored in memory (not localStorage)              │
└─────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────┐
│                  Refresh Token                       │
│  - Long-lived (7 days)                              │
│  - Used only to get new access tokens               │
│  - Stored in httpOnly cookie                        │
│  - Rotated on each use (one-time use)              │
└─────────────────────────────────────────────────────┘
```

## 令牌轮转流程

```
1. 登录
   客户端 ──────────────────────────────────▶ 服务器
          凭证                         
   客户端 ◀────────────────────────────────── 服务器
          access_token + refresh_token (Cookie)

2. API请求
   客户端 ──────────────────────────────────▶ 服务器
          Authorization: Bearer {access_token}
   客户端 ◀────────────────────────────────── 服务器
          响应

3. 令牌刷新（当access token过期时）
   客户端 ──────────────────────────────────▶ 服务器
          POST /auth/refresh (自动发送Cookie)
   客户端 ◀────────────────────────────────── 服务器
          new_access_token + new_refresh_token
          (旧刷新令牌失效)
```

## TypeScript实现

### 令牌服务

```typescript
// token-service.ts
import jwt from 'jsonwebtoken';
import crypto from 'crypto';
import { Redis } from 'ioredis';

interface TokenConfig {
  accessTokenSecret: string;
  refreshTokenSecret: string;
  accessTokenExpiry: string;  // e.g., '15m'
  refreshTokenExpiry: string; // e.g., '7d'
}

interface TokenPayload {
  userId: string;
  email: string;
  role: string;
}

interface TokenPair {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}

class TokenService {
  constructor(
    private config: TokenConfig,
    private redis: Redis
  ) {}

  async generateTokenPair(payload: TokenPayload): Promise<TokenPair> {
    // Generate access token
    const accessToken = jwt.sign(payload, this.config.accessTokenSecret, {
      expiresIn: this.config.accessTokenExpiry,
    });

    // Generate refresh token (random + signed)
    const refreshTokenId = crypto.randomUUID();
    const refreshToken = jwt.sign(
      { ...payload, tokenId: refreshTokenId },
      this.config.refreshTokenSecret,
      { expiresIn: this.config.refreshTokenExpiry }
    );

    // Store refresh token in Redis for rotation tracking
    await this.storeRefreshToken(payload.userId, refreshTokenId);

    // Calculate expiry in seconds
    const decoded = jwt.decode(accessToken) as { exp: number };
    const expiresIn = decoded.exp - Math.floor(Date.now() / 1000);

    return { accessToken, refreshToken, expiresIn };
  }

  async refreshTokens(refreshToken: string): Promise<TokenPair | null> {
    try {
      // Verify refresh token
      const decoded = jwt.verify(
        refreshToken,
        this.config.refreshTokenSecret
      ) as TokenPayload & { tokenId: string };

      // Check if token is still valid (not rotated)
      const isValid = await this.validateRefreshToken(
        decoded.userId,
        decoded.tokenId
      );

      if (!isValid) {
        // Token reuse detected - potential theft
        // Invalidate all tokens for this user
        await this.revokeAllUserTokens(decoded.userId);
        return null;
      }

      // Invalidate old refresh token
      await this.invalidateRefreshToken(decoded.userId, decoded.tokenId);

      // Generate new token pair
      return this.generateTokenPair({
        userId: decoded.userId,
        email: decoded.email,
        role: decoded.role,
      });
    } catch (error) {
      return null;
    }
  }

  verifyAccessToken(token: string): TokenPayload | null {
    try {
      return jwt.verify(token, this.config.accessTokenSecret) as TokenPayload;
    } catch {
      return null;
    }
  }

  private async storeRefreshToken(userId: string, tokenId: string): Promise<void> {
    const key = `refresh_tokens:${userId}`;
    // Store with expiry matching refresh token
    await this.redis.sadd(key, tokenId);
    await this.redis.expire(key, 7 * 24 * 60 * 60); // 7 days
  }

  private async validateRefreshToken(userId: string, tokenId: string): Promise<boolean> {
    const key = `refresh_tokens:${userId}`;
    return (await this.redis.sismember(key, tokenId)) === 1;
  }

  private async invalidateRefreshToken(userId: string, tokenId: string): Promise<void> {
    const key = `refresh_tokens:${userId}`;
    await this.redis.srem(key, tokenId);
  }

  async revokeAllUserTokens(userId: string): Promise<void> {
    const key = `refresh_tokens:${userId}`;
    await this.redis.del(key);
  }
}

export { TokenService, TokenConfig, TokenPayload, TokenPair };
```

### 认证路由

```typescript
// auth-routes.ts
import { Router, Request, Response } from 'express';
import { TokenService } from './token-service';

const router = Router();

// Cookie options for refresh token
const REFRESH_COOKIE_OPTIONS = {
  httpOnly: true,
  secure: process.env.NODE_ENV === 'production',
  sameSite: 'strict' as const,
  path: '/auth', // Only sent to auth routes
  maxAge: 7 * 24 * 60 * 60 * 1000, // 7 days
};

router.post('/login', async (req: Request, res: Response) => {
  const { email, password } = req.body;

  // Validate credentials (implement your own)
  const user = await validateCredentials(email, password);
  if (!user) {
    return res.status(401).json({ error: 'Invalid credentials' });
  }

  // Generate tokens
  const tokens = await tokenService.generateTokenPair({
    userId: user.id,
    email: user.email,
    role: user.role,
  });

  // Set refresh token as httpOnly cookie
  res.cookie('refresh_token', tokens.refreshToken, REFRESH_COOKIE_OPTIONS);

  // Return access token in response body
  res.json({
    accessToken: tokens.accessToken,
    expiresIn: tokens.expiresIn,
    user: {
      id: user.id,
      email: user.email,
      role: user.role,
    },
  });
});

router.post('/refresh', async (req: Request, res: Response) => {
  const refreshToken = req.cookies.refresh_token;

  if (!refreshToken) {
    return res.status(401).json({ error: 'No refresh token' });
  }

  const tokens = await tokenService.refreshTokens(refreshToken);

  if (!tokens) {
    // Clear invalid cookie
    res.clearCookie('refresh_token', { path: '/auth' });
    return res.status(401).json({ error: 'Invalid refresh token' });
  }

  // Set new refresh token
  res.cookie('refresh_token', tokens.refreshToken, REFRESH_COOKIE_OPTIONS);

  res.json({
    accessToken: tokens.accessToken,
    expiresIn: tokens.expiresIn,
  });
});

router.post('/logout', async (req: Request, res: Response) => {
  const refreshToken = req.cookies.refresh_token;

  if (refreshToken) {
    // Invalidate the refresh token
    try {
      const decoded = jwt.decode(refreshToken) as { userId: string };
      if (decoded?.userId) {
        await tokenService.revokeAllUserTokens(decoded.userId);
      }
    } catch {
      // Ignore decode errors
    }
  }

  res.clearCookie('refresh_token', { path: '/auth' });
  res.json({ success: true });
});

export { router as authRouter };
```

### 认证中间件

```typescript
// auth-middleware.ts
import { Request, Response, NextFunction } from 'express';
import { TokenService, TokenPayload } from './token-service';

declare global {
  namespace Express {
    interface Request {
      user?: TokenPayload;
    }
  }
}

function authMiddleware(tokenService: TokenService) {
  return (req: Request, res: Response, next: NextFunction) => {
    const authHeader = req.headers.authorization;

    if (!authHeader?.startsWith('Bearer ')) {
      return res.status(401).json({ error: 'Missing authorization header' });
    }

    const token = authHeader.slice(7);
    const payload = tokenService.verifyAccessToken(token);

    if (!payload) {
      return res.status(401).json({ error: 'Invalid or expired token' });
    }

    req.user = payload;
    next();
  };
}

export { authMiddleware };
```

## Python实现

```python
# token_service.py
import jwt
import uuid
from datetime import datetime, timedelta
from typing import Optional, Dict, Any
from dataclasses import dataclass
import redis

@dataclass
class TokenPayload:
    user_id: str
    email: str
    role: str

@dataclass
class TokenPair:
    access_token: str
    refresh_token: str
    expires_in: int

class TokenService:
    def __init__(
        self,
        access_secret: str,
        refresh_secret: str,
        redis_client: redis.Redis,
        access_expiry_minutes: int = 15,
        refresh_expiry_days: int = 7,
    ):
        self.access_secret = access_secret
        self.refresh_secret = refresh_secret
        self.redis = redis_client
        self.access_expiry = timedelta(minutes=access_expiry_minutes)
        self.refresh_expiry = timedelta(days=refresh_expiry_days)

    def generate_token_pair(self, payload: TokenPayload) -> TokenPair:
        now = datetime.utcnow()
        
        # Access token
        access_payload = {
            "user_id": payload.user_id,
            "email": payload.email,
            "role": payload.role,
            "exp": now + self.access_expiry,
            "iat": now,
        }
        access_token = jwt.encode(access_payload, self.access_secret, algorithm="HS256")

        # Refresh token with unique ID
        token_id = str(uuid.uuid4())
        refresh_payload = {
            **access_payload,
            "token_id": token_id,
            "exp": now + self.refresh_expiry,
        }
        refresh_token = jwt.encode(refresh_payload, self.refresh_secret, algorithm="HS256")

        # Store refresh token ID
        self._store_refresh_token(payload.user_id, token_id)

        return TokenPair(
            access_token=access_token,
            refresh_token=refresh_token,
            expires_in=int(self.access_expiry.total_seconds()),
        )

    def refresh_tokens(self, refresh_token: str) -> Optional[TokenPair]:
        try:
            decoded = jwt.decode(refresh_token, self.refresh_secret, algorithms=["HS256"])
            
            # Validate token is still valid
            if not self._validate_refresh_token(decoded["user_id"], decoded["token_id"]):
                # Token reuse detected - revoke all
                self.revoke_all_user_tokens(decoded["user_id"])
                return None

            # Invalidate old token
            self._invalidate_refresh_token(decoded["user_id"], decoded["token_id"])

            # Generate new pair
            return self.generate_token_pair(TokenPayload(
                user_id=decoded["user_id"],
                email=decoded["email"],
                role=decoded["role"],
            ))
        except jwt.InvalidTokenError:
            return None

    def verify_access_token(self, token: str) -> Optional[TokenPayload]:
        try:
            decoded = jwt.decode(token, self.access_secret, algorithms=["HS256"])
            return TokenPayload(
                user_id=decoded["user_id"],
                email=decoded["email"],
                role=decoded["role"],
            )
        except jwt.InvalidTokenError:
            return None

    def _store_refresh_token(self, user_id: str, token_id: str) -> None:
        key = f"refresh_tokens:{user_id}"
        self.redis.sadd(key, token_id)
        self.redis.expire(key, int(self.refresh_expiry.total_seconds()))

    def _validate_refresh_token(self, user_id: str, token_id: str) -> bool:
        key = f"refresh_tokens:{user_id}"
        return self.redis.sismember(key, token_id)

    def _invalidate_refresh_token(self, user_id: str, token_id: str) -> None:
        key = f"refresh_tokens:{user_id}"
        self.redis.srem(key, token_id)

    def revoke_all_user_tokens(self, user_id: str) -> None:
        key = f"refresh_tokens:{user_id}"
        self.redis.delete(key)
```

## 前端集成

```typescript
// auth-client.ts
class AuthClient {
  private accessToken: string | null = null;
  private refreshPromise: Promise<boolean> | null = null;

  async login(email: string, password: string): Promise<boolean> {
    const response = await fetch('/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include', // Important for cookies
      body: JSON.stringify({ email, password }),
    });

    if (!response.ok) return false;

    const data = await response.json();
    this.accessToken = data.accessToken;
    
    // Schedule refresh before expiry
    this.scheduleRefresh(data.expiresIn);
    
    return true;
  }

  async fetch(url: string, options: RequestInit = {}): Promise<Response> {
    // Ensure we have a valid token
    if (!this.accessToken) {
      await this.refresh();
    }

    const response = await fetch(url, {
      ...options,
      headers: {
        ...options.headers,
        Authorization: `Bearer ${this.accessToken}`,
      },
    });

    // If 401, try refresh and retry
    if (response.status === 401) {
      const refreshed = await this.refresh();
      if (refreshed) {
        return fetch(url, {
          ...options,
          headers: {
            ...options.headers,
            Authorization: `Bearer ${this.accessToken}`,
          },
        });
      }
    }

    return response;
  }

  private async refresh(): Promise<boolean> {
    // Deduplicate concurrent refresh calls
    if (this.refreshPromise) {
      return this.refreshPromise;
    }

    this.refreshPromise = this.doRefresh();
    const result = await this.refreshPromise;
    this.refreshPromise = null;
    return result;
  }

  private async doRefresh(): Promise<boolean> {
    const response = await fetch('/auth/refresh', {
      method: 'POST',
      credentials: 'include',
    });

    if (!response.ok) {
      this.accessToken = null;
      return false;
    }

    const data = await response.json();
    this.accessToken = data.accessToken;
    this.scheduleRefresh(data.expiresIn);
    return true;
  }

  private scheduleRefresh(expiresIn: number): void {
    // Refresh 1 minute before expiry
    const refreshIn = (expiresIn - 60) * 1000;
    setTimeout(() => this.refresh(), refreshIn);
  }

  async logout(): Promise<void> {
    await fetch('/auth/logout', {
      method: 'POST',
      credentials: 'include',
    });
    this.accessToken = null;
  }
}

export const authClient = new AuthClient();
```

## 最佳实践

1. **短有效期访问令牌**：最长15分钟
2. **轮转刷新令牌**：一次性使用可防止令牌被盗用
3. **将刷新令牌存储在httpOnly Cookie中**：无法被JavaScript访问
4. **将访问令牌存储在内存中**：不使用localStorage
5. **检测令牌复用**：一旦检测到立即吊销该用户的所有令牌

## 常见错误

- 将令牌存储在localStorage中（易受XSS攻击）
- 访问令牌有效期过长（增大攻击窗口）
- 不轮转刷新令牌（被盗令牌可永久获取访问权限）
- 未检测刷新令牌复用
- 每次请求都发送刷新令牌

## 安全检查清单

- [ ] 访问令牌有效期 ≤ 15分钟
- [ ] 刷新令牌存储在httpOnly Cookie中
- [ ] 访问令牌仅存储在内存中
- [ ] 每次使用都轮转刷新令牌
- [ ] 具备令牌复用检测机制
- [ ] Cookie设置安全标识（httpOnly、secure、sameSite）
- [ ] 生产环境仅使用HTTPS