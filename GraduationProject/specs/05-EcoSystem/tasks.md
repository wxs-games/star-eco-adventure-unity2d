# 任务清单：05-EcoSystem

> 对应 requirements.md R1~R10（约 50 条 EARS）：净化协议（R1）、喷壶喷洒（R2）、蓄力净化波（R3）、净化剂资源（R4）、垃圾堆与挡路重垃圾（R5）、净化值系统（R6）、污染度氛围（R7）、Tower01 净化适配（R8）、掉落与拾取（R9）、EcoConfig 与玩家状态接入（R10）。命名空间 `StarEco.Eco`（玩家状态类沿用 M3 先例：文件置于 `Eco/`、命名空间 `StarEco.Player`）；代码落点 `Assets/Scripts/Eco/`；对 M3/M4 只调用 `IHittable`/`TakeDamage`/`RequestDisable` 与订阅 `EnemyDeathEvent`/`TowerDisableEvent`，不改其任何一行源码；对 M2 的最小增量见 T10（已核实：`PlayerAnimParams.Interact`（"M5 使用"注释已预留）与 `HeavyAttack` 常量均已存在，零新增参数名；M4 `DropTable` 字段为 `ScriptableObject` 占位类型，M5 新建具体类后直接兼容赋值）。

## 一、配置与协议层

- [x] T1 `EcoConfig.cs`（R10）：新建 `Assets/Scripts/Eco/EcoConfig.cs`，ScriptableObject，字段按 design §3.1 全量定义（25 个：SprayDPS/SprayTickSeconds/TankCapacity/InitialTank/TankConsumePerSecond/EnemySprayCostMultiplier/HeavyChargeSeconds/HeavyPurifyAmount/HeavyDamage/HeavyKnockback/HeavyCost/HeavyRecoverSeconds/SprayMoveSpeedFactor/GarbagePurifyRequired/GarbageWallPurifyRequired/GarbagePurityValue/GarbageWallPurityValue/TowerPurityValue/TowerPurifyRequired/PickupTankAmount/PollutedColor/CleanColor/AtmosphereLerpSpeed/GarbagePollutedTint/GarbageCleanTint），带 `[Header]`/`[Tooltip]`，`[CreateAssetMenu(menuName="StarEco/Eco Config")]`，`static CreateDefault()` 兜底 + `Debug.LogWarning`（与 PlayerConfig/EnemyConfig/CombatConfig 同策略）。验收：字段与 §3.1 一一对应、零硬编码数值、资产缺失走兜底告警。
- [x] T2 `IPurifiable.cs`（R1）：新建 `Assets/Scripts/Eco/IPurifiable.cs`，接口：`ReceivePurify(int amount, GameObject source)`、`bool IsPurified { get; }`、`int PurityValue { get; }`。验收：独立于 `StarEco.Combat.IHittable`（不引用任何战斗类型），净化与伤害双通道的协议锚点。
- [x] T3 事件结构 ×5（R4、R6、R9）：新建 `Assets/Scripts/Eco/Events/` 下 5 个文件——`PurifyTankChangedEvent.cs`（Current/Capacity）、`PurityChangedEvent.cs`（Current/Total/Percent/Pollution）、`GarbagePurifiedEvent.cs`（PurityValue/Position/TargetId）、`LevelPurifiedEvent.cs`（空载荷 one-shot）、`PickupCollectedEvent.cs`（PickupKind/Amount）。验收：全部 struct（零 GC）、经 `EventCenter` 广播、与 design §3.2 事件表一一对应。

## 二、资源与通用特效

- [x] T4 `PurifyTank.cs`（R4）：新建 `Assets/Scripts/Eco/PurifyTank.cs`，玩家挂载组件：容量/当前值（Awake 从 `EcoConfig` 读 TankCapacity/InitialTank）、`Consume(int)`/`Restore(int)` 返回实际变化并夹取 0~容量、`IsEmpty` 只读属性、变更时 `Publish(PurifyTankChangedEvent)`。验收：耗尽后 Consume 不产生负值、事件携带当前值与容量、数值全部来自 Config。
- [x] T5 `FxOneShot.cs`（R2、R5）：新建 `Assets/Scripts/Eco/FxOneShot.cs`，`[RequireComponent(typeof(Animator))]`；Update 检测 `GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f` → `PoolManager.Release(gameObject)`（`_sourcePrefab` 为空时兜底 SetActive(false)+告警，同 M4 Recycle 模式）。验收：通用组件不绑定 fx_purify（M6+ 可复用于其他 fx_*）、播完自动归池不 Destroy。

