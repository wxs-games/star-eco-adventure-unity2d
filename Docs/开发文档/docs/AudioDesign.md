# 音频设计文档（Audio Design Document）

> 项目：基于Unity2D的"星际环保"动作冒险游戏设计与实现
> 版本：v1.0（全量实测核对） | 最后更新：2026-10-30
> 撰写岗位：音频策划 / 音效设计（Sound Designer）
> 上游文档：`GDD.md` §6（音效与音乐，规划层）、`TDD.md` §2（AudioManager 技术方案）
> 下游实现：`Assets/Scripts/Framework/GameAudioLibrary.cs`、`AudioManager.cs`、`Tools/audio-pipeline/synthesize.py`

---

## 一、文档定位与权威顺序

| 层级 | 文档 | 回答什么 | 谁写 |
|---|---|---|---|
| 规划层 | `GDD.md` §6 | 需要几首曲、几类音效、情绪方向 | 主策划 |
| **规格层（本文）** | `AudioDesign.md` | **每个音的用途、时长、响度、触发点、混音通道、命名约定** | **音频策划** |
| 实现层 | `GameAudioLibrary.cs` / `AudioManager.cs` | 音效如何绑定、如何播放、通道如何混 | 程序 |
| 素材层 | `Tools/audio-pipeline/synthesize.py` | 每个音的实际合成参数（波形/频率/包络） | 音频策划兼实现 |

**权威顺序：资产与代码 > 本文 > GDD §6。** 三者冲突时以运行时实测为准，并在 §9 记录漂移。

本文所有数据来自 2026-10-30 实测：`ffprobe`/`volumedetect` 读 wav 头与响度、`grep` 扫全部 `SfxId.*` 调用点、读 `synthesize.py` 合成函数。**没有一条是"设计意图"式的空话**——凡是规划了但没做的，一律标 ❌ 并写清缺什么。

---

## 二、声音设计原则

GDD 的三条设计支柱在音频侧的对应：

| 支柱 | 音频落地手法 | 实现位置 |
|---|---|---|
| 1. 环保行为即核心动作 | 净化有一套**独立的、比攻击更"悦耳"的音色族**：琶音上行（C5-E5-G5-C6）。攻击是噪声+金属（打击感），净化是乐音（正反馈）——玩家闭眼能听出"我在做好事" | `sfx_purifycomplete` / `sfx_puzzlesolved` / `sfx_rescue` |
| 2. 难度随污染程度递进 | 三关 BGM 的 **BPM 单调递增**（88 → 64 → 128，L2 为情绪留白）+ 打击乐密度递增（hat 0.06 → 0 → 0.16） | `THEMES` 表 |
| 3. 教育内容融入场景 | 知识卡拾取用两音上行（E5→A5），短到不打断阅读 | `sfx_pickup` |

**一条硬规则：不给说教配乐。** 提示牌/对话无专属 BGM，只有 UI 点击音，避免"上课感"。

---

## 三、系统架构（实测）

```
                    ┌─ BGM 通道（1 条 AudioSource，loop=true）
AudioManager（单例）─┤      淡入淡出 0.8s（FadeDuration），默认音量 0.8
                    └─ SFX 通道（4 条 AudioSource 轮转，PlayOneShot）
                           默认音量 1.0，每条独立 pitch，可叠加不打断
                                   ↑
                    GameAudioLibrary（ScriptableObject）
                    Resources/Audio/GameAudioLibrary.asset
                    Sfx 表 22 项（实测已 100% 填充）+ Bgm 4 项
                                   ↑ 按文件名约定自动回填
                    Assets/Audio/Sfx/<SfxId>.wav
                    Assets/Resources/Audio/Bgm/<Name>.wav
                                   ↑
                    Tools/audio-pipeline/synthesize.py（程序化合成，无外部素材依赖）
```

