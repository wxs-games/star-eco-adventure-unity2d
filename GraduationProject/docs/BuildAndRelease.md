# 构建与发布流程 · BuildAndRelease

> 版本：v1.2 | 2026-10-31 | 维护：主程序
> 定位：**从"代码改完"到"一个能双击运行的 exe"的完整可复现流程**，含环境、定序、命令、验收、版本号、回滚、答辩演示包。
> 此前这些信息散落在 `docs/Balance/README.md` 与 `INSTALL.md`（位置不对），本文件是唯一权威。

---

## 1. 环境

| 项 | 值 | 说明 |
|---|---|---|
| Unity 版本 | **2022.3.62f3c1** | 精确到 revision，见 `ProjectSettings/ProjectVersion.txt`。换版本会触发全量重导入，答辩前不要升 |
| 目标平台 | **Windows Standalone x64** | 唯一交付平台 |
| 脚本后端 | **Mono**（`scriptingBackend: {}` 即默认 Mono） | 未用 IL2CPP：构建快、调试方便；代价是启动稍慢、易反编译（毕设无此顾虑） |
| API 兼容级别 | **.NET Standard 2.1**（`apiCompatibilityLevel: 6`） | |
| 分辨率 | 1920×1080，`fullscreenMode: 1`（全屏） | 与 UI 的 `CanvasScaler` 参考分辨率一致 |
| 公司 / 产品名 | `StarEco Studio` / `StarEco` | **决定存档路径**，见 §6 |
| 版本号 | `bundleVersion: 1.0` | 见 §5 |

**没有第三方插件依赖**（无 DOTween、无 Cinemachine、无 Odin）。UI、相机、音频、动画全部自写或 Unity 内建。这意味着**任何人拉下仓库、用同版本 Unity 打开就能构建**，不需要装包。

---

## 2. 场景定序（最容易出错的一步）

`ProjectSettings/EditorBuildSettings.asset` 的顺序**就是关卡序号**：

| Build index | 场景 | 对应 `LevelConfig.LevelIndex` |
|---|---|---|
| 0 | `Assets/Scenes/Title.unity` | —（主菜单，无 LevelConfig） |
| 1 | `Assets/Scenes/Level01.unity` | 1 |
| 2 | `Assets/Scenes/Level02.unity` | 2 |
| 3 | `Assets/Scenes/Level03.unity` | 3 |

### 2.1 为什么这个顺序是硬不变式

代码里有两处直接吃 `buildIndex`：

```csharp
// LevelManager.RestartLevel()：重开本关
SceneManager.LoadScene(gameObject.scene.buildIndex);

// ResultPanel：结算面板的「下一关」
nextBuild = result.LevelIndex + 1;  → LoadScene(nextBuild)
```

所以 **`LevelIndex == buildIndex`** 必须成立。一旦顺序错位：

- 结算「下一关」会跳到错误关卡；
- 「重新开始」会重载错误关卡；
- 第三关的落幕对白判定（`_config.LevelIndex == 3`）与实际场景不符 → 该播的不播。

**改顺序 ≡ 改三关 `LevelConfig.LevelIndex`**，两件事必须同一次提交。

### 2.2 谁负责保证

- `LevelSetup` / `Level02Setup` / `Level03Setup` 三个 Editor 脚本每次运行都会**重排 Build Settings**（幂等）。
- 手工在 Build Settings 里拖动 = 唯一会破坏不变式的操作。

---

## 3. 构建

### 3.1 图形界面（答辩前常用）

1. `File → Build Settings` → Platform 选 `Windows, Mac, Linux` → Target Platform `Windows`、Architecture `x86_64`。
2. 确认 Scenes In Build 顺序为 §2 的表。
3. `Build` → 输出目录约定见 §3.3。
4. 构建完**立刻双击 exe 跑一遍**，按 §4 验收。

### 3.2 命令行（可复现、可脚本化）

```bash
"C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" \
  -quit -batchmode -nographics \
  -projectPath "E:/desktop/UnityXiangMu/BiShe/BiShe" \
  -buildWindows64Player "E:/desktop/UnityXiangMu/BiShe/BiShe/Builds/2026-10-30/StarEco.exe" \
  -logFile "E:/desktop/UnityXiangMu/BiShe/BiShe/Builds/2026-10-30/build.log"
```

判据：**退出码 0 + exe 存在 + `build.log` 里没有 `error CS` 与 `Build failed`**。

> `-nographics` 在纯命令行环境必需；若机器有显卡且要跑 PlayMode 测试则去掉。