## 三、喷壶主体与玩家状态

- [x] T6 `SprayHitbox.cs`（R2）：新建 `Assets/Scripts/Eco/SprayHitbox.cs`，Player 子物体：`BoxCollider2D(IsTrigger=true)`；目标条目 struct（Collider2D + `IPurifiable`/`IHittable` 缓存引用）维护列表——`OnTriggerEnter2D` 加入（GetComponent 仅此一次）、`OnTriggerExit2D` 移除、`OnTriggerStay2D` 兜底补录（防先激活后重叠时 Enter 丢失）；公开只读目标列表 + `RemoveTarget(Collider2D)`；结算前对 Unity-null 条目剔除（M4 池化教训：对象 SetActive(false) 时 Unity 补发 Exit + null 双保险）。验收：零每帧 GetComponent、池化目标中途失活不产生 NRE。
- [x] T7 `SprayController.cs`（R2、R3、R8）：新建 `Assets/Scripts/Eco/SprayController.cs`，喷壶主组件：①输入——Update 读 K/L（`Input.GetKey/GetKeyUp`，沿用 M2 直接读取约定），暴露 `SprayHeld`/`ChargeHeld` 与 `TryConsumeSprayStart()`/`TryConsumeChargeStart()`（Consume 模式同 M3 `ConsumeAttackPressed`，防同帧重复消费；进入门条件：喷洒需 Tank>0、蓄力需 Tank≥HeavyCost 且非攻击/受击/死亡态）；②喷洒周期结算——计时器达 `SprayTickSeconds` 遍历 SprayHitbox 目标分流：`IPurifiable` → `ReceivePurify(Mathf.CeilToInt(SprayDPS × Tick))`；`IHittable` 且 layer ∈ EnemyLayer → `TakeDamage(DamageInfo{Amount, 无击退, 玩家 GO})`（R8 双通道）；消耗 `TankConsumePerSecond × Tick × (本 tick 命中敌方 ? EnemySprayCostMultiplier : 1)`；③fx——每 2 tick 在判定盒中心 `PoolManager.Get(fx_purify)` 播放；④镜像——每帧按 `SpriteRenderer.flipX` 设 hitbox `localPosition.x` 符号（M4 FirePoint 同款，不依赖 transform 缩放）；⑤蓄力波——`ReleasePurifyWave()`：`Physics2D.OverlapBoxNonAlloc`（复用静态数组）取玩家前方矩形范围 → `IPurifiable` 各收 `HeavyPurifyAmount`、敌方 `IHittable` 收 `DamageInfo{HeavyDamage, HeavyKnockback}`，同帧结算完毕（R3：不等动画事件帧）；暴露 `IsSpraying`/`SpraySpeedFactor`。验收：R2/R3 全部 EARS 覆盖、Update 内零 Find/零每帧 GetComponent、物理查询非分配。
- [x] T8 `PlayerSprayState.cs`（R2、R10）：新建 `Assets/Scripts/Eco/PlayerSprayState.cs`（命名空间 `StarEco.Player`，参照 `Combat/PlayerAttackState` 先例）：Enter 缓存 `SprayController`（GetComponent 一次）+ 开判定盒 + `TryFireAnim(PlayerAnimParams.Interact)`；FixedTick 调 `Player.MoveHorizontally(grounded, SpraySpeedFactor)`（减速不锁死）；Tick 首行 `TryHandleCombatTransitions()`（受击/死亡优先打断）→ 空中转 `PlayerAirState`（喷洒仅地面）→ `!SprayHeld || Tank.IsEmpty` → `SwitchGroundLocomotion()`。验收：播 Interact、判定盒开关与按住期一致、松开/耗尽自动退出不报错（R2）、受击优先。
- [x] T9 `PlayerChargeState.cs`（R3、R10）：新建 `Assets/Scripts/Eco/PlayerChargeState.cs`（命名空间 `StarEco.Player`）：Enter 站定（`StopHorizontal`）+ 缓存；Tick：`TryHandleCombatTransitions()` → `ChargeHeld` 期间累计 chargeTimer（达 `HeavyChargeSeconds` 置蓄满标志并保持）→ 松开：未满取消回 `SwitchGroundLocomotion()`（零消耗零效果）；蓄满 → `spray.ReleasePurifyWave()` + `Tank.Consume(HeavyCost)` + `TryFireAnim(PlayerAnimParams.HeavyAttack)` + `HeavyRecoverSeconds` 计时后回 `SwitchGroundLocomotion()`。验收：R3 四分支全覆盖（未满取消/蓄满释放/不足不进入/蓄满保持不重复消耗）、与喷洒互斥（本状态不检查 SprayHeld）。
- [x] T10 玩家侧接入增量（R10）：`Assets/Scripts/Player/PlayerController.cs` 三处最小增量——①`public SprayController Spray { get; private set; }` + Awake 缓存（参照既有 `Combat` 属性模式，约 3 行）；②`BuildStateMachine()` 注册 `PlayerSprayState`/`PlayerChargeState` 两行（PlayerController.cs:66-73 注册块之后）；③`MoveHorizontally(bool inAir)` 增加可选参数 `float speedFactor = 1f` 乘入 targetSpeed（PlayerController.cs:163，既有调用点零改动）；`Assets/Scripts/Player/PlayerStates.cs`——`PlayerIdleState`/`PlayerRunState` 的 Tick 在攻击转移之后各加喷洒/蓄力进入转移（`Player.Spray != null && Player.Spray.TryConsumeSprayStart()` → `SwitchState<PlayerSprayState>()`，蓄力同构，每处约 4 行）。验收：Player/ 目录增量合计约 15 行且全部服务 R2/R3/R10 必需功能（TDD §2.1 "Player/ 净化喷雾 ⏳ M5" 允许）；M3 夹钳攻击（J）/跳跃（Space）/受击/死亡回归不受影响（T23 复验）。

