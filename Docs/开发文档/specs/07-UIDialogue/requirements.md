# 需求规范：07-UIDialogue（UI 与对话）

> 对应 TDD.md M7 里程碑 | 格式：EARS
> 范围：HUD（HP/净化剂/净化值/目标/蓄力提示）、UI 框架（UIManager 面板栈）、主菜单（独立 Title 场景）、暂停菜单、设置（音量/画质）、关卡选择、结算面板（S/A/B/C 与星级并存）、图鉴（环保知识卡片）、NPC 对话与提示牌阅读、知识卡片场景拾取
> 非目标：Boss 相关 UI（M8）；第二/三关专属 UI（M9，框架复用不加新面板）；音频资源铺量（M10，仅留 AudioManager 调用）；画质选项仅简单三档分辨率/质量等级切换，不引入后处理栈（TDD §4 风险对策）
> 共用约定：代码落点 `Assets/Scripts/UI/`（命名空间 `StarEco.UI`），程序化构建 uGUI（与 M6 LevelResultUI 同款做法，无 UI 资源依赖）；内置字体 `LegacyRuntime.ttf`；数据 JSON 存 `Resources/Data/`；命名与结构遵循既有骨架（M1 事件/存档/音频 + M5 事件 + M6 关卡事件）

## R1 HUD（ heads-up display ）

> **美术动作**：不涉及（心形/进度条均用代码绘制） | **代码落点**：`Assets/Scripts/UI/HUDController.cs`

- THE SYSTEM SHALL 提供 HUD，常驻显示于关卡场景，包含：HP 心形（5 枚，实心=剩余，空心=损失）、净化剂条、净化值进度条（0~100%）、当前目标提示（目标 Label + 进度 x/y）。
- WHILE 关卡状态为 `Playing`，THE SYSTEM SHALL 实时刷新 HUD（HP/净化剂/净化值每帧读取数据源，目标文本订阅事件刷新）。
- WHEN 玩家 HP 发生变化，THE SYSTEM SHALL 更新心形显示（通过既有血量通知更新；不提供回血功能）。
- WHEN 当前净化值进度变化，THE SYSTEM SHALL 更新进度条填充量与百分比文本（订阅 `PurityChangedEvent`）。
- WHEN 关卡当前目标变化，THE SYSTEM SHALL 更新目标提示文本（订阅 `LevelObjectiveChangedEvent`；链完成时显示"关卡目标已完成"）。
- WHEN 目标进度推进（未完成目标计数 +1），THE SYSTEM SHALL 更新目标提示中的 x/y 进度。
- WHILE 玩家处于蓄力状态，THE SYSTEM SHALL 在净化剂条上方显示蓄力进度条（数据源为 SprayController 蓄力比例公开属性；蓄力满时高亮）。
- WHEN 净化剂不足以进入蓄力（低于 `HeavyCost`）且玩家尝试蓄力，THE SYSTEM SHALL 播放净化剂条闪烁提示（视觉反馈，不阻断输入）。
- WHEN 关卡进入终态（`Completed`/`Failed`），THE SYSTEM SHALL 隐藏 HUD，避免与结算面板重叠。
- THE SYSTEM SHALL HUD 数据源缺失时（如主菜单场景无玩家）不报错，组件处于未激活状态。

## R2 UI 框架

> **美术动作**：不涉及 | **代码落点**：`Assets/Scripts/UI/UIManager.cs`、`Assets/Scripts/UI/UIPanel.cs`

- THE SYSTEM SHALL 提供 `UIManager` 单例（`MonoSingleton`），管理面板栈：`Open(name)` / `Close(name)` / `Toggle(name)` / `CloseTop()`。
- THE SYSTEM SHALL 提供 `UIPanel` 抽象基类：`OnOpen()` / `OnClose()` / `OnUpdate()` 钩子 + 面板名标识；面板默认隐藏，打开时激活。
- WHEN 多个面板同时打开，THE SYSTEM SHALL 以后进先出（栈）顺序处理 Esc 返回（只关闭栈顶面板，不穿透关闭下层）。
- WHEN Esc 按下，THE SYSTEM SHALL 关闭栈顶面板；若栈空且处于关卡 Playing 状态，打开暂停菜单（R4）。
- WHILE 任何"阻断型"面板（暂停/设置/关卡选择/图鉴/对话/结算）位于栈顶，THE SYSTEM SHALL 冻结玩家输入（`PlayerController.InputLocked = true`），关闭时恢复。
- THE SYSTEM SHALL HUD 面板为非阻断型（入栈但不锁输入）。
- WHEN 面板打开/关闭，THE SYSTEM SHALL 播放 UI 音效（调用 M1 `AudioManager.PlaySfx`，音效资源缺失时静默跳过，不报错）。

## R3 主菜单（Title 场景）

> **美术动作**：不涉及 | **代码落点**：`Assets/Scripts/UI/MainMenuPanel.cs` + 新建场景 `Assets/Scenes/Title.unity`