| 项 | 实测值 | 说明 |
|---|---|---|
| 类 | `AudioManager : MonoSingleton<AudioManager>` | 随场景销毁，每个关卡场景各起一份 |
| BGM 源 | 1 条，`loop = true`，`playOnAwake = false` | 切曲时旧曲淡出→新曲淡入，同曲不重启 |
| 淡入淡出 | `FadeDuration = 0.8f` 秒 | 用 `unscaledDeltaTime`，**暂停时也能淡完** |
| SFX 源 | 4 条轮转（`SfxVoiceCount = 4`） | 轮转而非抢占：连击/连续碎裂同时响时互不打断 |
| 音高抖动 | `PlaySfx(id, pitchVariation)`，`pitch = 1 ± variation` | 避免机械重复，见 §5 表 |
| 默认音量 | BGM 0.8 / SFX 1.0 | 经 `SaveSystem` 存 `settings` 槽，`SettingsPanel` 双滑块可调 |
| 播放模式 | **全部 2D**（`PlayOneShot` + 无 `spatialBlend`） | 屏幕外敌人受击与面前同音量，见 §8 缺口 |
| 缺资产时 | 静默降级：`Library` 为 null 时直接 return，只 `LogWarning` 一次 | 保证演示机缺资源也不报错刷屏 |

**为什么用命名枚举而不是场景里挂 AudioClip 引用**：三关 + 标题 + 预制体里要挂几百处引用，任一美术改文件名就全体断链。改成 `SfxId` 枚举 + 文件名约定 + 编辑器一键回填（`StarEco → Setup → GameAudioLibrary`），新增音效只要丢一个同名 wav。

---

## 四、SFX 全集规格（22 个）

时长与响度为 `ffprobe` / `volumedetect` 实测；"构成"摘自 `synthesize.py` 对应函数。

### 4.1 移动（4）

| ID | 时长 | 平均响度 | 峰值 | 构成 | 触发 |
|---|---|---|---|---|---|
| `Jump` | 0.12s | −8.1 dB | −2.5 | 方波 300→700Hz 上扫，10ms 起音 | 起跳（`PlayerController` ×2：普通跳 + 墙跳共用） |
| `Land` | 0.09s | −13.1 dB | −2.5 | 900Hz 低通噪声，2ms 起音（"闷响"无音调） | 落地进入 `PlayerLandState` |
| `Footstep` | 0.12s | −16.6 dB | −2.5 | **foley 三件套**：96→58Hz 正弦闷击（重量）+ 900–4500Hz 噪声拍地（主体）+ 3.2–10kHz 颗粒（质感） | 地面跑动按速度插值间隔（`_footstepIntervalSlow→Fast`） |
| `GrassRustle` | 0.20s | −13.1 dB | −2.5 | 两簇错开带通噪声（2.3–9kHz 先擦外层叶、1.3–5.6kHz 再陷内层）+ 96→62Hz 闷响 | 与 `Footstep` **互斥**：脚下 `_grassContacts > 0` 时播草丛音 |

> 设计点：`GrassRustle` 比 `Footstep` 响 3.5dB。玩家进入草丛时"声音突然变厚"，是**听觉上的地形提示**，不靠 UI 图标。

### 4.2 战斗（6）

| ID | 时长 | 平均响度 | 峰值 | 构成 | 触发 |
|---|---|---|---|---|---|
| `Attack` | 0.24s | −13.0 dB | −2.5 | 三层错开：空气刀风（900–7000Hz，能量峰在 55ms）+ 金属冷光（3150/4720Hz 快衰）+ 低频带体（190→72Hz，延后 8ms） | 攻击段进入（三段连击每段各一次，pitch ±5%） |
| `Hurt` | 0.25s | −8.1 dB | −2.5 | 方波 200Hz 带 22Hz 颤音，5ms 起音（"惨叫"感来自颤音而非音高） | 玩家受击进入 `PlayerHurtState` |
| `Death` | 0.60s | −8.1 dB | −2.5 | 方波 400→60Hz 下扫（"泄气"曲线） | 玩家死亡 |
| `EnemyHit` | 0.20s | −14.6 dB | −2.5 | 高频脆响（1.4–6.5kHz）+ 中频闷击（260–1500Hz）+ 低频冲击（145→52Hz），三层同时起音，`soft_clip` drive 1.7 做失真 | 敌人受击（基类 `EnemyControllerBase`，pitch ±5%） |
| `BossHit` | 0.15s | −14.1 dB | −2.5 | 方波 150→90Hz + 500Hz 低通噪声 | Boss 受击（覆写 `HitSfxId`，**与普通怪区分**） |
| `HeavyRelease` | 0.50s | −13.0 dB | −2.5 | 300Hz 低通噪声 + 方波 130→45Hz | 蓄力攻击释放 |

### 4.3 净化（4）

