# 技术方案（SDD）：09-Level02（第二关 · 生态森林 与全流程解锁）

> 版本：v2.0（2026-10-30 按落地代码重写） | 对应 `requirements.md` R1~R7 | 命名空间 `StarEco.Level` + `StarEco.Eco` | 代码落点 `Assets/Scripts/Level/`、`Assets/Scripts/Eco/WaterBlockage.cs`
> 前置已就位：M5 `IPurifiable`/`GarbagePurifiedEvent`/`PuritySystem`/`SprayController`、M6 `LevelManager`/`LevelObjectiveTracker`/`LevelConfig`/`LevelSaveService`、M7 `UIManager`/`DialogueTrigger`（同构参考）/`LevelSelectPanel`/`ResultPanel`/`MainMenuPanel`、M10 `AudioManager`
> 立项决策（已定稿）：解救 = **E 键互动**（社区主流，零新输入教学）；水流解谜 = **清堵塞 → 水位升 → 通路**；目标链只加 2 个枚举值（最小增量）；木筏通路用 `SetActive` 整组启停，不做移动木筏
>
> **v2.0 修正的四处失实**：① 空间结构图里的 `CP_1`/`CP_2` 检查点**已不存在**（M6 后期整体移除）② 目标链末段标签是「击败 4 只变异怪物」而非「毒植物」③ ④ §2.5 里那条"堵塞点污染 `_purifiedTargets`"的隐患**已修**（`WaterBlock` 前缀独立分流），v1.0 把"问题"和"修正"写在同一格，读起来像还没修

## 1. 总体结构

### 1.1 三段式空间动线（横向约 64u）

```text
┌────────────────────┬──────────────────────────┬──────────────────────┐
│ 左：森林出生区       │ 中：河道（水域 + 堵塞点×3） │ 右：森林净化区        │
│ 出生点（Player 摆放）│ 木筏通路（初始 SetActive(false)）│ 污染源 ×3           │
│ 陷阱笼 ×2（E 解救）  │ 水位视觉体（低 Y → 高 Y，1.5s）│ 刷怪锚点 ×2          │
│ Ava_Forest(-27.4)   │                          │ Signboard_Forest(10.5)│
│ 知识卡 ×1           │ 知识卡 ×1                │ 知识卡 ×1             │
└────────────────────┴──────────────────────────┴──────────────────────┘
目标链：解救动物 ×2 → 疏通河道 ×3 → 净化污染源 ×3 → 击败 4 只变异怪物
```

**空间即引导**：左区教"E 键互动"，中区教"喷雾能清障碍"，右区收官（净化 + 战斗）。河道不过，右侧内容够不着——水流解谜天然成为进度门，不需要额外的"锁"。

> 逐物件坐标、尺寸、排序见 `docs/LevelDesign.md` §L2 与 `docs/LevelArt.md` §L2。本文只讲结构与机制。

### 1.2 与上游的边界

| 边界项 | 处理 | 依据 |
|---|---|---|
| M5 `IPurifiable` 净化流水线 | **复用**，堵塞点 `WaterBlockage` 实现该接口 | 复用 K 键喷洒 + `GarbagePurifiedEvent`，零新输入 |
| M6 `LevelObjectiveType` | **+2 枚举值**（`RescueTargets=4` / `PuzzleSolved=5`） | 目标链复用，推进算法不动 |
| M6 `LevelObjectiveTracker` | **+4 处增量**（订阅 / 前缀分支 / 2 个 case / 夹取分支） | 见 §2.3 |
| M7 `DialogueTrigger` | **不修改，只同构**：`RescueTarget` 照抄"半径 + 世界空间气泡 + E 键"模式 | 对话与解救是同一类"走近按 E"互动，各自独立组件比抽基类更省事（差异只有 3 个动作） |
| M6 `RestartLevel` | **复用**（重载场景） | 笼子/堵塞点/水位都是单向状态，原地重置不可靠 |
| 移动木筏 | **不做** | 物理驱动平台 + 载人是独立课题（`MovingPlatform` 已有，但木筏要做"载人 + 不卡边"），M10 未排期 |

### 1.3 新增 / 改动清单

**新增**

