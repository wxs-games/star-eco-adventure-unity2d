# Unity 2D 横版动作游戏（环保主题）· 美术资源说明

画风：高清卡通描边（非马赛克像素），角色统一朝右、透明底、脚底基线对齐；特效为黑底转透明（Additive 友好）。
主角：环保小英雄（橙针织帽 / 绿马甲 / 蓝净水背包 / 银灰机械夹）。

## 目录结构（社区主流）

```
ArtAssets\
├─ 00_角色设定\        角色定妆图
├─ Art\
│  ├─ Player\  Sheets\(+raw 白底留档)  Frames\<动作>\
│  ├─ Enemies\ Enemy01\ Enemy02\ Enemy03\   各含 Sheets\(+raw) 与 Frames\
│  ├─ Towers\  Tower01\
│  ├─ Boss\    Boss01\
│  └─ FX\      Sheets\(+raw)  Frames\<特效>\
└─ _预览\       深绿底总览图（人看，不导入 Unity）
```
每个动作：`Sheets\<名>_sheet.png`（等大透明精灵表，Unity 按 cell 切）；`Frames\<名>\<名>_00.png…`（单帧）。

## 一、主角 Player（P0+P1 已完成，22 动作 / 136 帧）

**Cell 692 × 833，参考身高 755px，Pivot = Bottom Center，建议 PPU = 378（身高≈2 世界单位）。**

| Clip | 帧数 | Samples | 循环 | 事件帧 |
|---|---|---|---|---|
| Player_Idle | 10 | 12 | 是 | — |
| Player_Run | 8 | 24 | 是 | Footstep |
| Player_Jump | 3 | 24 | 否 | JumpFx |
| Player_Fall | 4 | 12 | 是 | — |
| Player_Land | 3 | 24 | 否 | LandFx |
| Player_Hurt | 3 | 15 | 否 | 受击音 + 无敌起 |
| Player_Die | 7 | 18 | 否 | 死亡音 / DieEnd |
| Player_Attack01 | 10 | 24 | 否 | 挥击音 / HitOn / HitOff / End |
| Player_Attack02 | 8 | 24 | 否 | HitOn / HitOff |
| Player_Attack03 | 9 | 24 | 否 | HitOn / HitOff（第三段附击退） |
| Player_AttackAir | 6 | 24 | 否 | HitOn / HitOff |
| Player_Interact | 6 | 18 | 否 | 夹取 / 拾取 / End |
| Player_Dash | 5 | 30 | 否 | — |
| Player_Roll | 8 | 24 | 否 | — |
| Player_HeavyAttack | 18 | 24 | 否 | HitOn / HitOff |
| Player_Crouch | 2 | 15 | 否 | — |
| Player_WallSlide | 3 | 12 | 是 | — |
| Player_WallJump | 3 | 24 | 否 | — |
| Player_LedgeGrab | 3 | 12 | 是 | — |
| Player_Turn | 1 | 24 | 否 | — |
| Player_Victory | 8 | 15 | 是 | — |
| Player_Idle02 | 8 | 10 | 是 | — |

## 二、敌人 / 塔 / BOSS（P0 已完成）

每个角色内部动作尺寸统一；**跨角色体型差用 Unity `localScale` 体现**（见末列，PPU 统一 378）。

### Enemy01 污染污泥怪（关卡1·慢速）Cell 1312×872 / 22 帧　建议 localScale ≈ 0.64（约1.3单位）
| Clip | 帧 | Samples | 循环 | 事件 |
|---|---|---|---|---|
| Enemy01_Idle | 4 | 12 | 是 | — |
| Enemy01_Move | 6 | 10 | 是 | 慢速蠕动 |
| Enemy01_Attack | 4 | 15 | 否 | HitOn / HitOff |
| Enemy01_Hurt | 2 | 15 | 否 | 帧1 |
| Enemy01_Die | 6 | 15 | 否 | DieEnd（化油泥） |

### Enemy02 变异毒植物（关卡2·较快）Cell 1397×1247 / 28 帧　localScale ≈ 0.68（约1.6单位）
| Clip | 帧 | Samples | 循环 | 事件 |
|---|---|---|---|---|
| Enemy02_Idle | 4 | 12 | 是 | — |
| Enemy02_Move | 8 | 15 | 是 | 弹跳匍匐 |
| Enemy02_Attack | 6 | 18 | 否 | HitOn（咬/喷毒）/ HitOff |
| Enemy02_Hurt | 2 | 20 | 否 | 帧1 |
| Enemy02_Die | 8 | 15 | 否 | DieEnd（枯萎化毒水） |

### Enemy03 巡逻机器人（关卡3）Cell 993×1124 / 36 帧　localScale ≈ 0.72（约1.7单位）
| Clip | 帧 | Samples | 循环 | 事件 |
|---|---|---|---|---|
| Enemy03_Idle | 4 | 12 | 是 | 扫描眼 |
| Enemy03_Chase | 8 | 18 | 是 | 滚轮高速 |
| Enemy03_Attack | 8 | 20 | 否 | HitOn（电击）/ HitOff |
| Enemy03_Hurt | 2 | 20 | 否 | 帧1 |
| Enemy03_Die | 10 | 15 | 否 | DieEnd（坍塌冒烟） |
| Enemy03_Spawn | 4 | 12 | 否 | 出场 |

