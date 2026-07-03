---
name: atomic-matchmaking
description: 基于两阶段提交（Two-Phase Commit）的匹配机制，在创建对局前验证双方玩家的连接状态。可优雅处理断开连接情况，自动将状态正常的玩家重新加入队列。
license: MIT
compatibility: TypeScript/JavaScript, Python
metadata:
  category: api
  time: 6h
  source: drift-masterguide
tags: two-phase-commit, multiplayer-matchmaking, game-backend, python-implementation,
  connection-health
tags_cn: Two-Phase Commit, 多人对局匹配, 游戏后端开发, Python实现, 连接状态验证
---

# 基于Two-Phase Commit的原子化对局匹配

采用Two-Phase Commit语义实现对局创建，可优雅处理玩家断开连接的情况。

## 适用场景

- 构建实时多人对局匹配系统
- 需要处理对局创建过程中玩家断开连接的情况
- 希望避免出现孤立对局房间和陷入停滞的玩家
- 需要可靠的对局通知机制

## 核心概念

看似简单的玩家匹配实则暗藏难点。玩家可能在匹配成功到加入对局的过程中断开连接。本方案采用两阶段提交（Two-Phase Commit）机制：

1. **第一阶段**：通过ping/pong验证双方连接状态正常
2. **第二阶段**：创建对局房间、发送通知并确认送达
3. **回滚操作**：若出现任何失败，清理对局房间并将状态正常的玩家重新加入队列

## 实现方案

### Python

```python
from dataclasses import dataclass, field
from datetime import datetime
from typing import Optional, Tuple
from enum import Enum
import asyncio


class MatchStatus(str, Enum):
    SUCCESS = "success"
    PLAYER1_DISCONNECTED = "player1_disconnected"
    PLAYER2_DISCONNECTED = "player2_disconnected"
    BOTH_DISCONNECTED = "both_disconnected"
    NOTIFICATION_FAILED = "notification_failed"


@dataclass
class MatchTicket:
    player_id: str
    category: str
    queued_at: datetime = field(default_factory=datetime.utcnow)


@dataclass
class MatchResult:
    status: MatchStatus
    lobby_id: Optional[str] = None
    lobby_code: Optional[str] = None
    player1_healthy: bool = True
    player2_healthy: bool = True
    requeued_player: Optional[str] = None


class ConnectionHealthChecker:
    """Verifies player connections are healthy before matching."""
    
    PING_TIMEOUT = 2.0
    MAX_ACCEPTABLE_LATENCY = 500  # ms
    
    def __init__(self, connection_manager):
        self._manager = connection_manager
    
    async def check_health(self, player_id: str) -> Tuple[bool, Optional[float]]:
        if not self._manager.is_user_connected(player_id):
            return False, None
        
        success, latency = await self._manager.ping_user(
            player_id, timeout=self.PING_TIMEOUT
        )
        
        if not success:
            return False, None
        
        if latency and latency > self.MAX_ACCEPTABLE_LATENCY:
            return False, latency
        
        return True, latency
    
    async def verify_both_healthy(
        self, player1_id: str, player2_id: str
    ) -> Tuple[bool, bool, bool]:
        results = await asyncio.gather(
            self.check_health(player1_id),
            self.check_health(player2_id),
        )
        
        health1, _ = results[0]
        health2, _ = results[1]
        
        return (health1 and health2), health1, health2


class AtomicMatchCreator:
    """Creates matches with two-phase commit semantics."""
    
    NOTIFICATION_TIMEOUT = 2.0
    NOTIFICATION_RETRIES = 3
    
    def __init__(
        self,
        health_checker: ConnectionHealthChecker,
        lobby_service,
        queue_manager,
        notification_service,
    ):
        self._health_checker = health_checker
        self._lobby_service = lobby_service
        self._queue_manager = queue_manager
        self._notifications = notification_service
    
    async def create_match(
        self, player1: MatchTicket, player2: MatchTicket
    ) -> MatchResult:
        # Phase 1: Health Check
        both_healthy, health1, health2 = await self._health_checker.verify_both_healthy(
            player1.player_id, player2.player_id
        )
        
        if not both_healthy:
            return await self._handle_health_failure(player1, player2, health1, health2)
        
        # Phase 2: Create Lobby & Notify
        lobby = None
        try:
            lobby = await self._lobby_service.create_lobby(
                host_id=player1.player_id,
                category=player1.category,
            )
            
            await self._lobby_service.add_player(lobby["id"], player2.player_id)
            
            # Notify both players in parallel
            notify_results = await asyncio.gather(
                self._notify_with_retry(player1.player_id, lobby["code"], player2.player_id),
                self._notify_with_retry(player2.player_id, lobby["code"], player1.player_id),
                return_exceptions=True,
            )
            
            if not all(r is True for r in notify_results):
                raise Exception("Notification failed")
            
            return MatchResult(
                status=MatchStatus.SUCCESS,
                lobby_id=lobby["id"],
                lobby_code=lobby["code"],
            )
            
        except Exception as e:
            # Rollback: delete lobby if created
            if lobby:
                await self._lobby_service.delete_lobby(lobby["id"])
            
            return await self._handle_phase2_failure(player1, player2, str(e))
    
    async def _handle_health_failure(
        self, player1: MatchTicket, player2: MatchTicket,
        health1: bool, health2: bool
    ) -> MatchResult:
        if not health1 and not health2:
            return MatchResult(
                status=MatchStatus.BOTH_DISCONNECTED,
                player1_healthy=False,
                player2_healthy=False,
            )
        
        # Re-queue the healthy player with priority
        if health1 and not health2:
            await self._queue_manager.requeue_player(player1, priority=True)
            return MatchResult(
                status=MatchStatus.PLAYER2_DISCONNECTED,
                player1_healthy=True,
                player2_healthy=False,
                requeued_player=player1.player_id,
            )
        
        if health2 and not health1:
            await self._queue_manager.requeue_player(player2, priority=True)
            return MatchResult(
                status=MatchStatus.PLAYER1_DISCONNECTED,
                player1_healthy=False,
                player2_healthy=True,
                requeued_player=player2.player_id,
            )
        
        return MatchResult(status=MatchStatus.BOTH_DISCONNECTED)
    
    async def _notify_with_retry(
        self, player_id: str, lobby_code: str, opponent_id: str
    ) -> bool:
        for attempt in range(self.NOTIFICATION_RETRIES):
            try:
                success = await asyncio.wait_for(
                    self._notifications.notify_match_found(player_id, lobby_code, opponent_id),
                    timeout=self.NOTIFICATION_TIMEOUT,
                )
                if success:
                    return True
            except asyncio.TimeoutError:
                pass
            
            if attempt < self.NOTIFICATION_RETRIES - 1:
                await asyncio.sleep(0.1)
        
        return False
```

