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

### Tower01 毒气防御塔（关卡3·固定）Cell 1039×979 / 19 帧　localScale ≈ 0.77（约1.8单位）
| Clip | 帧 | 网格 | Samples | 循环 | 事件 |
|---|---|---|---|---|---|
| Tower01_Idle | 4 | 2×2 | 12 | 是 | 警戒灯/风扇 |
| Tower01_Aim | 4 | 2×2 | 12 | 否 | 帧4 锁定音（攻击前摇） |
| Tower01_Fire | 6 | 3×2 | 12 | 否 | 帧3 HitOn（喷毒）/ 帧5 HitOff |
| Tower01_Disable | 5 | 3×2 | 12 | 否 | 帧5 停机（被净化变灰） |

> Tower01 四套**统一 1039×979**（画格与 Fire 同规格，塔体在格内的像素坐标四套完全一致，切换 Clip 时零位移，故不再按底座锚点自适应宽度）；高度 979、底留白不变。
> 定妆图 = `Tower01_Fire_00`（射击第一帧）。Tower01_Idle / Aim / Disable **不走生成式管线**：塔体逐像素取自 Fire_00，仅在背景区叠加效果 —— Idle：罐顶警戒灯辉光脉动 + 风扇轮缘风影弧；Aim：炮口蓄能绿气由弱到强；Disable：逐帧去饱和变灰 + 顶部升烟。派生脚本见 `Tools/art-pipeline/make_tower_sets_from_fire.py`（三套的第 1 帧均与 Fire_00 逐像素相同）。Disable 做 **5 帧**是刻意与 `Disable.anim` 的 5 个键（00..04、末键 t=1/3 s）对齐，使**末帧即「全灰停机」终态**而不是永不播放的第 6 帧。
> 四套 Clip 同一座塔：粗短炮管一律朝左上 + 圆顶橄榄绿锈蚀塔身 + 塔身左侧大红圆灯 + 顶部金框白色压力表 + 右侧红顶黄黑斜条纹罐 + 右下黑色辐条风扇轮 + 喇叭形铆钉底座。

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
4. 背景**不要用 Sliced**：该图 `.meta` 的 border 为 0，Sliced 退化后会按 1920×1080 像素粒度切块，运行时斜铺出巨大色块。改用 `Simple + transform.scale` 拉伸。

**仍为灰盒、待补的占位符**（Level01）：无——`NPC_Ava`、两块提示牌、两个知识卡片、两个检查点、三个垃圾堆、挡路垃圾墙均已换真素材；`SpawnAnchorA/B` 是刷怪逻辑锚点，**刻意隐藏**（`SpriteRenderer.enabled = false`），仅在 Editor 中作位置句柄用。

## 六、道具 Props（M10 R2：Level01 占位符全部替换）

