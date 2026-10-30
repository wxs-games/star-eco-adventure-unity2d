# 技术设计文档（TDD）

> 项目：基于Unity2D的"星际环保"动作冒险游戏设计与实现
> 版本：v0.8（BossGate 未启用标注 + P0 喷口自伤修复同步） | 最后更新：2026-10-30
> 参考模板：rikegames Unity TGDD + KelptheWriter/TechDesignDocument 结构
> 关联文档：GDD.md（设计愿景）、**SystemOverview.md（系统总览，§2.1/§3.1 的完整版）**、LevelDesign.md（关卡落地）、AudioDesign.md（音频规格）、NarrativeDesign.md（叙事设定）、specs/（逐功能规范三件套）

---

## 1. 技术概览

| 项 | 决策 | 理由 |
|---|---|---|
| 引擎 | **Unity 2022.3.62f3c1**（2D，内置渲染管线） | 毕设主题指定；2D 工具链成熟 |
| 语言 | C#（.NET Standard 2.1） | 引擎原生 |
| 版本管理 | Git + GitHub（已有 main 分支），美术大文件不进 LFS（已提交验证） | 现状可用 |
| 目标平台 | Windows Standalone，60 FPS | 毕设演示环境 |
| 外部依赖 | 仅 TextMeshPro（内置）；其余自研 | 减少答辩时"用了多少现成库"的质疑 |

## 2. 工程结构与规范

### 2.1 目录结构（2026-10-30 实测，全部已落地）

> 系统切分、依赖矩阵、事件拓扑的完整说明见 **`SystemOverview.md`**，本节只列路径。

```
Assets/
├─ Art/           美术源（Player 22 动作 / Enemies / Boss / Towers / FX 6 套 / Environment）
├─ Scenes/        Title / Level01 / Level02 / Level03（Build Settings 顺序即关卡顺序）
├─ Prefabs/       Player、敌人、道具、FX/fx_purify
├─ Settings/      PlayerConfig / CombatConfig / Levels/Level01~03 / Enemy Config×3 / Boss01 / EcoConfig
├─ Resources/     Data/*.json（dialogues / knowledge_cards）、Audio/Bgm、Audio/GameAudioLibrary.asset
├─ Animations/    Player.controller（20 参数/22 状态）、Boss01、Enemy01~03、Tower01
├─ Audio/Sfx/     22 个 SfxId 命名 wav
├─ Tests/         EditMode 测试
└─ Scripts/       151 个 .cs / 25,855 行
   ├─ Core/        3 文件 / 381 行   — CsvTable、DamageMath、NumericSafety（L0 无依赖）
   ├─ Framework/   8 文件 / 908 行   — 单例、事件中心、对象池、存档、音频、自检（L1）
   ├─ Player/      7 文件 / 2,446 行 — 控制器、状态机、14 状态、动画驱动、工具锚点（L2）
   ├─ Combat/      11 文件 / 1,272 行— 伤害、判定框、连段、投掷物、打击感（L2）
   ├─ EnemyAI/     8 文件 / 1,512 行 — FSM 基类、三种敌人、Tower01（L2）
   ├─ Boss/        7 文件 / 1,826 行 — 三阶段状态机、封锁门、血条（L2）
   ├─ Eco/         21 文件 / 2,077 行— 净化值、污染度、垃圾堆、喷雾、知识卡、掉落（L2）
   ├─ Level/       20 文件 / 3,435 行— 目标链、关卡状态机、生成、机关、危害物、结算（L3）
   ├─ UI/          17 文件 / 4,121 行— 面板栈、HUD、菜单、结算、图鉴、对话（L4）
   └─ Editor/      49 文件 / 7,877 行— 场景生成、动画搭建、批量修复（L5，不进运行时）
```

> ⚠️ 旧版本本节写着"Scenes 当前仅 SampleScene.unity / EnemyAI 规划中 / LevelFlow 待建"——那是 M1 时期的规划态，**与本版实际结构严重不符**，已于 v0.7 全量重写。

### 2.2 编码规范
- 命名：类/方法 PascalCase，私有字段 `_camelCase`，常量 PascalCase；文件名=类名
- 模式应用：管理器用泛型单例；系统间通信走事件中心（解耦，论文可写"观察者模式"）；子弹/FX/敌人用对象池；敌人与玩家用状态机；配置用 ScriptableObject 数据驱动
- 性能红线：Update 内禁止 `Find`/`GetComponent`（缓存于 Awake）；物理逻辑只在 FixedUpdate；频繁生成物全部入池；每帧零 GC 分配为目标
- 碰撞分层：Player / Enemy / Boss / Ground / Interactable / FX 六层 + 碰撞矩阵配置表

