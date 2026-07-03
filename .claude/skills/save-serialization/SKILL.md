---
name: save-serialization
description: Unity 存档与序列化方案。涵盖 JsonUtility/Newtonsoft.Json/MessagePack 选型、二进制存档、版本迁移（version + migrator）、AES/XOR 加密、PersistentDataPath 路径策略、云存档同步与冲突解决。触发关键词：存档、save、序列化、json、binary、版本迁移、加密、PersistentDataPath、云存档、MessagePack、Newtonsoft。
tags: unity, save, serialization, json, migration
---

# Unity 存档与序列化

## 何时使用

- 设计首个存档系统，纠结选哪个序列化器
- 老存档读不出来（字段改名、类型变更、删字段）
- 玩家反馈存档损坏 / 闪退后丢档
- 要支持云存档（Steam Cloud / iCloud / Google Play Games）
- 防止玩家用 Cheat Engine / 文本编辑器改存档

## 核心原则

- **任何会上线的存档第一天就要带 `version` 字段**，没有迁移代码也要先留着
- **存档路径只能用 `Application.persistentDataPath`**，dataPath 在 iOS/Android 是只读
- **写盘走"写临时文件 + 原子重命名"**，防止写一半断电导致存档损坏
- **加密只防小白，不防破解者**，真要防外挂得服务器校验
- 不要在主线程同步写大存档，用 UniTask + `File.WriteAllBytesAsync`

## 关键模式

### 模式 A：序列化器选型

| 方案 | 速度 | 大小 | 字段命名 | 多态/字典 | 推荐场景 |
|------|------|------|----------|-----------|----------|
| `JsonUtility` | 中 | 大 | 仅 public 字段 | 不支持 | 简单单机、热更配置 |
| `Newtonsoft.Json` | 慢 | 大 | 全支持 + `[JsonProperty]` | 支持 | 大多数项目首选 |
| `MessagePack-CSharp` | 极快 | 小 | `[Key(0)]` 索引 | 支持 | 性能敏感、大存档、网络 |
| BinaryFormatter | - | - | - | - | **禁用**，安全漏洞且已弃用 |

Newtonsoft 设置基线：
```csharp
new JsonSerializerSettings {
    TypeNameHandling = TypeNameHandling.Auto,  // 多态
    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
    NullValueHandling = NullValueHandling.Ignore,
    Converters = { new StringEnumConverter() }
};
```

### 模式 B：版本字段 + 迁移器

```csharp
[Serializable] public class SaveData {
    public int version = CURRENT_VERSION;
    public const int CURRENT_VERSION = 3;
    public PlayerData player;
    public List<ItemData> items;
}

public static class SaveMigrator {
    static readonly Dictionary<int, Func<JObject, JObject>> _migrations = new() {
        [1] = v1 => { v1["items"] = new JArray(); return v1; },         // v1→v2 加 items
        [2] = v2 => { v2["player"]["lv"].Rename("level"); return v2; }, // v2→v3 改名
    };
    public static SaveData Load(string json) {
        var jo = JObject.Parse(json);
        int v = jo["version"]?.Value<int>() ?? 0;
        while (v < SaveData.CURRENT_VERSION) {
            jo = _migrations[v](jo);
            v++; jo["version"] = v;
        }
        return jo.ToObject<SaveData>();
    }
}
```

**铁律**：迁移函数只增不改、版本号只增不复用、旧迁移函数永远不删。

### 模式 C：原子写入

```csharp
public static async UniTask SaveAtomicAsync(string path, byte[] data) {
    var tmp = path + ".tmp";
    var bak = path + ".bak";
    await File.WriteAllBytesAsync(tmp, data);
    if (File.Exists(path)) File.Replace(tmp, path, bak); // 原子
    else File.Move(tmp, path);
}
```
读时若主文件损坏，fallback 到 `.bak`。

### 模式 D：加密（AES，防文本编辑器）

```csharp
using var aes = Aes.Create();
aes.Key = Convert.FromBase64String(KEY_B64); // 16/24/32 字节
aes.IV = iv;                                  // 每次随机生成存在文件头
using var enc = aes.CreateEncryptor();
var cipher = enc.TransformFinalBlock(plain, 0, plain.Length);
File.WriteAllBytes(path, iv.Concat(cipher).ToArray());
```
密钥**绝对不要硬编码字符串字面量**，用 `byte[]` 拼接 + IL2CPP 混淆。XOR 仅作"防熊孩子"用。

### 模式 E：云存档冲突

策略选一种并写文档：
- **Last Write Wins**：取 `lastModified` 大的，简单粗暴
- **Merge**：分字段合并（金币取大、关卡取深）
- **Ask User**：UI 让玩家选

存档结构里必须有 `lastModified`(UTC) + `deviceId` + `playTime` 三件套用于冲突判定。

## 常见坑

- **`JsonUtility` 不序列化 `Dictionary`**：用 List<KeyValuePair> 包装，或换 Newtonsoft
- **`[SerializeField] private` 字段 JsonUtility 能序列化，Newtonsoft 默认不能**：加 `[JsonProperty]`
- **Newtonsoft 反序列化 Vector3 报错**：循环引用 `normalized` 属性，加 `ReferenceLoopHandling.Ignore` 或自定义 Converter
- **MessagePack 加新字段后旧存档崩**：必须用 `[Key(index)]` 显式编号，不能用字段名；新字段加在末尾、永不复用旧 index
- **iOS 上 `persistentDataPath` 被系统清理**：标记为不参与 iCloud 备份用 `NSURLIsExcludedFromBackupKey`，否则审核可能拒
- **WebGL 没有真实文件系统**：用 `PlayerPrefs` 或 IndexedDB（`SyncFiles`）
- **Domain Reload 关闭后 static 字段残留**：存档单例缓存的"上次保存时间"在编辑器 PlayMode 重启后还在
- **存档异步写入时玩家退出 App**：注册 `Application.quitting` + iOS `OnApplicationPause(true)` 强制 flush

## 与其他 skill 的边界

- 与 **unity-architecture-di** 的区别：架构讲 `ISaveService` 接口注入，本 skill 讲实现细节
- 与 **multiplayer-game / unity-networking** 的区别：网络游戏存档以服务器为权威，本 skill 主要讲客户端本地存档
- 与 **secrets-management** 的区别：那个讲服务端密钥管理，本 skill 讲客户端存档密钥的妥协方案
