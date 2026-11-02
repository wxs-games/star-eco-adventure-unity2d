# 技术方案（SDD）：08-Boss（Boss01 与第三关）

> 版本：v2.0（2026-10-30 按落地代码重写） | 对应 `requirements.md` R1~R9 | 命名空间 `StarEco.Boss` | 代码落点 `Assets/Scripts/Boss/`（9 个 .cs / 1,865 行）
> 前置已就位：M3 `IHittable`/`DamageInfo`、M4 `EnemyControllerBase`/`EnemyStateMachine`/`EnemyStateBase`/`EnemyProjectile`/`EnemyAnimatorDriver`/`EnemyConfig`、M5 `FxOneShot`/`GarbagePurifiedEvent`、M6 `LevelManager`/`LevelObjectiveTracker`/`LevelConfig`、M7 `UIManager`/`UIUtility`
> 立项决策（已定稿）：Boss **继承** `EnemyControllerBase` 而非另起炉灶；`BossConfig : EnemyConfig`（配置继承，字段兼容）；阶段过场短无敌 + 一次性守卫；喷雾倍率靠 `DamageInfo.IsPurify` 标记（不改伤害流水线）；血条固定屏幕顶部（不跟头顶）
>
> **v2.0 修正的四处失实**：① 第三关 Build index 是 **3** 不是 2（Build 顺序 Title/L01/L02/L03）② 目标链首段是 `PurifyTargets` **4** 不是 3（含封锁墙）③ Boss `MaxHP` 实测 **80**（v1.0 写"血条显示 30/30"）④ v1.0 没写 5.5s 击败演出（`DefeatSequence`）——本节已补全
>
> **⚠️ v2.1 撤回一条（2026-10-30 23:58，A6 实测）**：v2.0 曾称"v1.0 完全没写封锁墙登场"并把它当成本关的实际功能补写。**实测证明毕设版场景根本没有挂 `BossGate`**——那个脚本是求职展示版用的，372 行代码在毕设版是死代码。§2.4 已整节重写。这是本项目第二次犯同类错误（前一次是"检查点"）：**照代码读结论，漏了验证代码在场景里有没有被挂上**。

## 1. 结构

### 1.1 类层次

```text
EnemyControllerBase（M4，1,000+ 行）
   ├─ HP/受击硬直/无敌/击退/死亡广播/感知/池回收   ← 全部继承
   └─ BossController（517 行）覆写 6 处：
        · BuildStateMachine()      注册 8 个 Boss 专属状态
        · Update()                 先 base，再 TickTransition/TickCooldowns/EvaluatePhase
        · TakeDamage()             IsPurify → ×PurifyDamageMultiplier；过场期无视伤害
        · Recycle()                死亡时追加广播 BossDefeatedEvent
        · OnDieEnd()               覆写：插入 5.5s 击败演出，演完才走 base
        · DecidePostHurtState()    P3 受击后直接回 Idle（不带追击延迟）

EnemyConfig（M4 SO）
   └─ BossConfig（109 行，+7 组字段）：
        Phase2HpPercent / Phase3HpPercent / PhaseTransitionSeconds
        AttackA* / AttackB* / Special* 三套「前摇-命中-后摇」
        ProjectileCount / SpreadDegrees / SpecialProjectileCount / SpecialSpreadDegrees
        PursueDelaySeconds / Phase2/3IntervalScale / PurifyDamageMultiplier
```

**为什么继承而不是新建基类**：`EnemyControllerBase` 已经把"怪物的通用生命周期"封装完整（HP、硬直、击退、死亡广播、池回收、感知）。Boss 与它的差异只有"技能集"和"阶段"——这两件事都可以用**状态**表达。若新写一套，等于把 1000 行已验证代码复制一遍，且 M4 每次修 bug 要改两处。

**代价**：Boss 会带上用不到的字段（`AttackKind`、`DropTable`）。可接受，由状态自己决定用哪套技能。