| 文件 | 行数 | 职责 |
|---|---|---|
| `Level/RescueTarget.cs` | 203 | E 键解救：半径 2.2u + 世界空间气泡「按 E 解救」+ 0.3s 开笼 + `AnimalRescuedEvent` |
| `Level/WaterPuzzleController.cs` | 198 | 堵塞点计数 → 水位 1.5s 上移 → 木筏通路 `SetActive(true)` → `PuzzleSolvedEvent` |
| `Level/DrownHazard.cs` | 55 | 河道溺水区（走 `IHittable` 伤害流水线，连续伤害直至死亡） |
| `Level/CameraFollow2D.cs` | 154 | 横向跟随 + 关卡边界钳制 + `Shake(duration, amplitude)`（Boss 登场/击败演出也用它） |
| `Eco/WaterBlockage.cs` | 119 | 堵塞点 `IPurifiable` 适配器（`TargetId = "WaterBlock1..3"`） |
| `Level/Events/AnimalRescuedEvent.cs` | — | struct：TargetId / Position |
| `Level/Events/PuzzleSolvedEvent.cs` | — | struct：Position / ElapsedInPuzzle |
| `Editor/Level02Setup.cs` | — | 一键搭场景（配置 + 物件 + Build Settings 定序） |

**最小增量**

| 文件 | 改动 | 论证 |
|---|---|---|
| `Level/LevelConfig.cs` | 枚举 +2 值 | 两个新目标类型 |
| `Level/LevelObjectiveTracker.cs` | 订阅 `AnimalRescuedEvent` + `WaterBlock` 前缀分支 + 2 个 `CurrentCountFor` case + `ClampRequired` 扫 `WaterBlockage` | 目标链复用 |
| `UI/LevelSelectPanel.cs` | 第二关 `BuildIndex` -1 → 2 + 简介文案 | 可进入 |
| `UI/MainMenuPanel.cs` | "继续游戏"钳制 1 → 3 | 三关都在 Build Settings 里了 |
| `UI/ResultPanel.cs` | 下一关 `nextBuild = next<=3 ? next : -1` | 第 3 关不再显示"下一关" |
| 场景 | 新增 `NPC_Ava_Forest` / `Signboard_Forest` / 3 张知识卡（M10 叙事补白） | 见 `NarrativeDesign.md` §5 |

## 2. 关键机制

### 2.1 解救 = E 键互动

- 半径 **2.2u** 圆形判定（`Vector2.Distance`，不走物理 Trigger——避免与净化判定盒互相触发）。
- 气泡：`Awake` 里 `UIUtility.CreateWorldCanvas` 建 `400×120` 的世界空间画布，`localScale=0.01` 换算，`_bubbleHeight=1.8` 悬在笼顶。
- 按 E → 播玩家 `Interact` 动画 → `_openDelay = 0.3s` 后开笼（等动画起播，视觉上"伸手 → 笼门开"）。
- 开笼：`_cageVisual.SetActive(false)`（笼体 + 动物一起隐藏）→ `PlaySfx(SfxId.Rescue)` → `AnimalRescuedEvent`。
- **幂等**：`_done = true` 后 `Update` 首行直接 return，永不重复触发。
- **面板阻断**：`UIManager.Instance.IsBlocking` 为真时不可互动（对话/暂停中按 E 不会解救）。

**为什么不做"喷雾解救 / 夹钳解救"**：喷雾是战斗资源，把任务进度绑到资源上会出现"没净化剂就卡关"。

### 2.2 水流解谜

```text
初始：_raftPath.SetActive(false)（整组，含碰撞体与视觉）；水位视觉体 y = _lowY
过程：玩家喷雾清堵塞点 → 每个堵塞点发 GarbagePurifiedEvent{TargetId="WaterBlockN"}
     WaterPuzzleController.OnGarbagePurified：前缀匹配 → _cleared.Add(TargetId)
     _cleared.Count >= ExpectedTotal()（= 场景内 WaterBlockage 实例数，实测 3）
完成：Solve()
     PlaySfx(SfxId.PuzzleSolved)
     Publish(PuzzleSolvedEvent{Position, ElapsedInPuzzle})
     _rising = true → Update 里 1.5s 把水位视觉体 y 从 _lowY 插值到 _highY
     到点 → _raftPath.SetActive(true) + 一行日志
```

