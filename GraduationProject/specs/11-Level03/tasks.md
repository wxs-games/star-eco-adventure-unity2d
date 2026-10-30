# 任务清单：11-Level03

> 对应 `requirements.md` R1~R8 / `design.md`。共 18 个任务，按依赖顺序排列。
> 状态标记：`[x]` 已完成且验证通过 / `[ ]` 未完成
> 说明：本 spec 为**事后补写**，任务状态如实反映当前实现情况，不是计划书。

## 一、spec 三件套

- [x] T1 `specs/11-Level03/requirements.md`：EARS 覆盖 R1~R8。
- [x] T2 `specs/11-Level03/design.md`：四段结构、机关参数与理由、Boss 三阶段、落幕对白实现取舍、边界表。
- [x] T3 `specs/11-Level03/tasks.md`：本清单。

## 二、场景与配置（R1 / R5）

- [x] T4 `Assets/Settings/Levels/Level03.asset`：`LevelIndex=3`、关卡名"第三关：能源工厂的危机"、两条目标（清理污染源 ×4 / 击败 Boss01 ×1）、`TimeLimit=0`。
- [x] T5 `Assets/Scenes/Level03.unity`：四段式布局，X ∈ [−20.1, 64.0]，正交尺寸 6.5。
- [x] T6 出生点：场景中 `Player` 实例摆放位置（≈ −18.6, 1.5）。`LevelConfig.SpawnPoint` 已废弃。
- [x] T7 Build Settings 定序：Title=0 / Level01=1 / Level02=2 / Level03=3（`LevelIndex == buildIndex` 是不变式）。

## 三、机关群（R2）

- [x] T8 `Level/CrumblePlatform.cs`：`Idle → Shaking → Broken → Reforming` 四状态；站立判定用包围盒几何（脚底 ≥ 顶面 −0.3u + 水平重叠 + `IsGrounded`），不用 `ContactPoint2D.normal`。**Play 实测**：0.50 s 碎裂、碰撞体关闭、角色 y 6.36→1.13；3.00 s 复原。
- [x] T9 布置 3 个 `CrumblePlatform`：`PlatformL1`、`Jump_Crumble1`、`Jump_Crumble2`。
- [x] T10 `Level/MovingPlatform.cs`：`_minX=−10.2 / _maxX=−1.7 / _speed=2 / _pauseAtEnds=0.35`，每帧把位移加到玩家 `Body.position`（玩家无摩擦材质）。**Play 实测**：严格 2.00 u/s，两端精确停靠，带人偏移恒定 0.04u。
- [x] T11 布置 2 个 `MovingPlatform`：`PlatformC`、`Jump_Mover`。
- [x] T12 `Level/SpikeHazard.cs`：3 个实例（`SpikeField`/`Jump_Spikes`/`Puzzle_Spikes`），伤害走 `IHittable` 统一管线。
- [x] T13 `Level/ConveyorZone.cs` + `Level/VentHazard.cs`：各 1 个实例（仅 L3）。
- [x] T14 `Level/FallHazard.cs`：三关各 1 个。

## 四、拉杆与 Boss（R3 / R4）

- [x] T15 `Level/LeverSwitch.cs`：2 个实例（`LeverSwitch_03` 于 4.26 高、`Puzzle_Lever` 于 3.20 高）。
- [x] T16 `Boss/BossController.cs`：三阶段（60% / 30% 阈值经 `BossConfig`）、`BossEnrageState` 过场 0.8 s、`AttackA`（砸地）/`AttackB`（喷毒）/`Special`（Phase3）、死亡时在 2.2 单位范围撒 `fx_purify`。
- [x] T17 `Boss/BossGate.cs`：订阅 `GarbagePurifiedEvent` 控制开门；进场封锁；Boss 死亡时解锁并在爆心 0.9 单位范围播 `fx_burst`。
- [x] T18 `UI/BossHealthBar.cs`：显示血量与阶段。

## 五、落幕对白（R6）

- [x] T19 `Level/LevelManager.cs`：新增 `_endingDialogueId = "ava_ending"` + `_endingPlayed` 守卫；`FinishLevel` 拆为「决策段 + `CommitFinish`」；`TryPlayEndingDialogue()` 只在 `LevelIndex == 3` 且数据可用时命中，否则降级；`CommitFinishAfterEnding()` 协程等面板关闭后**再 yield 1 帧**才结算。**Play 实测**：L3 先 `Dialogue` 后 `Result`；L2 直接 `Result`。

## 六、叙事载体（R7）

- [x] T20 `NPC_Robot_FactoryIntro`（X = −12.0）挂 `DialogueTrigger` → `robot_factory_intro`（承接 L2→L3 因果）。
- [x] T21 `KnowledgeCard_Factory_Carbon`（X = −10.5）→ `kc_carbon_energy`。
- [x] T22 `KnowledgeCard_Factory_Renewable`（X = 50.0）→ `kc_renewable`。
- [x] T23 `NPC_Ava_LeverHint`（X = −20.08）+ `Puzzle_NPC`（X = 60.80）：既有对白触发点。⚠️ 前者命名与贴图（机器人）不符，待改名。

## 七、验证（R8）

- [x] T24 编译零错误。
- [x] T25 全流程实测：出生 → 地刺 → 移动平台 → 二楼 → 拉杆 → Boss 门 → 跳跃段 → 解谜 → Boss 三阶段 → 落幕对白 → 结算 → 写档。
- [x] T26 回归：第一、二关流程不受落幕对白改动影响（有 `LevelIndex == 3` 守卫）。
- [x] T27 文档同步：`LevelDesign.md` / `LevelArt.md` / 本 spec 坐标一致。

---

## 遗留（不属于本 spec 范围，已登记到对应文档）

| # | 项 | 归属文档 |
|---|---|---|
| 1 | 地面未换工业地板（仍用污染土壤） | `LevelArt.md` P0 |
| 2 | 三面墙用土墙贴图 | `LevelArt.md` P1 |
| 3 | 7 块装饰牌全不可读，`Jump_Sign`/`Puzzle_Sign` 名不副实 | `LevelArt.md` P1 |
| 4 | `NPC_Ava_LeverHint` 用机器人贴图 | `LevelArt.md` §5.6 |
| 5 | `DropSpawner` 挂载但配置为空（敌人死亡不掉落） | `GDD.md` 道具章 |
| 6 | 背景图 1024×576，不足 1080p | `ArtStyleGuide.md` P1 |
| 7 | 无中途检查点（设计如此） | `GDD.md` §3.1 |
