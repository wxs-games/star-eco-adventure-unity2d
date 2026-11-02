# 任务清单：04-EnemyAI

> 对应 requirements.md R1~R11（73 条 EARS）：敌人 FSM 基类（R1）、玩家感知与目标锁定（R2）、Enemy01 慢速近战（R3）、Enemy02 远程毒气（R4）、Enemy03 高速追击+出场（R5）、Tower01 毒气防御塔 Aim→Fire→Disable（R6）、受击与 IHittable 对接（R7）、死亡/事件广播/对象池回收（R8）、敌人攻击对玩家伤害回流（R9）、EnemyConfig 数值配置（R10）、动画参数驱动与事件帧（R11）。命名空间 `StarEco.EnemyAI`，代码落点 `Assets/Scripts/EnemyAI/`，对 M3 只实现 `IHittable` + 调用 `TakeDamage`，不改 M3 任何一行源码。

## 一、通用层：基类与接口

- [ ] T1 `EnemyConfig.cs`（ScriptableObject，R10）：新建 `Assets/Scripts/EnemyAI/EnemyConfig.cs`，字段按 design §2.9 全量定义（EnemyTypeId/IsStatic/AttackKind/MaxHP/KnockbackResponse/MoveSpeed/AttackDamage/AttackRange/AttackInterval/AttackKnockback/PerceptionRadius/LoseTargetSeconds/LoseTargetBuffer/SameGroundToleranceY/HurtStunSeconds/ProjectileSpeed/ProjectileMaxRange/ProjectilePrewarm/AimSeconds/FireSeconds/SpawnSeconds/DropTable/AnimFrameRate），带 `[Header]`/`[Tooltip]`，`[CreateAssetMenu(menuName="StarEco/Enemy Config")]`，`static CreateDefault()` 兜底 + `Debug.LogWarning`（与 PlayerConfig/CombatConfig 同策略）。验收：字段齐全、零硬编码战斗数值、资产缺失走兜底告警。
- [ ] T2 `EnemyStateMachine.cs`（`IEnemyState` + 敌人状态机，R1）：新建 `Assets/Scripts/EnemyAI/EnemyStateMachine.cs`，定义 `IEnemyState`（Enter/Exit/Tick/FixedTick 四段式）与 `EnemyStateMachine`（`Register(state)` 字典键=state.GetType()、重复注册后者覆盖、`SetInitial<T>()` + `SetInitial(Type)` 非泛型重载、`SwitchState<T>()` 保证「先 Exit 当前态再 Enter 新态」、`Tick`/`FixedTick` 转发当前态）。验收：独立于 `StarEco.Player` 命名空间，不引用 `IPlayerState`/`PlayerStateMachine`，四段式与 M2 对齐。
- [ ] T3 `EnemyStateBase.cs`（状态公共基类，R1）：新建 `Assets/Scripts/EnemyAI/EnemyStateBase.cs`，持有 `EnemyControllerBase` + `EnemyStateMachine` 引用，提供 `TryHandleDieTransition()` / `TryHandleHurtTransition()` / `TryHandleCombatTransitions()` 快捷检查（优先级 死亡>受击>攻击>追击/移动>待机）。验收：状态类不引用各敌种 Controller 具体类型，一切转移经 `Machine.SwitchState<T>()`。（依赖 T2）
- [ ] T4 `EnemyAnimatorDriver.cs` + `EnemyAnimParams` 常量（R11）：新建 `Assets/Scripts/EnemyAI/EnemyAnimatorDriver.cs`，`EnemyAnimParams` 静态常量类（`Animator.StringToHash`：Speed/IsMove/IsAiming/Attack/Fire/Hurt/Die/Spawn），`EnemyAnimatorDriver` 每帧 `SetFloat/SetBool` + `FireOneShot(int)` 一次性 Trigger，`ResetParams()` 清 Float/Bool/Trigger。验收：代码零裸字符串参数名，`[RequireComponent(typeof(Animator))]`，与 M2 `PlayerAnimParams` 同策略。

