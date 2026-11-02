# 设计文档：10-Polish（M10 打磨）

> 对应 `requirements.md` R1~R4 / `tasks.md`。

## 1. 总体方案

三个相互独立的工作流，按依赖排序：

```
音频素材合成（Python，仓库外工具链） → Unity 音频库代码 + 接线 → 编译/回归
数值校准（审查 Config 资产） ──────────────────────────────┘
Build 验证（Unity 独立构建，只读） ────────────────────────┘
```

## 2. 音频素材：程序化合成（R1）

### 2.1 为什么合成而不是下载/云端生成

- Unity MCP 的 fal.ai 音频提供方**未配置 key**（`list_providers` 返回 `configured:false`），云端生成不可用；
- 联网下载免费素材有许可证杂乱、不可复现、链接失效三重风险；
- 程序化合成（numpy → WAV）：零外部依赖、完全可复现（固定 seed）、毕设答辩可现场演示"音频也是程序生成的"这一技术点，与项目已有的 `Tools/art-pipeline` 程序化美术管线气质一致。

**决策点 D1（自动模式采用）**：素材来源 = numpy 程序化合成。理由：社区中"无版权顾虑 + 可复现"的最稳做法，且与既有美术管线同构。

### 2.2 素材清单与合成参数

- 采样率 44100 Hz、16-bit PCM 单声道（SFX）/ 立体声（BGM），WAV 容器（Unity 原生导入）。
- 波形库：方波（lead/bass，芯片风）、三角波（柔和水声/氛围）、白噪声（打击/喷雾/水面），包络 AD/AR 统一函数。
- **SFX 共 22 个**（v1.0 写 18，后续补 4 个），时长 0.1~1.2s：

| SfxId | 合成手法 | 听感目标 |
|---|---|---|
| UIClick | 方波 880→660Hz 0.06s | 清脆点击 |
| Jump | 方波 300→700Hz 上滑 0.12s | 起跳"咻" |
| Land | 噪声 burst + 低通包络 0.08s | 落地"咚" |
| Attack | 噪声+方波 500→200Hz 下扫 0.15s | 挥击 |
| Hurt | 方波 200→120Hz 颤音 0.25s | 受击"哎哟" |
| Death | 方波 400→60Hz 长下扫 0.6s | 倒地 |
| EnemyHit | 方波 600→300Hz 0.08s | 小怪受击 |
| BossHit | 方波 150→90Hz + 噪声 0.15s | 沉闷命中 |
| Pickup | 方波 660→990Hz 双音 0.15s | 拾取"叮" |
| PurifyComplete | 三音上行琶音（C-E-G）0.4s | 净化完成 |
| SprayStart | 噪声高通 0.2s 渐入 | 喷雾启动 |
| HeavyRelease | 噪声+低频下扫 0.5s | 重喷释放 |
| Rescue | 方波 520→780Hz + 颤音 0.3s | 解救"啾" |
| PuzzleSolved | 四音琶音 + 长尾 0.8s | 解谜成功 |
| **Checkpoint** | 双音上行 0.25s | 检查点 ⚠️ **零调用点**（检查点系统已移除，素材与枚举保留） |
| Splash | 噪声带通 + 衰减 0.4s | 入水 |
| LevelComplete | 五音胜利琶音 1.2s | 通关 |
| LevelFail | 三音下行 + 低音 0.9s | 失败 |
| **BreakPlant** | 噪声 burst + 绿植折断质感 | 打断草木（第二关打碎植被） |
| **BreakStone** | 噪声 + 低频碎裂 + 短尾 | 碎石（第三关碎裂平台、封锁墙） |
| **Footstep** | 极短噪声脉冲，行走循环触发 | 脚步（pitch 随机 ±7%） |
| **GrassRustle** | 带通噪声，草丛穿行 | 草叶窸窣（pitch 随机 ±12%） |

> 逐个素材的实测时长/峰值响度/音色构成、26 个调用点与 pitch 随机范围，见 `docs/AudioDesign.md`（那是音频岗位的正式规格文档，本节只是合成方案）。

- **BGM 4 首**（`Title` / `Level01` / `Level02` / `Level03`），**全部 30.000s 无缝循环**（首尾相位对齐、采样级对齐），芯片风：方波 lead + 三角波 bass + 噪声 hi-hat。实测 BPM 如下（v1.0 的 100/84/108/128 是设计意图，实际合成值已按听感调过）：

| 曲目 | 调性 | BPM（实测） | 气质 |
|---|---|---|---|
| `Title` | C 大调 | **88** | 温暖、留白，配标题界面 |
| `Level01` | A 小调 | **64** | 压抑、缓慢，污染星球 |
| `Level02` | G 大调 | **128** | 明亮、流动，生态森林；**无鼓点**（避免与水流音冲突） |
| `Level03` | E 小调 | **96** | 驱动感、机械感，能源工厂 |

