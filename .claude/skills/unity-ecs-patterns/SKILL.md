---
name: unity-ecs-patterns
description: 通过DOTS、Jobs和Burst掌握Unity ECS（实体组件系统），用于高性能游戏开发。适用于构建面向数据的游戏、优化性能或处理大量实体的场景。
tags: unity-ecs, dots-framework, burst-compiler, entity-component-system, high-performance-gamedev
tags_cn: Unity ECS, DOTS技术栈, Burst编译器, 实体组件系统, 高性能游戏开发
---

# Unity ECS 模式

适用于Unity面向数据技术栈（DOTS）的生产级模式，包括实体组件系统（Entity Component System）、Job System和Burst编译器。

## 何时使用该技术

- 构建高性能Unity游戏
- 高效管理数千个实体
- 实现面向数据的游戏系统
- 优化CPU密集型游戏逻辑
- 将OOP游戏代码转换为ECS
- 使用Jobs和Burst进行并行处理

## 核心概念

### 1. ECS 与 OOP

| 方面          | 传统OOP          | ECS/DOTS        |
| ----------- | ----------------- | --------------- |
| 数据布局       | 面向对象            | 面向数据          |
| 内存          | 分散的             | 连续的            |
| 处理方式        | 逐对象             | 批量处理           |
| 扩展性         | 随数量增加性能下降        | 线性扩展           |
| 适用场景        | 复杂行为            | 大规模模拟          |

### 2. DOTS 组件

```
Entity: Lightweight ID (no data)
Component: Pure data (no behavior)
System: Logic that processes components
World: Container for entities
Archetype: Unique combination of components
Chunk: Memory block for same-archetype entities
```

## 模式

### 模式1：基础ECS设置

```csharp
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;
using Unity.Collections;

// Component: Pure data, no methods
public struct Speed : IComponentData
{
    public float Value;
}

public struct Health : IComponentData
{
    public float Current;
    public float Max;
}

public struct Target : IComponentData
{
    public Entity Value;
}

// Tag component (zero-size marker)
public struct EnemyTag : IComponentData { }
public struct PlayerTag : IComponentData { }

// Buffer component (variable-size array)
[InternalBufferCapacity(8)]
public struct InventoryItem : IBufferElementData
{
    public int ItemId;
    public int Quantity;
}

// Shared component (grouped entities)
public struct TeamId : ISharedComponentData
{
    public int Value;
}
```

### 模式2：使用ISystem的系统（推荐）

```csharp
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Burst;

// ISystem: Unmanaged, Burst-compatible, highest performance
[BurstCompile]
public partial struct MovementSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        // Require components before system runs
        state.RequireForUpdate<Speed>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        // Simple foreach - auto-generates job
        foreach (var (transform, speed) in
            SystemAPI.Query<RefRW<LocalTransform>, RefRO<Speed>>())
        {
            transform.ValueRW.Position +=
                new float3(0, 0, speed.ValueRO.Value * deltaTime);
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state) { }
}

// With explicit job for more control
[BurstCompile]
public partial struct MovementJobSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var job = new MoveJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
public partial struct MoveJob : IJobEntity
{
    public float DeltaTime;

    void Execute(ref LocalTransform transform, in Speed speed)
    {
        transform.Position += new float3(0, 0, speed.Value * DeltaTime);
    }
}
```

### 模式3：实体查询

```csharp
[BurstCompile]
public partial struct QueryExamplesSystem : ISystem
{
    private EntityQuery _enemyQuery;

    public void OnCreate(ref SystemState state)
    {
        // Build query manually for complex cases
        _enemyQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<EnemyTag, Health, LocalTransform>()
            .WithNone<Dead>()
            .WithOptions(EntityQueryOptions.FilterWriteGroup)
            .Build(ref state);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // SystemAPI.Query - simplest approach
        foreach (var (health, entity) in
            SystemAPI.Query<RefRW<Health>>()
                .WithAll<EnemyTag>()
                .WithEntityAccess())
        {
            if (health.ValueRO.Current <= 0)
            {
                // Mark for destruction
                SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged)
                    .DestroyEntity(entity);
            }
        }

        // Get count
        int enemyCount = _enemyQuery.CalculateEntityCount();

        // Get all entities
        var enemies = _enemyQuery.ToEntityArray(Allocator.Temp);

        // Get component arrays
        var healths = _enemyQuery.ToComponentDataArray<Health>(Allocator.Temp);
    }
}
```

### 模式4：实体命令缓冲区（结构变更）