## 四、可净化目标

- [x] T11 `GarbagePile.cs`（R5）：新建 `Assets/Scripts/Eco/GarbagePile.cs`，实现 `IPurifiable`：`[SerializeField]` purifyRequired/purityValue/blocksPath（字段 -1 = 跟随 EcoConfig 按 blocksPath 分流的默认值，≥0 显式覆盖，保留单一事实来源 + 关卡微调）；`ReceivePurify` 幂等守卫（IsPurified 直接 return）→ 累计进度 → `SpriteRenderer.color = Lerp(GarbagePollutedTint, GarbageCleanTint, progress)` 即时反馈 → 达阈值：`PoolManager.Get(fx_purify)` 播放 + `Publish(GarbagePurifiedEvent)` + blocksPath 时关闭 BoxCollider2D（通路打开）并降 alpha 至 0.3。验收：R5 全条（颜色插值、幂等、挡路开路、普通/挡路阈值差 4 vs 60）、进度中断不衰减（design §2.1）。
- [x] T12 `TowerPurifiable.cs`（R8）：新建 `Assets/Scripts/Eco/TowerPurifiable.cs`，`[RequireComponent(typeof(Tower01Controller))]`，实现 `IPurifiable`：purifyRequired/purityValue 从 EcoConfig（TowerPurifyRequired=30/TowerPurityValue=10）或字段覆盖；`ReceivePurify` 幂等 → 累计 → 完成时 `GetComponent<Tower01Controller>().RequestDisable()`（M4 幂等保证）+ `Publish(GarbagePurifiedEvent)`。验收：零改 M4 源码（只调用 RequestDisable）、净化完成事件与 M4 既有 TowerDisableEvent 各广播一次互不冲突。

## 五、关卡级系统

