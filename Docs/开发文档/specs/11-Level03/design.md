# 设计说明：11-Level03（第三关 · 能源工厂）

> 对应 `requirements.md` R1~R8 | 上游：`LevelDesign.md` §L3、`LevelArt.md` §五、`TDD.md` §3

---

## 1. 设计意图

第三关是**病根**。前两关玩家处理的是"症状"（垃圾、堵河），这一关要面对污染的源头。

这条叙事意图直接翻译成三个设计决定：

| 意图 | 设计决定 |
|---|---|
| 工厂是"机器在运转但已经坏了" | 传送带还在转、通风口还在吹、拉杆还能扳——**所有工业设备都有电，但都在伤害你** |
| 污染核心不是坏人有恶意 | Boss 无台词、无挑衅、三形态是"渗出→膨胀→爆散"，打赢等于"治好了" |
| 一关比一关更"人工" | 平台从木板（L1）→ 原木（L2）→ 工业钢（L3）；地面从土壤→（待改）工业地板 |

---

## 2. 空间结构

### 2.1 四段递进

```
X:  -20        -7         25        39              64
    ├──────────┼──────────┼─────────┼───────────────┤
    ① 入口段    ② 二楼段    ③ 跳跃段   ④ 解谜 + Boss 段
    20u         32u        14u       25u
```

| 段 | 长度 | 主要机制 | 教学目的 |
|---|---|---|---|
| ① 入口段 | 20u | 地刺 + 移动平台 | 教"平台会动" |
| ② 二楼段 | 32u | 垂直攀爬 + 拉杆 + 封锁门 | 教"拉杆开机关"，引出 Boss |
| ③ 跳跃段 | 14u | 碎裂平台 × 2 + 移动平台 | 教"平台会碎"，考验时机 |
| ④ 解谜 + Boss | 25u | 拉杆关地刺 + Boss 三阶段 | 综合考验 |

**为什么把 Boss 放在第四段末尾**：前三段是"复习"（L1 的跳跃、L2 的解谜），第四段才是新东西（Boss）。玩家在进入 Boss 前已经把所有机制都用过一遍。

### 2.2 平台与机关坐标（实测）

| 对象 | X | Y | 组件 | 说明 |
|---|---|---|---|---|
| `PlatformL2` | −15.60 | 2.48 | BoxCollider | 静态平台 |
| `PlatformL1` | −12.80 | 5.13 | **CrumblePlatform** | 通往二楼，站久必碎 |
| `SpikeField` | −12.10 | 0.00 | SpikeHazard | 地面地刺 |
| `PlatformC` | −7.93 | 6.63 | **MovingPlatform** | 10.2→1.7 横向摆渡 |
| `SecondFloor` | −2.87 | 3.48 | BoxCollider | 二楼地板 |
| `LeverSwitch_03` | −1.21 | 4.26 | LeverSwitch | 二楼拉杆 |
| `SecondFloorRightWall` | 0.23 | 5.15 | BoxCollider | 二楼右墙 |
| `PlatformR1` | 12.80 | 1.28 | BoxCollider | — |
| `PlatformR2` | 15.60 | 2.48 | BoxCollider | — |
| `Jump_Crumble1` | 27.00 | 1.88 | **CrumblePlatform** | 跳跃段起点 |
| `Jump_Mover` | 28.80 | 3.18 | **MovingPlatform** | 跳跃段摆渡 |
| `Jump_Spikes` | 33.50 | 0.00 | SpikeHazard | 跳跃段地刺 |
| `Jump_Crumble2` | 35.40 | 2.28 | **CrumblePlatform** | 跳跃段终点 |
| `Puzzle_Step` | 39.40 | 1.08 | BoxCollider | 解谜段台阶 |
| `Puzzle_Floor` | 44.85 | 2.63 | BoxCollider | 解谜段平台 |
| `Puzzle_Spikes` | 46.50 | 0.00 | SpikeHazard | 解谜段地刺（拉杆关闭） |
| `Puzzle_Lever` | 47.00 | 3.20 | LeverSwitch | 关地刺的拉杆 |

> ⚠️ **数据来源**：以上为场景文件实测值。`Level03ExtendSetup.cs`（另一会话创建）用 `ClonePlatform` 克隆生成跳跃段，与本文档坐标可能漂移，改场景后需同步本表。

---

## 3. 机关参数

