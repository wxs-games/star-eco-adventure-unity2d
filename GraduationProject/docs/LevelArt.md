# 《星际环保》关卡美术设计 · Level Art Design

> 版本：v1.0 | 最后更新：2026-10-30
> 岗位归属：**关卡美术（Level Artist）** 撰写；与关卡策划（`LevelDesign.md`）一一对应，同一关卡的两份文档必须同步改
> 上游约束：`ArtStyleGuide.md`（视觉规范）、`LevelDesign.md`（关卡坐标与流程）
> 数据来源：三关场景文件逐对象解析（`Assets/Scenes/Level0*.unity`），坐标与贴图为实测值
> 权威顺序：**场景 > 本文档 > 设计意图**

---

## 一、关卡美术与关卡策划的分工

同一关，两个人写两份文档，管的事不一样：

| | 关卡策划（`LevelDesign.md`） | 关卡美术（本文档） |
|---|---|---|
| 管什么 | 玩家在这里**做什么** | 玩家在这里**看到什么** |
| 产出 | 平台位置、跳跃间距、敌人配置、目标链 | 地面用什么贴图、树摆哪里、哪块牌子能读 |
| 判断标准 | 能不能过去、难度是否递进 | 能不能一眼看懂、画面是否成立 |
| 常见失误 | 跳不过去 | 看不出该往哪走 |

**本文档不重复 `LevelDesign.md` 的坐标表**，只讨论画面的构成。

---

## 二、三条总原则

### 2.1 一关一个色彩主张

| 关卡 | 一句话色彩主张 | 主色 | 禁止色 |
|---|---|---|---|
| L1 污染星球 | **死掉的星球**——灰紫、无生气、光照发哑 | 灰紫 #736685 | 高饱和绿、暖阳黄 |
| L2 生态森林 | **还在撑着的森林**——墨绿、湿润、有残存的生命力 | 墨绿 #598073 | 血红、工业橙 |
| L3 能源工厂 | **污染的源头**——暗紫褐、金属、无自然光 | 暗紫褐 #4C4257 | 任何自然绿（除非在净化特效里） |

这三条不是配色偏好，是**剧情因果的视觉表达**：L1 是"已经死了"，L2 是"还没死透"，L3 是"死因"。

### 2.2 视觉动线由「亮度差」引导，不靠箭头

玩家在横版游戏里永远向右走，但"该往上还是往右"必须靠画面提示。本项目的规定：

- **可通行路径（地面、平台）明度 ≥ 45%**
- **不可通行背景（远景、墙面）明度 ≤ 35%**
- 两者差值 ≥ 10%，路径会自然"浮"出来

当前问题：三关的 `Ground` 都用了同一张 `ground_polluted_soil.png`，其明度约 30%，与背景明度接近，**路径不"浮"**。这是 L2 玩家容易迷路的物理原因之一——不是设计问题，是明度问题。

### 2.3 可交互物必须有「非地形特征」

玩家必须在 0.3 秒内判断"这东西能不能碰"。可用特征只有四种：

1. **不属于地形形状**（矩形地面里冒出一根杆子）
2. **高饱和色**（拉杆是橙色，地面是灰的）
3. **有动效**（知识卡上下浮动、提示牌有微光）
4. **有交互提示**（靠近时冒出"E"气泡）

当前项目 1、2、4 有，**3 基本没有**——知识卡是静止的。这是"存在感弱"的主因。

---

## 三、Level01 · 污染星球

### 3.1 空间与构图

| 项 | 值 |
|---|---|
| 关卡范围 | X ∈ [−18.4, 18.2]，约 **37 单位** |
| 相机正交尺寸 | **4.5**（视野 9×16 单位） |
| 背景 | `bg_polluted_planet.png`（1920×1080，规格正确） |
| 地面 | `ground_polluted_soil.png`（**污染土壤，与主题匹配**） |
| 平台 | `platform_flat.png`（扁平木板） |
| 精灵总数 | 19 |

### 3.2 物件实测清单

| order | X | Y | 对象 | 贴图 | 美术评价 |
|---|---|---|---|---|---|
| −50 | 0 | 0 | Background | `bg_polluted_planet` | ✅ 规格正确 |
| −4 | −18.40 | 1.43 | Decor_Tree_L01a | `tree_forest` | ⚠️ 见 §3.4 |
| −4 | −15.60 | 1.17 | Decor_Tree_L01b | `tree_forest` | ⚠️ 同上 |
| −4 | 15.00 | 1.49 | Decor_Tree_R01a | `tree_forest` | ⚠️ 同上 |
| −4 | 18.20 | 1.23 | Decor_Tree_R01b | `tree_forest` | ⚠️ 同上 |
| 0 | 0 | −4.00 | Ground | `ground_polluted_soil` | ✅ |
| 2 | −13.00 | 0.58 | Decor_Sign_L01W | `sign_plastic_warning` | ⚠️ 见 §3.5 |
| 2 | 12.80 | 0.58 | Decor_Sign_R01R | `sign_recycle` | ⚠️ 同上 |
| 4 | −6.00 | 1.10 | NPC_Ava | `npc_ava` | ✅ 位置在关卡中段 |
| 4 | 2.00 | 0.58 | Signboard_Plastic | `sign_plastic_warning` | ⚠️ 与装饰牌同图 |
| 4 | 9.00 | 0.58 | Signboard_Recycle | `sign_recycle` | ⚠️ 同上 |
| 5 | −15.60 | 2.48 | PlatformL2 | `platform_flat` | ✅ |
| 5 | −12.80 | 1.28 | PlatformL1 | `platform_flat` | ✅（可碎裂） |
| 5 | 12.80 | 1.28 | PlatformR1 | `platform_flat` | ✅ |
| 5 | 15.60 | 2.48 | PlatformR2 | `platform_flat` | ✅ |
| 6 | −2.50 | 0.60 | KnowledgeCard1 | `knowledge_card` | ✅ |
| 6 | 7.50 | 0.60 | KnowledgeCard2 | `knowledge_card` | ✅ |
| 15 | −3.00 | 0.20 | SpawnAnchorA | — | 纯逻辑锚点，无美术 |
| 15 | 8.00 | 0.20 | SpawnAnchorB | — | 同上 |

### 3.3 区段划分（按 X）

| 区段 | X 范围 | 美术意图 | 现状 |
|---|---|---|---|
| **A 起始区** | −18.4 ~ −12 | 建立"这颗星球死了"的第一印象 | 两棵树 + 一块警示牌 + 缺口 |
| **B 教学区** | −12 ~ 2 | 跳跃教学，视线要通透 | 平台 + 艾娃 + 知识卡 |
| **C 净化学区** | 2 ~ 9 | 两个提示牌承担教育内容 | 两块牌 + 知识卡 |
| **D 收尾区** | 9 ~ 18.2 | 斜坡上行，通向出口 | 两块平台 + 两块装饰牌 + 两棵树 |

### 3.4 问题一：污染星球上长着茂盛的森林树

`Decor_Tree_L01a/b`、`Decor_Tree_R01a/b` 四棵装饰树全部使用 **`tree_forest.png`**——那是一棵枝叶繁茂的绿色阔叶树。

这颗星球的设定是"污染初现、地表失活"。画面上却站着四棵健康的树，视觉上直接否定了世界观。

**规范**：L1 的装饰树应为**枯树 / 半枯树**（无叶、扭曲、灰褐）。

> ✅ **已交付（2026-10-31，C 线）**：`Assets/Art/Art/Environment/Props/tree_dead.png`
> 580×881 @ PPU **339**、`pivot (0.5, 0.5)`、画布尺寸与 `tree_forest.png` **逐像素一致**，
> 且树干底边同样贴死画布底行 → **A 侧换图零改动**：只把 4 个 `Decor_Tree_*` 的
> `SpriteRenderer.sprite` 换成 `tree_dead`（guid `43a8d421960645bf9af0de5e89ee27e5`），
> `localScale` 0.9 / 0.95 / 1.1 / 1.15 和 `y` 坐标全部保持原值，落脚高度不变。
> 画风对齐原树：粗深色描边、中心向左受光、竖向木纹；配色偏灰褐并掺紫，贴合 L1 的灰紫主色。
> 廉价临时方案（改 `SpriteRenderer.color` 降饱和）因此**不再需要**。

