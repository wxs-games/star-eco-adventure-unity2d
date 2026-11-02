# 技术方案（SDD）：02-PlayerController

> 对应 `requirements.md` R1~R5 | 命名空间 `StarEco.Player` | 7 文件 / 2,446 行
> 版本：v2.0（M2 骨架 → 落地后全量重写） | 2026-10-30
> 上游：`docs/SystemOverview.md` §3.1（场景对象）/ §9（10 条硬约束）
> 下游：`03-Combat`（攻击/受击）、`06-Level01`（平台与手感验收）

---

## 1. 结构

### 1.1 与 M1/M3 的边界

| 复用 M1 | 本里程碑新建 | 为 M3 预留 |
|---|---|---|
| `MonoSingleton`、`EventCenter`（受击事件） | 控制器 / 状态机 / 14 个状态 / 配置 SO / 动画驱动 / 工具锚点 | `PlayerAttackState` 的状态槽、`AttackHitbox` 子物体挂载点、`PlayerCombat` 的 `IHittable` 实现 |

**边界原则**：M2 只管"怎么动"，不管"怎么打"。所以 `PlayerController` 里**没有一行伤害代码**，攻击只是"切到一个状态"。这条边界让 M3 的战斗系统能独立开发并回归测试。

### 1.2 文件职责表

| 文件 | 行 | 职责 |
|---|---:|---|
| `PlayerConfig.cs` | 144 | `ScriptableObject`：36 个手感参数 + 派生属性（`DodgeSpeed` / `DashSpeed`） |
| `PlayerController.cs` | 1,144 | 输入读取、`Rigidbody2D` 运动、土狼/缓冲计时、朝向、状态机持有、公开上下文（`Anim` / `Combat` / `IsGrounded` / `InputLocked`…） |
| `PlayerStateMachine.cs` | 70 | 通用 FSM：`Dictionary<Type, IPlayerState>` 注册表 + `SwitchState`；接口 `IPlayerState { Enter / Exit / Tick / FixedTick }` 定义在同文件顶部 |
| `PlayerStateBase.cs` | 165 | 状态基类：持 `Player` + `Machine`，提供 `Config` / `Body` / `Anim` / `MoveHorizontally` / `StopHorizontal` 等公共操作 |
| `PlayerStates.cs` | 692 | **11 个状态类挤在一个文件**（见 §4 取舍说明） |
| `PlayerAnimatorDriver.cs` | 131 | 每帧把 `Speed` / `IsGrounded` / `VelocityY` / `IsLongIdle` 写入 Animator |
| `PlayerToolAnchor.cs` | 100 | `[ExecuteAlways]` 工具锚点：排序跟随、握持点对齐（M3 武器解耦的前置） |

### 1.3 Prefab 结构

```
Player（Prefab）
 ├─ Rigidbody2D          Dynamic，FreezeRotation Z，材质 PlayerFrictionless（零摩擦）
 ├─ BoxCollider2D        主体
 ├─ CapsuleCollider2D    脚部（蹲伏时改 height）
 ├─ PlayerController     ← 唯一 Update / FixedUpdate 入口
 ├─ PlayerStateMachine   （纯 C# 类，非 MonoBehaviour）
 ├─ PlayerAnimatorDriver
 ├─ PlayerCombat         （M3 加）
 ├─ SpriteRenderer / Animator
 ├─ GroundChecker        子物体 + LayerMask
 └─ AttackHitbox         子物体（M3 加，Trigger，默认 disabled）
     └─ ToolAnchor        挂钳子的锚点（M3 加）
```

> ⚠️ **`PlayerFrictionless` 零摩擦材质是全局性约束**：玩家站在任何平台上都不会被摩擦力带走，因此移动平台必须**逐帧把位移直接加到 `Body.position`**（而不是给玩家加速度）。这条约束在 `MovingPlatform` 里体现，也在 `SystemOverview.md` §9 的硬约束 #4。

---

## 2. 关键机制

### 2.1 状态机（R1）

**14 个状态，全部在 `PlayerController.Awake` 里一次性注册**：

```
Idle / Run / Air / Land / Dodge / Dash / Crouch / WallSlide / Ledge
Attack / Spray / Charge / Hurt / Death
```

`PlayerStates.cs` 里定义 11 个；`PlayerAttackState`（Combat）、`PlayerSprayState` / `PlayerChargeState`（Eco）分居各自系统目录，因为它们依赖战斗/环保系统。

