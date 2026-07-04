# 最终分工：GamePlay × 画面表现

> 替代 01/02/03 的剩余任务划分。
> **P = 表现程序**（本文档读者之一，原程序A）：特效、后处理、动效、动画、所有"会动的视觉"。
> **G = GamePlay 程序**：规则、状态机、关卡流程、三选一、配置、输入、UI 逻辑。
> 接缝只有一条：**事件日志（List&lt;GameEvent&gt;）**。G 算出日志，P 播放日志。

---

## 为什么这样切是对的

现有代码已经是"先算后播"：`SmackResolver.ProcessEventQueue()` 纯逻辑跑完产出 `_executedLog`（每条事件含类型、起终点、得分、连击快照），然后才逐条播动画。
把播放部分拆出来给 P，两侧就完全解耦：

- **G 的世界里没有动画**：接一个 Null 播放器（立即回调），全部规则用 EditMode 测试 + SmackDebugger 验证。
- **P 的世界里没有规则**：拿一份序列化好的事件日志反复回放，调动画、调特效、调后处理，全程不需要 G 在场。

---

## 接口契约（在现有基础上改动极小）

### 1. 事件日志格式（冻结，双方共同资产）

`GameEvent` 现有字段即为契约：`Type / TargetPieceId / SourcePieceId / Direction / FromPos / ToPos / ScoreDelta / ComboAtTrigger / ScoreOriginPos / SpawnedPiece / RemovedView / Skipped / Executed`。
改字段 = 双方当面同意。

### 2. 播放器接口（新增，P 实现，G 调用）

```csharp
public interface IEventPlayer
{
    // G 在逻辑结算完成后调用；P 播完全部事件后回调 onComplete（恰好一次）
    void Play(List<GameEvent> log, Action onComplete);
    // G 的调试用：立即完成不播动画（P 顺手实现，一行）
    bool SkipAnimations { get; set; }
}
```

`SmackResolver` 改造：删除内部 `PlayEventLog/PlayEvent` 协程，改为 `Init(...)` 时注入 `IEventPlayer`，结算完调 `player.Play(_executedLog, () => onRoundStable(result))`。
G 侧测试用 `NullEventPlayer`（Play 即回调）。

### 3. 表现资源接口（现状保留，全部归 P 实现）

`IPieceView / IPieceViewFactory / IBoardView.HexToWorld / IHUDView.ShowScorePop` 维持现有签名。
注意：`Spawn` 事件中 View 的创建时机从"逻辑阶段"移到"播放阶段"——P 在播放 Spawn 事件时自己调工厂建 View 并赋给 `piece.View`（G 的逻辑阶段不再碰工厂，彻底纯逻辑）。

### 4. 表现驱动数据（G → P 的单向通知，除日志外仅这几条）

| 通知 | 时机 | P 的用途 |
|---|---|---|
| `OnBoardRebuilt(Board)` | 关卡/轮次初始化后 | 重建棋盘与棋子渲染 |
| `OnPieceSpawned(Piece)` | 三选一放置、初始生成 | 建 View、播生成动效 |
| `OnWallAdded(Hex, Piece removedPiece)` | 轮间墙体 | 建墙 View、播替换表现 |
| `OnOrientationChanged(int orientation)` | 旋转操作 | 播棋盘旋转动效（逻辑坐标不变，只转表现根节点） |
| `OnGameStateChanged(state)` | 状态机切换 | UI 转场、输入提示、后处理切换（如结算时景深/发光） |

实现方式不限（C# event 即可），签名由双方第一天定死。

---

## 现有代码移交清单

| 文件/目录 | 归属 |
|---|---|
| `Shared/Hex, Board, Piece, PieceTypes, SmackRules` | G（已冻结，P 只读） |
| `Core/SmackResolver`（去掉播放协程后） | G |
| `Core/GameEvent` | 契约（双方共有，改动需同意） |
| `Core/SmackDebugger`、`Tests/` 全部 | G（他的验证工具） |
| `Shared/IPieceView, IPieceViewFactory, IBoardView, IHUDView, PlaceholderPieceView` | P |
| `TempProgramBPlaytest/`（渲染、HexToWorld、旋转表现） | P（转正拆分：渲染部分留下，规则调用部分归 G 的流程代码） |
| `Prefabs/ZZNC/`、`Editor/ZZNC/ZZNCPrototypeArtGenerator` | P |

