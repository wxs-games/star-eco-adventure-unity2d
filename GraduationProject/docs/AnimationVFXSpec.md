# 《星际环保》动画与特效规格 · Animation & VFX Spec

> 版本：v1.0 | 最后更新：2026-10-30
> 岗位归属：**动画师（Animator）+ 特效师（VFX Artist）** 共同维护；主程序配合事件与曲线接入
> 上游约束：`ArtStyleGuide.md`（视觉规范）、`Art/README_素材说明.md`（素材切图规格）
> 数据来源：55 个 `.anim` 文件 + 9 个 `.controller` 解析、`PlayerStates.cs` / `HitFeel.cs` 代码实值（截至 2026-10-30）
> 权威顺序：**代码与 Animator 资产 > 本文档 > README 的 Samples 列**

---

## 一、这份文档管什么

动画素材的画法在 `Art/README_素材说明.md`（怎么切图、几帧、Pivot 在哪）。本文档管的是**素材之外的一切**：

- Animator 里到底有几个参数、叫什么、什么类型（README 只列了 5 个，实际 20 个）
- 状态怎么切、谁能打断谁
- 哪一帧该发事件、事件调什么函数
- 工具（钳子）为什么不在角色帧里
- 6 套特效素材为什么只有 1 套在用

> 一句话：README 管"图怎么画"，本文档管"图怎么动、什么时候出火花"。

---

## 二、动画系统架构

项目用的是**双轨制**，两条轨道必须区分清楚：

```
输入 ──► PlayerController（读输入、算速度）
              │
              ├──► 代码状态机（PlayerStateBase 派生类）── 决定"能做什么"
              │      Idle / Run / Air / Land / Hurt / Death / Dodge / Dash
              │      / Crouch / WallSlide / Ledge / Attack（Combat 下）
              │
              └──► Animator（PlayerAnimatorDriver 写参数）── 决定"看起来怎样"
                     Player.controller（20 个参数、22 个 clip）
```

**关键约定**：**代码状态机是权威，Animator 是表现**。

- 位移、伤害窗口、无敌帧全部由代码决定，动画不驱动逻辑（**不用 Root Motion**）。
- Animator 只接收参数、播 clip、在指定帧回调事件。
- 所以"动画播到一半被打断"不会导致逻辑错乱——逻辑在自己的状态机里。

**这条约定带来一个代价**：动画时长必须与代码的硬编码时长匹配（比如 `PlayerHurtState` 的持续 0.3 秒必须等于 `Hurt.anim` 时长），否则会出现"动画还在播、人已经能动了"或反之。当前 22 个 clip 的时长与代码基本对齐（见 §5）。

---

## 三、Animator 参数表（实测）

### 3.1 Player.controller：20 个参数

| 参数名 | 类型 | 用途 |
|---|---|---|
| `Speed` | Float | 水平速度绝对值，驱动 Run↔Idle |
| `VelocityY` | Float | 垂直速度，区分 Jump/Fall 的过渡 |
| `IsGrounded` | Bool | 是否着地 |
| `IsWallSliding` | Bool | 是否贴墙滑 |
| `IsLedgeGrab` | Bool | 是否抓边 |
| `IsCrouching` | Bool | 是否蹲下 |
| `IsLongIdle` | Bool | 站定 ≥10 秒（触发 Idle02） |
| `Attack` | Trigger | 第一段攻击 |
| `Attack2` | Trigger | 第二段 |
| `Attack3` | Trigger | 第三段（投掷） |
| `AttackAir` | Trigger | 空中攻击 |
| `HeavyAttack` | Trigger | 重击 |
| `Dash` | Trigger | 冲刺 |
| `Roll` | Trigger | 翻滚/闪避 |
| `Hurt` | Trigger | 受击 |
| `Death` | Trigger | 死亡 |
| `Interact` | Trigger | 交互（钳取/拾取） |
| `Turn` | Trigger | 转身 |
| `WallJump` | Trigger | 蹬墙跳 |
| `Victory` | Bool/Trigger | 胜利 |