### 3.1 碎裂平台（`CrumblePlatform`）

| 参数 | 值 | 设计理由 |
|---|---|---|
| `_breakDelay` | **0.5 s** | 社区主流区间 0.4~0.8 s。0.5 s 下，玩家"站上去犹豫一下就掉"，但正常跳跃反应时间足够 |
| `_shakeAmplitude` | 0.045 | 抖动幅度约平台自身高度的 4%，足够被察觉但不破坏定位 |
| `_shakeFrequency` | 26 Hz | 高频抖动，读作"碎裂前的震动"而不是"摇晃" |
| `_chunkCount` | 4 | 竖切 4 条碎块，复用 `BreakableDebris`，无需纹理 Read/Write |
| `_chunkLifetime` | 0.95 s | 碎块落地消失 |
| `_chunkGravity` | 17 | 比角色重力（3×9.8）快，让碎块"掉得比人快" |
| `_chunkSpreadX` | 1.5 | 横向散开 |
| `_chunkUpward` | 1.2 | 初始上抛 |
| `_respawnDelay` | **3 s** | 太短会让玩家反复踩空，太长会让卡关 |
| `_respawnFade` | 0.18 s | 淡入 |

**关键规则：计时一旦开始不因离开而取消。**

这是刻意的。如果"离开就复位"，玩家会学会"踩一下马上跳回来"来试探，平台就失去了威胁。现在一脚踩上去就开始倒计时，玩家必须**立刻决定走还是留**。

### 3.2 移动平台（`MovingPlatform`）

| 参数 | `PlatformC` | 设计理由 |
|---|---|---|
| `_minX` / `_maxX` | **−10.2 / −1.7** | 覆盖 11.3u 空档。满速跳距约 5.3u，跳不过去，必须坐平台 |
| `_speed` | **2 u/s** | 低于玩家跑速（6），玩家能在平台上自由走动 |
| `_pauseAtEnds` | **0.35 s** | 停一下让玩家有"上下车"的窗口 |
| `_carryRiders` | true | 必须带人（见下） |

**为什么必须显式带人**：玩家碰撞体挂的是 `PlayerFrictionless` 材质（摩擦 0），站在移动平台上**不会**被摩擦力带走。平台必须每帧把自身位移直接加到 `Body.position`，通过 `velocity` 无法实现（`velocity` 会被玩家自身的移动逻辑覆盖）。

### 3.3 其他机关

| 机关 | 行为 |
|---|---|
| `SpikeHazard` | 持续伤害区，走 `IHittable`（与敌人同管线，因此有无敌帧保护） |
| `ConveyorZone` | 持续水平推力，模拟传送带 |
| `VentHazard` | 周期性向上吹力/伤害，模拟通风口 |
| `FallHazard` | 掉出下界即判定（三关都有，不只 L3） |

---

## 4. Boss 战

### 4.1 三阶段

| 阶段 | 血量区间 | 变化 | 视觉 |
|---|---|---|---|
| **Phase1** | 100% ~ 60% | 基础攻击节奏 | Idle / Move / AttackA / AttackB |
| **Phase2** | 60% ~ 30% | 攻击间隔 × `Phase2IntervalScale` | 播 `Enrage`，过场 0.8 s |
| **Phase3** | 30% ~ 0% | 间隔 × `Phase3IntervalScale`，解锁 `Special` | 持续高压 |

阈值实现在 `BossConfig.Phase2HpPercent` / `Phase3HpPercent`（`combat_units.csv` 记为 60% / 30%）。

### 4.2 状态机

```
Idle ──► Move ──► AttackA / AttackB ──► Idle
  │                                    │
  └──────── Hurt ◄─────────────────────┘
              │
              ▼（HP 跨阈值）
           Enrage ──► Idle（新阶段）
              │
              ▼（HP ≤ 0）
            Death ──► EnemyDeathEvent（→ 目标链②完成 → 结算）
                   └► BossDefeatedEvent（当前 0 订阅者，见 08-Boss §5 B4）
```

- `AttackA`（砸地）：近战范围判定，2 s/次
- `AttackB`（喷毒）：3 发弹幕，3 s/次
- `Special`（Phase3）：14 帧特殊招，判定最宽

### 4.3 封锁墙与 Boss 登场（`BossGate`）——**毕设版未启用**

