# 需求规范：06-Level01（第一关完整可玩）

> 对应 TDD.md M6 里程碑 | 格式：EARS
> 范围：`LevelConfig` 关卡数据配置、关卡状态机与流程编排（`LevelManager`）、目标链（可配置多目标顺序推进）、检查点（快照/恢复）、污染度驱动的动态刷怪、关卡结算（面板 + 星级 + 统计）、结算数据落盘与读档续关（复用 M1 `SaveSystem`）
> 非目标：HUD（血条/净化剂/进度条本体）与对话/提示牌 UI（M7）；第二、三关内容（M8）；Boss 阶段机（M8）；结算面板的美术美化（程序化 uGUI 占位，M7 换皮）；音频铺量（M10）
> 共用约定：代码落点 `Assets/Scripts/Level/`（命名空间 `StarEco.Level`）；关卡数据资产 `Assets/Settings/Levels/Level01.asset`；关卡场景 `Assets/Scenes/Level01.unity`；对 M1~M5 **零改动**，只调用/订阅其公开接口与事件；美术缺失一律 WhiteSquare / 运行时生成 uGUI 占位

## R1 关卡数据配置（LevelConfig）

> **代码落点**：`Assets/Scripts/Level/LevelConfig.cs`

- THE SYSTEM SHALL 以 ScriptableObject `LevelConfig` 描述一关的全部可配置内容：关卡序号、关卡名、目标链、刷怪表、出生点、结算星级阈值、限时（0 = 不限时）、开场提示语。
- WHEN `LevelManager` 初始化关卡且未指定配置资产，THE SYSTEM SHALL 回退到 `Level01Config` 资源或代码默认值，且不抛异常。
- THE SYSTEM SHALL 关卡目标以有序数组 `Objectives` 表达，每项目标含目标类型（`PurifyTargets` 净化 N 个污染源 / `PurifyTowers` 净化 N 座塔 / `ReachPurity` 净化值达标 / `DefeatEnemies` 击杀 N 个敌人）、所需数量、显示文本。
- THE SYSTEM SHALL 刷怪以 `SpawnEntry` 数组表达，每项含敌人预制体、生成锚点、同时存在上限、基础间隔、仅在指定目标序号解锁后生成。
- THE SYSTEM SHALL 结算星级以三个阈值表达（三星/二星/一星所需净化度百分比），未达一星阈值仍记通关（星级 0）。

## R2 关卡状态机与流程编排（LevelManager）

> **代码落点**：`Assets/Scripts/Level/LevelManager.cs`、`Assets/Scripts/Level/LevelState.cs`

- THE SYSTEM SHALL 关卡状态为四态枚举：`NotStarted`（未开始）、`Playing`（进行中）、`Completed`（已完成）、`Failed`（失败），且状态单向流转（`NotStarted → Playing → Completed/Failed`，终态不再回退，除显式 `RestartLevel`）。
- WHEN 关卡进入 `Playing`，THE SYSTEM SHALL 记录开始时间、初始化目标链与刷怪器、拉取玩家/净化系统/氛围系统引用并开始计时。
- WHEN 关卡进入 `Completed` 或 `Failed`，THE SYSTEM SHALL 停止计时、冻结玩家输入、广播对应的关卡终态事件，且只广播一次（幂等）。
- IF 关卡进入 `Playing` 后玩家死亡，THE SYSTEM SHALL **直接**转入 `Failed` 并进入结算流程（R7 结算面板提供「重新开始（重开本关）」与「返回主菜单」），不做原地复活。
- WHEN 调用 `RestartLevel`，THE SYSTEM SHALL 重置关卡状态与统计并重新进入 `Playing`（同场景内重置，不重载场景）。
- IF 关卡所需的玩家 / `PuritySystem` / `LevelConfig` 任一缺失，THE SYSTEM SHALL 输出明确错误日志并将关卡置为 `Failed`，不进入半工作状态。

## R3 目标链推进

> **代码落点**：`Assets/Scripts/Level/LevelObjectiveTracker.cs`、`Assets/Scripts/Level/Events/ObjectiveCompletedEvent.cs`

- WHEN 关卡开始，THE SYSTEM SHALL 只激活目标链中的**第一个**目标作为当前目标，其余目标处于未解锁状态。
- WHEN 当前目标的达成条件满足，THE SYSTEM SHALL 将其标记完成、广播"目标完成"事件、激活下一个目标；目标链全部完成即视为关卡目标达成。
- WHEN 任一目标完成，THE SYSTEM SHALL 更新并广播当前目标描述文本，供 M7 HUD 显示（`LevelObjectiveChangedEvent`）。
- IF `PurifyTargets` / `PurifyTowers` 类型的当前目标要求数量大于场景实际存在的可净化目标数，THE SYSTEM SHALL 在关卡初始化时输出警告并按实际数量夹取，避免死锁。
- THE SYSTEM SHALL 目标链完成不等同于关卡完成：关卡完成以 `LevelPurifiedEvent`（净化值 100%）或配置目标链全完成二者**先到者**为准，避免玩家达成全部目标却因配置总量大于 100% 而卡关。
- THE SYSTEM SHALL 目标链进度只增不减，重放（重启关卡）时完全重置。