> 值得注意的是：L2（生态森林）也用了同一张 `tree_forest.png`。也就是说**两关的树是完全一样的**，只是 L2 摆了 8 棵、L1 摆了 4 棵。这削弱了"两颗星球不同"的感知。

### 3.5 问题二：装饰牌和可读提示牌长得一模一样

L1 有四块"牌子"：

| 对象 | X | 类型 | 贴图 |
|---|---|---|---|
| Decor_Sign_L01W | −13.00 | **纯装饰，无脚本** | `sign_plastic_warning` |
| Decor_Sign_R01R | 12.80 | **纯装饰，无脚本** | `sign_recycle` |
| Signboard_Plastic | 2.00 | **可读**（`SignboardTrigger`） | `sign_plastic_warning` |
| Signboard_Recycle | 9.00 | **可读**（`SignboardTrigger`） | `sign_recycle` |

四块牌子用了两种贴图，两两完全相同。玩家走到 `Decor_Sign_L01W` 前按 E 什么也不会发生，走到 `Signboard_Plastic` 前按 E 会弹出 130 字的教育文本——**但这两块牌子在画面上是同一张图**。

**规范（三选一，推荐第 1 条）**：

1. **可读提示牌加「发光边框」**：可读牌额外叠一层 `AccentGreen`（#7DFC99）的细边框 Sprite，或加一个轻微呼吸动画（scale 1.0↔1.02，2 秒周期）。成本低、效果明确。
2. 装饰牌换成完全不同的贴图（如纯色地标石、路障），从根上区分。
3. 装饰牌去掉贴图，改用地形元素（碎石堆、铁桶）。

> ✅ **美术件已交付（2026-10-31，C 线，方案 1）**：
> `Assets/Art/Art/Environment/Props/sign_glow_plastic.png` 与 `sign_glow_recycle.png`
> —— 与两张原牌**同画布同 PPU**（288×1024@890 / 260×926@805）、`pivot (0.5,0.5)`、中孔全透明（透明占比 88%），
> 边框沿板面外轮廓一圈（亮芯 + 柔光晕），叠加后不遮盖牌面内容。
> **A 侧挂法**：在原牌对象下建子对象 `SignGlow`，`localPosition = (0,0,0)`、`localScale = (1,1,1)`、
> `SpriteRenderer.sprite = sign_glow_*`、`Order in Layer = 原牌 +1`（即可读牌 6 → **7**）。
> 想加呼吸效果就给该子对象挂 `scale 1.0↔1.02 / 2s` 简单脚本（属 B 车道）。
> **装饰牌不要挂** —— 区分点就在于"只有能读的牌会发光"。
> 覆盖范围：L1 `Signboard_Plastic`（塑料警示牌）、`Signboard_Recycle`（回收牌）、L2 `Signboard_Forest`。
> **L3 不适用**：L3 七块牌子全是装饰牌、没有可读牌，本方案只能"区分"，无法制造出可读牌。

> 同样的问题在 L3 更严重：L3 有 **7 块装饰牌**、**0 块可读牌**。玩家在 L3 见到的每一块牌子都不能读。如果玩家在 L1/L2 养成了"看到牌子就按 E"的习惯，L3 会让他反复失望。

### 3.6 L1 待补

| 级别 | 项 | 做法 |
|---|---|---|
| ~~P1~~ ✅ | ~~枯树贴图 1 张~~ | **已交付** `tree_dead.png`（580×881@339，与 `tree_forest` 同尺寸同 pivot），待 A 换 4 棵 `Decor_Tree_*` 的 sprite |
| ~~P1~~ ✅ | ~~可读牌与装饰牌视觉区分~~ | **美术件已交付** `sign_glow_plastic` / `sign_glow_recycle`（见 §3.5），待 A 给 3 块可读牌挂 `SignGlow` 子对象 |
| P2 | 地表杂物（碎石、废料） | **已交付 3 件**：`debris_rubble` / `debris_scrap` / `debris_grass`（见 §8.3），待 A 沿地面散布摆位 |

---

## 四、Level02 · 生态森林

### 4.1 空间与构图

| 项 | 值 |
|---|---|
| 关卡范围 | X ∈ [−27.4, 25.1]，约 **52.5 单位** |
| 相机正交尺寸 | **6.5**（视野 13×23 单位） |
| 背景 | `bg_forest.png`（**1024×576，分辨率不足**） |
| 地面 | `ground_polluted_soil.png`（⚠️ **错配**） |
| 平台 | `platform_log.png`（原木，✅ 符合森林主题） |
| 精灵总数 | 35（其中 **17 个在兜底 order 15**） |

### 4.2 三层空间

L2 是唯一有明确三层结构的关卡：

| 层 | 内容 | 位置 |
|---|---|---|
| **上层** | 左右平台群（PlatformL1/L2/R1/R2）+ 两张知识卡 | y = 2.49 ~ 4.89 |
| **中层** | 地面、河岸、NPC、提示牌、木筏 | y = 0 ~ 1.10 |
| **下层** | 河道（水面 y≈−2.5、河床 y=−5.0） | y < 0 |

这是正确的做法：垂直方向上有三个可读的"带"，玩家的空间认知不会混乱。

### 4.3 关键区段

| 区段 | X 范围 | 美术内容 | 问题 |
|---|---|---|---|
| **左岸森林** | −27.4 ~ −10 | 4 棵树、2 块平台、1 张知识卡、艾娃 | 树与艾娃同层，遮挡风险 |
| **河道** | −15 ~ 4.5 | 3 个垃圾堵塞物（`garbage_wall`）、水面、河床、3 块木筏 | ⚠️ 木筏已换真贴图（`raft_deck`），但仍比河道宽 2.25u、沉在低位不跟水位（见 §4.5） |
| **笼区** | −2 ~ 2 | 2 个动物笼 + 2 只水獭 | order 15，与玩家同层 |
| **右岸森林** | 4.5 ~ 25.1 | 4 棵树、2 块平台、2 张知识卡、提示牌 | — |

### 4.4 问题一：森林关踩着污染土壤

`GroundL` 和 `GroundR` 都用 `ground_polluted_soil.png`——那张灰褐色的污染土壤。**专用的 `ground_forest.png` 躺在素材库里没人用**。

而河床（`Riverbed`）反而用了 `ground_forest.png`。也就是说：**该用森林地面的河岸没用，不该用的河床用了**。

这是全文档最直接的一处错配，修复成本极低（改 2 个 `SpriteRenderer.sprite`）。

### 4.5 问题二：三块木筏是纯色占位块（**原描述"隐形"有误**）

> ⚠️ **v1.1 更正（2026-10-30 23:40）**：本节原写「`SpriteRenderer.m_Sprite = {fileID: 0}`，木筏不可见」——**实测不成立**。
> 木筏的 sprite 是 `Assets/Settings/WhiteSquare.png`（4×4 px @ 100 PPU），可正常序列化；
> `localScale = (112.5, 10)` 放大后正好是 4.5×0.4 世界单位，`color = (0.55, 0.42, 0.28)` 棕色。
> 相机实拍（`.workbuddy/tmp/level02_raft.png`）确认：**解谜后玩家能看到一条棕色横带**，不是隐形。

`Raft1/Raft2/Raft3` 是玩家过河的唯一通路（`WaterPuzzleController` 解谜后 `RaftPath` 组激活）。

真实问题有三条，都不是"不可见"：

| # | 问题 | 实测 | 状态 |
|---|---|---|---|
| 1 | **纯色块，无木头纹理** | 4×4 白图放大 112.5 倍，视觉上是一块平涂棕色，读不出"木筏" | ✅ **已修（2026-10-30 23:59）** |
| 2 | **比河道宽** | 3 段各 4.5u 连成 −6.75~6.75 共 **13.5u**，而河道只有 −15~4.5；右端长出 2.25u 压在岸上 | ⚠️ 仍存在 |
| 3 | **整组在低位** | `RaftPath.y = −1.60`（= `WaterPuzzleController._lowY`），地面顶面在 **0.00**；水位升到 `highY = 0.05` 时木筏**不跟着升**，仍然沉在低位 | ⚠️ 仍存在 |

**成因**：`Level02Setup.MakeRaftPlank()` 用 `MakeWorldBox(name, white, 4.5f, 0.4f, ...)` 生成，靠 `WhiteSquare` + color 染色占位。这条路本身能序列化，问题在设计而非技术。