> ⚠️ **v1.2 重大更正（2026-10-30 23:58，A6 实机验收发现）**
>
> v1.1 把这一节写成了"玩家净化那面墙 → 墙崩解 → 地震 → Boss 破土登场 2.32s 演出"。
> **这个描述在毕设版里完全不成立。** 实测：
>
> | 核查项 | 结果 |
> |---|---|
> | `BossGate.cs` 是否存在 | ✅ 存在，**372 行**，代码完整 |
> | 五个场景（Title/L01/L02/L03/Sample）里有没有挂 `BossGate` 的对象 | ❌ **全部 0 引用** |
> | 场景里有没有叫 `BossGateWall` 的对象 | ❌ **没有** |
> | L3 的 5 个可净化对象 | `Garbage1`/`Garbage2`/`Garbage3`（TargetId 均为 `GarbagePile`）、`GarbageWall_03L`、`GarbageWall_03R` |
> | Boss 初始状态 | **一进场就 `active=True`** 站在 (68.0, ~2.3)，没有"从地下升起" |
>
> **真相**：`BossGate` 是**求职展示版**才启用的演出脚本。`Level03ExtendSetup.cs:25` 的注释写得很清楚：
>
> > 两份项目通用：右端封锁墙按名字查，**毕设版是 `GarbageWall_03R`，求职展示版是 `BossGateWall`**（后者挂着 BossGate 演出脚本，只移位不动逻辑）。
>
> 也就是说：**毕设版走的是"净化右端垃圾墙开路 → Boss 已经站在那里等你"**，没有破土登场。那套演出代码保留在仓库里，是为求职版准备的，毕设版场景没有挂。

```text
毕设版 L3 真实流程（实测）：
进场：Boss01 已在场地深处 (68.0, 2.3) active，被 GarbageWall_03R（x=71.5）挡在右侧
      —— 注意墙在 Boss 右边，不是挡在玩家和 Boss 之间
目标①：清 4 个污染源（5 个可净化对象里任意 4 个；Required=4，_purifiedTargets 按事件递增不去重）
目标②：击败 Boss01
结局：BaseDie → EnemyDeathEvent → 目标链完成 → 结局对白 ava_ending → 结算
```

**为什么文档会写错**：v1.1 那次修订是照 `BossGate.cs` 的代码读出来的——代码确实实现了那套演出，读代码得出"净化墙→破土登场"的结论在逻辑上没错，**但漏了最关键的一步：确认这个脚本在场景里到底有没有被挂上**。代码存在 ≠ 功能启用。这与本项目之前"检查点"的教训是同一类错误（三份文档都写"死亡回检查点"，实际无 `Checkpoint.cs`）——**判"有"要查三件：脚本、场景挂载、调用点**。

`_fxBurstPrefab`（`fx_burst`）在这套未启用的演出里是"破土那一瞬间"的特效，不是"Boss 死亡爆心"；该 prefab 已由 A4 补齐存在，但当前无引用方。

---

## 5. 数据流

### 5.1 一次踩碎平台（`CrumblePlatform`）

```text
① 玩家落到平台上（OnCollisionEnter2D，判定是玩家而非敌人/碎块）
      → 若 _armed == false：_armed = true；_timer = 0；开始抖动
      ⚠️ 一旦 _armed 就不会因玩家离开而取消（§3.1 的关键规则）

② 抖动阶段（0 → 0.5s）
      localPosition = base + (sin(t × 26Hz) × 0.045, cos(...) × 0.045)
      视觉上"平台在碎前的震动"，玩家读到"这玩意儿不结实"

③ 0.5s 到点 → 碎裂
      Collider2D.enabled = false（玩家立刻踩空）
      SpriteRenderer 隐藏
      生成 4 个 BreakableDebris 碎块：lifetime 0.95s / gravity 17 / spreadX 1.5 / upward 1.2
      AudioManager.PlaySfx(SfxId.BreakStone, pitch ±12%)

④ 3s 后复位
      延迟 3s → _respawnFade 0.18s 淡入 → Collider 重新启用 → _armed = false
      （复位后可以再次踩碎，循环往复）

⑤ 玩家在 0.4s 内跳走
      平台照碎（计时不取消）→ 玩家在空中看着它碎 → 落回来时已无落脚点
```

**这条设计的效果**：玩家会自然学会"踩上去就得往前走"，而不是"试探一下再回来"。平台从"障碍"变成"节拍器"。

