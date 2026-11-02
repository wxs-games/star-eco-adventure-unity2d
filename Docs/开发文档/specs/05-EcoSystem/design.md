# 技术方案：05-EcoSystem

> 版本：v2.0（2026-10-30 补 §3 数据流 4 条链路、§5 拆分取舍/风险、§6 偏差说明） | 对应 `requirements.md` R1~R10（约 50 条 EARS） | 命名空间 `StarEco.Eco` | 代码落点 `Assets/Scripts/Eco/`（26 个 .cs / 2,158 行）
> 前置已就位：M1 `EventCenter`/`PoolManager`/`AudioManager`、M2 玩家 FSM 与 `PlayerAnimParams`、M3 `IHittable`/`DamageInfo`/`PlayerCombat`（`AttackPressedThisFrame` 驱动模式）、M4 敌人对接入口（`EnemyControllerBase.TakeDamage`、`Tower01Controller.RequestDisable`、`EnemyDeathEvent`/`TowerDisableEvent`）
> 立项决策（已定稿，设计不再重议）：喷洒动作复用 `Player_Interact`、蓄力释放复用 `Player_HeavyAttack`（不新增美术，"喷壶是背包附件"设定）；喷壶对敌伤害**复用** `IHittable`/`DamageInfo`（不新增平行伤害通道）；Tower01 净化对接用**适配器组件** `TowerPurifiable`（零改 M4 源码）；蓄力净化波**同帧结算**（不走动画事件帧，无连段时序约束）；净化值采用**场景注册制**（`IPurifiable` 注册 → 统计总量）；氛围仅插值 `Camera.backgroundColor`（不引入后处理栈）；拾取自动（Trigger 碰撞，无按键交互）；垃圾堆/补给瓶美术缺失 → WhiteSquare 占位（美术后补仅换 Sprite）

## 1. 结构

### 1.1 与 M2/M3/M4 的边界：复用 / 新建 / 为何不重构（对应 R2、R3、R8、R10）

| 边界项 | 处理 | 依据 |
|---|---|---|
| `StarEco.Combat.IHittable` / `DamageInfo` | **直接复用，零改动** | 喷壶对污染生物的持续伤害走同一条命中流水线（R8：敌人侧 `TakeDamage` 已含扣血/硬直/死亡/回收全链路） |
| 玩家 `PlayerCombat` 的 `AttackPressedThisFrame` 驱动模式 | **复用设计模式，不改源码** | `SprayController` 暴露 `SprayHeld` / `ChargeHeld` / `ChargeReleased` 属性，玩家 FSM 状态读这些属性做转移（与 M3 攻击状态完全同构） |
| `PlayerController.cs` + `PlayerStates.cs` | **最小增量（合计约 15 行，已核实收敛）**：①`Spray` 属性 + Awake 缓存（参照 `Combat` 属性先例）；②`BuildStateMachine()` 两行注册；③`MoveHorizontally` 加可选参数 `speedFactor=1f`（移速因子注入点，既有调用点零改动）；④`PlayerIdleState`/`PlayerRunState` Tick 加喷洒/蓄力进入转移（M3 攻击转移同构，`TryConsumeXxx` Consume 模式） | R10 明确允许 M5 动 Player/（TDD §2.1 "Player/（净化喷雾 ⏳ M5）"）；进入转移无法只在 Eco/ 侧实现（M2 FSM 纪律：转移逻辑写在状态 Tick 内），15 行是 R2/R3 必需功能的下限 |
| `Tower01Controller.RequestDisable()` / `EnemyDeathEvent` / `TowerDisableEvent` | **直接调用/订阅，零改动** | M4 已预留的对接入口，M5 只做消费者 |
| 敌人 / 塔的攻击与感知 | **零改动** | 净化不改变敌人行为；喷洒期间玩家照常受击（受击优先级高于喷洒） |
| M1 `PoolManager` / `EventCenter` | **直接复用** | 特效/拾取物池化、全部系统间事件广播 |
| 垃圾堆美术 | **占位：WhiteSquare + 运行时变色** | 美术未排期（Art 目录无 Garbage）；SpriteRenderer.color 插值提供净化反馈，后补美术仅换 Sprite 不改逻辑 |

