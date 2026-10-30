# 技术方案（SDD）：07-UIDialogue（UI 面板栈 / 对话 / 图鉴）

> 版本：v2.0（2026-10-30 按落地代码重写） | 对应 `requirements.md` R1~R10 | 命名空间 `StarEco.UI` | 代码落点 `Assets/Scripts/UI/`（18 个 .cs / 4,139 行）
> 前置已就位：M1 `MonoSingleton`/`EventCenter`/`SaveSystem`/`AudioManager`、M2 `PlayerController.InputLocked`/`ExternalInputLock`、M3 `PlayerCombat.CurrentHP`、M5 `PurifyTank`/`PuritySystem`/`SprayController`、M6 `LevelManager`/`LevelObjectiveTracker`/`LevelSaveService`/`LevelConfig`
> 立项决策（已定稿）：**面板栈单例 + 程序化 uGUI**（不引入 Prefab/图集，样式改代码）；输入冻结在 `UIManager` 一处收口；世界空间气泡统一走 `UIUtility.CreateWorldCanvas`（`localScale=0.01` 做像素→世界换算）；字体用仓库自带 `NotoSansSC-Regular`（内置 `LegacyRuntime.ttf` 无 CJK 字形）
>
> **v2.0 修正的三处失实**：① `LevelResultUI` **没有**标 `[Obsolete]`，而是纯死代码（§6 D1）② 面板不是 7 个而是 **8 个 PanelId**（§1.2）③ 对白不是 8 段而是 **14 段 36 条**（§2.6，M10 叙事补白后）

## 1. 结构

### 1.1 分层与边界

```text
UIManager（MonoSingleton，挂 Title 场景根节点 → 跨场景常驻）
  ├─ List<UIPanel> _stack            面板栈（LIFO）
  ├─ Dictionary<string, UIPanel>     注册表（PanelName → 实例）
  ├─ Register/Unregister/Open/Close/Toggle/CloseTop/ClearStack
  └─ Esc 路由 + ApplyInputLock（唯一输入冻结收口）

UIPanel（抽象基类，MonoBehaviour）
  ├─ abstract string PanelName
  ├─ virtual bool BlocksInput => true      HUD 覆写为 false
  ├─ OnOpen()/OnClose()/UpdatePanel(dt)
  └─ Close() 在 !_isOpen 时直接 return（幂等）

具体面板（8 个 PanelId）
  HUD / Pause / Settings / MainMenu / LevelSelect / Codex / Dialogue / Result

世界空间叠加层（不是面板，不进栈）
  DialogueTrigger / SignboardTrigger / RescueTarget  → 自建 WorldSpace Canvas 气泡
  DamageNumberLayer / EnemyHealthBarLayer            → 订阅 EnemyDamagedEvent 的飘字与血条
  BossHealthBar                                      → 订阅 BossPhaseChangedEvent 的顶部血条
```

**边界**：`StarEco.UI` **不写游戏逻辑**，只订阅事件 + 轮询只读属性；对 M1~M6 **零源码改动**，唯一新增是 `SprayController.ChargeRatio`（~3 行只读属性，供蓄力条）。

### 1.2 八个面板清单（实测 `PanelId` 常量）

| 面板 | PanelId | BlocksInput | 打开入口 | 关闭入口 |
|---|---|---|---|---|
| `HUDController` | `HUD` | **false**（不阻断、不响应 Esc） | 关卡 `Start` 时自开 | 关卡结束随场景销毁 |
| `PausePanel` | `Pause` | true（另设 `Time.timeScale=0`） | Esc（栈空且 `LevelManager.State==Playing`） | 继续 / Esc |
| `SettingsPanel` | `Settings` | true | 主菜单或暂停菜单按钮 | 返回按钮 / Esc |
| `MainMenuPanel` | `MainMenu` | true | Title 场景 `Start` | 开始游戏（切场景） |
| `LevelSelectPanel` | `LevelSelect` | true | 主菜单「选择关卡」 | 返回 / Esc |
| `CodexPanel` | `Codex` | true | 主菜单「环保图鉴」 | 返回 / Esc |
| `DialoguePanel` | `Dialogue` | true | `DialogueTrigger` 按 E / 关卡落幕对白 | 末句按 E / Esc |
| `ResultPanel` | `Result` | true | 订阅 `LevelCompletedEvent`/`LevelFailedEvent` 自开 | 重新开始 / 下一关 / 返回主菜单 |