> ⚠️ **README §4.7 只列了 5 个参数**（Speed / IsGrounded / Attack / Hurt / Dash），与实际相差 15 个。以前按 README 接动画的人一定会接错。

### 3.2 敌人 / 塔 / Boss

| 控制器 | 参数（实测） |
|---|---|
| Enemy01 / 02 / 03 | `Speed`(Float)、`IsMove`(Bool)、`IsAiming`(Bool)、`Attack`(Trigger)、`Fire`(Trigger)、`Hurt`、`Die`、`Spawn` |
| Tower01 | 同上 8 个 |
| Boss01 | `Speed`、`IsMove`、`Attack`、`Charge`、`AttackB`、`Special`、`Enrage`、`Hurt`、`Die` |
| NPC_Ava_JinlingAssault | `Assault`（单参数，特殊演出用） |

**约定**：敌人统一使用 `Speed/IsMove/IsAiming/Attack/Fire/Hurt/Die/Spawn` 八参数模板。新增敌人直接复用这套参数名，不要发明新名字（否则通用 AI 脚本无法驱动）。

---

## 四、状态机与打断优先级

### 4.1 优先级（从高到低）

```
Death  >  Hurt  >  Attack / Interact / HeavyAttack  >  Dash / Roll
       >  WallJump / LedgeGrab / Crouch  >  Jump  >  Run / Idle
```

**规则**：

1. **高优先级可以打断低优先级，反之不行**。死亡永远最高——血量归零时不管在做什么，立刻进 Death。
2. **同优先级不可互相打断**。攻击中不能再次起手（连段靠 `Attack2/Attack3` 的顺序窗口，不是重复 Trigger）。
3. **Hurt 有 1 秒无敌**（`PlayerCombat`），无敌期内不再进 Hurt，避免连续受击把角色锁死。

### 4.2 特殊转移

| 转移 | 条件 | 说明 |
|---|---|---|
| Idle → Idle02 | 站定 ≥ **10 秒** | `PlayerAnimatorDriver.UpdateLongIdle()`；此前 `IsLongIdle` 参数有定义但无人 set，Idle02 是"死状态" |
| Run → Air | 走出平台边缘 | 不经过 Land |
| Air → Land | 落地且垂直速度 < 阈值 | Land 是短过渡，会立刻接 Idle/Run |
| Attack → Attack2 | 第一段命中窗口内再次攻击 | 三段连招 |
| Attack3 | 播到第 **5 帧（0.19 秒）** 调 `Player.ToolThrow.TryThrow()` | 投掷时机硬编码，改帧数时必须同步改这个时间 |
| 任意 → Crouch | 按住下 | 蹲下时不能移动 |
| 贴墙下落 → WallSlide | 有墙 + 水平输入朝墙 | — |

### 4.3 攻击窗口（Hitbox 何时开）

`AttackHitbox` 的开窗**完全由动画事件驱动**，不是代码里的计时器：

- `OnAttackWindowOpen()` —— 判定开
- `OnAttackWindowClose()` —— 判定关

实测：`Attack`、`Attack02`、`Attack03`、`AttackAir`、`HeavyAttack` 等 **9 个 clip 带这组事件**（共 9 开 + 9 关）。

> **这意味着：如果某个攻击 clip 少了事件，那次攻击就一点伤害都打不出来，而且不会报错。** 新增攻击动作时，必须检查事件是否贴上。

---

## 五、Clip 规格表（实测）

22 个玩家 clip，采样率、循环、时长实测如下。「时长」= 最后一帧时间，「帧数」= 素材帧数（来自 `README`）。

