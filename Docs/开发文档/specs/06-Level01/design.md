# 技术方案（SDD）：06-Level01（关卡流程框架 + 第一关）

> 版本：v2.0（2026-10-30 按落地代码重写） | 对应 `requirements.md` R1~R8 | 命名空间 `StarEco.Level` | 代码落点 `Assets/Scripts/Level/`
> 前置已就位：M1 `MonoSingleton`/`EventCenter`/`PoolManager`/`SaveSystem`、M2 `PlayerController`（`InputLocked`/`ExternalInputLock`/`IsDead`/`DiedFromFall`/`Body`/`Sprite`）、M3 `PlayerCombat`（`CurrentHP`/`MaxHP`）、M4 `EnemyControllerBase`（`MarkPooled`/`ResetForReuse`）与 `EnemyDeathEvent`/`TowerDisableEvent`、M5 `PuritySystem`/`IPurifiable`/`GarbagePurifiedEvent`/`PurityChangedEvent`/`LevelPurifiedEvent`、M7 `UIManager`/`ResultPanel`/`DialoguePanel`
> 立项决策（已定稿）：对 M1~M5 **零源码改动**；关卡内容由 `LevelConfig` ScriptableObject 数据驱动；**检查点系统已整体移除**，阵亡即失败、重开靠重载本关；完成判据 = 目标链全完成 **或** `LevelPurifiedEvent`（100%），先到者为准
>
> **v2.0 修正的四处失实（v1.0 写在 M6 阶段，之后被 M7/M9/M10 覆盖）**
> ① 结算面板不是 `LevelResultUI` 而是 `UI/ResultPanel`（`LevelResultUI.cs` 已降级为死代码，见 §6 偏差 D1）
> ② 目标链**不是**"未解锁目标不计数"，而是**无条件累计 + `while` 循环推进**（§2.2、§6 D2）
> ③ 目标类型不是 4 种而是 6 种（新增 `RescueTargets`/`PuzzleSolved`，§2.2）
> ④ 通关不再直接弹结算：第三关有落幕对白拦截（§2.5）

## 1. 结构

### 1.1 与上下游的边界：复用 / 新建 / 为何不重构

| 边界项 | 处理 | 依据 |
|---|---|---|
| M1 `MonoSingleton` | **复用为 `LevelManager` 基类** | 关卡流程天然全局单例；但 `LevelManager` 挂在场景子节点上，**不** `DontDestroyOnLoad`，随场景销毁（`RestartLevel` 依赖这一点） |
| M1 `EventCenter` | **复用，零改动** | 关卡事件走同一条类型化发布订阅，零 GC struct 风格 |
| M1 `PoolManager` | **复用，敌人走池** | `EnemyControllerBase.Recycle` 已实现"池借出走 `Release`、场景实例走 `SetActive(false)`"双路径 |
| M1 `SaveSystem` | **复用，零改动** | 泛型 `Save<T>/Load<T>` + `Exists`；`LevelSaveService` 只做 DTO 与槽位命名 |
| M2 `PlayerController` | **只调用**：`InputLocked` / `ExternalInputLock` / `IsDead` / `DiedFromFall` | 冻结输入、判失败、区分死因；**不**为关卡流程给玩家 FSM 加状态（YAGNI） |
| M3 `PlayerCombat` | **只读** `CurrentHP`/`MaxHP` | 结算统计 |
| M4 `EnemyControllerBase` | **只调用** `MarkPooled()`/`ResetForReuse()`/`Config.EnemyTypeId` | 刷怪 |
| M4 `EnemyDeathEvent` / `TowerDisableEvent` | **订阅** | 击杀统计、刷怪名额释放、塔目标计数 |
| M5 `PuritySystem` | **只读** `Current`/`Total`/`Percent`；订阅 `LevelPurifiedEvent`/`PurityChangedEvent` | 完成判据、刷怪速率 |
| M5 `IPurifiable` 分类计数 | **运行时统计**（不改 `PuritySystem`） | `PuritySystem.Total` 只有总量没有分类，`LevelObjectiveTracker` 自建 6 个计数器 |
| M7 `UIManager` / `ResultPanel` | **被调用**（`ResultPanel` 订阅关卡事件自行弹面板） | 关卡层不 `new` UI，只广播事件 |
| M7 `DialoguePanel` | **被调用**（仅第三关落幕对白，§2.5） | 收尾文本要有地方显示 |

