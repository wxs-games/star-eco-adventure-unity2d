# 技术方案（SDD）：03-Combat

> 对应 `requirements.md` R1~R8 | 命名空间 `StarEco.Player` / `StarEco.Combat` | 11 文件 / 1,272 行
> 版本：v2.0（**修正过时设计** + 补投掷/打击感/连段实况） | 2026-10-30
> ⚠️ 本版重写了 v1.x 的两处结论：①"原地攻击、新方向输入可取消"已于 2026-10-28 推翻，改为**边跑边打**；②第三段不再是纯近战，改为**投掷钳子（回旋镖式）**
> 上游：`02-PlayerController`（状态机 / `PlayerToolAnchor`）、`docs/SystemOverview.md` §8.2（伤害流）
> 下游：`04-EnemyAI`（`IHittable` 消费方）、`08-Boss`

---

## 1. 结构

### 1.1 与 M2 的边界

| 复用 M2 | 新建 | 改动 M2 的地方 |
|---|---|---|
| `PlayerStateMachine`、`PlayerStateBase`、`PlayerAnimatorDriver`、`PlayerConfig`（无敌/击退参数） | `DamageInfo` / `IHittable` / `CombatConfig` / `PlayerCombat` / `PlayerAttackState` / `AttackHitbox` / `HitFeel` / `HitFlash` / `PlayerToolThrow` / `ToolProjectile` | `PlayerController` 加攻击输入分支与 `InputLocked` 判定；`PlayerHurtState` / `PlayerDeathState` 从占位改为吃 `DamageInfo` |

**边界原则**：M2 管"怎么动"，M3 管"怎么打、怎么挨打"。战斗系统**不修改 M2 的移动逻辑**，只在 `PlayerAttackState` 里接管移动（调用 `MoveHorizontally`），退出时交还。

### 1.2 文件职责表

| 文件 | 行 | 职责 |
|---|---:|---|
| `DamageInfo.cs` | 44 | 伤害数据结构：`Amount` / `KnockbackDir` / `Source`（全项目唯一的伤害描述） |
| `IHittable.cs` | 11 | 唯一接口：`void TakeDamage(DamageInfo info)` |
| `CombatConfig.cs` | 72 | `ScriptableObject`：HP/伤害/连段窗/投掷全套参数（22 个字段） |
| `PlayerCombat.cs` | 94 | 实现 `IHittable`：HP、无敌计时、死亡分发 |
| `PlayerAttackState.cs` | 199 | 地面三段 + 空中单发 + 投掷释放时机 + 移动接管 |
| `AttackHitbox.cs` | 144 | 攻击判定框：按段开窗、同窗去重、第三段整段不开窗 |
| `PlayerToolThrow.cs` | 112 | 投掷协调者：暂停工具显隐、生成 `ToolProjectile`、回收后恢复 |
| `ToolProjectile.cs` | 346 | 回旋投掷物：出程 → 折返 → 归位，`CircleCastNonAlloc` 逐帧扫 |
| `HitFeel.cs` | 76 | 打击感：顿帧 45ms + 震屏 0.05 |
| `HitFlash.cs` | 127 | 受击闪白（SpriteRenderer 颜色闪 + 缓回） |
| `TestTarget.cs` | 47 | 开发期靶子（无 AI 的 `IHittable`） |

### 1.3 Prefab 增量

```
Player（M2 基础上加）
 ├─ PlayerCombat          HP / 无敌 / 死亡
 ├─ AttackHitbox（子物体） BoxCollider2D Trigger，默认 disabled
 │    └─ 靠 flipX 左右镜像：_hitboxLocalPosition.x 取 Abs 后按朝向乘符号
 └─ ToolAnchor（子物体）   钳子 visual + PlayerToolAnchor（[ExecuteAlways]）
      └─ ToolProjectile    投掷时从池里取，父节点临时挂到场景根
```

---

## 2. 关键机制

### 2.1 三段连击（R2）

```
按 J（地面） → PlayerAttackState(seg=1) → SetTrigger("Attack")
  ├─ _comboWindowTimer = ComboWindowSeconds (0.15)
  ├─ 窗口内再按 J → _nextQueued = true
  ├─ 本段结束 → 若 _nextQueued → seg=2 → SetTrigger("Attack2") → 窗口重置
  └─ seg=3 → SetTrigger("Attack3") + 播到 0.19s → PlayerToolThrow.TryThrow()
空中按 J → seg=0 → PlayOneShot("AttackAir")，单发不进链
```