**修法**（拆给两条车道）：

1. ~~**C（美术）**：替换 `SpriteRenderer.sprite`~~ → ✅ **已交付**：C3 产出 `Assets/Art/Art/Environment/Props/raft_deck.png`（512×74 @ PPU 113.78 = **4.50×0.65u**，透明底，独立 GUID `e2ab2e7d…`）
2. **A（场景）**：`RaftPath` 增加跟随水位的位移，或把三段缩到河道宽度并重排 x（当前 −4.5 / 0 / 4.5）——**未做**
3. **B（可选）**：`WaterPuzzleController` 在激活 `_raftPath` 时同步把它的 y 从 `lowY` 补到 `highY`——**未做**

#### ✅ 已完成部分（v1.2，2026-10-30 23:59）

| 项 | 改动前 | 改动后 |
|---|---|---|
| `SpriteRenderer.sprite` | `WhiteSquare`（4×4 纯白） | `raft_deck`（真木甲板，带纵向木条分界） |
| `SpriteRenderer.color` | `(0.55, 0.42, 0.28)` 棕色染色 | `(1, 1, 1)` 白（让贴图原色显示） |
| `localScale` | `112.5 × 10` | `1 × 1`（贴图本身已是 4.5×0.65u） |
| `BoxCollider2D.size` | `0.0392 × 0.0392`（×scale 后= 4.41×0.39 世界） | `4.41 × 0.39`（**世界尺寸刻意保持不变**） |

> **为什么 collider 要重算**：旧写法靠 `scale=112.5` 把 0.04 的 collider 放大到 4.41。换成 `scale=1` 后若不改 collider，碰撞盒会缩小 112 倍，玩家会直接穿过木筏掉进水里。这是换 sprite 时最容易漏的一步。


### 4.6 问题三：17 个物件挤在兜底层

L2 的 `Tree`（8 棵）、`Water`、`Riverbed`、`AnimalVisual`（2）、`CageVisual`（2）、`KillZone`、`SpawnAnchor`（2）全部 `order = 15`，与玩家（场景中也是 15）同层。

> **2026-10-31 复测**：上面这句已经过时——现在 L2 的 35 个 `SpriteRenderer` **全部是 `orderInLayer = 0`**
> （玩家也是 0），即从"统一 15"变成了"统一 0"，**同层问题没解决**。实测数据与完整落值建议见 **§8.4**。

后果：玩家走到树后面时，树不会遮挡玩家（同层靠 Y 排序，行为不确定）；走到笼子前面时，笼子可能盖住玩家。

**目标层**（详见 `ArtStyleGuide.md` §5.2）：树 → −4，水面/河床 → 0，笼/动物 → 5，玩家 → 10。

### 4.7 L2 待补

| 级别 | 项 | 做法 |
|---|---|---|
| ~~P1~~ ✅ | ~~木筏换真贴图~~ | **已完成**：`raft_deck.png` 已挂 Raft1/2/3，collider 同步换算（见 §4.5） |
| **P1** | 木筏收窄到河道内 + 跟随水位 | A 重排 3 段 x（当前 −4.5/0/4.5）或 B 在激活时把 y 从 `lowY` 补到 `highY` |
| **P0** | 地面换 `ground_forest.png` | 2 个 `SpriteRenderer` |
| **P0** | 17 个 order 15 归位 | 改场景排序值 |
| P1 | 背景图重出 1920×1080 | 当前 1024×576 放大约 1.88 倍 |
| P1 | 提示牌换森林主题贴图 | 当前用 `sign_recycle`（回收标志），语义不符 |
| ~~P2~~ ✅ | ~~加草丛/灌木/藤蔓~~ | **已交付**：`debris_grass`（草丛，1.15×0.80u）、`bush_shrub`（灌木，1.80×1.20u，中心 y 0.60）、`vine_hanging`（垂藤，0.55×2.00u，中心 y = 挂点 − 1.00），见 §8.3；待 A 摆进 L2 左右岸与平台边缘 |

---

## 五、Level03 · 能源工厂

### 5.1 空间与构图

| 项 | 值 |
|---|---|
| 关卡范围 | X ∈ [−20.1, 64.0]，约 **84 单位**（最长的一关） |
| 相机正交尺寸 | **6.5** |
| 背景 | `bg_factory.png`（**1024×576，分辨率不足**） |
| 地面 | `ground_polluted_soil.png`（⚠️ **错配**） |
| 平台 | `platform_flat_fire.png`（工业平台，✅） |
| 精灵总数 | 37 |

### 5.2 四段式结构

| 段 | X 范围 | 内容 | 美术意图 |
|---|---|---|---|
| **① 入口段** | −20 ~ −7 | 老机器人、地刺、两块平台、PlatformC 移动平台 | 建立"厂区"的工业感 |
| **② 二楼段** | −7 ~ 25 | 拉杆、墙、三块平台、Boss 门 | 垂直攀爬，从上往下看厂区 |
| **③ 跳跃段** | 25 ~ 39 | 碎裂平台、移动平台、地刺 | 纯机关，视觉留白 |
| **④ 解谜 + Boss** | 39 ~ 64 | 拉杆、地刺、提示牌、Boss 场地 | 收尾，必须最"空"（给 Boss 战留位置） |

### 5.3 物件实测清单（要点）

| order | X | 对象 | 贴图 | 美术评价 |
|---|---|---|---|---|
| 2 | −18.50 | Decor_Sign_03WL | `sign_plastic_warning` | 装饰牌 |
| 2 | −2.50 | Decor_Sign_03WM | `sign_plastic_warning` | 装饰牌 |
| 2 | −1.20 | Decor_Sign_03RL | `sign_recycle` | 装饰牌 |
| 2 | 12.50 | Decor_Sign_03RM | `sign_recycle` | 装饰牌 |
| 2 | 19.00 | Decor_Sign_03RR | `sign_recycle` | 装饰牌 |
| 2 | 25.00 | Jump_Sign | `sign_plastic_warning` | 装饰牌 |
| 2 | 53.50 | Puzzle_Sign | `sign_plastic_warning` | 装饰牌 |
| 3 | 0 | Visual ×2 | `wall_soil_block` | ⚠️ **土墙贴图用在工厂** |
| 6 | 0.23 | SecondFloorRightWall | `wall_soil_block` | ⚠️ 同上 |
| 8 | −1.21 | LeverSwitch_03 | `lever_switch_off` | ✅ 橙色，可交互 |
| 8 | 47.00 | Puzzle_Lever | `lever_switch_off` | ✅ |
| 4 | −20.08 | NPC_Ava_LeverHint | **`npc_guard_robot`** | ⚠️ **名字是 Ava，贴图是机器人** |
| 4 | −12.00 | NPC_Robot_FactoryIntro | `npc_guard_robot` | ✅ |
| 4 | 60.80 | Puzzle_NPC | `npc_ava` | ✅ 艾娃在 Boss 门前 |
| 6 | −12.10 | SpikeField | `spike_strip` | ✅ |
| 6 | 33.50 | Jump_Spikes | `spike_strip` | ✅ |
| 6 | 46.50 | Puzzle_Spikes | `spike_strip` | ✅ |

### 5.4 问题一：七块牌子全是装饰，一块都不能读

L3 有 7 块 `Decor_Sign_* / Jump_Sign / Puzzle_Sign`，**全部无脚本**。而 L3 的可读载体只有 3 张知识卡（2 张新增）+ 2 个 NPC 对话。

玩家在 L1 学会了"牌子能读"，在 L2 又验证了一次（1 块可读牌），到 L3 看到 7 块牌子——**全是摆设**。

而且 `Jump_Sign`（跳跃段前）和 `Puzzle_Sign`（解谜段前）的命名暗示它们本该是"提示牌"（告诉玩家"这里要跳"/"这里有谜题"），却只挂了 `SpriteRenderer`。

**这是"设计意图未实现"的典型**：名字写了 Sign（提示），实现是 Decor（装饰）。

**修法**：给 `Jump_Sign` 和 `Puzzle_Sign` 挂 `SignboardTrigger`，指向两段新的提示文本（文案已在 `NarrativeDesign.md` §9 备好）。其余 5 块保持装饰，但要换成"非牌子"贴图，避免误导。

### 5.5 问题二：土墙出现在工厂里

