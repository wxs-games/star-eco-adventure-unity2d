# 需求规范：04-EnemyAI（敌人 AI 与炮台）

> 对应 TDD.md M4 里程碑 | 格式：EARS
> 范围：敌人 FSM 基类、玩家感知与目标锁定、三种敌人（Enemy01 污染污泥怪 / Enemy02 变异毒植物 / Enemy03 巡逻机器人）行为、Tower01 毒气防御塔（Aim→Fire / 被净化 Disable）、与 `IHittable`/`DamageInfo` 的受击对接、敌人攻击对玩家的伤害回流、死亡事件广播与对象池回收、`EnemyConfig` 数值配置、动画参数驱动与事件帧
> 非目标：Boss01 阶段机与第三关 Boss 战（M8）；净化喷壶、净化判定与持续毒气/中毒 debuff 本体（M5，M4 仅为 Tower01 提供"被净化关闭"的响应入口）；掉落道具的实际效果与净化剂经济（M5）；关卡刷怪点布置与净化值/污染度驱动刷怪（M6/M9）；敌人跳跃、跨平台与复杂寻路（M4 敌人仅做地面直线移动）；敌人血量与伤害的存档读写（M6）
> 共用约定：代码落点 `Assets/Scripts/EnemyAI/`（命名空间 `StarEco.EnemyAI`）；美术帧目录 `Assets/Art/Art/Enemies/Enemy01~03/Frames/`、`Assets/Art/Art/Towers/Tower01/Frames/`；命名与结构遵循项目既有骨架（M2 状态机 + M3 伤害流水线）

## R1 敌人 FSM 基类与状态契约

> **美术动作**：不涉及具体动作，为 Enemy01/02/03/Tower01 全部状态提供公共骨架 | **代码落点**：`Assets/Scripts/EnemyAI/EnemyStateMachine.cs`（`IEnemyState` + 敌人状态机）、`Assets/Scripts/EnemyAI/EnemyStateBase.cs`（状态公共基类）

- THE SYSTEM SHALL 在 `StarEco.EnemyAI` 命名空间提供独立的敌人状态接口与敌人状态机，状态生命周期契约与 M2 玩家状态机对齐（`Enter` / `Exit` / `Tick` / `FixedTick` 四段式）。
- THE SYSTEM SHALL 以"状态类型 → 状态实例"字典注册 + 按类型切换的方式组织敌人状态，并保证切换顺序为「先 Exit 当前态、再 Enter 新态」；重复注册同名状态时，后者覆盖前者。
- THE SYSTEM SHALL 让敌人状态机独立于 `StarEco.Player` 命名空间，复用 M2 的"状态机设计模式"而不继承 `IPlayerState` / `PlayerStateMachine`（敌人不得反向依赖玩家控制器，避免 M2/M4 双向耦合）。
- THE SYSTEM SHALL 提供敌人状态公共基类，统一持有"敌人控制器 + 敌人状态机"引用，并提供死亡转移、受击转移的快捷检查方法，供 Enemy01/02/03/Tower01 的全部状态复用。
- WHEN 敌人状态发生切换，THE SYSTEM SHALL 仅在状态的 `Enter` / `Exit` 中驱动动画参数与物理/碰撞开关；状态内部不得直接调用其他状态的方法（一切转移经由状态机）。
- WHILE 敌人处于活动状态，THE SYSTEM SHALL 每帧按固定优先级检查状态转移：死亡 > 受击 > 攻击 > 追击/移动 > 待机，与 M2 玩家侧既有优先级约定保持一致。
- WHEN 敌人从对象池取出进入场景，THE SYSTEM SHALL 由状态机将初始状态复位（默认 Idle 或 Enemy03 的 Spawn），不复用上一次生命周期的当前状态。

## R2 玩家感知与目标锁定

> **美术动作**：无独立动作，为各敌人的 Idle ↔ 战斗状态切换提供条件 | **代码落点**：`Assets/Scripts/EnemyAI/EnemyControllerBase.cs`（感知与目标字段）、各敌人控制器

