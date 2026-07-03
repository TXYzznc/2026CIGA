---
name: redis-best-practices
description: Redis 入门最佳实践。覆盖键命名规范、TTL 策略、基本数据结构（String/Hash/List/Set/ZSet）、pipeline 批量操作、连接池、内存管理。触发：Redis 入门、最佳实践、键命名、TTL、pipeline、Redis 集群基础。❌ 不适用：分布式锁/pub-sub/限流/排行榜，请用 redis-specialist。
tags: redis-best-practices, caching-patterns, data-structures, high-performance-redis,
  redis-cluster
tags_cn: Redis开发最佳实践, 缓存模式, 数据结构, 高性能Redis, Redis集群
---

# Redis开发最佳实践

## 核心原则

- 将Redis用于缓存、会话存储、实时分析和消息队列
- 根据使用场景选择合适的数据结构
- 实施规范的键命名约定和过期策略
- 针对高可用性和持久化需求进行设计
- 监控内存使用情况并优化性能

## 键命名规范

- 使用冒号作为命名空间分隔符
- 在键名中包含对象类型和标识符
- 保持键名简短但具有描述性
- 在整个应用中使用一致的命名模式

```
# 良好的键命名示例
user:1234:profile
user:1234:sessions
order:5678:items
cache:api:products:list
queue:email:pending
session:abc123def456
rate_limit:api:user:1234
```

## 数据结构

### 字符串

- 用于简单键值存储、计数器和缓存
- 考虑使用MGET/MSET进行批量操作

```redis
# 简单缓存
SET cache:user:1234 '{"name":"John","email":"john@example.com"}' EX 3600

# 计数器
INCR stats:pageviews:homepage
INCRBY stats:downloads:file123 5

# 原子操作
SETNX lock:resource:456 "owner:abc" EX 30
```

### 哈希表

- 用于包含多个字段的对象存储
- 比多个字符串键更节省内存
- 支持部分更新

```redis
# 存储用户资料
HSET user:1234 name "John Doe" email "john@example.com" created_at "2024-01-15"

# 获取指定字段
HGET user:1234 email
HMGET user:1234 name email

# 递增数值字段
HINCRBY user:1234 login_count 1

# 获取所有字段
HGETALL user:1234
```

### 列表

- 用于队列、最近项和活动信息流
- 队列消费者可考虑使用阻塞操作

```redis
# 消息队列
LPUSH queue:emails '{"to":"user@example.com","subject":"Welcome"}'
RPOP queue:emails

# 工作进程使用阻塞弹出
BRPOP queue:emails 30

# 最近活动（保留最后100条）
LPUSH user:1234:activity "浏览了商品567"
LTRIM user:1234:activity 0 99

# 获取最近项
LRANGE user:1234:activity 0 9
```

### 集合

- 用于唯一集合、标签和关系存储
- 支持集合操作（并集、交集、差集）

```redis
# 用户标签/兴趣
SADD user:1234:interests "technology" "music" "travel"

# 检查成员资格
SISMEMBER user:1234:interests "music"

# 查找共同兴趣
SINTER user:1234:interests user:5678:interests

# 在线用户追踪
SADD online:users "user:1234"
SREM online:users "user:1234"
SMEMBERS online:users
```

### 有序集合

- 用于排行榜、优先级队列和时间序列数据
- 元素按分数排序

```redis
# 游戏排行榜
ZADD leaderboard:game1 1500 "player:123" 2000 "player:456" 1800 "player:789"

# 获取前10名
ZREVRANGE leaderboard:game1 0 9 WITHSCORES

# 获取玩家排名
ZREVRANK leaderboard:game1 "player:123"

# 基于时间的数据（分数=时间戳）
ZADD events:user:1234 1705329600 "登录" 1705330000 "购买"

# 获取时间范围内的事件
ZRANGEBYSCORE events:user:1234 1705329600 1705333200
```

### 流

- 用于事件流和日志数据
- 支持消费者组进行分布式处理

```redis
# 向流中添加事件
XADD events:orders * customer_id 1234 product_id 567 amount 99.99

# 从流中读取数据
XREAD COUNT 10 STREAMS events:orders 0

# 消费者组配置
XGROUP CREATE events:orders order-processors $ MKSTREAM
XREADGROUP GROUP order-processors worker1 COUNT 10 STREAMS events:orders >

# 确认已处理的消息
XACK events:orders order-processors 1234567890-0
```

## 缓存模式

### 旁路缓存模式

```python
# 旁路缓存伪代码
def get_user(user_id):
    # 先尝试从缓存获取
    cached = redis.get(f"cache:user:{user_id}")
    if cached:
        return json.loads(cached)

    # 缓存未命中 - 从数据库获取
    user = database.get_user(user_id)

    # 存入缓存并设置过期时间
    redis.setex(f"cache:user:{user_id}", 3600, json.dumps(user))

    return user
```

### 写穿缓存模式

