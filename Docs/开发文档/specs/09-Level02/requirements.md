# 需求规范：09-Level02（第二关与全流程）

> 对应 TDD.md M9 里程碑 | 格式：EARS
> 范围：Enemy02 远程毒植物第二关实装、E 键解救互动（陷阱笼）、水流解谜（清堵塞→水位升→木筏通路）、Level02 目标链与灰盒场景、全流程打通（Title→L01→L02→L03）与"通关即解锁下一关"对齐
> 非目标：Boss 数值再平衡（M10）；第二关专属 BGM/音效铺量（M10，仅留 AudioManager 占位）；第二关美术正式资源替换（灰盒先行，美术管线后补）；Enemy03/Tower01 行为扩展（已属 M4/M8）
> 用户决策（2026-10-18 中断前选择，本轮按此执行）：① 解救方式 = 互动键 E 解放；② 水流解谜 = 清堵塞→水位升→通路；③ 解锁规则 = 通关即解锁下一关
> 共用约定：新代码落点 `Assets/Scripts/Level/`（命名空间 `StarEco.Level`），事件沿用 M1 `EventCenter` struct 零 GC；目标链复用 M6 `LevelObjectiveTracker`（只加枚举与事件订阅，不改推进算法）；场景搭建复用 M6/M8 Editor 工具模式（灰盒 WhiteSquare + 一键菜单）；UI 沿用 M7 程序化 uGUI 与 `UIUtility`

## R1 Enemy02 远程毒植物（第二关主力敌人）

> **美术动作**：`Enemy02_Idle/Move/Attack/Hurt/Die`（5 套帧已就位，M4 已生成 controller） | **代码落点**：既有 `Assets/Scripts/EnemyAI/Enemy02/Enemy02Controller.cs`（M4 已实现，本里程碑只验证不新写）

- THE SYSTEM SHALL 在第二关中实装 Enemy02：预制体 `Assets/Prefabs/Enemies/Enemy02.prefab`（已含 `Enemy02Controller` + `EnemyAnimatorDriver` + `Enemy02Config.asset` + 毒气弹引用）以刷怪表方式进入 Level02，保持远程攻击行为（保持距离 → `Enemy02_Attack` 动画事件帧发射毒气弹）。
- WHEN 玩家进入 `Enemy02Config.PerceptionRadius`（16u），THE SYSTEM SHALL 进入追击并保持 `AttackRange`（14u）内游走，Attack 冷却就绪时播放攻击动画，经动画事件 `ProjectileSpawn` 从 `_firePoint` 发射 `EnemyProjectile`（复用 M4 对象池）。
- THE SYSTEM SHALL Enemy02 的净化喷雾伤害走既有 `DamageInfo` 流水线（小怪无倍率，行为与 M4 一致）。
- IF 第二关配置了刷怪条目，THEN THE SYSTEM SHALL 由 M6 `LevelSpawner` 按锚点与 `MaxAlive` 上限生成，死亡后经 `EnemyDeathEvent` 计入 `DefeatEnemies` 目标。

## R2 E 键解救互动（陷阱笼）

> **美术动作**：玩家 `Interact`（M2 已生成）；被困动物/笼子为灰盒（美术后补） | **代码落点**：`Assets/Scripts/Level/RescueTarget.cs`、`Assets/Scripts/Level/Events/AnimalRescuedEvent.cs`、`LevelObjectiveTracker` 最小增量