### 1.3 场景职责

| 场景 | Build index | 常驻内容 |
|---|---|---|
| `Title.unity` | 0 | `UIManager`（唯一实例，跨场景常驻）+ `MainMenuPanel` + `SettingsPanel` + `LevelSelectPanel` + `CodexPanel` + 标题 BGM |
| `Level01/02/03.unity` | 1/2/3 | `HUDController` + `PausePanel` + `DialoguePanel` + `ResultPanel` + 触发器（NPC/提示牌/救援笼）+ `DamageNumberLayer` + `EnemyHealthBarLayer` + （L3）`BossHealthBar` |

> **为什么关卡场景也不得再挂 `UIManager`**：M6 教训——`MonoSingleton` 的 `DontDestroyOnLoad` 会让第二个实例被销毁但**面板注册表已被新实例覆盖**，表现为"进关卡后 Esc 打不开暂停"。正解：全项目只有 Title 一个 `UIManager`，关卡内面板在 `Awake` 里 `FindObjectOfType<UIManager>()` 注册（`UIManager.Start` 还有一次兜底注册，因为首个场景的 `Awake` 顺序不确定）。

## 2. 关键机制

### 2.1 面板栈 LIFO 与 Esc 路由

```text
Esc 按下（UIManager.Update）
  if (_stack.Count > 0):
      top = 栈顶
      if (top.BlocksInput) { CloseTop(); return; }     ← HUD 不阻断，所以不会被 Esc 关掉
  // 栈空或只剩 HUD
  if (LevelManager.State == Playing) Open(Pause)
```

- LIFO 保证 Esc 永远关"最近打开的那个"（暂停 → 设置 → 返回 是必然路径）。
- `Open` 已入栈则调 `panel.Open()` 幂等返回，不重复入栈。
- `Close` 先 `_stack.Remove` 再 `panel.Close()`，最后 `ApplyInputLock()`——顺序反了会在面板还开着时解锁输入。

### 2.2 输入冻结双层收口（v2.0 补充的关键细节）

```text
ApplyInputLock():
  player = FindObjectOfType<PlayerController>()
  player.InputLocked         = IsBlocking
  player.ExternalInputLock   = IsBlocking      ← 第二层：外部强制锁
```

**为什么必须两层**：只写 `InputLocked` 会被玩家 FSM 的 `Exit` 清成 `false`——实测"对话中按一下 Shift 就又能走了"（翻滚/冲刺状态的 `Exit` 复位了它）。`ExternalInputLock` 是"外部强制"，只有 UI 能解除。

而 `Time.timeScale = 0` 只由 `PausePanel` 自己管：**游戏逻辑暂停 ≠ 输入冻结**，两者职责不同（对话要冻输入但游戏世界可以继续跑动画）。

### 2.3 HUD 数据源（2026-10-30 改版后只有 3 个元素）

> **v2.1 重要变更（2026-10-30）**：HUD 的**目标栏**与**净化值面板**已按"让玩家自行探索、不显示数值与目标"的决定移除。当前 HUD 只剩三项。

| HUD 元素 | 数据源 | 刷新方式 |
|---|---|---|
| HP 心形 ×5（实心=剩余 / 空心=损失） | `PlayerCombat.CurrentHP / MaxHP` | 每帧轮询（5 个 int 比较，可忽略） |
| 净化剂条 + 数值「净化剂 12/100」 | `PurifyTank.Current / Capacity` | 每帧轮询；低于阈值时条体闪烁（`PingPong` 插值到 `WarnRed`），只警告一次 |
| 蓄力进度条（蓄力中才显示） | `SprayController.ChargeRatio` | 每帧轮询 |
| （已移除）净化值进度条 + % | ~~`PuritySystem.Percent`~~ | 改由 `AtmosphereController` 的背景色插值承担 |
| （已移除）目标提示 + x/y | ~~`LevelObjectiveTracker.CurrentObjective`~~ | **不再显示**；目标推进对玩家不可见 |
| （已移除）HUD 提示条 | ~~`ObjectiveCompletedEvent`~~ | 该事件仍被订阅，但只留一条 `Debug.Log` |