## 3. 核心架构（对应论文"系统设计"章）

### 3.1 系统关系图

> **完整版见 `SystemOverview.md`**（12 系统清单、依赖矩阵、20 个事件拓扑、启动时序）。此处只留一张压缩图。

```
[L1 Framework]  EventCenter(静态) / MonoSingleton / PoolManager / AudioManager / SaveSystem
        ↑ 全部系统只依赖这一层
┌───────┴────────────────────────────────────────────────┐
│ [L2 玩法域·互不引用，仅走事件与 IHittable]              │
│  Player(14 状态)  Combat  EnemyAI  Boss  Eco            │
└───────┬────────────────────────────────────────────────┘
        ↑ 编排
[L3 Level]  LevelManager(状态机·终态幂等) / LevelObjectiveTracker(有序目标链)
            LevelSpawner / 机关(Crumble·Moving·Lever·Water) / 危害物 ×4
        ↑ 表现
[L4 UI]     UIManager(面板栈·输入阻塞仲裁) / HUD / 菜单 / 结算 / 图鉴 / 对话
```

**三条架构约束**（`SystemOverview.md` §5 有完整矩阵）：
1. L2 之间不互相 `using` —— `EnemyAI` 不知道 `Eco` 存在
2. L3 是唯一允许"知道所有人"的层
3. L0~L2 不得 `using StarEco.UI`

### 3.2 关键数据流
- 净化：SprayController 命中 `IPurifiable` → EcoSystem.AddPurity() → EventCenter 广播 → HUD/Atmosphere/LevelFlow 三方响应
- 敌人死亡：HealthSystem Die 事件 → ObjectPool 回收 → 掉落表(ScriptableObject) 生成补给
- 存档：`SaveSystem` 当前落盘 4 个槽——`settings`（音量等设置，`AudioManager`/`SettingsPanel`）、`progress`（通关进度，`LevelSaveService`）、`codex`（图鉴解锁，`CodexService`）、`level01_result`（上一局结算数据，用于结算面板复现）。⚠️ 原文「检查点触发 → 序列化 LevelState（净化值/已收集品/检查点 ID）」为**规划未实现**——项目无检查点系统，`LevelState` 是运行时内存态、不落盘，死亡即整关重载

### 3.3 ScriptableObject 数据模型
- `EnemyConfig`：HP/移速/攻击力/攻击间隔/掉落表/动画帧率
- `LevelConfig`：目标序列、净化值阈值、初始净化剂、污染度-色调曲线
- `DialogueAsset`：NPC 对话 JSON 引用 + 事件回调 ID
- `ItemConfig`：道具类型/效果/图标引用

## 4. 功能清单与风险
| 功能 | 风险 | 对策 |
|---|---|---|
| 横版手感（土狼时间/缓冲） | 手感调参耗时 | 参数全部入 SO，运行时可调 |
| Boss 三阶段状态机 | 复杂度最高 | 先做通用 FSM 基类，Boss 只写阶段脚本 |
| 污染度驱动视觉 | 需要后处理/色调插值 | 用简单材质属性插值，避免引入后处理栈 |
| 对话/图鉴系统 | 内容量大 | JSON 数据驱动，策划表可批量填 |
| 三个关卡搭建 | 场景工作量 | Tilemap + 预制件复用，MCP 工具辅助批量摆放 |
| Boss 破土登场（`BossGate.cs`，372 行） | **毕设版未启用**：五个场景零挂载；第三关右端墙是 `GarbageWall_03R`，净化即开路，Boss 开局就是 active 站在 (68.0, ~2.3)，**没有登场演出** | 用户拍板「保留 + 标注未启用」：脚本留给求职展示版（那版右端墙换 `BossGateWall`）；类头注释 / 本文 / `RiskRegister` T-18 三处标注，不把它算作已实现的流程 |

## 5. 实施计划（与 specs/ 对应）