| Clip | SampleRate | 循环 | 时长(s) | 帧数 | 备注 |
|---|---|---|---|---|---|
| `Idle` | 14.4 | ✅ | 0.83 | 10 | 14.4 = 12/0.833，反推值 |
| `Idle02` | 10 | ✅ | 1.20 | 8 | 长待机，站定 10s 触发 |
| `Run` | 24 | ✅ | 0.33 | 8 | 循环步长 0.67s（两轮） |
| `Jump` | 27 | ❌ | 0.44 | 3 | 起跳 |
| `Fall` | 36 | ✅ | 0.33 | 4 | 下落循环 |
| `Land` | 30 | ❌ | 0.40 | 3 | 落地过渡 |
| `Hurt` | 20 | ❌ | 0.30 | 3 | 与 1s 无敌错开 |
| `Death` | 15 | ❌ | 0.67 | 7 | 有 `OnDieEnd` |
| `Attack` | 24 | ❌ | 0.33 | 10 | 一段 |
| `Attack02` | 36 | ❌ | 0.33 | 8 | 二段 |
| `Attack03` | 24 | ❌ | 0.38 | 9 | 三段（投掷），第 5 帧抛钳 |
| `AttackAir` | 24 | ❌ | 0.25 | 6 | 空中 |
| `HeavyAttack` | 24 | ❌ | 0.50 | 18 | 最长攻击 |
| `Interact` | 18 | ❌ | 0.33 | 6 | 钳取/拾取 |
| `Dash` | 30 | ❌ | 0.40 | 5 | LeftControl |
| `Roll` | 24 | ❌ | 0.33 | 8 | LeftShift（闪避） |
| `Crouch` | 15 | ❌ | 0.13 | 2 | 蹲下保持 |
| `WallSlide` | 12 | ✅ | 0.25 | 3 | 贴墙滑 |
| `WallJump` | 24 | ❌ | 0.12 | 3 | 极短 |
| `LedgeGrab` | 12 | ✅ | 0.25 | 3 | 抓边保持 |
| `Turn` | 24 | ❌ | 0.04 | 1 | 单帧，几乎瞬时 |
| `Victory` | 15 | ✅ | 0.53 | 8 | 关卡结算 |

### 5.1 采样率问题

实测采样率有 **10 / 12 / 14.4 / 15 / 18 / 20 / 24 / 27 / 30 / 36** 十档。

原因：美术是按"逐帧等距"摆的关键帧，采样率 = 帧数 ÷ 时长 反推出来的，不是主动设定的。这在 Unity 里能正常播放，但有两个副作用：

1. **跨 clip 的"每帧多少秒"不统一**（`Run` 每帧 41.7ms、`Jump` 每帧 37ms、`Fall` 每帧 27.8ms），动画师想算"第 5 帧是第几秒"必须逐个查。
2. `14.4` 这种非整数采样率在 timeline 里显示为 0.83 秒循环，肉眼算不出是 12 帧。

**规范（针对新动作）**：

| 动作类型 | 采样率 | 理由 |
|---|---|---|
| 循环动作（Idle/Run/Fall/WallSlide/LedgeGrab） | **24** | 与主流逐帧动画一致，1 帧 = 41.7ms |
| 快速打击（Attack/Hurt/Dash/Roll） | **24** | 同上，强调节奏感 |
| 慢速演出（Death/Victory/Idle02） | **12** | 降低"手绘感"，更像定格 |

> 现有 22 个 clip **不返工**——它们都已经与代码时长对齐，改采样率会破坏"动画时长 = 逻辑时长"的等式，风险远大于收益。

---

## 六、动画事件

### 6.1 事件函数清单（实测，55 个 clip 内）

| 事件函数 | 出现次数 | 接收方 | 作用 |
|---|---|---|---|
| `OnAttackWindowOpen` | 9 | `PlayerController` | 开攻击判定框 |
| `OnAttackWindowClose` | 9 | `PlayerController` | 关攻击判定框 |
| `OnDieEnd` | 4 | 各敌人的死亡处理 | 死亡动画播完 → 消失/掉净化物 |
| `OnSpawnEnd` | 1 | `Enemy03` | 出场动画结束 → 开始行动 |
| `OnProjectileSpawn` | 1 | 敌人/塔 | 发射投射物 |
| `OnFireSpawn` | 1 | `Tower01` | 喷毒开始 |
| `OnFireEnd` | 1 | `Tower01` | 喷毒结束 |
| `OnAimEnd` | 1 | `Tower01` | 瞄准结束 |