目录 `Assets\Art\Art\Environment\Props\`。白底生成图经 **flood-fill 容差抠白** 处理成透明底并 trim：
背景色取四边中位数，只吃**与画面边界连通**的浅色区，因此道具内部的白色高光（提示牌上的白色标志、卡片上的白色叶脉）不会被误抠。

| 素材 | 像素 | PPU | 世界尺寸 | 用法 | 语义对应 |
|---|---|---|---|---|---|
| `npc_ava.png` | 390×922 | 485 | 0.80×1.90 | 教学 NPC，落地后 scale 1.16 → **2.20 单位高**（与主角等高） | `ava_intro` |
| `sign_plastic_warning.png` | 288×1024 | 890 | 0.32×1.15 | 灰绿警示牌，白色警告三角 | `Signboard_Plastic` ← `signboard_plastic`（塑料污染警示） |
| `sign_recycle.png` | 260×926 | 805 | 0.32×1.15 | 蓝绿牌，白色回收三角 | `Signboard_Recycle` ← `signboard_recycle`（可回收物识别） |
| `knowledge_card.png` | 509×538 | 1076 | 0.47×0.50 | 琥珀晶体卡，离地 0.35 | `KnowledgeCard1/2` |
| `checkpoint.png` | 377×746 | 481 | 0.78×1.55 | 青蓝布旗检查点，触发区 1.2×1.2 | `Checkpoint1/2` |
| `garbage_pile.png` | 724×327 | 452 | 1.60×0.72 | 可清理垃圾堆（塑料瓶/纸箱/锡罐），碰撞体 1.60×0.90 | `Garbage1/2/3` |
| `garbage_wall.png` | 809×957 | 578 | 1.40×1.66 | 高瘦捆扎挡路墙（麻绳+木箱），碰撞体 1.40×2.20 | `GarbageWall1` |

### 垃圾堆/墙的贴地约定（与 LevelSetup 的坑）

`GarbagePile` / `GarbageWall` 是 **prefab**（带 `GarbagePile` 逻辑组件），不走 LevelUISetup 那套。约定：

- prefab 根节点位于物件**中心**，`BoxCollider2D.offset = 0`，`localScale = 1`；
- 场景实例的 `position.y` 必须给「**视觉**高度 ÷ 2」才能让底边贴地：垃圾堆 0.36、垃圾墙 0.83。
  （曾按旧灰盒中心值 0.9/1.4 摆放 → 悬空；又曾用碰撞体半高 0.45/1.10 摆放 → 视觉与碰撞体错位。**一律以视觉半高为准**。）
- **碰撞体尺寸刻意沿用原灰盒值**（1.60×0.90 / 1.40×2.20），不随视觉高度改，避免改变关卡可通行性与手感；`LevelSetup.Run` 里的 y 值已同步为半高约定。
- 垃圾墙第一版生成成矮胖「一堆」，拉伸到 2.2 高会明显变形，已重新生成高瘦构图（宽高比 ≈ 1:1.17），与三个矮胖垃圾堆形成层次。

- **PPU 按「目标世界高度」反算**（`PPU = 像素高 / 目标单位高`），避免落地后靠 scale 硬凑把描边拉糊。
- 提示牌三张共用同一根木柱与边框锈斑（ImageGen 以 `sign_recycle` 为 image1 + `input_fidelity: high` 续生成），保证系列感。
- **`Signboard_Plastic` 配的是「警示」而非「堆肥」标**：查 `dialogues.json`，该牌文本讲塑料污染与「勿焚烧」，堆肥语义不符，故重做为警告三角。
- 脚本 `LevelSetup.MakeCheckpoint` / `LevelUISetup.CreateSignboard/CreateKnowledgeCard/CreateTriggerVisual` 均改为「优先真素材、缺失才退灰盒」，可重跑而不会冲掉美术。

**踩坑：pivot 在本工程 Unity 构建中改不动**

`TextureImporter` 反射确认**不存在** `spriteAlignment` / `spriteMeshType`（只有无用的 `spritePivot`），写 `.meta` 的 `alignment: 1` 也不生效——`sprite.bounds.center.y` 始终是 `-高/2`，即**实际按 Center pivot 处理**。
因此所有道具统一用 Center 语义摆放：`position.y = 地面顶面(0) + 高度/2`（悬浮物再加 yOffset），`localScale` 归一为 1。
FullRect / pivot 一律在 `.meta` 文本层设置，不走 API。

## 七、UI 素材（M11：主菜单）

目录 `Assets/Resources/UI/Menu/`（与 `UI/HUD/` 同级）。主菜单原先全靠运行时代码画纯色块，本次整体换成 AI 生成贴图。

| 素材 | 源尺寸 → 导入尺寸 | 用途 | 用法 |
|---|---|---|---|
| `menu_bg.png` | 2560×1440 → 1920×1080 | 全屏背景插画（左净水雨林 / 右污染工厂，中段刻意压暗留白） | 背景 Image，Type=**Simple**，Stretch |
| `menu_title_plate.png` | 2172×792 → 2048×747 | 标题牌匾，垫在「星际环保」之下 | RectTransform **700×255**，Type=Simple |
| `menu_button.png` | 2114×740 → 2048×717 | 按钮玻璃绿底板，6 个按钮共用一张 | **340×74**，Type=Simple |

- 加载入口：`StarEco.UI.MenuArt`（`Resources.Load<Sprite>("UI/Menu/…")`，缓存；取不到时自动回退原纯色表现）。
- 按钮皮肤会把 `Image.color` 置白并重设颜色渐变（常态白 / 悬停 ×1.25 / 按下 ×0.72 / 禁用灰）。

**生成与后处理约定**

- 含文字的贴图一律不生成文字（AI 中文字形不可靠）。中文标题仍由 `Text` 组件渲染，牌匾只做边框装饰。
- 抠底走**洋红键**而非白底：提示词要求主体置于纯洋红 `#FF00FF` 背景；脚本 `_tmp/menu/process.py` 以 `min(r-g, b-g)` 计算 alpha，并把与主体**不连通**的微弱光晕整体清零（首版未清，缩到 340px 后整屏浮一层绿雾），最后 despill 把残留洋红压回绿色域。
- `.meta` 以现有 HUD 帧贴图为模板生成（`spriteBorder` 归零，Simple 不需要九宫格 border）。
- 导入时 `maxTextureSize=2048` 会把 2114 / 2172 宽的图等比缩到 2048，观感无影响。

**踩坑：`Text` 大字号必须给够行框高度**

`UIUtility.CreateText` 的 `verticalOverflow` 固定为 `Truncate`。96 号字整行高约 **136px**，标题 RectTransform 只给 130px 时整行被丢弃（`cachedTextGenerator.lineCount == 0`），标题完全不显示且**不报任何错**；行框已给到 170px。

另：运行时 `AddComponent<Button>()` **不会**自动填 `targetGraphic`（`Selectable.Reset()` 仅在编辑器执行），需手动赋值，否则悬停/按下的颜色渐变不生效、换皮也会因 `targetGraphic` 为空而静默跳过。

