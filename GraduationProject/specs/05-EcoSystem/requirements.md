# 需求规范：05-EcoSystem（环保玩法）

> 对应 TDD.md M5 里程碑 | 格式：EARS
> 范围：`IPurifiable` 净化协议、净化喷壶（喷洒/蓄力两形态）、净化剂资源与拾取、垃圾堆/污染源（普通与挡路重垃圾）、净化值（关卡进度）、污染度与场景氛围（背景色调插值）、Tower01 被净化关闭对接、敌人死亡掉落（DropTable）、`EcoConfig` 数值配置、玩家 Interact/HeavyAttack 动作复用与 `fx_purify` 特效
> 非目标：HUD 面板与进度条 UI 本体（M7，M5 仅提供数据与事件）；污染度驱动的刷怪频率（M6 刷怪系统，M5 仅广播污染度相关事件）；检查点激活与关卡流程/LevelConfig 目标序列（M6）；环保知识卡片与图鉴（M7）；Boss 对净化的特殊反应（M8）；净化音效铺量（M10，仅留 `AudioManager` 调用占位）；NPC 对话与提示牌（M7）
> 共用约定：代码落点 `Assets/Scripts/Eco/`（命名空间 `StarEco.Eco`）；玩家侧新增状态类沿用 M3 先例置于本系统目录（参照 `PlayerAttackState` 位于 `Combat/`）；特效帧目录 `Assets/Art/Art/FX/Frames/fx_purify/`；垃圾堆/补给瓶美术缺失，以 WhiteSquare 色块占位（美术后补仅换 Sprite）；命名与结构遵循项目既有骨架（M1 事件/对象池 + M2 状态机 + M3 伤害流水线 + M4 敌人对接入口 `RequestDisable` / `EnemyDeathEvent` / `TowerDisableEvent`）

## R1 净化协议（IPurifiable）

> **美术动作**：不涉及 | **代码落点**：`Assets/Scripts/Eco/IPurifiable.cs`

- THE SYSTEM SHALL 提供净化目标统一接口 `IPurifiable`：接收净化量 `ReceivePurify(int amount, GameObject source)`、只读属性 `IsPurified` 与 `PurityValue`（净化完成后贡献的净化值）。
- WHEN 一个可净化目标收到净化量，THE SYSTEM SHALL 累加其内部净化进度，未达阈值时不产生任何关卡级结算。
- WHEN 净化进度达到该目标阈值，THE SYSTEM SHALL 将其标记为已净化（幂等：此后再次收到净化量直接忽略）并广播"垃圾/污染源已净化"事件（携带 `PurityValue` 与位置）。
- THE SYSTEM SHALL 净化接口独立于 `StarEco.Combat.IHittable`（净化与攻击是两条通道：净化走 `IPurifiable`，喷壶对污染生物的持续伤害走 `IHittable`/`DamageInfo`，见 R8）。

## R2 净化喷壶·喷洒（SprayController）

> **美术动作**：`Player_Interact`（站定喷洒表现，帧目录 `Assets/Art/Art/Player/Frames/Player_Interact/`） | **代码落点**：`Assets/Scripts/Eco/SprayController.cs`、`Assets/Scripts/Eco/SprayHitbox.cs`、`Assets/Scripts/Eco/PlayerSprayState.cs`

