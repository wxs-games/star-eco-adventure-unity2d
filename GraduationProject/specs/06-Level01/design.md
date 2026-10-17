# 技术方案：06-Level01

> 对应 `requirements.md` R1~R8 | 命名空间 `StarEco.Level` | 代码落点 `Assets/Scripts/Level/`
> 前置已就位：M1 `MonoSingleton`/`EventCenter`/`PoolManager`/`SaveSystem`、M2 `PlayerController`（`InputLocked`/`IsDead`/`Body`/`Sprite`）、M3 `PlayerCombat`（`CurrentHP`/`MaxHP`）、M4 `EnemyControllerBase`（`MarkPooled`/`ResetForReuse`）与 `EnemyDeathEvent`、M5 `PuritySystem`（`Current`/`Total`/`Percent`/`Pollution`）、`PurifyTank`（`Current`/`Consume`/`Restore`）、`IPurifiable`、`GarbagePurifiedEvent`/`PurityChangedEvent`/`LevelPurifiedEvent`/`PickupCollectedEvent`
> 立项决策（已定稿）：对 M1~M5 **零源码改动**，只调用公开接口与订阅事件；关卡内容用 `LevelConfig` ScriptableObject 数据驱动；结算面板程序化 uGUI（无美术）；检查点单槽位快照且**不回滚世界状态**（只回滚玩家）；关卡完成判据 = `LevelPurifiedEvent` 或目标链全完成先到者

## 1. 结构

### 1.1 与 M1~M5 的边界：复用 / 新建 / 为何不重构（对应 R2、R5、R6）

| 边界项 | 处理 | 依据 |
|---|---|---|
| M1 `MonoSingleton` | **复用为 `LevelManager` 基类** | 关卡流程天然全局单例（跨场景 DontDestroyOnLoad 是既有能力） |
| M1 `EventCenter` | **复用，零改动** | 新增关卡事件走同一条类型化发布订阅，保持零 GC struct 风格 |
| M1 `PoolManager` | **复用，敌人走池** | `EnemyControllerBase.Recycle` 已实现"池借出走 Release、场景实例走 SetActive(false)"双路径（R5：只需 `MarkPooled` + `_sourcePrefab`） |
| M1 `SaveSystem` | **复用，零改动** | 泛型 `Save<T>/Load<T>` + `Exists` 已足够（R6：`LevelSaveService` 只做 DTO 与槽位命名） |
| M2 `PlayerController` | **只调用**：`InputLocked` / `IsDead` / `transform.position` / `Body.velocity` / `Sprite.flipX` | R7 冻结输入、R4 传送恢复 |
| M2 `PlayerController.ApplyHit` 死亡入口 | **不改**，由 M6 侧 `Update` 轮询 `IsDead` 判失败 | 避免为 M6 在玩家 FSM 里加状态（YAGNI：死亡→关卡失败是流程问题，不是玩家状态） |
| M3 `PlayerCombat` | **只调用**：`CurrentHP` / `MaxHP` | R4 快照与恢复 |
| M4 `EnemyControllerBase` | **只调用**：`MarkPooled()`、`ResetForReuse()`、`Config.EnemyTypeId` | R5 刷怪 |
| M4 `EnemyDeathEvent` | **订阅** | R8 击杀统计 + R5 名额释放 |
| M5 `PuritySystem` | **只读** `Current`/`Total`/`Percent`/`Pollution`；订阅 `LevelPurifiedEvent`/`PurityChangedEvent` | R2 完成判据、R5 刷怪速率 |
| M5 `PurifyTank` | **只调用** `Current`/`Restore`/`Consume` | R4 快照与恢复 |
| M5 `IPurifiable` 目标计数 | **运行时统计**（非改 `PuritySystem`） | R3 需要"净化 N 个污染源"，`PuritySystem.Total` 只有总量没有分类计数，故 `LevelObjectiveTracker` 自建分类计数 |
| HUD / 对话 / 提示牌 / 第二三关 | **不做** | 需求已声明非目标 |

> **一句话边界**：M6 **新增** `StarEco.Level` 全部文件；M1~M5 全部**零改动**（本轮唯一可能触及的是 `PlayerCombat` 是否需要 `SetHP` —— 见 §3 取舍，结论为**不改**，用反射-free 的公开 setter 缺失方案：直接改 `CurrentHP` 不可行，改为 `RestoreHP` 最小增量 5 行，见 §2.3）。

### 1.2 文件结构

