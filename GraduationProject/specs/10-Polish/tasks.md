# 任务清单：10-Polish

> 对应 `requirements.md` R1~R4 / `design.md`。状态：`[x]` 完成并验证 / `[ ]` 未完成。
> 执行模式：**自动模式**（2026-10-18 用户切换），决策点按 design.md §6 清单执行。

## 一、spec 三件套

- [x] T1 `specs/10-Polish/requirements.md`：EARS R1~R4（音效/BGM/校准/Build/回归）。
- [x] T2 `specs/10-Polish/design.md`：合成方案、音频架构、接线表、校准策略、决策清单。
- [x] T3 `specs/10-Polish/tasks.md`：本清单。

## 二、音频素材合成（R1）

- [x] T4 `Tools/audio-pipeline/synthesize.py`：numpy 合成 18 SFX + 4 BGM（30s 循环），输出 design.md §2.3 目录；固定 seed 可复现。**已运行**：18 SFX（0.08~1.30s，rms 0.13~0.43）+ 4 BGM（各 30.00s 整）全部生成。
- [x] T5 22 个 WAV 均被 Unity 识别：AudioLibrarySetup 一键装配 18/18 SFX + 4/4 BGM 全部绑定成功。

## 三、音频库与接线（R1）

- [x] T6 `Framework/GameAudioLibrary.cs`：SfxId 枚举（18 项）+ SO（SFX 表 + 4 BGM）+ GetSfx/GetBgm。
- [x] T7 `AudioManager` 增量：PlaySfx(SfxId)/PlayTitleBgm/PlayBgmForLevel，Resources 懒加载 + 缺失静默（R1.7）；既有 API 不动。
- [x] T8 `Editor/AudioLibrarySetup.cs`：菜单 `StarEco/Setup/GameAudioLibrary` 一键生成/回填 GameAudioLibrary.asset（按文件名约定）。**已执行**，日志确认绑定数。
- [x] T9 接线 design.md §3.3 表全部 13 处调用点（其中"三处净化完成"合并为 PuritySystem 中心订阅 1 处，覆盖垃圾堆/塔/堵塞点三类目标）。
- [x] T10 BGM 挂点：LevelManager 初始化按 LevelIndex 播 BGM；MainMenuPanel 播 Title BGM。**Play 实测**：Level02/03 进入即自动播放对应 BGM（bgmClip=Level02/Level03）。

## 四、数值校准（R2）

- [x] T11 审查 6 组 Config 全部字段，产出 `specs/10-Polish/calibration.md`（字段|现值|结论|理由）。
- [x] T12 调整 2 项明显破坏项：Enemy01.AttackRange 10→3.5、Enemy03.AttackRange 9→3.0（近战 hitbox 触达仅 3u/2.5u，原值导致 3~10u 间停步空挥木桩）；其余 60+ 字段保持（与 GDD 一致或已经实测），前后值记录在案，资产已落盘。

## 五、Build 与回归（R3/R4）

- [x] T13 `.gitignore` 已含 `[Bb]uilds/`（无需改）；Windows x64 独立构建到 `Builds/`：47~59s，1052MB，0 错误 0 警告，产物 `StarEco.exe`（MZ 有效）+ `StarEco_Data/`（含 level0~3 四关数据）。
- [x] T14 构建前后 git status 对比：ProjectSettings/Scenes/Configs 零改动（R3.3 只读验证通过）。
- [x] T15 Unity 编译零 error；Level01/02/03 加载回归通过（Tracker idx=0、LevelManager/UIManager/音频库在位、Level02 水谜与解救、Level03 Boss）；控制台仅 SaveSystem 损坏 JSON 自检日志。**Play 音频链路实测**：18 SFX 全播无异常、玩家跳跃端到端触发 Jump 音效、BGM 分关自动切换。
- [x] T16 更新 TDD M10 行 + v0.4 变更记录 + tasks.md 状态 + 需求对齐报告 + git commit。

> 边界遵守：未联网下载素材（fal key 未配置，改用程序化合成）、未做语音/动态音乐/Mixer、未发布构建产物（Builds/ 不进 git）。