### 5.2 一次坐移动平台过 11.3u 空档（`PlatformC`）

```text
① 玩家落到平台顶（OnCollisionEnter2D → _riders.Add(player.Body)）

② 平台每帧：
      x = Mathf.MoveTowards(x, _targetX, _speed × dt)        _speed = 2 u/s
      到达端点 → 停 _pauseAtEnds = 0.35s → 反转 _targetX（-10.2 ↔ -1.7）
      delta = 本帧位移
      foreach rider in _riders：rider.position += delta     ← 关键：直接改 Body.position

③ 玩家跳起离开 → OnCollisionExit2D → 从 _riders 移除 → 之后平台移动不再带动玩家
```

**为什么必须直接改 `Body.position`**：玩家碰撞体用的是 `PlayerFrictionless`（摩擦 0），平台靠摩擦力带不动人。而通过给玩家 `velocity` 加速也不行——`PlayerController` 每帧会用自己的移动逻辑**覆盖** `velocity`。所以唯一可靠的做法是每帧把平台的位移量直接加到玩家位置上。

**为什么速度只有 2 u/s**：玩家跑速 6 u/s。平台慢到玩家能在上面自由走动、甚至逆向走到另一端；如果平台快过玩家跑速，玩家会被"甩下去"，那就不叫平台叫传送带了（传送带另有 `ConveyorZone`）。

### 5.3 一次拉杆关地刺（`LeverSwitch`）

```text
① 玩家进拉杆交互范围 → 出现「按 E」气泡（与 M7 对话同构）
② Input.GetKeyDown(E) → 拉杆动画切到"已扳"状态 + PlaySfx(BreakStone 低音量)
③ 拉杆的 _targets 列表里的对象收到回调：
      · Puzzle_Spikes → SpikeHazard.enabled = false（或碰撞体关闭）
      · 其他拉杆联动的机关同理
④ 一次性拉杆：扳后永久保持；可复用拉杆：再按一次复原
```

`LeverSwitch_03`（−1.21, 4.26）在二楼段，用于教"拉杆能开机关"；`Puzzle_Lever`（47.00, 3.20）在解谜段，用于关掉 `Puzzle_Spikes`（46.50）——**同一套组件，两次教学，递进关系**。

### 5.4 一次第三关全程推进（目标链 4 → 1 → 结局）

```text
【目标① 清理 4 个污染源】场景实测可净化对象共 5 个，任意 4 个即可
  Garbage1(-6.0) / Garbage2(2.0) / Garbage3(9.0)   ← TargetId 均为 "GarbagePile"
  GarbageWall_03L(-21.5) / GarbageWall_03R(71.5)   ← TargetId 为各自名字
  玩家喷雾净化，每个 CompletePurify → Publish(GarbagePurifiedEvent)
  → LevelObjectiveTracker.OnGarbagePurified → _purifiedTargets++（按事件递增，不按 TargetId 去重）
  → 第 4 个时 _purifiedTargets = 4 → ObjectiveCompleted(①)
  → 目标② DefeatEnemies(1) 解锁

【Boss】Boss01 一进场就 active 站在 (68.0, ~2.3)，没有登场演出，
       也没有"必须先清墙才能打"的门禁——玩家清够 4 个后可以直接走过去打
  §4.1 三阶段 → HP 80 → 0
  击败演出 5.5s（3 次净化波连炸 → 污染色褪绿 → 核心上浮消散 → Victory + 落幕字幕）

【结局】
  base.OnDieEnd → EnemyDeathEvent → _defeatedEnemies = 1 → CompleteChain()
  → LevelManager.FinishLevel(true) → 落幕对白 ava_ending（3 条）
  → 玩家按 E 看完 → 等 1 帧 → CommitFinish(true) → 结算面板 S/★★★
```

**关于目标①的"4 个"**：`Level03.asset` 里 `Required: 4`，Label 写「清理工厂污染源（含封锁墙）」。但**"含封锁墙"这个措辞已经失真**——场景里没有叫 `BossGateWall` 的封锁墙，只有左右两道普通垃圾墙 `GarbageWall_03L/03R`。当前 5 个对象都算普通污染源，凑满 4 个就过。`Label` 建议改为「清理工厂污染源 ×4」以免答辩时被追问"封锁墙在哪"（登记为 `RiskRegister` 的文案缺口）。