```text
Assets/Scripts/Level/
  LevelConfig.cs              ScriptableObject：关卡序号/名/目标链/刷怪表/星级阈值/限时/出生点
  LevelState.cs               四态枚举 + 终态判定
  LevelManager.cs             单例：状态机、快照、定时、终态事件、RestartLevel
  LevelObjectiveTracker.cs    目标链：顺序解锁、分类计数、目标文本广播
  LevelSpawner.cs             污染度驱动刷怪（对象池 + 锚点 + 名额）
  LevelStats.cs               击杀/拾取统计（被动事件）
  Checkpoint.cs               Trigger 检查点（Id + 提交快照）
  LevelSnapshot.cs            [Serializable] 快照 DTO
  LevelResultData.cs          [Serializable] 结算 DTO + [Serializable] ProgressData
  LevelSaveService.cs         SaveSystem 封装：写结算/读结算/查续关
  LevelResultUI.cs            程序化 uGUI 结算面板
  Events/
    LevelCompletedEvent.cs    关卡通关（含结算数据）
    LevelFailedEvent.cs       关卡失败
    LevelStateChangedEvent.cs 状态迁移（含新旧态）
    ObjectiveCompletedEvent.cs 单个目标完成（含剩余数）
    LevelObjectiveChangedEvent.cs 当前目标文本变化（供 M7 HUD）
    CheckpointActivatedEvent.cs 检查点激活
    RestartLevelEvent.cs      面板"重新开始"请求
    ReturnToTitleEvent.cs     面板"返回标题"请求（M7 消费）
```

### 1.3 资产与场景

```text
Assets/Settings/Levels/Level01.asset      LevelConfig 实例
Assets/Scenes/Level01.unity                第一关场景（灰盒）
Assets/Settings/Levels/Level01.prefab?     不需要（配置非预制体）
```

## 2. 关键设计

### 2.1 关卡状态机（R2）

```text
NotStarted --StartLevel()--> Playing --LevelPurifiedEvent/目标链全完成--> Completed (终态)
                              |
                              |--PlayerController.IsDead--> Failed (终态)
                              |
                              |--RestartLevel()--> NotStarted --StartLevel()--> Playing
```

- 状态迁移集中在 `LevelManager.SetState`，唯一入口带幂等守卫（同态不重复广播）。
- 终态 `Completed`/`Failed` 都走 `FinishLevel(bool success)`：停表 → 冻结输入 → 生成 `LevelResultData` → 写档 → 广播终态事件。
- `RestartLevel` 由面板调用，内部重置 `LevelObjectiveTracker`/`LevelSpawner`/`LevelStats`/`PuritySystem` 无法回滚的部分按 §3 取舍处理。

### 2.2 目标链（R3）

- `LevelObjective` 为 `[Serializable]` 内嵌类（`Type`/`Required`/`Label`），随 `LevelConfig` 存储。
- `LevelObjectiveTracker` 维护 `Index`（当前目标序号）与四类计数器：
  - `PurifyTargets` ← 订阅 `GarbagePurifiedEvent` 且 `TargetId` 非塔型（塔以 `TargetId` 前缀 `Tower` 判定，与 M5 `TowerPurifiable` 约定一致）
  - `PurifyTowers` ← 同事件但 `TargetId` 以 `Tower` 开头
  - `DefeatEnemies` ← 订阅 `EnemyDeathEvent`
  - `ReachPurity` ← 订阅 `PurityChangedEvent` 比较 `Percent`
- 完成判定在每次计数变化后只检查**当前**目标（未解锁目标不计数，避免"跳过前置任务"）。
- 分类计数与 M5 分类解耦的代价：`TargetId` 字符串前缀判定是弱约定，故在 design 中显式记录；若 M8 目标种类变多，改为 `IPurifiable` 增加 `PurifyCategory` 属性（本轮不做，YAGNI）。

### 2.3 玩家 HP / 净化剂恢复（R4）

- `PlayerCombat.CurrentHP` 当前为 `private set`，恢复 HP 需要写入口。
  - 方案 A：反射设私有 setter（零改动但脆弱、GC、Editor 风险）→ **否决**。
  - 方案 B：给 `PlayerCombat` 增加 `public void RestoreHP(int hp)` 最小增量（5 行，含夹取与不复活已死状态的守卫）→ **采用**。
  - 同理 `PlayerController` 需 `Revive()`（清 `IsDead`/`InputLocked`，5 行）→ **采用**。
  - 这两处是本轮**唯一**对既有文件的改动，已在 §1.1 标注为"最小增量"，其余全部零改动。
- 净化剂恢复用 M5 既有 `PurifyTank.Consume`/`Restore` 组合（先 Consume 到底再 Restore 到目标值），**零改动**。

### 2.4 检查点快照（R4）