### 6.2 事件使用规则

1. **伤害窗口只用 `OnAttackWindowOpen/Close`**。不要在代码里写"攻击 0.1 秒后开判定"——动画一改就错位。
2. **死亡收尾必须用 `OnDieEnd`**。不要用"播完自动消失"的代码计时器，因为不同敌人的死亡动画长度不同（Enemy01 6 帧、Boss01 24 帧）。
3. **事件不可重复注册**。同一帧挂两个同名事件会导致判定开两次（关一次），出现"只打到第一下"。
4. **新加事件必须在接收脚本里有同名 public 方法**，否则 Unity 会静默报 warning 但不中断。

---

## 七、武器与工具的动画解耦（本项目最特殊的设计）

### 7.1 问题

主角的手里拿着一把**钳子**。如果钳子画进每一帧角色图里，会有两个麻烦：

- 钳子要单独做高光/拖尾特效时，无法分离。
- 换工具（钳子 → 未来的其他工具）要重画全部 136 帧。

### 7.2 方案：帧只画空手，钳子是独立子物体

```
Player（order 10）
├── Body（SpriteRenderer，order 10）
└── ToolAnchor（order 11，pivot 在握持点）
    └── ToolSprite（钳子贴图）
```

动画曲线控制 `ToolAnchor` 的两条属性：

| 曲线 | 用途 |
|---|---|
| `m_LocalPosition.x/y` | 让钳子逐帧跟手（抬手、挥击、垂手时位置不同） |
| `m_IsActive` | 控制显隐——解耦动作 = 1，帧里烘焙了工具的动作 = 0（避免"两把钳子"） |

**不需要改状态机**：整套跟手逻辑活在 clip 的曲线里。

### 7.3 三条硬约束（踩过的坑）

1. **排序必须运行时跟随**。角色 `SpriteRenderer.sortingOrder` 在预制体里是 0，但**进场景后是 15**（`FixAllLevelSpriteSorting.GuessOrder()` 的兜底返回值）。所以工具的排序**不能写死绝对值**，必须每帧设成 `_body.sortingOrder + 1`。写死会让钳子被角色整块盖住——表现为"钳子消失了"。

2. **`PlayerToolAnchor` 必须 `[ExecuteAlways]`**，否则编辑模式下看不见工具，无法手动对齐握持点。

3. **投掷期间隐藏工具只能改 `SpriteRenderer.enabled`，不能 `SetActive(false)`**——clip 每帧都在写 `m_IsActive` 曲线，`SetActive` 会被下一帧覆盖回来。

### 7.4 握持精度验收口径

**一律按游戏实际尺寸验收，不看放大图。**

1080p、`orthographic size = 6.5` 时，角色屏幕高度只有 **约 166 px**。15.6 个 sprite 像素的握持误差 ≈ 屏幕 **3.3 px**，肉眼不可见。用 3 倍放大图验收会得出"没对齐"的错误结论。

---

## 八、特效规格

### 8.1 素材现状：6 套全有，预制体只做了 1 个

| 特效 | 素材帧数 | 用途（设计要求） | Prefab | 实际调用点 |
|---|---|---|---|---|
| `fx_slash` | 6 | 挥击斩击光，叠在 Attack01/02/03 开窗帧 | ❌ **无** | **0 处** |
| `fx_hit` | 6 | 任意命中瞬间，Pivot 对齐受击点 | ❌ **无** | **0 处** |
| `fx_purify` | 8 | 净化敌人/道具，Interact 与敌人 Die 末段 | ✅ `fx_purify.prefab` | 3 处 |
| `fx_dust_run` | 4 | Run 脚步 / Jump / Land 脚下扬尘 | ❌ **无** | **0 处** |
| `fx_gas` | 6 | 毒植物、毒气塔、Boss 喷毒判定处 | ❌ **无** | **0 处** |
| `fx_explode` | 8 | 机器人 / Boss 爆炸 | ❌ **无** | **0 处** |