- **水位只升不降**：关卡通关节奏，不做可逆解谜。
- **为什么用 `SetActive` 整组而不是 `collider.enabled`**：整组关闭更干净，恢复时无残留状态（`WaterPuzzleController` 持有 `GameObject` 引用）。
- **两套计数互不依赖**：`WaterPuzzleController` 管场景表现（自己数 `HashSet`），`LevelObjectiveTracker` 管目标链（数 `_waterCleared`）。两者都从同一个 `GarbagePurifiedEvent` 读，但一方出错不影响另一方。
- **`ExpectedTotal()` 从 Inspector 列表算**：为空时 `Start` 自动扫描场景 `WaterBlockage` 兜底（Setup 工具会显式回填）。

### 2.3 目标链扩展（4 处增量）

| # | 位置 | 增量 |
|---|---|---|
| 1 | `OnEnable/OnDisable` | 订阅/退订 `AnimalRescuedEvent` |
| 2 | `OnGarbagePurified` | 前缀 `WaterBlock` → `_waterCleared++` 并 **return**（不进 `_purifiedTargets`） |
| 3 | `CurrentCountFor` + `Initialize`/`ResetChain` | +2 个 case（`RescueTargets`/`PuzzleSolved`）+ 重置 `_rescuedAnimals`/`_waterCleared` |
| 4 | `ClampRequired` | 扫场景 `WaterBlockage`，把 `PuzzleSolved` 的 `Required` 夹到实际数量 |

**这条 `return` 是 v1.0 里差点漏掉的坑**：如果堵塞点既进 `_waterCleared` 又进 `_purifiedTargets`，那么目标链第③段（净化污染源 3 个）一激活就会秒完成——因为 3 处堵塞已经占了 3 个名额。修法就是前缀分流后**立即 return**。

推进算法（`while` 循环）、广播、幂等守卫全部零改动，见 06-Level01 §2.2。

### 2.4 全流程解锁（机制已在，只修对齐点）

- `LevelSaveService.CommitResult` 早已实现"通关即 `UnlockedLevelIndex = max(旧, 本关+1)`"（M6 遗留语义），M9 不需要改它。
- 需要对齐的是**入口索引**：`LevelSelectPanel` 的第二关卡片 `BuildIndex` 从 -1 改为 2；`MainMenuPanel` 的"继续游戏"钳制从 `Clamp(x,1,1)` 放开到 `Clamp(x,1,3)`。
- 第三关通关后 `UnlockedLevelIndex` 会变成 4——`LevelSelectPanel` 与 `ResultPanel` 都必须按"≤3 才算存在"处理，否则会出现"第 4 关"的空卡片。

### 2.5 相机与关卡边界

- `CameraFollow2D`：X 跟随（带死区与平滑），Y 固定；按关卡边界做 clamp。
- 这个组件**第一批只挂 Level02**，但 M11（第三关）与 Boss 演出都复用了它的 `Shake()` ——所以它实际已是关卡层通用件（06-Level01 §6 D8）。

## 3. 数据流

### 3.1 一次解救（第 2 只动物 → 目标①完成）

```text
玩家走进笼子 2.2u 内
 → RescueTarget.Update：
      _done? return；_opening? 递减计时器
      UIManager.IsBlocking? return                      ← 面板打开不可互动
      inRange 变化 → _bubble.SetActive(true)「按 E 解救」
 → Input.GetKeyDown(E) → StartRescue()
      _opening = true；_openTimer = 0.3；气泡隐藏
      pc.TryFireAnim(PlayerAnimParams.Interact)         ← 只触发动画，不改 FSM
 → 0.3s 后 CompleteRescue()
      _done = true；_cageVisual.SetActive(false)
      AudioManager.PlaySfx(SfxId.Rescue)
      Publish(AnimalRescuedEvent{TargetId="Animal02", Position})
 → LevelObjectiveTracker.OnAnimalRescued → _rescuedAnimals = 2 ≥ Required(2)
 → Evaluate：Publish(ObjectiveCompletedEvent{0,"解救被困动物"})；_index=1
 → 目标② `PuzzleSolved` 未满足（0/3）→ BroadcastCurrent()（无订阅者）
 → HUDController.OnObjectiveCompleted → 仅 Debug.Log（HUD 目标栏 2026-10-30 移除）
```

