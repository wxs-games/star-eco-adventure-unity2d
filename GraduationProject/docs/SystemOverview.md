# 系统总览（System Overview）

> 项目：基于 Unity2D 的"星际环保"动作冒险游戏
> 版本：v1.0 | 最后更新：2026-10-30
> 定位：**GDD 与各系统 SDD 之间的中间层**。GDD 说"要做什么"，本文说"系统怎么切、谁依赖谁、谁在什么时刻被唤醒"，SDD 说"某个系统内部怎么实现"。
> 读者：新接手的人（含答辩老师）先读本文，能在 20 分钟内建立全局心智模型，再按需下钻到 11 个 `specs/` 包。
> 数据口径：**全部为工程实测**（脚本统计 / 事件 grep / 场景 YAML 解析），非规划描述。凡标 ⚠️ 的是与文档或直觉不符的实况。

---

## 一、本文在文档体系中的位置

```
GDD.md ──────────────── 设计愿景（做什么、为什么）
  │
  ├─ SystemOverview.md ◄── 本文：系统切分与关系（怎么分、谁连谁）
  │        │
  │        ├─ specs/01~11 /*/design.md ── 逐系统 SDD（内部怎么实现）
  │        │
  │        └─ CodeStandard.md ─────────── 写代码时的强制规范
  │
  ├─ LevelDesign.md / NarrativeDesign.md / AudioDesign.md ── 内容层规格
  ├─ ArtStyleGuide.md / LevelArt.md / AnimationVFXSpec.md ── 表现层规格
  ├─ TDD.md ───────────── 技术选型与工程结构（偏"为什么选")
  └─ TestPlan.md ──────── 验证层
```

**与 TDD 的分工**：`TDD.md` 回答"为什么用 Unity / 为什么用 JsonUtility"；本文回答"最终切成了哪几个系统、边界在哪、事件怎么流"。TDD §2.1 的目录结构与 §3.1 的系统关系图**停留在 M1 规划态**（写着"当前仅 SampleScene.unity"），已过时，以本文为准。

---

## 二、分层架构（实测）

代码规模（`Assets/Scripts/`，151 个 `.cs`，25,855 行）：

| 目录 | 文件数 | 行数 | 层 | 职责一句话 |
|---|---:|---:|---|---|
| `Core/` | 3 | 381 | **L0 基础** | 无依赖纯计算：CSV 解析、伤害公式、数值安全 |
| `Framework/` | 8 | 908 | **L1 框架** | 单例 / 事件中心 / 对象池 / 存档 / 音频 |
| `Player/` | 7 | 2,446 | **L2 角色** | 控制器、状态机、动画驱动 |
| `Combat/` | 11 | 1,272 | **L2 战斗** | 伤害、判定框、连段、受击、投掷物 |
| `EnemyAI/` | 8 | 1,512 | **L2 AI** | 敌人 FSM、三种敌人、炮塔 |
| `Boss/` | 7 | 1,826 | **L2 AI** | 三阶段 Boss、封锁门、血条 |
| `Eco/` | 21 | 2,077 | **L2 玩法** | 净化值、污染度、垃圾堆、喷雾、知识卡、掉落 |
| `Level/` | 20 | 3,435 | **L3 关卡** | 目标链、关卡状态机、生成、危害物、机关、结算 |
| `UI/` | 17 | 4,121 | **L4 表现** | 面板栈、HUD、菜单、结算、图鉴、对话 |
| `Editor/` | 49 | 7,877 | **L5 工具** | 场景生成、动画搭建、批量修复（不进运行时） |
| | **151** | **25,855** | | 其中运行时 19,978 行 / 编辑器 7,877 行 |

### 依赖方向（单向，无环）

```
L5 Editor ──► L4 UI ──► L3 Level ──► L2 玩法域 ──► L1 Framework ──► L0 Core
                 │           │            │
                 └───────────┴────────────┴──► L1（事件中心 / 存档 / 音频）
```