| ID | 时长 | 平均响度 | 峰值 | 构成 | 触发 |
|---|---|---|---|---|---|
| `SprayStart` | 0.22s | −14.9 dB | −2.5 | 1200Hz 高通噪声，`t^0.6` 渐强（"起喷"的推力感） | 开始喷雾 |
| `PurifyComplete` | 0.40s | −7.6 dB | −2.5 | 琶音上行 **C5-E5-G5-C6**（MIDI 72/76/79/84），每音 0.1s | 任一污染源净化完成（`PuritySystem` 统一广播）+ Boss 阶段转换 + Boss 门破 |
| `BreakPlant` | 0.42s | −16.0 dB | −2.5 | 5 段纤维连续崩断（频段逐段上移 700→1180Hz）+ 叶片摩擦（1.8–9kHz）+ 树液下滑（260→90Hz） | 打碎植物类可破坏物（pitch ±7%） |
| `BreakStone` | 0.50s | −15.9 dB | −2.5 | 低频崩裂（120→42Hz）+ 崩裂噪声（900–5200Hz）+ **9 颗随机石屑**（1.6–8kHz 短点） | 打碎石类可破坏物 / **碎裂平台塌陷** / Boss 门破 |

> ⚠️ `BreakStone` 是复用最多的音效（3 个调用点）。碎裂平台用 ±12% 变调、Boss 门用 ±15%，让同一素材在三种场景听感不同——**这是低成本复用素材的标准手法，不是偷懒**。

### 4.4 交互（4）

| ID | 时长 | 平均响度 | 峰值 | 构成 | 触发 |
|---|---|---|---|---|---|
| `Pickup` | 0.15s | −7.8 dB | −2.5 | 方波 **E5 → A5**（MIDI 76→81），两音上行 | 拾取净化剂 / 知识卡（`PurifyPickup`） |
| `Rescue` | 0.32s | −9.2 dB | −2.5 | 方波 520→780Hz 上扫 + 780Hz 带 18Hz 颤音的尾巴 | 解救被困目标（`RescueTarget`） |
| `PuzzleSolved` | 0.83s | −9.4 dB | −2.5 | 琶音 C5-E5-G5-C6（每音 0.12s）+ C5 长音 0.35s 收尾 | 拉杆机关 / 水流解谜完成 |
| `UIClick` | 0.09s | −7.5 dB | −2.5 | 方波 880Hz 40ms + 660Hz 50ms（"嗒-嗒"） | 全局按钮点击（`UIUtility` 统一挂） |
| `Checkpoint` | 0.23s | −8.1 dB | −2.5 | 方波 **D5 → G5**（MIDI 74→79） | ❌ **无任何调用点**，见 §8 |

### 4.5 环境物理（2）

| ID | 时长 | 平均响度 | 峰值 | 构成 | 触发 |
|---|---|---|---|---|---|
| `Splash` | 0.40s | −17.4 dB | −2.5 | 350–3200Hz 带通噪声（"入水"的中空感） | 溺水域触发（`DrownHazard`） |
| `GrassRustle` | 0.20s | −13.1 dB | −2.5 | 见 §4.1 | 见 §4.1 |

> `Splash` 是全集最轻的一个（−17.4dB）。溺水域是致死区，音效轻是**故意的**：致死提示应由视觉+即时反馈承担，靠音量吓人只会让玩家以为是 bug。

### 4.6 结算（2）

| ID | 时长 | 平均响度 | 峰值 | 构成 | 触发 |
|---|---|---|---|---|---|
| `LevelComplete` | 1.30s | −7.4 dB | −2.5 | 五音上行 **C5-E5-G5-C6-E6**（每音 0.18s）+ 0.4s 尾音。全集最"亮"的一个 | 过关（`LevelManager` → `Completed`） |
| `LevelFail` | 1.16s | −12.9 dB | −2.5 | 三角波下行 **G4-E4-C4**（每音 0.22s）+ C3 低音 0.5s | 失败（`LevelManager` → `Failed`） |

> 过关与失败是**同一套音乐语汇的两极**：过关用方波琶音上行（明亮），失败用三角波下行（暗、软）。三角波比方波"软"，听感上不刺人——失败已经是负反馈，不该再扎玩家一下。

---

## 五、触发映射表（26 个调用点）