- THE SYSTEM SHALL 提供独立主菜单场景 `Title.unity`（Build Settings 注册为 index 0，社区主流结构），场景内含：标题文字、按钮组（开始新游戏 / 继续游戏 / 关卡选择 / 图鉴 / 设置 / 退出游戏）、BGM 播放。
- WHEN 点击"开始新游戏"，THE SYSTEM SHALL 重置关卡进度（`LevelSaveService.ClearAll()` 语义的进度部分）并加载 `Level01` 场景。
- WHEN 点击"继续游戏"且存在通关进度（`LevelSaveService.HasProgress()`），THE SYSTEM SHALL 加载进度中已解锁的最高关卡；无进度时按钮置灰不可点。
- WHEN 点击"关卡选择"，THE SYSTEM SHALL 打开关卡选择面板（R6）。
- WHEN 点击"图鉴"，THE SYSTEM SHALL 打开图鉴面板（R8）。
- WHEN 点击"设置"，THE SYSTEM SHALL 打开设置面板（R5）。
- WHEN 点击"退出游戏"，THE SYSTEM SHALL 退出应用（编辑器模式下记为日志）。
- THE SYSTEM SHALL 主菜单加载时播放主菜单 BGM（AudioManager，资源缺失时静默跳过）。

## R4 暂停菜单

> **美术动作**：不涉及 | **代码落点**：`Assets/Scripts/UI/PausePanel.cs`

- WHEN 关卡进行中按下 Esc 且无其他面板打开，THE SYSTEM SHALL 打开暂停菜单，暂停游戏逻辑（`Time.timeScale = 0`）并冻结玩家输入。
- THE SYSTEM SHALL 暂停菜单包含按钮：继续、重新开始、设置、返回主菜单。
- WHEN 点击"继续"，THE SYSTEM SHALL 关闭暂停菜单并恢复游戏（`Time.timeScale = 1`）。
- WHEN 点击"重新开始"，THE SYSTEM SHALL 恢复 timeScale 后广播 `RestartLevelEvent`（复用 M6 重载场景链路）。
- WHEN 点击"返回主菜单"，THE SYSTEM SHALL 恢复 timeScale 并加载 `Title` 场景。
- WHEN 设置面板从暂停菜单打开，THE SYSTEM SHALL 保持暂停状态不恢复 timeScale（返回后仍暂停）。

## R5 设置面板

> **美术动作**：不涉及 | **代码落点**：`Assets/Scripts/UI/SettingsPanel.cs`

- THE SYSTEM SHALL 提供设置面板，包含：BGM 音量滑条、SFX 音量滑条、画质档位下拉（低/中/高，映射 `QualityLevel` 与分辨率缩放）、返回按钮。
- WHEN 音量滑条变化，THE SYSTEM SHALL 实时调用 `AudioManager.SetBgmVolume` / `SetSfxVolume`（不重启音频）。
- WHEN 画质档位变化，THE SYSTEM SHALL 应用 `QualitySettings.SetQualityLevel` 并记录设置。
- WHEN 点击"返回"，THE SYSTEM SHALL 将设置写入 M1 `SaveSystem`（槽位 `settings`，`GameSettingsData`）并关闭面板。
- WHEN 设置面板打开时，THE SYSTEM SHALL 从存档读取并回填当前设置值。

## R6 关卡选择面板

> **美术动作**：不涉及 | **代码落点**：`Assets/Scripts/UI/LevelSelectPanel.cs`

- THE SYSTEM SHALL 提供关卡选择面板，展示 3 个关卡卡片：关卡名、简介一行、锁定/可玩状态、历史最佳星级。
- WHEN 面板打开，THE SYSTEM SHALL 从 `LevelSaveService.LoadProgress()` 读取 `UnlockedLevelIndex`：索引小于该值的关卡可进入，否则置灰锁定。
- WHEN 点击可玩关卡卡片，THE SYSTEM SHALL 恢复 timeScale（如从暂停打开）并加载对应场景（当前仅 Level01 可加载，其余点击时提示"敬请期待"并记录日志）。
- WHEN 点击锁定关卡卡片，THE SYSTEM SHALL 播放提示音效且不加载场景。

## R7 结算面板（统一进 UI 框架）

> **美术动作**：不涉及 | **代码落点**：`Assets/Scripts/UI/ResultPanel.cs`（改造 M6 `LevelResultUI`）