> **一句话边界**：`StarEco.Level` 是流程层，**只读**上游状态、**只发**事件；一切画面由 `StarEco.UI` 订阅后呈现。关卡层从不持有任何 UI 引用。

### 1.2 文件结构（`Assets/Scripts/Level/`，29 个 .cs）

```text
Level/
  LevelConfig.cs              SO：LevelIndex/LevelName/Objectives[]/星级阈值/SpawnEntries[]/TimeLimit
  LevelState.cs               四态枚举 + IsFinal() 扩展
  LevelManager.cs             单例：状态机 / 依赖解析 / 终态 / RestartLevel / 落幕对白拦截
  LevelObjectiveTracker.cs    目标链：6 类计数 / 夹取 Required / while 推进 / 目本文案广播
  LevelSpawner.cs             污染度驱动刷怪（锚点循环 + 名额上限 + 池）
  LevelStats.cs               击杀 / 拾取 / 用时统计（纯被动订阅）
  LevelResultData.cs          [Serializable] LevelResultData + ProgressData
  LevelSaveService.cs         SaveSystem 薄封装：result / progress / codex 三槽位
  LevelResultUI.cs            ⚠️ 旧版结算面板，已被 UI/ResultPanel 取代（死代码，见 §6 D1）
  CameraFollow2D.cs           跟随 + 边界夹取（关卡内的相机策略）
  ── 机关 ──
  CrumblePlatform.cs          碎裂平台（踩上 → 抖动 → 塌落 → 复位）
  MovingPlatform.cs           移动平台（路点往返 + 载人）
  LeverSwitch.cs              拉杆开关（一次性/可复用两种模式）
  WaterPuzzleController.cs    水流解谜：清堵塞 → 水位升 → 木筏通路激活
  RescueTarget.cs             被困动物笼：E 键解救 + 0.3s 开笼 + 事件
  ── 危险物 ──
  SpikeHazard.cs / FallHazard.cs / DrownHazard.cs / VentHazard.cs   四种致死或致伤区域
  ConveyorZone.cs             传送带（额外水平速度叠加）
  ── 事件（Events/）──
  LevelCompletedEvent / LevelFailedEvent / LevelStateChangedEvent
  ObjectiveCompletedEvent / LevelObjectiveChangedEvent
  RestartLevelEvent / ReturnToTitleEvent / AnimalRescuedEvent / PuzzleSolvedEvent
```

### 1.3 三关配置实测（`Assets/Settings/Levels/*.asset`）

| 关卡 | 目标链（按序解锁） | 星级阈值 | 实测备注 |
|---|---|---|---|
| **Level01** | ① `PurifyTargets` 3「清理散落的垃圾堆」→ ② `ReachPurity` 100「把关卡净化值推到 100%」 | 3★=100% / 2★=60% / 1★=30% | ① 完成后 ② 通常已接近满足，靠 `while` 一次推完 |
| **Level02** | ① `RescueTargets` 2「解救被困动物」→ ② `PuzzleSolved` 3「清理堵塞物，疏通河道」→ ③ `PurifyTargets` 3「净化森林污染源」→ ④ `DefeatEnemies` 4「击败 4 只变异怪物」 | 同上 | 四个目标覆盖四类事件，是全项目唯一"全类型"关卡 |
| **Level03** | ① `PurifyTargets` 4「清理工厂污染源（含封锁墙）」→ ② `DefeatEnemies` 1「击败工厂污染核心 Boss01」 | 同上 | 无 `ReachPurity`：打完 Boss 即通关（§6 D3） |

> 三关配置里 `PurifyTowers` 类型**一次都没用**（塔在 L1 是战斗单位、可被攻击击毁），代码里仍有该类型的计数与夹取分支——属"预留但未使用"，登记在 §5 R4。

## 2. 关键机制

### 2.1 关卡状态机（R2）

```text
NotStarted --StartLevel()------------------------------> Playing
Playing --LevelPurifiedEvent(100%) / 目标链全完成-------> Completed  (终态)
Playing --PlayerController.IsDead-----------------------> Failed     (终态)
Completed / Failed --RestartLevelEvent------------------> 重载场景 → NotStarted → Playing
```