> **一句话边界**：M5 **新增** `StarEco.Eco` 全部文件；对 M2 增量收敛为 PlayerController/PlayerStates 的约 15 行必需接入（见上表）；对 M3/M4 只**调用**其接口（`TakeDamage`/`RequestDisable`）与**订阅**其事件（`EnemyDeathEvent`/`TowerDisableEvent`），不改其任何一行源码。

### 1.2 Prefab 与资产结构（对应 R2、R4、R5、R9）

```text
Assets/Prefabs/Eco/GarbagePile.prefab            Assets/Prefabs/Eco/PurifyPickup.prefab
Assets/Prefabs/Eco/GarbageWall.prefab            Assets/Prefabs/FX/fx_purify.prefab
Assets/Settings/EcoConfig.asset                  Assets/Settings/Drops/Enemy01Drop.asset ~（可选）
```

```
GarbagePile / GarbageWall（Prefab 根，静态可净化目标）
 ├─ BoxCollider2D（实体，非 Trigger；GarbageWall 保留阻挡，GarbagePile 亦可小实体或 Trigger）
 ├─ SpriteRenderer（占位 WhiteSquare，运行时按净化进度变色）
 └─ GarbagePile            ← 实现 IPurifiable：净化进度/阈值/PurityValue/完成表现/通路开关
```

```
PurifyPickup（Prefab 根，拾取物，经对象池复用）
 ├─ CircleCollider2D（IsTrigger=true）
 ├─ SpriteRenderer（占位小色块）
 └─ PurifyPickup           ← Trigger 碰玩家 → 回复净化剂 → 归还池
```

```
fx_purify（Prefab 根，一次性特效，经对象池复用）
 ├─ SpriteRenderer
 ├─ Animator（fx_purify 单状态 one-shot）
 └─ FxOneShot              ← 动画播完自动归还池（通用组件，M6+ 可复用于其他 fx_*）
```

**玩家侧挂载**（场景中的 Player 实例 / Prefab 增量）：

```
Player（既有）
 ├─ SprayController（新增）      ← 输入读取/喷洒/蓄力流程/周期结算
 ├─ PurifyTank（新增）           ← 净化剂资源（容量/当前值/事件广播）
 ├─ PlayerSprayState / PlayerChargeState（新增，注册进既有 FSM）
 └─ SprayHitbox（子物体，新增）  ← Trigger 矩形，随 flipX 镜像；蓄力净化波复用同一盒放大范围
```

### 1.3 文件职责表（`Assets/Scripts/Eco/`，每行可直接拆解为一个编码任务）

| 文件 | 职责 | 所属 R |
|---|---|---|
| `EcoConfig.cs` | ScriptableObject：本里程碑全部数值 + `CreateDefault()` 兜底 + `CreateAssetMenu` | R10 |
| `IPurifiable.cs` | 净化目标统一接口：`ReceivePurify(int, GameObject)` / `IsPurified` / `PurityValue` | R1 |
| `SprayController.cs` | 喷壶主组件：K/L 输入读取、`SprayHeld`/`ChargeHeld`/`ChargeReleased` 驱动属性、喷洒周期结算（IPurifiable + IHittable 分流）、蓄力满判定与净化波同帧结算、`fx_purify` 池化播放、移速因子应用 | R2、R3、R8 |
| `SprayHitbox.cs` | 喷洒判定盒：TriggerEnter/Exit 维护目标列表（Collider + `IPurifiable`/`IHittable` 缓存），喷洒中 `OnTriggerStay` 兜底补录 | R2 |
| `PlayerSprayState.cs` | 玩家 FSM 喷洒状态：Enter 开判定盒播 Interact、Exit 关盒；期间移速因子 | R2、R10 |
| `PlayerChargeState.cs` | 玩家 FSM 蓄力状态：站定、蓄力进度、未满松开取消 / 蓄满松开触发净化波（播 HeavyAttack） | R3、R10 |
| `PurifyTank.cs` | 净化剂资源：当前值/容量/消耗/回复、`PurifyTankChangedEvent` 广播、夹取 | R4 |
| `GarbagePile.cs` | 垃圾堆：实现 `IPurifiable`、净化进度、Sprite 颜色插值、完成换绿植贴图（`_cleanSprite`）、完成表现（fx/事件/阻挡 Collider 关闭） | R5 |
| `TowerPurifiable.cs` | 塔净化适配器：实现 `IPurifiable`，内部调 `Tower01Controller.RequestDisable()` | R8 |
| `PuritySystem.cs` | 净化值统计：场景级注册收集、总量/已净化、`PurityChangedEvent` 广播、100% one-shot 事件、空场景防御 | R6 |
| `AtmosphereController.cs` | 氛围：订阅净化值 → 污染度 → `Camera.backgroundColor` 平滑插值（灰紫↔自然绿） | R7 |
| `DropTable.cs` | ScriptableObject 掉落表：条目（类型/数量/概率） | R9 |
| `DropSpawner.cs` | 场景级掉落生成器：订阅 `EnemyDeathEvent` → 按表概率经池生成拾取物 | R9 |
| `PurifyPickup.cs` | 拾取物：Trigger 碰玩家 → 回复净化剂 → 归还池 + 拾取事件 | R4、R9 |
| `FxOneShot.cs` | 通用一次性特效：Animator 播完（normalizedTime ≥ 1 或 Animation Event）自动归还对象池 | R2、R5 |
| `Events/PurifyTankChangedEvent.cs` | struct：当前值/容量 | R4 |
| `Events/PurityChangedEvent.cs` | struct：当前值/总量/百分比/污染度 | R6、R7 |
| `Events/GarbagePurifiedEvent.cs` | struct：PurityValue/位置/目标标识 | R1、R5 |
| `Events/LevelPurifiedEvent.cs` | struct：关卡净化完成（100%）one-shot | R6 |
| `Events/PickupCollectedEvent.cs` | struct：拾取类型/数量 | R9 |