**关键约束：L2 之间不互相 `using`**。`EnemyAI` 不知道 `Eco` 存在，`Eco` 不知道 `Boss` 存在——它们只共享 `Combat.IHittable` 接口和 `EventCenter` 事件。这条约束是"加第四个敌人不用改净化系统"的前提。

**L3 是唯一允许"知道所有人"的层**：`LevelManager` 直接持有 `PlayerController`、`LevelObjectiveTracker`、`LevelSpawner`，因为它是关卡的编排者。

---

## 三、运行时对象拓扑

### 3.1 四类"全局对象"

| 类型 | 实例 | 出现方式 | 场景内数量 |
|---|---|---|---|
| **懒加载单例**（`MonoSingleton<T>`） | `AudioManager` | 首次访问 `Instance` 时自建 GameObject | **0**（永不进场景） |
| | `PoolManager` | 同上 | **0** |
| **场景单例** | `UIManager` | 手工挂在场景根 | Title/L01/L02/L03 各 **1** |
| | `LevelManager` | 同上 | L01/L02/L03 各 **1**，Title **0** |
| **自举对象**（`RuntimeInitializeOnLoadMethod`） | `FrameworkSelfTest` | 进游戏自动跑一次自检 | 不挂场景 |
| | `DamageNumberLayer` | 自动创建飘字 Canvas | 不挂场景 |
| | `EnemyHealthBarLayer` | 自动创建敌人血条层 | 不挂场景 |

> ⚠️ **`AudioManager` / `PoolManager` 在四个场景里一个实例都没有**——它们靠懒加载在第一次 `PlaySfx` / 取池时自建。这意味着：如果某个场景从头到尾没播过音效，`AudioManager` 到退出都不会被创建（当前无影响，但排查"没声音"时要先确认这一点）。

> ⚠️ **`LevelManager` 只存在于三个关卡场景**，Title 场景没有。所以 Title 里任何 `LevelManager.Instance` 的调用都会拿到 null 或触发懒加载出一个空壳——写 UI 代码时必须先判空。

### 3.2 场景内对象的分工

以 Level03 为例（最复杂的一关）：

```
[场景根]
├─ UIManager                    面板栈与输入阻塞的唯一仲裁者
├─ LevelManager                 关卡编排：状态机 + 目标链 + 结算
├─ Player                       玩家（含 ToolAnchor 子物体挂钳子）
├─ Main Camera                  CameraFollow2D
├─ LevelSpawner                 敌人/道具生成
├─ LevelStats                   本局统计（击杀/拾取/耗时）
├─ 地形与机关（无脚本或单脚本）
│   ├─ Ground_* / Platform*      静态碰撞
│   ├─ PlatformL1 → CrumblePlatform（碎裂）
│   ├─ PlatformC  → MovingPlatform（左右移动）
│   ├─ Spike_*    → SpikeHazard
│   ├─ Conveyor   → ConveyorZone
│   ├─ Vent       → VentHazard
│   └─ FallZone   → FallHazard
├─ Boss 区
│   ├─ Boss01     → BossController（三阶段）
│   ├─ BossGate   → BossGate（封锁/解锁）
│   └─ BossHealthBar
└─ 叙事载体
    ├─ NPC_* → DialogueTrigger
    ├─ Signboard_* → SignboardTrigger
    └─ KnowledgeCard_* → KnowledgeCardPickup
```

---

## 四、系统清单（12 个）