**这次改版的两个连带后果**（已登记，不是"顺手改的"）：

1. `LevelObjectiveChangedEvent` 现在**0 订阅者**——`LevelObjectiveTracker` 每次推进照样广播，但没人接。它成为继 `BossDefeatedEvent`/`PuzzleSolvedEvent` 之后的又一个孤儿事件。
2. `LevelObjectiveTracker` 为"HUD 首帧主动拉一次"而暴露的 `CurrentProgress` / `CurrentRequired` 属性**当前无消费方**（保留，供将来恢复面板或做调试 HUD）。

**为什么不加 HP 变化事件**：每帧比 5 个 int 的成本远低于"给已验收的战斗系统加一条事件链"的风险。这是本项目一贯的取舍（能轮询就不加事件）。

M3 没有 HP 变化事件——不为 HUD 给 M3 加事件，因为"每帧比 5 个 int"的成本远低于"给已验收的战斗系统加一条事件链"的风险。

### 2.4 评级与星级并存

| 净化度 | 字母评级 | 星级 | 结算面板文案 |
|---|---|---|---|
| 100% | S | ★★★ | `评级：S    ★★★` |
| ≥60% | A | ★★ | |
| ≥30% | B | ★ | |
| <30% | C | ☆ | |

阈值来自 `LevelConfig.EvaluateStars(PurityPercent)`（三关都是 100/60/30）。星级是**存档里可比较的量化指标**（`ProgressData.BestStars`），字母评级是**给人看的**——两者并存是用户 2026-10-18 确认的方案。

### 2.5 图鉴：8 张卡 + 单槽位存档

| Id | 标题 | 关 |
|---|---|---|
| `kc_plastic_bottle` | 一个塑料瓶的 450 年 | L1 |
| `kc_plastic_bag` | 塑料袋的海洋漂流记 | L1 |
| `kc_recycle_symbol` | 三角循环标志的秘密 | L1 |
| `kc_forest_oxygen` | 森林是地球的肺 | L2 |
| `kc_food_chain` | 一条溪流的食物链 | L2 |
| `kc_invasive_species` | 外来物种为何危险 | L2 |
| `kc_carbon_energy` | 碳足迹与能源选择 | L3 |
| `kc_renewable` | 可再生能源三兄弟 | L3 |

- 数据源 `Resources/Data/knowledge_cards.json`（`KnowledgeCardCatalog { List<KnowledgeCardData> }`），`CodexService` 静态类统一读。
- 存档槽位 `codex.json` 只存 `List<string> CollectedIds`——**不存正文**，正文永远从 JSON 读，改文案不需要存档迁移。
- `CodexService.CollectCard(id)` 返回 `bool`（首次收集为 true），`CodexPanel` 据此决定是否弹 toast。
- 场景里 7 个 `KnowledgeCardPickup` 实例（L1 2 + L2 3 + L3 2），卡片 Id 由序列化字段指定。

### 2.6 对话系统（14 段 36 条）

| 数据层 | 位置 |
|---|---|
| 对白 | `Resources/Data/dialogues.json` → `DialogueData{ Id, Once, Entries[] }`，Entry `{ Speaker, AvatarTint, Text, EventId }` |
| 知识卡 | `Resources/Data/knowledge_cards.json` |
| 运行时索引 | `DialogueRepository.Find(id)`（静态字典，首次访问时全量加载） |

**播放规则（`DialoguePanel`）**：

| 状态 | 按 E 的行为 |
|---|---|
| 正在逐字（`TypeInterval=0.03s/字`） | 立刻补完整句（`CompleteLineInstant`） |
| 整句已显示 | 下一句（`Advance`） |
| 最后一句已显示 | `Finish()` → `Close()` + 广播 `EventId`（如有） |

- `DialoguePanel.IsDialogueActive` 是**静态**属性（`OnOpen` 置 true / `OnClose` 置 false），供关卡层轮询——`LevelManager` 的落幕对白协程就靠它（06-Level01 §2.5）。
- 说话人头像：无美术图，用 `AvatarTint` 色块 + 名字。Ava 用 `#7CFC98`（全息绿），机器人用工业橙。
- `EventId` 机制**只预留、不接关卡逻辑**（`DialogueEventIdEvent` 当前 0 订阅者，见 §5 U1）。