> 说明：`TowerPurifiable` 是适配器而非让 `Tower01Controller` 实现 `IPurifiable`，目的是**零改 M4 已验收代码**（与 M4"不改 M3"同一纪律）；代价是多一个组件，挂 Prefab 一次性成本。

## 2. 关键机制

### 2.1 净化协议与结算分流（R1、R2、R8）

```text
SprayController（每 SprayTickSeconds）
 ├─ 对 SprayHitbox 目标列表逐个：
 │   ├─ IPurifiable（垃圾/塔适配器）→ ReceivePurify(SprayDPS × Tick, player)
 │   └─ IHittable 且 layer ∈ EnemyLayer → TakeDamage(DamageInfo{Amount=SprayDPS×Tick, 无击退})
 ├─ 消耗：TankConsumePerSecond × Tick ×（本 tick 内是否有敌方目标的加倍系数）
 └─ fx_purify 在判定盒中心播放（池化，节流：每 N tick 一次）
```

- **双通道原则**：净化（`IPurifiable`）与伤害（`IHittable`）分流——同一帧同一目标可能只走其一（垃圾只有 `IPurifiable`；敌人只有 `IHittable`；Tower01 挂适配器只有 `IPurifiable`，其"关闭"即净化语义）。
- **净化进度模型**：`GarbagePile.PurifyAmount += amount`，达 `PurifyRequired` 即完成并 `IsPurified = true`（此后 `ReceivePurify` 直接 return，幂等）。
- **无进度衰减**：中断喷洒不清零进度（GDD"长按 2 秒"为连续时长上限，中断保留进度对手感更宽容；若需衰减 M6 调参）。

### 2.2 喷洒判定与镜像（R2）

- `SprayHitbox` 为 Player 子物体：`BoxCollider2D(IsTrigger=true)`，`localPosition = (±offsetX, 0)`。
- 镜像方案沿用 M4 FirePoint 的教训：**不依赖 transform 镜像**，由 `SprayController` 在 FlipChanged 时显式设 `hitbox.transform.localPosition = new Vector3(Mathf.Sign(facing) × |offsetX|, y, 0)`（每帧检查，成本 O(1)）。
- 目标列表维护：`OnTriggerEnter2D` 加入（缓存 `IPurifiable`/`IHittable` 引用，避免每 tick `GetComponent`）；`OnTriggerExit2D` 移除；对象 SetActive(false) 时由 `OnDisable` 主动剔除（池化对象风险）。

### 2.3 蓄力净化波（R3）