| # | 系统 | 入口类 | 依赖 | 被谁依赖 | 状态 |
|---|---|---|---|---|---|
| 1 | 基础计算 | `DamageMath` / `NumericSafety` / `CsvTable` | 无 | 全项目 | ✅ |
| 2 | 框架 | `EventCenter` / `MonoSingleton` / `PoolManager` / `SaveSystem` | Core | 全项目 | ✅ |
| 3 | 音频 | `AudioManager` / `GameAudioLibrary` | Framework | Player/Combat/Eco/Level/UI | ✅ 22 SFX + 4 BGM |
| 4 | 玩家控制 | `PlayerController` / `PlayerStateMachine` | Framework | Level/UI | ✅ |
| 5 | 战斗 | `PlayerCombat` / `AttackHitbox` / `PlayerAttackState` / `ToolProjectile` | Framework + Player | EnemyAI/Boss/Eco | ✅ 三段 + 空中 + 投掷 |
| 6 | 敌人 AI | `EnemyBase` / `EnemyConfig` | Framework + Combat | Level | ✅ 三型 + 炮塔 |
| 7 | Boss | `BossController` / `BossGate` | Framework + Combat | Level | ✅ 三阶段 |
| 8 | 环保玩法 | `PuritySystem` / `SprayController` / `GarbagePile` / `CodexService` | Framework + Combat | Level/UI | ✅（道具掉落空转） |
| 9 | 关卡编排 | `LevelManager` / `LevelObjectiveTracker` / `LevelSpawner` | 全部 | UI | ✅ |
| 10 | 关卡机关 | `CrumblePlatform` / `MovingPlatform` / `LeverSwitch` / `WaterPuzzleController` / 四种 Hazard | Framework | Level | ✅ |
| 11 | UI | `UIManager` / `HUDController` / `DialoguePanel` / `CodexPanel` | Framework | Level | ✅（8 面板） |
| 12 | 编辑器工具 | 49 个脚本 | Editor API | 无（开发期） | ✅ |

---

## 五、系统依赖矩阵

行 = 谁发起调用，列 = 被调用。`E` 表示只通过 `EventCenter` 通信（无直接引用）。

| ↓调用者 / 被调→ | Core | Fw | Audio | Player | Combat | Enemy | Boss | Eco | Level | UI | Editor |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| **Core** | — | | | | | | | | | | |
| **Framework** | ● | — | | | | | | | | | |
| **Player** | ● | ● | ● | — | ● | | | E | | | |
| **Combat** | ● | ● | ● | ● | — | | | | | | |
| **EnemyAI** | ● | ● | ● | ● | ● | — | | | | | |
| **Boss** | ● | ● | ● | ● | ● | | — | | | | |
| **Eco** | ● | ● | ● | ● | ● | E | | — | | | |
| **Level** | ● | ● | ● | ● | ● | ● | ● | ● | — | ● | |
| **UI** | ● | ● | ● | ● | | | | ● | ● | — | |
| **Editor** | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | — |

三条**禁止**（目前均遵守）：
1. `Eco` 不得 `using StarEco.EnemyAI`（敌人死亡由事件回报，不反向调用）
2. `UI` 不得直接改玩法状态（只能读 + 发事件；唯一例外是 `PausePanel` 调 `LevelManager.RestartLevel`）
3. `L0~L2` 不得 `using StarEco.UI`（表现层不能被玩法层反向依赖）

---

## 六、事件总线拓扑（20 个事件实测）

`EventCenter` 是静态类，`Dictionary<Type, List<Delegate>>`，同步分发、无队列、无优先级。**发布在调用方线程当场执行完所有订阅者**——这是"事件处理器里不能再改正在遍历的集合"的原因（已用倒序遍历规避）。