- THE SYSTEM SHALL 提供 `RescueTarget` 组件：场景中放置陷阱笼（灰盒笼子 + 内部动物色块），玩家进入交互半径时显示"按 E 解救"气泡（复用 M7 `DialogueTrigger` 的世界空间气泡模式）。
- WHEN 玩家在交互半径内按下互动键（`KeyCode.E`，与 M7 对话一致），THE SYSTEM SHALL 播放玩家 `Interact` 动画（经 `PlayerAnimatorDriver.Interact` Trigger，若玩家存在），0.3s 后打开笼子（笼体消失/动物逃离动画）、广播 `AnimalRescuedEvent`（携带 `TargetId`），并使该 RescueTarget 失效（不可重复触发）。
- THE SYSTEM SHALL 新增目标类型 `LevelObjectiveType.RescueTargets`：`LevelObjectiveTracker` 订阅 `AnimalRescuedEvent` 累加计数，参与目标链推进（算法复用，仅加一个枚举分支与事件订阅）。
- THE SYSTEM SHALL 新增目标类型 `LevelObjectiveType.PuzzleSolved`：按河道堵塞点（`WaterBlockage`，TargetId 前缀 `WaterBlock`）被净化的数量计数，HUD 显示"清理堵塞物 x/3"进度；需求数按场景实际堵塞点夹取（防死锁）。`WaterPuzzleController` 收齐后广播 `PuzzleSolvedEvent` 供 FX/音效观察者消费（不参与计数，避免双源）。
- WHEN 解救全部被困动物，THE SYSTEM SHALL 经既有目标链机制推进到下一目标（HUD 文本自动刷新）。

## R3 水流解谜（清堵塞→水位升→木筏通路）

> **美术动作**：不涉及（水/木筏/堵塞点均为灰盒） | **代码落点**：`Assets/Scripts/Level/WaterPuzzleController.cs`、`Assets/Scripts/Level/Events/PuzzleSolvedEvent.cs`

- THE SYSTEM SHALL 提供 `WaterPuzzleController`：管理一组"堵塞点"（阻塞河道的污染障碍，挂 `IPurifiable`，即用喷雾净化清理，复用 M5 净化流水线）与一片水域（视觉水体 + 木筏通路）。
- WHEN 全部堵塞点被净化（`GarbagePurifiedEvent` 计数达到配置数），THE SYSTEM SHALL 触发解谜完成：水位从低位上升到高位（1.5s 平滑插值）、木筏通路碰撞体激活（`RaftPath` 组），并广播 `PuzzleSolvedEvent`。
- WHILE 水位未上升，THE SYSTEM SHALL 木筏通路不可通行（碰撞体禁用或平台未激活），玩家无法跨越河道到达右侧区域。
- WHEN 水位上升完成，THE SYSTEM SHALL 保持高位（本关卡内不回退），玩家可经木筏/通路平台跨越水域。
- THE SYSTEM SHALL 堵塞点未清完时，HUD 当前目标文本显示解谜进度（如"清理堵塞物 1/3"，由目标链 `PuzzleSolved` 计数天然提供）。
- IF 任一堵塞点漏配或场景中不存在，THEN THE SYSTEM SHALL 在启动时记警告日志，并按场景实际堵塞点数夹取 `PuzzleSolved` 目标需求（防死锁，复用 M6 `ClampRequired` 思路）。

## R4 Level02 目标链与配置

> **美术动作**：不涉及 | **代码落点**：`Assets/Settings/Levels/Level02.asset`（Editor 工具生成）

- THE SYSTEM SHALL `Level02.asset`：`LevelIndex=2`、关卡名"第二关：生态失衡的森林"、出生点 (-14, 2)、星级阈值 100/60/30、`IntroText` 说明本关任务（解救动物→疏通河道→净化森林）。
- THE SYSTEM SHALL 目标链为四段（顺序解锁）：① `RescueTargets` 2（解救 2 只被困动物）→ ② `PuzzleSolved` 1（清理 3 处堵塞、疏通河道）→ ③ `PurifyTargets` 3（净化 3 处森林污染源）→ ④ `DefeatEnemies` 4（击败 4 只变异毒植物）。
- THE SYSTEM SHALL 刷怪表：Enemy02 两条（初始 1 条 MaxAlive 2 + 目标②完成后 1 条 MaxAlive 3），复用 M6 `LevelSpawner`。
- WHEN 目标链全部完成，THE SYSTEM SHALL 沿用 M6 流程进入 `Completed` 并弹出 M7 结算面板（S/A/B/C + 星级），经 `LevelSaveService.CommitResult` 解锁第三关（R6）。

## R5 Level02 场景（生态森林）