- WHEN 玩家进入 `EnemyConfig.PerceptionRadius` 且与敌人处于同一地面层，THE SYSTEM SHALL 将玩家锁定为目标并允许切换到战斗相关状态。
- WHILE 敌人已锁定目标且玩家仍在"感知半径 + 脱战缓冲"之内，THE SYSTEM SHALL 保持锁定，不因单帧瞬移或擦边超出半径而立即脱战（防抖）。
- WHEN 玩家脱离警戒范围持续超过 `EnemyConfig.LoseTargetSeconds`，THE SYSTEM SHALL 清除目标并回到待机/巡逻行为。
- IF 玩家处于死亡状态，THE SYSTEM SHALL 清除目标并回到待机，不再追击或攻击。
- IF 敌人自身处于出场期（Enemy03 Spawn）或已死亡，THE SYSTEM SHALL 不进行感知与目标锁定。
- THE SYSTEM SHALL 通过 `SpriteRenderer.flipX` 使敌人面向目标（沿用 M2 约定，不缩放 Transform，避免碰撞体镜像问题）。

## R3 Enemy01 污染污泥怪（慢速近战）

> **美术动作**：`Enemy01_Idle` / `Enemy01_Move` / `Enemy01_Attack` / `Enemy01_Hurt` / `Enemy01_Die`（帧目录 `Assets/Art/Art/Enemies/Enemy01/Frames/`） | **代码落点**：`Assets/Scripts/EnemyAI/Enemy01/`（第一关教学单位）

- WHEN 未锁定目标，THE SYSTEM SHALL 播放 `Enemy01_Idle` 并静止待机。
- WHEN 已锁定目标且与玩家的水平距离大于攻击距离，THE SYSTEM SHALL 以 `EnemyConfig.MoveSpeed`（第一关慢速）播放 `Enemy01_Move` 朝玩家水平移动。
- WHEN 目标进入 `EnemyConfig.AttackRange`，THE SYSTEM SHALL 停止移动并播放 `Enemy01_Attack`，由 `HitOn` / `HitOff` 事件帧开启与关闭命中判定。
- WHEN `Enemy01_Attack` 动画播放结束且目标仍在攻击距离内，THE SYSTEM SHALL 进入 `EnemyConfig.AttackInterval` 冷却，冷却结束后才允许下一次攻击（不连续刷攻）。
- WHEN `Enemy01_Attack` 命中玩家，THE SYSTEM SHALL 走 R9 的伤害回流，由玩家侧决定扣血、硬直与无敌。
- WHEN Enemy01 收到伤害（`TakeDamage`），THE SYSTEM SHALL 打断当前动作、播放 `Enemy01_Hurt` 并进入受击硬直，硬直结束后按是否仍锁定目标回到战斗或待机。
- WHEN Enemy01 的 HP 归零，THE SYSTEM SHALL 播放 `Enemy01_Die` 并走 R8 的死亡回收流程。

## R4 Enemy02 变异毒植物（远程毒气）

> **美术动作**：`Enemy02_Idle` / `Enemy02_Move` / `Enemy02_Attack` / `Enemy02_Hurt` / `Enemy02_Die`（帧目录 `Assets/Art/Art/Enemies/Enemy02/Frames/`） | **代码落点**：`Assets/Scripts/EnemyAI/Enemy02/`、`Assets/Scripts/EnemyAI/EnemyProjectile.cs`（毒气弹体）

- WHEN 未锁定目标，THE SYSTEM SHALL 播放 `Enemy02_Idle`。
- WHEN 已锁定目标且水平距离大于攻击距离，THE SYSTEM SHALL 播放 `Enemy02_Move` 朝玩家移动，直至进入远程攻击距离。
- WHEN 目标进入 `EnemyConfig.AttackRange`（远程攻击距离，大于 Enemy01），THE SYSTEM SHALL 播放 `Enemy02_Attack`，并在 `HitOn` 事件帧生成毒气弹体，弹体沿当前朝向飞向目标（弹体经对象池获取，不即时实例化）。
- WHEN 毒气弹体命中携带 `IHittable` 的目标、或触地、或抵达 `EnemyConfig.ProjectileMaxRange`，THE SYSTEM SHALL 结算一次伤害与 `fx_gas` 特效（命中时），并将弹体归还对象池。
- WHEN `Enemy02_Attack` 播放结束，THE SYSTEM SHALL 进入 `AttackInterval` 冷却，冷却结束后可再次攻击。
- WHEN Enemy02 收到伤害，THE SYSTEM SHALL 播放 `Enemy02_Hurt` 并进入硬直；当 HP 归零，THE SYSTEM SHALL 播放 `Enemy02_Die` 并走 R8 死亡回收。
- THE SYSTEM SHALL 毒气弹体不伤害其发射者自身，也不与其他敌方单位互相结算伤害。

## R5 Enemy03 巡逻机器人（高速追击）