- 迁移唯一入口 `SetState(next)`：同态直接 return（幂等），否则发布 `LevelStateChangedEvent{OldState,NewState}`。
- `Finished` 有独立 `_finished` 布尔守卫，与 `State` 无关——因为第三关落幕对白期间 `State` 还是 `Playing`，此时若再触发 `FinishLevel` 会重入。
- `RestartLevel()`：`enabled=false` → `gameObject.SetActive(false)` → `SceneManager.LoadScene(scene.buildIndex)`。
  **为什么必须先禁用自己**：`LoadScene` 是延迟到帧末的，不禁用的话本帧后续 `Update` 还会再判一次 `IsDead`，把刚置的失败再播一遍。

### 2.2 目标链（R3）

**6 类计数器**（`LevelObjectiveTracker.cs:31~35`）：

| 计数器 | 事件来源 | `LevelObjectiveType` |
|---|---|---|
| `_purifiedTargets` | `GarbagePurifiedEvent`，`TargetId` **不以** `Tower`/`WaterBlock` 开头 | `PurifyTargets` |
| `_purifiedTowers` | ① `GarbagePurifiedEvent` 且 `TargetId` 以 `Tower` 开头；② `TowerDisableEvent` | `PurifyTowers` |
| `_waterCleared` | `GarbagePurifiedEvent` 且 `TargetId` 以 `WaterBlock` 开头 | `PuzzleSolved` |
| `_defeatedEnemies` | `EnemyDeathEvent` | `DefeatEnemies` |
| `_rescuedAnimals` | `AnimalRescuedEvent` | `RescueTargets` |
| （实时读） | `PuritySystem.Percent × 100` 取整 | `ReachPurity` |

**推进算法（这是 v1.0 写错的地方）**：

```text
任意计数事件 → 对应计数器 +1 → Evaluate()

Evaluate():
  if (!_initialized || ChainCompleted) return;
  while (!ChainCompleted) {
      obj = CurrentObjective;
      if (obj == null) { CompleteChain(); return; }
      if (当前计数 < 当前 Required) { BroadcastCurrent(); return; }
      Publish(ObjectiveCompletedEvent{Index, Label});
      _index++;
      if (_index >= Objectives.Length) { CompleteChain(); return; }
  }
```

- **计数无条件累加**：玩家提前净化/提前解救，事件一到就计入自己的桶，不会丢。
- **用 `while` 而不是 `if`**：一次事件可能导致**连续多个**目标同时满足（例：L1 清完第 3 个垃圾后，100% 净化值目标往往同时达标）。写成 `if` 会让链停在那儿，直到下一次同类事件才恢复——M9 实测到过这个卡关。
- **`ClampRequired()` 防死锁**：初始化时扫描场景，把 `PurifyTargets`/`PurifyTowers`/`PuzzleSolved` 的 `Required` 夹到场景实际可满足的数量（`LevelObjectiveTracker.cs:274~337`），并 `Debug.LogWarning` 记录。`LevelConfig` 资产本身不被修改（避免编辑器里资产被标脏持久化）。
- `TargetId` 前缀是**弱约定**（字符串匹配）。代价已接受：换强类型要动 M5 的 `IPurifiable` 接口，而 M5 已验收。

### 2.3 死亡即失败（R4 检查点已移除）

- `LevelManager.Update` 每帧：`if (State == Playing && _player.IsDead) FinishLevel(false, 死因)`。
- **死因区分**：`_player.DiedFromFall ? "跌落山崖（掉出了关卡地面）" : "玩家被击败"`。不区分的话，掉出地图的玩家会看到"被击败"，误以为是怪打死的。
- 失败终态：停刷怪 → `InputLocked=true` → 生成 `LevelResultData`（`Cleared=false`、`Stars=0`）→ `LevelSaveService.CommitResult` → 播 `SfxId.LevelFail` → 发 `LevelFailedEvent{Reason}`。
- 没有检查点、没有原地复活、没有关卡快照。重开 = 重载场景。

### 2.4 污染度驱动刷怪（R5）