- 状态流：`Ground/Idle → (按住L) → PlayerChargeState`；蓄力值 0→1（`HeavyChargeSeconds` 充满）；松开：未满→取消回 Idle；蓄满→同帧 `Physics2D.OverlapBoxNonAlloc`（All 的非分配变体，复用静态数组，落实 R10 零 GC 目标）取范围目标 → `IPurifiable.ReceivePurify(HeavyPurifyAmount)` + 敌方 `TakeDamage(HeavyDamage, 带击退)` → 播 `Player_HeavyAttack` → 冷却 `HeavyRecoverSeconds` 后回 Idle。
- 与喷洒互斥：蓄力期间忽略 K 键（`PlayerChargeState` 不检查 `SprayHeld`）。
- **同帧结算依据**：无连段/无开窗时序（对比 M3 攻击必须等 HitOn 事件帧），净化波是瞬发 AOE，先结算后表现可避免"动画被击打断导致判定丢失"。

### 2.4 净化值与污染度（R6、R7）

```text
场景初始化：
  PuritySystem.Start() → 收集场景全部 IPurifiable（含 inactive？否——只收 active，
  池化敌人不参与）→ TotalValue = Σ PurityValue → 广播 PurityChanged{0, Total, 0%, 污染度=1}

每次净化完成（GarbagePurifiedEvent）：
  CurrentValue += PurityValue → 广播 PurityChanged{Current, Total, pct, 污染度=1-pct}
  → AtmosphereController：目标背景色 = Lerp(CleanColor, PollutedColor, 污染度)
  → 每帧向目标色平滑（Lerp 因子 EcoConfig.AtmosphereLerpSpeed × dt）
  → pct==100% → LevelPurifiedEvent（one-shot）
```

- `TowerPurifiable` 的 `PurityValue` 计入总分（塔净化 = 关卡净化进度的一部分，GDD 循环"净化值解锁通路/削弱敌人"的数值基础）。

### 2.5 掉落与拾取（R9）

- `DropSpawner`（场景级）订阅 `EnemyDeathEvent`：`DropTable != null` 时逐条目 `Random.value <= 概率` → `PoolManager.Get(PurifyPickup)` → 摆放在死亡位置 ± 小随机偏移。
- 拾取物无物理运动（静态 Trigger，落到死亡位置附近即可；穿地风险用生成时 y 微调规避，不做刚体模拟）。

## 3. 数据流

四条主链路。括号内为真实类型名，`:行号` 为代码锚点，答辩时可直接跳转。

### 3.1 一次喷洒结算（按住 K，每 0.25s 一个 tick）

