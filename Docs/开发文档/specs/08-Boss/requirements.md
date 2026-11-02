# 需求规范：08-Boss（Boss 与第三关）

> 对应 TDD.md M8 里程碑 | 格式：EARS
> 范围：`BossConfig` 配置（继承 M4 `EnemyConfig`）、Boss01 控制器与三阶段状态机（P1→P2→P3）、双攻击技能（AttackA 近战 / AttackB 弹幕）+ P3 特殊招、伤害通道（夹钳与净化喷雾，喷雾 1.5× 倍率）、Boss 血条 UI（顶部 + 阶段刻度）、Boss 死亡对接关卡完成、第三关（能源工厂）灰盒与目标链、Boss01 Animator 生成工具
> 非目标：第二关（M9）；Boss 专属 BGM 与音效铺量（M10，仅留 AudioManager 调用占位）；Boss 美术帧新制（帧已就位，只需装配）；多 Boss / Boss 连战（M10 打磨）
> 共用约定：代码落点 `Assets/Scripts/Boss/`（命名空间 `StarEco.Boss`），状态机复用 M4 `EnemyStateMachine`/`EnemyStateBase`；UI 沿用 M7 程序化 uGUI 与 `UIUtility`；事件沿用 M1 `EventCenter` struct 零 GC；Boss01 帧目录 `Assets/Art/Art/Boss/Boss01/Frames/Boss01_*`

## R1 Boss 配置

> **美术动作**：不涉及 | **代码落点**：`Assets/Scripts/Boss/BossConfig.cs`、`Assets/Settings/Boss/Boss01.asset`

- THE SYSTEM SHALL 提供 `BossConfig` 配置资产，继承 M4 `EnemyConfig`（复用 HP/移速/攻击间隔/感知等字段），额外提供：阶段阈值（`Phase2HpPercent=60`、`Phase3HpPercent=30`）、双技能参数（`AttackARange`/`AttackAInterval`/`AttackBDamage`/`AttackBInterval`/`ProjectileCount`）、特殊招参数（`SpecialInterval`/`SpecialProjectileCount`）、净化伤害倍率（`PurifyDamageMultiplier=1.5`）、狂暴移速倍率（`EnrageSpeedMultiplier=1.3`）。
- THE SYSTEM SHALL `Boss01.asset` 初始值：HP 30、阶段阈值 60%/30%、AttackA 近战范围 2.2u 间隔 2.0s、AttackB 弹幕 3 发 间隔 3.0s、P3 特殊招 5 发 间隔 6.0s、喷雾倍率 1.5、狂暴移速 ×1.3（GDD §八 数值表初值 30 HP / 60%/30%）。
- WHEN 配置缺失，THE SYSTEM SHALL 回落到 `BossConfig.CreateDefault()` 并记日志，不抛异常。

## R2 Boss 阶段状态机

> **美术动作**：`Boss01_Idle/Move/Hurt/Die/Enrage` | **代码落点**：`Assets/Scripts/Boss/BossController.cs`、`Assets/Scripts/Boss/BossPhase.cs`、`Assets/Scripts/Boss/BossStates.cs`

- THE SYSTEM SHALL 提供 `BossController`，继承 M4 `EnemyControllerBase`（复用 HP/受击硬直/死亡广播/感知/击退），配置槽位接受 `BossConfig`（因配置继承，字段兼容）。
- THE SYSTEM SHALL Boss 持有三阶段枚举 `BossPhase { Phase1, Phase2, Phase3 }`，初始为 `Phase1`；阶段只升不降。
- WHEN Boss 当前 HP 百分比首次降到 `Phase2HpPercent`（60%）以下，THE SYSTEM SHALL 切入 `Phase2`：播放 `Boss01_Enrage` 动画、进入无敌短暂过场（0.8s，期间不响应伤害）、广播 `BossPhaseChangedEvent`（携带阶段号）。
- WHEN HP 百分比首次降到 `Phase3HpPercent`（30%）以下，THE SYSTEM SHALL 用同样规则切入 `Phase3`。
- WHILE 处于 `Phase2`/`Phase3`，THE SYSTEM SHALL 提高移速（`EnrageSpeedMultiplier`）并缩短攻击间隔（P2 ×0.8、P3 ×0.7）。
- WHEN 阶段切换过场结束，THE SYSTEM SHALL 回到可战斗状态（清无敌、恢复状态机常规流转）。
- THE SYSTEM SHALL 阶段切换为一次性：同一阈值不重复触发过场（`_triggeredPhase2/3` 守卫）。

