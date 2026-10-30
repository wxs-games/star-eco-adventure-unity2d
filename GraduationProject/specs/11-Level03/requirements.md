# 需求规范：11-Level03（第三关 · 能源工厂）

> 格式：EARS | 对应 `LevelDesign.md` §L3 / `LevelArt.md` §五
> 范围：第三关四段式场景、机关群（碎裂平台 / 移动平台 / 地刺 / 传送带 / 通风口）、双拉杆谜题、Boss 战与封锁门、目标链、落幕对白
> 非目标：Boss 数值再平衡（属 M10 已完成）；第三关专属美术正式资源替换（属 `LevelArt.md` 待补项）；环境音（属 `AudioDesign.md` P1）
> 状态：**全部已实现并通过实测**（本文档为补写，非新建需求）
> 共用约定：新代码落点 `Assets/Scripts/Level/`、`Assets/Scripts/Boss/`（命名空间 `StarEco.Level` / `StarEco.Boss`）；事件沿用 `EventCenter` struct 零 GC；目标链复用 `LevelObjectiveTracker`；场景生成用 Editor 工具模式

---

## R1 场景与四段式结构

> **场景**：`Assets/Scenes/Level03.unity`（Build index = 3） | **配置**：`Assets/Settings/Levels/Level03.asset`

- THE SYSTEM SHALL 提供 `Level03.unity`，横向约 **84 单位**（X ∈ [−20.1, 64.0]），是三个关卡中最长的一关。
- THE SYSTEM SHALL 场景按四段递进布置：
  - **① 入口段**（X −20 ~ −7）：老机器人开场、地刺、两块平台、移动平台 `PlatformC`
  - **② 二楼段**（X −7 ~ 25）：拉杆 `LeverSwitch_03`、右墙、三块平台、Boss 封锁门
  - **③ 跳跃段**（X 25 ~ 39）：两块碎裂平台、移动平台 `Jump_Mover`、地刺
  - **④ 解谜 + Boss 段**（X 39 ~ 64）：拉杆 `Puzzle_Lever`、地刺、Boss 场地
- THE SYSTEM SHALL 场景挂载全部系统组件：`LevelManager`、`LevelObjectiveTracker`、`LevelSpawner`、`LevelStats`、`PuritySystem`、`AtmosphereController`、`DropSpawner`、`UIManager`、7 个 UI 面板。
- THE SYSTEM SHALL 出生点为场景中 `Player` 预制体实例的摆放位置（约 X = −18.6, Y = 1.5）。**`LevelConfig.SpawnPoint` 已废弃**，运行时不再按配置瞬移。
- THE SYSTEM SHALL 相机正交尺寸为 **6.5**，关卡内不变化。

## R2 机关群

> **代码落点**：`Assets/Scripts/Level/CrumblePlatform.cs`、`MovingPlatform.cs`、`SpikeHazard.cs`、`ConveyorZone.cs`、`VentHazard.cs`、`FallHazard.cs`

- THE SYSTEM SHALL 在第三关布置 **3 个** `CrumblePlatform`（`PlatformL1`、`Jump_Crumble1`、`Jump_Crumble2`）。
- WHEN 玩家站上碎裂平台，THE SYSTEM SHALL 在 **0.5 s** 后进入抖动（振幅 0.045、频率 26），随后碎裂并关闭碰撞体；计时一旦开始**不因玩家离开而取消**。
- THE SYSTEM SHALL 碎裂后 **3 s** 复原（淡入 0.18 s）。
- THE SYSTEM SHALL 布置 **2 个** `MovingPlatform`（`PlatformC`、`Jump_Mover`）。
- WHEN 玩家站上移动平台，THE SYSTEM SHALL 每帧把平台位移直接加到玩家 `Body.position`（玩家为无摩擦力材质，靠 `velocity` 无法被带动）。
- THE SYSTEM SHALL 布置 **3 个** `SpikeHazard`（`SpikeField`、`Jump_Spikes`、`Puzzle_Spikes`），踩到造成伤害，走 `IHittable` 与敌人伤害同一管线。
- THE SYSTEM SHALL 布置 1 个 `ConveyorZone`（传送带，持续施加水平速度）与 1 个 `VentHazard`（通风口，周期性向上吹力/伤害）。
- THE SYSTEM SHALL 三关均布置 `FallHazard`（坠落危险区），玩家掉出关卡下界即判定受伤/失败。

