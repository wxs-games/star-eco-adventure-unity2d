# 任务清单：06-Level01

> 对应 `requirements.md` R1~R8 / `design.md`。共 26 个任务，按依赖顺序排列。
> 状态标记：`[x]` 已完成且验证通过 / `[ ]` 未完成

## 一、关卡数据配置（R1）

- [x] T1 `LevelConfig.cs`：`LevelObjectiveType` 枚举（`PurifyTargets`/`PurifyTowers`/`ReachPurity`/`DefeatEnemies`）、`[Serializable] LevelObjective`（Type/Required/Label）、`[Serializable] SpawnEntry`（Prefab/RequiredObjectiveIndex/MaxAlive/BaseInterval/Anchors[]）、`LevelConfig` 字段（LevelIndex/LevelName/Objectives/SpawnEntries/StarThresholds/TimeLimit/SpawnPoint/IntroText）+ `CreateDefault()` 兜底。验收：可 Create 资产，缺配置时返回默认值不报错。

## 二、关卡事件（R2、R3、R4、R7）

- [x] T2 `Events/` 七个事件结构体：`LevelCompletedEvent`（携带 `LevelResultData Result`）、`LevelFailedEvent`、`LevelStateChangedEvent`（Old/New）、`ObjectiveCompletedEvent`（Label/Index）、`LevelObjectiveChangedEvent`（Label/Index/Current/Required）、`CheckpointActivatedEvent`（CheckpointId/Position）、`RestartLevelEvent`、`ReturnToTitleEvent`。验收：全部 `struct`、字段只含值类型/字符串（零 GC）。

## 三、统计与存档 DTO（R6、R8）

- [x] T3 `LevelStats.cs`：击杀数、拾取数、开始时间、耗时；`Reset()`、`OnEnemyDied`、`OnPickup`；订阅 `EnemyDeathEvent`/`PickupCollectedEvent`。验收：OnEnable/OnDestroy 成对订阅/退订，重启归零。
- [x] T4 `LevelSnapshot.cs`：[Serializable] 玩家位置/HP/净化剂/击杀/拾取/耗时。
- [x] T5 `LevelResultData.cs`：[Serializable] 关卡序号/是否通关/净化度/耗时/击杀/拾取/星级/时间戳；另含 `[Serializable] ProgressData`（`unlockedLevelIndex`）。验收：字段全部 JsonUtility 可序列化（无字典/嵌套泛型）。

## 四、SaveSystem 封装（R6）

- [x] T6 `LevelSaveService.cs`：槽位常量（`level01_result.json`/`progress.json`）、`SaveResult`/`LoadResult`/`SaveProgress`/`LoadProgress`/`HasProgress`；读写失败沿用 `SaveSystem` 语义不抛异常。验收：槽位名集中定义，无散落字符串。

## 五、目标链（R3）

- [x] T7 `LevelObjectiveTracker.cs`：构造注入 `LevelConfig`/`PuritySystem`/`LevelStats`；`Initialize()` 激活首个目标并夹取不合理 Required；订阅 `GarbagePurifiedEvent`（按 `TargetId` 前缀分类塔/污染源）/`EnemyDeathEvent`/`PurityChangedEvent`；`Progress` 只检查当前目标；全部完成广播 `OnChainCompleted`。验收：未解锁目标不计数；分类计数与 M5 解耦方式符合 design §2.2。

## 六、检查点（R4）

- [x] T8 `Checkpoint.cs`：`[RequireComponent(Collider2D)]`，`_checkpointId`（默认 `CP_1`）、`_respawnOffset`；`OnTriggerEnter2D` 判定 `PlayerController` 后向 `LevelManager.Instance` 提交快照并广播激活事件（每关每 Id 只提交一次）。
- [x] T9 `LevelManager` 快照槽位与恢复：单槽位 `CurrentSnapshot` 覆盖写；`RespawnFromCheckpoint()` 传送玩家 + 恢复 HP/净化剂 + 保持世界净化值（design §2.4 明确不回滚）；无快照时回退配置出生点并计一次 `NoCheckpointRespawn`。

## 七、玩家最小增量（R4，设计 §2.3 唯一允许的既有文件改动）

- [x] T10 `PlayerCombat.RestoreHP(int hp)`：夹取 `[0, MaxHP]`、`hp>0` 时不复活已死状态（不碰 `IsDead`）。验收：`M3` 行为零回归（伤害流程不变）。
- [x] T11 `PlayerController.Revive()`：清 `IsDead`/`InputLocked`、零化速度与击退、复位动画参数。验收：死亡→复活后可正常跳跃/攻击。

## 八、刷怪（R5）