- WHEN 玩家按住喷洒键（K）且净化剂大于 0，THE SYSTEM SHALL 进入喷洒状态：播放 `Player_Interact`、在玩家面朝侧开启喷洒判定盒（Trigger 矩形）、持续消耗净化剂。
- WHILE 喷洒进行中，THE SYSTEM SHALL 按固定周期（每 `EcoConfig.SprayTickSeconds`）对判定盒内的目标结算：`IPurifiable` 目标收到 `SprayDPS × TickSeconds` 净化量；`IHittable` 敌方目标收到 `DamageInfo`（伤害 = `SprayDPS × TickSeconds`，无击退）。
- WHILE 喷洒命中敌方目标（污染生物），THE SYSTEM SHALL 以 `EcoConfig.EnemySprayCostMultiplier`（加倍）的速率消耗净化剂。
- WHEN 喷洒键松开、或净化剂耗尽，THE SYSTEM SHALL 退出喷洒状态、关闭判定盒、回到玩家原有状态。
- IF 玩家处于受击/死亡/攻击中，THE SYSTEM SHALL 不进入喷洒状态（状态优先级沿用玩家 FSM：死亡 > 受击 > 攻击 > 喷洒 > 移动）。
- WHILE 喷洒进行中，THE SYSTEM SHALL 将玩家水平移动速度乘以 `EcoConfig.SprayMoveSpeedFactor`（降低但不锁死，保证横版操作手感）。
- THE SYSTEM SHALL 喷洒判定盒随 `SpriteRenderer.flipX` 镜像到面朝侧（`localPosition.x` 符号翻转，沿用 M4 FirePoint 同款做法）。
- WHILE 喷洒进行中，THE SYSTEM SHALL 在判定盒中心周期性播放 `fx_purify` 特效（经对象池获取，播完自动归还）。
- WHEN 净化剂耗尽时玩家仍按住喷洒键，THE SYSTEM SHALL 自动退出喷洒且不报错、不产生负值净化剂。

## R3 净化喷壶·蓄力净化（HeavyPurify）

> **美术动作**：`Player_HeavyAttack`（蓄力释放表现，帧目录 `Assets/Art/Art/Player/Frames/Player_HeavyAttack/`） | **代码落点**：`Assets/Scripts/Eco/SprayController.cs`（蓄力子流程）、`Assets/Scripts/Eco/PlayerChargeState.cs`

- WHEN 玩家按住蓄力键（L）且净化剂不低于 `EcoConfig.HeavyCost`，THE SYSTEM SHALL 进入蓄力状态：站定、禁止移动与夹钳攻击、显示蓄力进度（数据接口，UI 本体 M7）。
- WHILE 蓄力键持续按住，THE SYSTEM SHALL 累积蓄力时长，达到 `EcoConfig.HeavyChargeSeconds` 为蓄满（蓄满后保持，不重复消耗）。
- WHEN 蓄力未满时松开蓄力键，THE SYSTEM SHALL 取消蓄力（不消耗净化剂、不产生净化效果）。
- WHEN 蓄力已满后松开蓄力键，THE SYSTEM SHALL 扣除 `EcoConfig.HeavyCost` 净化剂、播放 `Player_HeavyAttack`、并在玩家面前释放一次性净化波（矩形范围，随朝向镜像）：范围内全部 `IPurifiable` 各收到 `EcoConfig.HeavyPurifyAmount` 净化量，范围内敌方 `IHittable` 收到一次 `DamageInfo`（伤害 = `EcoConfig.HeavyDamage`，带击退）。
- THE SYSTEM SHALL 净化波判定与结算在同一帧完成（不等动画事件帧；动画仅作表现，与 M4"事件帧为权威"的区别原因：净化波无连段时序约束）。
- IF 净化剂低于 `HeavyCost`，THE SYSTEM SHALL 不进入蓄力状态并可提示（事件占位，UI 本体 M7）。

## R4 净化剂资源（PurifyTank）

> **美术动作**：无（HUD 条为 M7） | **代码落点**：`Assets/Scripts/Eco/PurifyTank.cs`、`Assets/Scripts/Eco/Events/PurifyTankChangedEvent.cs`

- THE SYSTEM SHALL 玩家挂载净化剂资源组件：当前值 / 容量（`EcoConfig.TankCapacity`），初始值取 `EcoConfig.InitialTank`。
- WHEN 净化剂发生增减，THE SYSTEM SHALL 经事件中心广播"净化剂变化"事件（携带当前值与容量，供 M7 HUD 订阅），并将值夹取在 0~容量之间。
- WHEN 拾取物与玩家碰撞，THE SYSTEM SHALL 自动回复净化剂（无需按键，数值见 R9）并回收拾取物。
- IF 净化剂为 0，THE SYSTEM SHALL 喷洒与蓄力均不可用（R2/R3 的门条件）。

## R5 垃圾堆与污染源（GarbagePile）