| 场景 | SfxId | 变调幅度 | 代码位置 |
|---|---|---|---|
| 普通起跳 | `Jump` | 0 | `PlayerController.cs:354` |
| 墙跳 | `Jump` | 0 | `PlayerController.cs:603` |
| 落地 | `Land` | 0 | `PlayerStates.cs:223` |
| 玩家受击 | `Hurt` | 0 | `PlayerStates.cs:290` |
| 玩家死亡 | `Death` | 0 | `PlayerStates.cs:348` |
| 跑动脚步（地面） | `Footstep` | ±7% | `PlayerController.cs:821` |
| 跑动脚步（草丛） | `GrassRustle` | ±12% | `PlayerController.cs:817` |
| 挥击（每段） | `Attack` | ±5% | `PlayerAttackState.cs:42` |
| 敌人受击 | `EnemyHit` | ±5% | `EnemyControllerBase.cs:331` |
| 防御塔受击 | `EnemyHit` | ±5% | `Tower01Controller.cs:57` |
| Boss 受击 | `BossHit` | ±5% | `BossController.cs:296`（覆写 `HitSfxId`） |
| Boss 阶段转换 | `PurifyComplete` | 0 | `BossController.cs:407` |
| Boss 门破 | `PurifyComplete` + `BreakStone` | 0 / ±15% | `BossGate.cs:96` / `:270` |
| 开始喷雾 | `SprayStart` | 0 | `SprayController.cs:229` |
| 蓄力释放 | `HeavyRelease` | 0 | `SprayController.cs:290` |
| 污染源净化完成 | `PurifyComplete` | 0 | `PuritySystem.cs:66`（统一入口） |
| 拾取净化剂/知识卡 | `Pickup` | 0 | `PurifyPickup.cs:58` |
| 可破坏物（植物） | `BreakPlant` | ±7% | `BreakableProp.cs:63` |
| 可破坏物（石头） | `BreakStone` | ±7% | `BreakableProp.cs:63` |
| 碎裂平台塌陷 | `BreakStone` | ±12% | `CrumblePlatform.cs:255` |
| 解救目标 | `Rescue` | 0 | `RescueTarget.cs:163` |
| 拉杆机关 | `PuzzleSolved` | 0 | `LeverSwitch.cs:156` |
| 水流解谜 | `PuzzleSolved` | 0 | `WaterPuzzleController.cs:183` |
| 溺水域 | `Splash` | 0 | `DrownHazard.cs:24` |
| 过关 | `LevelComplete` | 0 | `LevelManager.cs` → `CommitFinish` |
| 失败 | `LevelFail` | 0 | `LevelManager.cs` → `CommitFinish` |

> **时序注（2026-10-30 起）**：`LevelComplete` / `LevelFail` 都在 `CommitFinish` 里播。第三关通关时会先播落幕对白 `ava_ending`，`CommitFinish` 被协程推迟到对白结束——因此**胜利音乐在结局台词之后才响**。这是有意为之：台词和音乐同时砸上来会互相盖住。
| UI 点击 | `UIClick` | 0 | `UIUtility.cs:320`（所有按钮统一） |

**未接线（1）**：`Checkpoint` —— 见 §8。

---

## 六、BGM 规格（4 首）

全部由 `synthesize.py` 程序化合成，**30.000s 无缝循环**（每首音符量化到 8 分音符网格，BPM 取 8 的倍数保证整小节收尾，接缝处尾音绕回开头）。44.1kHz 立体声 PCM。

| 曲目 | 场景 | BPM | 和弦进行 | 配器 | 平均响度 | 情绪定位 |
|---|---|---|---|---|---|---|
| `Title` | 主菜单 | 96 | C–G–Am–F | 三角波贝斯 + 方波 8 分琶音 + 底鼓/军鼓/hi-hat | −18.3 dB | 明亮、有希望，循环 30s 不腻 |
| `Level01` | 污染星球 | 88 | Am–F–C–G | 同上，hat 减弱到 0.06 | −18.1 dB | 小调起步但有推进感——教学关不该压人 |
| `Level02` | 生态森林 | 64 | Am–F–C–G / Am–F–Dm–E | **抒情编制**：钢琴/竖琴式主旋律 + 弦乐铺底 + 低音提琴，**无鼓点** | −15.6 dB | 空洞骑士式留白。森林是"还没坏透"的地方，节奏慢下来让玩家看风景 |
| `Level03` | 能源工厂 | 128 | Em–C–G–Dm | 同上 + hat 0.16，整体低通放宽到 5200Hz（更亮更硬） | −18.0 dB | 全集最快最硬，Boss 战在此曲之上叠 SFX |

