# 设计文档：08-Boss（Boss 与第三关）

> 对应 `requirements.md` R1~R9。只做架构与关键决策，不贴大段源码。

## 1. 总体架构

```
BossController : EnemyControllerBase（M4）        ← 复用 HP/受击/死亡/感知/击退
   ├─ Config: BossConfig : EnemyConfig（M4）      ← 配置继承，字段兼容
   ├─ EnemyStateMachine（M4 复用）
   │    ├─ BossIdleState / BossChaseState
   │    ├─ BossAttackAState（近战，事件帧开窗）
   │    ├─ BossAttackBState（弹幕，EnemyProjectile + 对象池）
   │    ├─ BossSpecialState（P3 环绕弹幕）
   │    ├─ BossEnrageState（阶段过场，无敌）
   │    └─ BossHurtState / BossDieState（继承 M4 语义）
   └─ 阶段判定：Phase1 → Phase2(60%) → Phase3(30%)，只升不降

BossHealthBar（M8，程序化 uGUI）
   ├─ 血条 Fill + 数值 + Boss 名
   ├─ 60% / 30% 两条阶段刻度竖线
   └─ 阶段切换提示文字（1.5s 淡出）

第三关（Assets/Scenes/Level03.unity，Build index 2）
   ├─ LevelConfig（目标链：PurifyTargets 3 → DefeatEnemies 1）
   ├─ 复用 M6：LevelManager / LevelSpawner / LevelObjectiveTracker / Checkpoint
   ├─ 复用 M7：HUD / Pause / Result / Settings / Codex / LevelSelect
   └─ Boss01 实例（BossController + Animator + 血条）
```

## 2. 关键设计决策

### 2.1 为什么 Boss 继承 EnemyControllerBase 而不是另起炉灶

TDD §4 风险表已定："先做通用 FSM 基类，Boss 只写阶段脚本"。M4 的 `EnemyControllerBase` 已封装 HP/受击硬直/死亡广播/感知/击退/对象池回收，`EnemyStateMachine` 与 `EnemyStateBase` 也是通用的。继承后 Boss 只需写"阶段判定 + 三套技能状态"，新增代码量集中在差异点，符合"复杂度最高"风险项的既定对策。

代价：Boss 会带上部分用不到的字段（如 `AttackKind`）。可接受——`AttackKind` 由 Boss 状态自己决定用哪套技能，配置字段仅作兜底。

### 2.2 配置继承而非平行 SO

`BossConfig : EnemyConfig`。好处：`BossController` 的 `_config` 槽位类型仍是 `EnemyConfig`，注入 `BossConfig` 实例即可，M4 基类代码零改动；Inspector 里阶段参数自然跟在通用参数后面。

平行 SO（BossConfig 不继承）会导致基类字段读不到 Boss 参数，需要两份配置分别注入，多一层同步成本。

### 2.3 阶段切换过场为什么设短无敌

阶段切换要播 `Enrage`（约 0.8s）。若不过场无敌，玩家可以在过场动画里继续输出，出现"血条已过阈值但 Boss 仍在播狂暴动画"的视觉矛盾，也可能在切换瞬间被连续打断（每次掉血都重触发过场检测）。0.8s 无敌 + 一次性守卫（`_triggeredPhase2/3`）让过场只播一次，是横版 Boss 战社区常见做法。

### 2.4 喷雾倍率如何实现（最小改动权衡）

需求是"夹钳与喷雾都能打，喷雾 1.5×"。两条伤害路径都走 `IHittable.TakeDamage(DamageInfo)`，而 `DamageInfo` 现有字段（Amount/SourceDirection/Source/KnockbackPower）无法区分来源——`Source` 两种情况下都是玩家对象。

三个方案：

| 方案 | 改动 | 评价 |
|---|---|---|
| **给 `DamageInfo` 加 `bool IsPurify`** | M3 struct +1 行；M5 喷雾两处调用设 true | **采用**。struct 加字段对既有构造点无影响（默认 false），语义明确，未来其他系统也可复用该标记 |
| Boss 额外实现新接口，喷雾特判 | M5 调用点加分支 + 新接口 | 喷雾要区分"普通敌人 vs Boss"，破坏统一流水线 |
| 按 `Source` 反射判断技能来源 | 0 | 脆弱，靠对象类型猜来源不可靠 |

`BossController.TakeDamage` 覆写：按 `IsPurify` 乘 `PurifyDamageMultiplier`（向下取整），其余流程（硬直/击退/死亡）交回基类。普通敌人不受影响（基类不读该字段）。

