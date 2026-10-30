# 游戏设计文档（GDD）

> 项目：基于Unity2D的"星际环保"动作冒险游戏设计与实现
> 版本：v1.1（补系统总览 + SDD 全量重写） | 最后更新：2026-10-30
> 参考模板：schrodinger-skills GDD 模板（按动作冒险类型裁剪）
> **文档总索引见 `README.md`**（岗位-文档矩阵、权威顺序、同步规则）
> 下游文档：
> - 策划线：`LevelDesign.md`（关卡坐标/物件/动线）、`LevelDesign.docx`（关卡概念案）、`NarrativeDesign.md`（世界观/角色小传/剧情链/对白风格）、`AudioDesign.md`（22 个 SFX 触发点/BGM/响度审计）
> - 美术线：`ArtStyleGuide.md`（视觉支柱/三关色板/渲染层/UI 令牌）、`LevelArt.md`（逐关画面构成/资产清点）、`AnimationVFXSpec.md`（Animator 参数/事件/动画规格/特效落地）
> - 程序线：`SystemOverview.md`（系统总览：分层/系统清单/依赖矩阵/事件拓扑/启动时序）、`TDD.md`（技术方案）、`CodeStandard.md`（编程规范/已踩坑清单）、`specs/01~11`（逐功能 EARS 规范）
> - 验证：`TestPlan.md`（60+ 验收用例/回归清单）、`性能实测报告.md`

---

## 一、设计首页

| 区域 | 内容 |
|---|---|
| 项目一句话 | 玩家扮演一名星际环保员小队成员，在横版星球场景中奔跑跳跃、净化污染、对抗污染生物，逐步恢复三颗星球的生态 |
| 游玩条件 | 单人 / PC（Windows）/ 键盘手柄 / 单关 10~15 分钟 / 共 3 关 |
| 玩家承诺 | 用"净化喷壶"清理污染的可动性 + 面对污染生物的横版动作挑战 |
| 设计支柱 | 1. 环保行为即核心动作（净化不是过场动画，是玩法本身）2. 难度随污染程度递进（场景越脏，敌人越强）3. 教育内容融入场景（提示牌/NPC，不做说教弹窗） |
| 反例校准 | 拒绝"资源建造经营"玩法（偏离动作冒险）；拒绝纯问答式环保教学（破坏节奏） |
| 核心动词 | 跑、跳、攻击（夹钳）、净化（喷壶） |
| 循环一句话 | 探索受污染区域 → 清理垃圾/击败污染生物获得净化值 → 净化值解锁通路/削弱敌人 → 到达净化核心完成关卡 |
| 当前范围 | 3 个关卡 + 1 个 Boss + 3 种小怪 + 1 种防御塔；明确不做：多人、开放世界、塔防经营 |

## 二、玩法走读（60~120 秒样例）

玩家降落在第一关星球表面（目标：清理眼前垃圾堆）→ 看到 HUD 上净化进度 34% → 前方塑料袋怪（Enemy01）缓慢逼近，玩家用夹钳攻击两下击退，靠近垃圾堆按互动键喷洒净化 → fx_purify 特效亮起，进度 +5% → 遇到分叉：上层平台有提示牌（讲塑料污染），下层有被垃圾堵住的风扇通路 → 玩家选择先读提示牌，获得"污染生物惧怕净化剂"的知识 → 用喷壶蓄力攻击一次清掉挡路垃圾，通路打开 → 小成功：净化值过半，场景背景色从灰暗转向清新。

## 三、核心机制

### 3.1 基础规则
- 移动：左右跑动（Run）、跳跃（Jump）、下落与落地（Fall/Land），支持土狼时间与跳跃缓冲（手感窗口各 0.1s）
- 攻击：近战夹钳（Attack01，fx_slash），三段连击第三段击退
- 净化：喷壶喷射（消耗净化剂，fx_purify），对垃圾堆/污染源长按 2 秒完成净化；喷壶只用于净化环境，**不对污染生物造成伤害**（对敌输出统一由夹钳承担）
- 生命：玩家 10 半心点 = 5 颗心（Player_Hurt/Die，HUD 支持半心显示），受伤无敌 1 秒
- 胜负：关卡内净化值达 100% 且击败守关敌人 → 过关；HP 归零 → **本局失败**，弹出结算面板（重新开始 = 重载本关 / 返回主菜单）
- 通关收尾顺序：**最后一关先播落幕对白（`ava_ending`）再弹结算**，前两关直接弹结算。`LevelManager.TryPlayEndingDialogue()` 按 `LevelIndex==3` 分支，播完才 `CommitFinish`（详见 `NarrativeDesign.md` §9）
- 重生粒度：**整关重开，无中途检查点**。`PlayerDeathState.Tick` 注释与 `LevelManager.Update` 一致——死亡即进入失败终态；`RestartLevel()` 用 `SceneManager.LoadScene` 重载，原因是已净化对象、永久熄灭的敌人等运行时状态单向不可逆，原地重置无法回到初始态

