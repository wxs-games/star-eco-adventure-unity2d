# 开发文档索引

本目录的开发文档从主项目 `BiShe/BiShe/GraduationProject/` 恢复而来，已剔除开题报告、论文大纲、论文查重预案、答辩问答清单等毕设学术类材料，仅保留用于求职展示的开发设计文档。

## 目录结构

```
Docs/开发文档/
├── docs/                    # 各专业方向设计文档
│   ├── GDD.md               # 游戏设计文档（核心设计愿景）
│   ├── TDD.md               # 技术设计文档（技术选型与架构）
│   ├── SystemOverview.md    # 系统总览（分层/依赖/事件拓扑）
│   ├── CodeStandard.md      # 编程规范与架构约定
│   ├── LevelDesign.md       # 关卡设计文档
│   ├── LevelArt.md          # 关卡美术设计
│   ├── NarrativeDesign.md   # 叙事设计（世界观/角色/剧情）
│   ├── AudioDesign.md       # 音频设计文档
│   ├── ArtStyleGuide.md     # 美术风格指南
│   ├── AnimationVFXSpec.md  # 动画与特效规格
│   ├── Localization.md      # 本地化规范
│   ├── BuildAndRelease.md   # 构建与发布流程
│   ├── TestPlan.md          # 测试计划与验收用例
│   ├── 性能实测报告.md       # 性能实测数据
│   ├── 风险登记.md          # 风险台账（RiskRegister.md）
│   ├── UXDesign.md          # UX 设计规格
│   ├── 竞品分析.md          # 竞品/参考作品分析（CompetitorAnalysis.md）
│   ├── 交接文档.md          # 交接文档
│   ├── 交接文档-2026-10-19.md
│   ├── 测试记录-2026-10-31.md
│   ├── 策划实现对账清单.md
│   └── README.md            # 文档总索引（原主项目）
├── specs/                   # 11 个模块的逐功能技术规范（EARS 格式）
│   ├── 01-Framework/        # 事件总线、对象池、存档、单例
│   ├── 02-PlayerController/ # 移动、状态机、输入
│   ├── 03-Combat/           # 攻击、受击、伤害管线
│   ├── 04-EnemyAI/          # 敌人 AI、感知、刷怪
│   ├── 05-EcoSystem/        # 净化、氛围、收集
│   ├── 06-Level01/          # 第一关
│   ├── 07-UIDialogue/       # 面板栈、对话、提示牌
│   ├── 08-Boss/             # Boss 三阶段、封锁门
│   ├── 09-Level02/          # 第二关、水流解谜、解救
│   ├── 10-Polish/           # 打磨、校准
│   └── 11-Level03/          # 第三关 · 能源工厂
└── Balance/                 # 战斗数值表
    ├── combat_units.csv
    ├── INSTALL.md
    └── README.md
```

## 已剔除的学术类材料

以下文件主项目中存在，但已从求职版中剔除：
- `开题报告.docx`
- `开题报告-要求对照表.md`
- `论文大纲-草案.md`
- `论文查重预案.md`
- `答辩问答准备清单.md`

如需这些材料，请到主项目 `BiShe/BiShe/GraduationProject/docs/` 查找。