| 事件 | 发布 | 订阅 | 发布者 → 订阅者 | 诊断 |
|---|---:|:-:|---|---|
| `GarbagePurifiedEvent` | 3 | 5 | 垃圾堆/堵塞物/净化塔 → 目标链、HUD、净化值、氛围 | ✅ 主干 |
| `EnemyDeathEvent` | 1 | 4 | 敌人 → 目标链、统计、生成器、掉落 | ✅ 主干 |
| `PurityChangedEvent` | 1 | 2 | 净化值 → 氛围、目标链 | ✅ 主干（HUD 净化值面板 2026-10-30 移除后 −1） |
| `EnemyDamagedEvent` | 2 | 2 | 敌人 → 血条、飘字 | ✅ |
| `LevelCompletedEvent` | 1 | 2 | 关卡 → `ResultPanel`、`LevelResultUI`（后者未挂载，见 §10 债 2） | ⚠️ 双订阅者 |
| `LevelFailedEvent` | 1 | 2 | 关卡 → `ResultPanel`、`LevelResultUI`（后者未挂载） | ⚠️ 双订阅者 |
| `ObjectiveCompletedEvent` | 1 | 1 | 目标链 → HUD（**只剩一条 Debug.Log**，目标栏已移除） | ⚠️ 事实空订阅 |
| `AnimalRescuedEvent` | 1 | 1 | 解救点 → 目标链 | ✅ |
| `TowerDisableEvent` | 1 | 1 | 净化塔 → 目标链 | ✅ |
| `LevelPurifiedEvent` | 1 | 1 | 净化值 → 关卡 | ✅ |
| `PickupCollectedEvent` | 1 | 1 | 拾取 → 图鉴 | ✅ |
| `BossDefeatedEvent` | 1 | **0** | Boss → （无） | ⚠️ 孤儿（血条走 `HpPercent` 轮询 + `BossPhaseChangedEvent`） |
| `BossPhaseChangedEvent` | 1 | 1 | Boss → 血条 | ✅ |
| `RestartLevelEvent` | 3 | 1 | 结算/暂停/UI → 关卡 | ✅ |
| `PurifyTankChangedEvent` | 1 | **0** | 净化罐 → （无） | ⚠️ 孤儿 |
| `LevelObjectiveChangedEvent` | 1 | **0** | 目标链 → （无） | ⚠️ 孤儿 |
| `LevelStateChangedEvent` | 1 | **0** | 关卡 → （无） | ⚠️ 孤儿 |
| `DialogueEventIdEvent` | 1 | **0** | 对话 → （无） | ⚠️ 孤儿 |
| `PuzzleSolvedEvent` | 1 | **0** | 水解谜 → （无） | ⚠️ 孤儿 |
| `ReturnToTitleEvent` | **0** | **0** | — | ⚠️ 完全未用 |

**6 个孤儿事件的成因而非"漏接线"**：
- `PuzzleSolvedEvent`：`Level02` 的水解谜计数**不走事件**，而是 `LevelObjectiveTracker` 数 `WaterBlock` 前缀的 `GarbagePurifiedEvent`（`LevelObjectiveTracker.cs:140` 注释明写）。这个事件是给未来"解谜演出"预留的。
- `LevelObjectiveChangedEvent` / `LevelStateChangedEvent` / `PurifyTankChangedEvent` / `DialogueEventIdEvent`：HUD 与各面板改为**直接轮询/持有引用**（`HUDController` 每帧读 `LevelObjectiveTracker.Current`），事件版本发布后无人订阅。
- `BossDefeatedEvent`：第三关通关实际走 `EnemyDeathEvent` → 目标链（Boss 也是 `EnemyControllerBase`，死亡会广播通用死亡事件）；血条走 `HpPercent` 轮询。它纯粹是"将来做 Boss 战计时/成就"的预留。
- `ReturnToTitleEvent`：定义了但返回主菜单是直接 `SceneManager.LoadScene`，没用事件。

> **架构债**：这 6 个事件是"设计时想用事件、实现时图快直接引用"的残留。它们**不影响功能**，但会让读代码的人误以为有订阅方。已在 §10 列为 P2 清理项。

---

## 七、启动与生命周期时序

### 7.1 全局启动（进游戏一次）

```
Unity 引擎初始化
  └─ RuntimeInitializeOnLoadMethod(AfterSceneLoad)
       ├─ FrameworkSelfTest    → 跑一次自检（事件/池/存档/音频），结果打 Console
       ├─ DamageNumberLayer    → 建 Canvas（order 100）
       └─ EnemyHealthBarLayer  → 建 Canvas（order 90）
  └─ 加载 BuildSettings[0] = Title.unity
```

**场景顺序是不变式**：`Title(0) → Level01(1) → Level02(2) → Level03(3)`。`LevelIndex == buildIndex` 是全项目依赖的约定——`LevelManager.RestartLevel()` 用 `LoadScene(scene.buildIndex)`，`ResultPanel` 用 `LevelIndex+1` 算下一关。**调换 Build Settings 顺序会静默打乱所有关卡跳转。**