**为什么把 11 个状态塞进一个 692 行文件**：状态类平均 60 行，彼此高度同构（`Enter` 设动画、`Tick` 判转移、`Exit` 清状态）。拆成 11 个文件会让"读完一个状态的转移条件"需要跳 11 次；合并后可以在一个文件里横向对比所有转移，改手感时不用来回跳。代价是文件偏长——这是**刻意的可读性取舍**。

**`SwitchState` 语义**：`old.Exit()` → `_current = new` → `new.Enter()`。**没有转移优先级表**——优先级由"谁先调用"决定，即 `Tick` 里 `if` 的顺序：

```
Death > Hurt > Dodge/Dash（无敌位移） > Attack/Spray/Charge（战斗） > Air > Crouch > WallSlide > Ledge > Land > Run > Idle
```

### 2.2 手感四件套（R2）

| 机制 | 字段 | 实值 | 实现 |
|---|---|---:|---|
| **土狼时间** | `CoyoteTime` | 0.1s | `_coyoteCounter` 在触地时刷新，离地后 `-= dt`；跳跃消耗置 0 |
| **跳跃缓冲** | `JumpBufferTime` | 0.1s | `_jumpBufferCounter` 按下时刷新；`FixedUpdate` 触地且缓冲 > 0 → 起跳并清零 |
| **可变跳高** | `JumpCutMultiplier` | 0.5 | 松开跳跃键时若 `velocity.y > 0` → `velocity.y *= 0.5` |
| **非对称重力** | `GravityScale` / `FallGravityMultiplier` / `MaxFallSpeed` | 3 / 1.6 / 20 | 下落时 `gravityScale` 从 3 平滑 `MoveTowards` 到 4.8（升得快、落得沉） |

实测包线（`LevelDesign.md` §1）：**最大跳高 2.87u、单跳滞空 0.79s、满速水平跳距 4.75u**。这三个数字是所有关卡平台间距的设计依据——改 `JumpVelocity` 或 `MoveSpeed` 会让三个关卡的可达性同时失效。

### 2.3 移动与输入（R1）

- **输入源**：旧版 Input Manager（`GetAxisRaw("Horizontal")` / `GetButtonDown("Jump")`），零依赖。
- **输入采集在 `Update`，物理写入在 `FixedUpdate`**。`Update` 只把 `HorizontalInput` 缓存成字段，`FixedUpdate` 读字段。
- **加速度**：`Acceleration = 60`，从 0 到 6 u/s 约 0.1s——几乎瞬间到位，但保留一点起步手感。空中按 `AirControlRatio = 0.85` 打折。
- **朝向**：`SpriteRenderer.flipX` 由 `HorizontalInput` 符号决定，输入为 0 时不改（防止停下瞬间翻转）。

### 2.4 扩展状态（R3/R4）

M2 后期一次补齐了 6 个"非必需但完整度高"的状态，全部有对应美术帧：

| 状态 | 触发 | 关键参数 | 备注 |
|---|---|---|---|
| `Dodge` | Shift + 方向 | 0.333s / 3u / 无敌 0.33s / CD 0.6s / 空中 1 次 | 有 `DodgePassThroughLayers`（默认 Layer 9）——**翻滚时忽略该层碰撞** |
| `Dash` | Shift 无方向 | 0.4s / 2.8u / CD 0.5s / 空中 1 次 | 无无敌帧（与 Dodge 区分：Dash 是位移，Dodge 是闪避） |
| `Crouch` | 按住下 | 移速 ×0.45 / 碰撞高 ×0.62 | 改 `CapsuleCollider2D.height`，**松开必须还原，否则卡在低矮空间** |
| `WallSlide` | 贴墙下落 | 下滑限速 4 / 墙检距离 0.1 | 触墙且 `velocity.y < 0` 时进入 |
| `Ledge` | 抓边缘 | 高度窗 0.35~1.7 / CD 0.4s | **当前无关卡用到**（无设计好的抓取点），属预留 |
| `Land` | 落地 | `LandLockTime` 0.06s | 锁移动 0.06s，避免落地瞬间抖动 |

> ⚠️ **`Ledge` 是零使用状态**：三个关卡的平台边缘都没有配置抓取点，玩家永远进不了这个状态。它在控制器里注册、有美术帧、能跑，但没有关卡内容承接。答辩若被问"你做了哪些没用到的东西"，这是可诚实回答的一项。