### 1.2 文件职责（`Assets/Scripts/Boss/`）

| 文件 | 行数 | 职责 |
|---|---|---|
| `BossController.cs` | 517 | 阶段判定与过场、技能冷却、净化倍率、死亡钉刚体、击败演出、落幕字幕、`BossDefeatedEvent` |
| `BossStates.cs` | 496 | `BossStateBase` + `BossIdleState`/`BossChaseState`/`BossAttackStateBase`/`BossAttackAState`/`BossAttackBState`/`BossSpecialState`/`BossEnrageState`（`BossHurtState`/`BossDieState` 复用 M4） |
| `BossConfig.cs` | 109 | `BossConfig : EnemyConfig` + `CreateDefault()` 兜底 |
| `BossPhase.cs` | — | 三阶段枚举 + `DisplayName()` 扩展 |
| `BossAnimParams.cs` | — | 动画参数 hash 常量（`AttackB`/`Special`/`Enrage`/`Victory` 等） |
| `BossHealthBar.cs` | 294 | 顶部血条：Fill + 数值 + 60%/30% 刻度 + 阶段提示文字（1.5s 淡出） |
| `BossGate.cs` | **372** | **封锁墙登场演出**（求职版启用）：监听指定 `TargetId` 的 `GarbagePurifiedEvent` → 墙崩解 → 地震 → Boss 破土登场。⚠️ **毕设版五场景零挂载，当前是死代码**（见 §2.4） |
| `Events/BossPhaseChangedEvent.cs` | — | 阶段变化（Phase/PhaseName/HpPercent） |
| `Events/BossDefeatedEvent.cs` | — | 击败（BossId/LevelIndex/ElapsedSeconds） |

### 1.3 第三关场景构成

| 项 | 值 |
|---|---|
| 场景 | `Assets/Scenes/Level03.unity`，**Build index = 3** |
| 配置 | `Assets/Settings/Levels/Level03.asset`（`LevelIndex = 3`） |
| Boss 配置 | `Assets/Settings/Boss/Boss01.asset`（`BossConfig`） |
| 目标链 | ① `PurifyTargets` **4**「清理工厂污染源（含封锁墙）」→ ② `DefeatEnemies` **1**「击败工厂污染核心 Boss01」。⚠️ Label 里的「含封锁墙」与实际场景不符（无封锁墙），建议改文案 |
| 机关 | `CrumblePlatform`（碎裂平台）/ `MovingPlatform`（移动平台）/ `VentHazard`（蒸汽）/ `ConveyorZone`（传送带） |
| 复用 | M6 全套（`LevelManager`/`LevelSpawner`/`LevelObjectiveTracker`/`LevelStats`）、M7 全套面板、M10 音频 |

> 目标链的 4 个污染源，实测场景里可净化的对象共 **5 个**（`Garbage1/2/3` TargetId 均为 `GarbagePile`，加 `GarbageWall_03L/03R`），`_purifiedTargets` 按事件递增不去重，任意 4 个即达成。**原本设计的"第 4 个必然是封锁墙 → 打开 Boss 的门"这层因果在毕设版不成立**：既没有封锁墙，Boss 也没有门禁，一直 active 站在原地。

### 1.4 Boss01 实测数值（`Assets/Settings/Boss/Boss01.asset`）