## R3 双攻击技能与特殊招

> **美术动作**：`Boss01_AttackA`（近战横扫）、`Boss01_AttackB`（远程弹幕）、`Boss01_Special`（P3 大招） | **代码落点**：`Assets/Scripts/Boss/BossStates.cs`、`Assets/Scripts/Boss/BossAttackHitbox.cs`

- WHILE 目标进入 `AttackARange` 且 AttackA 冷却就绪，THE SYSTEM SHALL 播放 `Boss01_AttackA`，并在动画事件帧开窗近战判定（沿用 M4"事件帧为权威"的做法）：命中玩家造成 `AttackDamage` 并带击退。
- WHILE 处于 P1 且 AttackB 冷却就绪，THE SYSTEM SHALL 在距离目标较远（超出 AttackA 范围）时播放 `Boss01_AttackB` 并发射弹幕（复用 M4 `EnemyProjectile` 与对象池），弹数 = `ProjectileCount`（P1 默认 3）。
- WHILE 处于 P2，THE SYSTEM SHALL 同时启用 AttackA 与 AttackB（P2 起弹幕解锁，弹数 +1）。
- WHILE 处于 P3 且 Special 冷却就绪，THE SYSTEM SHALL 播放 `Boss01_Special` 并发射环绕弹幕（`SpecialProjectileCount`，默认 5），Special 期间不转向。
- WHEN 任一技能动画结束，THE SYSTEM SHALL 设置对应冷却并回到追击/待机（冷却 = `AttackInterval × 阶段系数`）。
- THE SYSTEM SHALL 所有弹幕经 `PoolManager` 获取并 `MarkPooled()`，超出 `ProjectileMaxRange` 自动归还（沿用 M4 做法，零额外 GC）。

## R4 伤害通道（用户 2026-10-18 确认：两种都吃，喷雾加倍）

> **美术动作**：`Boss01_Hurt` | **代码落点**：`Assets/Scripts/Boss/BossController.cs` + M3 `DamageInfo` 最小增量

- THE SYSTEM SHALL Boss 同时接受 M3 夹钳攻击与 M5 净化喷雾造成的 `DamageInfo`（两者都走既有 `IHittable` 流水线）。
- WHEN Boss 收到标记为净化的伤害（`DamageInfo.IsPurify == true`），THE SYSTEM SHALL 将伤害乘以 `PurifyDamageMultiplier`（1.5）后向下取整再扣血，并记日志（调试可见）。
- THE SYSTEM SHALL 喷雾倍率只对 Boss 生效，不影响 M4 小怪（普通敌人 `TakeDamage` 逻辑不变）。
- WHILE Boss 处于阶段切换过场（`IsPhaseTransition`），THE SYSTEM SHALL 忽略所有伤害（不扣血、不进入受击状态）。
- WHEN Boss HP 归零，THE SYSTEM SHALL 进入死亡：播放 `Boss01_Die`、广播 M4 `EnemyDeathEvent`（复用既有事件，让 M6 目标链 `DefeatEnemies` 计数）、并额外广播 `BossDefeatedEvent`（携带关卡序号与耗时，供 UI/结算）。

## R5 Boss 血条 UI

> **美术动作**：不涉及 | **代码落点**：`Assets/Scripts/Boss/BossHealthBar.cs`

- THE SYSTEM SHALL 提供屏幕上方 Boss 血条：Boss 名称、当前/最大 HP、血条填充（`Image.Filled`）。
- THE SYSTEM SHALL 在血条上标出两个阶段分隔刻度（60% / 30% 处画细竖线），使玩家可见阶段阈值。
- WHEN Boss 受击，THE SYSTEM SHALL 平滑更新血条（向目标值插值，不瞬移）。
- WHEN 阶段切换，THE SYSTEM SHALL 短暂显示"狂暴阶段"/"最终阶段"提示文字（1.5s 后淡出）。
- WHEN Boss 死亡或场景内无 Boss，THE SYSTEM SHALL 隐藏血条（不残留）。
- THE SYSTEM SHALL 血条为程序化 uGUI（复用 M7 `UIUtility`），不阻断玩家输入。