## 二、事件结构

- [ ] T5 `Events/EnemyDeathEvent.cs` + `Events/TowerDisableEvent.cs`（R8、R6）：新建 `Assets/Scripts/EnemyAI/Events/EnemyDeathEvent.cs`（struct：`EnemyTypeId` string + `Position` Vector2 + `DropTable` ScriptableObject 占位透传）与 `Assets/Scripts/EnemyAI/Events/TowerDisableEvent.cs`（struct：塔标识 + 位置，供 M5/M6 订阅）。验收：事件为 struct（零 GC），`DropTable` 允许 null（M5 落地前）。

## 三、控制器基类、攻击判定与弹体

- [ ] T6 `EnemyControllerBase.cs`（R2、R7、R8）：新建 `Assets/Scripts/EnemyAI/EnemyControllerBase.cs`，`MonoBehaviour, IHittable`。持 HP/目标/感知半径/脱战计时/硬直/击退/攻击冷却/死亡 one-shot 守卫；`SenseTarget()`（Update 内、Machine.Tick 之前：`IsDead||IsSpawnImmune` 短路、已锁定用 `PerceptionRadius+LoseTargetBuffer` 防抖、`Physics2D.OverlapCircleNonAlloc` + 同层垂直容差、脱战计时超时清目标）、`DecideCombatState()`、`MoveTowardsTarget()`、`Recycle()`（`PoolManager.Release`，禁 Destroy，`_sourcePrefab` 为空兜底 SetActive(false)+告警）、`BroadcastDeath()`（`EventCenter.Publish(EnemyDeathEvent)`，`_recycleGuard` 至多一次）、`ResetForReuse()`（复位 HP/状态/Animator 参数/速度/朝向）。`TakeDamage(DamageInfo)` 判定顺序：`IsDead`→return、`IsSpawnImmune`→return、扣 HP、记 `_pendingHurt`+击退、HP 归零转 Die。朝向用 `SpriteRenderer.flipX` 不缩放 Transform。玩家死亡清目标用「Layer 探测 + 脱战计时」双保险，不引用 `StarEco.Player` 具体类型。验收：零具体类型引用、每帧零 GC（OverlapCircleNonAlloc + 复用数组）、`GetComponent` 全在 Awake 缓存。（依赖 T1、T2、T5）
- [ ] T7 `EnemyAttackHitbox.cs`（R9）：新建 `Assets/Scripts/EnemyAI/EnemyAttackHitbox.cs`，`[RequireComponent(typeof(EnemyControllerBase))]`。`OnAttackWindowOpen()`/`OnAttackWindowClose()`（Animation Event：HitOn/HitOff）、`OnTriggerEnter2D` 首行 `if(!_windowOpen) return`、`HashSet<IHittable>` 单窗去重、要求目标来自 PlayerLayer（不误伤敌方）、发 `DamageInfo{AttackDamage, 反向朝向, 敌人GO, AttackKnockback}`、命中反馈 `fx_hit` 占位 + `AudioManager.PlaySfx` 占位（clip 可空）、朝向翻转时设 `localPosition.x` 符号保证盒在面朝侧。验收：开窗期外不命中、单窗单目标一次结算、玩家无敌由玩家侧处理（敌人侧不做无敌判断）、不改玩家 `AttackHitbox`。（依赖 T6）
- [ ] T8 `EnemyProjectile.cs`（R4、R6、R9）：新建 `Assets/Scripts/EnemyAI/EnemyProjectile.cs`，`Launch(ProjectileParams)`（Direction/Speed/MaxRange/Damage/Knockback/Source/OwnerLayer）、FixedUpdate 经 `Rigidbody2D.MovePosition` 位移（Kinematic、gravityScale=0）、累计飞行距离、终止条件先到先算（命中 PlayerLayer 且挂 IHittable→结算一次+fx_gas→归还；命中 GroundLayer→不结算归还；距离>=MaxRange→不结算归还）、`_resolved` 标志单目标单次、`other.transform.root==Source.transform.root` 或 `layer==OwnerLayer`→忽略（不伤发射者与敌方）。验收：位移只在 FixedUpdate、池化不即时 Instantiate、fx_gas 仅命中玩家时生成、触地/超程不生成特效。（依赖 T1）