- [x] T13 `PuritySystem.cs`（R6）：新建 `Assets/Scripts/Eco/PuritySystem.cs`，场景级组件：Start 一次性收集场景内全部 active 的 `IPurifiable`（`FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude)` 过滤，仅 Start 允许 Find）→ TotalValue = Σ；广播基线 `PurityChangedEvent{0, Total, 0, 1.0}`；订阅 `GarbagePurifiedEvent` → CurrentValue += PurityValue → 广播 `PurityChangedEvent`（携带当前/总量/百分比/污染度）→ 100% 时 one-shot `LevelPurifiedEvent`；Total==0 防御分支：初始化 100% + `Debug.LogWarning`。验收：只收 active（池化敌人不参与，design §2.4）、净化值只增不减、LevelPurifiedEvent 全程至多一次。
- [x] T14 `AtmosphereController.cs`（R7）：新建 `Assets/Scripts/Eco/AtmosphereController.cs`：Awake 缓存 `Camera.main`；Start 立即应用污染度 100% 对应 `PollutedColor`（不瞬跳基线）；订阅 `PurityChangedEvent` → 目标色 = `Color.Lerp(CleanColor, PollutedColor, Pollution)`；Update 每帧向目标色平滑（`AtmosphereLerpSpeed × deltaTime`）；暴露 `CurrentPollution` 只读查询（M6 刷怪预留）。验收：仅插值 `Camera.backgroundColor`（零后处理栈）、平滑过渡不瞬跳。

## 六、掉落与拾取

- [x] T15 `DropTable.cs`（R9）：新建 `Assets/Scripts/Eco/DropTable.cs`，ScriptableObject：`[Serializable] Entry{PickupKind 枚举（初期仅 PurifyBottle）/Count/Probability[0~1]}` + `List<Entry>`，`[CreateAssetMenu(menuName="StarEco/Drop Table")]`。验收：允许空表（不掉落）、继承 ScriptableObject 故 M4 既有 `EnemyConfig.DropTable`/`EnemyDeathEvent.DropTable`（ScriptableObject 占位类型）直接兼容赋值，零改 M4。
- [x] T16 `PurifyPickup.cs`（R4、R9）：新建 `Assets/Scripts/Eco/PurifyPickup.cs`：`OnTriggerEnter2D` 判玩家（PlayerLayer）→ 玩家 `PurifyTank.Restore(EcoConfig.PickupTankAmount)` → `Publish(PickupCollectedEvent)` → `PoolManager.Release`（`_sourcePrefab` 兜底同 T5）。验收：Trigger 自动拾取无按键、归池不 Destroy、碰撞回调内一次性 GetComponent 允许。
- [x] T17 `DropSpawner.cs`（R9）：新建 `Assets/Scripts/Eco/DropSpawner.cs`，场景级：OnEnable 订阅 `EnemyDeathEvent`/OnDisable 退订；`DropTable == null` → 直接 return 不报错；逐条目 `Random.value <= Probability` → `PoolManager.Get(PurifyPickup)` × Count → 摆放死亡位置 ± 0.5 随机偏移（y 微调防穿地，不做刚体模拟）。验收：掉落经对象池不即时实例化、概率独立判定、null 表安全。

## 七、Config 资产

- [x] T18 `EcoConfig.asset` + 测试掉落表（R10、R9）：创建 `Assets/Settings/EcoConfig.asset`（字段值按 design §3.1 默认值全量填写）与 `Assets/Settings/Drops/TestDrop.asset`（1 条目：PurifyBottle ×1 概率 100%，链路验证用）。验收：字段与 §3.1 一一对应、单一事实来源、无遗留占位字段。

## 八、Prefab

- [x] T19 4 份 Prefab（R5、R9、R2）：创建 `Assets/Prefabs/Eco/GarbagePile.prefab`（BoxCollider2D 非 Trigger + SpriteRenderer WhiteSquare 占位 + GarbagePile{blocksPath=false}）、`Assets/Prefabs/Eco/GarbageWall.prefab`（同构，blocksPath=true，阈值 60）、`Assets/Prefabs/Eco/PurifyPickup.prefab`（CircleCollider2D IsTrigger + SpriteRenderer 小色块 + PurifyPickup）、`Assets/Prefabs/FX/fx_purify.prefab`（SpriteRenderer + Animator + FxOneShot，6 帧帧序 1→6）。验收：结构同 design §1.2、fx_purify 播完自动归池、Wall 阈值显著高于 Pile。

## 九、Animator Controller