### 3.3 输出目录约定（当前混乱，需统一）

**现状**（实测 `Builds/` 下有 4 套产物，命名不一致）：

```text
Builds/StarEco.exe + StarEco_Data/          2026-10-18（最早，M10 前）
Builds/StandaloneWindows64/BiShe.exe        产品名还是 BiShe 时期的构建
Builds/2026-10-29/StarEco.exe               按日期目录（推荐形态）
Builds/GcMeasure/                           GC 实测专用包
```

**约定（今后只允许这一种）**：

```text
Builds/<YYYY-MM-DD>/StarEco.exe
Builds/<YYYY-MM-DD>/StarEco_Data/
Builds/<YYYY-MM-DD>/version.txt        ← 见 §5
Builds/<YYYY-MM-DD>/build.log
```

`Builds/` 已被 `.gitignore` 的 `[Bb]uilds/` 忽略（实测 `git check-ignore` 命中），**产物不进库**。旧的 `StandaloneWindows64/` 是历史遗留，可手动删除。

---

## 4. 构建后验收清单（10 项，逐条打勾）

| # | 检查 | 通过标准 |
|---|---|---|
| 1 | 双击启动 | 出现标题界面，无黑屏/Crash |
| 2 | 分辨率 | 1920×1080 全屏，UI 不裁切（HUD 三元素可见：心形/净化剂条/蓄力条） |
| 3 | 音频 | 标题 BGM 播放；点按钮有 `UIClick`；进关卡 BGM 切换 |
| 4 | 中文 | 所有文本正常显示（**不是方块**）——内置字体无 CJK 字形是历史坑 |
| 5 | 第一关 | 能移动/跳/攻击/喷洒；清 3 个垃圾堆 → 100% → 结算面板出现 |
| 6 | 第二关 | 解救 2 只动物 → 清 3 处堵塞（水位上升、木筏通路出现）→ 清 3 个污染源 → 杀 4 只 → 结算 |
| 7 | 第三关 | 清 4 个污染源（场景共 5 个可净化对象，任意 4 个）→ 走到 x≈68 打 Boss（**Boss 一进场就 active，无破土登场演出，见 `11-Level03` §4.3**）→ 三阶段 → 击败演出 → 落幕对白 → 结算。⚠️ **已知 P0**：Boss 被 `Env_Vent` 持续扣血，约 6.5 分钟会自己死（`RiskRegister` T-15） |
| 8 | 存档 | 通关后回主菜单 →「继续游戏」解锁进度正确；图鉴里已拾取的卡是亮的 |
| 9 | 退出 | 主菜单「退出游戏」能退出（不是静默失败） |
| 10 | 日志 | `%USERPROFILE%\AppData\LocalLow\StarEco Studio\StarEco\Player.log` 无 `Exception`/`NullReference` |

> 第 5~7 项是**三关回归**，也是 `TestPlan.md` §2.4 的同一份清单。构建后不跑这三关，等于没验。

### 4.1 最近一次实测记录（2026-10-31 01:54，同日第三次：含三关分层 + L3 地面 + SignGlow）

| 项 | 实测值 |
|---|---|
| 构建方式 | `manage_build`，target=windows64，options=`clean_build`，development=false |
| 输出 | `Builds/2026-10-31/StarEco.exe`（按 §3.3 的日期目录约定） |
| 耗时 | **55.3 s**（00:18 第一次）/ **57.1 s**（00:34 第二次）/ **63.7 s**（01:54 第三次，同日覆盖重建） |
| 产物大小 | 330.37 MB（前两次）→ **334.29 MB**（第三次，多出 L3 工业地面 1.77MB + 两张 1080p 背景 + 8 组 Props 新素材） |
| 结果 | **Result: Success**，errors **0**、warnings **0**（前两次各有 1 warning，本次连带修净） |
| 场景打包 | ✅ `level0/level1/level2/level3` 四个全在 `StarEco_Data/` 下 |
| 程序集 | `StarEco.Runtime.dll` + `MCPForUnity.Runtime.dll`（后者不该在，见 §10 B-05） |
| 无窗口冒烟 | `StarEco.exe -batchmode -nographics -quit` → `Loaded All Assemblies, in 4.072 seconds` ✓ → `[UIUtility] 场景缺少 EventSystem，已自动创建` ✓ → `[UIManager] UI 管理器就绪` ✓（`smoke3.log`；前两次为 `smoke.log` / `smoke2.log`） |
| 冒烟中的 shader error | 5 条 `shader is not supported on this GPU`（Hidden/VoxelizeShader、Sprites/Default、Sprites/Mask、GUI/Text、UI/Default）——**`-nographics` 用 NullGfxDevice 的必然结果，不是打包缺 shader**，真实 GPU 下不会出现 |
| 上一版 | `Builds/2026-10-30/`（62.4s / 330.43MB，P0 修复前），保留不删，见 §8 |