`Visual`（×2）、`SecondFloorRightWall` 三个对象用 `wall_soil_block.png`——一堵**泥土地块墙**。这在 L1 合理（污染星球的土坡），在"能源工厂"里则是明显的素材挪用。

**修法**：换成工业材质（金属板、锈蚀钢板）。素材库里有 `ground_industrial.png`（2048×1024，未被任何场景使用），可裁切后作为墙面。

### 5.6 问题三：艾娃的提示 NPC 用了机器人贴图

`NPC_Ava_LeverHint`（X = −20.08）的命名是"艾娃·拉杆提示"，贴图却是 `npc_guard_robot.png`（老机器人）。

从叙事上看这不一定是错的——**如果她本来就不是艾娃，而是老机器人在提示拉杆，那应该改名**。当前命名与贴图矛盾，会让后续维护者（和答辩评审）困惑。

**建议**：改名为 `NPC_Robot_LeverHint`。改名不影响任何引用（无脚本按名字查找）。

### 5.7 L3 待补

| 级别 | 项 | 做法 |
|---|---|---|
| **P0** | 地面换工业地板 | `ground_industrial` 或新画 |
| **P0** | 三面土墙换工业材质 | 裁 `ground_industrial` 或新画 |
| P1 | `Jump_Sign` / `Puzzle_Sign` 挂可读触发器 | 挂 `SignboardTrigger` + 2 段文案 |
| P1 | 背景图重出 1920×1080 | 同 L2 |
| P2 | 加管道、支架、警示带 | 目前厂区只有平台+牌子，缺"工业"的体量感 |

---

## 六、通用元素规范

### 6.1 地面

| 关 | 当前 | 应为 | 规格 |
|---|---|---|---|
| L1 | `ground_polluted_soil` ✅ | 污染土壤 | 2048×1024，无缝 |
| L2 | `ground_polluted_soil` ❌ | `ground_forest` | 2048×1024（现有 512×128，需重画） |
| L3 | `ground_polluted_soil` ❌ | `ground_industrial` | 2048×1024（现成可用） |

### 6.2 平台

| 关 | 贴图 | 说明 |
|---|---|---|
| L1 | `platform_flat` | 扁平木板 ✅ |
| L2 | `platform_log` | 原木 ✅（森林主题正确） |
| L3 | `platform_flat_fire` | 工业平台 ✅ |

平台是三关中唯一做到"每关不同"的元素，应保持。

### 6.3 装饰牌

| 项 | 规范 |
|---|---|
| 用途 | 纯视觉，制造"有人来过"的痕迹 |
| 贴图 | **不得复用可读提示牌贴图**（当前违规） |
| 密度 | 每 10~15 单位最多 1 块 |
| 位置 | y ≈ 0.58（贴地） |

### 6.4 可读提示牌

| 项 | 规范 |
|---|---|
| 组件 | `SignboardTrigger` + `_dialogueId` + `_radius`（默认 2.5） |
| 贴图 | 独立贴图，或有发光边框/呼吸动画 |
| 内容 | 单页 ≤ 80 字，超出自动分页 |
| 分布 | 每关 1~2 块，放在"刚经历完机制"的位置 |

### 6.5 关卡内 NPC

| 项 | 规范 |
|---|---|
| 组件 | `DialogueTrigger`（`_radius` 默认 2.6） |
| 贴图 | 艾娃用 `npc_ava`，工业侧用 `npc_guard_robot` |
| ⚠️ | **交互半径不得与 `RescueTarget`（r=2.2）重叠**，否则按一次 E 会同时开对话并救动物 |

---

## 七、净化前后的视觉变化（现存最弱的一环）

当前只有**一个**视觉变化：摄像机背景色从 #736685 插值到 #8FBD80。

其余全部不变：
- 地面贴图不变（污染土壤永远是污染土壤）
- 装饰树不变（永远是绿色）
- 天空不变
- 敌人不变（死掉的敌人消失）

**结果**：玩家净化了 100%，画面只"整体亮一点点"，没有"这颗星球活过来了"的实感。

### 建议的扩展（按性价比排序）

| 优先 | 做法 | 成本 | 效果 |
|---|---|---|---|
| 1 | **地面贴图双状态**：污染度 < 50% 时把 `Ground.sprite` 换成"草地版" | 低（改 1 个 sprite + 1 个事件监听） | 最强——玩家脚下变化最直观 |
| 2 | **装饰树染色**：随污染度降低，树的 `color` 从灰褐渐变为饱和绿 | 低（改 `SpriteRenderer.color`） | 中——"树活过来了" |
| 3 | 远景背景图淡出/淡入（两张背景交叉淡化） | 中（需要第二张背景图） | 中 |
| 4 | 粒子（飘落的花瓣/浮尘） | 中（需新素材） | 弱 |

> 第 1 条是**唯一能让玩家"看到"净化的手段**，且不需要新素材（`ground_forest.png` 已经在库里）。建议列为下一个 P0。

---

## 八、资产使用现状（全量）

### 8.1 环境资产清单

> 地面/水面 6 行已于 2026-10-31 **按 GUID 全库检索重核**，原来的 4 处判断是错的（见 §8.3 末尾说明）。

| 文件 | 尺寸 | 被引用 | 结论 |
|---|---|---|---|
| `bg_polluted_planet.png` | 1920×1080 | L1 | ✅ 规格正确 |
| `bg_forest.png` | 1024×576 | L2 | ⚠️ 分辨率不足 |
| `bg_factory.png` | 1024×576 | L3 | ⚠️ 分辨率不足 |
| `ground_polluted_soil.png` | 2048×1024 @128 | **0** | ❌ 闲置（原始大图；三关实际都没用它） |
| `ground_polluted.png` | 760×190 | **L1** | ✅ 在用（L1 地面确实是它） |
| `ground_forest.png` | 512×128 | **L2×3** | ⚠️ 在用，但规格过小 |
| `ground_factory.png` | 512×128 | **L3** | ⚠️ 在用，但规格过小 |
| `ground_industrial.png` | 2048×1024 @128 | **0** | ❌ 闲置（规格合格，可直接替换 L3 的小图） |
| `water_river.png` | 512×128 @32 | **0**（序列 `water_river_00~` 被 L2 `Water` 用） | ⚠️ 母图闲置，真正在用的是序列帧 |
| `garbage_wall.png` | — | L2 三个堵塞物 | ✅ |
| `platform_flat.png` | — | L1 | ✅ |
| `platform_log.png` | — | L2 | ✅ |
| `platform_flat_fire.png` | — | L3 | ✅ |
| `tree_forest.png` | — | L1×4 + L2×8 | ⚠️ 两关同图 |
| `spike_strip.png` | — | L3 三个 | ✅ |
| `lever_switch_off.png` | — | L3 两个 | ✅ |
| `wall_soil_block.png` | — | L3 三个 | ⚠️ 用错关卡 |
| `sign_plastic_warning.png` | — | L1/L3 | ⚠️ 与可读牌混用 |
| `sign_recycle.png` | — | L1/L2/L3 | ⚠️ 同上 |
| `knowledge_card.png` | — | 三关 | ✅ |
| `npc_ava.png` | — | L1/L2/L3 | ✅ |
| `npc_guard_robot.png` | — | L3×2 | ⚠️ 其中一个是"艾娃"命名 |
| `animal_otter.png` | — | L2×2 | ✅ |
| `animal_cage.png` | — | L2×2 | ✅ |

### 8.2 结论

- **闲置环境资产 3 个**：`ground_polluted_soil`（2048 大图）、`ground_industrial`（2048 大图）、`water_river`（母图，序列帧在用）。
- **规格不合格 4 个**：两张背景 1024×576、`ground_forest`/`ground_factory` 512×128。
- **错配 4 处**（L3 用 512 小图当地面、L3 土墙、L1 森林树、L2 提示牌贴图）。
- **另有 15 个精灵从未挂载**（既有道具 5 + 地面大图 2 + 本轮新增 8），尺寸合格、规则已给出，见 §8.3。

### 8.3 未挂载精灵落位表（2026-10-31 实测）

判定方式：读 `.meta` 的 `spritePixelsToUnits` 与 `spritePivot`，按「底边贴 `y=0`」反推中心 y；再按 GUID 全库检索确认 0 引用。

