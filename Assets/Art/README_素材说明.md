# Unity 2D 横版动作游戏（环保主题）· 美术资源说明

画风：高清卡通描边（非马赛克像素），角色统一朝右、透明底、脚底基线对齐；特效为黑底转透明（Additive 友好）。
主角：环保小英雄（橙针织帽 / 绿马甲 / 蓝净水背包 / 银灰机械夹）。

## 目录结构（社区主流）

```
ArtAssets\
├─ 00_角色设定\        角色定妆图
├─ Art\
│  ├─ Player\  Sheets\(+raw 透明底留档)  Frames\<动作>\
│  ├─ Enemies\ Enemy01\ Enemy02\ Enemy03\   各含 Sheets\(+raw) 与 Frames\
│  ├─ Towers\  Tower01\
│  ├─ Boss\    Boss01\
│  └─ FX\      Sheets\(+raw)  Frames\<特效>\
└─ _预览\       深绿底总览图（人看，不导入 Unity）

批量生成/补齐资源时：先读 `Tools/art-pipeline/美术资源生成流程.md`（操作手册，与管线脚本同目录，含流程/脚本/复用指令），本文件只作风格基准。
```
每个动作：`Sheets\<名>_sheet.png`（等大透明精灵表，Unity 按 cell 切）；`Frames\<名>\<名>_00.png…`（单帧）。

## 一、主角 Player（P0+P1+P2 全部完成，22 动作 / 136 帧）

**Cell 基准 692 × 833：高度固定 833，宽度以条内最宽帧为准（不足 692 取 692，挥砍类横向大动作自动加宽，见各条目实际格宽）；基准身高取条内最高帧 760px，Pivot = Bottom Center，建议 PPU = 378（身高≈2 世界单位）。**

> 宽、高都以条内**最大帧**为基准，保证任何一帧都不会被画格裁掉；缩放按最高帧定，画格宽按最宽帧定。

| Clip | 帧数 | 网格 | Samples | 循环 | 时长 | 事件帧 |
|---|---|---|---|---|---|---|
| Player_Idle | 10 | 5×2 | 12 | 是 | 0.83s | — |
| Player_Run | 8 | 4×2 | 24 | 是 | 0.33s | 帧1 Footstep |
| Player_Jump | 3 | 3×1 | 24 | 否 | 0.13s | 帧1 JumpFx |
| Player_Fall | 4 | 4×1 | 12 | 是 | 0.33s | — |
| Player_Land | 3 | 3×1 | 24 | 否 | 0.13s | 帧1 LandFx |
| Player_Attack01 | 10 | 5×2 | 24 | 否 | 0.42s | 帧1 挥击音；帧4 HitOn；帧6 HitOff；帧10 End |
| Player_Interact | 6 | 3×2 | 18 | 否 | 0.33s | 帧3 夹取；帧5 拾取；帧6 End |
| Player_Hurt | 3 | 3×1 | 15 | 否 | 0.20s | 帧1 受击音+无敌起 |
| Player_Die | 7 | 7×1 | 18 | 否 | 0.58s | 帧1 死亡音；帧7 DieEnd |

### P1 扩展动作（13 条已补齐，Cell 高同 833、宽按条内最宽帧自适应，多数仍为 692）

| Clip | 帧数 | 网格 | Samples | 循环 | 事件帧 | 备注 |
|---|---|---|---|---|---|---|
| Player_Dash | 5 | 5×1 | 30 | 否 | 帧1 DashFx；帧1-5 无敌 | 闪避位移 |
| Player_Roll | 8 | 4×2 | 24 | 否 | 帧2-6 无敌窗口 | 身高≈430px 团身姿态 |
| Player_Attack02 | 8 | 4×2 | 24 | 否 | 帧3 HitOn；帧7 HitOff | 连击二段 |
| Player_Attack03 | 9 | 3×3 | 24 | 否 | 帧4-5 HitOn | 连击收尾 |
| Player_AttackAir | 6 | 3×2 | 24 | 否 | 帧3 HitOn；帧5 HitOff | 空中挥击 |
| Player_Victory | 8 | 4×2 | 15 | 是 | 帧1 胜利音（可选） | 过关结算 |
| Player_Turn | 1 | 1×1 | 24 | 否 | 无 | 转向过渡（可裁剪） |
| Player_HeavyAttack | 18 | 6×3 | 24 | 否 | 帧1-8 蓄力；帧10-12 HitOn | 重击 |
| Player_WallSlide | 3 | 3×1 | 12 | 是 | 无 | 贴墙下滑 |
| Player_WallJump | 3 | 3×1 | 24 | 否 | 帧1 JumpFx | 墙跳 |
| Player_LedgeGrab | 3 | 3×1 | 12 | 是 | 末帧 Hold | 抓边悬挂 |
| Player_Crouch | 2 | 2×1 | 15 | 否 | 帧2 Hold | 下蹲（身高≈500px） |
| Player_Idle02 | 8 | 4×2 | 10 | 是 | 帧1 打哈欠音（可选） | 长待机 |