### 支持优先级重新入队的队列管理器

```python
from collections import deque
from typing import Dict, Optional, Set
import asyncio


class MatchmakingQueue:
    """FIFO queue with priority re-queue support."""
    
    def __init__(self):
        self._queues: Dict[str, deque] = {}
        self._player_tickets: Dict[str, MatchTicket] = {}
        self._lock = asyncio.Lock()
    
    async def enqueue(self, ticket: MatchTicket, priority: bool = False) -> bool:
        async with self._lock:
            if ticket.player_id in self._player_tickets:
                return False
            
            if ticket.category not in self._queues:
                self._queues[ticket.category] = deque()
            
            queue = self._queues[ticket.category]
            
            if priority:
                queue.appendleft(ticket)
            else:
                queue.append(ticket)
            
            self._player_tickets[ticket.player_id] = ticket
            return True
    
    async def dequeue_pair(self, category: str) -> Optional[tuple]:
        async with self._lock:
            queue = self._queues.get(category)
            if not queue or len(queue) < 2:
                return None
            
            ticket1 = queue.popleft()
            ticket2 = queue.popleft()
            
            self._player_tickets.pop(ticket1.player_id, None)
            self._player_tickets.pop(ticket2.player_id, None)
            
            return ticket1, ticket2
    
    async def requeue_player(self, ticket: MatchTicket, priority: bool = True) -> None:
        await self.remove_player(ticket.player_id)
        await self.enqueue(ticket, priority=priority)
```

## 使用示例

### 对局创建流程

```python
health_checker = ConnectionHealthChecker(connection_manager)
match_creator = AtomicMatchCreator(
    health_checker, lobby_service, queue_manager, notification_service
)

# When two players are matched
result = await match_creator.create_match(ticket1, ticket2)

if result.status == MatchStatus.SUCCESS:
    print(f"Match created: {result.lobby_code}")
elif result.requeued_player:
    print(f"Re-queued {result.requeued_player}")
```

## 最佳实践

1. 始终在创建对局房间前验证连接状态 - 避免出现孤立对局房间
2. 优先将状态正常的玩家重新加入队列 - 他们已经等待过一段时间
3. 使用通知重试机制 - 网络状态可能不稳定
4. 任何失败时执行回滚操作 - 清理部分创建的状态
5. 记录关联ID - 对调试对局失败问题至关重要

## 常见错误

- 在验证连接前创建对局房间（导致孤立对局房间）
- 未将状态正常的玩家重新加入队列（玩家陷入停滞状态）
- 未设置通知重试机制（网络波动导致对局丢失）
- 缺少回滚逻辑（资源泄漏）
- 未使用优先级重新入队（对状态正常的玩家不公平）

## 相关模式

- websocket-management - 连接状态验证
- distributed-lock - 避免匹配过程中的竞态条件
- graceful-shutdown - 关闭时排空队列