| 资产 | 像素 @ PPU | 世界尺寸 | 引用 | 建议中心 y | 用途 / 摆放约束 |
|---|---|---|---|---|---|
| `crate_wood.png` | 440×400 @400 | 1.10×1.00 | 0 | **0.50** | 可破坏木箱；左右各留 1.2u 净空。L1 空位 x≈-8.6 / -4.0 / +4.9，L3 空位 x≈-7.0 |
| `bulkhead_industrial.png` | 192×384 @128 | 1.50×3.00 | 0 | **1.50** | 工业舱壁/门框，竖直件，底边贴地；L3 厂区墙面空缺处 |
| `checkpoint.png` | 377×746 @481 | 0.78×1.55 | 0 | **0.775** | 存档点标志，竖直件；L3 现有 `Checkpoint1/2` 可直接换图（当前用的是别的贴图） |
| `platform_stairs.png` | 1585×938 @355.3 | 4.46×2.64 | 0 | **1.32** | 楼梯，底边贴地；宽 4.46u，需 4.6u 净空 |
| `platform_catwalk.png` | 993×416 @166 | 5.98×2.51 | 0 | **h − 1.253** | 检修通道，**按"顶面高度 h"摆放**（h 为设计走行高度），不是按底边贴地 |
| `ground_industrial.png` | 2048×1024 @128 | 16.00×8.00 | 0 | **−4.00** | 地面横条，与 `Ground` 的 8 高一致 → 中心 y=−4 时顶面正好 y=0，可直接替换 L3 的 512 小图 |
| `ground_polluted_soil.png` | 2048×1024 @128 | 16.00×8.00 | 0 | **−4.00** | 同上规则；若要替换 L2/L3 地面，注意它画的是污染泥土，与森林/厂区主题不符 |
| `tree_dead.png` | 580×881 @339 | 1.71×2.60 | 0 | **沿用原实例值** | L1 枯树。画布尺寸 / PPU / pivot 与 `tree_forest.png` **完全一致**、树干底边同样贴死画布底行 → 换图时 `y` 与 `localScale` 一个都不用改（原值即"中心 y = 2.60/2 × scale"，如 scale 1.1 → y≈1.43） |
| `sign_glow_plastic.png` | 288×1024 @890 | 0.324×1.151 | 0 | **跟随原牌 transform** | 可读牌的发光边框（对应 `sign_plastic_warning`）。同画布同 PPU，子对象 `localPosition=(0,0,0)` 即零偏移对齐；order = 原牌 **+1** |
| `sign_glow_recycle.png` | 260×926 @805 | 0.323×1.150 | 0 | **跟随原牌 transform** | 同上，对应 `sign_recycle`。两张牌的画布/PPU 不同，故拆成两张边框，不要互相套用 |
| `debris_rubble.png` | 520×240 @400 | 1.30×0.60 | 0 | **0.30** | 碎石堆，三关通用地表杂物；底边贴 y=0，沿地面随机散布，彼此留 1.2u 以上间隔 |
| `debris_scrap.png` | 560×260 @400 | 1.40×0.65 | 0 | **0.325** | 废料堆（锈铁板 + 弯管 + 钢筋），L1/L3 优先；同上摆法 |
| `debris_grass.png` | 460×320 @400 | 1.15×0.80 | 0 | **0.40** | 草丛，L2 优先（L1 可当"残存杂草"）；同上摆法 |
| `bush_shrub.png` | 720×480 @400 | 1.80×1.20 | 0 | **0.60** | 灌木丛，底边贴 y=0；L2 左右岸与树之间填空，与 `debris_grass` 混摆；单株间距 **≥ 2.0u**，不要排成一条直线 |
| `vine_hanging.png` | 220×800 @400 | 0.55×2.00 | 0 | **挂点 y − 1.00** | 垂藤，**顶端贴住挂点**（画布顶行 22px 有像素、底行全透明），所以中心 y = 平台/横梁顶面 **− 1.00**；挂在 L2 平台边缘或树干上，不要落地 |

**两条换算规则（后续摆任何环境精灵都直接用）**：

1. **地面类**（高 8u 的横条）→ 中心 `y = −4`，顶面正好落在 `y = 0`（与 L1/L3 的 `Ground` 一致）。
2. **立物件**（箱子/楼梯/舱壁/存档杆）→ 中心 `y = 世界高度 / 2`，底边贴 `y = 0`；平台类先定走行高度 `h`，中心 `y = h − 高度/2`。

> **本表纠正的旧判断（原先 4 处错误）**：①`ground_polluted_soil` 原写"三关共用"，实测 **0 引用**；
> ②`ground_factory` 原写"闲置"，实测**被 L3 使用**；③`ground_polluted` 原写"闲置"，实测**被 L1 使用**；
> ④`ground_industrial` 原写"闲置"——这条是对的。根因是 A2 把三关地面改成"每关专用"之后，本表没有跟着更新。

### 8.4 排序层实测：§4.6 的 P0「17 个 order 15 归位」**并未完成**

2026-10-31 逐对象解析 `.unity` 的 `m_OrderInLayer` 与 `m_LocalPosition.z`：

| 场景 | SpriteRenderer 数 | `orderInLayer` 分布 | z 坐标 |
|---|---|---|---|
| Level01 | 19 | **全部 0** | 除相机 z=−10、一只对象 z=10 外，全为 0 |
| Level02 | 35 | **全部 0** | 同上 |
| Level03 | 37 | **全部 0** | 同上 |
| `Player.prefab` | 若干 | **无 `m_OrderInLayer` 字段 → 默认 0** | 全为 0 |

**结论**：§4.6 记录的"17 个物件全部 order = 15、与玩家同层"已不存在，但**问题本身没被修好**——
现在是从"统一 15"变成"统一 0"，**玩家也在 0**，仍然是"全场同层、靠 Y 排序、行为不确定"。
真正的解法是 §4.6 已写明的目标层（`ArtStyleGuide.md` §5.2），那个还没落地。

另外：三关都**没有定义自定义 Sorting Layer**（`m_SortingLayerID = 0`，即 Default），
所以分层只能靠 `orderInLayer` 的数值，不能靠 Layer 名。

**建议落值（L2，按 `ArtStyleGuide.md` §5.2 的目标层展开，待 A 确认）**：

| 对象 | 建议 order | 理由 |
|---|---|---|
| `Background` | **−100** | 背景最底 |
| `GroundL` / `GroundR` | **−10** | 地面永远最底（背景之上） |
| `PlatformL1/L2` / `PlatformR1/R2` | **−6** | 阶梯/平台在树下、玩家下 |
| `Tree`×8 | **−4** | 在玩家背后；玩家走过树后应被树冠遮挡 → 需把树拆成 `trunk` / `canopy` 两件，或用 Sorting Group 按 y 动态排序 |
| `Riverbed` | **−1** | 河床压在地面之上、水面之下 |
| `Water` / `WaterBlock1~3` | **0** | 水面盖住河床 |
| `Raft1/2/3` | **2** | 甲板浮在水面之上、玩家之下 |
| `CageVisual` / `AnimalVisual` | **5** | 笼子不该盖住玩家 |
| `NPC_Ava_Forest` / `Signboard_Forest` / `KnowledgeCard_*` | **6** | 可读物件要在玩家之上才不被挡 |
| 玩家 | **10** | 基准层 |
| `SpawnAnchorA/B` / `KillZone` | **−5** | 逻辑占位，视觉上不该被玩家看到 |

**逐对象清单（Level02 全部 35 个 `SpriteRenderer`，A 可照行直接填）**：