> **美术动作**：灰盒（WhiteSquare 色块，美术后补） | **代码落点**：`Assets/Scenes/Level02.unity`、`Assets/Scripts/Editor/Level02Setup.cs`

- THE SYSTEM SHALL 提供一键搭建工具（菜单 `StarEco/Setup/Level02`）：生成 `Level02.asset` 与 `Level02.unity` 灰盒，并把场景加入 Build Settings（index 2：Title=0、Level01=1、Level02=2、Level03=3）。
- THE SYSTEM SHALL 场景布局（横向约 64u）：左侧森林区（出生点 + 2 陷阱笼 + 检查点）→ 中游河道（水域视觉 + 3 处堵塞点 + 木筏通路平台组，初始禁用）→ 右侧森林区（3 处污染源 +  Enemy02 刷怪锚点 ×2 + 检查点）。
- THE SYSTEM SHALL 场景挂载 M6/M7 全部系统：`EcoSystems`（PuritySystem/AtmosphereController/DropSpawner）、`LevelSystems`（LevelManager 挂非根节点/LevelObjectiveTracker/LevelSpawner/LevelStats）、根节点 `UIManager`、UI 面板（HUD/Pause/Result/Dialogue/Settings/Codex/LevelSelect）——缺 UIManager 会令面板静默失败（M8 教训）。
- WHEN 玩家跨越河道需要木筏通路，THE SYSTEM SHALL 该通路仅在 R3 解谜完成后可通行（空间上形成"不过桥够不着右侧"的动线）。
- THE SYSTEM SHALL 场景含 2 个检查点（出生区、右侧区），死亡回检查点（M6 既有逻辑）。

## R6 全流程打通与解锁规则

> **美术动作**：不涉及 | **代码落点**：`Assets/Scripts/UI/LevelSelectPanel.cs`、`Assets/Scripts/UI/ResultPanel.cs`、`Assets/Scripts/UI/MainMenuPanel.cs`（均为对齐既有 `LevelSaveService` 语义的最小修正）

- THE SYSTEM SHALL 解锁规则为"通关即解锁下一关"（用户确认）：`LevelSaveService.CommitResult` 已有该语义（`UnlockedLevelIndex = max(当前, 通关关序号+1)`），本里程碑不新增机制，只保证 UI 消费一致。
- THE SYSTEM SHALL `LevelSelectPanel` 第二关卡片 `BuildIndex` 由 -1 改为 2，简介文案更新为实际内容；锁定态继续按 `UnlockedLevelIndex` 渲染（未通关第一关时第二关不可点）。
- THE SYSTEM SHALL `ResultPanel`"下一关"按钮在 `UnlockedLevelIndex > 本关序号` 时显示并加载对应场景（第三关同理，BuildIndex 3）。
- THE SYSTEM SHALL 全流程可走通：Title（开始新游戏→Level01 / 继续游戏→按进度）→ Level01 通关 → 解锁 Level02 → Level02 通关 → 解锁 Level03 → Level03 通关；每关通关后回 Title 或下一关均有明确入口。
- WHEN 玩家在第一关通关前尝试从关卡选择进入第二关，THE SYSTEM SHALL 该卡片为锁定态不可交互（既有行为，回归验证）。

## R7 编译、验证与文档

- Unity 编译零错误（`Level/` 新增/改动 .cs + `Level02Setup.cs` + UI 三处最小修正 + `LevelObjectiveTracker` 增量）。
- 运行验证：Enemy02 远程攻击与净化伤害正常；E 解救 2 只动物后目标链推进；清 3 处堵塞后水位上升、木筏通路激活；四段目标链依次完成后结算面板弹出并写入进度；Title→L01→L02→L03 全流程可走；未通关时第二关锁定。
- 回归验证：第一关、第三关既有流程不受影响（目标链枚举扩展不改变旧关行为）。
- `tasks.md` 状态行更新 + 需求对齐报告（AGENTS.md §1.3）。
- git commit：`M9: level02 — enemy02/rescue/water-puzzle + full flow`。