## R6 第三关目标链与结算对接

> **美术动作**：不涉及 | **代码落点**：`Assets/Settings/Levels/Level03.asset`、`Assets/Scripts/Level/LevelObjectiveTracker.cs`（仅配置数据，不改逻辑）

- THE SYSTEM SHALL 第三关目标链为两段（用户 2026-10-18 确认）：① 净化 3 处工厂污染源（PurifyTargets 3）→ ② 击败 Boss01（DefeatEnemies 1）。
- WHEN Boss 死亡事件到达，THE SYSTEM SHALL 由既有 `LevelObjectiveTracker` 的 `DefeatEnemies` 计数推进（M6 已实现该分支，M8 零逻辑改动，仅配置）。
- WHEN 目标链全部完成，THE SYSTEM SHALL 沿用 M6 流程进入 `Completed` 并弹出 M7 结算面板（S/A/B/C + 星级）。
- WHEN Boss 未被击败但净化目标已完成，THE SYSTEM SHALL 不判通关（两段都要满足）。

## R7 第三关场景（能源工厂）

> **美术动作**：灰盒（WhiteSquare 色块，美术后补） | **代码落点**：`Assets/Scenes/Level03.unity`、`Assets/Scripts/Editor/BossSetup.cs`

- THE SYSTEM SHALL 提供一键搭建工具（菜单 `StarEco/Setup/Level03`）：生成 `Level03.asset`、`Level03.unity` 灰盒（地面 + 两侧平台 + 玩家 + 3 处污染源 + Boss01 + 刷怪锚点 + 检查点 + 各系统组件 + HUD/暂停/结算 UI）、并把场景加入 Build Settings。
- THE SYSTEM SHALL 第三关地面为单层长平台（Boss 战需要横向空间，宽 48u），两端各设 1 个检查点。
- WHEN 场景内 Boss01 存在，THE SYSTEM SHALL 挂 `BossController` + `EnemyAnimatorDriver` + Boss 血条。
- THE SYSTEM SHALL 第三关复用 M6 的 `LevelManager`/`LevelSpawner`/`LevelObjectiveTracker` 与 M7 的 UI 面板，不新增平行系统。

## R8 Boss01 动画资源装配

> **美术动作**：8 套帧已就位 | **代码落点**：`Assets/Scripts/Editor/BossAnimationSetup.cs`

- THE SYSTEM SHALL 提供一键工具（菜单 `StarEco/Setup/Boss01Animation`）：按帧目录生成 `Assets/Animations/Boss01/Boss01.controller` 与 8 个 clip（Idle/Move/AttackA/AttackB/Hurt/Die/Enrage/Special），参数沿用 M4 `EnemyAnimParams` 并新增 `AttackB`/`Special`/`Enrage` 三个 Trigger。
- THE SYSTEM SHALL clip 命名遵循社区规范 PascalCase 无前缀（与 M2/M4 一致），帧率取 `BossConfig.AnimFrameRate`（默认 12）。
- WHEN 工具重复运行，THE SYSTEM SHALL 幂等覆盖同名 clip，不产生重复资产。

## R9 编译、验证与文档

- Unity 编译零错误（`Assets/Scripts/Boss/` 全部 .cs + Editor 两个工具 + M3/M4/M5 最小增量）。
- 运行验证：Boss 三阶段按 HP 阈值切换且过场无敌；AttackA/AttackB/Special 均能命中玩家；喷雾伤害倍率生效（日志可见 1.5×）；血条与阶段刻度显示正确；Boss 死亡 → 目标链推进 → 结算面板；第三关可从关卡选择进入（解锁后）。
- `tasks.md` 状态行更新 + 需求对齐报告（AGENTS.md §1.3）。
- git commit：`M8: boss + level03 — boss phase FSM/dual attacks/health bar/level03`。
