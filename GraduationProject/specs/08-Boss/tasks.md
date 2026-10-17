# 任务清单：08-Boss

> 对应 `requirements.md` R1~R9 / `design.md`。共 22 个任务，按依赖顺序排列。
> 状态标记：`[x]` 已完成且验证通过 / `[ ]` 未完成

## 一、配置与数据（R1）

- [x] T1 `Boss/BossConfig.cs`：继承 M4 `EnemyConfig`；加 `Phase2HpPercent`(60)/`Phase3HpPercent`(30)/`AttackARange`(2.2)/`AttackAInterval`(2.0)/`AttackBInterval`(3.0)/`ProjectileCount`(3)/`SpecialInterval`(6.0)/`SpecialProjectileCount`(5)/`PurifyDamageMultiplier`(1.5)/`EnrageSpeedMultiplier`(1.3) + `CreateDefault()`。验收：可 Create 资产，缺配置回落默认值不报错。
- [x] T2 `Assets/Settings/Boss/Boss01.asset`：HP 30、阈值 60/30、上述各参数初值（GDD §八 数值表）。

## 二、Boss 控制器与阶段机（R2）

- [x] T3 `Boss/BossPhase.cs`：`BossPhase` 枚举（Phase1/2/3）+ `IsFinal` 辅助。
- [x] T4 `Boss/BossController.cs`：继承 `EnemyControllerBase`；`CurrentPhase` 属性、阶段阈值判定（一次性守卫）、过场无敌 `IsPhaseTransition`、移速/攻击间隔按阶段缩放、`EnemyDeathEvent` + `BossDefeatedEvent` 广播。验收：HP 降到 60%/30% 各触发一次过场且不重复。
- [x] T5 `Boss/Events/BossPhaseChangedEvent.cs`、`Boss/Events/BossDefeatedEvent.cs`：struct 零 GC。
- [x] T6 `Boss/BossStates.cs`：`BossIdleState`/`BossChaseState`/`BossAttackAState`/`BossAttackBState`/`BossSpecialState`/`BossEnrageState`，派生 `EnemyStateBase`；P2 解锁弹幕、P3 解锁 Special。验收：三阶段各技能均能触发。

## 三、攻击与弹幕（R3）

- [x] T7 `Boss/BossAttackHitbox.cs`：近战判定盒（随朝向镜像），动画事件开窗期间生效，命中玩家 `DamageInfo`（带击退）。
- [x] T8 弹幕复用 M4 `EnemyProjectile` + `PoolManager`：AttackB 直线弹幕、`Special` 环绕扇形弹幕；超出 MaxRange 归还。验收：连续射击无泄漏（池计数稳定）。

## 四、伤害通道（R4）

- [x] T9 M3 `DamageInfo` 加 `public bool IsPurify;`（design §2.4 论证的最小增量）。
- [x] T10 M5 `SprayController` 两处 `DamageInfo` 构造设 `IsPurify = true`（喷洒 tick + 蓄力净化波）。
- [x] T11 `BossController.TakeDamage` 覆写：`IsPurify` 时伤害 ×`PurifyDamageMultiplier` 向下取整；过场无敌期间忽略伤害；其余交回基类。验收：普通小怪伤害数值不变（M4 回归）。

## 五、Boss 血条 UI（R5）

- [x] T12 `Boss/BossHealthBar.cs`：程序化 uGUI（复用 M7 `UIUtility`）；名称/HP 数值/血条 Fill + 60%/30% 两条刻度竖线 + 阶段切换提示文字（1.5s 淡出）；平滑插值；无 Boss 时隐藏；不阻断输入。验收：血条随伤害平滑变化，阶段提示出现。

## 六、第三关（R6、R7）

- [x] T13 `Assets/Settings/Levels/Level03.asset`：`LevelIndex=3`、目标链 `PurifyTargets 3` → `DefeatEnemies 1`、星级阈值 100/60/30、出生点 (-12, 2)、无刷怪条目或仅 1 条 Enemy03（可选）。
- [x] T14 `Editor/BossSetup.cs`（菜单 `StarEco/Setup/Level03`）：生成 Level03.unity 灰盒（48u 长地面 + 两侧平台 + 玩家 + 3 处污染源 + Boss01 + 2 检查点 + 各 M6/M7 系统）并加入 Build Settings（index 2）。验收：Play 即可打 Boss。
- [x] T15 M7 `LevelSelectPanel` 第三关 `BuildIndex` 改为实际索引（第 2 关保持 -1"敬请期待"）。

## 七、动画装配（R8）

- [x] T16 M4 `EnemyAnimParams` 加 `AttackB`/`Special`/`Enrage` 三个 hash。
- [x] T17 `Editor/BossAnimationSetup.cs`（菜单 `StarEco/Setup/Boss01Animation`）：按 8 个帧目录生成 clip 与 `Boss01.controller`，AttackA/B 挂事件帧回调；幂等覆盖。验收：controller 含 8 clip 且参数齐全。

## 八、编译与运行验证（R9）

- [x] T18 Unity 编译零错误：`Boss/` 全部 .cs + 两个 Editor 工具 + M3/M4/M5/M7 四处最小增量。
- [x] T19 Play 验证：Boss 血条 30/30；打到 60% 触发过场无敌（伤害被忽略）；P2 解锁弹幕；P3 Special；喷雾 ×1.5 日志；Boss 死亡 → 目标链推进 → 结算面板；关卡选择可进第三关。验收：逐条通过。
- [x] T20 回归验证：M4 小怪伤害不受影响；M6 第一关全流程仍可通关。
- [x] T21 `tasks.md` 状态行更新 + 需求对齐报告（AGENTS.md §1.3）。
- [x] T22 git commit：`M8: boss + level03 — boss phase FSM/dual attacks/health bar/level03`。

> 状态：已完成（2026-10-18，Play 实测：Boss 30/30 血条、三阶段按 60%/30% 切换、净化伤害 10→15 生效、Boss 死亡→目标链推进→结算 S/★★★、第一关回归正常）。共 22 个任务，覆盖 R1~R9 全部 EARS。
> 对 M1~M7 边界：仅 4 处最小增量（`DamageInfo.IsPurify` 1 行、`SprayController` 2 处标记、`EnemyAnimParams` 3 个 hash、`LevelSelectPanel` 1 个索引），design §3 已论证必要性。