- `LevelSpawner` 每条 `SpawnEntry` 独立计时；间隔 `BaseInterval × Lerp(1.5, 0.5, pollution)` —— 污染越重刷得越快，与"星球在恶化"的叙事同向。
- 名额 `_aliveCount[i] < MaxAlive` 才生成；锚点循环取 `Anchors[i % n]` 实现多方向来敌。
- 生成：`PoolManager.Get(prefab)` → `MarkPooled()` → 摆锚点 → `SetTarget(player)`。
- 回收计数：`EnemyDeathEvent` 按 `EnemyTypeId` 匹配条目 → `_aliveCount--`。
- **Boss 不受刷怪表管理**（场景实例，非池借出），所以 L3 的 `DefeatEnemies(1)` 目标只可能由 Boss 满足。

### 2.5 落幕对白拦截结算（2026-10-30 新增，v1.0 不存在）

```text
FinishLevel(success=true)
  ├─ _finished = true；停刷怪；InputLocked = true
  ├─ if (success && TryPlayEndingDialogue())
  │     → StartCoroutine(CommitFinishAfterEnding(reason))   并 return（不写档！）
  └─ else CommitFinish(success, reason)

TryPlayEndingDialogue() 四道闸门：
  ① _endingPlayed 已播过 → false
  ② _config.LevelIndex != 3 → false          （台词讲三颗星球数据互通，放前两关语义不通）
  ③ DialoguePanel.IsDialogueActive → false    （已有对话在播就不抢，例如玩家正站在艾娃面前打完 Boss）
  ④ DialogueRepository.Find("ava_ending") == null → false + Warning

CommitFinishAfterEnding(reason):
  yield return null;                       // SafeOpen 当帧只入栈，等一帧让 OnOpen 生效
  while (DialoguePanel.IsDialogueActive) yield return null;
  yield return null;                       // ← 关键：最后一句是玩家按 E 关的，不等这帧结算面板会被同一个 E 立刻按掉
  CommitFinish(true, reason);
```

- **为什么用轮询而不是回调**：`UIPanel` 栈没有"关闭回调"接口；轮询 `DialoguePanel.IsDialogueActive`（静态属性）对 Esc 中断也成立。
- **为什么写在 `LevelManager` 而不是 `BossController`**：Boss 只负责"我死了"，"死了之后流程怎么走"是关卡层的职责。Boss 侧已有一段 5.5s 的击败演出（见 08-Boss §3.1），两段不重叠。

### 2.6 存档（R6）

| 槽位 | DTO | 写入时机 | 读取方 |
|---|---|---|---|
| `level01_result.json` | `LevelResultData`（关卡号/是否通关/净化率/用时/击杀/拾取/星级/UTC ticks） | `CommitResult` | `ResultPanel` |
| `progress.json` | `ProgressData`（`UnlockedLevelIndex` / `BestStars` / `LevelCleared`） | `CommitResult`（通关时解锁下一关） | `MainMenuPanel` 的"继续游戏"、`LevelSelectPanel` |
| `codex.json` | `CodexProgress`（`List<string> CollectedIds`） | `CodexService.SaveProgress()` | `CodexPanel` |

- 全部 `[Serializable]` + `JsonUtility` 可用类型。**`JsonUtility` 不支持字典与嵌套泛型**，故 `ProgressData` 用平铺 `int`。
- 失败局面也写 `level01_result.json`（`Cleared=false`），但**不解锁**下一关、不刷新 `BestStars`。
- ⚠️ 槽位名 `level01_result` 是**三关共用一个文件**（不是每关一份），后写的覆盖前写的。这是 M6 的单关遗留，见 §5 R2。

## 3. 数据流

### 3.1 一次关卡启动

