# 需求规范：03-Combat（战斗系统）

> 对应 TDD.md M3 里程碑 | 格式：EARS
> 版本：v2.0（2026-10-30 校正；新增 R9） | 上一版 v1.0 为 M3 开工前编写
> 范围：三段近战连击（含空中攻击）、**第三段投掷钳子**、攻击开窗与命中判定、HP/受击硬直、无敌帧、玩家与敌人的伤害接口
> 非目标：HeavyAttack 蓄力攻击、敌人 AI（M4）、净化攻击（M5，见 05-EcoSystem）、输入自定义（沿用 M2 硬编码）
>
> ⚠️ **本版推翻 v1.0 的两条结论**（实现已改，文档未跟，属漂移）：
> 1. v1.0「地面攻击保持原地、新按方向立即取消」→ 实际为 **边跑边打，方向输入不取消攻击**（2026-10-28）
> 2. v1.0「第三段命中附带击退」→ 实际为 **第三段投掷钳子，伤害全交给投掷物**（2026-10-28）

## R1 三段连击

- WHEN 玩家在地面按下攻击键（默认 J），THE SYSTEM SHALL 进入攻击状态并播放 `Attack`（三段链第一段）。
- WHEN 玩家处于第一段或第二段攻击的连段窗口（`ComboWindowSeconds`，默认 0.15s）内再次按下攻击键，THE SYSTEM SHALL 在当前段动画结束后无缝衔接 `Attack2` / `Attack3`。
- IF 连段窗口已关闭或玩家未再按攻击键，THE SYSTEM SHALL 在当前段动画播放完毕后回到 Idle（或空中状态）。
- **WHEN 玩家处于地面攻击中按下方向键（含反向），THE SYSTEM SHALL 继续攻击（不取消），并在同帧按正常移动逻辑位移——地面全速、空中按 `AirControlRatio`。**（v2.0 修改，原为"立即取消"）
- **WHEN 玩家起手时已按住某一方向，THE SYSTEM SHALL 在攻击全程保持该方向的移动输入，不做特殊处理。**（v2.0 修改，原为"避免被自身输入打断"）
- WHEN 玩家在攻击期间朝向输入反向，THE SYSTEM SHALL 忽略翻转（攻击中不可转身）。

## R2 空中攻击

- WHEN 玩家离地按下攻击键，THE SYSTEM SHALL 播放 `AttackAir`（单发，不参与三段链）。
- WHEN 空中攻击动画播放完毕且玩家仍离地，THE SYSTEM SHALL 回到 Air 状态。
- **WHEN 空中攻击期间按下方向键，THE SYSTEM SHALL 保持攻击不取消，仅按 `AirControlRatio` 施加空中位移。**（v2.0 修改，原为"立即取消"）
- IF 玩家在空中攻击动画结束前触地，THE SYSTEM SHALL 按 M2 的 Land 流程处理（触地优先）。

## R3 攻击开窗与命中判定

- THE SYSTEM SHALL 通过 Animation Event 在每段攻击的有效帧开启/关闭命中开窗，开窗期外的碰撞体不产生命中。
- WHEN 命中开窗期间检测到带 `IHittable` 的目标，THE SYSTEM SHALL 对其调用一次 `TakeDamage`（同一目标在单次开窗内不重复命中，用 `HashSet<IHittable>` 去重）。
- WHEN 攻击未命中任何目标，THE SYSTEM SHALL 正常走完动画（攻击不因落空取消）。
- **WHEN 当前段为第三段，THE SYSTEM SHALL 整段不开启近战命中窗（`CurrentSegment == 3` 时直接 return），伤害全部由投掷物结算。**
- WHEN 攻击状态因任何原因退出（受击 / 死亡 / 落地），THE SYSTEM SHALL 无条件关闭命中窗。

## R4 伤害与受击

- THE SYSTEM SHALL 玩家基础 HP = **10 半心点 = 5 颗心**（`CombatConfig.MaxHP`），受击时按 `DamageInfo.Amount` 扣减。（v2.0 校正：原写 5，实为半心点单位）
- WHEN 玩家在无敌期外受到伤害，THE SYSTEM SHALL 进入 Hurt 状态：沿 `DamageInfo.KnockbackDir` 反方向击退（`HurtKnockback` = 6），硬直锁输入 0.6s。
- WHEN Hurt 硬直结束，THE SYSTEM SHALL 回到 Idle 或 Air（按 `IsGrounded` 判定）。
- IF 玩家攻击中受到伤害，THE SYSTEM SHALL 打断攻击并进入 Hurt（受击优先级高于攻击）。

