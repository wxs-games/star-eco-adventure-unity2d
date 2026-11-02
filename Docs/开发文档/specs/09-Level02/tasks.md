# 任务清单：09-Level02

> 对应 `requirements.md` R1~R7 / `design.md`。共 20 个任务，按依赖顺序排列。
> 状态标记：`[x]` 已完成且验证通过 / `[ ]` 未完成

## 一、spec 三件套（先行）

- [x] T1 `specs/09-Level02/requirements.md`：EARS 全覆盖 R1~R7（含用户三项决策记录）。
- [x] T2 `specs/09-Level02/design.md`：动线结构、E 键/水流解谜取舍、目标链最小增量、边界表。
- [x] T3 `specs/09-Level02/tasks.md`：本清单。

## 二、目标类型与事件（R2/R3 基础设施）

- [x] T4 `Level/LevelConfig.cs`：`LevelObjectiveType` 加 `RescueTargets=4`、`PuzzleSolved=5`，注释同步。
- [x] T5 `Level/Events/AnimalRescuedEvent.cs`：struct（`TargetId`/`Position`）。
- [x] T6 `Level/Events/PuzzleSolvedEvent.cs`：struct（`ElapsedInPuzzle`/`Position`，FX/音效观察者用，不参与计数）。
- [x] T7 `Level/WaterBlockage.cs`：`IPurifiable` 适配器（仿 `TowerPurifiable`），TargetId=`WaterBlockN`；净化完成照旧广播 `GarbagePurifiedEvent`。

## 三、E 键解救互动（R2）

- [x] T8 `Level/RescueTarget.cs`：半径检测（复用 DialogueTrigger 气泡模式）+ E 键触发 + 玩家 `Interact` Trigger（PlayerAnimatorDriver，空值保护）+ 0.3s 后开笼（笼体与动物隐藏/销毁）+ 广播 `AnimalRescuedEvent` + 幂等（不可重复触发）。验收：进入半径出气泡、E 后笼消失且 HUD 计数 +1。**Play 实测通过**（进度 0/2→1/2→2/2，链推进）。
- [x] T9 `LevelObjectiveTracker` 增量：订阅 `AnimalRescuedEvent`；`_rescuedAnimals` 计数 + `CurrentCountFor` case + `Initialize/ResetChain` 重置。验收：解救 2 只后目标链自动推进。**Play 实测通过**。

## 四、水流解谜（R3）

- [x] T10 `Level/WaterPuzzleController.cs`：持有堵塞点列表 + 水位视觉 + 木筏通路组引用；监听 `GarbagePurifiedEvent` 统计 `WaterBlock` 前缀；收齐 → 水位 1.5s 上升 → 激活通路 → 广播 `PuzzleSolvedEvent`；未收齐时通路保持禁用。验收：清 3 处后水位上升且通路可走。**Play 实测通过**（水位 -1.6→0.05，RaftPath 激活）。
- [x] T11 `LevelObjectiveTracker` 增量：`OnGarbagePurified` 加 `WaterBlock` 前缀分支（`_waterCleared++`，不进 `_purifiedTargets`）；`CurrentCountFor` 加 `PuzzleSolved` case；`ClampRequired` 加 `WaterBlockage` 扫描夹取。验收：HUD 显示"清理堵塞物 x/3"。**Play 实测通过**。

## 五、Level02 配置与场景（R4/R5）

- [x] T12 `Editor/Level02Setup.cs`（菜单 `StarEco/Setup/Level02`）：BuildConfig 生成 `Level02.asset`（LevelIndex=2、四段目标链、Enemy02 刷怪表 2 条、星级 100/60/30、出生点 (-26,1.5)、IntroText）。
- [x] T13 Level02Setup 场景：左区（出生点 + 陷阱笼×2 + CP_1）→ 中游河道（水域 + 堵塞点×3 + 木筏通路组初始禁用）→ 右区（污染源×3 + 刷怪锚点×2 + CP_2）；地面 64u + 跳跃平台若干。
- [x] T14 Level02Setup 系统挂载：EcoSystems / LevelSystems（LevelManager 挂非根节点）/ 根节点 UIManager / M7 全套面板；加入 Build Settings（index 2）。验收：Play 直接可玩。**实测通过**（Build Settings 定序 0:Title|1:Level01|2:Level02|3:Level03，移除占位 SampleScene）。
- [x] T15 Level02Setup 注入：config 注入 Manager/Tracker/Spawner；WaterPuzzleController 挂水域根节点并回填堵塞点/水位/通路引用；RescueTarget 挂笼子并设 TargetId。**execute_code 全量核对通过**。

## 六、全流程与解锁（R6）

- [x] T16 `UI/LevelSelectPanel.cs`：第二关卡片 `BuildIndex` -1 → 2，简介文案更新为"E 键解救动物、喷雾清堵疏通河道、木筏渡河"。
- [x] T17 `UI/MainMenuPanel.cs`："继续游戏"目标关钳制 `Mathf.Clamp(progress.UnlockedLevelIndex, 1, 1)` → `(1, 3)`（三关均在 Build Settings）。
- [x] T18 `UI/ResultPanel.cs`：验证"下一关"对 index 2 生效（无需改逻辑则记录结论）。**结论**：原硬编码 -1 不适用，已改 `next <= 3 ? next : -1`；Play 实测 NextButton 显示且可点，存档 `UnlockedLevelIndex` 递增。

## 七、编译、验证与提交（R7）

- [x] T19 Unity 编译零错误 + Play 分项验证：E 解救（气泡→开笼→计数）、水流解谜（清堵→水位升→通路）、四段目标链→结算、解锁写入；回归 Level01/Level03。**全部通过**：
  - Level02 全链路通关：结算面板"第二关：生态失衡的森林 / 净化度 100% / 评级 S ★★★"，存档 cleared=True stars=3。
  - Level01/Level03 加载回归：Tracker idx=0 链未完成、LevelManager/UIManager 在位，控制台无新增错误（仅 SaveSystem 损坏 JSON 自检日志）。
  - 追加修复：`LevelObjectiveTracker.Evaluate` 停滞 bug（单次 if → while 循环）；定向回归：提前净化 3 污染源后，链在清堵完成时自动跳过已满足目标直达 idx=3（旧代码会停滞）。
- [x] T20 更新 `docs/TDD.md` M9 状态 + `tasks.md` 状态行 + 需求对齐报告 + git commit `M9: level02 — enemy02/rescue/water-puzzle + full flow`。

> 状态：**已完成**（2026-10-18）。用户三项决策：E 键解救 / 清堵塞→水位升→通路 / 通关即解锁下一关。
> 对 M1~M8 边界：目标链只加枚举与计数分支（算法不动）；UI 三处索引/文案对齐；`LevelSaveService` 解锁机制零改动（M6 已实现语义）。
> 已知特性（非 bug，design.md 有记录）：`DrownHazard` 对敌人生效——引敌入水可溺亡计入击杀，属 emergent 战术；出生点 (-26,2) 与 CP_1 触发区仅约 0.1u 擦边，已通关路径不触发，建议后续关卡迭代时下移出生点或左移 CP_1。
