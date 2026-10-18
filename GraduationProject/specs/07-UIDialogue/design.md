# 设计文档：07-UIDialogue（UI 与对话）

> 对应 `requirements.md` R1~R10。本设计只做架构与关键决策，不贴大段源码。

## 1. 总体架构

```
UIManager（MonoSingleton，DontDestroyOnLoad）
   ├─ 面板栈 List<UIPanel>（LIFO）
   ├─ Register(panel) / Open(name) / Close(name) / CloseTop()
   └─ Esc 路由：栈顶优先；栈空 + 关卡 Playing → 打开暂停

UIPanel（抽象基类，MonoBehaviour）
   ├─ 属性 PanelName
   ├─ 虚方法 OnOpen() / OnClose() / OnUpdate(float dt)
   └─ 静态工厂 BuildXxx(...)：程序化 uGUI，返回 GameObject 引用树

HUDController（非阻断面板）
   ├─ HP 心形行（Image × 5，白/灰）
   ├─ 净化剂条（Image fill + 数值）
   ├─ 净化值进度条（Image fill + %）
   ├─ 目标提示（Text）
   └─ 蓄力条（默认隐藏）

阻断面板：MainMenu / Pause / Settings / LevelSelect / Codex / Dialogue / Result
```

场景结构（社区主流）：

- `Title.unity`（Build index 0）：UIManager + MainMenu 面板 + BGM。
- `Level01.unity`（Build index 1）：UIManager + HUD + 暂停挂载 + 对话触发器×N + 知识卡片×2 + 结算订阅。

UIManager 跨场景常驻（DontDestroyOnLoad），`OnDestroy` 时不退订（单例生命周期 = 应用生命周期）；面板注册表在场景加载时由各面板 `Awake` 重建。

## 2. 关键设计决策

### 2.1 为什么面板栈用 LIFO + Esc 栈顶

多面板叠加是必然场景（暂停→设置→返回）。LIFO 保证 Esc 永远关"最近打开的那个"，符合玩家直觉；若用集合无序，Esc 行为不可预测。HUD 例外：入栈但不参与阻断锁输入，也不响应 Esc 关闭。

### 2.2 输入冻结的统一收口

冻结不在各面板各自实现，而是 UIManager 在 `Open/Close` 时按栈顶面板的 `BlocksInput` 属性统一设置 `PlayerController.InputLocked`。避免面板间互相覆盖（如暂停中开设置又关设置，输入状态错乱）。暂停另加 `Time.timeScale = 0`，仅 PausePanel 自己管理（游戏逻辑暂停 ≠ 输入冻结，职责不同）。

### 2.3 HUD 数据源与 M3 的零改动边界

| HUD 元素 | 数据源 | 刷新方式 |
|---|---|---|
| HP 心形 | `PlayerCombat.CurrentHP/MaxHP` | 每帧轮询（便宜，5 个 int 比较） |
| 净化剂条 | `PurifyTank.Current` | 每帧轮询 |
| 净化值进度条 | `PuritySystem.Percent` | 订阅 `PurityChangedEvent` |
| 目标提示 | `LevelObjectiveTracker.CurrentObjective.Label` | 订阅 `LevelObjectiveChangedEvent` |
| 蓄力条 | `SprayController.ChargeRatio`（M7 新增只读属性，约 3 行） | 每帧轮询 |

M3 无 HP 变化事件，但每帧轮询 5 个心形的显隐在本项目规模下开销可忽略（对比引入事件要改 M3 源码，违反"M7 对既有系统零源码改动"原则——唯一例外是 `SprayController.ChargeRatio` 只读属性，design §4 论证）。

### 2.4 结算评级：S/A/B/C 与星级并存

用户 2026-10-18 确认。映射表（阈值沿用 M6 `LevelConfig.StarThresholds`）：

| 净化度 | 字母 | 星级 |
|---|---|---|
| 100% | S | ★★★ |
| ≥60% | A | ★★ |
| ≥30% | B | ★ |
| <30% | C | ☆ |

M6 `LevelResultUI` 退场：其订阅逻辑（`LevelCompletedEvent`/`LevelFailedEvent`）平移到 `ResultPanel`，类保留但标记 `[Obsolete]` 并由 LevelSetup 停止挂载，保证 git 历史中 M6 提交不被改写。`LevelConfig.EvaluateStars` 复用不变。

### 2.5 图鉴数据与存档

- 卡片 JSON：`Resources/Data/knowledge_cards.json`，`KnowledgeCardData[]`，JsonUtility 可序列化。
- 收集进度：`LevelSaveService` 新增 `SaveCodex/CollectCard/LoadCodex`，槽位 `codex.json`（不带 .json 后缀，M6 教训），DTO `CodexProgress { List<string> CollectedIds }`。
- 拾取物 `KnowledgeCardPickup`：MonoBehaviour + Trigger，读 `CardId` 字段；收集后 `SetActive(false)` 并写档。场景中每张卡片一个实例（同关 2 张）。

**环保知识卡片文案（8 条，M7 撰写）**：

| Id | 标题 | 所属关 |
|---|---|---|
| kc_plastic_bottle | 一个塑料瓶的 450 年 | L1 |
| kc_plastic_bag | 塑料袋的海洋漂流记 | L1 |
| kc_recycle_symbol | 三角循环标志的秘密 | L1 |
| kc_forest_oxygen | 森林是地球的肺 | L2 |
| kc_food_chain | 一条溪流的食物链 | L2 |
| kc_invasive_species | 外来物种为何危险 | L2 |
| kc_carbon_energy | 碳足迹与能源选择 | L3 |
| kc_renewable | 可再生能源三兄弟 | L3 |