**段时长**（`CombatConfig.SegmentEndTimes`，索引 0 为空中）：

| 段 | 时长 | 伤害 | 备注 |
|---|---:|---:|---|
| 空中（0） | 0.25s | 2 | 单发 |
| 一段（1） | 0.3333s | 2 | |
| 二段（2） | 0.3333s | 2 | |
| 三段（3） | 0.375s | 0（判定框） + 3（投掷物） | **命中伤害全交给投掷物** |

**第三段的特殊性**：`AttackHitbox.OnAttackWindowOpen()` 内部第一件事就是

```csharp
if (CurrentSegment == 3) { _hitThisWindow.Clear(); if (_hitbox != null) _hitbox.enabled = false; return; }
```

即**第三段整段不开近战窗**。原因：第三段的动作是"把钳子扔出去"，手臂已经离身，近战判定框继续开会在身前产生一个"看不见的伤害区"，与投掷物的飞行判定重复计算伤害。

### 2.2 边跑边打（⭐ 2026-10-28 改动，v1.x 文档未记录）

v1.x 的设计是"原地攻击，新按方向输入立即取消攻击"。**已推翻**：

| 项 | v1.x（过时） | 现在 |
|---|---|---|
| 攻击期间移动 | `StopHorizontal()`，原地 | `MoveHorizontally(!IsGrounded)` —— 地面全速、空中按 `AirControlRatio` |
| 新按方向 | 取消攻击，切 Run | **不取消**，攻击继续，人照走 |
| 取消方式 | 方向键 | 只有受击 / 死亡 / 落地状态切换能打断 |

改动理由：原地攻击在横版动作游戏里会让"打一下挪两步"变得笨重；实测第 3 段 0.375s 定身期间玩家会被敌人的位移攻击白打一次，容错过低。

> ⚠️ **这条改动没写进 requirements/design，只在代码里**。本版补入，并在 requirements 加 R9。

### 2.3 投掷钳子（R9，v1.x 完全没有）

**触发**：`PlayerAttackState` 第三段播到 `ThrowReleaseTime = 0.19s`（24fps 下约第 5 帧，手臂挥到最前）时调 `Player.ToolThrow.TryThrow()`。

**飞行逻辑**（`ToolProjectile`，346 行，是本系统最复杂的类）：

```
Launch(worldPos, facing, FlightParams, onReturned)
  → 出程：沿 facing 直线飞，速度 ThrowSpeed(17)，最多 ThrowRange(5.5u)
         （飞行距离用累计位移判，不用计时——避免改速度后射程漂移）
  → 终止条件（任一）：飞满射程 / 撞墙（CircleCast 命中 Ground 层）/ 超时(ThrowMaxFlightTime 4s)
  → 折返：朝 ReturnTarget（= ToolAnchor）飞，速度 ThrowReturnSpeed(22)
  → 归位：距离 < 阈值 → 回调 onReturned → 回池
```

**判定方式**：**不用物理碰撞**，每个 `FixedUpdate` 沿"上一位置 → 当前位置"做一次 `Physics2D.CircleCastNonAlloc`（半径 `ThrowHitRadius = 0.26`）。原因：

- 速度 17 u/s ÷ 50Hz = 0.34 u/步，而敌人体宽约 0.8u——用 `OnTriggerEnter2D` 理论上够，但折返瞬间位置会跳变，容易漏判；
- `CircleCast` 是连续检测，高速不穿透；
- `NonAlloc` 版本零 GC，符合性能红线。

**同一目标只打一次**：`HashSet<IHittable>` 记录本次飞行已命中者，出程折返全程共用同一个集合（即**回程不再打已经打过的敌人**）。

**投掷期间工具显隐**：`PlayerToolThrow` 在投掷期间把工具 `SpriteRenderer.enabled = false`，**不能 `SetActive(false)`**——因为 `.anim` 每帧写 `m_IsActive` 曲线会立刻把它设回来。这是武器解耦架构的硬约束（`SystemOverview.md` §9 #3）。

### 2.4 攻击判定框（R3）