### 3.2 进阶系统
- 净化剂经济：地图拾取 + 击败敌人掉落，净化与蓄力攻击消耗（资源抉择）
- 检查点：❌ **未实现**（设计意图为「净化过的净化塔自动激活」）。全项目无检查点脚本、场景无 `CP_*` 对象，`SfxId.Checkpoint` 是孤儿条目（22 个音效中唯一无调用点）。若后续补做，需新增 `Checkpoint.cs` + 场景触发器 + `LevelManager` 记录重生锚点（当前 `RestartLevel` 只能重载整关）。详见 `AudioDesign.md` §7 与 `LevelDesign.md` §2.3
- 污染度：全关污染源未清理的比例影响场景色调与敌人刷新频率（支柱 2 的落地）

### 3.3 难度与节奏
- 第一关：慢速 Enemy01 + 简单跳跃平台，教学化
- 第二关：Enemy02（远程毒气，fx_gas）+ 陷阱解救互动 + 水流解谜
- 第三关：Tower01 防御塔（Fire 状态弹幕）+ Enemy03（Chase 追击型）+ Boss01 三阶段战

## 四、内容系统

### 4.1 角色/单位（美术已就位 ✓，动作集以 Assets/Art/Art 实际帧目录为准）
| 单位 | 动画集（套数） | 定位 |
|---|---|---|
| 玩家 | Idle/Run/Jump/Fall/Land/Hurt/Die/Attack01/Attack02/Attack03/AttackAir/Interact/Dash/Roll/HeavyAttack/Crouch/WallSlide/WallJump/LedgeGrab/Turn/Victory/Idle02（22 套） | 主角，背净化喷壶背包 |
| Enemy01 污染污泥怪 | Idle/Move/Attack/Hurt/Die（5 套） | 慢速近战，第一关 |
| Enemy02 变异毒植物 | Idle/Move/Attack/Hurt/Die（5 套） | 远程毒气，第二关 |
| Enemy03 巡逻机器人 | Idle/Chase/Attack/Hurt/Die/Spawn（6 套） | 发现玩家后高速追击，第三关 |
| Tower01 毒气防御塔 | Idle/Fire/Disable/Aim（4 套） | 固定炮台，可被净化关闭 |
| Boss01 工厂污染核心 | Idle/Move/AttackA/AttackB/Hurt/Die/Enrage/Special（8 套） | 第三关 BOSS，双攻击技能 + 狂暴/特殊招 |
| 艾娃（NPC） | Idle（12 帧） | 星球监测站 AI 的全息投影，三关引导者；**动画帧未被任何场景引用，场景里用的是静态 `npc_ava.png`** |
| 老维护机器人（NPC） | 静态立绘 | 第三关旧型厂区机器人，给机关提示；`npc_guard_robot.png` |

> 进度注：玩家动作已全部补齐（22 套）；敌方单位动作亦已就位（含 Enemy03_Spawn、Tower01_Aim、Boss01_Enrage/Special 等 P1 补充动作）。
> 叙事设定：**三颗星球均无人类居民**，玩家是唯一在场的人类，场景中的"人"只有 AI 投影与旧机器人。角色小传与三关剧情链见 `NarrativeDesign.md`。

### 4.2 关卡（落地细节见 LevelDesign.md，创意案见 LevelDesign.docx，叙事链见 NarrativeDesign.md §4）
1. 污染初现的星球 —— 教学 + 轻度战斗（40u，3 条目标链）
2. 生态失衡的森林 —— 解救动物 + 解谜（56u，4 条目标链，14u 河道）
3. 能源工厂的危机 —— 高强度战斗 + Boss（96u 四段式，2 条目标链）