## 四、共享状态集

- [ ] T9 `EnemyStates.cs`（R1、R3、R4、R5）：新建 `Assets/Scripts/EnemyAI/EnemyStates.cs`，含 `EnemyIdleState`（待机/感知）、`EnemyChaseState`（水平追击）、`EnemyMeleeAttackState`（近战开窗+冷却，Enter 置 `AttackCooldownTimer=Config.AttackInterval`）、`EnemyRangedAttackState`（远程生成弹体+冷却）、`EnemyHurtState`（硬直+击退，Tick 递减硬直后按 `DecidePostHurtState()` 回战斗/Idle）、`EnemyDieState`（Enter 关碰撞/判定/停移动、FireOneShot(Die)、等 `DieEnd` 事件帧→`OnDieEnd()` 走回收+广播）。每状态 Tick 首行调 `TryHandleCombatTransitions()`，动画与物理开关只在 Enter/Exit。验收：6 个共享状态覆盖 Enemy01/02/03 行为差异（差异由 Config 数值 + Animator 参数表达），状态内部不直接调其他状态方法。（依赖 T3、T6）

## 五、各敌种控制器

- [ ] T10 `Enemy01/Enemy01Controller.cs`（R3）：新建 `Assets/Scripts/EnemyAI/Enemy01/Enemy01Controller.cs`，继承 `EnemyControllerBase`，注册 Idle/Move/Attack(近战)/Hurt/Die（共享状态），初始态 Idle，`Enemy01Config` 慢速近战。验收：无专属状态文件，全复用 `EnemyStates.cs`。（依赖 T9）
- [ ] T11 `Enemy02/Enemy02Controller.cs`（R4）：新建 `Assets/Scripts/EnemyAI/Enemy02/Enemy02Controller.cs`，继承 `EnemyControllerBase`，注册 Idle/Move/Attack(远程)/Hurt/Die，`OnProjectileSpawn()`（HitOn 事件帧）取 `FirePoint.position` + 当前朝向经 `PoolManager.Get(EnemyProjectile)` 生成弹体，远程攻击距离大于 Enemy01。验收：弹体经对象池获取不即时实例化。（依赖 T9、T8）
- [ ] T12 `Enemy03/Enemy03Controller.cs` + `Enemy03/Enemy03SpawnState.cs`（R5）：新建 `Assets/Scripts/EnemyAI/Enemy03/Enemy03Controller.cs`（注册 Spawn/Idle/Chase/Attack/Hurt/Die，重写 `InitialStateType => typeof(Enemy03SpawnState)`，高速追击 MoveSpeed 显著大于 Enemy01）与 `Assets/Scripts/EnemyAI/Enemy03/Enemy03SpawnState.cs`（播放 Spawn Trigger、期间 `IsSpawnImmune=true` 不移动不攻击不感知、`OnSpawnEnd()` 转 Idle、`SpawnSeconds>0` 时配置兜底强制退出）。验收：出场期三态同时成立（无敌/不移动/不攻击），缺事件也能出场。（依赖 T9）
- [ ] T13 `Tower01/Tower01Controller.cs` + `Tower01/Tower01States.cs`（R6）：新建 `Assets/Scripts/EnemyAI/Tower01/Tower01Controller.cs`（注册 Idle/Aim/Fire/Disable，`IsStatic=true`、`KnockbackResponse=0`、无 Rigidbody2D、`RequestDisable()` 幂等入口 `if(_isDisabled) return`、`_isDisabledOneShot` 守卫、`OnFireSpawn()` 取 FirePoint 发射弹体、`OnAimEnd()`/`OnFireEnd()` 事件帧+Config 兜底时长、`RequestReactivate()` 空壳供 M6）与 `Assets/Scripts/EnemyAI/Tower01/Tower01States.cs`（`Tower01IdleState`/`Tower01AimState`（flipX 朝玩家不旋转）/`Tower01FireState`（Fire Trigger+冷却→Aim 循环）/`Tower01DisableState`（终态、停感知、关判定、不再生成弹体、广播 `TowerDisableEvent` 仅首次一次））。验收：Idle→Aim→Fire→Aim 循环、Disable 幂等不重播不报错不重复广播、M4 不自行恢复。（依赖 T9、T5）