**素材 6/6 齐，落地 1/6。**

`fx_purify` 的 3 个调用点：

| 位置 | 语义 |
|---|---|
| `BossController.cs:411` | Boss 死亡时在尸体附近随机撒 2.2 单位范围内的净化光 |
| `GarbagePile.cs:120` | 垃圾堆被净化完成时 |
| `SprayController.cs:206` | 净水喷洒命中判定点（每 2 tick 播一次） |

### 8.2 打击感当前靠什么

既然 `fx_hit` / `fx_slash` 都没有，打击感只能靠 `HitFeel`：

| 手段 | 参数 | 说明 |
|---|---|---|
| **顿帧** | **45 ms**（`DefaultStopSeconds = 0.045`） | 命中瞬间 `Time.timeScale` 短暂归零，连续命中不叠加 |
| **震屏** | 时长 `max(0.08, 顿帧 × 2.2)`、振幅 **0.05** | 由 `CameraFollow2D.Shake()` 执行 |
| **伤害数字** | 世界空间飘字 | `DamageNumberLayer` |
| **音效** | `SfxId.Attack`（±5% 变调） | — |
| **命中特效** | ❌ **无** | — |

**结论**：有顿帧 + 震屏 + 飘字 + 音效，**唯独缺视觉火花**。这是打击感"总差一口气"的直接原因。

### 8.3 补齐这 5 个 prefab 的做法（半天工作量）

所有 FX 预制体结构完全一致，`fx_purify.prefab` 就是模板：

```
fx_<名>（GameObject）
├── Animator（Controller = fx_<名>.controller，CullTransparentMesh）
├── SpriteRenderer（sortingOrder = 21，材质 Additive）
└── FxOneShot（脚本，播完自动归还对象池）
```

**接入步骤**：

1. 复制 `fx_purify.prefab` → 改名 → 换 SpriteRenderer 的 Sprite 与 Animator Controller。
2. 在调用方脚本加 `[SerializeField] private FxOneShot _fxHitPrefab;`。
3. 调用 `FxOneShot.Play(_fxHitPrefab, 位置)`。
4. 在编辑器生成脚本里自动赋值（否则场景重建后引用又丢）。

**各特效的接入点建议**：

| 特效 | 接在哪 | 位置 | order |
|---|---|---|---|
| `fx_slash` | `AttackHitbox` 开窗时 | 角色朝向前方 0.8 单位 | 21（最前） |
| `fx_hit` | `IHittable.TakeDamage` 成功后 | 受击点（`info.Point`） | 21 |
| `fx_dust_run` | `PlayerAnimatorDriver` 的 Run 步频 / Jump / Land | 脚底（`transform.position`） | 20（角色后） |
| `fx_gas` | `Enemy02.Attack` / `Tower01.Fire` / `BossController.AttackB` | 判定框中心 | 21 |
| `fx_explode` | `Enemy03.Die` / `BossController.Die` 的 `OnDieEnd` | 尸体坐标 | 21 |

**材质**：`fx_slash` / `fx_hit` / `fx_purify` / `fx_explode` 用 **Additive**（黑底完全透明，自然发光）；`fx_gas` / `fx_dust_run` 用 **Transparent**（烟尘不该发光）。

### 8.4 特效美术规范

| 项 | 规定 |
|---|---|
| 帧数 | 4~8 帧（超过 8 帧会"拖沓"，失去冲击感） |
| 单帧时长 | 24 sampleRate 下 1 帧 = 41.7ms；整个特效 **0.2~0.35 秒** |
| Pivot | **Center**（便于按命中点对齐） |
| 底 | 黑底转透明（Additive 友好），不允许白底 |
| 尺寸 | 与判定框同量级，不超过角色身高的 1.5 倍 |
| 排序 | 命中类 21、环境类 20 |
| 禁止 | 屏幕全白闪、长时间残留、多特效叠加同一位置超过 3 层 |

---

## 九、扩展 SOP

### 9.1 新增一个玩家动作