| 字段 | 值 | 说明 |
|---|---|---|
| `MaxHP` | **80** | 玩家夹钳一段 1 点、三段合计约 2 点；净化波 3×1.5=4。设计上约 40~60 秒战斗 |
| `KnockbackResponse` | 1 | 可被击退（但 `IsArmored` 期间不打断动作） |
| `AttackKnockback` | 6 | 打玩家时的击退 |
| `PerceptionRadius` | 20 | 全场感知（第三关 Boss 场地约 30u 宽） |
| `Phase2HpPercent` / `Phase3HpPercent` | 60 / 30 | 阈值 |
| `PhaseTransitionSeconds` | 0.8 | 过场无敌时长 |
| `Phase2IntervalScale` / `Phase3IntervalScale` | 0.8 / 0.7 | 冷却倍率（越小越凶） |
| `AttackARange` / `AttackAInterval` | 2.8 / 2.0 | 近战横扫 |
| `AttackAWindup` / `Active` / `Recover` | 0.20 / 0.62 / 0.50 | 前摇霸体、命中窗由动画事件开 |
| `AttackBInterval` | 3.0 | 弹幕 |
| `AttackBWindup` / `Active` / `Recover` | 1.10 / 0.60 / 0.50 | 蓄力 1.1s（有可见动作） |
| `ProjectileCount` / `SpreadDegrees` | 3 / 24° | 扇形三连 |
| `SpecialInterval` | 6.0 | 仅 P3 |
| `SpecialProjectileCount` / `SpreadDegrees` | 5 / 140° | 环绕扇形 |
| `SpecialWindup` / `Active` / `Recover` | 1.40 / 0.62 / 0.35 | |
| `PursueDelaySeconds` | 1.8 | 放完远程后观望 1.8s，不贴脸 |
| `PurifyDamageMultiplier` | 1.5 | 喷雾/净化波伤害 ×1.5 |
| `DropTable` | **null** | Boss 不掉落 |

## 2. 关键机制

### 2.1 三阶段（只升不降）

```text
Update（每帧，放在最后）
  if (BossCfg == null || IsPhaseTransition) return;
  percent = HpPercent;
  if (!_triggeredPhase3 && percent <= 30) { _triggeredPhase3 = true; EnterPhase(P3); return; }
  if (!_triggeredPhase2 && percent <= 60) { _triggeredPhase2 = true; EnterPhase(P2); }

EnterPhase(next)：
  CurrentPhase = next
  _transitionTimer = 0.8                       ← 过场计时，>0 期间 IsPhaseTransition=true
  _pendingHurt = false；_hurtStunTimer = 0      ← 清掉待处理受击，过场不受伤
  IsArmored = false
  Machine.SwitchState<BossEnrageState>()       ← 站定播 Enrage
  TryFireAnim(Enrage)
  Publish(BossPhaseChangedEvent{Phase, PhaseName, HpPercent})   ← 血条据此弹"狂暴阶段"提示

TickTransition：
  _transitionTimer -= dt；到 0 → Machine.SwitchState<BossIdleState>()（恢复战斗）
```

- **为什么阶段判定放在 `Update` 最后**：本帧受击掉血后立刻反映到阶段，不会晚一帧。若放在前面，从 61% 打到 59% 时这一帧仍按 P1 出招，下一帧才切——玩家能看到"血条已过线但 Boss 还在用旧招"。
- **为什么过场要无敌**：0.8s 的 Enrage 动画期间玩家能继续输出的话，会出现"血条早过阈值、Boss 还在播狂暴"的视觉矛盾，且每次掉血都重触发过场检测。一次性守卫 `_triggeredPhase2/3` 保证每段只播一次。
- **三阶段的实质差异**：① 可用技能集（P1/P2 无 Special）② 冷却倍率（0.8/0.7 → 出招频率提升 25%/43%）③ 移速倍率（手写在 `BossChaseState`，不依赖基类 `MoveSpeed` 缩放）。

### 2.2 三套技能与"前摇-命中-后摇"

| 技能 | 触发 | 前摇（霸体、不动） | 命中 | 后摇（可被打断） |
|---|---|---|---|---|
| **A · 近战横扫** | 玩家距离 ≤ `AttackARange` 2.8 | 0.20s 起手动作 | **动画事件** `OnAttackWindowOpen/Close` 开窗 0.62s | 0.50s 后回 Idle，并 `BeginPursueDelay()` |
| **B · 扇形弹幕** | 冷却好且不在近战距离 | 1.10s 蓄力（`Charge`，核心鼓胀有可见预兆） | 前摇结束时一次生成 3 发、扇形 24° | 0.50s |
| **Special · 环绕弹幕**（仅 P3） | `SpecialInterval` 6s | 1.40s 蓄力 | 一次 5 发、扇形 140° | 0.35s |