- 快照内容：玩家位置、`CurrentHP`、`PurifyTank.Current`、`PuritySystem.Current`、`LevelStats` 击杀/拾取、已耗时。
- 单槽位：`LevelManager.CurrentSnapshot` 覆盖写。
- **不回滚世界**：**检查点恢复**不回滚已净化的污染源与已击杀的敌人（避免重复结算与状态回滚复杂度），只回滚玩家状态与位置；该取舍在 design §3 说明。
- **关卡重开回滚世界**：`RestartLevel` 必须把可净化目标与净化值一起重置（`IResettablePurifiable.ResetPurify` + `PuritySystem.ResetProgress`），否则重开后所有目标已是已净化态、不再广播事件，目标链永远无法完成（实现期实测到的真实死局，见 §3 取舍表）。

### 2.5 污染度驱动刷怪（R5）

- `LevelSpawner` 每帧维护一个 `_timer`，各 `SpawnEntry` 独立计时。
- 间隔公式：`currentInterval = entry.BaseInterval × Mathf.Lerp(0.5f, 1.5f, pollution)`（污染高 → 刷得快）。
- 名额：`_aliveCount[entryIndex] < entry.MaxAlive` 才生成。
- 锚点：循环取 `entry.Anchors[i % count]`（复用锚点实现"多方向来敌"）。
- 生成：`PoolManager.Instance.Get(entry.Prefab, 1)` → `MarkPooled()` → 设位置 → `SetTarget(player)`。
- 死亡：`EnemyDeathEvent` 里按 `EnemyTypeId` 匹配对应条目 `_aliveCount--`。

### 2.6 结算面板（R7）

- `LevelResultUI` 在 `Awake` 时按需构建（`Canvas` + `EventSystem` + 竖排 `Text` 行 + 两个 `Button`）。
- 字体：`Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`（2022.3 内置字体名，非 `Arial.ttf`）。
- 布局用 `RectTransform` 锚点 + `sizeDelta`，不引入 UI 资源包。
- 按钮监听在代码内 `onClick.AddListener`，不序列化 `UnityEvent`。
- 星级文本 `★★★`/`★★`/`★`/空串。

### 2.7 存档（R6）

- 槽位：`level01_result.json`（结算）、`progress.json`（已解锁关卡序号 + 是否有可续关进度）。
- DTO 全部 `[Serializable]` 且只含 JsonUtility 支持的字段（`int/float/string/bool` + `List<int>`）。
- 注意：`JsonUtility` **不支持字典与嵌套泛型**，故 `ProgressData` 用 `int unlockedLevelIndex` 平铺。

## 3. 已知取舍与后续扩展点

| 取舍 | 现状 | 后续 |
|---|---|---|
| 检查点恢复不回滚世界状态 | §2.4 | M7+ 如需"读档回到任意进度"，为敌人池记录"本局已死亡"集合 |
| 关卡重开需要世界回滚 | 新增 `IResettablePurifiable`（Eco 侧可选接口）+ `PuritySystem.ResetProgress()`；`RestartLevel` 遍历重置 | 已实现。**注意塔是单向开关**：`Tower01Controller.RequestDisable` 后不可复活，故重开后塔保持关闭、不再贡献净化值 —— M6 灰盒的一关可正常重跑；若要完美重开需 M4 增加塔复活入口 |
| 塔型目标靠 `TargetId` 前缀判定 | §2.2 | M8 目标种类增多时改为强类型分类字段 |
| 面板为程序化 uGUI 占位 | §2.6 | M7 换正式 UI 预制体 + 美术图，逻辑层只换 View 实现 |
| 限时失败（timeout）本轮未实现 | `LevelConfig` 预留 `TimeLimit` 字段但 R2 未要求超时失败 | R2 只要求玩家死亡失败；超时失败列入 M7（M7 有提示牌/时间压力 UI 时一起做），本轮字段保留不实现 |
| 第二三关内容 | 不做 | M8 只需新建 `Level02/03` 配置资产 + 场景，复用全部逻辑 |
| Player 预制体此前缺 M3/M5 组件 | 新增 `Editor/PlayerPrefabSetup.cs`（菜单 `StarEco/Setup/Player Prefab`），用 `LoadPrefabContents` 隔离上下文固化 | 实现期发现：M3/M5 开发时组件直接挂在 SampleScene 实例上，预制体并非唯一事实来源，新场景实例化会缺战斗与环保功能 |

> **注意**：上表"限时失败未实现"是一项**主动的范围收敛**（超出 R1~R8 需求），不是漏项 —— `TimeLimit` 字段写入配置但无消费方，`LevelSetup` 不填该值。

## 4. 验收方式

1. `LevelSetup` 一键生成 `Level01.asset` + `Level01.unity` 灰盒（地面/玩家出生点/3 垃圾堆/1 挡路墙/1 塔/2 刷怪锚点/2 检查点/氛围+PuritySystem+LevelManager）。
2. Console 零 error。
3. 运行验证清单（写入 `tasks.md` T20）：目标链推进、污染度刷怪、检查点死亡恢复、结算面板星级、存档写读。