### 2.3 目录落位

```
Tools/audio-pipeline/synthesize.py     # 合成脚本（随仓库走，可复现）
Assets/Audio/Sfx/*.wav                 # SFX 素材（22 个）
Assets/Resources/Audio/Bgm/*.wav       # BGM 素材（4 个，需在 Resources 下供运行时按名加载）
Assets/Resources/Audio/GameAudioLibrary.asset   # 音频库资产（SFX 表 + 4 BGM 引用）
```

> BGM 放 Resources 的原因：MainMenuPanel/LevelManager 运行时按关卡索引取 BGM，走资产引用需要在每个场景/配置里接线；按名加载一处资产全搞定，缺失时静默降级（R1.7）。
> SFX 也统一走 GameAudioLibrary 资产引用，AudioManager 懒加载该资产。

## 3. Unity 音频架构（R1）

### 3.1 GameAudioLibrary（新增，Framework）

```csharp
public enum SfxId { UIClick, Jump, Land, Attack, Hurt, Death, EnemyHit, BossHit,
                    Pickup, PurifyComplete, SprayStart, HeavyRelease, Rescue,
                    PuzzleSolved, Checkpoint, Splash, LevelComplete, LevelFail,
                    BreakPlant, BreakStone, Footstep, GrassRustle }   // 22 个

[CreateAssetMenu] public class GameAudioLibrary : ScriptableObject {
    public List<SfxEntry> Sfx;              // SfxId → AudioClip
    public AudioClip TitleBgm, Level01Bgm, Level02Bgm, Level03Bgm;
    public AudioClip GetSfx(SfxId id);
    public AudioClip GetBgm(int levelIndex); // 1/2/3；0 或越界 → TitleBgm
}
```

### 3.2 AudioManager 增量（M1 既有类，只加不改）

- `PlaySfx(SfxId)`：经懒加载的 library 解析 clip → 走既有 `PlaySfx(AudioClip)`；
- `PlayTitleBgm()` / `PlayBgmForLevel(int levelIndex)`：解析 BGM → 走既有 `PlayBgm(AudioClip)`；
- 懒加载：`Resources.Load<GameAudioLibrary>("Audio/GameAudioLibrary")`，失败缓存 null 并不再重试（无错误刷屏，R1.7）；
- 既有 `PlaySfx(AudioClip)`/`PlayBgm(AudioClip)`/音量 API 全部保留（M1 自测与设置面板依赖）。

### 3.3 调用点接线（最小增量，全部替换现传 null 的分支）

| 调用点 | 现况 | 改为 |
|---|---|---|
| UIUtility.PlayClickSfx | `PlaySfx(null)` | `PlaySfx(SfxId.UIClick)` |
| MainMenuPanel.OnContinue 等 | `PlayBgm(null)` | `PlayTitleBgm()` |
| PlayerController 跳/落/受击/死 | 无 | `PlaySfx(Jump/Land/Hurt/Death)` |
| PlayerCombat 攻击段 | 无 | `PlaySfx(Attack)` |
| SprayController 启/放 | 序列化字段（未配） | `PlaySfx(SprayStart/HeavyRelease)` |
| GarbagePile/TowerPurifiable/WaterBlockage 净化完成 | 无 | `PlaySfx(PurifyComplete)` |
| PurifyPickup 收集 | 无 | `PlaySfx(Pickup)` |
| RescueTarget 解救 | 无 | `PlaySfx(Rescue)` |
| WaterPuzzleController 解谜完成 | 无 | `PlaySfx(PuzzleSolved)` |
| ~~Checkpoint 激活~~ | — | **不接**：检查点系统已整体移除，`SfxId.Checkpoint` 与素材保留但无调用点（AudioDesign §4 已登记） |
| LevelManager 完成/失败 | 无 | `PlaySfx(LevelComplete/LevelFail)` |
| EnemyControllerBase 受击/死亡 | 无 | `PlaySfx(EnemyHit)`；Boss 受击 `BossHit` |
| DrownHazard 致死 | 无 | `PlaySfx(Splash)` |

> BGM 切换挂点：LevelManager 关卡初始化时按 `config.LevelIndex` 调 `PlayBgmForLevel`；MainMenuPanel 打开时 `PlayTitleBgm`。

### 3.4 AudioLibrarySetup（新增，Editor）

菜单 `StarEco/Setup/GameAudioLibrary`：生成/更新 `Assets/Resources/Audio/GameAudioLibrary.asset`，按文件名约定自动回填（`Assets/Audio/Sfx/Jump.wav` → `SfxId.Jump`；`Title.wav` → `TitleBgm`…），可重复执行不重复添加。