| # | 对象名 | x | y | 当前 order | 目标 order |
|---|---|---|---|---|---|
| 1 | `NPC_Ava_Forest` | −27.40 | 1.10 | 0 | **6** |
| 2 | `Tree` | −26.86 | 1.30 | 0 | **−4** |
| 3 | `KnowledgeCard_Forest_Oxygen` | −24.50 | 3.18 | 0 | **6** |
| 4 | `PlatformL1` | −24.50 | 2.49 | 0 | **−6** |
| 5 | `AnimalVisual` | −22.00 | 0.99 | 0 | **5** |
| 6 | `CageVisual` | −22.00 | 1.30 | 0 | **5** |
| 7 | `Tree` | −21.36 | 1.30 | 0 | **−4** |
| 8 | `PlatformL2` | −20.50 | 3.66 | 0 | **−6** |
| 9 | `AnimalVisual` | −18.00 | 0.99 | 0 | **5** |
| 10 | `CageVisual` | −18.00 | 1.30 | 0 | **5** |
| 11 | `GroundL` | −17.50 | −4.00 | 0 | **−10** |
| 12 | `WaterBlock3` | −15.00 | 0.00 | 0 | **0** |
| 13 | `Tree` | −14.36 | 1.30 | 0 | **−4** |
| 14 | `WaterBlock2` | −12.50 | 0.00 | 0 | **0** |
| 15 | `KnowledgeCard_Forest_Chain` | −11.50 | 0.60 | 0 | **6** |
| 16 | `SpawnAnchorA` | −10.00 | 0.20 | 0 | **−5** |
| 17 | `WaterBlock1` | −10.00 | 0.00 | 0 | **0** |
| 18 | `Tree` | −9.36 | 1.30 | 0 | **−4** |
| 19 | `Raft1` | −4.50 | −1.45 | 0 | **2** |
| 20 | `Background` | 0.00 | 2.20 | 0 | **−100** |
| 21 | `KillZone` | 0.00 | −2.40 | 0 | **−5** |
| 22 | `Raft2` | 0.00 | −1.45 | 0 | **2** |
| 23 | `Riverbed` | 0.00 | −5.00 | 0 | **−1** |
| 24 | `Water` | 0.00 | −4.10 | 0 | **0** |
| 25 | `Raft3` | 4.50 | −1.45 | 0 | **2** |
| 26 | `Tree` | 8.14 | 1.30 | 0 | **−4** |
| 27 | `Signboard_Forest` | 10.50 | 0.58 | 0 | **6** |
| 28 | `Tree` | 13.64 | 1.30 | 0 | **−4** |
| 29 | `SpawnAnchorB` | 16.00 | 0.20 | 0 | **−5** |
| 30 | `GroundR` | 17.50 | −4.00 | 0 | **−10** |
| 31 | `Tree` | 19.64 | 1.30 | 0 | **−4** |
| 32 | `PlatformR1` | 20.50 | 2.80 | 0 | **−6** |
| 33 | `KnowledgeCard_Forest_Invasive` | 24.50 | 4.89 | 0 | **6** |
| 34 | `PlatformR2` | 24.50 | 4.20 | 0 | **−6** |
| 35 | `Tree` | 25.14 | 1.30 | 0 | **−4** |

> 清点核对：35 行 = 场景内全部 `SpriteRenderer`，无遗漏。
> 一次性的做法是给这 35 个 `SpriteRenderer` 逐个填 `Order in Layer`，**不改碰撞体、不改坐标**，
> 属纯视觉改动、可整体回滚。同名对象有多个时按 x 坐标区分（表里已列出 x/y）。

**L1 逐对象清单（19 行，同样全是 order 0）**：

| # | 对象名 | x | y | 目标 order |
|---|---|---|---|---|
| 1 | `Decor_Tree_L01a` | −18.40 | 1.43 | **−4** |
| 2 | `PlatformL2` | −15.60 | 2.48 | **−6** |
| 3 | `Decor_Tree_L01b` | −15.60 | 1.17 | **−4** |
| 4 | `Decor_Sign_L01W` | −13.00 | 0.58 | **6** |
| 5 | `PlatformL1` | −12.80 | 1.28 | **−6** |
| 6 | `NPC_Ava` | −6.00 | 1.10 | **6** |
| 7 | `SpawnAnchorA` | −3.00 | 0.20 | **−5** |
| 8 | `KnowledgeCard1` | −2.50 | 0.60 | **6** |
| 9 | `Background` | 0.00 | 2.00 | **−100** |
| 10 | `Ground` | 0.00 | −4.00 | **−10** |
| 11 | `Signboard_Plastic` | 2.00 | 0.58 | **6** |
| 12 | `KnowledgeCard2` | 7.50 | 0.60 | **6** |
| 13 | `SpawnAnchorB` | 8.00 | 0.20 | **−5** |
| 14 | `Signboard_Recycle` | 9.00 | 0.58 | **6** |
| 15 | `PlatformR1` | 12.80 | 1.28 | **−6** |
| 16 | `Decor_Sign_R01R` | 12.80 | 0.58 | **6** |
| 17 | `Decor_Tree_R01a` | 15.00 | 1.49 | **−4** |
| 18 | `PlatformR2` | 15.60 | 2.48 | **−6** |
| 19 | `Decor_Tree_R01b` | 18.20 | 1.23 | **−4** |

**L3 逐对象清单（37 行，同样全是 order 0）**：

| # | 对象名 | x | y | 目标 order |
|---|---|---|---|---|
| 1 | `NPC_Ava_LeverHint` | −20.08 | 1.10 | **6** |
| 2 | `Decor_Sign_03WL` | −18.50 | 0.58 | **6** |
| 3 | `PlatformL2` | −15.60 | 2.48 | **−6** |
| 4 | `PlatformL1` | −12.80 | 5.13 | **−6** |
| 5 | `SpikeField` | −12.10 | 0.00 | **−5** |
| 6 | `NPC_Robot_FactoryIntro` | −12.00 | 1.10 | **6** |
| 7 | `KnowledgeCard_Factory_Carbon` | −10.50 | 0.60 | **6** |
| 8 | `PlatformC` | −7.93 | 6.63 | **−6** |
| 9 | `SpawnAnchorA` | −4.20 | 0.20 | **−5** |
| 10 | `SecondFloor` | −2.87 | 3.48 | **−6** |
| 11 | `Decor_Sign_03WM` | −2.50 | 0.58 | **6** |
| 12 | `LeverSwitch_03` | −1.21 | 4.26 | **6** |
| 13 | `Decor_Sign_03RL` | −1.20 | 4.06 | **6** |
| 14 | `Background` | 0.00 | 2.50 | **−100** |
| 15 | `SecondFloorRightWall` | 0.23 | 5.15 | **−6** |
| 16 | `SpawnAnchorB` | 10.00 | 0.20 | **−5** |
| 17 | `Decor_Sign_03RM` | 12.50 | 0.58 | **6** |
| 18 | `PlatformR1` | 12.80 | 1.28 | **−6** |
| 19 | `PlatformR2` | 15.60 | 2.48 | **−6** |
| 20 | `Decor_Sign_03RR` | 19.00 | 0.58 | **6** |
| 21 | `Ground` | 24.00 | −4.00 | **−10** |
| 22 | `Jump_Sign` | 25.00 | 0.58 | **6** |
| 23 | `Jump_Crumble1` | 27.00 | 1.88 | **−6** |
| 24 | `Jump_Mover` | 28.80 | 3.18 | **−6** |
| 25 | `Jump_Spikes` | 33.50 | 0.00 | **−5** |
| 26 | `Jump_Crumble2` | 35.40 | 2.28 | **−6** |
| 27 | `Puzzle_Step` | 39.40 | 1.08 | **−6** |
| 28 | `Puzzle_Floor` | 44.85 | 2.63 | **−6** |
| 29 | `Puzzle_Spikes` | 46.50 | 0.00 | **−5** |
| 30 | `Puzzle_Lever` | 47.00 | 3.20 | **6** |
| 31 | `KnowledgeCard_Factory_Renewable` | 50.00 | 0.60 | **6** |
| 32 | `Puzzle_Sign` | 53.50 | 0.58 | **6** |
| 33 | `SpawnAnchorC` | 54.00 | 0.20 | **−5** |
| 34 | `Puzzle_NPC` | 60.80 | 1.10 | **6** |
| 35 | `Visual` | 63.00 | 0.14 | **−3** |
| 36 | `SpawnAnchorD` | 64.00 | 0.20 | **−5** |
| 37 | `Visual` | 65.20 | 0.22 | **−3** |

**三关统一的分层口径**（同一类对象跨关必须同值，否则同一个玩家会在不同关卡被不同的东西挡住）：