### 7.2 进入关卡（每次 LoadScene）

```
Awake 阶段（Unity 不保证同层顺序，故代码里全部做了 null 兜底）
  UIManager.Awake      建面板字典，不打开任何面板
  LevelManager.Awake   找 Player、找 Tracker、找 Spawner；GetComponent 补挂 PlayerToolThrow
  PlayerController.Awake 缓存 Rigidbody2D/Animator/Combat；建状态机
  → UIManager 的 Canvas 在 Awake 后注册进 EventSystem

OnEnable / Start 阶段（顺序由脚本执行顺序决定，故用事件 + 轮询双保险）
  LevelObjectiveTracker.Start   读 LevelConfig 构建目标链 → 发首个 LevelObjectiveChangedEvent
  HUDController.Start           订阅事件 + 主动读一次 Tracker（防止首发事件早于订阅）
  LevelSpawner.Start            按配置布敌
  DialogueTrigger.Start         若 autoTalk 且玩家在半径内 → 弹对话
```

> ⚠️ **"首帧事件早于订阅"是这个项目的通用陷阱**。凡是"发布方在 `Start`、订阅方也在 `Start`"的组合都会漏掉第一发。项目里的统一解法是：**订阅后主动读一次当前值**（`HUDController.cs:73` 附近）。新增系统必须照抄这个模式。

### 7.3 一局的完整状态流

```
LevelState:
  None ──► Playing ──┬──► Completed  （目标链全清 + 关卡条件满足）
                     │      └─ LevelIndex==3 → 先播 ava_ending 对白 → 再结算
                     │      └─ 其余 → 直接结算
                     └──► Failed     （玩家 HP 归零 / 掉落致死）
                            └─ 结算面板："重开本关" / "返回主菜单"
```

`FinishLevel()` 是唯一终态入口，**幂等**（`_finished` 守卫）。进入终态做四件事：停表 → 冻结输入（`Player.InputLocked`）→ 生成结算数据并写档 → 广播事件。

> ⚠️ **项目没有中途检查点**。`PlayerDeathState` 只负责播死亡动画，重生靠 `LevelManager` 判定失败 → `LoadScene(buildIndex)` 整关重载。GDD/TDD 早期的"检查点"描述已全部标为未实现。

---

## 八、五条主干数据流

### 8.1 净化流（环保系统的核心闭环）

```
玩家按 K 喷雾
 → SprayController 每帧发射 SprayHitbox
 → 命中 IPurifiable（垃圾堆/堵塞物/净化塔）
 → 目标.TakePurify(amount)
      ├─ PuritySystem.AddPurity(amount)      累加净化值
      ├─ 破坏累积 ≥ 阈值 → 播碎裂动画 → Publish(GarbagePurifiedEvent)
      └─ 每帧 Publish(PurityChangedEvent)
 → 三方响应（全部解耦）：
      ├─ AtmosphereController  背景色 CleanColor↔PollutedColor 插值
      ├─ LevelObjectiveTracker 目标进度推进
      └─ LevelSpawner          按净化阶段刷新敌人
```

**设计要点**：净化值不是全局单值，而是 `PuritySystem` 统计场景内所有 `IPurifiable` 的剩余量。所以"净化 100%"等价于"场景里没有未净化的东西"——不需要单独维护目标值。

### 8.2 伤害流（战斗系统的统一入口）

```
任何伤害源
 → DamageInfo { Amount, KnockbackDir, Source }
 → IHittable.TakeDamage(info)
      ├─ 玩家 → PlayerCombat：判无敌帧 → 扣 HP → Hurt/Death 状态
      └─ 敌人 → EnemyBase：扣 HP → 闪白 → Publish(EnemyDamagedEvent) → HP≤0 → Death
 → 死亡 → Publish(EnemyDeathEvent) → 目标链/统计/掉落四方响应
```