## 六、EnemyConfig 资产

- [ ] T14 4 份 `EnemyConfig` 资产（R10）：创建 `Assets/Settings/Enemy01Config.asset` / `Enemy02Config.asset` / `Enemy03Config.asset` / `Tower01Config.asset`，HP 初值 3/4/5（GDD §8），Tower01 `IsStatic=true`/`KnockbackResponse=0`/`MaxHP=0`（不可死，走 Disable），Enemy02 `AttackKind=Ranged`/`AttackRange` 大于 Enemy01，Enemy03 `MoveSpeed` 显著大于 Enemy01/`SpawnSeconds>0`，其余字段按 design §2.9 默认值填。验收：每单位一份资产、同字段结构各配数值、单一事实来源。（依赖 T1）

## 七、Prefab

- [ ] T15 `Enemy01.prefab`（R3）：创建 `Assets/Prefabs/Enemies/Enemy01.prefab`，根挂 Rigidbody2D（Dynamic、freezeRotation=true）+BoxCollider2D（非 Trigger）+SpriteRenderer+Animator（Enemy01 Controller）+Enemy01Controller+EnemyAnimatorDriver，子物体 AttackHitbox（EnemyAttackHitbox + BoxCollider2D IsTrigger=true）。验收：结构同 design §1.2。（依赖 T10、T14）
- [ ] T16 `Enemy02.prefab`（R4）：创建 `Assets/Prefabs/Enemies/Enemy02.prefab`，结构同 Enemy01 + FirePoint 子物体（弹体生成锚点）。验收：FirePoint 在面朝侧。（依赖 T11、T14）
- [ ] T17 `Enemy03.prefab`（R5）：创建 `Assets/Prefabs/Enemies/Enemy03.prefab`，结构同 Enemy01（Animator 挂 Enemy03 Controller）。验收：初始态 Spawn。（依赖 T12、T14）
- [ ] T18 `Tower01.prefab`（R6）：创建 `Assets/Prefabs/Towers/Tower01.prefab`，根挂 BoxCollider2D（非 Trigger，无 Rigidbody2D）+SpriteRenderer+Animator（Tower01 Controller）+Tower01Controller+EnemyAnimatorDriver，子物体 FirePoint。验收：固定单位无位移、无 Rigidbody2D。（依赖 T13、T14）
- [ ] T19 `EnemyProjectile.prefab`（R4、R6）：创建 `Assets/Prefabs/Projectiles/EnemyProjectile.prefab`，根挂 Rigidbody2D（Kinematic、gravityScale=0）+CircleCollider2D（IsTrigger=true）+SpriteRenderer+EnemyProjectile。验收：位移只在 FixedUpdate、可池化复用。（依赖 T8）

## 八、Animator Controller 与动画事件帧