| 类别 | order |
|---|---|
| `Background` | −100 |
| `Ground` | −10 |
| 平台/台阶/移动块/二层地板（`Platform*`、`SecondFloor*`、`Jump_Crumble*`、`Jump_Mover`、`Puzzle_Step`、`Puzzle_Floor`） | −6 |
| 危险物（`SpikeField`、`Jump_Spikes`、`Puzzle_Spikes`、`KillZone`） | −5 |
| 树木/枯树（`Tree`、`Decor_Tree_*`） | −4 |
| 喷口等环境装置（L3 的两个 `Visual`） | −3 |
| 河床 `Riverbed` | −1 |
| 水面 `Water`、`WaterBlock1~3` | 0 |
| 木筏 `Raft1~3` | 2 |
| 笼/动物 `CageVisual`、`AnimalVisual` | 5 |
| 可读/可交互/NPC（牌子、知识卡、NPC、拉杆） | 6 |
| 玩家 | 10 |
| 逻辑占位（`SpawnAnchorA~D`） | −5 |

---

## 九、待补汇总（按优先级）

| 级别 | 项 | 关卡 | 成本 | 影响 |
|---|---|---|---|---|
| **P1** | 木筏换真贴图 | L2 | 40 min | 原判「隐形」有误：实为纯色块，能看见但读不出是木筏 |
| **P0** | 三关地面换本关专用贴图 | L1/L2/L3 | L1 无需改；L3 直接换 `ground_industrial`；L2 需先重画 forest 到 2048 | 一眼可见的主题错配 |
| **P0** | L2 的 17 个 order 15 归位 | L2 | 1 h | 玩家被树/笼遮挡 |
| **P0** | 净化后地面换草地版 | 全部 | 2 h | 净化"看得见" |
| P1 | 三面土墙换工业材质 | L3 | 1 h | 厂区不像厂区 |
| ~~P1~~ ✅ | ~~L1 枯树贴图~~ | L1 | 1 h | **已交付** `tree_dead.png`（见 §3.4），待 A 换 4 棵 `Decor_Tree_*` 的 sprite |
| ~~P1~~ ✅ | ~~可读牌与装饰牌视觉区分~~ | 全部 | 1 h | **美术件已交付** `sign_glow_plastic` / `sign_glow_recycle`（见 §3.5），待 A 挂 `SignGlow` 子对象 |
| P1 | 两张背景重出 1080p | L2/L3 | 2 h | 边缘发软 |
| P1 | `Jump_Sign`/`Puzzle_Sign` 接可读触发器 | L3 | 30 min | 7 块牌子全不能读 |
| P2 | 草丛/灌木/藤蔓/管道等密度元素 | 全部 | 各 2 h | **部分交付**：`debris_grass`（草丛）、`debris_rubble`（碎石）、`debris_scrap`（废料）、`bush_shrub`（灌木）、`vine_hanging`（垂藤）已出；**管道/支架/警示带仍未做**（见 §5.7） |

---

## 十、漂移记录

| 日期 | 发现 | 说明 |
|---|---|---|
| 2026-10-30 | 三关地面共用污染土壤贴图，3 张专用贴图闲置 | 生成脚本 `MakeGround` 从未按关卡取不同贴图 |
| 2026-10-30 | L2 木筏 `SpriteRenderer.sprite` 为 `WhiteSquare`（**非 null**） | v1.1 更正：4×4 白图 × scale 112.5 = 4.5×0.4u，能序列化也能看见，只是纯色无纹理 |
| 2026-10-30 | L2 有 17 个对象 order = 15（兜底值） | `FixAllLevelSpriteSorting.GuessOrder()` 返回 15 |
| 2026-10-30 | L3 `NPC_Ava_LeverHint` 用机器人贴图 | 命名与素材矛盾 |
| 2026-10-30 | L3 七块装饰牌中 `Jump_Sign`/`Puzzle_Sign` 名字暗示提示牌但无脚本 | 设计意图未落地 |
| 2026-10-30 | L1 污染星球用 `tree_forest`（茂盛绿树） | 无枯树素材 |
| 2026-10-30 | 可读提示牌与装饰牌贴图完全复用 | 无"可交互"视觉标识 |

---

## 十一、变更记录

| 版本 | 日期 | 变化 | 原因 |
|---|---|---|---|
| v1.0 | 2026-10-30 | 首版。三关逐关物件实测清单（19/35/37 个精灵）、区段划分与美术意图、三层空间结构、7 处具体缺陷与修法、通用元素规范（地面/平台/装饰牌/提示牌/NPC）、净化视觉扩展 4 项、资产使用全量清点（3 闲置 / 3 规格不合格 / 4 错配） | 项目此前只有关卡策划视角的 `LevelDesign.md`（管坐标和流程），完全没有"关卡美术"这一岗位的文档。实测发现木筏隐形、三关地面同一张、17 个物件挤兜底层等硬伤，都没有落到任何文档里 |
| v1.1 | 2026-10-30 | 新增「附录 A · 美术资产清账」：`Assets/Art/**` 624 个资产全量分类（有效保留 533 / 归档 91），归档清单与冻结区说明落表 | 美术目录累积了大量拼版中间产物与备份，缺少可核查的账目；C1 清账需要一份"每个文件去哪了"的记录 |

---

## 附录 A · 美术资产清账（C1 · 2026-10-30）

**口径**：清点 `Assets/Art/**` 下全部资产文件（`.meta` 不计入条目数，但随资产一起移动）。
「是否被引用」= 在 `Assets/` 内 113 个非 Art 文本资产（`.anim` / `.prefab` / `.unity` / `.asset` / `.mat` / `.controller` / `.json` 等）里能否搜到该文件的 GUID。
按路径（`Sheets/`、`_预览/`、`*.xlsx`、`*.meta_keep`）判定中间产物，按 GUID 判定是否在用，两者交叉复核。

**总量**：624 个资产 · 被引用 469 · 未引用 155 · **归档 91** · 保留 533。

### A.1 分类汇总

| 类别 | 文件数 | 处置 | 说明 |
|---|---|---|---|
| 有效替换 · 动画帧 | 422 | **保留** | `Frames/<角色>_<动作>/*.png`，全部被 `.anim` 按 GUID 引用 |
| 有效替换 · 精灵 | 46 | **保留** | `Environment/Props`、`Environment/Tiles`、`Environment/Background`、`FX/Frames`，被场景或 FX prefab 引用 |
| 新增待接入 · 精灵 | 8 | **保留** | 已生成但尚未挂到场景，等 A 线接入（见 A.4） |
| 参考稿 | 1 | **保留** | `00_角色设定/Player_Concept.png`（设定稿，非运行时资源） |
| 文档 / 工具图 | 3 | **保留** | `README_素材说明.md`、`Player/Lockup/Player_Lockup_A.png`、`Player/Tools/Player_Tool_Pliers.png` |
| 中间产物 · raw 图集 | 64 | 38 归档 / 26 保留 | `Sheets/raw/*_raw.png` 是拼版前的原始大图，Unity 不引用；保留的 26 个在冻结区 `Player/` 内 |
| 中间产物 · 拼版图集 | 58 | 37 归档 / 21 保留 | `Sheets/*_sheet.png` 是管线副产物（运行时只吃 `Frames/`），故归档；保留的 21 个在冻结区 |
| 中间产物 · 预览图 | 13 | **归档** | `_预览/contact_*.png`、`_预览/check_*.png`，人工比对用的取样拼图 |
| 中间产物 · 排期表 | 3 | **归档** | `Assets/Art` 根下的 `*.xlsx`（含 `_备份_原始规划`），属项目管理文件，不属运行时资产 |
| 中间产物 · 对齐数据 | 3 | **保留** | `Player/Frames/*/align_fist.json`，在冻结区内 |
| 未引用帧 | 3 | **保留（不动）** | `Enemy02_Attack_02`、`NPC_Ava_Idle_03`、`NPC_Ava_Idle_05`。当前 `.anim` 未引用，但处在帧序列中间，删除会让"重跑管线 → 帧数变化"，故只记录不归档 |

### A.2 归档去向

全部移到 `Assets/Art/_archive/`，**目录结构按 `Assets/Art/` 相对路径原样镜像**（例如
`Assets/Art/Art/Boss/Boss01/Sheets/raw/Boss01_Idle_raw.png` → `Assets/Art/_archive/Art/Boss/Boss01/Sheets/raw/Boss01_Idle_raw.png`）。
移动用 `git mv`，`.png` 与 `.png.meta` 一起移动，**GUID 不变**，因此不存在引用断链。
复核结果：归档后全库美术资产被引用数仍为 469，**归档区内被引用文件 = 0**。

### A.3 归档清单（91 个资产）

