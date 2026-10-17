# 设计文档：09-Level02（第二关与全流程）

> 对应 `requirements.md` R1~R7。本文只记录"为什么这样做"与关键取舍，不重复需求条文。

## 1. 总体结构

```
Level02（横向 64u，三段式空间动线）
┌───────────────┬──────────────────────┬───────────────────┐
│ 左：森林出生区  │ 中：河道（水域+堵塞点） │ 右：森林净化区      │
│ 出生点(-14,2)  │ 堵塞点 ×3（IPurifiable）│ 污染源 ×3          │
│ 陷阱笼 ×2      │ 木筏通路（初始禁用）    │ 刷怪锚点 ×2        │
│ 检查点 CP_1    │ 水位（低→高）          │ 检查点 CP_2        │
└───────────────┴──────────────────────┴───────────────────┘
目标链：解救动物×2 → 疏通河道×1 → 净化污染源×3 → 击败毒植物×4
```

空间即引导：左区教解救、中区教解谜、右区收官（净化+战斗）。河道不过，右侧内容够不着——水流解谜天然成为进度门。

## 2. 关键设计决策

### 2.1 解救 = E 键互动（用户确认，社区主流）

- 与 M7 `DialogueTrigger` 完全同构：半径检测 + 世界空间气泡"按 E 解救" + `Input.GetKeyDown(KeyCode.E)`。
- 不新增输入通道：E 键在 M7 已是"互动"语义（对话/提示牌），解救是第三种消费方，语义一致。
- 玩家 `Interact` 动画（M2 已生成）：触发时经 `PlayerAnimatorDriver.Interact` Trigger 播放，0.3s 后开笼。若玩家未挂 Animator 或动画缺失，跳过动画不阻断流程（`Animator` 空值保护）。
- 为什么不做"喷雾解救/夹钳解救"：喷雾是战斗/净化资源通道，把任务进度绑在战斗资源上会让"没净化剂就卡关"；E 键零成本、无失败态，符合"教学关之后的轻度叙事互动"定位。

### 2.2 水流解谜 = 清堵塞→水位升→通路（用户确认）

- 堵塞点 = 挂 `IPurifiable` 的河道污染障碍（复用 M5 净化流水线）：玩家用喷雾清 3 处 → `GarbagePurifiedEvent` ×3 → `WaterPuzzleController` 收齐后触发完成。
- 为什么堵塞点用净化而不是新交互：① 复用既有输入（K 喷雾）与既有目标计数（`GarbagePurifiedEvent`）；② 与关卡主题一致——"清理污染让生态恢复"；③ 零新输入教学成本。
- 注意：堵塞点的 `GarbagePurifiedEvent` 会被 `LevelObjectiveTracker` 同时计入 `_purifiedTargets`（见 2.5 的边界处理），因此堵塞点的 `TargetId` 使用 `WaterBlock` 前缀，并在 Tracker 侧对 PuzzleSolved 目标做独立计数，互不干扰。
- 水位上升 = 视觉水体色块 1.5s 插值上移 + 木筏通路 `BoxCollider2D` 组启用。上升完成后保持高位（本关不回退——关卡通关节奏，不做可逆解谜）。
- 通路初始禁用的两种实现：A. 碰撞体 `enabled=false`；B. 平台 GameObject `SetActive(false)`。选 B（整组关闭更干净，恢复也无残留），但在 `WaterPuzzleController` 持有引用以便启停。

### 2.3 目标链扩展：两个新枚举（最小增量）

`LevelObjectiveType` 增加：
- `RescueTargets = 4`：`AnimalRescuedEvent` 计数。
- `PuzzleSolved = 5`：`GarbagePurifiedEvent` 中 TargetId 前缀 `WaterBlock` 的计数（= 已清理堵塞点数，HUD 可见 x/3 进度）。

`LevelObjectiveTracker` 增量只有 4 处：
1. `OnEnable/OnDisable` 加 1 个订阅（`AnimalRescuedEvent`）；
2. `OnGarbagePurified` 加 1 个前缀分支（`WaterBlock` → `_waterCleared++`，不再进 `_purifiedTargets`）；
3. `CurrentCountFor` 加 2 个 case（RescueTargets / PuzzleSolved）+ `Initialize/ResetChain` 重置 `_rescuedAnimals`/`_waterCleared`；
4. `ClampRequired` 加 1 个扫描分支（数场景 `WaterBlockage` 实例夹取 PuzzleSolved 需求）。

推进算法、广播逻辑零改动。

### 2.4 全流程与解锁：机制已在，只修对齐点