## R4 检查点（已移除）

> 原「检查点快照 / 死亡回检查点」需求已按验收反馈整体移除：玩家阵亡即结束本局，由结算面板的「重新开始」重载本关。

- THE SYSTEM SHALL **不**提供检查点组件、关卡快照与死亡原地复活逻辑。
- WHEN 玩家死亡，THE SYSTEM SHALL 进入 `Failed` 终态并弹出结算面板（见 R2 / R7）。
- 已删除代码：`Level/Checkpoint.cs`、`Level/LevelSnapshot.cs`、`Level/Events/CheckpointActivatedEvent.cs`、`LevelManager.CommitCheckpoint/RespawnFromCheckpoint`、`LevelStats` 快照字段、`PlayerController.Revive`、`PlayerCombat.RestoreHP`。

## R5 污染度驱动的动态刷怪

> **代码落点**：`Assets/Scripts/Level/LevelSpawner.cs`

- WHEN 关卡进入 `Playing`，THE SYSTEM SHALL 按 `LevelConfig.SpawnEntries` 启动刷怪：每项在指定锚点附近、以同时存在上限为约束、按基础间隔生成敌人预制体。
- WHILE 关卡进行中，THE SYSTEM SHALL 依据当前污染度（`PurityChangedEvent.Pollution`）动态调整刷怪间隔：污染度越高间隔越短（`间隔 = 基础间隔 × Mathf.Lerp(1.5f, 0.5f, Pollution)`）。
- WHEN 某 `SpawnEntry` 绑定了 `RequiredObjectiveIndex`，THE SYSTEM SHALL 仅在该目标已完成后才开始生成（目标链驱动刷怪）。
- WHEN 生成的敌人死亡，THE SYSTEM SHALL 递增击杀计数并释放该 `SpawnEntry` 的同时存在名额。
- THE SYSTEM SHALL 刷怪产出的敌人走 M1 `PoolManager` 对象池并调用 `MarkPooled`（保证归还路径统一）。
- IF 锚点数量为 0 或预制体缺失，THE SYSTEM SHALL 输出错误并跳过该条目，不中断其余刷怪。

## R6 关卡结算数据与存档

> **代码落点**：`Assets/Scripts/Level/LevelResultData.cs`、`Assets/Scripts/Level/LevelSaveService.cs`

- WHEN 关卡进入终态（`Completed` / `Failed`），THE SYSTEM SHALL 生成一份 `LevelResultData`：关卡序号、是否通关、净化度百分比、耗时秒数、击杀数、拾取数、星级、达成时间戳。
- WHEN 关卡结算完成，THE SYSTEM SHALL 经 M1 `SaveSystem` 将 `LevelResultData` 写入槽位 `level01_result.json`（覆盖写），并写入一份 `ProgressData`（已解锁关卡序号）用于续关判断。
- WHEN 关卡开始，`LevelSaveService` SHALL 提供"是否存在可续关存档"的查询接口供 UI 决定是否显示"继续游戏"。
- WHEN 读取存档且数据缺失/损坏，THE SYSTEM SHALL 返回默认值（0 通关、0 星级）且不抛异常（沿用 `SaveSystem` 语义）。
- THE SYSTEM SHALL 结算星级按 `LevelConfig` 阈值从三星到一星逐级比对，净化度达标即至少一星；失败局面星级为 0。

## R7 结算面板（程序化 uGUI）

> **代码落点**：`Assets/Scripts/Level/LevelResultUI.cs`

- WHEN 关卡进入终态，THE SYSTEM SHALL 显示结算面板，展示：胜/负标题、关卡名、净化度百分比、耗时、击杀数、拾取数、星级（三星图标序列）。
- WHEN 结算面板显示，THE SYSTEM SHALL 冻结玩家输入（`InputLocked = true`）且面板自身接收按键（ESC 或回车关闭 / 重开）。
- WHEN 玩家在面板上选择"重新开始"，THE SYSTEM SHALL 调用 `LevelManager.RestartLevel()` 并隐藏面板。
- WHEN 玩家在面板上选择"返回标题"，THE SYSTEM SHALL 广播回标题请求（实际切场景由 M7 实现，本里程碑仅广播事件 + 日志）。
- THE SYSTEM SHALL 结算面板完全由代码构建 uGUI（Canvas/Image/Text，LegacyRuntime 内置字体），不依赖任何美术资源；星级用文本 `★★★/★★/★` 表示。
- IF 场景中无 `EventSystem`，THE SYSTEM SHALL 在构建面板时自动创建一个，避免按钮无响应。
- THE SYSTEM SHALL 面板在 `Failed` 状态下只显示"重新开始"，不显示"返回标题"以外的额外按钮（保持最小实现）。

## R8 关卡内数据统计

> **代码落点**：`Assets/Scripts/Level/LevelStats.cs`

- THE SYSTEM SHALL 统计本次关卡的击杀数、拾取数（经 `EnemyDeathEvent` / `PickupCollectedEvent` 计数）。
- THE SYSTEM SHALL 统计从 M5 事件流被动获得净化度百分比，不额外轮询场景对象。
- WHEN 关卡重启，THE SYSTEM SHALL 全部统计归零。