本轮相对于 v1.0.0（`7ebb3b1`）的变化：

*代码*：`VentHazard` 只结算玩家（P0）、`BossHealthBar` 进战区才显示、新增 `UI/ToastLayer`、`EnemyAnimatorDriver` 缺参数不再刷屏、结算面板弹出时收血条 + 抑制底部 toast。

*场景与素材*（第三次构建新增）：三关 91 个 `SpriteRenderer` 按 `ArtStyleGuide.md` §5.2 的分层表归位（**K-03**：此前 L2 的树/水/河床/笼/动物与玩家同为 15，走到树后会被整棵挡住）；L3 地面换 `ground_industrial`（`drawMode` Tiled、`m_Size` 96×8 未动，顶面仍在 y=0）；L1 两块可读牌加 `SignGlow` 子对象做「有边框 = 能读」的视觉区分（**K-05** 判为设计如此，剩的是外貌误导）；两张背景超分到 1920×1080 并由素材侧把 PPU 改成 187.5 保持世界尺寸不变。

> 同日被打过三次：00:18 的 v1.0.1、00:34 的 v1.0.2（`+987dc69`）均已被 01:54 的 **v1.0.3（`+80462e3`）** 覆盖（`clean_build`），故耗时三行并列。要回退到 P0 修复前请用 `Builds/2026-10-30/`。

> 无窗口冒烟只能证明「能启动、能加载场景、能初始化管理器」，**不能替代真机通关**。第 5~7 项仍需人工跑。



---

## 5. 版本号规范

**格式**：`v<主>.<次>.<构建号>+<commit 短哈希>`

| 情况 | 主 | 次 | 构建号 |
|---|---|---|---|
| 加/删功能、改玩法、改关卡结构 | +1 | 归 0 | 递增 |
| 修 bug、调数值、改文案 | — | +1 | 递增 |
| 纯构建（同代码重打） | — | — | 递增 |

写入 `Builds/<日期>/version.txt`：

```text
StarEco v1.0.0+7ebb3b1
Unity 2022.3.62f3c1 / Mono / .NET Standard 2.1
Build 2026-10-30 22:10 GMT+8
Scenes: Title(0) Level01(1) Level02(2) Level03(3)
```

同时建议把这一行打到启动日志开头（`Debug.Log`），这样 `Player.log` 第一行就能对上是哪个构建。

---

## 6. 存档位置与清档

```text
%USERPROFILE%\AppData\LocalLow\StarEco Studio\StarEco\
  ├─ progress.json          解锁进度 + 最佳星级
  ├─ level01_result.json    最近一局结算（三关共用这一个文件名，见 T-08）
  └─ codex.json             已收集知识卡 Id
```

- **答辩演示前务必清档**，否则会带着调试时的奇怪记录（例如一条 0.37 秒的"通关"）。
- 游戏内清档：主菜单「开始新游戏」会 `ClearProgress()` + `ClearResult()`（保留设置）。
- 手动清档：删掉上述目录即可（游戏会自动重建）。

---

## 7. 回滚

| 场景 | 做法 |
|---|---|
| 新构建有问题，要回到上一版 | `Builds/` 保留上一版目录，直接换 exe（**不要删旧构建**，至少留两版） |
| 代码要回滚 | `git tag v1.0.x` 标记可交付点；`git revert <commit>`（**优先 revert 而非 reset**，保留历史） |
| 只回滚某个场景/资产 | `git checkout <commit> -- Assets/Scenes/Level03.unity`，然后重新构建 |

> 教训依据：2026-10-27 曾因"继续推进"误重做跑循环动画，靠 `revert 9eb0319` 回到用户定稿版。**能回滚的前提是提交粒度够细**——这也是 §8 要求"文档 / 场景 / 代码分开提交"的原因。

---

## 8. 提交与构建的关系（重要纪律）

| 规则 | 理由 |
|---|---|
| **构建前必须提交** | 否则 exe 对应的代码状态无处可查 |
| **文档 / 场景 / 代码分开提交** | 回滚时能只回滚一类（例如只回滚美术，不动代码） |
| **构建后 `git status` 必须与构建前一致** | 排除"构建过程悄悄改了场景/资产"（Build 一般不会，但 `[ExecuteAlways]` 组件与 Setup 脚本可能） |
| 场景改动一律"打开→改→保存"在同一原子操作内 | 并发会话抢场景会导致改动丢失（已发生两次） |