- [ ] T20 4 份 Animator Controller（R11）：为 Enemy01/02/03/Tower01 各创建 Animator Controller，参数名与 `EnemyAnimParams` 常量严格一致（Speed/IsMove/IsAiming/Attack/Fire/Hurt/Die/Spawn），状态转移按 design §2.10 映射表（Enemy01/02=Idle/Move/Attack/Hurt/Die；Enemy03=Spawn/Idle/Chase/Attack/Hurt/Die；Tower01=Idle/Aim/Fire/Disable），持续型用 Bool/Float、一次性行为用 Trigger。验收：参数名以常量类为准、Tower01 Disable 用专用 Trigger 或复用 Die Trigger（与美术对齐后定稿）。（依赖 T4）
- [ ] T21 Animation Event 帧对齐校验（R11）：为各 clip 补 Animation Event，方法名与挂载组件按 design §2.10（HitOn→`OnAttackWindowOpen`、HitOff→`OnAttackWindowClose`、FireSpawn→`OnFireSpawn`、ProjectileSpawn→`OnProjectileSpawn`、AimEnd→`OnAimEnd`、FireEnd→`OnFireEnd`、SpawnEnd→`OnSpawnEnd`、DieEnd→`OnDieEnd`），帧号与排期表「动画事件帧」列严格对应，逐 clip Play 目检。验收：无"判定早于表现"或"永不命中"，事件帧为权威、Config 时长仅兜底。（依赖 T20）

## 九、场景接入与碰撞矩阵

- [ ] T22 Layer / Collision Matrix 配置（R9）：配置物理层与碰撞矩阵——玩家攻击盒↔敌人层、敌人攻击盒↔玩家层、弹体↔玩家层+地面层，互不误伤（敌人互殴、弹体打中敌人自身均禁止）。验收：Play 下玩家攻击只命中敌人、敌人攻击只命中玩家、弹体只命中玩家/地面。（依赖 T15、T16、T17、T18、T19）
- [ ] T23 测试场景接入（R1~R11）：将 Enemy01/02/03 与 Tower01 预制体摆入测试场景，订阅 `EnemyDeathEvent` 打印日志并统计，准备玩家可攻击路径。验收：4 种单位均可 Play 激活、死亡事件可观测。（依赖 T22）

## 十、编译与运行验证

- [ ] T24 Unity 编译零报错 + 控制台检查：Refresh Unity，确认 Console 无 error/warning（资产缺失告警除外），15 个 .cs 全部编译通过。验收：零报错。（依赖 T23）
- [ ] T25 运行验证（需用户配合按 Play 目检）：逐条走查——受击链路（玩家三段攻击命中敌人→TakeDamage 回调→按 Config 扣血→Hurt 播放+硬直→HP 归零转 Die）；Enemy01 慢速接近→Attack 开窗命中玩家→受击→DieEnd 归池；Enemy02 毒气弹体生成/命中/触地/超程回收；Enemy03 Spawn 出场→Chase 高速追击→Attack 冷却；Tower01 Idle→Aim→Fire 循环 + 收到关闭信号进 Disable 且不再开火（幂等重发不报错）；回流链路（敌人攻击命中玩家→掉 HP+反向击退+硬直+无敌闪烁，无敌期内不再扣血）；事件与池（每次死亡"广播一次+归还一次"，连杀 20 个后 Profiler 无新增实例、无每帧 GC）；配置与动画（切换 Config 数值立即生效，Animator 状态转移目检正确）。验收：逐条通过。（依赖 T24）
- [ ] T26 git commit：提交 M4 敌人 AI 全部代码与资产，commit message `M4: enemy AI — FSM/perception/Enemy01-03/Tower01/IHittable/pool`。验收：commit 含 15 个 .cs + 4 份 Config 资产 + 5 个 Prefab + 4 份 Animator Controller + 场景/层配置变更。（依赖 T25）

> 状态：待编码。共 26 个任务（15 个 .cs + 4 份 Config 资产 + 5 个 Prefab + 4 份 Animator Controller + 动画事件帧校验 + 层/场景接入 + 编译/运行验证 + git 提交），覆盖 R1~R11 全部 73 条 EARS。对 M3 边界：只实现 `IHittable` + 调用 `TakeDamage`，不改 M3 任何一行源码。