- **优先级**：`BossIdleState.TryStartAttack()` 顺序为 **Special（P3）→ A（近身）→ B（远程）**。
- **A 走动画事件、B/Special 走前摇计时**：近战是"表现窗口 = 判定窗口"，开窗必须跟动画对齐；弹幕是"发射点一次性"，用计时与 `Charge` 动画时长对齐即可（这正是 v1.0 的 `AttackBActiveSeconds` 覆盖剩余帧的用意）。
- **打完后不立刻贴脸**：A 结束调 `BeginPursueDelay()`（1.8s），`BossChaseState` 期间 `IsPursueDelayed` 为真就不追——否则 Boss 会一直黏在玩家身上，玩家没有喘息距离。
- **霸体**：`IsArmored=true` 时 `EnemyControllerBase.CanBeHurtInterrupted` 返回 false，受击不打断动作。

### 2.3 净化伤害倍率（`IsPurify` 标记）

```text
TakeDamage(info)：
  if (IsDead) return;
  if (IsPhaseTransition) { Log("阶段过场无敌，伤害被忽略"); return; }
  if (info.IsPurify && PurifyDamageMultiplier > 0)
      info.Amount = max(1, floor(info.Amount × 1.5))
  base.TakeDamage(info)        ← 扣血/硬直/击退/死亡全交基类
```

`DamageInfo` 原本无法区分伤害来源（`Source` 两种情况都是玩家对象）。三个方案里选了"给 struct 加一个 `bool IsPurify`"：对既有构造点零影响（默认 false），语义明确，未来别的系统也能复用这个标记。**这条改动只影响 Boss**——基类不读该字段，普通怪物的数值一分不变。

### 2.4 封锁墙与 Boss 登场（`BossGate`）——**毕设版未启用，代码是死代码**

> ⚠️ **v2.1 重大更正（2026-10-30 23:58，A6 实机验收发现）**
>
> v2.0 把这一节写成了"实际功能"，并称"v1.0 完全没写"。**两处都错**：
>
> | 核查项 | 实测结果 |
> |---|---|
> | `BossGate.cs` | ✅ 存在，**372 行**，代码完整（`Reveal()` 协程、地震、破土、`PushPlayerClearOf` 全都在） |
> | 五个场景挂载数（Title/L01/L02/L03/Sample） | ❌ **全部 0** |
> | 场景里有 `BossGateWall` 吗 | ❌ **没有** |
> | Boss 初始状态 | **一线入场就 `active=True`** 站在 (68.0, ~2.3)，无升起/破土 |
>
> **真相**：这套演出是**求职展示版**专属。`Level03ExtendSetup.cs:25` 原文：
> > 两份项目通用：右端封锁墙按名字查，**毕设版是 `GarbageWall_03R`，求职展示版是 `BossGateWall`**（后者挂着 BossGate 演出脚本，只移位不动逻辑）。
>
> 毕设版的实际流程是「净化右端垃圾墙开路 → Boss 已经站在那里等你」。下面这段代码逻辑**描述的是求职版行为**，毕设版不执行：