> **美术动作**：`Enemy03_Idle` / `Enemy03_Chase` / `Enemy03_Attack` / `Enemy03_Hurt` / `Enemy03_Die` / `Enemy03_Spawn`（帧目录 `Assets/Art/Art/Enemies/Enemy03/Frames/`） | **代码落点**：`Assets/Scripts/EnemyAI/Enemy03/`

- WHEN Enemy03 被实例化/激活，THE SYSTEM SHALL 先播放 `Enemy03_Spawn` 出场动画；出场期间不接受伤害、不移动、不攻击。
- WHEN `Enemy03_Spawn` 播放结束且未锁定目标，THE SYSTEM SHALL 播放 `Enemy03_Idle` 待机（巡逻米位由场景摆放决定）。
- WHEN 锁定目标，THE SYSTEM SHALL 播放 `Enemy03_Chase`，以 `EnemyConfig.MoveSpeed`（第三关高速，显著大于 Enemy01）快速追击玩家。
- WHEN 追击中进入 `EnemyConfig.AttackRange`，THE SYSTEM SHALL 播放 `Enemy03_Attack`，由 `HitOn` / `HitOff` 事件帧开窗命中，攻击结束后按 `AttackInterval` 冷却并可再次追击或攻击。
- WHEN 目标脱离警戒范围超过 `LoseTargetSeconds`，THE SYSTEM SHALL 退出 Chase 回到 Idle。
- WHEN Enemy03 收到伤害，THE SYSTEM SHALL 播放 `Enemy03_Hurt` 并打断追击；当 HP 归零，THE SYSTEM SHALL 播放 `Enemy03_Die` 并走 R8 死亡回收。

## R6 Tower01 毒气防御塔（固定炮台）

> **美术动作**：`Tower01_Idle` / `Tower01_Aim` / `Tower01_Fire` / `Tower01_Disable`（帧目录 `Assets/Art/Art/Towers/Tower01/Frames/`） | **代码落点**：`Assets/Scripts/EnemyAI/Tower01/`、`Assets/Scripts/EnemyAI/EnemyProjectile.cs`

- THE SYSTEM SHALL Tower01 为固定单位，不参与位移、追击与地面移动。
- WHEN 玩家进入 Tower01 的感知/射程范围，THE SYSTEM SHALL 从 `Tower01_Idle` 切换到 `Tower01_Aim`，并将瞄准方向朝向玩家。
- WHEN `Tower01_Aim` 结束且玩家仍在射程内，THE SYSTEM SHALL 切换到 `Tower01_Fire`，并在 `Fire` 的事件帧生成毒气弹体、沿当前瞄准方向发射（弹体经对象池获取）。
- WHEN `Tower01_Fire` 播放结束，THE SYSTEM SHALL 进入 `AttackInterval` 冷却，冷却后若玩家仍在射程内则重新进入 `Aim`（形成 Idle → Aim → Fire → Aim 循环）。
- WHEN 玩家脱离射程或脱离警戒范围，THE SYSTEM SHALL 停止瞄准与开火并回到 `Tower01_Idle`。
- WHEN Tower01 收到关闭信号（M5 净化命中时由外部调用，M4 只提供入口），THE SYSTEM SHALL 播放 `Tower01_Disable` 并进入关闭态：不再 Aim/Fire、不再生成弹体、不再对玩家造成伤害。
- IF Tower01 已处于关闭态再次收到关闭信号，THE SYSTEM SHALL 忽略该信号（幂等，不重播 Disable、不报错）。
- WHERE Tower01 处于关闭态，THE SYSTEM SHALL 保持关闭直到外部（M6 检查点流程）显式请求重新激活；M4 不自行恢复。
- WHEN Tower01 被关闭，THE SYSTEM SHALL 通过事件中心广播"防御塔已净化/关闭"事件，供 M5/M6 订阅（检查点激活、净化值结算）。

## R7 受击与伤害接口对接

> **美术动作**：各敌人 `Hurt` 动作 | **代码落点**：`Assets/Scripts/EnemyAI/EnemyControllerBase.cs`（实现 `StarEco.Combat.IHittable`）