```python
def update_user(user_id, data):
    # 更新数据库
    database.update_user(user_id, data)

    # 更新缓存
    redis.setex(f"cache:user:{user_id}", 3600, json.dumps(data))
```

### 缓存失效

```redis
# 删除指定缓存
DEL cache:user:1234

# 按模式删除（生产环境谨慎使用）
# 大数据集下使用SCAN替代KEYS
SCAN 0 MATCH cache:user:* COUNT 100

# 使用集合实现基于标签的失效
SADD cache:tags:user:1234 "cache:user:1234:profile" "cache:user:1234:orders"
# 失效所有相关缓存
SMEMBERS cache:tags:user:1234
# 然后逐个删除键
```

## 过期策略与内存管理

### TTL最佳实践

- 始终为缓存键设置TTL
- 使用抖动避免缓存雪崩
- 会话数据可考虑使用滑动过期

```redis
# 设置键并指定过期时间
SET cache:data:123 "value" EX 3600

# 为已存在的键设置过期时间
EXPIRE cache:data:123 3600

# 查看键的剩余TTL
TTL cache:data:123

# 持久化键（移除过期时间）
PERSIST cache:data:123
```

### 内存管理

```redis
# 查看内存使用情况
INFO memory

# 获取单个键的内存占用
MEMORY USAGE cache:large:object

# 配置最大内存策略
CONFIG SET maxmemory 2gb
CONFIG SET maxmemory-policy allkeys-lru
```

## 事务与原子性

### MULTI/EXEC事务

```redis
# 事务块
MULTI
INCR stats:views
LPUSH recent:views "page:123"
EXEC

# 使用WATCH实现乐观锁
WATCH user:1234:balance
balance = GET user:1234:balance
MULTI
SET user:1234:balance (balance - 100)
EXEC
```

### Lua脚本

- 用于复杂原子操作
- 脚本执行具有原子性

```lua
-- 限流脚本
local key = KEYS[1]
local limit = tonumber(ARGV[1])
local window = tonumber(ARGV[2])

local current = tonumber(redis.call('GET', key) or '0')

if current >= limit then
    return 0
end

redis.call('INCR', key)
if current == 0 then
    redis.call('EXPIRE', key, window)
end

return 1
```

```redis
# 执行Lua脚本
EVAL "return redis.call('GET', KEYS[1])" 1 mykey
```

## 发布/订阅与消息队列

```redis
# 发布者
PUBLISH channel:notifications '{"type":"alert","message":"新订单"}'

# 订阅者
SUBSCRIBE channel:notifications

# 模式订阅
PSUBSCRIBE channel:*
```

## 高可用性

### 主从复制

- 使用从节点实现读扩展
- 在主节点上配置合适的持久化策略

```redis
# 在从节点上配置主节点地址
REPLICAOF master_host 6379

# 查看复制状态
INFO replication
```

### Redis哨兵

- 用于自动故障转移
- 至少部署3个Sentinel实例

### Redis集群

- 用于水平扩展
- 数据自动分片到各个节点
- 使用哈希标签确保相关键存储在同一个槽位

```redis
# 哈希标签确保相关键进入同一个槽位
SET {user:1234}:profile "data"
SET {user:1234}:settings "data"
```

## 持久化

### RDB快照

```redis
# 手动触发快照
BGSAVE

# 配置自动快照策略
CONFIG SET save "900 1 300 10 60 10000"
```

### AOF（仅追加文件）

```redis
# 启用AOF
CONFIG SET appendonly yes
CONFIG SET appendfsync everysec

# 重写AOF文件
BGREWRITEAOF
```

## 安全

- 启用身份验证
- 使用TLS加密连接
- 绑定到特定网络接口
- 禁用危险命令

```redis
# 设置密码
CONFIG SET requirepass "your_strong_password"

# 身份验证
AUTH your_strong_password

# 重命名危险命令（在redis.conf中配置）
rename-command FLUSHALL ""
rename-command FLUSHDB ""
rename-command KEYS ""
```

## 监控

```redis
# 查看服务器信息
INFO

# 查看内存统计
INFO memory

# 查看客户端连接
CLIENT LIST

# 查看慢查询日志
SLOWLOG GET 10

# 监控命令执行（仅用于调试）
MONITOR

# 查看各数据库的键数量
INFO keyspace
```

## 连接管理

- 使用连接池
- 设置合理的超时时间
- 优雅处理重连逻辑

```python
# Python连接池示例
import redis

pool = redis.ConnectionPool(
    host='localhost',
    port=6379,
    max_connections=50,
    socket_timeout=5,
    socket_connect_timeout=5
)

redis_client = redis.Redis(connection_pool=pool)
```

## 性能优化技巧

- 使用流水线处理批量操作
- 避免使用大键（值大于100KB）
- 生产环境使用SCAN替代KEYS
- 监控并优化内存使用
- 复杂JSON操作可考虑使用RedisJSON

```redis
# 流水线示例（伪代码）
pipe = redis.pipeline()
pipe.get("key1")
pipe.get("key2")
pipe.set("key3", "value")
results = pipe.execute()
```