> **美术动作**：真素材（`garbage_pile.png`）；净化中颜色插值，净化完成换 `garbage_pile_clean.png`（绿植） | **代码落点**：`Assets/Scripts/Eco/GarbagePile.cs`

- THE SYSTEM SHALL 提供垃圾堆组件实现 `IPurifiable`：配置净化阈值 `PurifyRequired` 与净化值贡献 `PurityValue`。
- WHILE 垃圾堆被喷洒中（净化进度增长），THE SYSTEM SHALL 其 Sprite 颜色由污染色向净化色按进度插值（给玩家即时反馈）。
- WHEN 垃圾堆净化完成，THE SYSTEM SHALL 播放 `fx_purify`（池化）、广播净化完成事件（R1）、并将 Sprite 换为净化后绿植贴图（`_cleanSprite`；未配置时回退为净化色），此后不再参与任何净化结算。
- WHEN 挡路型垃圾堆（`BlocksPath = true`）净化完成，THE SYSTEM SHALL 关闭其阻挡 Collider（通路打开）并使 Sprite 变淡/隐藏（视觉上"清掉"）。
- THE SYSTEM SHALL 挡路型垃圾堆的 `PurifyRequired` 显著高于普通垃圾堆（数值设计：普通垃圾 2 秒喷洒可清，挡路垃圾需蓄力净化 1~2 次，见 design §3）。
- IF 未净化的挡路垃圾堆与玩家/敌人重叠，THE SYSTEM SHALL 保持实体阻挡（非 Trigger Collider，复用现有层规则）。

## R6 净化值系统（PuritySystem）

> **美术动作**：无（进度条为 M7） | **代码落点**：`Assets/Scripts/Eco/PuritySystem.cs`、`Assets/Scripts/Eco/Events/PurityChangedEvent.cs`、`Assets/Scripts/Eco/Events/GarbagePurifiedEvent.cs`

- THE SYSTEM SHALL 每个关卡场景存在一个净化值系统（场景级单例组件）：统计全部已注册 `IPurifiable` 的 `PurityValue` 总量作为满分。
- WHEN 场景初始化（Start），THE SYSTEM SHALL 广播一次"净化值变化"事件（初始 0%，供 HUD/氛围系统获得基线）。
- WHEN 任一 `IPurifiable` 净化完成，THE SYSTEM SHALL 累加已净化值并广播"净化值变化"事件（携带当前值、总量、百分比）。
- WHEN 净化值百分比达到 100%，THE SYSTEM SHALL 广播"关卡净化完成"事件一次（one-shot，供 M6 关卡流程判定过关条件）。
- IF 场景中不存在任何 `IPurifiable`，THE SYSTEM SHALL 初始化为 100% 并告警（配置错误的防御分支）。
- THE SYSTEM SHALL 净化值只增不减（本里程碑无"再污染"机制）。

## R7 污染度与场景氛围（AtmosphereController）

> **美术动作**：无（作用于 Camera 背景色） | **代码落点**：`Assets/Scripts/Eco/AtmosphereController.cs`

- THE SYSTEM SHALL 每个关卡场景存在一个氛围控制器：订阅净化值变化事件，计算污染度 = 1 − 净化值百分比。
- WHILE 污染度变化，THE SYSTEM SHALL 将主摄像机背景色在"灰紫（污染）/ 自然绿（清新）"间按污染度线性插值（`EcoConfig.PollutedColor` / `CleanColor`，插值随时间平滑过渡，不瞬跳）。
- WHEN 场景初始化，THE SYSTEM SHALL 立即应用污染度 100% 对应的背景色。
- THE SYSTEM SHALL 污染度数值随净化值事件一并可查询（供 M6 刷怪频率预留，本里程碑不做刷怪逻辑）。
- THE SYSTEM SHALL 不引入后处理栈（内置渲染管线 + Camera.backgroundColor 即可，对应 TDD §4 风险对策）。

## R8 Tower01 与污染生物的净化对接

> **美术动作**：复用 M4 `Tower01_Disable` 与敌 Hurt/Die | **代码落点**：`Assets/Scripts/Eco/TowerPurifiable.cs`（适配器组件，挂 Tower01 Prefab）