**14 段对白分布（实测 `dialogues.json`：14 段 / 36 条）**：

| 关 | 段数 | 段 Id（条数） |
|---|---|---|
| L1 | 6 | `ava_intro`(6) / `ava_intro_repeat`(3) / `signboard_plastic`(1) / `signboard_recycle`(1) / `ava_lever_hint`(3) / `ava_lever_hint_repeat`(2) |
| L2 | 3 | `ava_forest_intro`(4) / `ava_forest_intro_repeat`(2) / `signboard_forest`(1) |
| L3 | 5 | `ava_factory_gate`(3) / `ava_factory_gate_repeat`(2) / `robot_factory_intro`(3) / `robot_factory_intro_repeat`(2) / `ava_ending`(3) |

> 命名规律：**首次进入 / 重复进入**成对出现（`_repeat` 后缀），保证玩家二刷不会被同一段长对白拦住。`ava_ending` 是唯一 `Once=true` 的段（第三关落幕，见 06-Level01 §2.5）。

### 2.7 世界空间气泡：像素→世界换算的坑

```text
UIUtility.CreateWorldCanvas(go, parent, localOffset)
  Canvas.renderMode = WorldSpace
  CanvasScaler 完全不生效（RenderMode=WorldSpace 下 scaleFactor 被忽略）
  换算必须落在 transform.localScale = WorldUiScale(0.01)
  RectTransform.sizeDelta 仍按像素填（400×120 → 4×1.2 世界单位）
```

早期把换算写在 `CanvasScaler` 上，400×120 的气泡被当作 **400×120 世界单位**渲染（视野仅 16×9）——一个气泡横跨 25 屏，表现为斜穿画面的巨大白带，**并污染了当时所有运行时截图**，使那一轮的视觉验收结论全部失真。这条写进 CodeStandard 的"已踩坑"清单。

另一个坑：气泡 Canvas **必须在 `Awake` 创建，不能在 `OnEnable`**。`OnEnable` 在编辑器模式下也会跑，会把世界空间 Canvas 建进场景，Scene 视图里显示成一大片白色线框——看起来像"挡住路的隐形墙"（`RescueTarget.cs:44~48` 有原注释）。

### 2.8 伤害飘字与敌人血条

- `DamageNumberLayer` / `EnemyHealthBarLayer` 都订阅 `EnemyDamagedEvent`，各自维护世界空间对象池（飘字上浮淡出 / 血条跟随敌人 `transform`）。
- 不进面板栈：它们是"场景内叠加层"，`BlocksInput=false` 且不需要 Esc 响应。
- 敌人血条在敌人 `Recycle` 后必须隐藏——靠 `EnemyDamagedEvent` 里带的存活态或定时超时兜底（池化对象 `SetActive(false)` 不广播事件，这是 M4 的老坑）。

## 3. 数据流

### 3.1 一次「暂停 → 设置 → 返回 → 继续」

```text
① 玩家按 Esc（关卡 Playing）
   UIManager.Update: Input.GetKeyDown(Escape) → HandleEscape()
     _stack = [HUD]，栈顶 HUD.BlocksInput = false → 不关它
     LevelManager.State == Playing → Open("Pause")

② Open("Pause")
   _panels["Pause"] 存在 → _stack.Contains? 否 → _stack.Add(panel)
   panel.Open() → PausePanel.OnOpen()：Time.timeScale = 0；根节点 SetActive(true)
   ApplyInputLock()：IsBlocking = true → player.InputLocked = true；ExternalInputLock = true
   UIUtility.PlayClickSfx()

③ 玩家点「设置」→ UIManager.Open("Settings") → 入栈成为栈顶
   ApplyInputLock 再次执行（仍 blocking，幂等）

④ 设置里点「返回」→ UIManager.Close("Settings") → 出栈 → ApplyInputLock → 栈顶变回 Pause（仍 blocking）

⑤ 玩家按 Esc（或点「继续」）→ CloseTop() → 关 Pause
   PausePanel.OnClose()：Time.timeScale = 1
   ApplyInputLock：栈顶变回 HUD（BlocksInput=false）→ IsBlocking=false → 两层锁都解除 → 玩家能动

⑥ UI 动画在 timeScale=0 时仍能跑：UIManager.Update 用 Time.unscaledDeltaTime 驱动 UpdatePanel
```