```text
① 输入
   SprayController.Update → ReadInput()
     SprayHeld   = inputAllowed && Input.GetKey(KeyCode.K)
     ChargeHeld  = inputAllowed && Input.GetKey(KeyCode.L)
     └ inputAllowed 已排除：玩家 InputLocked / ExternalInputLock / UIManager.IsBlocking

② 进状态（由 M2 的 Idle/Run Tick 主动拉，SprayController 不反向驱动 FSM）
   PlayerIdleState / PlayerRunState.Tick
     if (_player.Spray.TryConsumeSprayStart()) → Machine.SwitchState<PlayerSprayState>()

③ Enter
   PlayerSprayState.Enter → Spray.BeginSpray()
       · Animator 播 Player_Interact
       · SprayHitbox 的 Collider2D 打开，按 flipX 摆 localPosition.x
       · AudioManager.PlaySfx(SfxId.SprayStart)（喷淋底噪，见 AudioDesign §3）
   期间每帧：MoveHorizontally(speedFactor: SpraySpeedFactor = 0.3)

④ 周期结算
   SprayController.UpdateSprayTicks（在 Update 内）
     _tickTimer += dt; while (_tickTimer >= SprayTickSeconds) { _tickTimer -= tick; SettleSprayTick(tick); }

⑤ 分流（同一帧同一目标只走其中一条）
   SettleSprayTick：
     foreach target in SprayHitbox.Targets（Enter 加 / Exit 减 / OnDisable 剔除，缓存组件引用）
       ├ IPurifiable            → ReceivePurify(round(SprayDPS × tick), player)   ← 垃圾、堵塞点、塔适配器
       └ IHittable 且 IsEnemyLayer → TakeDamage(DamageInfo{ Amount = SprayDPS×tick, IsPurify = true })
   扣料：PurifyTank.Consume(TankConsumePerSecond × tick ×（本 tick 有敌方目标 ? EnemySprayCostMultiplier : 1)）

⑥ 目标侧完成（GarbagePile.cs:69 起）
   ReceivePurify → _progress += amount
     if (_progress < PurifyRequired) { 颜色按进度插值；return; }
     → CompletePurify()：
         · SpriteRenderer.sprite = _cleanSprite（绿植）
         · _blocksPath 时 collider.enabled = false（通路打开）
         · fx_purify 经 PoolManager 取用，FxOneShot 播完自动归还
         · Publish(GarbagePurifiedEvent{ TargetId, PurityValue, Position })   (GarbagePile.cs:122)

⑦ 三个订阅方并行消费（互不依赖，这是本系统解耦的关键）
   PuritySystem.OnGarbagePurified (PuritySystem.cs:63)
       Current += evt.PurityValue → Broadcast() → Publish(PurityChangedEvent{ Current, Total, Percent, Pollution })
       if (Current >= Total && !_completed) { _completed = true; Publish(LevelPurifiedEvent); }
   AtmosphereController.OnPurityChanged
       目标色 = Lerp(CleanColor, PollutedColor, Pollution)；每帧向目标色 Lerp(AtmosphereLerpSpeed × dt)
       （只改 Camera.backgroundColor，不动渲染器）
   LevelObjectiveTracker.OnGarbagePurified
       按 TargetId 前缀分类计数：WaterBlock* → _waterCleared；Tower* → _purifiedTowers；其余 → _purifiedTargets
       → Evaluate() 推进目标链（详见 06-Level01 §3.1）

⑧ 收尾
   LevelManager.OnLevelPurified（仅 100% 路径）→ FinishLevel(true, "净化值达到 100%")
   目标链完成路径 → LevelObjectiveTracker.CompleteChain → 回调 → LevelManager.FinishLevel
```

**为什么 GarbagePurifiedEvent 要三个订阅方而不是一条链**：净化值（表现/进度）、目标链（流程）、关卡终止（终态）三者的失效方式不同——目标链可以夹取 Required，净化值不能夹；关卡终止要求幂等。串成一条链任一处抛错就全断。

### 3.2 一次蓄力净化波（长按 L 蓄满后松开）

```text
① PlayerIdleState / PlayerRunState → tryConsumeChargeStart() → PlayerChargeState
② PlayerChargeState：
     · 站定（StopHorizontal），进度 0 → 1 用 HeavyChargeSeconds(0.8s)
     · 期间忽略 K（喷洒与蓄力互斥，本状态不检查 SprayHeld）
     · ChargeHeld 抬起：
         未满 → 取消，直接回 Idle（不扣料）
         蓄满 → Spray.ReleasePurifyWave()
③ ReleasePurifyWave（SprayController.cs:247）：
     PurifyTank.Consume(HeavyCost=25) 失败 → 直接 return（回 Idle，不播动画）
     center/size 取自 SprayHitbox 当前世界包围盒
     Physics2D.OverlapBoxNonAlloc(center, size, 0f, WaveOverlaps)   ← 静态数组 24，零 GC（R10）
     foreach：
       IPurifiable → ReceivePurify(HeavyPurifyAmount=40)
       敌人 IHittable → TakeDamage(DamageInfo{ Amount = HeavyDamage=3, Knockback = HeavyKnockback=8, IsPurify = true })
     AudioManager.PlaySfx(SfxId.HeavyRelease)
     Animator 播 Player_HeavyAttack
④ PlayerChargeState 用 HeavyRecoverSeconds(0.5s) 做硬直 → 回 Idle
```

**同帧结算的含义**：上面 ③ 的伤害在动画起播前就已打完。对比 M3 近战必须等 `HitOn` 动画事件帧开窗。差别原因：近战是"表现窗口=判定窗口"的手感契约，AOE 是一次性脉冲，若也等事件帧，动画被打断就丢判定。

### 3.3 一次敌人死亡 → 掉落 → 拾取（当前三关为断链）

