# 数值 kit 装配记录（BiShe）

> 装配日期：2026-10-22。来源：可复用资源库的 `unity-balance-kit`（**复制，不移动**；源库未被改动）。
> 本文件只回答「装了什么、放哪、有没有启用、回拷要注意什么」；数值本身看 `README.md`。

## 1. 位置映射

| 源（资源库内） | 目标（本工程） | 作用 | 启用状态 |
|---|---|---|---|
| `unity-balance-kit\Tools\balance-kit\` | `Tools\balance-kit\` | 手册 + 校验脚本 + 示例表 | 手动运行；不参与 Unity 编译 |
| `...\tables\combat_units.csv` | `docs\Balance\combat_units.csv` | 战斗数值表（已换成项目实值） | 文档；`check` 读取 |
| `...\tables\progression_curve.csv` | — | 成长曲线 | **未装**：本作无等级成长，理由见 `README.md` §4 |
| `...\tables\economy_flows.csv` | — | 货币收支 | **未装**：本作无货币，理由见 `README.md` §4 |
| `...\templates\CsvTable.cs` | `Assets\Scripts\Core\CsvTable.cs` | 运行时 CSV 解析 | **已编译，未被任何代码/场景引用** |
| `...\templates\DamageMath.cs` | `Assets\Scripts\Core\DamageMath.cs` | 伤害公式三族 + DPS/EHP/TTK | **已编译，未被引用** |
| `...\templates\NumericSafety.cs` | `Assets\Scripts\Core\NumericSafety.cs` | 货币溢出保护 + 紧凑数字格式 | **已编译，未被引用** |
| （无对应源） | `GraduationProject\docs\GDD.md` §8 | 数值落地状态回填 | 文档 |

> 「未被引用」= 不挂载、不被现有脚本调用，对运行零影响。要启用时在代码里 `using StarEco.Core;` 即可。

## 2. 与源模板的差异（回拷时必须重做）

1. **命名空间**：三个 `.cs` 由 `GameFramework` 改为 `StarEco.Core`（工程内既有约定）。
2. **TTK 阈值**：`balance_check.py` 顶部改为项目带宽 `trash 1.0~2.5 / elite 2.5~6 / boss 8~30`，
   依据见 `README.md` §2。模板默认 0.1~0.8s 不适用于本作。
3. **表**：只保留 `combat_units.csv`；`progression_curve.csv` / `economy_flows.csv` 未装。

从资源库重新复制 `Tools\balance-kit\` 会把 1、2、3 覆盖回模板默认值，必须重新做一遍。

## 3. 验证记录

| 项 | 结果 |
|---|---|
| 数值表体检 | `python Tools/balance-kit/balance_check.py check docs/Balance --strict` → `0 error / 0 warn / 5 info`，退出码 0 |
| Unity 编译 | 2026-10-22 12:17 触发一次重编译，Editor.log 无 `error CS`，`StarEco.Runtime.dll` 内含三个类型 |
| 运行时影响 | `Assets` 内除自身外**零引用**，场景/Prefab 无引用 |
| 单位规范化（2026-10-22） | 战斗值改为整数定点「半心点」（2 点 = 1 心），CSV 体检 `0 error / 0 warn / 5 info`；Play 实测玩家 10/10=5 心、远程 1 点掉半心、近战 2 点掉 1 心、场上敌人 HP 6/6/8 与表一致 |
| 难度曲线 v0.5（2026-10-22） | CSV 体检 `0 error / 0 warn / 5 info`（Boss TTK 15.01s）；Play 实测 L1 场上 2 只 Enemy01（6/6）+ 静态塔（16/16）、第二条目未解锁不生成；L3 场上 1 只 Enemy03（10/10）+ Boss 80/80；Unity 编译 0 error |
| Boss 专项测试（2026-10-22） | 程序化桩测试（玩家钉位+锁无敌，按 5.33 点/s 注入），非真人试玩。阶段阈值 HP 48/80（60%）、24/80（30%）；两次过场均 **0.80s**，过场中额外 2 点伤害被忽略（HP 不变）。弹幕：P1 **3 发/3.00s**、P2 **4 发/2.40s**、P3 **B 4 发/2.10s + Special 5 发/4.20s**，同屏峰值 **9 发**；近战 A 间隔 P1 **2.00s** / P2 **1.60s** / P3 **1.40s**（= 2.0s × 阶段系数）。木桩纯输出 **15.00s** + 过场 1.60s = **16.60s**，对应实战 30~40s 需有效输出占比约 42%~55%，与 P3 躲避节奏吻合。Special 实测间隔 4.86s（被 A 状态挤占，理论 4.2s）；连续输出期间 Boss 仍成功放弹幕，未被硬直锁死。Play 退出后控制台 0 error / 0 warning |

## 4. 以后在本工程继续做时

- 改数值的顺序：**先改 `docs/Balance/combat_units.csv` → 跑 `check` → 再改 `Assets/Settings/*.asset`**，最后回填 `GDD.md` §8。
- 新增等级/货币系统后，再按 `Tools/balance-kit/BalanceHandbook.md` Step 5/6 启用成长曲线与经济表。
- 动喷壶/蓄力波/Boss 净化相关代码前，先看 `README.md` §5 的四项「代码 vs GDD」不一致，
  那是设计决策点，不是 bug 清单。

## 5. 回退方式

删除 `Tools\balance-kit\`、`docs\Balance\`、`Assets\Scripts\Core\` 三个目录；
`GDD.md` §8 的三行状态改回「⏳ 设计初值（Mx 未落地）」，并删掉表下那两行引用说明。