### 3.2 一次 NPC 对话

```text
① 玩家走进 DialogueTrigger 半径
   Update：UIManager.Instance.IsBlocking → true 时直接 return（面板开着不可互动）
   inRange 变化 → 世界空间气泡 SetActive(true)「按 E 交谈」

② Input.GetKeyDown(E) → UIManager.SafeOpen("Dialogue") → 新面板入栈（blocking → 冻输入）
   然后 FindObjectOfType<DialoguePanel>().Play(DialogueRepository.Find(_dialogueId))

③ Play(data)
   _entryIndex = 0；BuildTypedLines(entry.Text)（按 \n 与标点切段，供逐字显示）
   IsDialogueActive = true
   ShowCurrentLine()：设说话人名/头像色块 → 逐字 _charCount += dt/TypeInterval

④ 按键推进（UpdatePanel 内读 E）
   逐字中 → CompleteLineInstant()：_charCount = 全长
   整句完成 → Advance()：_entryIndex++，未越界则 ShowCurrentLine()
   末句完成 → Finish()：Close() → IsDialogueActive = false
             → 若 data.Entries[last].EventId 非空 → Publish(DialogueEventIdEvent{...})

⑤ Close() → UIManager.Close("Dialogue") → 出栈 → ApplyInputLock → 玩家恢复控制
```

**一次性对白**：`DialogueData.Once=true` 时 `DialogueTrigger` 播完自禁用；但**「播过了」不写进存档**（`Once` 只在本次运行内有效）——因为教学对白重播无害，而存档迁移成本高（§5 U2）。

### 3.3 一次知识卡拾取 → 图鉴点亮

```text
① KnowledgeCardPickup.OnTriggerEnter2D(other)
     other 是玩家层 → _collected 已 true 则 return（幂等）

② CodexService.CollectCard(_cardId)
     Progress.CollectedIds 已含 → return false（不重复弹 toast）
     否则 Add → SaveProgress()（写 codex.json）→ return true

③ 拾取表现
     _sprite 淡出 / 物件 SetActive(false)（一次性，永久消失）
     AudioManager.PlaySfx(SfxId.KnowledgeCard)
     toast：HUDController 显示「获得知识卡：<标题>」

④ 玩家打开图鉴（主菜单「环保图鉴」或暂停菜单）
     CodexPanel.OnOpen() → CodexService.Cards 全量 8 张
       已收集 → 显示标题 + 正文；未收集 → 显示「？？？」灰条
```

### 3.4 一次敌人受击 → 飘字 + 血条

```text
EnemyControllerBase.TakeDamage(info)
 → EventCenter.Publish(EnemyDamagedEvent{ Enemy, Amount, IsCrit, Position })
 → DamageNumberLayer.OnEnemyDamaged：从池取一个 Popup → 定位到世界点 → 上浮淡出（unscaledTime）
 → EnemyHealthBarLayer.OnEnemyDamaged：找到/创建该敌人的血条 → 更新 fill → 2s 无受击自动隐藏
 → （L3）BossHealthBar：Boss 走 BossPhaseChangedEvent 与 HpPercent 轮询，不走这条
```

## 4. 设计取舍