### 3.2 一次清理堵塞 → 水位上升 → 通路打开

```text
玩家按住 K 喷第 3 处堵塞（WaterBlock3）
 → SprayController.SettleSprayTick → WaterBlockage.ReceivePurify(amount, player)
 → 累计达标 → 完成：
      · 视觉：堵塞物 Sprite 淡出/压扁 → SetActive(false)
      · Publish(GarbagePurifiedEvent{TargetId="WaterBlock3", PurityValue, Position})

三个订阅方并行：
  ① PuritySystem：Current += PurityValue → PurityChangedEvent → 背景色向绿插值（HUD 净化值面板已移除）
  ② LevelObjectiveTracker：前缀 WaterBlock → _waterCleared = 3（**不进** _purifiedTargets）
        → 目标② PuzzleSolved(3) 满足 → ObjectiveCompletedEvent
        → _index=2 → 目标③ PurifyTargets(3)：当前 _purifiedTargets = 0 → 广播「净化森林污染源 0/3」
  ③ WaterPuzzleController：_cleared.Add("WaterBlock3") → 3 >= ExpectedTotal(3) → Solve()
        PlaySfx(PuzzleSolved)
        Publish(PuzzleSolvedEvent{Position, ElapsedInPuzzle})
           ⚠️ 该事件 0 订阅者（见 §5 V3）
        _rising = true
 → Update（1.5s）：_waterVisual.position.y 从 _lowY 插值到 _highY（相机不动）
 → t >= 1：_raftPath.SetActive(true) → 玩家可过河
```

**注意顺序**：目标②在 `WaterPuzzleController` 完成水位动画**之前**就算完成了，因为计数走的是 `GarbagePurifiedEvent`。如果玩家在 1.5s 水位上升期间就冲过去，会被还没启用的通路挡住——这是有意的（视觉上"水还在涨"）。

### 3.3 一次死亡 → 重开（第二关特有：单向状态回滚）

```text
玩家落水溺亡（DrownHazard 连续伤害）或被打死
 → LevelManager.FinishLevel(false, "玩家被击败" / "跌落山崖")
 → ResultPanel 弹面板
 → 点「重新开始」→ RestartLevelEvent → 重载 Level02 场景

重载后自动回滚的全部单向状态：
  · 已解救的笼子 → 场景原始状态（笼体可见、_done=false）
  · 已清的堵塞点 → 重新存在
  · 水位 → 回到 _lowY（Start 里显式设位）
  · 木筏通路 → 重新 SetActive(false)（Start 里显式关闭）
  · 已净化的污染源 / 已死的敌人 / 净化值 → 全部归零
```

### 3.4 一次通关 → 解锁第三关

```text
目标④ DefeatEnemies(4) 满足 → CompleteChain → LevelManager.FinishLevel(true, ...)
 → LevelIndex == 2 ≠ 3 → 跳过落幕对白 → CommitFinish(true)
 → CommitResult：UnlockedLevelIndex = max(旧, 3)
 → Publish(LevelCompletedEvent) → ResultPanel：S/A/B/C + ★ + 「下一关」可见
 → 点「下一关」→ nextBuild=3 → SceneManager.LoadScene(3) → Level03
```

## 4. 设计取舍

| 取舍项 | 选择 | 放弃的方案与理由 |
|---|---|---|
| 解救交互 | E 键（与对话/提示牌同键） | 放弃"喷雾解救"：绑战斗资源会卡关；放弃"夹钳打笼子"：会误伤动物，语义也不对 |
| 解谜形式 | 清堵塞 → 水位升 → 通路 | 放弃"拉杆/开关"：`LeverSwitch` 已在第一关用过，重复；"清理污染让生态恢复"更贴主题 |
| 通路启停 | `SetActive(false)` 整组 | 放弃"只关 Collider"：视觉上木筏还在，玩家会以为能踩；也放弃"移动木筏"：物理载人平台是独立课题 |
| 水位 | 只升不降 | 放弃"可逆水位"：单向解谜的认知负担最低 |
| 堵塞点计数 | 前缀 `WaterBlock` 独立分流 | 放弃"共用 `_purifiedTargets`"：会让第③段秒完成（§2.3） |
| 相机 | 独立 `CameraFollow2D` | 放弃"用 Cinemachine"：内建包依赖 + 配置成本，154 行手写够用，还能给 Boss 演出提供 `Shake` |
| 出生点 | 场景里 Player 的摆放位置 | 放弃"按 `LevelConfig.SpawnPoint` 瞬移"：编辑器里看见的才是唯一事实（M10 改掉，见 06-Level01 §6） |
| 检查点 | 不做 | 与 06-Level01 一致：阵亡即失败 + 重载 |