## 二、敌人 / 塔 / BOSS（P0 已完成）

每个角色内部动作尺寸统一；**跨角色体型差用 Unity `localScale` 体现**（见末列，PPU 统一 378）。

### Enemy01 污染污泥怪（关卡1·慢速）Cell 1312×872 / 22 帧　建议 localScale ≈ 0.64（约1.3单位）
| Clip | 帧 | 网格 | Samples | 循环 | 事件 |
|---|---|---|---|---|---|
| Enemy01_Idle | 4 | 2×2 | 12 | 是 | — |
| Enemy01_Move | 6 | 3×2 | 10 | 是 | 慢速蠕动 |
| Enemy01_Attack | 4 | 2×2 | 15 | 否 | 帧3 HitOn / 帧4 HitOff |
| Enemy01_Hurt | 2 | 2×1 | 15 | 否 | 帧1 |
| Enemy01_Die | 6 | 3×2 | 15 | 否 | 帧6 DieEnd（化油泥） |

### Enemy02 变异毒植物（关卡2·较快）Cell 1397×1247 / 28 帧　localScale ≈ 0.68（约1.6单位）
| Clip | 帧 | 网格 | Samples | 循环 | 事件 |
|---|---|---|---|---|---|
| Enemy02_Idle | 4 | 2×2 | 12 | 是 | — |
| Enemy02_Move | 8 | 4×2 | 15 | 是 | 弹跳匍匐 |
| Enemy02_Attack | 6 | 3×2 | 18 | 否 | 帧3 HitOn（咬/喷毒）/ 帧5 HitOff |
| Enemy02_Hurt | 2 | 2×1 | 20 | 否 | 帧1 |
| Enemy02_Die | 8 | 4×2 | 15 | 否 | 帧8 DieEnd（枯萎化毒水） |

### Enemy03 巡逻机器人（关卡3）Cell 993×1124 / 36 帧　localScale ≈ 0.72（约1.7单位）
| Clip | 帧 | 网格 | Samples | 循环 | 事件 |
|---|---|---|---|---|---|
| Enemy03_Spawn | 4 | 2×2 | 24 | 否 | 帧1 开机音（出场启动） |
| Enemy03_Idle | 4 | 2×2 | 12 | 是 | 扫描眼 |
| Enemy03_Chase | 8 | 4×2 | 18 | 是 | 滚轮高速 |
| Enemy03_Attack | 8 | 4×2 | 20 | 否 | 帧3 HitOn（电击）/ 帧5 HitOff |
| Enemy03_Hurt | 2 | 2×1 | 20 | 否 | 帧1 |
| Enemy03_Die | 10 | 5×2 | 15 | 否 | 帧10 DieEnd（坍塌冒烟） |

### Tower01 毒气防御塔（关卡3·固定）Cell 942×979 / 20 帧　localScale ≈ 0.77（约1.8单位）
| Clip | 帧 | 网格 | Samples | 循环 | 事件 |
|---|---|---|---|---|---|
| Tower01_Idle | 4 | 2×2 | 12 | 是 | 警戒灯/风扇 |
| Tower01_Aim | 4 | 2×2 | 15 | 否 | 帧4 锁定音（攻击前摇） |
| Tower01_Fire | 6 | 3×2 | 18 | 否 | 帧3 HitOn（喷毒）/ 帧5 HitOff |
| Tower01_Disable | 6 | 3×2 | 12 | 否 | 帧6 停机（被净化变灰） |

### Boss01 工厂污染核心（关卡3 最终 BOSS）Cell 831×798 / 77 帧　localScale ≈ 2.25（约4.5单位）
| Clip | 帧 | 网格 | Samples | 循环 | 事件 |
|---|---|---|---|---|---|
| Boss01_Idle | 6 | 3×2 | 10 | 是 | 烟囱冒烟/核心脉动 |
| Boss01_Move | 10 | 5×2 | 12 | 是 | 沉重推进 |
| Boss01_AttackA | 10 | 5×2 | 20 | 否 | 帧4 HitOn（砸地）/ 帧6 HitOff |
| Boss01_AttackB | 12 | 4×3 | 18 | 否 | 帧4 HitOn（喷毒）/ 帧8 HitOff |
| Boss01_Special | 14 | 5×3 | 24 | 否 | 帧6 技能生效（熔渣喷吐）/ 帧14 End |
| Boss01_Enrage | 6 | 3×2 | 20 | 否 | 帧1 狂暴音；帧6 切换强化状态 |
| Boss01_Hurt | 3 | 3×1 | 15 | 否 | 帧1 |
| Boss01_Die | 16 | 4×4 | 15 | 否 | 帧16 DieEnd（坍塌废墟） |