## R3 双拉杆与二楼段

> **代码落点**：`Assets/Scripts/Level/LeverSwitch.cs`

- THE SYSTEM SHALL 布置 **2 个** `LeverSwitch`（`LeverSwitch_03` 于 X = −1.21, Y = 4.26；`Puzzle_Lever` 于 X = 47.0, Y = 3.20）。
- WHEN 玩家进入拉杆交互半径并按 E，THE SYSTEM SHALL 切换拉杆状态（`lever_switch_off` ↔ 开启态）并触发绑定的机关（开门 / 关闭地刺）。
- THE SYSTEM SHALL 拉杆位于高处，需先经平台攀爬到达；老机器人在入口段提示"拉杆在高处，我上不去"。

## R4 Boss 战与封锁门

> **代码落点**：`Assets/Scripts/Boss/BossController.cs`、`BossGate.cs`、`Assets/Scripts/Boss/BossConfig.asset`

- THE SYSTEM SHALL 在第四段布置 `Boss01`（`BossController`），体型为玩家的 **2.25 倍**（4.5 世界单位高）。
- THE SYSTEM SHALL Boss 血量 **80 点**（= 40 颗心），分三阶段：
  - **Phase1**（100% ~ 60%）：基础攻击
  - **Phase2**（60% ~ 30%）：`Phase2IntervalScale` 缩短攻击间隔，播放 `Enrage` 动画
  - **Phase3**（30% ~ 0%）：`Phase3IntervalScale` 进一步缩短，可用 `Special` 特殊招
- WHEN 血量跨越阶段阈值，THE SYSTEM SHALL 进入 `BossEnrageState`，持续 `PhaseTransitionSeconds`（默认 0.8 s），期间不发起新攻击。
- THE SYSTEM SHALL Boss 拥有两套攻击：`AttackA`（砸地）、`AttackB`（喷毒），均由动画事件 `HitOn`/`HitOff` 驱动判定。
- THE SYSTEM SHALL `BossGate`（封锁门）在玩家进入 Boss 场地后关闭，封锁退路。
- WHEN Boss 被击败，THE SYSTEM SHALL 解锁封锁门、广播 `BossDefeatedEvent`、在尸体附近生成 `fx_purify` 特效（随机 2.2 单位范围）。
- THE SYSTEM SHALL 提供 `BossHealthBar` UI，显示当前阶段与血量。

## R5 目标链

> **配置**：`Assets/Settings/Levels/Level03.asset`

- THE SYSTEM SHALL 目标链为两段：
  1. `PurifyTargets` Required = **4**（"清理工厂污染源（含封锁墙）"）
  2. `DefeatEnemies` Required = **1**（"击败工厂污染核心 Boss01"）
- WHEN 目标①完成，THE SYSTEM SHALL 推进到目标②（Boss 战）。
- WHEN 目标②完成，THE SYSTEM SHALL 进入完成流程（见 R6）。
- THE SYSTEM SHALL 第三关**无时间限制**（`TimeLimit: 0`）。

## R6 落幕对白与结算

> **代码落点**：`Assets/Scripts/Level/LevelManager.cs`（`TryPlayEndingDialogue` / `CommitFinishAfterEnding`）