文案正文在 JSON 数据文件中，每条 60~100 字，面向玩家科普口吻。

### 2.6 对话系统

- 数据：`Resources/Data/dialogues.json`，`DialogueData[] { Id, Once, Entries[] }`，Entry `{ Speaker, AvatarTint(int argb 简写为 hex 字符串), Text, EventId }`。
- `DialogueTrigger`：圆形触发区 + `DialogueId` + `InteractKey`（默认 E，复用 M2 交互输入）；玩家进入范围显示"按 E 交谈"气泡提示（Text + 描边），按 E 打开 `DialoguePanel`。
- `DialoguePanel` 逐字：`TypeInterval = 0.03s/字`；显示中按 E → 整句补齐；句间按 E → 下一句；末句确认 → `Close` + 广播 `EventId`（如有，走 `EventCenter.Publish(new DialogueEventIdEvent{...})`，R9 新增事件 struct）。
- `SignboardTrigger`：同触发交互，但面板为阅读模式：整段文本分页（每页 80 字），E 翻页。
- 与 M6 目标链的衔接：第一关放 1 个 NPC（"生态守卫者艾娃"，教学对话）+ 2 块提示牌。对话 `EventId` 本期只预留机制，不挂实际关卡逻辑。

### 2.7 对既有系统的最小改动清单

| 文件 | 改动 | 行数 | 论证 |
|---|---|---|---|
| `SprayController.cs` | 加 `public float ChargeRatio => ...` 只读属性 | ~3 | R1 蓄力条唯一数据源；不加则 HUD 只能反射读私有字段，更脆 |
| `LevelResultUI.cs` | 标记 Obsolete、停止被 LevelSetup 引用 | 0 增 | 保留 M6 提交历史 |
| `LevelSetup.cs` | HUD/UIManager/触发器/卡片挂载 + Build 场景 | 扩展 | 集成必须 |
| `LevelSaveService.cs` | 加 Codex 槽位与读写 | ~30 | R8 |

除上述外对 M1~M6 零源码改动。

## 3. 程序化 uGUI 约定（沿用 M6 LevelResultUI 先例）

- 屏幕级 Canvas：`RenderMode.ScreenSpaceOverlay`，`CanvasScaler` 用 `ScaleWithScreenSize`（ReferenceResolution 1920×1080，Match 0.5）。
- 世界空间气泡 Canvas（对话/提示牌/救援）：**`RenderMode.WorldSpace` 下 `CanvasScaler` 完全不生效**，`scaleFactor` 被忽略。像素→世界的换算必须落在 `transform.localScale`（`UIUtility.WorldUiScale = 0.01f`，见 `UIUtility.CreateWorldCanvas`），RectTransform 的 `sizeDelta` 仍按像素填写（400×120 → 4×1.2 世界单位）。
  - 早期版本误把换算写在 CanvasScaler 上，导致 400×120 的气泡被当作 **400×120 世界单位**渲染（视野仅 16×9），一个气泡横跨 25 屏，表现为斜穿画面的巨大白色带，且**污染所有运行时截图**，使视觉验收结论全部失真。
- EventSystem 缺失时自动创建（`LevelResultUI` 已有同款代码，抽到 `UIUtility` 复用）。
- 字体：`LegacyRuntime.ttf` **不含 CJK 字形**，中文会渲染为方块（乱码）。改用 `Resources.Load<Font>("Fonts/NotoSansSC-Regular")`（仓库内自带，SIL OFL 1.1，可随毕设分发），未找到时才回退内置字体并打 Warning。见 `UIUtility.BuiltinFont` / `LevelResultUI`。
- 面板默认隐藏：`UIPanel.Close()` 在 `_isOpen == false` 时直接 return，场景中激活态面板没有任何代码路径能隐藏。因此 `SetRoot(root, visibleByDefault: false)` 在 Awake 时主动 `SetActive(false)`（HUD 传 true 常驻）。
- 颜色：面板底 `#1A1A2EE6`（半透明深蓝紫）、主文字白、强调绿 `#7CFC98`、警示红 `#FF6B6B`、禁用灰 `#888`。
- 布局：锚点预设（左上/居中/底部），不用 LayoutGroup（面板元素少，手动锚点更可控）。

## 4. 测试要点

- EditMode：CodexService 读写幂等；评级映射边界（99.9/60/30/29.9）。
- Play：Title→开始→Level01 HUD 显示；Esc 暂停→设置→返回→继续；NPC 对话逐字与快进；提示牌翻页；卡片拾取 toast + 图鉴亮起；净化 100% 结算显示 S/★★★ 与下一关按钮（隐藏，因第 2 关未建）；死亡失败面板。

## 5. 风险

| 风险 | 对策 |
|---|---|
| uGUI 程序化代码量大 | 复用 M6 `UIUtility`（抽公共构建函数）；每个面板独立文件 |
| UIManager 跨场景与 MonoSingleton 的 DontDestroyOnLoad 冲突（M6 教训：重载场景产生重复） | UIManager 挂 Title 场景根节点（本来就 DontDestroyOnLoad 语义，符合预期）；关卡场景不再挂 UIManager，各面板 Awake 时 `FindObjectOfType<UIManager>()` 注册 |
| 对话中场景重载导致触发器状态丢失 | 触发器无持久状态（Once 也不存档，本期对话均为可重播教学对话） |
| 文字溢出（长文本/小屏） | 面板 Text 统一开 `HorizontalOverflow/Truncate` 策略 + 字号下限 20 |
