# 需求文档：10-Polish（M10 打磨）

> 对应 TDD M10「打磨：音效铺量/数值校准/Build」。EARS 表述法。
> 前置：M1~M9 已完成（三关全流程可玩、解锁链路打通）。

## R1 音效铺量

- R1.1 当玩家执行跳跃/落地/受击/死亡动作时，系统应播放对应音效（Jump/Land/Hurt/Death），音量受设置面板 SFX 音量控制。
- R1.2 当玩家发动攻击、开启/释放喷雾时，系统应播放对应音效（Attack/SprayStart/HeavyRelease）。
- R1.3 当污染源/堵塞点被净化完成、拾取物被收集、动物被解救、水解谜完成、检查点激活时，系统应播放对应音效（PurifyComplete/Pickup/Rescue/PuzzleSolved/Checkpoint）。
- R1.4 当关卡完成/失败、Boss 受击、敌人受击、玩家溺水时，系统应播放对应音效（LevelComplete/LevelFail/BossHit/EnemyHit/Splash）。
- R1.5 当玩家点击 UI 按钮时，系统应播放 UIClick 音效。
- R1.6 系统应提供分关卡 BGM：标题页 1 首 + 三个关卡各 1 首，进入对应场景时自动切换（淡入淡出），BGM 音量受设置面板控制。
- R1.7 音频素材缺失时系统应静默跳过，不得报错或崩溃（与 M7 UIUtility 既有降级行为一致）。

## R2 数值校准

- R2.1 系统应对 PlayerConfig / CombatConfig / EcoConfig / EnemyConfig(01~03) / BossConfig 的全部数值进行一次审查，产出校准表（字段 | 现值 | 结论 | 理由）。
- R2.2 仅当数值明显破坏体验（如不可能通关、一刀秒杀 Boss、敌人零威胁）时才允许调整，且必须在校准表中记录调整前后值。
- R2.3 校准后的数值必须落回对应 Config 资产（数据驱动不改代码硬编码）。

## R3 Build 验证

- R3.1 当执行 Windows 独立构建时，构建应成功退出（exit code 0）且产出可执行文件。
- R3.2 构建产物应输出到仓库内 `Builds/` 目录（该目录不进 git）。
- R3.3 构建不得修改任何场景/资产内容（构建是只读验证）。

## R4 编译与回归

- R4.1 Unity 编译零 error（warning 不新增）。
- R4.2 Level01/Level02/Level03 三个场景加载回归无新增错误（Tracker idx=0、LevelManager/UIManager 在位）。
- R4.3 已有玩法链路不得回归（M6~M9 已验收行为不变）。

## 用户决策记录

- 2026-10-18：本里程碑无新增用户决策；按自动模式执行（决策点清单见 design.md §6）。

## 边界（明确不做）

- 不做语音/旁白、不做动态音乐系统、不做音频混合器快照（Mixer Snapshot）分级。
- 不联网下载素材；音频一律程序化合成（design.md §2）。
- 不发布/上传构建产物（sites 部署不在本范围）。