---

## 9. 答辩演示包

```text
StarEco_Demo/
  ├─ StarEco.exe  +  StarEco_Data/  +  UnityPlayer.dll  +  MonoBleedingEdge/
  ├─ version.txt
  ├─ 操作说明.txt          ← 键位表（A/D、Space、J、K、L、E、Esc）
  └─ README_答辩.txt       ← 三关流程 + 每关要展示的 3 个技术点
```

**演示前 30 分钟清单**：

1. 清档（§6）。
2. 跑一遍三关（§4 第 5~7 项），确认没有意外。
3. 关掉录屏/直播软件（会抢全屏焦点）。
4. 准备好"如果不小心跳到桌面"的恢复动作（`Alt+Tab` 回游戏后按 Esc 暂停再继续）。
5. 若计划演示"死亡即失败 + 重载"，**先把这一局玩到有进度再死**，否则看不出"重载后回到初始态"。

---

## 10. 已知问题（与本文相关）

| # | 问题 | 影响 | 处理 |
|---|---|---|---|
| B-01 | `Builds/` 下 4 套历史产物命名不一致 | 容易拿错 exe 演示 | 统一到 §3.3 约定，旧目录手动删 |
| B-02 | 无自动构建脚本（每次手点或手敲命令） | 易漏步骤（尤其场景顺序） | 可加 `Tools/build.ps1`（可选，非必需） |
| B-03 | 构建不写入 `version.txt` | 无法反查 exe 对应提交 | §5 规范，待落地 |
| B-04 | 未做 IL2CPP / 未签名 | exe 可能被杀软误报 | 毕设环境可接受；如需分发再用 IL2CPP |
| **B-05** | **发布包带进 `MCPForUnity.Runtime.dll`**（32 KB）：`com.coplaydev.unity-mcp` 是编辑器工具，但它的 `Runtime/MCPForUnity.Runtime.asmdef` 里 `includePlatforms: []` 为空 = 全平台编译，于是被打进包 | 不专业（答辩时包里有开发工具），功能无害（8 个 Helper/Serialization 类，无网络代码）；占包体 0.01% | **不改**：这是第三方包，改 asmdef 会被下次更新覆盖，且设成 Editor-only 可能影响 Play 模式下的 MCP 桥接。只在此登记 |
| **B-06** | **四个场景都没有 EventSystem**：Title/L01/L02/L03 全为 0，靠 `UIUtility.EnsureEventSystem()` 运行时创建 | 编辑模式下看不到 EventSystem；理论上首帧点击可能落空 | P2：给四个场景各存一个 EventSystem 即可；当前有兜底，不影响运行 |

---

## 11. 变更记录

| 版本 | 日期 | 变化 |
|---|---|---|
| v1.1 | 2026-10-30 | §4 验收第 7 项删掉「第 4 个是封锁墙 → Boss 破土登场」（BossGate 在毕设版未挂载，见 `11-Level03` §4.3）并标注 T-15 这个 P0；新增 **§4.1 最近一次实测记录**（62.35s / 331MB / 0 error / level0~3 全打包 / 无窗口冒烟通过）；§10 新增 B-05（发布包带 MCPForUnity.Runtime.dll）、B-06（四场景均无 EventSystem） | A6 收口实际跑了一次 clean build，把实测数据落进文档而不是只写在对话里 |
| v1.2 | 2026-10-31 | **§4.1 换成 P0 修复后的第二次实测**（55.3s / 330.37MB / Result Success / 冒烟三段全过）；输出按 §3.3 的日期目录约定落到 `Builds/2026-10-31/`，上一版 `2026-10-30/` 保留；新增 `version.txt`（v1.0.1+b210562）与「本轮相对 v1.0.0 的代码变化」四项；头部版本号从 v1.0 补到 v1.2（此前 v1.1 只在变更记录里，头部没同步） | P0 喷口伤 Boss 修复 + 三处 B 线改动后必须重打一版，否则答辩演示用的包里 Boss 仍会被自己的喷气口打死 |
| v1.0 | 2026-10-30 | 首版。环境锁定（2022.3.62f3c1/Mono/.NET Std 2.1）、场景定序不变式与理由、GUI + CLI 两种构建方式、输出目录统一约定、10 项构建后验收、版本号规范、存档路径与清档、回滚策略、提交纪律、答辩演示包与 30 分钟清单 |
