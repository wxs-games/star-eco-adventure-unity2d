# 任务清单：07-UIDialogue

> 对应 `requirements.md` R1~R10 / `design.md`。共 24 个任务，按依赖顺序排列。
> 状态标记：`[x]` 已完成且验证通过 / `[ ]` 未完成

## 一、UI 框架（R2）

- [x] T1 `UI/UIUtility.cs`：Canvas/Scaler/EventSystem 保活、内置字体、颜色常量、Image/Text/Button 工厂、锚点辅助。验收：LevelResultUI 同类公共代码抽离后行为不变。
- [x] T2 `UI/UIPanel.cs`：抽象基类 `PanelName` + `BlocksInput` + `OnOpen/OnClose/OnUpdate`。验收：编译通过，派生类只需 override。
- [x] T3 `UI/UIManager.cs`：MonoSingleton；Register/Open/Close/Toggle/CloseTop；Esc 路由（栈顶优先、栈空+Playing→暂停）；输入冻结收口；UI 音效占位。验收：连续开关多面板栈序正确。

## 二、HUD（R1）

- [x] T4 `UI/HUDController.cs`：心形×5、净化剂条、净化值进度条+百分比、目标提示、蓄力条；订阅 `PurityChangedEvent`/`LevelObjectiveChangedEvent`/`ObjectiveCompletedEvent`；终态隐藏；无数据源时禁用不报错。验收：Play 中四项数据与实际一致。

## 三、设置/关卡选择（R5、R6）

- [x] T5 `UI/SettingsPanel.cs`：BGM/SFX 滑条实时调 `AudioManager`；画质三档 `QualitySettings`；打开时回填、关闭时写 `settings` 槽位。验收：改音量立即生效，重开菜单后保持。
- [x] T6 `UI/LevelSelectPanel.cs`：3 关卡卡片（名/简介/锁/最佳星）；`UnlockedLevelIndex` 控制可玩；锁卡点击只播音效；可玩仅 L1 可加载。验收：锁卡不可进，L1 可进。

## 四、主菜单与暂停（R3、R4）

- [x] T7 `UI/MainMenuPanel.cs`：标题+6 按钮（开始/继续/关卡选择/图鉴/设置/退出）；继续按钮按 `HasProgress` 置灰；开始=清进度+Load L1；退出=Application.Quit。验收：Title 场景全按钮流转正确。
- [x] T8 `UI/PausePanel.cs`：Esc 打开（栈空时）、timeScale=0、4 按钮（继续/重开/设置/回主菜单）；从暂停开设置保持暂停；重开走 `RestartLevelEvent`。验收：暂停期间游戏静止，恢复后继续。

## 五、结算统一（R7）

- [x] T9 `UI/ResultPanel.cs`：订阅完成/失败事件；标题/关卡名/净化度/用时/击杀/拾取；S/A/B/C + ★★ 并存评级；失败原因；下一关按钮按 `UnlockedLevelIndex` 显隐；重开/回主菜单。验收：净化 100% 显示 S/★★★。
- [x] T10 `LevelResultUI.cs` 标记 `[Obsolete]`，LevelSetup 停止挂载它（M6 历史保留）。

## 六、图鉴（R8）

- [x] T11 `Resources/Data/knowledge_cards.json`：8 条环保知识卡片文案（design §2.5 表）。
- [x] T12 `UI/CodexService.cs`：`CodexProgress` DTO + 读写（槽位 `codex.json`）+ `CollectCard(id)` 幂等 + `IsCollected(id)`。
- [x] T13 `UI/CodexPanel.cs`：网格 8 卡（标题/正文/锁定态"???"）；打开时刷新收集状态。验收：未收集暗、已收集亮。
- [x] T14 `Eco/KnowledgeCardPickup.cs`：Trigger 拾取、toast"获得知识卡片：{Title}"、写档、SetActive(false)、幂等。验收：碰一次入档，再碰无变化。

## 七、对话（R9）

- [x] T15 `Resources/Data/dialogues.json`：艾娃教学对话 1 组（≥6 句）+ 提示牌×2 文案。
- [x] T16 `UI/DialogueData.cs`：`DialogueData/DialogueEntry` [Serializable] + 静态 Load。
- [x] T17 `Events/DialogueEventIdEvent.cs`：对话结束事件 struct（携带 EventId）。
- [x] T18 `UI/DialoguePanel.cs`：逐字显示（0.03s/字）、说话人名、头像占位、E 快进/推进/收尾、结束后广播 EventId。
- [x] T19 `UI/DialogueTrigger.cs`：进入范围显示"按 E 交谈"气泡；按 E 打开对话；复用 M2 交互输入。
- [x] T20 `UI/SignboardTrigger.cs`：阅读界面分页（80 字/页）、E 翻页、末页关闭。

## 八、场景与集成（R1~R9）

- [x] T21 `Editor/TitleSetup.cs`：生成 `Title.unity`（UIManager+MainMenu+BGM+标题字），注册 Build Settings index 0；生成对话/卡片 JSON 资产放置校验。
- [x] T22 `Editor/LevelSetup.cs` 扩展：Level01 挂 UIManager/HUD/Pause 挂载/ResultPanel（替代 LevelResultUI）/艾娃 NPC 触发器×1/提示牌×2/知识卡片×2；`SprayController.ChargeRatio` 接线验证。

## 九、编译与运行验证

- [x] T23 Unity 编译零错误 + Play 验证：HUD 四项、Title 全流转、Esc 暂停、对话逐字/快进、提示牌翻页、卡片拾取+图鉴亮起、结算 S/A/B/C 与星级、下一关按钮隐藏逻辑。验收：逐条通过。
- [x] T24 `tasks.md` 状态行更新 + 需求对齐报告（AGENTS.md §1.3）+ git commit：`M7: UI/dialogue — HUD/UIManager/title/pause/settings/level-select/codex/dialogue`。

> 状态：已完成（2026-10-18，Play 实测：HUD 四项数据正确、暂停/设置栈序与 timeScale 正确、对话逐字+输入锁、卡片拾取精确入图鉴 1/8、结算 S 与 ★★★ 并存、Title→Level01 跨场景单例常驻无错误）。共 24 个任务，覆盖 R1~R9 全部 EARS。
> 对 M1~M6 边界：仅 4 处最小增量（`SprayController.ChargeRatio` 只读属性、`LevelResultUI` 标记废弃、`LevelSaveService` 图鉴槽位、`LevelSetup` 扩展），design §2.7 已论证必要性。