```text
① 敌人死亡
   EnemyDieState → Animator DieEnd 事件 → EnemyControllerBase.OnDieEnd()
     → BroadcastDeath() → Publish(EnemyDeathEvent{ EnemyTypeId, Position, DropTable })   (EnemyControllerBase.cs:437)
     → Recycle()（池借出走 PoolManager.Release，场景实例走 SetActive(false)）

② 掉落（DropSpawner，三关各挂 1 个）
   OnEnemyDeath(evt)：
     if (_dropTable == null) return;        ← 三关实测：Level01/02/03 的 _dropTable 全为 {fileID: 0}
     foreach entry in _dropTable.Entries：
        if (Random.value <= entry.Chance) → PoolManager.Get(entry.PickupPrefab) → 摆到 evt.Position ± 随机偏移

③ 拾取
   PurifyPickup.OnTriggerEnter2D(other)：
     other 是玩家 → PurifyTank.Refill(PickupTankAmount=20) → Publish(PickupCollectedEvent) → 归还池

④ 计数
   LevelStats 订阅 PickupCollectedEvent → PickupCount++（进结算面板）
```

> **诚实登记**：`②` 的前置条件当前不成立。`_dropTable` 三关全空、`PurifyPickup.prefab` 场景内 0 引用（仅作为 Prefab 资产存在），故整条链路实际是**空转**：敌人死亡只广播事件，不产生任何掉落。`DropSpawner` 与 `DropTable` 的代码是正确的，缺的是资产配置——登记在 README §5 与 TestPlan 已知问题。

### 3.4 一次净化剂耗尽

```text
PurifyTank.Current 降到 0
  → SprayController.SettleSprayTick 的 Consume 返回 false
  → 本 tick 不结算伤害/净化（并非半伤），但喷洒状态继续（不强制退出）
  → HUD 能量条见底闪烁（HUDController 拉取 PurifyTank.Current/Capacity）
  → 玩家只能靠拾取物回复；当前掉落链断 → 实际表现为"净化剂用完即死锁"
```

这条链是 `§3.3` 断链的**下游后果**，也是本轮把它列为 P0 缺口的原因：不是"少个掉落物"，是关卡可玩性有死锁风险（第一关 `InitialTank=50`、`TankConsumePerSecond=8` → 裸喷 6.25 秒见底）。

## 4. 数据契约

### 4.1 EcoConfig 字段表（单一事实来源；GDD §8 落地）

| 字段 | 默认值 | 说明 |
|---|---|---|
| `SprayDPS` | 2 | 喷洒净化量/秒（对垃圾）；同时是对敌伤害/秒（GDD §8） |
| `SprayTickSeconds` | 0.25 | 结算周期（4 次/秒，伤害取整向上） |
| `TankCapacity` | 100 | 净化剂容量（GDD §8） |
| `InitialTank` | 50 | 开局净化剂 |
| `TankConsumePerSecond` | 8 | 喷洒基础消耗/秒 |
| `EnemySprayCostMultiplier` | 2 | 命中敌方时消耗加倍（GDD §3.1） |
| `HeavyChargeSeconds` | 0.8 | 蓄力时长 |
| `HeavyPurifyAmount` | 40 | 净化波单目标净化量 |
| `HeavyDamage` | 3 | 净化波对敌伤害（≈1.5 倍夹钳三段总伤） |
| `HeavyKnockback` | 8 | 净化波击退 |
| `HeavyCost` | 25 | 净化波净化剂消耗 |
| `HeavyRecoverSeconds` | 0.5 | 释放后硬直 |
| `SprayMoveSpeedFactor` | 0.3 | 喷洒中移速因子 |
| `GarbagePurifyRequired` | 4 | 普通垃圾阈值（= 2 秒喷洒，GDD"长按 2 秒"） |
| `GarbageWallPurifyRequired` | 60 | 挡路垃圾阈值（蓄力波 ×1.5 次，普通喷洒不现实） |
| `GarbagePurityValue` | 5 | 普通垃圾净化值贡献（%，GDD 走读 +5%） |
| `GarbageWallPurityValue` | 10 | 挡路垃圾贡献 |
| `TowerPurityValue` | 10 | 塔净化贡献 |
| `TowerPurifyRequired` | 30 | 塔净化阈值（3.75 秒持续喷洒或蓄力波×1） |
| `PickupTankAmount` | 20 | 拾取回复量 |
| `PollutedColor` | 灰紫 `(0.45, 0.40, 0.52)` | 污染态背景色（GDD 主色板） |
| `CleanColor` | 自然绿 `(0.56, 0.74, 0.50)` | 清新态背景色 |
| `AtmosphereLerpSpeed` | 2 | 背景色平滑速率（1/s） |
| `GarbagePollutedTint` / `GarbageCleanTint` | 灰褐 / 淡绿 | 垃圾堆净化进度变色两端（完成时换 `garbage_pile_clean.png` 绿植） |