- WHEN 关卡完成且 `LevelIndex == 3`，THE SYSTEM SHALL **先**播放落幕对白 `ava_ending`（3 条），**再**弹出结算面板。
- THE SYSTEM SHALL 落幕对白仅在第三关触发（第 1、2 关直接结算）。
- IF 对话面板不可用或对白数据缺失，THEN THE SYSTEM SHALL 降级为直接结算，**不得阻塞通关**。
- WHEN 对白播放完毕，THE SYSTEM SHALL 等待 1 帧后再弹结算（同一帧内弹面板会被玩家关闭对白的同一个 E 键立刻按掉）。
- THE SYSTEM SHALL 结算评级按净化率：**100% → S，≥60% → A，≥30% → B，否则 C**。
- THE SYSTEM SHALL 结算结果经 `LevelSaveService.CommitResult` 写档。

## R7 叙事载体

- THE SYSTEM SHALL 第三关布置 **3 个** `DialogueTrigger`：
  - `NPC_Robot_FactoryIntro`（X = −12.0）：承接第二关的因果开场
  - `NPC_Ava_LeverHint`（X = −20.08）：拉杆提示
  - `Puzzle_NPC`（X = 60.80）：Boss 门前
- THE SYSTEM SHALL 布置 **2 个** `KnowledgeCardPickup`：`kc_carbon_energy`（X = −10.5）、`kc_renewable`（X = 50.0）。
- THE SYSTEM SHALL 第三关**不布置可读提示牌**（`SignboardTrigger` 数为 0）；场景中有 7 块装饰牌，**均无交互**。此为本关的已知叙事缺口（见 `NarrativeDesign.md`）。

## R8 编译与验证

- Unity 编译零错误。
- 运行验证：出生→过地刺→踩移动平台→上二楼→扳拉杆→进 Boss 门→跳跃段（碎裂/移动平台）→解谜段→Boss 战三阶段→落幕对白→结算→写档。
- 回归验证：第一、二关流程不受影响（`LevelManager` 的落幕对白有 `LevelIndex == 3` 守卫）。
- 文档同步：`LevelDesign.md` / `LevelArt.md` / `Level03.asset` 三者坐标一致。

---

## 需求 ↔ 实现对照

| 需求 | 实现位置 | 状态 |
|---|---|---|
| R1 四段式场景 | `Level03.unity`（37 个 SpriteRenderer / 27 个 MonoBehaviour） | ✅ |
| R2 机关群 | Crumble×3 / Moving×2 / Spike×3 / Conveyor×1 / Vent×1 / Fall×1（实测组件计数） | ✅ |
| R3 双拉杆 | `LeverSwitch_03`、`Puzzle_Lever` | ✅ |
| R4 Boss 战 | `BossController` + `BossGate` + `BossHealthBar` | ✅ |
| R5 目标链 | `Level03.asset` 2 条目标 | ✅ |
| R6 落幕对白 | `LevelManager.TryPlayEndingDialogue()` | ✅（Play 实测：先 Dialogue 后 Result） |
| R7 叙事载体 | 3 对话 + 2 知识卡 + 0 提示牌 | ⚠️ 提示牌缺失（已知缺口） |
| R8 编译验证 | 0 error | ✅ |

## 已知缺口（不在本 spec 范围内，登记备查）

| # | 缺口 | 归属 |
|---|---|---|
| 1 | 地面仍用 `ground_polluted_soil`，未换工业地板 | `LevelArt.md` P0 |
| 2 | 三面墙用 `wall_soil_block`（土墙） | `LevelArt.md` P1 |
| 3 | 7 块装饰牌全部不可读，`Jump_Sign`/`Puzzle_Sign` 命名暗示提示牌 | `LevelArt.md` P1 / `NarrativeDesign.md` |
| 4 | `NPC_Ava_LeverHint` 使用机器人贴图，命名与素材矛盾 | `LevelArt.md` §5.6 |
| 5 | `DropSpawner` 已挂载但 `_dropTable` / `_pickupPrefab` 全为空，敌人死亡不掉落 | `GDD.md` 道具章 |