`LevelSaveService.CommitResult` 已实现"通关即解锁下一关"（M6 遗留语义）。M9 对齐点：
- `LevelSelectPanel.Levels[1].BuildIndex`：-1 → 2（第二关已建成）；
- `ResultPanel` 下一关加载逻辑按 `BuildIndex` 走既有分支（第三关已验证，第二关同构）；
- `MainMenuPanel`"继续游戏"钳制 `Mathf.Clamp(progress.UnlockedLevelIndex, 1, 1)` → 放开到 3（原来只允许第 1 关是因为只建了 1 关；现在三关都在 Build Settings 里）。

### 2.5 边界与取舍

| 边界 | 处理 |
|---|---|
| 堵塞点被喷雾清零时同时污染度上升 | 接受：堵塞点算污染源的一部分，`ReachPurity` 目标天然受益；本关目标链无 ReachPurity 段，无副作用 |
| `GarbagePurifiedEvent` 的 `_purifiedTargets` 计数被堵塞点占用 | 目标链第③段在堵塞点全部完成后才激活，此时 3 处堵塞已计入 3；但堵塞点 TargetId 用 `WaterBlock` 前缀，Tracker 的 `IsTower` 判定只分流 Tower，`_purifiedTargets` 仍会 +3。第③段 Required=3 指"右侧 3 处污染源"，而此时计数已有 3 → 第③段会瞬间完成。**修正：堵塞点不计入 `_purifiedTargets`**（Tracker 按 TargetId 前缀 `WaterBlock` 跳过累加，只进 `_waterCleared`） |
| 玩家死亡后 RescueTarget 状态 | 重载场景重置（M6 `RestartLevel` 策略，同 Tower 单向开关教训） |
| 木筏通路激活后玩家站上面 | 通路是静态平台，激活后玩家正常行走；无移动木筏（避免物理驱动复杂度，M10 可升级） |
| 第二关通关星级 | 与 M6 一致按 `PurityPercent` 评 S/A/B/C：净化堵塞点 + 3 处污染源可达 100% |

## 3. 文件清单与改动面

### 新增

| 文件 | 职责 |
|---|---|
| `Assets/Scripts/Level/RescueTarget.cs` | E 键解救互动（气泡+开笼+事件） |
| `Assets/Scripts/Level/WaterPuzzleController.cs` | 堵塞点计数→水位上升→通路激活→事件 |
| `Assets/Scripts/Level/DrownHazard.cs` | 河道溺水区（复用既有 IHittable 伤害流水线致死→检查点复活） |
| `Assets/Scripts/Level/CameraFollow2D.cs` | 相机 X 跟随 + 关卡边界钳制（56u 长关必需；只挂 Level02） |
| `Assets/Scripts/Level/Events/AnimalRescuedEvent.cs` | struct 事件 |
| `Assets/Scripts/Level/Events/PuzzleSolvedEvent.cs` | struct 事件 |
| `Assets/Scripts/Eco/WaterBlockage.cs` | 河道堵塞点：`IPurifiable` 适配器（与 GarbagePile/TowerPurifiable 同目录；TargetId=`WaterBlockN`） |
| `Assets/Scripts/Editor/Level02Setup.cs` | 一键搭 Level02（配置+场景+Build Settings 定序） |
| `GraduationProject/specs/09-Level02/{requirements,design,tasks}.md` | 本 spec 三件套 |

### 最小增量（既有文件）

| 文件 | 改动 | 论证 |
|---|---|---|
| `Level/LevelConfig.cs` | 枚举 +2 值 | R3 解谜与 R2 解救的目标类型 |
| `Level/LevelObjectiveTracker.cs` | 订阅 1 事件（AnimalRescued）+ 2 计数 case + `WaterBlock` 前缀分支 + `ClampRequired` 扫描堵塞点 | 目标链复用，算法不动 |
| `UI/LevelSelectPanel.cs` | 第二关 BuildIndex -1→2 + 简介文案 | R6 |
| `UI/MainMenuPanel.cs` | 继续游戏钳制 1→3 + `SceneNames.ForLevel` | R6 |
| `UI/ResultPanel.cs` | 下一关 `nextBuild` 由硬编码 -1 改为 `next<=3 ? next : -1` | R6 |

## 4. 验证策略

1. **编译**：Unity MCP `refresh_unity` + `read_console` 零错误。
2. **Play 分项**（`manage_editor` 进 Play，`execute_code` 断言）：
   - 解救：靠近笼子出现气泡 → E → 笼消失 → HUD 目标从"解救动物 0/2"变 1/2；
   - 解谜：喷雾清 1/2/3 处堵塞 → HUD"疏通河道" → 水位上升 → 通路可走；
   - 目标链：四段依次完成 → 结算面板 S/A/B/C；
   - 解锁：通关后 `LevelSaveService.LoadProgress().UnlockedLevelIndex` 递增；
   - 回归：Level01/Level03 各进一次无新错误。
3. **静态检查**：Build Settings 顺序（Title/01/02/03 = 0/1/2/3）与 `LevelSelectPanel` 索引一致。