```csharp
// Structural changes (create/destroy/add/remove) require command buffers
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct SpawnSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (spawner, transform) in
            SystemAPI.Query<RefRW<Spawner>, RefRO<LocalTransform>>())
        {
            spawner.ValueRW.Timer -= SystemAPI.Time.DeltaTime;

            if (spawner.ValueRO.Timer <= 0)
            {
                spawner.ValueRW.Timer = spawner.ValueRO.Interval;

                // Create entity (deferred until sync point)
                Entity newEntity = ecb.Instantiate(spawner.ValueRO.Prefab);

                // Set component values
                ecb.SetComponent(newEntity, new LocalTransform
                {
                    Position = transform.ValueRO.Position,
                    Rotation = quaternion.identity,
                    Scale = 1f
                });

                // Add component
                ecb.AddComponent(newEntity, new Speed { Value = 5f });
            }
        }
    }
}

// Parallel ECB usage
[BurstCompile]
public partial struct ParallelSpawnJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ECB;

    void Execute([EntityIndexInQuery] int index, in Spawner spawner)
    {
        Entity e = ECB.Instantiate(index, spawner.Prefab);
        ECB.AddComponent(index, e, new Speed { Value = 5f });
    }
}
```

### 模式5：Aspect（组件分组）

```csharp
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

// Aspect: Groups related components for cleaner code
public readonly partial struct CharacterAspect : IAspect
{
    public readonly Entity Entity;

    private readonly RefRW<LocalTransform> _transform;
    private readonly RefRO<Speed> _speed;
    private readonly RefRW<Health> _health;

    // Optional component
    [Optional]
    private readonly RefRO<Shield> _shield;

    // Buffer
    private readonly DynamicBuffer<InventoryItem> _inventory;

    public float3 Position
    {
        get => _transform.ValueRO.Position;
        set => _transform.ValueRW.Position = value;
    }

    public float CurrentHealth => _health.ValueRO.Current;
    public float MaxHealth => _health.ValueRO.Max;
    public float MoveSpeed => _speed.ValueRO.Value;

    public bool HasShield => _shield.IsValid;
    public float ShieldAmount => HasShield ? _shield.ValueRO.Amount : 0f;

    public void TakeDamage(float amount)
    {
        float remaining = amount;

        if (HasShield && _shield.ValueRO.Amount > 0)
        {
            // Shield absorbs damage first
            remaining = math.max(0, amount - _shield.ValueRO.Amount);
        }

        _health.ValueRW.Current = math.max(0, _health.ValueRO.Current - remaining);
    }

    public void Move(float3 direction, float deltaTime)
    {
        _transform.ValueRW.Position += direction * _speed.ValueRO.Value * deltaTime;
    }

    public void AddItem(int itemId, int quantity)
    {
        _inventory.Add(new InventoryItem { ItemId = itemId, Quantity = quantity });
    }
}

// Using aspect in system
[BurstCompile]
public partial struct CharacterSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;

        foreach (var character in SystemAPI.Query<CharacterAspect>())
        {
            character.Move(new float3(1, 0, 0), dt);

            if (character.CurrentHealth < character.MaxHealth * 0.5f)
            {
                // Low health logic
            }
        }
    }
}
```

### 模式6：单例组件

```csharp
// Singleton: Exactly one entity with this component
public struct GameConfig : IComponentData
{
    public float DifficultyMultiplier;
    public int MaxEnemies;
    public float SpawnRate;
}

public struct GameState : IComponentData
{
    public int Score;
    public int Wave;
    public float TimeRemaining;
}

// Create singleton on world creation
public partial struct GameInitSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        var entity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(entity, new GameConfig
        {
            DifficultyMultiplier = 1.0f,
            MaxEnemies = 100,
            SpawnRate = 2.0f
        });
        state.EntityManager.AddComponentData(entity, new GameState
        {
            Score = 0,
            Wave = 1,
            TimeRemaining = 120f
        });
    }
}

// Access singleton in system
[BurstCompile]
public partial struct ScoreSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Read singleton
        var config = SystemAPI.GetSingleton<GameConfig>();

        // Write singleton
        ref var gameState = ref SystemAPI.GetSingletonRW<GameState>().ValueRW;
        gameState.TimeRemaining -= SystemAPI.Time.DeltaTime;

        // Check exists
        if (SystemAPI.HasSingleton<GameConfig>())
        {
            // ...
        }
    }
}
```

### 模式7：Baking（转换GameObject）

```csharp
using Unity.Entities;
using UnityEngine;

// Authoring component (MonoBehaviour in Editor)
public class EnemyAuthoring : MonoBehaviour
{
    public float Speed = 5f;
    public float Health = 100f;
    public GameObject ProjectilePrefab;

    class Baker : Baker<EnemyAuthoring>
    {
        public override void Bake(EnemyAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new Speed { Value = authoring.Speed });
            AddComponent(entity, new Health
            {
                Current = authoring.Health,
                Max = authoring.Health
            });
            AddComponent(entity, new EnemyTag());

            if (authoring.ProjectilePrefab != null)
            {
                AddComponent(entity, new ProjectilePrefab
                {
                    Value = GetEntity(authoring.ProjectilePrefab, TransformUsageFlags.Dynamic)
                });
            }
        }
    }
}

// Complex baking with dependencies
public class SpawnerAuthoring : MonoBehaviour
{
    public GameObject[] Prefabs;
    public float Interval = 1f;

    class Baker : Baker<SpawnerAuthoring>
    {
        public override void Bake(SpawnerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new Spawner
            {
                Interval = authoring.Interval,
                Timer = 0f
            });

            // Bake buffer of prefabs
            var buffer = AddBuffer<SpawnPrefabElement>(entity);
            foreach (var prefab in authoring.Prefabs)
            {
                buffer.Add(new SpawnPrefabElement
                {
                    Prefab = GetEntity(prefab, TransformUsageFlags.Dynamic)
                });
            }

            // Declare dependencies
            DependsOn(authoring.Prefabs);
        }
    }
}
```