## R5 无敌帧

- WHEN 玩家受击进入 Hurt，THE SYSTEM SHALL 同时开启 1s 无敌期（`HurtInvincibleTime`），期间 `SpriteRenderer.alpha` 以 12Hz 闪烁。
- IF 玩家在无敌期内再次受到伤害，THE SYSTEM SHALL 忽略该伤害（不扣 HP、不击退、不打断动作）。
- WHEN 无敌期结束，THE SYSTEM SHALL 恢复正常受击与显示（alpha 复位为 1）。

## R6 死亡

- WHEN 玩家 HP 降至 0，THE SYSTEM SHALL 进入 Die 状态（停用输入与受击，播放 `Death`）。
- **重生态义：`LevelManager` 检测到 `IsDead` 后判本局失败，`SceneManager.LoadScene(buildIndex)` 整关重载。项目无中途检查点。**（v2.0 校正：原写"回检查点由 LevelFlow 接管"，检查点未实现）

## R7 伤害接口

- THE SYSTEM SHALL 定义统一伤害数据 `DamageInfo`（`Amount` / `KnockbackDir` / `Source`）与 `IHittable` 接口（`TakeDamage(DamageInfo)`）。
- THE SYSTEM SHALL 玩家（`PlayerCombat`）、三种敌人、Boss、可破坏道具全部实现 `IHittable`。
- WHEN 攻击命中携带 `IHittable` 的目标，THE SYSTEM SHALL 仅通过接口回调，不直接引用任何具体目标类型。

## R8 数据配置与动画对接

- THE SYSTEM SHALL 战斗参数（HP、伤害值、连段窗口、硬直时长、无敌时长、击退力、投掷全套参数）全部配置于 `CombatConfig`（ScriptableObject），代码不硬编码。
- WHEN 驱动攻击动画，THE SYSTEM SHALL 仅通过 Animator Trigger（`Attack` / `Attack2` / `Attack3` / `AttackAir`）切换，与 `PlayerAnimationSetup` 生成的控制器一致。
- WHEN 攻击开窗/结束事件触发，THE SYSTEM SHALL 由 Animation Event 调用脚本方法（事件帧与 clip 内实际帧号严格对应；重生成动画必须重打事件）。

## R9 第三段投掷钳子（⭐ v2.0 新增）

- WHEN 第三段（`Attack3`）播放到 `ThrowReleaseTime`（0.19s，24fps 下约第 5 帧），THE SYSTEM SHALL 调用 `PlayerToolThrow.TryThrow()` 发射投掷物。
- WHEN 发射投掷物，THE SYSTEM SHALL 隐藏手持工具的 `SpriteRenderer.enabled`，**不得 `SetActive(false)`**（clip 的 `m_IsActive` 曲线会覆盖）。
- WHEN 投掷物飞行，THE SYSTEM SHALL 每个 `FixedUpdate` 沿位移路径做一次 `Physics2D.CircleCastNonAlloc` 判定（半径 0.26），不使用物理碰撞。
- WHEN 投掷物命中 `IHittable` 且该目标本次飞行未被命中过，THE SYSTEM SHALL 施加 `DamageInfo{ ThrowDamage=3, 飞行方向, player }`。
- WHEN 投掷物飞满射程（5.5u）/ 撞墙 / 超时（4s）任一条件成立，THE SYSTEM SHALL 折返朝 `ToolAnchor` 归位（速度 22）。
- WHEN 投掷物归位完成，THE SYSTEM SHALL 回收进对象池并恢复手持工具显示。

## 验收方式

- 编译零报错；Play 后逐条验证：三段连击衔接、连段窗口关闭回落、空中攻击、开窗期外不命中
- **边跑边打**：攻击中持续按方向，确认人物位移且攻击不中断（v2.0 新增用例）
- **第三段投掷**：确认第三段贴脸不产生近战伤害、钳子飞出后回旋归位、回程不对同一目标二次伤害
- 用测试靶（`TestTarget`，带 `IHittable`）验证命中回调、玩家受击击退、无敌期闪烁与伤害忽略、HP 归零进 Die
- Animator 窗口目检 `Attack → Attack2 → Attack3` 链与回落