> 三关采用**溯源式**结构：塑料星球（伤口）→ 生态森林（病灶）→ 能源工厂（病根，污染总来源）。叙事载体已补齐为三关各 5 个交互点：L1（1 对话 + 2 提示牌 + 2 知识卡）、L2（1 对话 + 1 提示牌 + 3 知识卡）、L3（3 对话 + 2 知识卡）。补白前后与摆放约束见 `NarrativeDesign.md` §5.1 / §9。

### 4.3 道具

| 道具 | 作用 | 落地状态（2026-10-30 核） |
|---|---|---|
| 可回收垃圾桶 | 净化剂补给 | ⚠️ 组件 `PurifyPickup` 已实现，**三关场景内 0 实例**（未摆放） |
| 空气净化装置 | 区域减速敌人 | ❌ 未实现 |
| 能量核心 | HP 补给 | ❌ 未实现（`TestDrop.asset` 掉落表仅 `PurifyBottle` 一种） |
| 剧情收集品 | 环保知识卡片 | ✅ `KnowledgeCardPickup` + `CodexService`；卡片数据 **8 张**（`Resources/Data/knowledge_cards.json`），但**仅 Level01 摆 2 个**，Level02/03 未摆 |

> 结论：4 种道具中 1 种完整落地、1 种「有组件无摆放」、2 种未实现。掉落系统（`DropTable` / `DropSpawner`）**代码已就绪、三关场景各挂 1 个组件，但 `_dropTable` 与 `_pickupPrefab` 两个字段全部为空**——空表安全跳过，所以敌人死亡不掉任何东西。接上 `TestDrop.asset` + `PurifyPickup.prefab` 即可启用（约 10 分钟），但会改变关卡难度，需与数值表一起评估。

## 五、界面规划

| 界面 | 内容 | 落地状态（2026-10-30 核） |
|---|---|---|
| HUD | HP（心形×5）、净化剂条、净化值进度条、当前目标提示 | ✅ `HUDController` |
| 主菜单 | 开始 / 继续 / 关卡选择 / 图鉴 / 设置 / 退出 | ✅ `MainMenuPanel` |
| 暂停 | 继续 / 重开 / 返回主菜单 | ✅ `PausePanel` |
| 设置 | BGM 音量、音效音量、画质档位 | ✅ `SettingsPanel`（双音量滑块 + 画质切换） |
| 关卡选择 | 三关解锁状态 | ✅ `LevelSelectPanel` |
| 结算 | 净化度 / 用时 / 击杀 / 拾取 + 字母评级 + ★ | ✅ `ResultPanel`（100%→S，≥60%→A，≥30%→B，其余 C） |
| 图鉴 | 已收集知识卡片 | ✅ `CodexPanel` + `CodexService`，Title/Level01/02/03 四场景均已挂载 |
| 对话框 | NPC 对话（逐字显示 + 头像位）、提示牌阅读 | ✅ `DialoguePanel` / `SignboardTrigger` |

> 界面规划**全部落地**。唯一缺口在内容侧而非界面侧：图鉴有 8 张卡片数据，场景只摆了 2 张（见 4.3）。

## 六、音效与音乐

| 项目 | 规划 | 落地状态（2026-10-30 核） |
|---|---|---|
| BGM | 主菜单 1 + 每关 1 + Boss 1（共 5 首） | ⚠️ **4 首已就位**（`Resources/Audio/Bgm/`：Title / Level01 / Level02 / Level03），**Boss BGM 未制作** |
| SFX | 跳跃 / 落地 / 攻击 / 喷射 / 净化 / 受伤 / 死亡 / 拾取 / UI（9 类） | ✅ **22 个已就位**（`SfxId` 枚举 22 项 ↔ `Assets/Audio/Sfx/*.wav` 22 个，一一对应） |
| 环境音 | 第一关工厂低鸣、第二关风声鸟鸣、第三关机械运转 | ❌ **未实现**：无环境音资产，`SfxId` 无 Ambience 类，`AudioManager` 无环境音通道 |
| 污染度驱动 | BGM 随污染度改变混音亮度 | ⚠️ **仅视觉侧实现**：`AtmosphereController` 把污染度插值到主摄像机背景色（灰紫 ↔ 自然绿），**音频侧未做**（BGM 无亮度/滤波参数） |

**SFX 全集（22 个，按 `SfxId` 分组）**