### Tower01 毒气防御塔（关卡3·固定）Cell 942×979 / 20 帧　localScale ≈ 0.77（约1.8单位）
| Clip | 帧 | Samples | 循环 | 事件 |
|---|---|---|---|---|
| Tower01_Idle | 4 | 12 | 是 | 警戒灯/风扇 |
| Tower01_Aim | 4 | 18 | 否 | 瞄准 |
| Tower01_Fire | 6 | 18 | 否 | HitOn（喷毒）/ HitOff |
| Tower01_Disable | 6 | 12 | 否 | 停机（被净化变灰） |

### Boss01 工厂污染核心（关卡3 最终 BOSS）Cell 831×798 / 85 帧　localScale ≈ 2.25（约4.5单位）
| Clip | 帧 | Samples | 循环 | 事件 |
|---|---|---|---|---|
| Boss01_Idle | 6 | 10 | 是 | 烟囱冒烟/核心脉动 |
| Boss01_Move | 10 | 12 | 是 | 沉重推进 |
| Boss01_AttackA | 10 | 20 | 否 | HitOn（砸地）/ HitOff |
| Boss01_AttackB | 12 | 18 | 否 | HitOn（喷毒）/ HitOff |
| Boss01_Hurt | 3 | 15 | 否 | 帧1 |
| Boss01_Die | 24 | 15 | 否 | DieEnd（坍塌废墟） |
| Boss01_Enrage | 6 | 15 | 否 | 狂暴阶段切换 |
| Boss01_Special | 14 | 18 | 否 | P1 特殊招 |

> 已完成补充（P1）：Enemy03_Spawn、Tower01_Aim、Boss01_Special/Enrage，以及主角第二/三批动作（Attack02、Attack03、AttackAir、Dash、Roll、HeavyAttack、Crouch、WallSlide、WallJump、LedgeGrab、Turn、Victory、Idle02）均已就位。

## 三、独立特效 FX（黑底转透明，38 帧）

| 特效 | 帧 | Samples | 用途 / 挂载点 |
|---|---|---|---|
| fx_slash | 6 | 24 | 主角挥夹判定帧，叠在 Attack01/02/03 开窗帧 |
| fx_hit | 6 | 24 | 任意命中瞬间，Pivot 对齐受击点 |
| fx_purify | 8 | 20 | 净化敌人/收集道具，Interact 与敌人 Die 末段 |
| fx_dust_run | 4 | 15 | Run 脚步帧 / Jump / Land 脚下 |
| fx_gas | 6 | 15 | 毒植物、毒气塔、BOSS 喷毒判定处 |
| fx_explode | 8 | 24 | 机器人/BOSS 爆炸 |

FX 用法：材质推荐 **Additive**（剑光/火花/净化/爆炸会自然发光，黑底完全透明）；毒气/扬尘用普通 Transparent 即可。Pivot = Center。

## 四、Unity 导入设置

1. 角色 `Sheets\*.png`：Sprite Mode **Multiple** → Sprite Editor → Slice **Grid By Cell Size**，填该角色上表 Cell 尺寸；Pivot **Bottom Center**；删末尾空格。
2. Filter **Bilinear(Soft)**；Compression **BC7**（PC）/ ASTC 6×6（移动）；**关 Mipmap**；角色 Max Size **8192**（个别敌人 sheet 略宽于 4096），FX 用 4096。
3. 全角色统一 **Pixels Per Unit = 378**；体型差用各角色 `localScale`（上表系数），不要改 PPU。
4. 朝左用 `SpriteRenderer.flipX`；位移由 Rigidbody2D 驱动（关 Root Motion）。
5. Idle/Run/Move/Chase/Fall 等循环动作勾 Loop Time，其余不循环。
6. Run 0.67s × moveSpeed = 单循环步长（moveSpeed=4 时约 2.67 单位），按此防打滑。
7. Animator 参数：Speed(float)、IsGrounded(bool)、VVelocity(float)、Attack/Hurt/Dash(Trigger)、IsDead(bool)；打断优先级 Die>Hurt>Attack/Interact>Dash>Jump>Run/Idle。

## 五、已知 AI 素材瑕疵（可手修，不影响使用）

- Player_Attack01 个别帧机械夹偏大（运行尺寸小，且叠 fx_slash）；Player_Die 第1帧表情笔触偏重。
- 敌人/BOSS 喷毒、电击等攻击帧的喷出物在格边缘略有截断，运行时由 fx_gas / fx_hit 覆盖。
- 移动类动作机械夹挂背包/腰侧，攻击与互动才手持（平台游戏主流做法）。

## 制作流程（已自动化，脚本在会话工作目录）

AI 网格精灵表 → 按行列切帧 → 白底连通域抠像（保留白 T 恤/眼白，去碎影）/ 黑底亮度转 alpha（FX）
→ 以典型身高中位数统一缩放、脚底基线对齐 → 重拼等大透明精灵表；坏帧用相邻帧替换。