- THE SYSTEM SHALL 全部敌人（Enemy01/02/03）与 Tower01 实现 `StarEco.Combat.IHittable`，通过 `StarEco.Combat.DamageInfo`（Amount / SourceDirection / Source / KnockbackPower）接收伤害，直接接入 M3 玩家攻击经 `AttackHitbox` 触发的同一条命中流水线，不新增平行的伤害通道。
- WHEN 敌人收到 `TakeDamage(DamageInfo)`，THE SYSTEM SHALL 按 `info.Amount` 扣减 HP，并以 `info.SourceDirection` 的反方向施加受击表现（硬直时长与击退强度取自 `EnemyConfig`）。
- IF 敌人处于出场期（Enemy03 Spawn）或已死亡，THE SYSTEM SHALL 忽略该次伤害。
- WHEN `info.KnockbackPower > 0`，THE SYSTEM SHALL 按 `EnemyConfig.KnockbackResponse` 折算实际位移（重甲/固定单位如 Tower01 及必要时 Enemy01 可配置为 0，即受击不位移）。
- THE SYSTEM SHALL 敌人受击仅通过 `IHittable` 接口回调，不引用攻击方的任何具体类型（与 M3 R7 约定一致）。
- WHEN 敌人 HP 因受击归零，THE SYSTEM SHALL 由受击流程直接转入死亡状态（不由攻击方决定死亡表现）。

## R8 死亡、事件广播与对象池回收

> **美术动作**：各敌人 `Die` 动作（带 `DieEnd` 事件帧） | **代码落点**：`Assets/Scripts/EnemyAI/EnemyControllerBase.cs`、`Assets/Scripts/EnemyAI/Events/EnemyDeathEvent.cs`

- WHEN 敌人 HP 归零，THE SYSTEM SHALL 进入死亡状态：停止移动与攻击逻辑、关闭受击判定与攻击判定、播放对应的 `Die` 动画。
- WHEN `Die` 动画播放到 `DieEnd` 事件帧，THE SYSTEM SHALL 触发回收流程（与玩家 Die 动画同款"事件帧驱动"机制，不用定时器猜测时长）。
- WHEN 回收流程执行，THE SYSTEM SHALL 通过 `StarEco.Framework.PoolManager` 将敌人实例归还其来源对象池，禁止直接 `Destroy`（TDD §2.2 性能红线）。
- WHEN 敌人死亡，THE SYSTEM SHALL 通过 `StarEco.Framework.EventCenter` 广播敌人死亡事件，事件至少携带敌人类型标识、死亡位置与 `EnemyConfig.DropTable` 引用，供 HUD、掉落生成、关卡流程解耦订阅。
- WHEN 敌人从对象池取出复用，THE SYSTEM SHALL 重置 HP、当前状态、Animator 参数（Float/Bool/Trigger）、速度与朝向为初始值，避免上一生命周期的残留状态。
- IF 同一敌人实例被重复归还对象池，THE SYSTEM SHALL 安全忽略（沿用 `ObjectPool` 的重复归还防护，不销毁、不报错）。
- THE SYSTEM SHALL 敌人死亡事件的广播与对象池归还各自至多执行一次，不重复触发。

## R9 敌人攻击对玩家的伤害回流

> **美术动作**：各敌人与 Tower01 的 `Attack` / `Fire` 动作（带 `HitOn` / `HitOff` 事件帧） | **代码落点**：`Assets/Scripts/EnemyAI/EnemyAttackHitbox.cs`、`Assets/Scripts/EnemyAI/EnemyProjectile.cs`

- WHEN 敌人攻击的命中开窗期检测到携带 `IHittable` 的玩家，THE SYSTEM SHALL 对玩家调用一次 `TakeDamage(DamageInfo)`，携带敌人攻击力、来源方向、来源对象与击退力，交由 M3 的 `PlayerCombat` / `PlayerController` 处理扣血、硬直与无敌帧。
- THE SYSTEM SHALL 敌人攻击的命中开窗通过 `HitOn` / `HitOff` 事件帧开关，开窗期外不产生命中（与 M3 R3 同规则）。
- THE SYSTEM SHALL 同一玩家目标在敌人的单次开窗内只结算一次伤害（去重），毒气弹体对同一目标也只结算一次。
- IF 玩家处于无敌期，THE SYSTEM SHALL 由玩家侧忽略伤害（敌人侧不做额外无敌判断，避免两处各写一半）。
- WHEN 敌人攻击命中玩家，THE SYSTEM SHALL 由敌人侧给出命中反馈（`fx_hit` 与命中音效占位）。
- THE SYSTEM SHALL 敌人攻击判定与玩家攻击判定相互独立，互不重复结算同一次命中。

## R10 EnemyConfig 数值配置

> **美术动作**：不涉及 | **代码落点**：`Assets/Scripts/EnemyAI/EnemyConfig.cs`（ScriptableObject）、资产置于 `Assets/Settings/`