---

## G 的任务清单（GamePlay 程序）

1. **接手 SmackResolver**：读懂现有实现（40+ 测试是最好的文档），完成 IEventPlayer 注入改造，用 NullEventPlayer 保持全部测试绿。
2. **状态机**：enum + switch，照需求文档 14。
3. **关卡/轮次流程**：初始化（建棋盘/摆初始棋子/墙体）、轮次结算（`CurrentScore >= TargetScore` 通过，需求文档 5.2）、失败重开、关卡切换、无尽入口。
4. **三选一逻辑**：抽选、放置、棋盘满丢弃。UI 的按钮响应归 G，按钮的动效归 P。
5. **输入**：旋转（维护 BoardOrientation 0~5）、拍击（扣次数、锁输入、调 ExecuteSmack）。
6. **配置表**：LevelConfig / PieceConfig / 棋盘模板（6/8/10）/ SmackRules 打包。
7. **UI 逻辑**：HUD 数据绑定（分数/次数/目标分）、结算与失败界面的显示逻辑、调试作弊面板（加分/强制结算/跳三选一——测流程必备）。
8. **发出第 4 节的全部通知**。

**独立验证**：NullEventPlayer + 作弊面板 + 现有测试。整个游戏无画面跑通（Console 日志即验收）。

## P 的任务清单（表现程序，你）

1. **EventPlayer**：从 SmackResolver 拆出播放协程，实现 IEventPlayer。严格串行（策划案硬要求），用 View 返回时长串时序，实现 SkipAnimations。
2. **回放调试器（你的第一优先）**：把 SmackDebugger 四个 Case 的事件日志序列化存成资产（或运行时录制），做一个 `ReplayDebugger`：按键加载日志→循环播放。**这是你全程的独立验证器**，调任何动画特效都靠它，不需要 G。
3. **棋盘与棋子渲染转正**：TempPlaytest 的渲染部分整理成正式 BoardView / PieceViewFactory；HexToWorld 含棋盘旋转。
4. **棋子动画**：移动补间（DOTween）、受击震动、能力特效（四种棋子区分：得分金色、爆炸放射、分裂双核心）、生成/移除、推出棋盘飞出。
5. **棋盘旋转动效**：60° 补间、重力箭头常驻指示。
6. **屏幕后处理**：拍击冲击（震屏/径向模糊）、连击升级的画面反馈、结算时刻的强调（URP Volume）。
7. **飘分与连击数字**：实现 ShowScorePop；连击数字随 ComboAtTrigger 递增的表现升级。
8. **UI 动效皮肤**：按钮反馈、界面转场、"棋盘已满/能量过载"提示的表现。UI 里的数字逻辑是 G 的，你只管让它好看。
9. **旋转预览虚影**（可裁）：现有 SimulateSmack 原型转正，落点虚影+碰撞描边。

**独立验证**：ReplayDebugger 回放日志即可开发验证 1~7；UI 动效用 G 的作弊面板或自建假数据驱动。

---

## 联调点（两个）

| 时间点 | 内容 | 验收 |
|---|---|---|
| 联调①：IEventPlayer 拆分完成后（尽早，半天内） | G 的 SmackResolver 注入 P 的真 EventPlayer | SmackDebugger 四个 Case 在场景里带动画全通过 |
| 联调②：全部完成后 | 完整三关 + 无尽入口 | 需求文档 4.1 全流程；之后只修 bug |

## 裁剪顺序

① 后处理打磨 → ② 旋转预览虚影 → ③ UI 动效（保留纯功能 UI） → ④ 连击表现升级（保留基础飘字） → ⑤ 无尽模式（退化通关画面）。
**不可裁**：事件严格串行播放（玩法可读性底线）。

## 纪律

- `GameEvent` 字段与 `IEventPlayer` 签名 = 最高级契约，改动双方当面同意。
- G 不写动画不调 View；P 不写规则不写 Board（播放阶段建 View 除外）。
- P 的 ReplayDebugger 场景自留；主场景归 G 搭骨架，P 往里填表现。
- 每 2~3 小时 push。