```text
【以下为 BossGate.cs 代码逻辑，毕设版场景未挂载，不会执行】
Boss 被一面「封锁墙」挡在场地深处，墙本身是一个 IPurifiable（TargetId = "BossGateWall"）
玩家把它净化完 → GarbagePurifiedEvent{TargetId="BossGateWall"}
 → BossGate.OnGarbagePurified：_revealed 守卫 → StartCoroutine(Reveal())

Reveal() 时间轴：
  ① 相机 Shake(0.22s, 0.10) + PlaySfx(PurifyComplete)
  ② 0.22s：墙体 Sprite 淡出 + 压扁（Y 缩到 45%）     ← 只关 SpriteRenderer，不能关整个物体：
                                                         BossGate 自己挂在这面墙上，SetActive(false) 会停掉协程，
                                                         Boss 就永远等不出来（源码里有这句注释）
  ③ 1.8s 地震：相机幅度 0.35，每次伴随 BreakStone(0.15 音量) 的碎石音
  ④ 0.3s 破土出：Boss 从地下升起（初始 scale 0.35 → 1.0，下压 1.6u 深度）
  ⑤ 把玩家挤出 Boss 包围盒（PushPlayerClearOf），防止被"顶"进墙里
  ⑥ Boss 激活，进入 BossIdleState
```

**这段代码的价值**：它是一套完整可用、随时可挂的登场演出（求职版正在用）。毕设版如果要启用，只需在 `GarbageWall_03R` 上加 `BossGate` 组件并把 `_gateTargetId` 设为该墙的 TargetId——代码不需要改。

**为什么文档会写错**：v2.0 是照代码读的。代码确实实现了完整演出，读代码得出"这就是实际流程"在逻辑上没错，**漏的是"这个脚本在场景里到底有没有被挂上"这一步**。这与本项目"检查点"教训同类——**判"有"必须查三件：脚本存在、场景挂载、调用点**。

### 2.5 击败演出（`DefeatSequence`，5.5s）

Boss 死亡时不立刻走基类流程。`OnDieEnd()` 被覆写：

```text
OnDieEnd()
  if (_defeatSequenceStarted) return;      ← 幂等
  _defeatSequenceStarted = true;
  StartCoroutine(DefeatSequence())

DefeatSequence()：
  0) 玩家 InputLocked = true；GrantInvincibility(6.5s)
     场上其它敌人 enabled = false（第三关还有 Enemy03 巡逻，不能打断收尾）
     Body 改 Kinematic（否则关掉 Collider 后会穿过地面掉出画面——玩家只看到"凭空消失"）
  1) 0.75s：3 次净化波连炸（每次间隔 0.25s）+ 相机 Shake
  2) 0.9s ：污染色 → 净化绿 (0.55,1,0.68) 线性插值，伴随细密震动
  3) 1.2s ：核心上浮 1.4u 并淡出，象征污染源消散
  4) 玩家播 Victory 动画
  5) ShowEpilogue()：底部黑底条 + 两行浅绿字幕
        淡入 0.45s / 停留 1.7s / 淡出 0.45s
        文案：「污染核心停转了——工厂的烟囱，第一次透出天光。\n这颗星球，重新开始呼吸了。」
  6) base.OnDieEnd() → 广播 EnemyDeathEvent → 关卡目标链推进 → 结算
```

**为什么演出必须在 `EnemyDeathEvent` 之前**：`LevelResultUI`/`ResultPanel` 一收到 `LevelCompletedEvent` 就会弹结算并冻结输入。若死亡事件先发，演出会被结算面板整段盖住。所以"先演完，再广播"——这也是 `OnDieEnd` 必须被覆写的原因（基类里它就是"广播+回收"）。

### 2.6 血条（`BossHealthBar`）

- 屏幕顶部固定位，**不跟 Boss 头顶**：Boss 会横向移动甚至短暂移出视野，贴头顶的血条会乱飘、遮挡战斗区域。
- 元素：名称「工厂污染核心 · Boss01」+ Fill 条 + 数值 + **60% / 30% 两条阶段刻度竖线**（提前告诉玩家"还有两个阶段"，减少"怎么打不完"的挫败）。
- 阶段切换：`BossPhaseChangedEvent` → 弹「狂暴阶段 / 最终形态」提示文字，1.5s 淡出。
- 血条不走 `EnemyHealthBarLayer`：Boss 是唯一单位，独立组件比通用层更省事。