- THE SYSTEM SHALL 敌人与炮台的全部战斗数值配置于 `EnemyConfig`（ScriptableObject），代码不硬编码；至少包含：HP、移速、攻击力、攻击间隔、攻击距离、感知半径、脱战时长、受击硬直时长、受击击退系数、弹体速度、弹体最大射程、掉落表、动画帧率。
- THE SYSTEM SHALL 为 Enemy01、Enemy02、Enemy03、Tower01 各提供一份可编辑的 `EnemyConfig` 资产（可共享同一字段结构，按单位各配一份数值）。
- THE SYSTEM SHALL 落地 GDD §8 设计初值：Enemy01 / Enemy02 / Enemy03 的 HP 分别为 3 / 4 / 5；其余数值（移速、攻击力、攻击间隔、感知半径等）以 `EnemyConfig` 资产为唯一事实来源，实现完成后回填 GDD §8。
- WHEN 对应 `EnemyConfig` 资产缺失，THE SYSTEM SHALL 使用代码内默认值并在控制台输出警告（与 M2 `PlayerConfig`、M3 `CombatConfig` 同一兜底策略）。
- THE SYSTEM SHALL 敌人基础数值与玩家攻击力解耦（玩家攻击力仍取自 `CombatConfig`，敌人只读取自身 `EnemyConfig`）。

## R11 动画参数驱动与事件帧

> **美术动作**：Enemy01/02 = Idle/Move/Attack/Hurt/Die；Enemy03 = Idle/Chase/Attack/Hurt/Die/Spawn；Tower01 = Idle/Aim/Fire/Disable | **代码落点**：`Assets/Scripts/EnemyAI/EnemyAnimatorDriver.cs`（`EnemyAnimParams` 常量 + 参数写入）

- THE SYSTEM SHALL 敌人动画驱动与玩家一致：仅通过 Animator 参数（Float / Bool / Trigger）与 Animator 通信，逻辑代码不直接引用 `AnimationClip` 或 `Animation` 组件。
- THE SYSTEM SHALL 使用美术实有动作名组成敌人动画状态集：Enemy01 / Enemy02 为 Idle / Move / Attack / Hurt / Die；Enemy03 为 Idle / Chase / Attack / Hurt / Die / Spawn；Tower01 为 Idle / Aim / Fire / Disable。
- WHEN `Attack` 类动作播放到 `HitOn` / `HitOff` 事件帧，THE SYSTEM SHALL 由 Animation Event 调用敌人攻击判定盒的开窗 / 关窗方法（帧号与排期表「动画事件帧」列严格对应）。
- WHEN `Die` 动作播放到 `DieEnd` 事件帧，THE SYSTEM SHALL 触发 R8 的死亡回收与结算流程。
- WHEN 敌人处于持续型行为（Idle / Move / Chase / Aim），THE SYSTEM SHALL 使用 Bool 或 Float 参数表达；一次性行为（Attack / Hurt / Die / Spawn / Fire）THE SYSTEM SHALL 使用 Trigger 触发。
- THE SYSTEM SHALL 由常量类集中定义敌人动画参数名（与 M2 `PlayerAnimParams` 同策略），代码中不出现裸字符串参数名。
- WHERE 敌人动画控制器由一键搭建工具或手工生成，THE SYSTEM SHALL 保证参数名与常量类一致；参数名不一致时以常量类为准修正动画控制器。

## 验收方式

- 编译零报错；为 Enemy01/02/03 与 Tower01 各创建预制体并放入测试场景，进入 Play 逐条验证
- 受击链路：用玩家三段攻击命中敌人，确认 `TakeDamage` 回调、按 `EnemyConfig` 扣血、`Hurt` 播放与硬直、HP 归零转 `Die`
- 敌人行为逐条走查：Enemy01 慢速接近 → Attack 开窗命中玩家 → 受击 → DieEnd 归池；Enemy02 毒气弹体生成/命中/触地/超程回收；Enemy03 Spawn 出场 → Chase 高速追击 → Attack 冷却；Tower01 Idle → Aim → Fire 循环，及收到关闭信号后进入 Disable 且不再开火（幂等重发不报错）
- 回流链路：敌人攻击命中玩家，确认玩家掉 HP、沿来源反方向击退、硬直与无敌期闪烁；无敌期内敌人攻击不再扣血
- 事件与池：订阅敌人死亡事件打印日志并统计，确认每次死亡"广播一次 + 归还一次"；连续击杀 20 个敌人后 Profiler 无新增实例、无每帧 GC 分配
- 配置与动画：切换不同 `EnemyConfig` 数值（如移速/HP）立即生效；Animator 窗口目检各敌人状态转移与参数驱动正确