- `(根)/`（3）
  - 2D游戏动画资源排期表.xlsx
  - 2D游戏动画资源排期表_备份_原始规划.xlsx
  - schedule_before_attack03.xlsx
- `_预览/`（13）
  - check_Enrage01.png
  - check_Move00_armpit.png
  - contact_a.png
  - contact_b.png
  - contact_Boss_Boss01.png
  - contact_Boss01_20261026.png
  - contact_Enemies_Enemy01.png
  - contact_Enemies_Enemy02.png
  - contact_Enemies_Enemy03.png
  - contact_FX.png
  - contact_P1_new_clips.png
  - contact_sheets.png
  - contact_Towers_Tower01.png
- `Art/Boss/Boss01/Sheets/`（9）
  - Boss01_AttackA_sheet.png
  - Boss01_AttackB_sheet.png
  - Boss01_Charge_sheet.png
  - Boss01_Die_sheet.png
  - Boss01_Enrage_sheet.png
  - Boss01_Hurt_sheet.png
  - Boss01_Idle_sheet.png
  - Boss01_Move_sheet.png
  - Boss01_Special_sheet.png
- `Art/Boss/Boss01/Sheets/raw/`（10）
  - Boss01_AttackA_raw.png
  - Boss01_AttackB_raw.png
  - Boss01_Charge_raw.png
  - Boss01_Die_raw.png
  - Boss01_Die_raw_OLD2048.png.meta_keep
  - Boss01_Enrage_raw.png
  - Boss01_Hurt_raw.png
  - Boss01_Idle_raw.png
  - Boss01_Move_raw.png
  - Boss01_Special_raw.png
- `Art/Enemies/Enemy01/Sheets/`（5）
  - Enemy01_Attack_sheet.png
  - Enemy01_Die_sheet.png
  - Enemy01_Hurt_sheet.png
  - Enemy01_Idle_sheet.png
  - Enemy01_Move_sheet.png
- `Art/Enemies/Enemy01/Sheets/raw/`（5）
  - Enemy01_Attack_raw.png
  - Enemy01_Die_raw.png
  - Enemy01_Hurt_raw.png
  - Enemy01_Idle_raw.png
  - Enemy01_Move_raw.png
- `Art/Enemies/Enemy02/Sheets/`（5）
  - Enemy02_Attack_sheet.png
  - Enemy02_Die_sheet.png
  - Enemy02_Hurt_sheet.png
  - Enemy02_Idle_sheet.png
  - Enemy02_Move_sheet.png
- `Art/Enemies/Enemy02/Sheets/raw/`（5）
  - Enemy02_Attack_raw.png
  - Enemy02_Die_raw.png
  - Enemy02_Hurt_raw.png
  - Enemy02_Idle_raw.png
  - Enemy02_Move_raw.png
- `Art/Enemies/Enemy03/Sheets/`（6）
  - Enemy03_Attack_sheet.png
  - Enemy03_Chase_sheet.png
  - Enemy03_Die_sheet.png
  - Enemy03_Hurt_sheet.png
  - Enemy03_Idle_sheet.png
  - Enemy03_Spawn_sheet.png
- `Art/Enemies/Enemy03/Sheets/raw/`（6）
  - Enemy03_Attack_raw.png
  - Enemy03_Chase_raw.png
  - Enemy03_Die_raw.png
  - Enemy03_Hurt_raw.png
  - Enemy03_Idle_raw.png
  - Enemy03_Spawn_raw.png
- `Art/FX/Sheets/`（6）
  - fx_dust_run_sheet.png
  - fx_explode_sheet.png
  - fx_gas_sheet.png
  - fx_hit_sheet.png
  - fx_purify_sheet.png
  - fx_slash_sheet.png
- `Art/FX/Sheets/raw/`（6）
  - fx_dust_run_raw.png
  - fx_explode_raw.png
  - fx_gas_raw.png
  - fx_hit_raw.png
  - fx_purify_raw.png
  - fx_slash_raw.png
- `Art/NPC/NPC_Ava/Sheets/`（2）
  - NPC_Ava_Idle_sheet.png
  - NPC_Ava_JinlingAssault_sheet.png
- `Art/NPC/NPC_Ava/Sheets/raw/`（2）
  - NPC_Ava_Idle_raw.png
  - NPC_Ava_JinlingAssault_raw.png
- `Art/Towers/Tower01/Sheets/`（4）
  - Tower01_Aim_sheet.png
  - Tower01_Disable_sheet.png
  - Tower01_Fire_sheet.png
  - Tower01_Idle_sheet.png
- `Art/Towers/Tower01/Sheets/raw/`（4）
  - Tower01_Aim_raw.png
  - Tower01_Disable_raw.png
  - Tower01_Fire_raw.png
  - Tower01_Idle_raw.png

### A.4 保留但尚未接入场景的 8 张精灵（交 A 线挂载）

| 文件 | 位置 | 用途 |
|---|---|---|
| `checkpoint.png` | `Environment/Props/` | 存档点标记 |
| `platform_catwalk.png` | `Environment/Props/` | L3 工业栈道 |
| `platform_stairs.png` | `Environment/Props/` | 楼梯平台（StarEco 版 L3 用作 `StairsL/StairsR`） |
| `bulkhead_industrial.png` | `Environment/Props/` | 工业舱壁 |
| `ground_industrial.png` | `Environment/Tiles/` | L3 地面候选 |
| `ground_polluted_soil.png` | `Environment/Tiles/` | L1 污染土壤（大幅原图，3.4 MB） |
| `water_river.png` | `Environment/Tiles/` | L2 水面横条 |
| `Boss01_Lockup_A.png` | `Boss/Boss01/Lockup/` | Boss 定妆图（美术留档，不进运行时） |
| `raft_deck.png` | `Environment/Props/` | **C3 新增**。木筏甲板，1800×157 @ PPU 400 = 4.50×0.3925u，**与 `Level02` 中 `Raft1/2/3` 的 `BoxCollider2D`（4.41×0.392）齐平**（初版 4.50×0.65 比碰撞盒高 0.13u，角色脚底会陷进甲板，已按碰撞盒重生）；由 A 挂载 |
| `crate_wood.png` | `Environment/Props/` | **C3 新增**。木箱，440×400 @ PPU 400 = 1.10×1.00u，与 `breakable_rock`（1.57×0.95）同量级；当前**无场景引用**。落位规则：中心 `y=0.50`（底边正好落在 `Ground` 顶面 `y=0`），左右需 **1.2u 净空**。实测空闲区间：L1 约 `x=-8.6 / -4.0 / +4.9`，L3 约 `x=-7.0`——最终摆位由 A 决定 |

### A.5 冻结区说明

`Assets/Art/Art/Player/**` 本轮**零动作**：未归档、未改名、未移动。
该区共 441 项改动（299 修改 / 124 新增 / 18 删除）原样保留，其中 362 个资产被 `.anim` 引用，
`Sheets/raw`、`Sheets/*_sheet.png`、`align_fist.json` 虽属中间产物，因冻结规则一并原地保留。

### A.6 命名与目录的约定例外（2026-10-30 定稿）

C2 复核时发现两处「不符合字面规范但**有意保留**」的情况，经确认按现状固化，不再改动：

1. **帧文件用 PascalCase，不用 snake_case**。
   `Frames/<角色>_<动作>/<角色>_<动作>_NN.png` 这套命名已被 **422 个帧文件**统一沿用，
   且 `process_sheets.SPECS`、`verify_raw_grid.GRID`、`auto_pipeline.py` 的模式识别都按它匹配。
   改成 snake_case 会让这批脚本全部失配，收益为零、风险很高，故**保留 PascalCase**。
   新增的**独立精灵**（非帧）仍要求 snake_case，两类命名的分界线是「是否属于某个动作序列」。
2. **`npc_ava.png`、`npc_guard_robot.png` 保留在 `Environment/Props/`**。
   严格按分类它们属于「角色」类，应放 `NPC/`；但这两个是场景直接摆放的单张 NPC 立绘
   （不是逐帧动画），且正在被关卡场景按 GUID 引用，迁移只会带来一次无意义的资源重导入。
   故**原地保留**，后续若 NPC 也改成逐帧动画，再随新目录一起迁。

**结论**：`Assets/Art` 根目录已无散文件，新增资产全部落在七个约定目录内；
上述两条属已确认的例外，不作为缺陷。