**三关 BPM 曲线：88 → 64 → 128。** 不是单调递增，而是"紧张 → 舒缓 → 最紧张"的三段式。第二关放慢是刻意设计：如果一路加速，第三关的 128 就没有对比，Boss 战的压迫感会被前两关提前消耗掉。

**缺失**：Boss 专属 BGM（GDD §6 规划 5 首，实到 4 首）。Boss 战当前与普通 Level03 共用一曲，靠 `BossHit`/`PurifyComplete`/镜头震动营造紧张——**这是本音频设计最大的缺口**，见 §8。

---

## 七、混音与响度

### 7.1 通道与增益链

```
素材（峰值归一 −2.5dB）
  → SFX: × SfxVolume(默认 1.0)  → 4 源轮转 PlayOneShot → 主输出
  → BGM: × 淡入淡出 × BgmVolume(默认 0.8) → 主输出
```

**没有 AudioMixer 分组**（无 Master/SFX/BGM 三条总线，无 EQ/压缩/侧链）。当前"混音"完全靠素材本身的响度差 + 两个音量滑块。

### 7.2 响度审计（2026-10-30 实测）

| 分组 | 平均响度区间 | 峰值 |
|---|---|---|
| SFX 全部 22 个 | −17.4 ~ −7.4 dB | 统一 −2.5 dB（无削波） |
| BGM 全部 4 首 | −18.3 ~ −15.6 dB | −2.9 ~ −3.1 dB |

**结论：**

1. **峰值统一归一，零削波** —— 22+4 个文件峰值全在 −2.5/−3.0 附近，说明 `norm(peak=0.89)` 在管线里生效了。这是最容易翻车的一环（多个音同时播时叠加溢出），已经守住。
2. **SFX 内部动态范围 10dB，是合理的** —— 最响的是 UI/奖励类（`LevelComplete` −7.4、`UIClick` −7.5、`PurifyComplete` −7.6、`Pickup` −7.8），最轻的是环境类（`Splash` −17.4、`Footstep` −16.6、`BreakPlant` −16.0）。"重要的事听得见、背景的事不抢戏"，符合听觉优先级。
3. **SFX 比 BGM 响约 5–8dB** —— SFX 均值约 −13dB，BGM 乘 0.8 后有效值约 −20.9dB。差值 8dB 保证动作反馈清晰压过音乐，方向正确。
4. **`Checkpoint`（−8.1dB）偏响** —— 与 `UIClick`(−7.5)/`Hurt`(−8.1) 同档。若将来接上检查点，它会在安静段落突然盖过 BGM，**接功能时应先降到 −13dB 左右**。

### 7.3 未做的混音功能

| 功能 | 状态 | 说明 |
|---|---|---|
| BGM ducking（对话/过场时压低音乐） | ❌ | 对话只有 UI 点击音；若后续加语音，必须补侧链或脚本压低 BGM |
| 污染度驱动 BGM 滤波 | ❌ | GDD §6 规划"BGM 随污染度改变混音亮度"，实际只有 `AtmosphereController` 改摄像机背景色，音频侧无参数 |
| 3D 空间衰减 / 声像 | ❌ | 全部 2D。屏幕外敌人受击与面前同音量——横版游戏可接受，但 Boss 战多目标时会糊 |
| Master 总线限制器 | ❌ | 依赖素材峰值归一，没有运行时限幅兜底 |

---

## 八、缺口与待办

### P0（功能缺口，影响答辩可信度）

| # | 缺口 | 现状 | 补做成本 |
|---|---|---|---|
| 1 | **`Checkpoint` 音效无调用点** | 项目**没有检查点系统**——无 `Checkpoint.cs`、场景无 `CP_*` 对象、`LevelManager` 只有"死亡即整关重载"。该 wav 是孤儿资产 | 要么补检查点系统（脚本+场景+存档锚点，约 1 天），要么把该条目降级为"预留"并在 GDD 标注 |
| 2 | **Boss BGM 缺失** | 规划 5 首实到 4 首，Boss 战与 Level03 共用 | `synthesize.py` 加一段 `BossTheme`（建议 140BPM、减和弦、加入不和谐小二度），1 小时可出 |
| 3 | **环境音完全缺失** | 规划"工厂低鸣/风声鸟鸣/机械运转"三种，实际 0 资产、`SfxId` 无 Ambience 类、`AudioManager` 无环境音通道 | 需先加第 3 条通道（循环 + 交叉淡入），再加 3 个循环素材，约 1 天 |