| 阶段 | Spec 包 | 交付物 | 进度 |
|---|---|---|---|
| M1 框架 | 01-Framework | 单例/事件/对象池/存档/音频 五件套 + FrameworkSelfTest（Boot 场景并入后续） | ✅ 已完成 |
| M2 玩家 | 02-PlayerController | 完整移动跳跃手感 + 动画状态机 + 一键搭建工具 | ✅ 已完成 |
| M3 战斗 | 03-Combat | 三段攻击 + 空中攻击 + HP/受击/无敌帧 | ✅ 已完成 |
| M4 敌人 | 04-EnemyAI | FSM 基类 + 三种敌人 + Tower01 | ✅ 已完成 |
| M5 环保玩法 | 05-EcoSystem | 净化喷雾/垃圾堆/净化值/污染度 | ✅ 已完成 |
| M6 关卡一 | 06-Level01 | 第一关完整可玩（含检查点/结算） | ✅ 已完成 |
| M7 UI/对话 | 07-UIDialogue | HUD/菜单/对话/图鉴 | ✅ 已完成 |
| M8 Boss+关三 | 08-Boss | Boss 阶段机 + 第三关 | ✅ 已完成 |
| M9 关卡二三 | 09-Level02-03 | 解谜/解救玩法 + 全流程打通 | ✅ 已完成（Level02：E 解救/水解谜/Enemy02；Level03 沿用 M8；三关全流程+解锁链路打通） |
| M10 打磨 | 10-Polish | 音效铺量/数值校准/Build | ✅ 已完成（18 SFX+4 BGM 程序化合成、全调用点接线、校准 2 项、Windows 构建通过） |

## 6. 测试计划
- 单元级：伤害计算、净化值累加、存档序列化（EditMode Test）
- 集成级：每 M 完成后走查 GDD"玩法走读"路径
- 性能级：Profiler 检查关卡 GC=0、帧率稳定 60
- 验收级：对照对应 spec 的 requirements.md 逐条 EARS 验证

## 7. 变更记录
| 版本 | 日期 | 变化 |
|---|---|---|
| v0.1 | 2026-10-15 | 初稿：架构与 M1~M10 计划 |
| v0.2 | 2026-10-17 | 进度同步：M4~M8 已完成（敌人/环保/关卡一/UI 对话/Boss+关三） |
| v0.3 | 2026-10-18 | M9：Level02（E 键解救/水流解谜/Enemy02）+ 全流程打通；修 `LevelObjectiveTracker.Evaluate` 链停滞（单次 if→while）；UI 三处索引对齐 |
| v0.4 | 2026-10-18 | M10：音频铺量（Tools/audio-pipeline 合成 18 SFX+4 BGM、GameAudioLibrary、AudioManager 命名 API、13 处调用点接线）；校准 Enemy01/03 近战 AttackRange（10→3.5 / 9→3.0，修"范围远大于命中几何"导致空挥木桩）；Windows x64 构建通过（Builds/，1052MB，0 错误 0 警告） |
| v0.5 | 2026-10-19 | 性能：GC=0 达成（Build 版口径）。消除 2 处每帧分配——`DialogueTrigger.Update` 每帧 `FindObjectOfType`（Level01，40 B/帧）、`BossHealthBar` 每帧字符串插值（Level03）；实测证实 Editor 下 ~375 次/帧为编辑器底噪、不可作为 GC 判定口径。详 `性能实测报告.md` |
| v0.6 | 2026-10-30 | §3.2 存档数据流校正：实为 4 个槽（`settings`/`progress`/`codex`/`level01_result`），原文"检查点触发 → 序列化 LevelState"标为**规划未实现**（无检查点系统，死亡即整关重载）；头部版本号从 v0.2 补到实况；关联文档补 AudioDesign.md / NarrativeDesign.md | 头部版本与变更记录脱节 5 个版本；存档与检查点描述与实现不符（同 GDD v0.8 的检查点修正） |
| v0.7 | 2026-10-30 | **§2.1 目录结构全量重写**：M1 规划态（"当前仅 SampleScene.unity"、"EnemyAI 规划中"、不存在的 `Dialogue/` `Quest/` `LevelFlow/` 三个目录）→ 实测的 10 目录 151 文件 25,855 行；**§3.1 系统关系图重绘**：删掉不存在的 `GameManager(Facade)` / `SceneManagerEx` / `HealthSystem`，改为真实的三层约束图；新增 `SystemOverview.md` 承接完整版 | TDD 是 M1 时写的，之后 22 次提交从未更新 §2.1/§3.1，导致"工程结构"和"系统关系"两节描述的是**开工前的设想**而非成品。答辩时若按 TDD 讲架构，会讲到三个不存在的类 |
| v0.8 | 2026-10-30 | **§4 新增 `BossGate` 未启用行**：明确它是求职展示版模块、毕设版零挂载，避免答辩时把它讲成"已实现的 Boss 登场演出"；同步记录 P0 修复——`VentHazard` 增加 `_damagePlayerOnly`（只结算玩家），修掉"Boss 被自己的喷气口顶飞 + 掉血"（实测 96s 游戏时间 80→80）；`BossHealthBar` 显示时机改为进战区（距 13u 或已掉血）才弹 | 用户拍板三件：① 喷口只伤玩家 ② Boss 血条进战区显示 ③ BossGate 保留 + 标注未启用 |
