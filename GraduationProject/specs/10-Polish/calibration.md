# 数值校准记录：10-Polish（M10 R2）

> 2026-10-18。审查对象：PlayerConfig / CombatConfig / EcoConfig / Enemy01~03Config / Tower01Config / Boss01Config 全部 public 字段（值取自资产实际值，非代码默认值）。
> 校准策略（design.md D4）：只修"明显破坏体验"项；未做真实玩家测试前不大改。每项给出结论与理由。

## 1. 调整项（2 处）

| 资产 | 字段 | 调整前 | 调整后 | 理由 |
|---|---|---|---|---|
| Enemy01Config | AttackRange | 10 | **3.5** | 近战 hitbox 实际触达仅 3u（BoxCollider 5×7 + localOffset 0.5）。原值下敌人在 10u 处即进入近战状态并停步，Tick 逻辑"只要在范围内就继续攻击"，变成 3~10u 间反复空挥的木桩炮台，威胁归零。Boss01 的正确范式是 range≈hitbox 触达+0.5 缓冲（2.2≈2.1+0.1），依此修正 |
| Enemy03Config | AttackRange | 9 | **3.0** | 同上：hitbox 触达 2.5u（4×8 + offset 0.5）。Enemy03 是高速追击者（移速 6=玩家），修正后"贴身快攻"设计才成立 |

> 验证方式：静态核对 hitbox 几何 + 状态机 Tick 逻辑推导 + Boss01/Tower01 同构范式对照；Play 回归见 tasks.md T15（Level01~03 加载与基础链路无回归）。

## 2. 保持不动项（含理由）

### PlayerConfig（全部保持）
| 字段 | 值 | 结论 |
|---|---|---|
| MoveSpeed / Acceleration | 6 / 60 | 跳跃高度=12²/(2×3×9.81)≈2.45u，与 Level02 平台/河道几何匹配（M2 已验证手感） |
| JumpVelocity / JumpCutMultiplier | 12 / 0.5 | 可变跳高，社区标配手感参数 |
| GravityScale / FallGravityMultiplier | 3 / 1.6 | 下落更快更利落，M2 调校值 |
| CoyoteTime / JumpBufferTime | 0.1 / 0.1 | 宽容窗口标准值 |
| HurtInvincibleTime / HurtKnockback | 1 / 6 | M3 验收值 |

### CombatConfig（全部保持）
MaxHP=5 / AttackDamage=1 / ComboWindow=0.15s / ThirdHitKnockback=6 / Stun=0.6s——三段连击 3 伤害 vs 小怪 3~5 HP，节奏正确；均为 M3 验收值。

### EcoConfig（全部保持）
喷雾 DPS=2、tick=0.25s（普通垃圾 4 点=2 秒，GDD"长按 2 秒"）、蓄力波 40 净化/3 伤害、 Tank 100/初始 50/拾取 20、垃圾 5%/墙 10%/塔 10%——与 GDD §8 数值表逐项一致，M5 验收值。

### 敌人（除 AttackRange 外全部保持）
| 资产 | 字段 | 值 | 理由 |
|---|---|---|---|
| Enemy01 | MoveSpeed=3 / HP=3 | 保持 | 慢速近战炮灰，玩家（6）可拉开；HP3=两套普攻 |
| Enemy01 | Perception=14 / SameGroundToleranceY=2 | 保持 | 同层大范围感知=持续交战压力，横版紧凑关卡可接受；M4~M6 已验证刷怪/脱战行为 |
| Enemy02 | HP=4 / 移速=2 / 弹速=8 / 射程=16 / AttackRange=14 | 保持 | 远程单位 range≈射程自洽（M9 新档，结构与 Tower01 同构） |
| Enemy03 | 移速=6 / HP=5 / SpawnSeconds=0.8 | 保持 | 高速追击型，难度顶点之一；M8 第三关已通关验证可达性 |
| Tower01 | HP=0 / range=12 / 射程=14 / interval=1.5 | 保持 | 固定炮台，range<射程自洽 |
| Boss01 | HP=30 / 阶段 60%/30% / 近战 range=2.2 / 弹幕 3 发 24° / 狂暴 1.3× | 保持 | 三阶段结构完整，近战 range 与 hitbox 自洽；M8 已通关 |

## 3. 结论

- 调整 2 项（Enemy01/03 近战 AttackRange），均为"范围触发器远大于实际命中几何"导致的威胁失效；
- 其余 60+ 字段全部保持：与 GDD 数值表一致或已经 M2~M8 实测验证，无证据表明破坏体验；
- 后续若做玩家实测，优先复测：Enemy01/03 贴身攻击命中率、Enemy03 追击压迫感、Boss 一阶段近战频率。