### 模式8：使用Native Collections的Jobs

```csharp
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;

[BurstCompile]
public struct SpatialHashJob : IJobParallelFor
{
    [ReadOnly]
    public NativeArray<float3> Positions;

    // Thread-safe write to hash map
    public NativeParallelMultiHashMap<int, int>.ParallelWriter HashMap;

    public float CellSize;

    public void Execute(int index)
    {
        float3 pos = Positions[index];
        int hash = GetHash(pos);
        HashMap.Add(hash, index);
    }

    int GetHash(float3 pos)
    {
        int x = (int)math.floor(pos.x / CellSize);
        int y = (int)math.floor(pos.y / CellSize);
        int z = (int)math.floor(pos.z / CellSize);
        return x * 73856093 ^ y * 19349663 ^ z * 83492791;
    }
}

[BurstCompile]
public partial struct SpatialHashSystem : ISystem
{
    private NativeParallelMultiHashMap<int, int> _hashMap;

    public void OnCreate(ref SystemState state)
    {
        _hashMap = new NativeParallelMultiHashMap<int, int>(10000, Allocator.Persistent);
    }

    public void OnDestroy(ref SystemState state)
    {
        _hashMap.Dispose();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var query = SystemAPI.QueryBuilder()
            .WithAll<LocalTransform>()
            .Build();

        int count = query.CalculateEntityCount();

        // Resize if needed
        if (_hashMap.Capacity < count)
        {
            _hashMap.Capacity = count * 2;
        }

        _hashMap.Clear();

        // Get positions
        var positions = query.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        var posFloat3 = new NativeArray<float3>(count, Allocator.TempJob);

        for (int i = 0; i < count; i++)
        {
            posFloat3[i] = positions[i].Position;
        }

        // Build hash map
        var hashJob = new SpatialHashJob
        {
            Positions = posFloat3,
            HashMap = _hashMap.AsParallelWriter(),
            CellSize = 10f
        };

        state.Dependency = hashJob.Schedule(count, 64, state.Dependency);

        // Cleanup
        positions.Dispose(state.Dependency);
        posFloat3.Dispose(state.Dependency);
    }
}
```

## 性能优化技巧

```csharp
// 1. Use Burst everywhere
[BurstCompile]
public partial struct MySystem : ISystem { }

// 2. Prefer IJobEntity over manual iteration
[BurstCompile]
partial struct OptimizedJob : IJobEntity
{
    void Execute(ref LocalTransform transform) { }
}

// 3. Schedule parallel when possible
state.Dependency = job.ScheduleParallel(state.Dependency);

// 4. Use ScheduleParallel with chunk iteration
[BurstCompile]
partial struct ChunkJob : IJobChunk
{
    public ComponentTypeHandle<Health> HealthHandle;

    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex,
        bool useEnabledMask, in v128 chunkEnabledMask)
    {
        var healths = chunk.GetNativeArray(ref HealthHandle);
        for (int i = 0; i < chunk.Count; i++)
        {
            // Process
        }
    }
}

// 5. Avoid structural changes in hot paths
// Use enableable components instead of add/remove
public struct Disabled : IComponentData, IEnableableComponent { }
```

## 最佳实践

### 建议事项

- **优先使用ISystem而非SystemBase** - 性能更优
- **所有代码都使用Burst编译** - 大幅提升速度
- **批量处理结构变更** - 使用ECB
- **使用Profiler进行性能分析** - 定位性能瓶颈
- **使用Aspects** - 更清晰地分组组件

### 禁忌事项

- **不要使用托管类型** - 会破坏Burst编译
- **不要在Job中进行结构变更** - 使用ECB替代
- **不要过度设计** - 从简单实现开始
- **不要忽略块利用率** - 对相似实体进行分组
- **不要忘记释放资源** - Native集合会导致内存泄漏

## 参考资源

- [Unity DOTS 文档](https://docs.unity3d.com/Packages/com.unity.entities@latest)
- [Unity DOTS 示例](https://github.com/Unity-Technologies/EntityComponentSystemSamples)
- [Burst 用户指南](https://docs.unity3d.com/Packages/com.unity.burst@latest)