### 4.2 事件表（全部 struct，经 EventCenter，零 GC）

| 事件 | 载荷 | 发布方 | 订阅方 |
|---|---|---|---|
| `PurifyTankChangedEvent` | Current/Capacity | `PurifyTank` | **（无）** —— HUD 改为每帧轮询 `PurifyTank.Current/Capacity`，该事件成孤儿（SystemOverview §6） |
| `PurityChangedEvent` | Current/Total/Percent/污染度 | `PuritySystem` | `AtmosphereController`、M7 HUD |
| `GarbagePurifiedEvent` | PurityValue/位置/标识 | `GarbagePile`/`TowerPurifiable` | `PuritySystem` |
| `LevelPurifiedEvent` | — | `PuritySystem`（one-shot） | M6 关卡流程（M5 内部验证用） |
| `PickupCollectedEvent` | 类型/数量 | `PurifyPickup` | M7 HUD |
| （订阅）`EnemyDeathEvent` | DropTable/位置 | M4 既有 | `DropSpawner` |
| （订阅）`TowerDisableEvent` | 塔标识/位置 | M4 既有 | M6 检查点（M5 验证用） |

### 4.3 键位与动画映射

| 输入 | 键 | 玩家状态 | 动画（复用） |
|---|---|---|---|
| 喷洒（按住） | K | `PlayerSprayState` | `Player_Interact` |
| 蓄力（长按 L 松开释放） | L | `PlayerChargeState` → 释放播 `Player_HeavyAttack` | `Player_HeavyAttack` |
| 夹钳攻击 | J（M3 既有） | `PlayerAttackState` | `Player_Attack01~03`（不变） |

> `Player.controller` 已含 22 参数/22 状态（M3 提交记录），`Interact`/`HeavyAttack` 大概率已有参数与状态位；任务中"核对 + 缺则补"，参数名以 `PlayerAnimParams` 常量为准。

## 5. 设计取舍与风险

### 5.1 设计取舍

| 取舍项 | 选择 | 放弃的方案与理由 |
|---|---|---|
| 喷洒时的移动 | 减速（`SprayMoveSpeedFactor=0.3`） | 放弃"锁移动"：横版手感优先；站定喷在测试中易被敌人贴脸，惩罚过重 |
| 净化波的结算时机 | 同帧结算 | 放弃"走动画事件帧"：无连段时序；防动画被打断丢判定（M4 事件帧模式适用于"表现窗口=判定窗口"的近战，不适合瞬发 AOE） |
| 敌人是否可被净化秒杀 | 只受伤（走 `DamageInfo`） | 放弃"净化即死"：GDD 仅承诺"持续伤害 + 消耗加倍"；秒杀会让战斗系统失去意义 |
| 净化进度中断 | 不衰减 | 放弃"衰减/清零"：防"差 0.1 秒清零重来"的挫败；GDD "长按 2 秒"是时长上限不是计时器 |
| 塔的净化方式 | 两条路径并存：攻击击毁（`Tower01Controller` HP 归零）或持续喷洒（`TowerPurifiable` → `RequestDisable()`），都汇到 `TowerDisableEvent` | 放弃"只能净化"：塔作为战斗单位要能被正常打死，否则玩家在只有塔的场景里无事可做 |
| 喷洒判定盒的目标维护 | `OnTriggerEnter/Exit` 维护列表 + `OnTriggerStay` 兜底 | 放弃"纯 Stay 每帧查询"：Stay 每帧回调且顺序不定；列表缓存组件引用可做到零每帧 `GetComponent` |
| 氛围表现 | 只插值 `Camera.backgroundColor` | 放弃"全场景 renderer tint"：需遍历渲染器，收益低成本高；内置管线零成本方案已足够表达"星球在恢复" |
| 垃圾堆美术 | 真素材 + 进度颜色插值 + 完成换绿植贴图 | 放弃"仅颜色变化"：完成瞬间换贴图才有"净化成功"的明确反馈 |