| 分组 | 音效 |
|---|---|
| 移动（3） | `Jump` `Land` `Footstep` |
| 战斗（6） | `Attack` `Hurt` `Death` `EnemyHit` `BossHit` `HeavyRelease` |
| 净化（4） | `SprayStart` `PurifyComplete` `BreakPlant` `BreakStone` |
| 交互（5） | `Rescue` `PuzzleSolved` `Checkpoint` `Pickup` `UIClick` |
| 环境物理（2） | `Splash` `GrassRustle` |
| 结算（2） | `LevelComplete` `LevelFail` |

> 绑定方式：`Assets/Scripts/Editor/AudioLibrarySetup.cs` 按**文件名约定**自动回填 `GameAudioLibrary` 资产（`Assets/Audio/Sfx/<SfxId>.wav`、`Resources/Audio/Bgm/<Name>.wav`）；缺失项运行时静默降级，不报错。素材由 `Tools/audio-pipeline/synthesize.py` 程序化合成。
>
> **逐音规格（时长/响度/构成）、26 个触发点定位、BGM 三关 BPM 曲线、响度审计、扩展流程详见 `AudioDesign.md`**（本文只保留规划层结论）。

## 七、视觉风格

| 项目 | 规格 | 落地状态（2026-10-30 核） |
|---|---|---|
| 整体风格 | 2D 横版，手绘卡通（明亮色块 + 粗描边） | ✅ 见 `Assets/Art/README_素材说明.md`（风格基准） |
| 主色板 | 自然绿（未污染）↕ 灰紫（污染），随净化值插值过渡 | ✅ `AtmosphereController` 订阅 `PurityChangedEvent` → 主摄像机背景色插值 |
| FX 清单 | 6 个：`fx_dust_run` `fx_explode` `fx_gas` `fx_hit` `fx_purify` `fx_slash` | ✅ **6/6 就位**（`Assets/Art/Art/FX/Frames/`），运行时由 `FxOneShot` 按需实例化 |

## 八、数值配置
| 参数 | 值 | 落地状态 |
|---|---|---|
| 玩家移速 MoveSpeed | 6 u/s | ✅ 已落地 PlayerConfig |
| 跳跃初速 JumpVelocity | 13 u/s（单次跳跃，长按决定高矮） | ✅ 已落地 PlayerConfig |
| 重力 | GravityScale 3（约 29.4 u/s²）+ 下落倍率 1.6 | ✅ 已落地 PlayerConfig |
| 最大下落速度 MaxFallSpeed | 20 u/s | ✅ 已落地 PlayerConfig |
| 玩家 HP | 10 半心点 = 5 颗心（受击硬直 0.6s / 无敌 1s） | ✅ 已落地 CombatConfig |
| 夹钳伤害 / 三段时长 | 2 半心点/段（1 颗心）；Attack01~03 = 0.4167 / 0.3333 / 0.375 s | ✅ 已落地 CombatConfig |
| 连段窗口 ComboWindow | 0.15 s | ✅ 已落地 CombatConfig |
| 第三段击退 ThirdHitKnockback | 6 u/s | ✅ 已落地 CombatConfig |
| 喷壶净化速率 / 净化剂容量 | 2 净化点/s / 100 | ✅ 已落地 EcoConfig（2026-10-22 校准） |
| Enemy01~03 HP | 6 / 8 / 10 半心点（3 / 4 / 5 颗心） | ✅ 已落地 Enemy01~03Config（2026-10-22 校准） |
| 敌人伤害 | 近战 2 半心点（1 心）；Enemy02 远程 1 半心点（半心） | ✅ 已落地 Enemy01~03 / Tower01 配置 |
| Boss01 HP / 阶段阈值 | 80 半心点 = 40 颗心（60%/30% 切阶段；木桩 TTK 15.0s，实战估算 30~40s） | ✅ 已落地 Boss01（2026-10-22 商业基线） |
| Boss01 技能节奏（前摇/命中/后摇，秒） | 近战 A 0.20 / 0.62 / 0.30（总 1.12）；弹幕 B 0.40 / 0.60 / 0.30（总 1.30）；特殊招 0.55 / 0.62 / 0.35（总 1.52）。前摇与命中阶段霸体（只吃伤害、不被硬直打断），后摇解除霸体、可被打断 | ✅ 已落地 Boss01.asset + BossStates（2026-10-22） |
> **单位约定（2026-10-22 起）**：战斗结算一律使用整数定点「半心点」，2 点 = 1 颗心，禁止 float 累加
> （`Tools/balance-kit/BalanceHandbook.md` Step 2.1）；HUD 每颗心支持 空 / 半 / 满 三态。
> "已落地"值即当前资产实际值；战斗实值整理于 `docs/Balance/combat_units.csv`，
> 体检命令：`python Tools/balance-kit/balance_check.py check docs/Balance`。