```text
SceneManager.LoadScene(Level01) 完成
 → LevelManager.Start()                                        (LevelManager.cs:59)
      ResolveDependencies()：_player/_purity/_stats/_tracker/_spawner 各自 FindObjectOfType（已在 Inspector 挂的则跳过）
      _config == null → Resources.Load<LevelConfig>("Level01Config")（兜底；实际走场景序列化引用）
      !HasRequiredDependencies() → LogError + SetState(Failed) + return
 → StartLevel()                                                (LevelManager.cs:114)
      _finished = false
      _stats.Reset()                                           ← 计时归零
      _tracker.Initialize(_config, _purity, _stats, OnObjectiveChainCompleted)
          → ClampRequired() 扫描场景夹取 Required
          → BroadcastCurrent() → Publish(LevelObjectiveChangedEvent)  ← 该事件当前 0 订阅者（HUD 目标栏 2026-10-30 已移除），广播仅作调试/将来恢复用
      _player.InputLocked = false
      SetState(Playing) → Publish(LevelStateChangedEvent)
      AudioManager.PlayBgmForLevel(_config.LevelIndex)          ← M10
      _spawner.Begin(_config, _tracker)

各 Update 首帧：
  LevelManager.Update → State != Playing 则 return（此时已 Playing，进入死亡检查）
  LevelObjectiveTracker.Evaluate 会在首个计数事件到来时才推进
  HUDController 只轮询 HP / 净化剂 / 蓄力三项（目标与净化值面板已移除）
```

**启动顺序的坑（历史）**：`BroadcastCurrent()` 发生在 `StartLevel` 里，早于其它 `Start()`。当初 HUD 有目标栏时，若只靠事件初始化就会永远显示空目标——所以 `LevelObjectiveTracker` 暴露了 `CurrentProgress`/`CurrentRequired` 供「主动拉一次」。**2026-10-30 目标栏移除后这两个属性已无消费方**，但保留（恢复面板或调试用）。

### 3.2 一次目标推进（以 L2 解救第 2 只动物为例）

```text
玩家走进 RescueTarget 半径 2.2u
 → RescueTarget.Update：UIManager.IsBlocking 则直接 return（面板打开时不可互动）
      inRange 变化 → 世界空间气泡 Canvas SetActive(true)「按 E 解救」
 → Input.GetKeyDown(E) → StartRescue()
      _opening=true；_openTimer = 0.3s；隐藏气泡
      pc.TryFireAnim(PlayerAnimParams.Interact)               ← 只播动画，不改玩家状态机
 → 0.3s 后 CompleteRescue()
      _cageVisual.SetActive(false)                            ← 笼体+动物一起消失
      AudioManager.PlaySfx(SfxId.Rescue)
      Publish(AnimalRescuedEvent{TargetId, Position})
 → LevelObjectiveTracker.OnAnimalRescued → _rescuedAnimals++ → Evaluate()
      2 >= Required(2) → Publish(ObjectiveCompletedEvent{0,"解救被困动物"})
      _index = 1 → 下一个目标 PuzzleSolved(3) 未满足 → BroadcastCurrent()（广播，当前无订阅者）
      HUDController.OnObjectiveCompleted → 只写一条 Debug.Log（目标栏已移除，玩家侧零反馈，见 UXDesign §7 G9）
 → RescueTarget 自身保持 _done=true，永久不可再触发（幂等）
```

### 3.3 一次死亡 → 失败 → 重开

```text
玩家 HP ≤ 0 → PlayerCombat 判死 → PlayerController.ApplyHit → IsDead=true（DiedFromFall=false）
 → LevelManager.Update（同帧或下一帧）→ FinishLevel(false, "玩家被击败")
      _spawner.Stop()                                          ← 停止再生
      _player.InputLocked = true
      success=false → 跳过落幕对白 → CommitFinish(false, reason)
          BuildResult(false)：PurityPercent/ElapsedSeconds/KillCount/PickupCount/Stars=0
          LevelSaveService.CommitResult(result)：写 result 槽位；progress 只更新 LevelCleared（false 不改变）
          AudioManager.PlaySfx(SfxId.LevelFail)
          SetState(Failed) → Publish(LevelStateChangedEvent)
          Publish(LevelFailedEvent{Reason})
 → ResultPanel.OnFailed → 填「关卡失败 + 死因 + 净化率」→ 隐藏"下一关"按钮 → UIManager.SafeOpen("Result")
      UIManager.ApplyInputLock：IsBlocking=true → player.InputLocked = ExternalInputLock = true
 → 玩家点「重新开始」→ Publish(RestartLevelEvent)
 → LevelManager.OnRestartRequested → RestartLevel()
      enabled=false；SetActive(false)；SceneManager.LoadScene(buildIndex)
 → 场景重载：所有对象重新 Awake/Start，可净化目标回到未净化，净化值归零
```