## 6. 落幕对白的实现取舍

### 5.1 问题

Boss 死后，`LevelManager` 原本立刻 `SetState(Completed)` → 弹结算 → 冻结输入。玩家**没有移动窗口**，在场景里摆 `DialogueTrigger` 等于摆设（永远走不到）。

### 5.2 方案

把 `FinishLevel(success)` 拆成两段：

```
FinishLevel(success)
  ├─ 若 success && LevelIndex == 3 && TryPlayEndingDialogue() 命中：
  │     ├─ UIManager.SafeOpen(Dialogue)
  │     ├─ DialoguePanel.Play(ava_ending)
  │     └─ StartCoroutine(CommitFinishAfterEnding)   ← 到此 return，不结算
  └─ 否则：CommitFinish(success, reason)              ← 原逻辑

CommitFinishAfterEnding:
  yield 1 帧
  while (DialoguePanel.IsDialogueActive) yield
  yield 1 帧        ← 关键：不等这一帧，结算会被关闭对白的同一个 E 按掉
  CommitFinish(true, reason)
```

### 5.3 三条约束

1. **必须只在 `LevelIndex == 3` 触发**。否则第 1、2 关通关也会讲"三颗星球数据互通"，语义错误。
2. **必须能在面板不可用时降级**。数据缺失、面板缺失 → 直接 `CommitFinish`，绝不阻塞通关。
3. **胜利音效顺延**。`SfxId.LevelComplete` 在 `CommitFinish` 里，现在被推到对白之后——先安静看完结局、再响音乐，比同时砸上来干净。这是有意保留的副作用。

---

## 7. 边界、失败模式与风险

### 7.1 边界与失败模式（逐场景）

| 场景 | 行为 | 依据 |
|---|---|---|
| 玩家在碎裂平台上站满 0.5 s | 掉落，y 从 6.36 → 1.13（实测） | `CrumblePlatform` |
| 玩家在碎裂平台上 0.4 s 内跳走 | 平台仍碎，玩家不掉落 | 计时不取消 |
| 玩家不坐移动平台硬跳 | 跳不过 11.3u 空档（满速跳距 5.3u） | 平台是必经 |
| 玩家在移动平台上跳跃 | 正常起跳（平台不锁定玩家） | `_carryRiders` 只加位移 |
| Boss 阶段切换中被打死 | `IsPhaseTransition` 期间不判新阶段，直接进 Death | `EvaluatePhase` 守卫 |
| 落幕对白数据缺失 | 降级直接结算 | R6 |
| 玩家在 Boss 门口死亡 | 整关重载（**无中途检查点**） | 全项目无检查点系统 |
| 玩家清理污染源数量不足 4 个 | 目标①不完成，目标②不解锁，**Boss 仍可被攻击但打死也不算通关** | `LevelObjectiveTracker` |
| 玩家清够 4 个后直接去打 Boss | 允许（**无门禁**，Boss 一直 active） | 毕设版无 `BossGate`（见 §4.3） |

### 7.2 风险