## 4. 数值校准（R2）

- 校准对象：PlayerConfig / CombatConfig / EcoConfig / EnemyConfig×3 / BossConfig 全部 public 字段；
- 输出：`specs/10-Polish/calibration.md`（字段 | 现值 | 结论 | 理由，调整项含前后值）；
- 调整门槛：仅"明显破坏体验"才改（R2.2），其余保持不动——数值未经过真实玩家测试前，大改是赌博；
- 落盘方式：Editor 脚本或 execute_code 直接改 Config 资产字段（数据驱动，R2.3）。

## 5. Build 验证（R3）

- 目标：Windows Standalone (x64)，输出 `Builds/Windows/StarEco.exe`；
- `Builds/` 加入 `.gitignore`（产物不进库）；
- 验证：退出码 0 + exe 存在 + 构建日志无 error；
- 构建前后 `git status` 对比，确认无场景/资产被改（R3.3）。

## 6. 数据流

### 6.1 一次命名音效播放（`AudioManager.PlaySfx(SfxId.X)`）

```text
调用方（例：PlayerController 落地）
 → AudioManager.PlaySfx(SfxId.Land)
     ├─ _library == null? → LoadLibrary()：Resources.Load<GameAudioLibrary>("Audio/GameAudioLibrary")
     │     失败 → 缓存 null 并置 _libraryLoadFailed = true，此后不再重试（防日志刷屏，R1.7）
     ├─ clip = _library.GetSfx(SfxId.Land)
     │     null（素材缺失/未回填）→ 静默 return（不抛异常，游戏照常跑）
     └─ PlaySfx(clip)                        ← M1 既有实现：AudioSource.PlayOneShot
           pitch = 1 + Random(-0.05, +0.05)（Attack）；Footstep ±7%；GrassRustle ±12%；BreakStone ±12%
 → 结束（无返回值，调用方不感知失败）
```

**设计要点：音频永远不能成为失败点**。素材缺失、库资产缺失、音源被打满——全部静默降级。这条在 M10 定下后，全项目 22 个调用点没有一处需要 `try/catch` 或判空。

### 6.2 一次 BGM 切换（Title → L1 → L2 → L3）

```text
Title 场景：MainMenuPanel.Start → AudioManager.PlayTitleBgm()
   → GetBgm(0) → TitleBgm → PlayBgm(clip)（loop=true，音量随设置面板的 BgmVolume 缩放）

点「开始游戏」→ SceneManager.LoadScene(1)
 → LevelManager.Start → StartLevel()
     → AudioManager.PlayBgmForLevel(_config.LevelIndex)
           GetBgm(1) → Level01Bgm
           **不 Stop 上一首，直接交叉切换**：PlayBgm 内部先停旧源再播新源（M1 语义）
 → 通关 → 下一关 → 同上，Index 2 → Level02Bgm；Index 3 → Level03Bgm
 → 返回主菜单 → MainMenuPanel.OnOpen → PlayTitleBgm()
```

- `GetBgm(levelIndex)`：`0` 或越界 → 回退 `TitleBgm`。所以即使 `LevelIndex` 配错也不会静音（只会放错曲子，且日志可见）。
- 4 首 BGM 都是严格 30.000s、首尾相位对齐的无缝循环，`AudioSource.loop = true` 时听不出接缝（`AudioDesign.md` §5 有采样级说明）。

### 6.3 一次构建（Windows x64）

```text
git status（记录基线，确认工作区干净）
 → Unity: BuildPipeline.BuildPlayer(BuildTarget.StandaloneWindows64)
     scenes 顺序取自 EditorBuildSettings：Title(0) / Level01(1) / Level02(2) / Level03(3)
     输出 Builds/Windows/StarEco.exe
 → 判据：退出码 0 + exe 存在 + 构建日志无 error
 → git status 二次对比：确认 Build 过程没有改动任何场景/资产（R3.3）
     ⚠️ 唯一允许的变化：Library/ 与 Builds/（均已 gitignore）
```

## 7. 风险

