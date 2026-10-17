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
- SFX 共 18 个，时长 0.1~1.2s：

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
| Checkpoint | 双音上行 0.25s | 检查点 |
| Splash | 噪声带通 + 衰减 0.4s | 入水 |
| LevelComplete | 五音胜利琶音 1.2s | 通关 |
| LevelFail | 三音下行 + 低音 0.9s | 失败 |

- BGM 4 首，各 30s 无缝循环（首尾相位对齐），芯片风：方波 lead + 三角波 bass + 噪声 hi-hat；
  - TitleBgm：C 大调、中速（100 BPM）、温暖；
  - Level01Bgm：A 小调、慢速（84 BPM）、压抑（污染星球）；
  - Level02Bgm：G 大调、中速（108 BPM）、明亮（生态森林）；
  - Level03Bgm：E 小调、快速（128 BPM）、驱动感（能源工厂）。

### 2.3 目录落位

```
Tools/audio-pipeline/synthesize.py     # 合成脚本（随仓库走，可复现）
Assets/Audio/Sfx/*.wav                 # SFX 素材（18 个）
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
                    PuzzleSolved, Checkpoint, Splash, LevelComplete, LevelFail }

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
| Checkpoint 激活 | 无 | `PlaySfx(Checkpoint)` |
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

## 6. 自动模式决策点清单（复盘用）

| # | 决策点 | 采用方案 | 为什么是主流 |
|---|---|---|---|
| D1 | 音频素材来源 | numpy 程序化合成 WAV | 无 key 可用/下载有许可证与失效风险；合成可复现、零依赖，与既有 art-pipeline 同构 |
| D2 | SFX 分发架构 | ScriptableObject 音频库 + 枚举键 + AudioManager 命名 API | Unity 社区音频管理主流范式（SO 数据驱动 + 静态快捷入口），避免场景到处挂引用 |
| D3 | BGM 分发 | 单一资产内 4 个引用 + 按 LevelIndex 解析 | 与 SFX 同架构，关卡配置零音频字段，新增关卡只加一个 clip 字段 |
| D4 | 校准策略 | 只记录+只修明显破坏项 | 未做玩家测试前大改数值是赌博；先建立可审计基线 |
| D5 | Build 目标 | Windows x64 独立构建到 Builds/ | 毕设交付/答辩演示的最常用目标平台 |

## 7. 文件清单

新增：
- `Tools/audio-pipeline/synthesize.py`
- `Assets/Scripts/Framework/GameAudioLibrary.cs`
- `Assets/Scripts/Editor/AudioLibrarySetup.cs`
- `Assets/Audio/Sfx/*.wav`（18）
- `Assets/Resources/Audio/Bgm/*.wav`（4）
- `Assets/Resources/Audio/GameAudioLibrary.asset`
- `specs/10-Polish/` 三件套 + `calibration.md`

修改（最小增量）：
- `Assets/Scripts/Framework/AudioManager.cs`（+3 API）
- `Assets/Scripts/UI/UIUtility.cs`、`UI/MainMenuPanel.cs`
- `Assets/Scripts/Player/PlayerController.cs`、`Combat/PlayerCombat.cs`、`Eco/SprayController.cs`
- `Assets/Scripts/Eco/GarbagePile.cs`、`Eco/TowerPurifiable.cs`、`Eco/WaterBlockage.cs`、`Eco/PurifyPickup.cs`
- `Assets/Scripts/Level/RescueTarget.cs`、`Level/WaterPuzzleController.cs`、`Level/Checkpoint.cs`、`Level/LevelManager.cs`
- `Assets/Scripts/EnemyAI/EnemyControllerBase.cs`（+Boss 受击分支）
- `Assets/Scripts/Level/DrownHazard.cs`
- `.gitignore`（+Builds/）