## 3. 数据流

### 3.1 一次受击 → 阶段切换

```text
玩家夹钳命中 → AttackHitbox → BossController.TakeDamage(DamageInfo{Amount=1, IsPurify=false})
  非过场期 → base.TakeDamage：CurrentHP 80→79
  基类广播 EnemyDamagedEvent → DamageNumberLayer 飘字 / EnemyHealthBarLayer（Boss 不走）
  BossHealthBar.Update 轮询 HpPercent → Fill 变化
  BossController.Update 末行 EvaluatePhase()：79/80 = 98.75% > 60% → 无阶段变化

玩家用净化波命中 → DamageInfo{Amount=3, IsPurify=true}
  TakeDamage：3 × 1.5 = 4（向下取整）→ 日志「净化伤害 3 ×1.5 → 4」
  ...继续打到 48/80 = 60% → EvaluatePhase：
      percent(60) <= 60 → _triggeredPhase2 = true → EnterPhase(Phase2)
      SwitchState<BossEnrageState>()；IsPhaseTransition=true（0.8s）
      Publish(BossPhaseChangedEvent{2,"狂暴阶段",60})
          → BossHealthBar 弹「狂暴阶段」提示 + 血条边框变红
      → 0.8s 内玩家所有伤害被 TakeDamage 开头挡掉（日志可见）
      → TickTransition 到点 → SwitchState<BossIdleState> → 冷却乘 0.8、解锁 Special？否（P3 才解锁）
```

### 3.2 一次净化污染源 → 目标①完成 → 打完 Boss → 结算

```text
【清污染源】场景共 5 个可净化对象，凑满 4 个即完成目标①
玩家喷雾净化任一个
 → GarbagePile.CompletePurify → Publish(GarbagePurifiedEvent{TargetId=该对象的 TargetId})
 → 并行两方：
     · PuritySystem：净化值 +10 → PurityChangedEvent → 背景色向自然绿插值
     · LevelObjectiveTracker：_purifiedTargets++ → 到 4 时目标①完成 → 目标② DefeatEnemies(1) 解锁
（没有第三方的 BossGate —— 毕设版未挂载，见 §2.4）

【Boss】Boss01 从一进场就 active 站在 (68.0, ~2.3)，无登场演出、无门禁

【战斗】三阶段，见 §3.1

【击败】
Boss HP ≤ 0 → 基类 DieState → 播 Die 动画 → Animator 事件 DieEnd
 → BossController.OnDieEnd()（覆写）→ 5.5s 演出（§2.5）
 → base.OnDieEnd()
      BroadcastDeath → Publish(EnemyDeathEvent{EnemyTypeId="Boss01", ...})   (EnemyControllerBase.cs:437)
      Recycle() → Boss 是场景实例（非池借出）→ SetActive(false)
      BossController.Recycle 覆写：已死 → Publish(BossDefeatedEvent{BossId, LevelIndex=3, Elapsed})
          ⚠️ BossDefeatedEvent 当前 0 订阅者（见 §5 B4）

 → LevelObjectiveTracker.OnEnemyDied：_defeatedEnemies = 1 ≥ Required(1)
 → Evaluate：_index=2 ≥ 2 → CompleteChain() → 回调 LevelManager.OnObjectiveChainCompleted
 → LevelManager.FinishLevel(true, "全部关卡目标完成")
 → TryPlayEndingDialogue()：LevelIndex==3 → 播 ava_ending（3 条）
 → 玩家按 E 看完 → 等 1 帧 → CommitFinish(true)
 → CommitResult 写 result + 解锁进度 → Publish(LevelCompletedEvent)
 → ResultPanel 弹「S / ★★★ / 重新开始 · 下一关(隐藏) · 返回主菜单」
```