### 2.5 动画驱动（R5）

`PlayerAnimatorDriver.Update()` 每帧写 4 个参数：

| 参数 | 类型 | 来源 |
|---|---|---|
| `Speed` | float | `Mathf.Abs(Body.velocity.x)` |
| `IsGrounded` | bool | `Controller.IsGrounded` |
| `VelocityY` | float | `Body.velocity.y`（用于 Jump/Fall 分支） |
| `IsLongIdle` | bool | `UpdateLongIdle()`：站定 10s 触发（**M9 后补，之前是有转移无来源的死状态**） |

其余 Trigger（`Attack` / `Hurt` / `Dash` / `Interact` / `Death` …）由各状态在 `Enter` 时 `SetTrigger`，**不走 Driver**——Trigger 是"事件"，不该每帧写。

Animator 控制器共 **20 参数 / 22 状态**，由 `Editor/PlayerAnimationSetup.cs` 从美术帧目录一键生成（菜单 `StarEco/Setup/Player Animation`），12fps 采样、幂等重建。

> ⚠️ **状态名必须与代码常量一致**。项目用 `PlayerAnimParams` 静态常量类统一，但**Animator 里改状态名不会编译报错**，只会在运行时静默不播。改 Animator 后必须跑 `TestPlan.md` 的动画验收用例。

### 2.6 工具锚点排序（M3 前置，`[ExecuteAlways]`）

`PlayerToolAnchor` 存在的原因是一个具体 bug：角色 `SpriteRenderer.sortingOrder` **在预制体里是 0，进场景后被脚本改成 15**（`FixAllLevelSpriteSorting.GuessOrder()` 的兜底值）。工具若按"父 order + 1"在编辑期固化，就会得 1，进场景被角色整块盖住。

解法是三层保险：
1. `BuildPrefab` 里工具 role 直接设 15；
2. 排序脚本加 `toolanchor = 16`；
3. 运行时 / 编辑模式 `LateUpdate` 每帧跟随 `_body.sortingOrder + 1`。

**必须 `[ExecuteAlways]`**，否则编辑模式看不到工具。但 `[ExecuteAlways]` 的 `LateUpdate` 在编辑器不重绘时不跑——所以不能只靠它，三层缺一不可。

---

## 3. 数据流

### 3.1 一帧的完整执行顺序

```
Update（每渲染帧）
  ├─ ReadInput()           缓存 HorizontalInput / JumpPressedThisFrame / AttackPressedThisFrame
  ├─ 刷新 _coyoteCounter / _jumpBufferCounter
  ├─ _machine.Tick(dt)     各状态判转移（若切换 → old.Exit → new.Enter）
  └─ PlayerAnimatorDriver.Update()  写 4 个持续参数

FixedUpdate（每物理步，默认 50Hz）
  ├─ 若 InputLocked → velocity.x = 0，直接 return
  ├─ _machine.FixedTick(dt)   各状态做物理写入
  │    └─ 通用：MoveHorizontally() 改 velocity.x；Air 状态改 gravityScale
  └─ 落速钳制（velocity.y < -MaxFallSpeed → 钳到 -20）
```

**为什么所有物理写入都在 `FixedTick`**：`PlayerController.FixedUpdate` 只做"分发"，具体速度由状态类改。这样"某个状态要特殊物理"（Dodge 的固定速度、WallSlide 的限速）不需要在控制器里加 `if`，符合开闭原则。

### 3.2 一次跳跃

```
玩家按 J
 → Update: JumpPressedThisFrame = true，_jumpBufferCounter = 0.1
 → FixedUpdate → Idle.FixedTick：
      if (_jumpBufferCounter > 0 && (IsGrounded || _coyoteCounter > 0))
          velocity.y = JumpVelocity (13)
          SwitchState(Air) → Air.Enter → PlayOneShot("Jump")
 → 玩家松开 J（若 velocity.y > 0）
 → Air.Tick: velocity.y *= JumpCutMultiplier (0.5)   ← 可变跳高
 → velocity.y < 0 → Animator VelocityY 分支 → 播 Fall 帧
 → 落地 → IsGrounded true → Air.Tick → SwitchState(Land) → 锁 0.06s → Idle
```

### 3.3 一次受击（M3 接入后的路径）