**接口只有一个**：`IHittable.TakeDamage(DamageInfo)`。玩家、三种敌人、Boss、可破坏道具全部实现它，所以"给某类目标加伤害"永远只改一处。

### 8.3 目标链流（关卡节奏的驱动者）

`LevelObjectiveTracker` 持有一个**有序目标数组**（来自 `LevelConfig`），只有当前目标会被评估：

```
Publish(GarbagePurified / EnemyDeath / AnimalRescued / TowerDisable / PuzzleSolved)
 → Tracker 收到 → 若事件类型匹配当前目标的 Type → 计数 +1
 → 计数 ≥ Required → Publish(ObjectiveCompletedEvent) → Index++ → 推进下一个
 → 全部完成 → 关卡通
```

> ⚠️ **必须是 `while` 而不是 `if`**。多个事件在同一帧到达（一次爆炸杀两只怪）时，`if` 只会推进一格，导致目标链卡死。TDD v0.3 记录过这个 bug 的修复。

### 8.4 结算流

```
FinishLevel(success)
 → BuildResult()：耗时 / 净化率 / 击杀 / 拾取 / 星级（S≥100% / A≥60% / B≥30% / C）
 → LevelSaveService.CommitResult() 写 `level01_result` 槽
 → Publish(LevelCompletedEvent | LevelFailedEvent)
 → ResultPanel 打开（UIManager 栈顶，阻塞输入）
```

### 8.5 存档流（4 个槽，全部 `Application.persistentDataPath/*.json`）

| 槽 | 内容 | 写入方 | 读取方 | 时机 |
|---|---|---|---|---|
| `settings` | BGM/SFX 音量 | `SettingsPanel` | `AudioManager` | 改滑块 |
| `progress` | 通关进度、星级 | `LevelSaveService` | `LevelSelectPanel` | 通关 |
| `codex` | 已解锁知识卡 | `CodexService` | `CodexPanel` | 拾取知识卡 |
| `level01_result` | 上局结算数据 | `LevelSaveService` | `ResultPanel` | 通关/失败 |

> ⚠️ **没有"关卡内进度"存档**。净化值、已收集品、敌人状态全部是运行时内存态，退出即失去。这是刻意取舍（毕设体量下"整关重开"比"断点续关"更好测），但答辩若被问"能不能中途退出再进来"，答案是"不能，只能从头"。

---

## 九、跨系统的 10 条硬约束

这些是踩过坑之后固化的规则，违反任何一条都会产生**静默错误**（不报错、但表现异常）：

| # | 约束 | 违反后果 | 出处 |
|---|---|---|---|
| 1 | 角色 `sortingOrder` 进场景后是 **15** 而非预制体的 0 | 工具/特效被角色整块盖住（"钳子消失"） | 记忆 + `FixAllLevelSpriteSorting` |
| 2 | 武器排序必须运行时跟随 `_body.sortingOrder+1`，不能写死 | 同上 | `PlayerToolAnchor` |
| 3 | 攻击期间隐藏工具只能改 `SpriteRenderer.enabled`，不能 `SetActive` | clip 每帧写 `m_IsActive` 会覆盖回来 | `PlayerToolThrow` |
| 4 | 玩家材质是 `PlayerFrictionless`（零摩擦），平台必须逐帧把位移直接加到 `Body.position` | 平台走人不带走，玩家原地掉落 | `MovingPlatform` |
| 5 | 站立判定用包围盒几何，不用 `ContactPoint2D.normal` | 零摩擦材质下 normal 不可靠 | `CrumblePlatform` |
| 6 | 订阅事件后必须主动读一次当前值 | 漏掉 `Start` 阶段的首发事件 | `HUDController` |
| 7 | 目标链推进用 `while` | 同帧多事件只推进一格，链卡死 | `LevelObjectiveTracker` |
| 8 | 交互半径（`E` 键）必须算重叠 | 两个触发器同时响应，一次按键做两件事 | `DialogueTrigger` r=2.6 vs `RescueTarget` r=2.2 |
| 9 | `Play` 模式下禁止 `EditorSceneManager.OpenScene` | 抛 `InvalidOperationException` | Unity 限制 |
| 10 | `[SerializeField]` 字段改名 = 丢数据 | 场景里的序列化值全部清空，无报错 | `CodeStandard.md` |