> 待补（P1）清单已于 2026-10-15 全部补齐：主角 13 条扩展动作（82 帧）、Enemy03_Spawn（4 帧）、Tower01_Aim（4 帧）、Boss01_Special/Enrage（20 帧），共新增 **110 帧**；总览见 `_预览\contact_P1_new_clips.png`。

## 三、独立特效 FX（黑底转透明，36 帧）

| 特效 | 帧 | 网格 | Samples | 用途 / 挂载点 |
|---|---|---|---|---|
| fx_slash | 6 | 3×2 | 24 | 主角挥夹判定帧，叠在 Attack01 帧4-5 |
| fx_hit | 6 | 3×2 | 24 | 任意命中瞬间，Pivot 对齐受击点 |
| fx_purify | 8 | 4×2 | 20 | 净化敌人/收集道具，Interact 与敌人 Die 末段 |
| fx_dust_run | 4 | 2×2 | 15 | Run 脚步帧 / Jump / Land 脚下 |
| fx_gas | 6 | 3×2 | 15 | 毒植物、毒气塔、BOSS 喷毒判定处 |
| fx_explode | 6 | 4×2 | 24 | 机器人/BOSS 爆炸（浓烟阶段已并入火光 6 帧） |

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
- Player_Run 为"手持机械爪"构图（用户确认保留）；其余移动类动作机械夹挂背包/腰侧，攻击与互动才手持。

## 制作流程（已自动化，脚本在会话工作目录）

alpha-first 精灵图集管线：入口先归一化为**透明底 RGBA master**——源图已是透明底则直通（原生 alpha 最保真），
白底/黑底则先抠图变透明（阈值抠图质量天然低于原生 alpha，故对这类来源额外做 1px 收缩去白晕）
→ 分帧（网格切格 / 连通域自动分帧，可只留最大域去悬空配件）→ 条内身高中位数统一缩放 → 脚底基线对齐（留白 7px）
→ 重拼等大透明精灵表；坏帧用相邻帧替换。全程**不再经过白底中间图**，raw 与成品均为透明底 RGBA；
透明区做颜色外溢（alpha bleeding）避免缩放渗色出晕圈。FX 另走一条：黑底亮度转 alpha（Additive 友好）。

## 五、环境 Environment（M10 R1：Level01 先行）

风格沿用上文的「高清卡通描边」，色调偏暗紫 + 病态黄绿，呼应「污染星球」设定。

| 素材 | 尺寸 | PPU | 世界尺寸 | 用法 |
|---|---|---|---|---|
| `Environment\Tiles\ground_polluted.png` | 760×190 | **190** | **4×1 单位** | 横向无缝周期，Sprite **Tiled**，WrapMode=Repeat，Mesh Type=FullRect |
| `Environment\Background\bg_polluted_planet.png` | 1920×1080 (16:9) | 100 | 16×9 单位 | 跟随相机铺满视野，sortingOrder −100 |

**拼接基准（改相机参数时必须同步重算）**

- Level01 相机 `orthographicSize = 4.5`、`position.y = 2` → 视野高 9 单位，地面顶面（世界 y=0）占屏高 (2+4.5−0)/9 = **72.2%**。
- 背景图地平线已按此对齐在图内 **72.2%**（y=780/1080），使远景工厂剪影正好坐在地面线上。
- 背景图宽高比必须 = 视野宽高比（16:9），否则会被拉伸变形。

**Unity 导入要点**

1. 地面瓦片：`Sprite Mode = Single`、`Mesh Type = **FullRect**`（Tiled 必需）、`Wrap Mode = **Repeat**`、关 Mipmap、Bilinear。
2. **`SpriteRenderer.size` 在 Tiled 模式下是「世界单位」，不是像素**（Simple 模式才是像素）。传像素值会放大 PPU 倍铺满全屏——本项目曾踩过。
3. 灰盒 Ground 换素材时注意：`MakeWorldBox` 靠 `transform.scale`（原 1000×25）把 0.04u 的 WhiteSquare 撑成 40×1，碰撞体存的是被放大的局部单位。换真素材须先算 `collider.size × scale` 得世界尺寸，再把 scale 归一、碰撞体改回世界单位。

**仍为灰盒、待补的占位符**（Level01）：`NPC_Ava`、`Signboard_Plastic/Recycle`、`KnowledgeCard1/2`、`Checkpoint1/2`、`SpawnAnchorA/B`。