- [x] T12 `LevelSpawner.cs`：按 `SpawnEntries` 逐条目独立计时、`MaxAlive` 名额、锚点轮询、`PoolManager.Get` + `MarkPooled()` + `SetTarget(player)`；间隔 `BaseInterval × Lerp(0.5,1.5,Pollution)`；订阅 `PurityChangedEvent` 缓存污染度、`EnemyDeathEvent` 释放名额（按 `EnemyTypeId` 匹配）；锚点为空/预制体缺失则报错并跳过。验收：连续击杀后名额正确回收不泄漏。

## 九、关卡状态机与流程编排（R2）

- [x] T13 `LevelState.cs`：四态枚举 + `IsFinal` 辅助。
- [x] T14 `LevelManager.cs`：`MonoSingleton<LevelManager>`；`[SerializeField] LevelConfig _config`；`StartLevel/RestartLevel/SetState/FinishLevel`；每帧计时与 `IsDead` 轮询；依赖缺失 → 报错 + `Failed`；终态幂等（一次）；`LevelPurifiedEvent` 订阅 → `Completed`。
- [x] T15 `LevelManager` 目标链联动：订阅 `ObjectiveCompletedEvent` 的链完成信号与 `LevelObjectiveChangedEvent` 广播当前目标；依赖注入 `LevelObjectiveTracker`/`LevelSpawner`/`LevelStats`（场景 Awake 顺序不确定，采用惰性获取 `Instance`）。

## 十、结算面板（R7）

- [x] T16 `LevelResultUI.cs`：程序化构建 Canvas + EventSystem（缺失时自动创建）；`LegacyRuntime.ttf` 内置字体；展示胜负标题/关卡名/净化度/耗时/击杀/拾取/星级文本；`InputLocked` 冻结玩家；两个按钮（重新开始 / 返回标题）分别发 `RestartLevelEvent`/`ReturnToTitleEvent`；`Failed` 态只显示重新开始。验收：无 UI 资源、无 EventSystem 也能正常显示与点击。

## 十一、一键搭建工具与场景接入（R1~R7 集成）

- [x] T17 `Editor/LevelSetup.cs`（菜单 `StarEco/Setup/Level01`）：建 `Assets/Settings/Levels/Level01.asset`（目标链 = 净化 3 污染源 → 净化 1 塔 → 净化度 100%；刷怪表 = Enemy01×2 条目，锚点解锁绑目标 0/2；星级阈值 100/60/30；出生点 (0,2)）；建 `Level01.unity` 灰盒：地面 `Ground` + 玩家（含 M2/M3/M5 组件）+ GarbagePile×3 + GarbageWall×1 + Tower01(TowerPurifiable) + 刷怪锚点×2 + Checkpoint×2 + AtmosphereController + PuritySystem + DropSpawner + LevelManager + LevelSpawner + LevelObjectiveTracker + LevelResultUI；Esc 触发保存。验收：Play 即可跑通全链路。

## 十二、编译与运行验证

- [x] T18 Unity 编译零报错：Refresh Unity 后 Console 无 error；`Level/` 全部 .cs + `Editor/LevelSetup.cs` + `PlayerCombat`/`PlayerController` 两处最小增量编译通过。验收：零 error。
- [x] T19 运行验证（需用户配合按 Play 目检）：目标链按序推进（先 3 垃圾→再塔→再 100%）；污染度下降时刷怪变慢、击杀后名额回收；死亡后从最近检查点恢复（位置/HP/净化剂正确、世界已净化目标不回退）；结算面板星级正确、重新开始可重跑、返回标题仅广播；存档文件写入 `persistentDataPath/level01_result.json` 与 `progress.json` 且可读。验收：逐条通过。
- [x] T20 `tasks.md` 状态行更新 + 需求对齐报告（AGENTS.md §1.3）。
- [x] T21 git commit：`M6: level01 — level config/objectives/checkpoints/spawner/result/save`。

> 状态：已完成（2026-10-18，T19 运行验证通过 Play 实测：目标链 0→1→2、净化度 100%、结算★★★、检查点死亡恢复 CP_1、重开场景干净、刷怪名额回收、两份 JSON 存档正确）。共 21 个任务（R1 配置 1、R2~R7 事件 1、统计与 DTO 3、存档服务 1、目标链 1、检查点 2、玩家最小增量 2、刷怪 1、状态机与编排 3、结算面板 1、一键工具 1、编译验证 1、运行验证 1、文档与提交 2），覆盖 R1~R8 全部约 45 条 EARS。
> 对 M1~M5 边界：除 `PlayerCombat.RestoreHP` 与 `PlayerController.Revive` 两处共约 10 行最小增量（T10/T11，设计 §2.3 已论证必要性）外，**对既有系统零源码改动**。