## 九、变更记录
| 版本 | 日期 | 变化 | 原因 |
|---|---|---|---|
| v0.1 | 2026-10-15 | 初稿，基于 LevelDesign.docx 与美术盘点生成 | 项目启动 |
| v0.2 | 2026-10-17 | 项目化校准：引擎版本、目录结构、里程碑基线 | 仓库结构定型 |
| v0.3 | 2026-10-18 | 回写 M1–M10 全部里程碑的设计变更（详见下表） | 开发流程审计发现变更记录停摆在v0.1，答辩必问项 |
| v0.4 | 2026-10-22 | §8 战斗数值统一为整数定点「半心点」（HP/伤害 ×2，TTK 不变）；跳跃初速 12→13；补录敌人伤害与重生规则 | 按 balance-kit 规范（Step 2.1 禁止 float 结算）统一单位，并修正文档与资产实值不一致 |
| v0.5 | 2026-10-22 | §3.1 喷壶改为只净化不对敌（取消消耗加倍）；§8 Boss HP 60→80；三关节奏重排（L1 解锁条目修正、L3 追击怪改为单只轮换） | 按商业化难度曲线落地：压力集中到 Boss，而不是堆怪；避免第二套伤害来源稀释夹钳 |
| v0.6 | 2026-10-22 | §8 补录 Boss01 三套技能的前摇/命中/后摇；技能 B 与特殊招由瞬发改为到点结算；前摇/命中霸体、后摇可打断 | 原实现 B/特殊招在 Enter 瞬发且状态一帧即退出，动画被切、攻击无预警；补节奏让玩家能读招、并在后摇窗口反击 |
| v0.7 | 2026-10-30 | §4.3 道具 / §5 界面 / §6 音效 / §7 视觉 **全部补「落地状态」列**；§6 音效清单由 9 类扩为 22 个全集；§4.2 关卡引用改为指向 LevelDesign.md | 提交前审计发现文档描述了不存在的内容（Boss BGM、3 种环境音、3 种道具、"污染度影响混音"），而实际落地项（22 个 SFX、图鉴、结算评级）反而没写全。按 §8「每项标注已落地资产」的同一标准统一 |
| v0.8 | 2026-10-30 | §3.1/§3.2 **检查点改为「未实现」**：重生实际为「死亡即本局失败 + 整关重载」，无中途检查点；§9 M6 里程碑同步修正；新增下游文档 `AudioDesign.md`（音频规格层） | 二次核对发现「检查点」从 GDD 到 TDD 到 LevelDesign.md 三份文档全写着有，但代码里没有 `Checkpoint.cs`、场景无 `CP_*` 对象、`SfxId.Checkpoint` 无调用点。文档描述了一套不存在的中途重生机制，答辩演示无法自证 |
| v0.9 | 2026-10-30 | §4.1 补艾娃/老机器人两行 NPC（含"动画未接线"状态）；§4.2 补三关溯源式叙事链与载体分布实测；新增下游文档 `NarrativeDesign.md`；立项**「三颗星球均无人类居民」**为核心设定前提 | 此前无任何叙事文档，三关之间没有一句台词解释顺序因果；实测 L2 叙事载体为 0（0 对话/0 提示牌/0 知识卡），L3 有 0 张知识卡。叙事是毕设"完整性"最容易被追问的一环，且现有 8 段 21 条对白实际只覆盖 L1/L3 |
| v1.0 | 2026-10-30 | 头部改为「文档总索引 + 四条线下游文档」；§4.3 道具更正 **`DropSpawner` 三关均已挂载但配置为空**（原文「未接入任何场景」为误判）；新增 6 份下游文档：`README.md`（索引）、`ArtStyleGuide.md`、`LevelArt.md`、`AnimationVFXSpec.md`、`CodeStandard.md`、`TestPlan.md` | 按岗位补齐文档：此前只有"设计"与"技术方案"两层，美术（主美/关卡美术/动画/特效）、UI 视觉、编码规范、测试验收五个岗位全无文档。补齐过程中实测发现 `DropSpawner` 在三关都有实例（原文误判为未接入），实际是「挂了组件但 `_dropTable`/`_pickupPrefab` 全空」——文档口径从"未接入"改为"空转" |