### 2.5 Boss 死亡如何接到关卡完成

Boss 死亡沿用 M4 的 `EnemyDeathEvent`（携带 EnemyTypeId），M6 `LevelObjectiveTracker` 的 `DefeatEnemies` 分支已订阅该事件——所以第三关配置 `DefeatEnemies 1` 即可让 Boss 死亡推进目标链，**M6 逻辑零改动**。

额外新增 `BossDefeatedEvent`（struct）：供 M8 血条隐藏与将来的 Boss 战结算/成就使用，不影响 M6 通路。事件只携带 `BossId`/`LevelIndex`/`ElapsedSeconds` 三个值类型字段，零 GC。

### 2.6 血条为什么放屏幕顶部固定位

用户 2026-10-18 从三个方案里选了"顶部血条 + 阶段标记"。理由记录：横版 Boss 战中 Boss 会横向移动甚至短暂移出视野，贴头顶的血条会跟着乱飘、遮挡；屏幕顶部固定位是社区主流（魂系/银河城类横版 Boss 战通用），且能同时容纳阶段刻度与阶段提示文字。

### 2.7 第三关目标链为什么是两段

用户选"铺垫 + Boss"。Target 链 = `PurifyTargets 3` → `DefeatEnemies 1`。第一段复用已验证的净化玩法（顺便给净化剂补给机会），第二段 Boss 压轴。纯 Boss 竞技场会让关卡过于单薄；三目标（清怪→塔→Boss）工作量更大且 GDD 第三关描述是"高强度战斗 + Boss"，两段已足够。

## 3. 对既有系统的最小改动清单

| 文件 | 改动 | 行数 | 论证 |
|---|---|---|---|
| `Combat/DamageInfo.cs`（M3） | 加 `public bool IsPurify;` | 1 | R4 喷雾倍率唯一区分手段（§2.4） |
| `Eco/SprayController.cs`（M5） | 两处 `DamageInfo` 构造设 `IsPurify = true` | 2 | 同上 |
| `EnemyAI/EnemyAnimatorDriver.cs`（M4） | `EnemyAnimParams` 加 `AttackB`/`Special`/`Enrage` 三个 hash | 3 | Boss 动画参数；不改驱动逻辑 |
| `UI/LevelSelectPanel.cs`（M7） | 第三关卡片 `BuildIndex` 从 -1 改为实际索引；第 2 关保持"敬请期待" | ~2 | 让第三关可从菜单进入 |

除上述外对 M1~M7 零源码改动。

## 4. 动画装配（R8）

Boss01 帧目录已就位 8 套：`Idle / Move / AttackA / AttackB / Hurt / Die / Enrage / Special`。

- 工具 `BossAnimationSetup` 按 M4 `EnemyAnimationSetup` 同款做法：读帧目录 → 建 clip（帧率 `AnimFrameRate`）→ 建/更新 `Boss01.controller` → 配参数与转移。
- 新增参数：`AttackB`（Trigger）、`Special`（Trigger）、`Enrage`（Trigger）；其余沿用 M4 的 `IsMove`/`Attack`/`Hurt`/`Die`/`Speed`。
- 事件帧：AttackA 与 AttackB 各在有效帧挂 `OnAttackWindowOpen/Close`（M4 `EnemyControllerBase` 已有这两个回调），与 M4"事件帧为权威"一致。

## 5. 测试要点

- EditMode：`BossConfig` 默认回落；阶段阈值边界（HP 恰好 60%/30%）。
- Play：Boss 血条显示 30/30；打到 60% 触发过场（无敌期间伤害被忽略且日志可见）；P2 起弹幕解锁；P3 Special；喷雾伤害日志显示 `×1.5`；Boss 死亡 → 目标链推进 → 结算面板 → progress 解锁。
- 回归：M4 小怪伤害不受 `IsPurify` 影响；M6 第一关不受影响。

## 6. 风险

| 风险 | 对策 |
|---|---|
| Boss 帧序列与 clip 命名不匹配 | 工具按目录名生成 clip，运行前先打印清单供核对 |
| 弹幕数量 × 对象池预热不足导致卡顿 | `ProjectilePrewarm` 按 `SpecialProjectileCount` 上调；池不足时 PoolManager 会自动扩容（M1 语义） |
| 第三关与 M6 第一关共用 LevelManager，可能出现场景串数据 | Level03 独立场景 + 独立 LevelConfig，BossSetup 明确注入 Level03.asset |
| Boss 过场无敌被玩家误判为"卡住" | 过场同时显示"狂暴阶段"提示文字（R5） |