## 5. 风险

| # | 风险 | 概率 | 影响 | 现状与对策 |
|---|---|---|---|---|
| V1 | 笼子与对话/提示牌的 E 键**半径重叠** → 一键两用 | 高（踩过） | 中（误解救或误开对话） | `DialogueTrigger` 半径 2.6u、`RescueTarget` 半径 2.2u，两者都在 `Update` 里读 `Input.GetKeyDown(E)`，没有互斥机制。**摆放铁律**：同类互动点中心间距必须 ≥ 4.8u（2.6+2.2）。`Ava_Forest` 就是因此从 -23.8 移到 -27.4 |
| V2 | 左岸空间被两个笼子的交互半径占满 | 中 | 中（提示牌放不下） | 两个笼子半径连续覆盖 -24.2~-15.8，提示牌只能放右岸（`Signboard_Forest` 在 10.5） |
| V3 | `PuzzleSolvedEvent` **0 订阅者** | 已存在 | 低（预留） | 目标链走 `GarbagePurifiedEvent` 前缀计数，不依赖它。将来做"解谜成就/计时"才需要 |
| V4 | `ExpectedTotal()` 依赖 Inspector 列表非空 | 中 | 中（解谜永不完成或立即完成） | `Start` 自动扫描兜底 + 列表为空时打 Warning；`ClampRequired` 也会夹 `PuzzleSolved` 的 `Required` |
| V5 | 水位上升期间玩家冲过去被挡（通路还没开） | 中 | 低（观感） | 有意为之：视觉上"水还在涨"。若反馈不佳可把 `_riseDuration` 从 1.5s 降到 0.8s |
| V6 | 第三关通关后 `UnlockedLevelIndex = 4` 越界 | 中 | 中（出现"第 4 关"空卡） | `LevelSelectPanel` / `ResultPanel` 都按 `<= 3` 判断存在性 |
| V7 | `CameraFollow2D` 的边界参数每关不同，改场景忘了改参数 | 中 | 中（相机露出场景外） | 边界值写在组件序列化字段上，`Level02Setup`/`Level03Setup` 各自回填；`OnDrawGizmosSelected` 画边界框 |

## 6. 与 requirements 的偏差说明

| 编号 | requirements 原文口径 | 实际实现 | 原因 |
|---|---|---|---|
| D1 | 检查点 `CP_1`/`CP_2` | **不存在**（M6 后期整体移除检查点系统） | 阵亡即失败 + 重载，见 06-Level01 §2.3 |
| D2 | 目标链末段「击败毒植物 ×4」 | 「击败 **4 只变异怪物**」（`DefeatEnemies`，不区分类别） | 敌人类型统一由 `EnemySpawner` 表驱动，目标只数总数；文案随之改 |
| D3 | 木筏通路"激活后玩家正常行走" | 实现无误，但**木筏 `Sprite` 为空**——`MakeRaftPlank` 传的是 `UIUtility.WhiteSprite`（运行时 `Sprite.Create`，不可序列化），存盘后 `m_Sprite` 变 null → **木筏在场景里隐形** | 视觉缺陷（不是逻辑缺陷）；玩家能走但看不见脚下是什么。登记于 `docs/LevelArt.md` 缺陷表与 RiskRegister（P1） |
| D4 | （未提） | 新增 `NPC_Ava_Forest` / `Signboard_Forest` / 3 张知识卡 | M10 叙事补白：原第二关**0 段对白 0 张卡**，是全项目叙事密度最低的关卡 |
| D5 | `DrownHazard` 复用"IHittable 致死 → 检查点复活" | 只是持续伤害致死，**无复活** | 检查点移除后的连带修正 |
| D6 | 相机"只挂 Level02" | 已挂 Level02/Level03（Boss 演出需要 `Shake`） | 复用扩展 |