- THE SYSTEM SHALL 结算面板作为 UI 框架正式面板（替代 M6 `LevelResultUI` 独立写法，订阅同一 `LevelCompletedEvent`/`LevelFailedEvent`）。
- WHEN 关卡完成，THE SYSTEM SHALL 显示：标题"关卡完成"、关卡名、净化度百分比、用时、击杀数、拾取数、评级。
- THE SYSTEM SHALL 同时显示字母评级与星级：净化度 100%→S（★★★）、≥60%→A（★★）、≥30%→B（★）、<30%→C（☆）；阈值沿用 M6 `LevelConfig.StarThresholds`。
- WHEN 关卡失败，THE SYSTEM SHALL 显示标题"关卡失败"、失败原因、提供"重新开始"与"返回主菜单"按钮。
- WHEN 结算面板打开且存在下一关（`result.LevelIndex < UnlockedLevelIndex`），THE SYSTEM SHALL 显示"下一关"按钮；否则隐藏。
- WHEN 点击"下一关"，THE SYSTEM SHALL 加载下一关场景（当前仅第 2 关未建成，则提示"敬请期待"并记录日志，不崩溃）。
- THE SYSTEM SHALL 结算面板阻断输入（沿用 R2 阻断语义），按钮点击后恢复。

## R8 图鉴（环保知识卡片）

> **美术动作**：不涉及（卡片用文字+色块） | **代码落点**：`Assets/Scripts/UI/CodexPanel.cs`、`Assets/Scripts/UI/CodexService.cs`、`Assets/Scripts/Eco/KnowledgeCardPickup.cs`

- THE SYSTEM SHALL 提供图鉴面板：网格展示全部知识卡片，已收集显示完整内容（标题+知识正文），未收集显示"???"占位与剪影。
- THE SYSTEM SHALL 知识卡片数据存 JSON（`Resources/Data/knowledge_cards.json`，共 8 条，覆盖三关主题：塑料污染/森林生态/能源与气候），字段：`Id/Title/Body/LevelIndex`。
- THE SYSTEM SHALL 图鉴收集进度持久化（`LevelSaveService` 扩展槽位 `codex.json`，存已收集卡片 Id 列表）。
- WHEN 玩家碰到场景中的知识卡片拾取物，THE SYSTEM SHALL 播放拾取表现（弹入动画可省，直接显示 toast"获得知识卡片：{Title}"）、标记该卡片为已收集、销毁拾取物（幂等：重复触发不重复入档）。
- WHEN 图鉴面板打开，THE SYSTEM SHALL 刷新收集状态（新收集的卡片即时亮起）。
- THE SYSTEM SHALL 主菜单与关卡内均可打开图鉴（HUD 不提供入口，从主菜单进；关卡内 Esc 栈无面板时不提供图鉴入口，避免误触）。

## R9 NPC 对话与提示牌

> **美术动作**：不涉及 | **代码落点**：`Assets/Scripts/UI/DialoguePanel.cs`、`Assets/Scripts/UI/DialogueTrigger.cs`、`Assets/Scripts/UI/SignboardTrigger.cs`、数据 `Resources/Data/dialogues.json`

- THE SYSTEM SHALL 提供对话系统：对话数据 JSON（`Resources/Data/dialogues.json`），每条含 `Id/Entries[]`，Entry 含 `Speaker/AvatarTint/Text/EventId`（EventId 为对话结束时广播的事件标识，如 `unlock_shortcut`）。
- THE SYSTEM SHALL 对话面板：逐字显示（每 `TypeInterval` 秒出一字）、说话人名、头像占位（色块+首字）、"按 E 继续"提示。
- WHEN 玩家靠近 NPC 触发器并按交互键，THE SYSTEM SHALL 打开对话面板、冻结输入、从第一条开始播放；对话播放中再按 E 快进到整句显示。
- WHEN 一句显示完毕按 E，THE SYSTEM SHALL 推进到下一句；最后一句确认后关闭面板、恢复输入、广播该对话的 `EventId` 事件（如有）。
- WHEN 玩家靠近提示牌触发器并按交互键，THE SYSTEM SHALL 以阅读界面展示提示牌全文（不分句，支持翻页：每页最多 `PageCharCount` 字，E 翻页，末页关闭）。
- WHILE 对话或阅读界面打开，THE SYSTEM SHALL 冻结玩家输入（R2 阻断语义）。
- WHEN 同一触发器重复交互，THE SYSTEM SHALL 允许重播（不锁一次性；一次性触发器由数据字段 `Once` 控制，默认 false）。

## R10 编译、验证与文档

- Unity 编译零错误（`Assets/Scripts/UI/` 全部 .cs + `Editor/TitleSetup.cs` + 对 `LevelResultUI` 的改造 + `PickupKind`/`CodexService` 最小增量）。
- 运行验证：HUD 四项数据正确；Title 场景按钮流转（开始/继续置灰逻辑/关卡选择锁定/设置保存/图鉴）；Esc 暂停/恢复；对话逐字与快进；提示牌翻页；知识卡片拾取入图鉴；结算 S/A/B/C 与星级并存、下一关按钮逻辑。
- `tasks.md` 状态行更新 + 需求对齐报告（AGENTS.md §1.3）。
- git commit：`M7: UI/dialogue — HUD/UIManager/title/pause/settings/level-select/codex/dialogue`。