**为什么不做原地重置**：已净化对象、永久关闭的塔、单向开关的拉杆……都是单向状态；原路回滚需要给每个组件写 `Reset()` 并保证顺序，而重载场景是"重新初始化"的天然正确解。

### 3.4 一次通关 → 写档 → 结算面板（第三关带落幕对白）

```text
L3：Boss HP 归零 → Boss 死亡动画 DieEnd → BossController 击败演出 5.5s（见 08-Boss §3.1）
 → base.OnDieEnd → BroadcastDeath → EnemyDeathEvent
 → LevelObjectiveTracker._defeatedEnemies++ → Evaluate()
      目标② DefeatEnemies(1) 满足 → _index=2 ≥ Objectives.Length → CompleteChain()
      ChainCompleted=true → _onChainCompleted() 回调
 → LevelManager.OnObjectiveChainCompleted → FinishLevel(true, "全部关卡目标完成")
      _finished=true；停刷怪；InputLocked=true
      TryPlayEndingDialogue()：LevelIndex==3 且无对话在播且 ava_ending 存在 → 打开 DialoguePanel 播 3 条
      → CommitFinishAfterEnding 协程等待
      …玩家看完按 E 关掉 → 再等 1 帧 → CommitFinish(true, reason)
          BuildResult(true)：Stars = _config.EvaluateStars(percent)
          CommitResult：写 result + 解锁 UnlockedLevelIndex = max(旧, 4)（第 3 关通关后为 4，即"全部解锁"）
          PlaySfx(SfxId.LevelComplete) → SetState(Completed) → Publish(LevelCompletedEvent{Result})
 → ResultPanel.OnCompleted → 评级 S/A/B/C + 星级 ★★★ → "下一关"按钮（有下一关时显示）→ SafeOpen("Result")
```

## 4. 设计取舍

| 取舍项 | 选择 | 放弃的方案与理由 |
|---|---|---|
| 检查点系统 | **整体移除**，阵亡即失败 + 重载本关 | 放弃"回检查点复活"：需要关卡快照、单向状态回滚、`RestoreHP`/`Revive` 打洞玩家类；工作量与 bug 面远超收益，且横版短关卡（实测 L1 1~2 分钟）不需要 |
| 重开方式 | 重载场景 | 放弃"原地重置"：见 §3.3 末 |
| 目标链判定 | 计数无条件累加 + `while` 推进 | 放弃"只给当前目标计数"：玩家提前清理会让目标链卡死到下次同类事件 |
| `Required` 越界 | 运行时夹取 + Warning | 放弃"编辑器校验"：场景内容由 Setup 脚本生成，配置和场景可能先于对方存在；运行期夹取是唯一能保证不卡关的位置 |
| 分类计数口径 | `TargetId` 字符串前缀 | 放弃"给 `IPurifiable` 加强类型字段"：要改 M5 已验收接口；代价是弱约定，已在 §2.2 显式登记 |
| 完成判据 | 目标链 **或** 100% 净化，先到者为准 | 放弃单一判据：L3 目标链不含 `ReachPurity`，只留 100% 会让"打完 Boss 但没净化满"卡关；反之只留目标链会让 L1 无法"靠净化通关" |
| 结算面板实现 | 程序化 uGUI（`UIUtility` 造控件） | 放弃序列化 Prefab：无美术、无 UI 图集，程序化省掉 20+ 个 `.meta` 与手动锚点；代价是改样式要改代码 |
| 关卡层与 UI 的关系 | 只发事件、不持有 UI 引用 | 放弃"关卡里 `new` 面板"：UI 会随 M7 换皮，关卡层不应随之改动 |
| 限时失败 | **不实现**（`LevelConfig.TimeLimit` 字段保留无消费方） | 主动范围收敛，非漏项；超时失败需要压力 UI，M10 也未做 |

## 5. 风险