- THE SYSTEM SHALL 以适配器组件让 Tower01 实现 `IPurifiable`（净化阈值与净化值贡献由 `EcoConfig` 或组件字段配置），内部调用 M4 已预留的 `Tower01Controller.RequestDisable()`，不改 M4 任何源码。
- WHEN Tower01 净化完成（适配器收到足量净化量），THE SYSTEM SHALL 触发 `RequestDisable()`（幂等由 M4 保证）、贡献净化值、订阅方收到 M4 既有 `TowerDisableEvent`。
- WHILE 喷洒命中污染生物（Enemy01/02/03），THE SYSTEM SHALL 仅走 `IHittable`/`DamageInfo` 伤害通道（消耗加倍由 R2 承担），不对其调用 `IPurifiable`。
- WHEN 污染生物被喷壶伤害致死，THE SYSTEM SHALL 其死亡照常走 M4 `EnemyDeathEvent`（掉落由 R9 订阅该事件生成），净化流程无额外分支。

## R9 掉落与拾取（DropTable / PurifyPickup）

> **美术动作**：占位（WhiteSquare 小色块；美术后补仅换 Sprite） | **代码落点**：`Assets/Scripts/Eco/DropTable.cs`、`Assets/Scripts/Eco/DropSpawner.cs`、`Assets/Scripts/Eco/PurifyPickup.cs`

- THE SYSTEM SHALL 提供 `DropTable`（ScriptableObject）：条目列表（拾取物类型 + 掉落数量 + 掉落概率），允许为空（不掉落）。
- WHEN 敌人死亡（M4 `EnemyDeathEvent` 携带 `DropTable` 引用），THE SYSTEM SHALL 按掉落表概率在死亡位置生成拾取物（经对象池获取，不即时实例化）。
- WHEN 拾取物与玩家碰撞（Trigger），THE SYSTEM SHALL 自动结算（净化剂回复 `EcoConfig.PickupTankAmount`）并归还对象池，广播拾取事件。
- THE SYSTEM SHALL 拾取物落地后保持静止（不掉落穿地：复用 Ground 层碰撞或静态摆放）。
- IF `EnemyDeathEvent.DropTable` 为 null，THE SYSTEM SHALL 不生成任何拾取物且不报错。

## R10 配置与玩家状态接入（EcoConfig / 动画 / 键位）

> **美术动作**：`Player_Interact` / `Player_HeavyAttack`（复用既有 22 套，不新增美术） | **代码落点**：`Assets/Scripts/Eco/EcoConfig.cs`；`Assets/Scripts/Player/PlayerController.cs`（仅注册状态两行增量）

- THE SYSTEM SHALL 提供 `EcoConfig`（ScriptableObject，`Assets/Settings/EcoConfig.asset`）承载本里程碑全部数值（字段清单见 design §3），带 `CreateDefault()` 兜底与 `CreateAssetMenu`，资产缺失走告警 + 默认值（与 PlayerConfig/EnemyConfig 同策略）。
- THE SYSTEM SHALL 玩家 FSM 注册喷洒与蓄力两个新状态（`PlayerSprayState` / `PlayerChargeState`），转移条件由 `SprayController` 的输入属性驱动（参照 M3 `AttackPressedThisFrame` 驱动 `PlayerAttackState` 的既有模式）。
- THE SYSTEM SHALL 键位：喷洒 = 按住 K；蓄力 = 长按 L 松开释放；夹钳攻击 J 与跳跃 Space 等既有键位不变。
- THE SYSTEM SHALL 玩家 Animator 的 `Player_Interact` / `Player_HeavyAttack` 状态与参数按 `PlayerAnimParams` 既有常量核对接入（不新增参数名；若既有 Controller 缺状态则补建）。
- THE SYSTEM SHALL 本里程碑零硬编码战斗/资源数值（全部读 `EcoConfig`），`Update` 内零 `Find`/`GetComponent`（缓存于 Awake）、每帧零 GC 分配为目标（复用数组的物理查询）。