### 5.2 风险

| # | 风险 | 概率 | 影响 | 现状与对策 |
|---|---|---|---|---|
| E1 | **掉落链断链 → 净化剂死锁** | 已发生 | 高（关卡可玩性） | 三关 `_dropTable` 全 null、`PurifyPickup` 场景 0 引用。短期：`InitialTank=50` 足够打完 L1（实测可行）；根治：给三关配 `DropTable` 资产（P0，登记于 TestPlan） |
| E2 | 池化对象中途 `SetActive(false)` 导致喷洒列表持死引用 | 中 | 中（NRE） | 目标方 `OnDisable` 主动剔除 + 结算前判 null（已实现） |
| E3 | `SprayHitbox` 镜像依赖每帧写 `localPosition.x` | 低 | 低 | 沿用 M4 FirePoint 教训；`flipX` 与判定盒同帧更新，实测无错位 |
| E4 | 蓄力波 `OverlapBoxNonAlloc` 固定 24 容量，怪物密集时截断 | 低 | 低 | 第三关同屏敌人 ≤ 6（`LevelSpawner` 上限），24 有 4 倍余量；若后续加量需扩容 |
| E5 | 净化值与目标链双计数口径不一致（一个按 `PurityValue` 加权、一个按个数） | 中 | 中（HUD 与面板数字不符） | 现状是可解释的：目标链数"清掉几个"，HUD 数"净化了百分之几"。已在 HUD 上用不同文案区分 |
| E6 | `TowerPurifiable` 与攻击击毁双路径，可能重复计 `_purifiedTowers` | 低 | 低 | `RequestDisable()` 幂等（M4 保证），`TowerDisableEvent` 只广播一次；`GarbagePurifiedEvent` 的 `Tower*` 前缀分支与 `TowerDisableEvent` 分支可能都涨 1 —— 但三个关卡配置都没有 `PurifyTowers` 目标，故当前不触发 |

## 6. 与 requirements 的偏差说明

`requirements.md` 写于 M5 立项，以下是落地后与它不一致的地方。**以本节为准**，`requirements.md` 只在答辩追溯时作为历史记录。

| 编号 | requirements 原文口径 | 实际实现 | 原因 |
|---|---|---|---|
| R1 | "净化目标统一接口 `IPurifiable`" | 实现为 `IPurifiable` + `IResettablePurifiable`（后者多一个 `ResetPurify()`） | 编辑器 Setup 工具与场景重载需要复位入口；接口拆分避免所有实现都背一个用不到的复位方法 |
| R5 | 塔通过 `TowerPurifiable` 净化 | 攻击击毁与净化关闭**两条路径**，都汇到 `TowerDisableEvent`；`LevelObjectiveTracker` 同时认 `GarbagePurifiedEvent` 的 `Tower*` 前缀与 `TowerDisableEvent` | 塔先作为战斗单位加入（M4），玩家预期"能打死"；净化是补的环保表达 |
| R6 | 净化值 100% 由 `LevelPurifiedEvent` 通知关卡 | 100% 与"目标链完成"**两个判据并存，先到者为准** | 第三关目标链不含 `ReachPurity`（打完 Boss 即通关），只留 100% 判据会卡关 |
| R8 | 喷洒对敌人是"持续伤害" | 实现带 `IsPurify=true` 标记，Boss 侧按 `PurifyDamageMultiplier` 放大（见 08-Boss） | 让"喷"在 Boss 战有独立价值，否则第三关的喷雾键是废键 |
| R9 | 掉落表与拾取物接入关卡 | **代码完整，资产未配**（`_dropTable` 全 null） | 排期未到；不谎报为"已接入" |
| — | （requirements 未提） | 新增 `KnowledgeCardPickup` / `WaterBlockage` / `GrassZone` / `BreakableProp` / `BreakableDebris` | M9 第二/三关需求倒灌进 M5 目录（同属 `StarEco.Eco` 命名空间），见 09-Level02 §3 |