```
Enemy.TakeDamage → PlayerCombat.TakeDamage(DamageInfo)
 → 若 InvincibleTimer > 0 → return（R5 无敌）
 → HP -= info.Amount → HP > 0 ? SwitchState(Hurt) : SwitchState(Death)
 → Hurt.Enter: velocity = -info.KnockbackDir * HurtKnockback(6)
               InputLocked = true
               InvincibleTimer = 1.0（12Hz 闪烁 alpha）
```

---

## 4. 设计取舍

| 取舍 | 选择 | 理由 | 代价 |
|---|---|---|---|
| Jump/Fall 分开 vs 合并 `Air` | **合并为 `PlayerAirState`**，动画由 `VelocityY` 分支 | 物理上是同一个自由落体过程；状态数减半；论文可写"状态内聚性" | 状态内动画切换要判 `VelocityY`，比分开略绕 |
| 状态类拆文件 vs 合并 | **11 个合并在 `PlayerStates.cs`** | 转移条件能横向对比，调手感时不用跳 11 次 | 单文件 692 行 |
| 输入用旧版 vs 新 Input System | **旧版 Input Manager** | 零依赖、零包体；答辩不会被问"为什么装了个包只用一点" | 无法做按键重绑定 UI；无法做手柄 |
| 状态机写在 `MonoBehaviour` vs 纯 C# 类 | **纯 C# 类**，由 `PlayerController` 持有 | 状态不需要 `Update`（由控制器统一分发），避免 14 个 `MonoBehaviour` 的调用开销 | 状态无法在 Inspector 里看（调试靠日志） |
| 优先级用表 vs 用 `if` 顺序 | **`if` 顺序** | 直观、改优先级就是挪代码 | 顺序本身就是隐式契约，新增状态时要读完整段 `Tick` |

---

## 5. 风险

| # | 风险 | 触发条件 | 现状 |
|---|---|---|---|
| 1 | **改 `JumpVelocity` / `MoveSpeed` 会让三关不可达** | 调手感 | 平台间距已按 2.87u / 4.75u 设计（`LevelDesign.md` §1）；改参数必须重跑可达性 |
| 2 | `Crouch` 松开未还原碰撞体高度 → 卡在低矮空间 | 蹲着钻过缝隙后立刻起跳 | `Exit()` 里还原；若状态被强制打断（如死亡）需确认 `Exit` 被调用 |
| 3 | **Play 模式注入输入基本无效** | 自动化测试想驱动 Idle/Run 转移 | `Update` 里的 `ReadInput` 会覆盖帧末注入的值。只有 `FixedTick` 里读的输入（攻击键反射写 + `PlayerCombat.enabled=false`）能注入。验收只能手测或走反射 |
| 4 | `Ledge` 状态零使用 | — | 无关卡配置抓取点，状态永远不进入（§2.4） |
| 5 | 零摩擦材质 + 平台 | 站移动平台 | 平台必须逐帧加 `Body.position`（`MovingPlatform` 已处理）；**新增任何移动平台都要照抄** |
| 6 | `Dodge` / `Dash` 语义易混 | 玩家反馈"闪避怎么有时候有无敌有时候没有" | 两者是不同状态：Dodge 有 0.33s 无敌、Dash 没有。UI/教学未区分（无操作提示） |

---

## 6. 与 requirements 的偏差说明

| 编号 | requirements 原文 | 实现 | 说明 |
|---|---|---|---|
| R1 | 水平移动 + 跳跃，状态机驱动 | ✅ 一致 | 实际 14 状态，远超原需求的 7 个（Idle/Run/Jump/Fall/Land/Hurt/Die） |
| R2 | 土狼时间 + 跳跃缓冲 + 可变跳高 | ✅ 一致 | 额外加了非对称重力（`FallGravityMultiplier`），原需求未提 |
| R3 | 冲刺 | ✅ 一致 | 拆成 Dodge（闪避，带无敌）与 Dash（位移，无无敌）两个状态，原需求只有一个"冲刺" |
| R4 | 墙滑 + 蹲伏 + 抓边 | ⚠️ 部分 | 墙滑/蹲伏已实现且可用；**抓边（Ledge）已实现但无关卡内容使用** |
| R5 | 动画驱动 4 参数 | ✅ 一致 | 原需求写 3 个（Speed/IsGrounded/VelocityY），实际 4 个（多 `IsLongIdle`，M9 补） |
| — | （原需求未提） | `PlayerToolAnchor` | 为 M3 武器解耦前置；`[ExecuteAlways]` + 三层排序保险 |