| # | 风险 | 概率 | 影响 | 现状与对策 |
|---|---|---|---|---|
| **N0** | **Boss 站在自己关卡的喷气口旁，被 `Env_Vent` 持续扣血 + 吹起** | **已发生** | **P0：Boss 血不受玩家控制地流失** | A6 实机 144.7s 内从 80 掉到 **54（67.5%）**，约 2.5s/点。Boss collider x=65.82~70.18 与 `Env_Vent` x=64.4~66.0 **重叠 0.18**；`VentHazard.OnTriggerStay2D` 只找 `IHittable` 不筛敌我，边扣血边 `_liftSpeed` 上推 → Boss 在 y2.3↔6.0 震荡。阈值 Phase2=60%/Phase3=30% → 约 3 分钟自动进二阶段、6.5 分钟自己死。**两种修法待选**：① A 移开/缩窄 `Env_Vent`（场景，快）；② B 让 `VentHazard` 只伤害玩家（语义更正确） |
| **N0b** | **Boss 血条永不显示** | **已发生** | **高：打 Boss 看不到血量** | `BossHealthBar._boss` 在 Inspector 预绑 `Boss01`，`TryBindBoss()` 首行 `if(_boss!=null) return;` 直接返回，**永不调用 `Show()`**；`Build()` 末尾已 `Hide()` → `BossHealthBarCanvas.active=False`。修法：把 `Show()` 移到预绑分支之前，或去掉预绑改由运行时查找（归 B） |
| ~~N1~~ | ~~封锁墙 `TargetId` 与 `BossGate._gateTargetId` 不一致~~ | — | — | ❌ **风险不存在**：毕设版场景未挂 `BossGate`（§4.3 已更正），该脚本 372 行当前是死代码 |
| N2 | 平台坐标与 `Level03ExtendSetup.cs` 的 `ClonePlatform` 结果漂移 | 高 | 中（文档失真） | §2.2 表为场景实测值；每次跑 Setup 或手改场景后必须回填本表。已在表下写警示 |
| N3 | 碎裂平台"计时不取消"被误判为 bug 改掉 | 中 | 高（关卡节奏崩坏） | §3.1 与 §5.1 两处都写明这是刻意设计；`CrumblePlatform` 源码注释保留原委 |
| ~~N4~~ | ~~玩家被 Boss 破土顶进墙里~~ | — | — | ❌ **风险不存在**：无破土演出的情况下 `PushPlayerClearOf` 那套逻辑根本不会执行 |
| N5 | 击败演出期间输入锁 + 无敌，若协程异常会卡死 | 低 | 高 | 见 08-Boss §5 B2；当前无超时解锁兜底 |
| N6 | 第三关地面曾与三关同图 | **已修** | — | A2 已改为 `ground_factory`（`117e444`） |
| N7 | 目标① Label 写「含封锁墙」但场景无封锁墙 | 已存在 | 低（答辩被问） | 见 §5.4 末段；建议改文案为「清理工厂污染源 ×4」 |



---

## 8. 与上下游文档的关系

| 文档 | 关系 |
|---|---|
| `LevelDesign.md` §L3 | 上游：关卡策划的动线与目标链视角 |
| `LevelArt.md` §五 | 平级：同一关的美术视角（贴图错配、装饰牌、三层结构） |
| `specs/08-Boss` | 上游：Boss 战的技术规范（本 spec 只引用不重复） |
| `NarrativeDesign.md` §4 | 上游：老机器人开场、落幕对白的文案 |
| `AnimationVFXSpec.md` §8 | 上游：Boss 死亡特效的实现现状 |

---

## 9. 变更记录

| 版本 | 日期 | 变化 | 原因 |
|---|---|---|---|
| v1.2 | 2026-10-30 | **§4.3 整节重写**：`BossGate` 372 行是死代码，五场景零引用，毕设版右端墙是 `GarbageWall_03R`（求职版才是 `BossGateWall`）→ "净化墙→破土登场 2.32s 演出"在毕设版**不存在**；§5.4 目标①改为"5 个可净化对象任意 4 个"并指出 Label「含封锁墙」措辞失真；§7.1 删"门不开"改"无门禁"；§7.2 删 N1/N4（风险不存在）、新增 **N0 Boss 被喷气口持续扣血（P0，实测 144.7s 掉到 67.5%）** 与 N0b Boss 血条永不显示 | A6 实机验收四个场景时发现。v1.1 是照代码读出来的结论，**漏了验证脚本在场景里到底有没有挂**——与"检查点"同类的错误：判"有"必须查脚本+场景挂载+调用点三件 |
| v1.1 | 2026-10-30 | 修正 §4.3：`BossGate` 是"挡住 Boss 的墙"不是"封锁退路的门"（v1.0 写反了）；§4.2 死亡箭头改为 `EnemyDeathEvent`（`BossDefeatedEvent` 0 订阅）；新增 §5 数据流（碎平台 / 移动平台 / 拉杆 / 全程推进四条链路）与 §7.2 风险表 6 条 | 按落地代码复核 01~10 时发现第三关两处描述与实现相反；且全文缺数据流，"平台为什么带人"这类关键结论无处可查 |
| v1.0 | 2026-10-30 | 首版（补写）。四段结构与 17 个平台/机关实测坐标、碎裂与移动平台参数表及设计理由、Boss 三阶段与状态机、封锁门机制、落幕对白的三段式实现与三条约束、8 条边界表 | `specs/` 有 01~10 共 10 个功能规范，唯独第三关没有。三关里最复杂的一关（84u / 37 个精灵 / Boss 战 / 落幕对白）反而无规范可依，后续改动全凭记忆 |