### P1（质量缺口）

| # | 缺口 | 说明 |
|---|---|---|
| 4 | 无 AudioMixer 分组 | 建议建 Master/SFX/BGM/Ambience 四组，BGM 组挂 `AudioDuckVolume` 供对话侧链使用 |
| 5 | 污染度不驱动混音 | 最省事的落地：按污染度给 BGM 的 `AudioLowPassFilter.cutoffFrequency` 做 12000→3500Hz 插值，与视觉色调同源（订阅同一个 `PurityChangedEvent`） |
| 6 | `Checkpoint` 响度待调 | 见 §7.2 结论 4 |

### P2（锦上添花）

| # | 项 | 说明 |
|---|---|---|
| 7 | Boss 战音乐分段 | 三阶段切 stem（同一曲分层静音/解静），而不是换曲 |
| 8 | 脚步随机化 | 目前 `Footstep` 单素材 + ±7% 变调，连续跑 10s 仍能听出重复；建议 3 个变体轮换 |
| 9 | 音频素材替换为真实录音 | 当前全程序化合成（毕设约束：无版权素材、无外部依赖），商业项目应换 foley 实录音 |

---

## 九、命名约定与扩展流程

### 9.1 命名

| 类别 | 规则 | 示例 |
|---|---|---|
| SFX 枚举 | PascalCase，语义化动词/名词 | `BreakPlant` 而非 `PlantBreak01` |
| SFX 文件 | `Assets/Audio/Sfx/<SfxId>.wav`，**文件名必须与枚举名逐字符相同** | `PurifyComplete.wav` |
| BGM 文件 | `Assets/Resources/Audio/Bgm/<Name>.wav`，Name 与 `GameAudioLibrary` 字段对应 | `Level02.wav` → `Level02Bgm` |
| 格式 | SFX：44.1kHz 单声道 PCM；BGM：44.1kHz 立体声 PCM | 由 `write_wav()` 统一 |

### 9.2 新增一个音效（4 步，无代码改动）

1. 在 `synthesize.py` 里写 `def sfx_xxx()`（返回 float64 单声道数组），注册进 `SFX` 字典
2. `python Tools/audio-pipeline/synthesize.py` → 生成 `Assets/Audio/Sfx/Xxx.wav`
3. 在 `GameAudioLibrary.cs` 的 `enum SfxId` 末尾加 `Xxx`
4. Unity 菜单 `StarEco → Setup → GameAudioLibrary` 一键回填

播放处写 `AudioManager.Instance?.PlaySfx(SfxId.Xxx)`。**缺音效不会崩**——`Library.GetSfx` 返回 null 时 `PlaySfx` 直接 return。

### 9.3 新增一首 BGM（5 步）

1. `synthesize.py` 的 `THEMES` 加一项（bpm/prog/scale/hat，或 `style="lyrical"` 走抒情编制）
2. 生成到 `Assets/Resources/Audio/Bgm/<Name>.wav`
3. `GameAudioLibrary.cs` 加 `public AudioClip <Name>Bgm;`
4. `GetBgm()` 的 switch 加一个 case
5. 菜单 `StarEco → Setup → GameAudioLibrary` 回填

**约束：BPM 必须是 8 的倍数**，否则 `render_bgm` 的 `assert abs(total - BGM_TOTAL) < 1e-6` 会失败（30s 无法整小节收尾，循环接缝会有咔哒声）。

---

## 十、变更记录

| 版本 | 日期 | 变化 | 原因 |
|---|---|---|---|
| v1.0 | 2026-10-30 | 首版。22 个 SFX + 4 首 BGM 全量实测建档；26 个调用点逐个定位；响度审计；缺口清单 | 此前音频只有 GDD §6 的一行规划表，无触发映射、无响度依据、无扩展流程。音频是毕设三关唯一完全程序化合成的模块，答辩必问"你怎么保证音效一致/如何新增"，需要有据可查的规格文档 |