- [x] T20 fx_purify Controller + 玩家 Controller 核对（R2、R3、R11 对应项）：创建 fx_purify 专用 Animator Controller（单状态 one-shot、循环关闭、帧率对齐美术帧目录）；核对既有 `Player.controller`：Interact/HeavyAttack 状态位与转移是否已建（参数 `PlayerAnimParams.Interact`/`HeavyAttack` 已核实存在，零新增参数名），缺状态则按既有 Idle/Run→动画→回 Idle 的 Trigger 驱动模式补建。验收：喷洒播 Interact、蓄力释放播 HeavyAttack、播完回 Idle、零新增参数。

## 十、场景接入与碰撞矩阵

- [x] T21 测试场景接入 + Layer/碰撞矩阵（R1~R9）：Player 增挂 SprayController + PurifyTank + 子物体 SprayHitbox（面朝侧 offsetX）；场景摆放 GarbagePile ×3 / GarbageWall ×1（挡路验证）/ Tower01（增挂 TowerPurifiable）/ PuritySystem / AtmosphereController / DropSpawner；Enemy01Config 的 DropTable 字段挂 TestDrop（类型直接兼容）；碰撞矩阵——SprayHitbox(Trigger)↔垃圾/敌人层、PurifyPickup(Trigger)↔玩家层、垃圾实体↔玩家/敌人层。验收：Play 下各组件激活、垃圾可被喷、墙挡路、塔可净化。

## 十一、编译与运行验证

- [x] T22 Unity 编译零报错 + 控制台检查：Refresh Unity，确认 Console 无 error/warning（资产缺失告警除外），20 个 .cs（Eco/ 内 15 文件 + Events/ 5 文件）+ Player/ 增量全部编译通过。验收：零报错。
- [ ] T23 运行验证（需用户配合按 Play 目检）：逐条走查——喷洒链路（按住 K → 播 Interact + 减速移动 + 垃圾色块渐变 + 2 秒净化完成 → fx_purify + 事件 + 净化值 +5%）；挡路墙（普通喷洒进度缓慢 → 蓄力波 ×1.5 次净化 → Collider 关闭通路打开 + Sprite 变淡）；蓄力（长按 L 站定 → 0.8 秒蓄满 → 松开同帧波结算 + 播 HeavyAttack + 扣 25 净化剂 → 对敌 3 伤害 + 击退 8 → 0.5 秒恢复；未满松开零消耗取消）；净化剂经济（喷洒 8/秒消耗、命中敌加倍 16/秒、耗尽自动退出不报错、拾取瓶 +20）；塔净化（持续喷 3.75 秒 → RequestDisable → TowerDisableEvent + 净化值 +10%，幂等重喷不报错）；氛围（初始灰紫 → 净化推进背景色平滑渐变自然绿 → 100% 时 LevelPurifiedEvent 恰一次）；掉落（杀敌按表概率掉瓶 → 触碰自动拾取归池）；池化（连杀 20 + 连续 fx 后 Profiler 无新增实例、无每帧 GC）；优先级与互斥（喷洒中受击被打断、攻击/受击/死亡中按 K/L 不进入、喷洒与蓄力互斥、J 夹钳攻击与 Space 跳跃回归正常）。验收：逐条通过。
- [ ] T24 git commit：提交 M5 环保玩法全部代码与资产，commit message `M5: eco system — spray/charge/purify/purity/atmosphere/drops`。验收：commit 含 20 个 .cs + Player/ 两文件增量 + EcoConfig/DropTable 资产 + 4 个 Prefab + Animator Controller + 场景/层配置变更。

> 状态：T1~T22 已完成（代码 21 个 .cs + 资产 + 预制体 + Animator + 场景接入，编译零报错）。共 24 个任务（20 个 .cs + Player/ 最小增量 + 2 份资产 + 4 个 Prefab + Animator 核对补建 + 场景/碰撞接入 + 编译/运行验证 + git 提交），覆盖 R1~R10 全部约 50 条 EARS。对 M3/M4 边界：只实现/调用 `IHittable`·`TakeDamage`·`RequestDisable` 与订阅既有事件，零改其源码；对 M2 增量已收敛为 T10 的约 15 行（全部为 R2/R3/R10 必需功能，TDD §2.1 允许）。