- `AttackHitbox` 持有 `Collider2D`，默认 `enabled = false`。
- **开窗由动画事件驱动**：clip 里打 `AttackWindowOpen(seg)` / `AttackWindowClose(seg)`，不用定时器。改帧只需改 clip 事件，代码零改动。
- **同窗去重**：`HashSet<IHittable> _hitThisWindow`，`OnTriggerEnter2D` 时先查集合，命中过的跳过。开窗时清空。
- **左右镜像**：`_hitboxLocalPosition.x` 存绝对值，运行时按 `SpriteRenderer.flipX` 乘符号。
- **退出必关窗**：`PlayerAttackState.Exit()` 无条件调 `OnAttackWindowClose()`，防止"受击打断后人被推开、判定框还留在原地"。

> ⚠️ **帧号是硬编码在 clip 里的**。重生成动画（帧数变化）必须重打事件帧，否则开窗时机整体偏移。`TestPlan.md` 的动画验收用例专门查这个。

### 2.5 受击与无敌（R5）

`PlayerCombat` 实现 `IHittable`：

```
TakeDamage(info):
  if (InvincibleTimer > 0) return;              // 无敌期完全忽略，不扣血不击退
  HP -= info.Amount;
  if (HP <= 0) → SwitchState(Death)
  else {
      InvincibleTimer = PlayerConfig.HurtInvincibleTime (1.0)
      SwitchState(Hurt)                          // Hurt.Enter 里吃 info.KnockbackDir
  }
```

- **无敌闪烁**：12Hz（≈0.083s）切换 `SpriteRenderer.alpha`，由 `PlayerHurtState.Tick` 内计算，**不用协程**（协程在状态切换时会被停掉，导致 sprite 永久半透明）。
- **无敌计时器**：`PlayerController.InvincibleTimer` 在 `Update` 里递减——放在控制器而非状态里，因为它要跨状态存活（Hurt 结束后无敌还没结束）。

### 2.6 打击感（R6）

`HitFeel.Play()` 两个效果叠加，**不引入后处理**：

| 效果 | 实值 | 实现 |
|---|---|---|
| 顿帧 | 45ms | `EditorApplication` 之外用 `Time.timeScale = 0` 一小段协程（`StopFrames`），帧数按 `Mathf.Max(0.01f, seconds)` 取整 |
| 震屏 | 0.05 幅度 / 0.1s | 调 `CameraFollow2D.Shake(max(0.08, stopSeconds*2.2), amplitude)` |

`HitFlash` 负责闪白：命中瞬间把 `SpriteRenderer.color` 置白，然后 `MoveTowards` 缓回原色（约 0.08s）。

---

## 3. 数据流

### 3.1 一次命中（近战）

```
按 J → PlayerController.AttackPressedThisFrame
 → SwitchState(PlayerAttackState)
 → Enter: seg=1, comboWindow=0.15, SetTrigger("Attack"), 播挥击音（±5% 变调）
 → clip 播到开窗帧 → OnAttackWindowOpen → hitbox.enabled = true
 → OnTriggerEnter2D(enemy) → enemy 实现 IHittable？
      → TakeDamage(DamageInfo{ 2, facing, player })
      → 入 _hitThisWindow（同窗不再打）
      → HitFlash 闪白 + HitFeel.Play() → 顿帧 45ms + 震屏
      → enemy.HP ≤ 0 → 敌人死亡 → Publish(EnemyDeathEvent)
 → 关窗帧 → OnAttackWindowClose → hitbox.enabled = false
 → 结束帧 → 若 _nextQueued → seg=2 ...
```

### 3.2 一次命中（投掷，第三段）

```
seg=3 Enter: SetTrigger("Attack3"), _throwCountdown = 0.19
 → FixedTick 里 _throwCountdown -= dt
 → ≤0 → PlayerToolThrow.TryThrow()
      → 隐藏工具 SpriteRenderer（不改 active）
      → 从 PoolManager 取 ToolProjectile
      → Launch(pos, facing, { ThrowSpeed 17, Range 5.5, HitRadius 0.26, … }, onReturned)
 → 投掷物每 FixedUpdate：CircleCastNonAlloc 沿路径扫
      → 命中未打过的 IHittable → TakeDamage({3, 飞行方向, player})
 → 出程结束 → 朝 ToolAnchor 折返
 → 归位 → onReturned → 池回收 + 恢复工具 SpriteRenderer
```

### 3.3 一次玩家受击