| # | 风险 | 概率 | 影响 | 现状与对策 |
|---|---|---|---|---|
| P1 | 素材与 `SfxId` 枚举不同名 → 库资产回填漏项 | 中 | 低（静默无声） | `AudioLibrarySetup` 按文件名约定回填；漏项表现为"某个动作没声音"，`AudioDesign.md` §4 的调用点表可反查 |
| P2 | `AudioManager` 懒加载失败后永久不重试 | 低 | 中（全程静音） | 只会在"库资产不存在"时发生；`AudioLibrarySetup` 生成资产后需重启 Play 才恢复。已在 CodeStandard 记录 |
| P3 | 22 个 SFX 的峰值响度不统一 | 中 | 中（某几个音效过响） | 已做响度审计：全部峰值 −2.5dB、零削波；唯一例外 `Checkpoint` −8.1dB（但它零调用，无实际影响）。见 `AudioDesign.md` §6 |
| P4 | `Checkpoint.wav` 与 `SfxId.Checkpoint` 是死资源 | 已存在 | 低 | 保留：删除要同时改枚举（破坏已有库资产的索引），成本大于收益。文档已标注 |
| P5 | Build 顺序被误改导致 `LevelIndex != buildIndex` | 中 | 高（进错关） | 不变式写在 CodeStandard 与 06-Level01 §5 L6；`Level03Setup` 每次运行都会重排 Build Settings |
| P6 | 数值校准只做了"明显破坏项"，未做玩家测试 | 高（客观事实） | 中 | `calibration.md` 记录全部字段与结论；答辩时明确"未经真实玩家验证"是诚实项 |

## 8. 与 requirements 的偏差说明

| 编号 | requirements 原文口径 | 实际实现 | 原因 |
|---|---|---|---|
| D1 | SFX 18 个 | **22 个**（+`BreakPlant`/`BreakStone`/`Footstep`/`GrassRustle`） | 第二关打碎植被、第三关碎石平台、脚步与草丛需要独立音色；v1.0 的表是立项时的估计 |
| D2 | BGM BPM 100/84/108/128 | 实测 **88/64/128/96** | 合成后按听感回调：L1 需要更慢更压抑、L2 需要更活泼、L3 降到 96 以免与 Boss 战弹幕节奏打架 |
| D3 | L02 BGM 与其它同构 | L02 **无鼓点** | 第二关核心玩法是水流，hi-hat 与水流噪声频段重叠，听感浑浊 |
| D4 | `SfxId.Checkpoint` 接"检查点激活" | **零调用点**（检查点系统整体移除） | 素材与枚举保留，见 §7 P4 |
| D5 | Build 目标 Windows x64 | 已实现，产物不入库 | 无偏差，仅记录 `Builds/` 已加入 `.gitignore` |



## 9. 自动模式决策点清单（复盘用）

| # | 决策点 | 采用方案 | 为什么是主流 |
|---|---|---|---|
| D1 | 音频素材来源 | numpy 程序化合成 WAV | 无 key 可用/下载有许可证与失效风险；合成可复现、零依赖，与既有 art-pipeline 同构 |
| D2 | SFX 分发架构 | ScriptableObject 音频库 + 枚举键 + AudioManager 命名 API | Unity 社区音频管理主流范式（SO 数据驱动 + 静态快捷入口），避免场景到处挂引用 |
| D3 | BGM 分发 | 单一资产内 4 个引用 + 按 LevelIndex 解析 | 与 SFX 同架构，关卡配置零音频字段，新增关卡只加一个 clip 字段 |
| D4 | 校准策略 | 只记录+只修明显破坏项 | 未做玩家测试前大改数值是赌博；先建立可审计基线 |
| D5 | Build 目标 | Windows x64 独立构建到 Builds/ | 毕设交付/答辩演示的最常用目标平台 |

## 10. 文件清单

新增：
- `Tools/audio-pipeline/synthesize.py`
- `Assets/Scripts/Framework/GameAudioLibrary.cs`
- `Assets/Scripts/Editor/AudioLibrarySetup.cs`
- `Assets/Audio/Sfx/*.wav`（22）
- `Assets/Resources/Audio/Bgm/*.wav`（4）
- `Assets/Resources/Audio/GameAudioLibrary.asset`
- `specs/10-Polish/` 三件套 + `calibration.md`

修改（最小增量）：
- `Assets/Scripts/Framework/AudioManager.cs`（+3 API）
- `Assets/Scripts/UI/UIUtility.cs`、`UI/MainMenuPanel.cs`
- `Assets/Scripts/Player/PlayerController.cs`、`Combat/PlayerCombat.cs`、`Eco/SprayController.cs`
- `Assets/Scripts/Eco/GarbagePile.cs`、`Eco/TowerPurifiable.cs`、`Eco/WaterBlockage.cs`、`Eco/PurifyPickup.cs`
- `Assets/Scripts/Level/RescueTarget.cs`、`Level/WaterPuzzleController.cs`、`Level/LevelManager.cs`
- `Assets/Scripts/EnemyAI/EnemyControllerBase.cs`（+Boss 受击分支）
- `Assets/Scripts/Level/DrownHazard.cs`
- `.gitignore`（+Builds/）