| # | 风险 | 概率 | 影响 | 现状与对策 |
|---|---|---|---|---|
| L1 | **`LevelResultUI` 死代码被重新激活 → 双结算面板** | 中 | 中（两个面板叠一起，按钮互抢） | `LevelSetup.cs:257` 仍会 `AddComponent<LevelResultUI>()`。当前三关场景里 0 个实例（`LevelUISetup` 移除过），但**任何人重跑 `StarEco/Setup/Level` 就会复活它**。对策：删除该 AddComponent 或直接删 `LevelResultUI.cs`（P0，登记于 RiskRegister） |
| L2 | 三关共用 `level01_result.json` 槽位，后写覆盖前写 | 高（已存在） | 中（"最近一局结算"语义可行，但无法按关查询） | 现状可接受：`ResultPanel` 只显示"本局"，不读历史。若要"每关最佳成绩"必须扩槽位（P2） |
| L3 | 第三关落幕对白与"玩家自己站在艾娃前对话"撞车 | 中 | 低（对白丢失） | 闸门③：`IsDialogueActive` 为真就跳过落幕对白直接结算。已实测：不会双播 |
| L4 | `PurifyTowers` 目标类型三关均未使用 | — | 低（代码冗余） | 保留：L1 的塔可攻击击毁，`TowerDisableEvent` 已接进 `_purifiedTowers`；将来加"击毁 N 座塔"目标无需改代码 |
| L5 | `ClampRequired` 用 `FindObjectsByType<MonoBehaviour>` 全场景扫描 | 中 | 低（启动尖峰） | 只在 `Initialize` 执行一次；实测 L3 场景约 400 个 MonoBehaviour，耗时 < 5ms。若关卡规模翻倍可改为按接口缓存 |
| L6 | `RestartLevel` 依赖 `buildIndex`，Build Settings 顺序即关卡序号 | 高 | 高（加载错关卡） | `LevelIndex == buildIndex` 是全项目不变式，已写进 CodeStandard；改 Build Settings 顺序必须同时改三关 `LevelConfig.LevelIndex` |

## 6. 与 requirements 的偏差说明

`requirements.md` 写于 M6 立项（单关阶段），之后被 M7/M9/M10 覆盖。**以本节为准**。

| 编号 | requirements 原文口径 | 实际实现 | 原因 |
|---|---|---|---|
| D1 | R7 结算面板 = `LevelResultUI`（程序化 uGUI） | 实际 = `UI/ResultPanel`（M7 面板栈成员）；`LevelResultUI.cs` 保留但无场景实例 | M7 统一面板栈后，结算必须走 `UIManager.Open`，`LevelResultUI` 自成一套 Canvas/EventSystem 与面板栈冲突 |
| D2 | R3 "完成判定只检查当前目标，未解锁目标不计数" | 计数器**无条件累加**，`Evaluate` 用 `while` 连续推进 | 原设计会卡关：玩家提前完成后续目标时不计数，链必须等下一次同类事件才能恢复（M9 实测） |
| D3 | R2 完成判据 = `LevelPurifiedEvent` | 目标链全完成 **或** 100%，先到者为准 | L3 目标链不含 `ReachPurity`，否则打完 Boss 但净化未满会卡关 |
| D4 | R3 目标类型 4 种 | 6 种（+`RescueTargets`、`PuzzleSolved`） | M9 第二关需求：解救动物、疏通河道 |
| D5 | （未提） | 新增 `LevelManager` 的落幕对白拦截（`TryPlayEndingDialogue` / `CommitFinishAfterEnding`） | M10 叙事补白：直接弹结算会冻结输入，玩家没机会看收尾文本 |
| D6 | （未提） | 新增 `LevelObjectiveTracker` 的 `ClampRequired` | 防死锁；配置与场景可能不同步 |
| D7 | R6 存档槽位"每关一份" | 三关共用 `level01_result.json` | M6 单关遗留，未随多关改造（§5 L2） |
| D8 | （未提） | `Level/CameraFollow2D.cs` 属关卡层 | 相机边界是关卡内容（每关边界不同），不是通用框架能力 |

## 7. 验收方式

1. `StarEco/Setup/Level` 一键生成 `Level01.asset` + `Level01.unity`（灰盒：地面 / 出生点 / 3 垃圾堆 / 1 挡路墙 / 1 塔 / 刷怪锚点 / 氛围 + `PuritySystem` + `LevelManager`）。
2. Console 零 error。
3. 运行验证（对应 `tasks.md` T20 与 `docs/TestPlan.md` §4.2）：目标链推进、污染度刷怪、死亡即失败并弹结算、星级计算、存档写读、`RestartLevel` 彻底回滚。