| 取舍项 | 选择 | 放弃的方案与理由 |
|---|---|---|
| UI 构建方式 | 程序化 uGUI（`UIUtility` 工厂） | 放弃序列化 Prefab/图集：无 UI 美术资源，程序化省掉 20+ 个 `.meta` 与手工锚点，且样式集中在一处；代价是改样式要改代码 |
| 输入冻结位置 | `UIManager.ApplyInputLock` 一处 + 双层锁 | 放弃"各面板自己设"：面板叠加时互相覆盖，暂停中开设置再关会导致输入错乱 |
| HUD 刷新 | 能用轮询就轮询（HP/净化剂/蓄力） | 放弃"给 M3 加事件"：改动已验收系统风险 > 每帧 5 次 int 比较 |
| 评级体系 | 字母 S/A/B/C **与** 星级并存 | 放弃二选一：星级要存档比较，字母要给人看 |
| 图鉴存档 | 只存 Id 列表 | 放弃"存正文快照"：改文案就要做存档迁移，毫无必要 |
| 对话推进 | 同一颗 E 键三态（补句/下一句/关闭） | 放弃"空格继续 + Esc 关闭"双键：单手可玩，与救援/拾取统一为 E |
| 世界空间气泡 | 每处自建 Canvas + `localScale=0.01` | 放弃 `CanvasScaler`：`WorldSpace` 下它完全不生效（踩过，见 §2.7） |
| 字体 | 仓库自带 `NotoSansSC-Regular`（OFL 1.1） | 放弃内置 `LegacyRuntime.ttf`：无 CJK 字形，中文全渲染成方块 |
| 面板与场景 | `UIManager` 只在 Title 一份 | 放弃"每场景挂一个"：`DontDestroyOnLoad` 会留下重复实例并覆盖注册表 |

## 5. 风险与架构债

| # | 风险 / 债 | 概率 | 影响 | 现状与对策 |
|---|---|---|---|---|
| U1 | `DialogueEventIdEvent` **0 订阅者** | 已存在 | 低（预留机制） | 对白的 `EventId` 字段能填、能发，但无人接。将来做"对话触发机关"时才有消费方；当前是诚实的"预留" |
| U2 | `Once` 对白不写档，重进关卡会重播 | 已存在 | 低 | 教学对白重播无害；若要做"只播一次"需扩 `progress.json` |
| U3 | 8 个面板全部程序化构建，首次进入场景有构建开销 | 中 | 低（首次约 1~2 帧） | 面板在 `Awake` 构建、默认隐藏；实测 Title 首帧无卡顿。若面板增至 15+ 应改 Prefab |
| U4 | `EnemyHealthBarLayer` 依赖敌人 `Recycle` 后隐藏自己 | 中 | 中（残留血条浮空） | 池化 `SetActive(false)` 不广播事件——用 2s 超时兜底隐藏 |
| U5 | 长文本溢出（提示牌正文/知识卡） | 中 | 低 | `Text` 统一 `Truncate` + 字号下限 20 + 提示牌分页（每页 80 字） |
| U6 | `LevelResultUI` 死代码若被重新挂载会与 `ResultPanel` 抢事件 | 中 | 中 | 见 06-Level01 §5 L1 |
| U7 | SettingsPanel 的音量/画质是否与 `AudioManager` 真正联动 | 待查 | 中 | 设置项写 PlayerPrefs，`AudioManager` 启动时读取；未做"实时生效"验证，登记为待办 |

## 6. 与 requirements 的偏差说明

| 编号 | requirements 原文口径 | 实际实现 | 原因 |
|---|---|---|---|
| D1 | R7 "`LevelResultUI` 标记 `[Obsolete]` 并由 `LevelSetup` 停止挂载" | **未加 `[Obsolete]` 特性**；`LevelSetup.cs:257` **仍在 AddComponent**；仅 `LevelUISetup` 工具会移除场景实例 | 当时的改动只做了"迁移订阅"，没做"下线旧类"；三关场景当前无实例故无表现问题，属隐性债 |
| D2 | 面板 7 个 | 8 个（`Settings` 当时未单列） | 设置页从暂停菜单内嵌改为独立面板 |
| D3 | 对白 8 段 | 14 段 36 条（M10 叙事补白：+L2 4 段/ L3 3 段/ `ava_ending`） | 见 `NarrativeDesign.md` §5 |
| D4 | R9 `DialogueEventIdEvent` 用于驱动关卡逻辑 | 只发布、无订阅者 | 预留给"对话触发机关"，本期无此需求 |
| D5 | 知识卡"同关 2 张" | L1 2 / L2 3 / L3 2 共 7 个实例，目录 8 张（`kc_recycle_symbol` 未摆放） | L2 叙事补白时加了一张；目录 8 张有一张未放置，属可接受冗余 |
| D6 | （未提） | 新增 `DamageNumberLayer` / `EnemyHealthBarLayer` / `BossHealthBar` 三个世界空间叠加层 | M8 战斗可读性需求（无血条时玩家不知道敌人还剩多少血） |