```
Enemy.Attack → PlayerCombat.TakeDamage(info)
 → InvincibleTimer > 0 ? return : HP -= Amount
 → HP > 0 → SwitchState(Hurt)
      → Enter: velocity = -KnockbackDir * HurtKnockback(6)
               InputLocked = true
               InvincibleTimer = 1.0
      → Tick: 12Hz 闪 alpha；计时到 → SwitchState(Idle/Air)
 → HP ≤ 0 → SwitchState(Death) → 播死亡动画 → LevelManager 判 Failed → 整关重载
```

---

## 4. 设计取舍

| 取舍 | 选择 | 理由 | 代价 |
|---|---|---|---|
| 开窗用动画事件 vs 定时器 | **动画事件** | 动画帧率就是攻击节奏的权威；改帧不用改代码 | 帧号硬编码在 clip 里，重生成动画必须重打事件 |
| 第三段伤害交给投掷物 vs 近战+投掷双份 | **只给投掷物** | 双份会在身前出现"看不见的伤害区"，且三段总伤害会失衡 | 第三段贴脸打反而没有近战伤害，手感上"第三下必须扔出去" |
| 投掷用 CircleCast vs 物理碰撞 | **CircleCastNonAlloc** | 高速不穿透、零 GC、折返跳变不出漏判 | 需要手写"同目标只打一次"，且不与物理层矩阵联动 |
| 攻击期间可移动 vs 原地 | **可移动**（10-28 改） | 横版动作游戏里定身打击容错太低 | 攻击中位移会让"打一下挪两步"变成"边跑边砍"，节奏偏快 |
| HP 放 `PlayerCombat` vs `PlayerConfig` | **`PlayerCombat`** | Config 是静态手感参数，HP 是运行时状态 | 无法在 Inspector 里改初始血量（只能在 CombatConfig 改 `MaxHP`） |
| 无敌计时放控制器 vs 状态内 | **控制器** | 要跨状态存活（Hurt 结束无敌未结束） | 控制器字段增多 |

---

## 5. 风险

| # | 风险 | 触发条件 | 现状 |
|---|---|---|---|
| 1 | 动画事件帧号硬编码 | 重生成动画 / 改帧数 | 需按 `TestPlan.md` 逐段手测开窗时机 |
| 2 | 第三段"贴脸没伤害"的认知落差 | 玩家在贴脸时用第三段 | 无教学提示；属于设计选择而非 bug，但玩家会误以为是 bug |
| 3 | 投掷物归位目标为 null | 玩家在飞行途中死亡 / 换场景 | `ToolProjectile` 里 `if (_p.ReturnTarget == null)` 走超时回收；但工具可能停在隐藏状态 |
| 4 | 顿帧用 `Time.timeScale = 0` | 顿帧期间暂停游戏 | 暂停菜单在顿帧窗口内打开会让 timeScale 归 1 异常；窗口仅 45ms，实际未复现 |
| 5 | `AttackHitbox` 的 collider 自动查找 | 预制体结构变动 | `Awake` 里 `GetComponents<Collider2D>()` 找第一个非 Trigger 的；若新增 collider 会取错 |
| 6 | 边跑边打与"取消攻击"旧逻辑残留 | 有旧分支仍在 | v2.0 已确认 `PlayerAttackState.FixedTick` 无 `StopHorizontal`、无取消分支 |

---

## 6. 与 requirements 的偏差说明

| 编号 | requirements 原文 | 实现 | 说明 |
|---|---|---|---|
| R1 | 三段近战连击 + 空中攻击 | ✅ 一致 | |
| R2 | 连段输入窗口 0.15s | ✅ 一致 | `ComboWindowSeconds` 可调 |
| R3 | 攻击开窗由动画事件控制 | ✅ 一致 | |
| R4 | 同一目标同窗只受一次伤害 | ✅ 一致 | `HashSet<IHittable>` |
| R5 | 受击无敌 1s + 闪烁 | ✅ 一致 | |
| R6 | 打击感（顿帧 + 震屏） | ✅ 一致 | 45ms / 0.05 |
| R7 | 统一伤害结构 `DamageInfo` | ✅ 一致 | 玩家/敌人/Boss/道具共用 |
| R8 | 多态目标 `IHittable` | ✅ 一致 | |
| **R9** | **（原需求未提）** | **第三段 = 投掷钳子 + 边跑边打** | 2026-10-28 新增。原 v1.x 写"原地攻击、移动输入可取消"，已推翻。本版补入 requirements |
| — | （原需求未提） | `HitFlash` / `TestTarget` | 闪白为打击感补充；`TestTarget` 为开发期靶子，**Build 里不带**（无场景引用） |