**注意时序**：`BossDefeatedEvent` 在 `Recycle` 里发，此时 `EnemyDeathEvent` 已经发完、目标链已经推进。所以任何"Boss 死了之后做事"的逻辑都应该挂在 `EnemyDeathEvent` 或 `LevelCompletedEvent` 上，而不是 `BossDefeatedEvent`。

### 3.3 一次 Special 环绕弹幕

```text
P3 且 _specialCooldown <= 0 且 BossIdleState 判定通过
 → Machine.SwitchState<BossSpecialState>()（继承 BossAttackBState）
 Enter：_specialCooldown = 6.0；播 Special Trigger；IsArmored = true
 0 → 1.4s：蓄力（不动、不受伤硬直）
 1.4s：FireVolley() —— 覆写父类的弹幕内容：
      for i in 0..4：角度 = -70 + 140 × i/(5-1)（140° 扇形）
        EnemyProjectile 经 PoolManager.Get → Launch(方向, 速度 7, 射程 14)
 1.4 → 2.0s：命中/后摇
 2.0s → +0.35s：Recover → 回 Idle
```

## 4. 设计取舍

| 取舍项 | 选择 | 放弃的方案与理由 |
|---|---|---|
| Boss 基类 | 继承 `EnemyControllerBase` | 放弃"独立 Boss 基类"：会复制 1000 行已验证代码，且 M4 每次修 bug 要改两处 |
| 配置 | `BossConfig : EnemyConfig` | 放弃"平行 SO"：基类字段读不到 Boss 参数，需要两份配置互相同步 |
| 阶段切换 | 短无敌（0.8s）+ 一次性守卫 | 放弃"无过场"：血条过了阈值但 Boss 还在用旧招，视觉矛盾；也放弃"长过场"：横版 Boss 战超过 1.5s 会打断节奏 |
| 近战判定 | 动画事件开窗 | 放弃"计时开窗"：近战的手感契约是"表现窗口=判定窗口"，错一帧玩家就觉得打空 |
| 弹幕判定 | 前摇计时 + 蓄力动画 | 放弃"也走动画事件"：弹幕是一次性发射点，用计时与 `Charge` 时长对齐更可控（也避免动画改帧就改判定） |
| 净化倍率 | `DamageInfo.IsPurify` 标记 | 放弃"Boss 特判喷雾"（破坏统一流水线）/ "按 Source 反射猜技能"（脆弱） |
| 血条位置 | 屏幕顶部固定 | 放弃"贴头顶"：Boss 横向移动会移出视野，血条乱飘且遮挡战斗区 |
| Boss 登场方式 | 玩家净化封锁墙后破土 | 放弃"走到场地就触发"：让第三关两个目标有因果关系；也放弃"过场动画"：无美术资源做不了 |
| 击败表现 | 5.5s 脚本演出 + 字幕 | 放弃"直接播 Die 动画就结算"：结算面板会瞬间盖住一切，玩家看不到任何收尾 |
| Boss 死亡钉刚体 | 死亡首帧 `bodyType = Kinematic` | 放弃"不加处理"：死亡态关了 `Collider2D`，动态刚体会在 2s 死亡动画里带着 Boss 穿过地面掉出画面 |

## 5. 风险