| v1.1 | 2026-10-30 | 程序线新增 `SystemOverview.md`（系统总览）；`TDD.md` §2.1 目录结构与 §3.1 系统关系图全量重写为实测态；`specs/01~03` 的 SDD 从里程碑骨架重写为落地后版本，**其中 03-Combat 修正了两处已被推翻的设计**（"原地攻击+移动可取消"→边跑边打；第三段→投掷钳子） | 此前**没有系统总览**：TDD §3.1 只有一张 20 行文字 UML 且停留在 M1 规划态（写着不存在的 `GameManager`/`SceneManagerEx`/`HealthSystem`）。`specs/01~03` 是 M1~M3 时期写的，只描述"打算怎么做"，其中 03 的两条核心结论在 10-27/10-28 已被推翻但文档未改，导致 SDD 与实现相反 |

### v0.3 里程碑变更明细（M1–M10）

| 里程碑 | 设计变更 | 变更原因 |
|---|---|---|
| M1 框架 | 新增分层架构：Framework（EventCenter / ObjectPool / SaveSystem）+ Events 结构体事件集 | 支撑系统解耦与跨模块通信，避免各系统直接互相引用 |
| M2 玩家控制器 | 由"直接操作Transform"改为 Rigidbody2D + 状态机（Idle/Run/Jump/Fall/Land/Hurt/Dash/Crouch/Charge） | 需要冲刺、蓄力、受击硬直等复合输入，直接操作无法表达 |
| M3 战斗系统 | 统一伤害数据 DamageInfo（含 KnockbackPower / IsPurify 扩展字段） | 玩家夹钳、净化喷雾、敌人反击三类伤害需共用一套结算与击退契约 |
| M4 敌人 AI | 三种小怪差异化：近战（Enemy01）/ 远程（Enemy02）/ 跳跃（Enemy03）+ 静态塔（Tower01） | 关卡需要不同施压节奏，塔提供不可移动的威胁锚点 |
| M5 生态净化 | 净化从"单一垃圾堆交互"扩展为 IPurifiable 接口族（垃圾堆 / 净化塔 / 河道堵塞点） | 三个关卡各有不同的污染载体，需共用同一套"净化进度"语义 |
| M6 第一关 | 新增目标链系统（LevelObjectiveTracker，顺序目标）+ **检查点（⚠️ 规划，实际未落地：现为死亡即整关重载）** + 结算界面 + 存档 | 毕设需要完整可玩闭环，而非单点玩法验证 |
| M7 UI/对话 | 新增面板栈 UIManager（LIFO）+ 8 个面板 + 艾娃 NPC 对话 + 知识卡片 | 环保主题需要知识传递载体；面板栈解决 Esc 路由与输入冻结 |
| M8 Boss | Boss01 六状态机（含 Enrage / Special）+ 双阶段 + 生命条 | 第三关作为难度顶点需要 Boss 战收尾 |
| M9 第二关 | 新增河道清堵解谜（WaterPuzzleController）+ 动物解救（RescueTarget） | 三关需要玩法类型递进：净化 → 协作救援 → 战斗 |
| M10 打磨 | 18 SFX + 4 BGM 程序化合成；近战 AttackRange 校准；Windows x64 构建 | 音频铺量与手感校准；构建产物用于演示 |

### v0.3 技术规范变更（影响全部美术资产）

| 项目 | 原设计 | 变更后 | 原因 |
|---|---|---|---|
| 贴图 PPU | 100 | 378 | 旧值使角色视觉达7.6 世界单位（应约 2），占屏高 76% |
| 精灵 pivot | Bottom Center（脚本假设） | **Center**（`.meta` `alignment:0`） | Bottom Center 在 Unity 枚举中实为 `alignment:1`=TopLeft，导致 `sprite.bounds` 相对 transform 偏移（0.92, -2.15），碰撞体与视觉整体错位一个身高 |
| 碰撞体约定 | 各脚本手工硬编码世界单位 | **一律由 `sprite.bounds.size × 0.9` 推导，`offset = 0`** | 与 PPU、素材尺寸彻底解耦，PPU 或素材变更不再需要重算 |
| 命中盒 / 发射点 | 相对脚底抬到身体中心 | 相对身体中心，沿 +X 前伸 | Center pivot 下 transform 原点即身体中心 |
| 环境道具摆放 | 混用灰盒 scale 与真素材 | 统一 `position.y = 地面顶面 + 视觉高/2` | 视觉与碰撞体贴合 |