---

## 十、已知架构债（诚实登记）

| # | 问题 | 影响 | 优先级 |
|---|---|---|---|
| 1 | **`LevelResultUI` 是死代码**：`StarEco.Level` 下有一套程序化结算 UI（订阅 `LevelCompletedEvent`），但**四个场景里一个都没挂**；实际生效的是 `UI/ResultPanel`（UIManager 栈式面板）。两套并存，`LevelCompletedEvent` 有 2 个订阅者，其中 1 个永远不触发 | 读代码误判结算在哪实现 | P2 删除 |
| 2 | **6 个孤儿事件 + 1 个完全未用事件**（§6） | 误以为有订阅方 | P2 清理；其中 `LevelObjectiveChangedEvent` 是 2026-10-30 HUD 目标栏移除后才变孤儿的，需在恢复面板时优先启用 |
| 3 | **`DropSpawner` 三关都挂着但配置全空**（`_dropTable` / `_pickupPrefab` 均为 null） | 敌人死亡不掉任何东西；文档曾误判为"未接入" | P1 补配置 |
| 4 | **`TDD.md` §2.1 目录结构与 §3.1 系统关系图停留在 M1 规划态** | 与实际严重不符（写着"当前仅 SampleScene.unity"） | **P0 已在本轮修正** |
| 5 | **`specs/01~03` 的 SDD 是 M1~M3 时期写的**，其中 03 描述的"原地攻击 + 移动可取消"已被 10-27/10-28 两次改动推翻 | SDD 与实现不符 | **P0 已在本轮修正** |
| 6 | **`EnemyAI` 与 `Eco` 之间用事件解耦，但 `Boss` 直接引用 `Player.Combat`** | Boss 无法脱离玩家单独测试 | P3 可接受 |
| 7 | **`Editor/` 49 个脚本 7,877 行超过运行时的一半**，但都是"一次性搭建脚本"，重建场景时会与手工修改冲突 | 生成脚本与场景已漂移（Level02 平台顶面 1.40 vs 实际 2.58） | P1 已在 LevelDesign.md 登记 |

---

## 十一、如何新增一个系统（SOP）

1. 判断归属层（§2）。若跨层，说明切分错了。
2. 在对应目录建脚本，命名空间跟随目录（`StarEco.<目录名>`）。
3. 需要跨系统通信 → **先找 §6 有没有现成事件**；没有则新建事件类放 `Events/`，并在本文 §6 表格登记。
4. 需要全局单例 → 用 `MonoSingleton<T>`，**不要挂场景**（照 `AudioManager` 的模式）。
5. 需要配置 → `ScriptableObject` 放 `Assets/Settings/`，不留硬编码。
6. 写 `specs/NN-系统名/` 三件套（requirements EARS / design / tasks），格式照 `04-EnemyAI`。
7. 回本文更新 §4 系统清单与 §5 依赖矩阵。
8. 在 `TestPlan.md` 补验收用例。

---

## 附：变更记录

| 版本 | 日期 | 变化 | 原因 |
|---|---|---|---|
| v1.0 | 2026-10-30 | 首版。分层架构（151 文件/25,855 行实测）、12 系统清单、依赖矩阵、20 事件拓扑（含 6 个孤儿）、启动时序、5 条主干数据流、10 条硬约束、7 项架构债 | 此前项目**没有任何一份系统总览**：TDD §3.1 只有一张 20 行文字 UML 且停留在 M1 规划态（"当前仅 SampleScene.unity"），真实系统切分、事件拓扑、启动顺序只能靠读 151 个脚本反推。答辩被问"你这游戏分几个系统、怎么解耦"时无法直接回答 |