| # | 风险 | 概率 | 影响 | 现状与对策 |
|---|---|---|---|---|
| **B0** | **Boss 站在自己关卡的喷气口旁，被 `Env_Vent` 持续扣血 + 吹起** | **已发生** | **P0：Boss 血不受玩家控制地流失** | A6 实测 144.7s 内从 80 掉到 **54（67.5%）**，约 2.5s/点。Boss collider x=65.82~70.18 与 `Env_Vent` x=64.4~66.0 **重叠 0.18**；`VentHazard.OnTriggerStay2D` 只找 `IHittable` 不筛敌我。阈值 60%/30% → 约 3 分钟自动进二阶段、6.5 分钟自己死。详见 `11-Level03` §7.2 N0 |
| **B0b** | **Boss 血条永不显示** | **已发生** | **高：打 Boss 看不到血量** | `BossHealthBar._boss` 预绑 `Boss01` → `TryBindBoss()` 首行 `if(_boss!=null) return;` **永不 `Show()`**；`Build()` 末尾已 `Hide()` → `BossHealthBarCanvas.active=False`。修法：把 `Show()` 提到预绑分支之前 |
| ~~B1~~ | ~~封锁墙 `TargetId` 不一致 → Boss 永不登场~~ | — | — | ❌ **风险不存在**：毕设版场景未挂 `BossGate`，Boss 一进场就 active（§2.4 已更正）。该 Warning 已由 B4 任务加进 `BossGate.cs`，但代码本身不执行 |
| B2 | 击败演出期间玩家输入被锁，若协程被异常打断会永久锁输入 | 低 | 高 | `GrantInvincibility(6.5s)` 是超时兜底；`InputLocked` 无超时兜底。若演出抛异常，玩家会卡在不能动的状态。对策：`try/finally` 或在 `LevelManager` 侧加超时解锁（P1） |
| B3 | 阶段过场无敌期间玩家输出全被丢弃，可能误判为"打不动" | 中 | 中 | 血条同步弹「狂暴阶段」提示 + 日志；过场仅 0.8s |
| B4 | `BossDefeatedEvent` **0 订阅者** | 已存在 | 低（预留） | 本关通关实际走 `EnemyDeathEvent` → 目标链。该事件当前是纯冗余，将来做"Boss 战计时/成就"才有消费者 |
| B5 | Boss `DropTable` 为 null | 已存在 | 低 | Boss 不掉落是设计选择（结算面板给奖励），非缺陷 |
| B6 | 弹幕池预热不足导致 P3 卡顿 | 低 | 中 | `ProjectilePrewarm = 10`；P3 最密一波 5 发，余量 2 倍。`PoolManager` 池不足会自动扩容（M1 语义），代价是当帧分配 |
| B7 | 第三关 `LevelIndex` 依赖 Build index = 3 | 高 | 高 | 与 06-Level01 §5 L6 同一条不变式：Build 顺序 Title/L01/L02/L03，`LevelIndex == buildIndex` |

## 6. 与 requirements 的偏差说明

| 编号 | requirements 原文口径 | 实际实现 | 原因 |
|---|---|---|---|
| D1 | 第三关 Build index = 2 | **3**（Title=0、L01=1、L02=2、L03=3） | M9 加入第二关后整体后移一位，文档未同步 |
| D2 | 目标链 `PurifyTargets 3` → `DefeatEnemies 1` | `PurifyTargets` **4** | 第 4 个是封锁墙本身；改 3 会导致"清完 3 个但墙没开、Boss 不出现" |
| D3 | （未提） | 新增 `BossGate`（封锁墙 + 破土登场，350 行） | 让第三关两个目标有因果；也让 Boss 登场有戏剧性 |
| D4 | （未提） | 新增 `DefeatSequence`（5.5s 击败演出 + 落幕字幕） | 直接结算会盖住一切收尾；叙事需要"星球重新呼吸"这一幕 |
| D5 | R5 血条"显示 Boss 名 + 数值 + 阶段刻度" | 实现无误，但 v1.0 文档写"显示 30/30"——实际 `MaxHP = 80` | 数值在校准阶段从 30 提到 80（30 太短，玩家还没看到 P3 就打完了） |
| D6 | R9 `BossDefeatedEvent` 供血条隐藏与结算使用 | 血条用 `HpPercent` 轮询 + `BossPhaseChangedEvent`；结算走 `EnemyDeathEvent`；该事件 **0 订阅** | 实现演化后它被两条更早的通路取代 |
| D7 | （未提） | `BossChaseState` 手写阶段移速倍率，不依赖 `EnemyConfig.MoveSpeed` | 阶段移速是 Boss 专属手感，写进 SO 会污染通用敌人配置 |