1. 美术：按 `Art/README` 规格出帧（cell 692×833、Pivot Bottom Center）。
2. 切图 → 生成 `.anim`（采样率按 §5.1）。
3. 在 `Player.controller` 加状态 + 参数（参数名遵循 §3）。
4. 在 `PlayerStates.cs` 加对应的 `PlayerXxxState`，`Enter()` 里 Trigger 参数。
5. 若要开攻击判定 → 在判定首帧贴 `OnAttackWindowOpen`、末帧贴 `OnAttackWindowClose`。
6. **检查打断优先级**：新状态属于哪一级？能否被 Hurt/Death 打断？
7. 更新本文档 §5 的 clip 表。

### 9.2 新增一个敌人

1. 复用八参数模板（`Speed/IsMove/IsAiming/Attack/Fire/Hurt/Die/Spawn`）。
2. 死亡动画必须在最后一帧贴 `OnDieEnd`。
3. 剪影必须能与现有 6 种区分（见 `ArtStyleGuide.md` §7.2）。
4. `localScale` 按体型设，**不改 PPU**。

### 9.3 新增一个特效

1. 素材 4~8 帧，黑底转透明，Pivot Center。
2. 复制 `fx_purify.prefab`，换 Sprite + Controller，order 按 §8.3。
3. 调用 `FxOneShot.Play(prefab, pos)`，**不要自己 `Instantiate`**（会绕过对象池）。
4. 在编辑器生成脚本里补自动赋值。

---

## 十、待补清单

| 级别 | 项 | 成本 | 影响 |
|---|---|---|---|
| **P0** | 补 `fx_hit` prefab 并接到 `TakeDamage` | 1 h | 打击感缺最关键的视觉火花 |
| **P0** | 补 `fx_slash` prefab 并接到攻击开窗 | 1 h | 挥击"看不见刀光" |
| P1 | 补 `fx_explode`（Boss/机器人死亡） | 1 h | 死亡瞬间不够重 |
| P1 | 补 `fx_dust_run`（跑动扬尘） | 1 h | 跑动缺乏重量感 |
| P1 | 补 `fx_gas`（毒气） | 1 h | 毒气攻击没有预警 |
| P2 | `NPC_Ava` 的 12 帧 Idle 动画接入场景 | 1 h | 四场景全用静态图，动画闲置 |
| P2 | README §4.7 的 5 参数表更新为 20 参数 | 10 min | 后续接动画必错 |

---

## 十一、漂移记录

| 日期 | 发现 | 处理 |
|---|---|---|
| 2026-10-30 | README 只列 5 个 Animator 参数，实测 20 个 | 本文档 §3 收录完整表 |
| 2026-10-30 | README 用 `Player_Attack01` 命名 clip，实际 clip 名是 `Attack`（无前缀） | 本文档 §5 用实际名 |
| 2026-10-30 | README 的 Samples 列与实际 `m_SampleRate` 不一致（如 Idle 写 12，实测 14.4） | 本文档 §5 用实测值 |
| 2026-10-30 | FX 素材 6 套齐全，prefab 只有 `fx_purify` 1 个；其余 5 套 0 调用 | 登记为 P0/P1 待补 |
| 2026-10-30 | `NPC_Ava` 12 帧 Idle 动画四场景全未引用（场景用 `npc_ava.png` 静态图） | 登记为 P2 |

---

## 十二、变更记录

| 版本 | 日期 | 变化 | 原因 |
|---|---|---|---|
| v1.0 | 2026-10-30 | 首版。双轨架构说明、20 个 Animator 参数实测表、打断优先级、22 个 clip 的采样率/时长/循环实测、8 种动画事件清单、工具解耦三条硬约束、6 套 FX 落地率 1/6 的实况、打击感构成拆解（45ms 顿帧 + 0.05 震屏 + 飘字 + 音效，缺视觉火花）、扩展 SOP | 「动画师」与「特效师」两个岗位此前完全无文档。素材 README 只覆盖切图；Animator 参数、事件、打断规则、特效接入点全靠口口相传，实际已导致 FX 5/6 未落地而无人察觉 |
