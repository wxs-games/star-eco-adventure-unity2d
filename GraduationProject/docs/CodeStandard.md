# 《星际环保》编程规范与架构约定 · Code Standard

> 版本：v1.0 | 最后更新：2026-10-30
> 岗位归属：**主程序（Lead Programmer）** 撰写并执行；所有提交代码者必须遵守
> 上游约束：`TDD.md`（技术方案）、`specs/01-Framework`（框架规范三件套）
> 数据来源：177 个 `.cs` 文件逐层扫描（26,601 行）、3 个 `.asmdef`、实际代码风格归纳（截至 2026-10-30）

---

## 一、为什么需要这份文档

`TDD.md` 回答的是"用什么技术方案"（状态机、事件总线、对象池）。它不回答：

- 私有字段到底要写 `_config` 还是 `config`？
- 事件订完之后谁负责退订？忘退订会怎样？
- 场景里的对象必须用脚本生成，还是可以手动拖？
- 为什么 `SpriteRenderer.sortingOrder` 在预制体里是 0、进场景就变成 15？

这些是**每天都会遇到、每次都靠猜**的问题。这份文档把它们固定下来。

**适用规模**：177 个脚本、26,601 行、单人主导开发。规范按这个规模裁剪——不引入接口先行、依赖注入、分层测试等重流程，只固化"能立刻减少 bug 的约定"。

---

## 二、工程结构与分层

### 2.1 三个程序集（实测）

| 程序集 | 路径 | 作用 |
|---|---|---|
| `StarEco.Runtime` | `Assets/Scripts/StarEco.Runtime.asmdef` | 全部运行时逻辑 |
| `StarEco.EditorTools` | `Assets/Scripts/Editor/StarEco.EditorTools.asmdef` | 编辑器工具与场景生成脚本，**仅编辑器** |
| `StarEco.Tests.EditMode` | `Assets/Tests/EditMode/*.asmdef` | 纯逻辑单元测试 |

**规则**：编辑器脚本一律放 `Assets/Scripts/Editor/` 下，不得混入运行时目录。运行时程序集**不允许**出现 `using UnityEditor`（哪怕包在 `#if UNITY_EDITOR` 里，也会带来打包风险与编译时间成本）。

### 2.2 目录 ↔ 命名空间映射（严格一一对应）

| 目录 | 命名空间 | 文件数 |
|---|---|---|
| `Scripts/Core/` | `StarEco.Core` | 3 |
| `Scripts/Framework/` | `StarEco.Framework` | 8 |
| `Scripts/Player/` | `StarEco.Player` | 7 |
| `Scripts/Combat/` | `StarEco.Combat` | 11 |
| `Scripts/EnemyAI/` | `StarEco.EnemyAI` | 8 |
| `Scripts/EnemyAI/Events/` | `StarEco.EnemyAI.Events` | 3 |
| `Scripts/Boss/` | `StarEco.Boss` | 7 |
| `Scripts/Boss/Events/` | `StarEco.Boss.Events` | 2 |
| `Scripts/Eco/` | `StarEco.Eco` | 19 |
| `Scripts/Eco/Events/` | `StarEco.Eco.Events` | 5 |
| `Scripts/Level/` | `StarEco.Level` | 21 |
| `Scripts/Level/Events/` | `StarEco.Level.Events` | 9 |
| `Scripts/UI/` | `StarEco.UI` | 17 |
| `Scripts/UI/Events/` | `StarEco.UI.Events` | 1 |
| `Scripts/Editor/` | `StarEco.EditorTools` | 49 |

**规则**：新增目录必须同时新增命名空间，命名空间必须等于 `StarEco.<目录名>`（事件子目录加 `.Events`）。**不允许**在 `StarEco.Player` 下写 `StarEco.Level` 的类。

### 2.3 分层依赖方向

```
        Core（纯 C#，无 UnityEngine 依赖除数学）
          ▲
          │
      Framework（事件总线、对象池、存档、音频、单例）
          ▲
          │
  ┌───────┴────────┬──────────┬─────────┐
Player  Combat  EnemyAI   Eco  Level  Boss
  └───────┬────────┴──────────┴─────────┘
          ▼
         UI（只通过事件与公开 API 触碰上面所有层）
```

**硬规则**：

1. `Core` **不许** `using UnityEngine`（`DamageMath`、`CsvTable`、`NumericSafety` 是纯算法，可在 EditMode 测试里跑）。
2. `Framework` 不许引用任何业务命名空间。
3. 业务层之间**禁止直接互相 `GetComponent` 穿透调用**，跨系统一律走 `EventCenter` 或已公开的接口（`IHittable`）。
4. `UI` 只订阅事件 + 调 `LevelManager` 等门面方法，**不直接改游戏状态**。

---

## 三、命名约定

### 3.1 字段与属性

| 种类 | 约定 | 实例 |
|---|---|---|
| 私有字段 | `_camelCase`（下划线前缀） | `_config`、`_player`、`_finished` |
| 静态私有 | `_camelCase` | `_instance`、`_runner` |
| 常量 | `PascalCase` 或 `UPPER_SNAKE`（历史混用，**新代码用 PascalCase**） | `AxisHorizontal`、`WorldUiScale`、`DefaultStopSeconds` |
| 公有属性 | `PascalCase` | `IsGrounded`、`HorizontalInput`、`TopPanelName` |
| 事件 | `PascalCase` + `Event` 后缀 | `PurityChangedEvent`、`BossDefeatedEvent` |
| 枚举 | `PascalCase` 类型名 + `PascalCase` 值 | `LevelState.Completed`、`SfxId.BreakStone` |
| 接口 | `I` + `PascalCase` | `IHittable` |

### 3.2 方法

| 种类 | 约定 | 实例 |
|---|---|---|
| 公有方法 | `PascalCase`、动词开头 | `TakeDamage`、`MoveHorizontally`、`RestartLevel` |
| 私有方法 | `PascalCase` | `ReadInput`、`BuildResult`、`TryPlayEndingDialogue` |
| 协程 | `PascalCase`，`IEnumerator` 返回 | `CommitFinishAfterEnding` |
| 布尔判定 | `Is/Has/Can` 开头 | `IsGrounded`、`HasObjective`、`CanJump` |
| 事件回调 | `On` 前缀 | `OnPurityChanged`、`OnDestroy` |

### 3.3 MonoBehaviour 生命周期与自定义方法的顺序

文件内一律按此顺序排列：

```csharp
1. 常量 / 静态字段
2. [SerializeField] 私有字段
3. 公有属性
4. Unity 生命周期：Awake → OnEnable → Start → FixedUpdate → Update → LateUpdate → OnDisable → OnDestroy
5. 公有方法（API）
6. 私有方法（按调用顺序）
7. 嵌套类型 / 内部类
```

`PlayerController.cs`（1144 行）基本遵守这个顺序，可作为参照。

---

## 四、序列化字段规范

### 4.1 写法

```csharp
// ✅ 正确：私有 + [SerializeField]，序列化字段名保留 _ 前缀
[SerializeField] private LevelConfig _config;
[SerializeField] private float _radius = 2.6f;

// ❌ 错误：public 字段（Inspector 里可改，但没有封装，且改名会丢引用）
public float radius = 2.6f;
```

**理由**：项目所有构建脚本（`LevelSetup` 等）都通过 `SerializedObject.FindProperty("_radius")` 赋值，**字段名即接口**。改成 public 会让所有生成脚本一起失效。

### 4.2 字段改名 = 数据丢失

Unity 按**字段名**序列化。改 `_radius` → `_interactRadius` 会让所有场景、预制体里已配好的值变成默认值，**且不报错**。

**规范**：改序列化字段名必须同步做三件事：

1. 用 `[FormerlySerializedAs("_旧名")]` 保留兼容；
2. 全局搜索 `FindProperty("_旧名")`，改所有编辑器脚本；
3. 手动核一遍场景里的实际值有没有丢。

### 4.3 默认值必须写在字段上

```csharp
[SerializeField] private float _radius = 2.6f;   // ✅ 场景没配也能跑
[SerializeField] private float _radius;          // ❌ 场景没配就是 0，静默失效
```

---

## 五、事件系统（EventCenter）

### 5.1 唯一跨系统通信方式

```csharp
// 发布
EventCenter.Publish(new PurityChangedEvent { Pollution = p, Purity = 1 - p });

// 订阅
EventCenter.Subscribe<PurityChangedEvent>(OnPurityChanged);

// 退订（必须）
EventCenter.Unsubscribe<PurityChangedEvent>(OnPurityChanged);
```

**规则**：

1. **订阅必须在 `OnEnable` 或 `Start`，退订必须在 `OnDisable` 或 `OnDestroy`**，两者严格配对。少了退订 → 对象销毁后事件仍指向它 → `MissingReferenceException` 或内存泄漏。
2. 事件类必须是**只读数据载体**：只有字段、无方法、无逻辑。
3. 事件命名固定 `XxxEvent`，放对应系统的 `Events/` 子目录。
4. **不在事件回调里做耗时操作**（加载场景、创建大量对象）。回调是同步的，会阻塞发布方。
5. `EventCenter.ClearAll()` 只在场景卸载/重载时调（`LevelManager` 用），不要在业务代码里随手调。

### 5.2 现有事件清单（实测 20 个）

| 命名空间 | 事件 |
|---|---|
| `StarEco.Level.Events` | `LevelCompletedEvent`、`LevelFailedEvent`、`ObjectiveProgressEvent`、`ObjectiveCompletedEvent` 等 9 个 |
| `StarEco.Eco.Events` | `PurityChangedEvent`、`PurifyCompletedEvent` 等 5 个 |
| `StarEco.EnemyAI.Events` | 3 个 |
| `StarEco.Boss.Events` | `BossDefeatedEvent` 等 2 个 |
| `StarEco.UI.Events` | 1 个 |

---

## 六、单例与全局对象

### 6.1 MonoSingleton

```csharp
public class UIManager : MonoSingleton<UIManager> { ... }
```

- 首次访问 `Instance` 时**自动创建 GameObject 并 `DontDestroyOnLoad`**，不需要预先摆在场景里。
- 应用退出期（`_quitting`）`Instance` 返回 `null`，防止退出时复活。
- **访问前必须判空**：`AudioManager.Instance?.PlaySfx(...)`。退出期拿到的就是 null。

**现有单例**：`UIManager`、`AudioManager`、`PoolManager`、`SaveSystem`（静态类）、`LevelManager`（场景内单例，随场景销毁）。

### 6.2 静态工具类

`UIUtility`、`HitFeel`、`CsvTable`、`LevelSaveService` 是无状态静态类，**不许在其中缓存场景对象**（`HitFeel` 缓存了 `CameraFollow2D`，是唯一的例外，且做了 `FindObjectOfType` 兜底重取）。

---

## 七、对象池

### 7.1 用法

```csharp
var fx = PoolManager.Instance.Get(prefab);
fx.transform.position = pos;
// 用完归还
PoolManager.Instance.Release(prefab, fx);
```

**规则**：

1. **任何会反复生成/销毁的东西必须走池**：特效、子弹、伤害飘字、碎块、掉落物。
2. **禁止对池化对象用 `Destroy`**。销毁会让池里留下一个空引用，下次 `Get` 拿到 null。
3. `Get` 出来的对象可能带着上次的状态（位置、Animator 播放进度、颜色），**`OnEnable` 里必须重置**。
   - `FxOneShot` 的做法：`OnEnable` 里 `_resolved = false`，并重新 `Init(sourcePrefab)`。
4. 池的父节点在 `PoolManager` 下，**不要把它们挂回场景**（挂回去会让池被场景卸载清掉）。

---

## 八、配置与资产

### 8.1 ScriptableObject 配置

| 资产 | 用途 | 谁读 |
|---|---|---|
| `Assets/Settings/LevelConfig*.asset` | 关卡参数（`LevelIndex`、目标、时限） | `LevelManager` |
| `Assets/Settings/EcoConfig.asset` | 净化参数 + 氛围色 | `PuritySystem`、`AtmosphereController` |
| `Assets/Settings/PlayerConfig.asset` | 移动/跳跃/战斗数值 | `PlayerController` |
| `Assets/Resources/Audio/GameAudioLibrary.asset` | 音效/音乐映射 | `AudioManager` |

**规则**：数值不许硬编码在 `MonoBehaviour` 里（`HitFeel` 的 45ms 顿帧是例外，因为它不属于"可调平衡数值"）。

### 8.2 Resources 加载

```csharp
Resources.Load<Font>("Fonts/NotoSansSC-Regular")
```

**限制**：`Resources/` 下的所有内容会被**无条件打进包体**，不管用不用。当前 `Resources/` 里只有字体、数据 JSON、音频库、BGM，规模可控。**不要往里塞大图**。

---

## 九、编辑器脚本与场景生成规范

### 9.1 两条路线，必须选一条

项目里同时存在两种建场景方式：

| 方式 | 代表 | 优点 | 缺点 |
|---|---|---|---|
| **代码生成** | `LevelSetup.cs`、`Level02Setup.cs`、`BuildPrefab.cs` | 可重现、可版本控制 | 与手动改动会漂移 |
| **手动编辑 + 保存场景** | 大量后期微调 | 直观 | 不可重现 |

**由此产生的真实问题**：`Level02Setup` 里写的 `PlatformL1` 顶面是 1.40，场景实际是 2.58；`SpawnAnchorA` 脚本写 x=16、场景是 −10。菜单重建时因为"场景已存在就跳过"，这批漂移永远不会被发现。

**规范**：

1. 生成脚本是**初始化工具**，不是"真相来源"。**场景文件才是真相**。
2. 生成脚本一旦跑过，后续改动以场景为准，脚本只做"补齐缺失对象"，**不许覆盖已有对象的数值**。
3. 每次用生成脚本重建场景后，**必须**校核一遍文档里的坐标表。

### 9.2 编辑器脚本的四条硬约束

1. **`[MenuItem]` 必须有确认弹窗**（重建场景是破坏性操作）。
2. **不许在 `OnValidate` 里修改场景对象**（会触发无限重入）。
3. **不许在 `[ExecuteAlways]` 的 `LateUpdate` 里依赖"编辑器会重绘"**——编辑器不重绘时这段代码不跑，表现会时好时坏。
4. 跑批处理前必须检查 `EditorApplication.isPlaying` / `isCompiling`，两者为 true 时不要动场景。

---

## 十、已踩坑清单（新代码必读）

这一节是本项目**真实踩过**的坑，每条都附现象与解法。新代码凡是碰到这些 API，先看这里。

### 10.1 `SpriteRenderer.sortingOrder` 会自己变成 15

**现象**：预制体里设的 `sortingOrder = 0`，进场景后变成 15，导致武器/子物体被角色整块盖住。

**原因**：`Assets/Scripts/Editor/FixAllLevelSpriteSorting.cs` 的 `GuessOrder()` 对认不出的对象**兜底返回 15**，并且这把脚本被跑过多次。

**解法**：
- 运行时跟随父物体，不写死：`sr.sortingOrder = _body.sortingOrder + 1`。
- 需要在编辑器里也能看到 → 加 `[ExecuteAlways]`。
- 新加物件要在 `GuessOrder` 里登记识别规则，否则又会被扔进 15。

### 10.2 运行时创建的 Sprite 不能序列化

**现象**：编辑器脚本用 `UIUtility.WhiteSprite`（`Sprite.Create` 出来的内存精灵）建对象，保存场景后贴图消失，只剩透明碰撞体。

**实例**：第二关三块木筏 `Raft1/2/3` 现在就是隐形的——`SpriteRenderer.m_Sprite` 为 null。

**规则**：**凡是要保存进场景/预制体的 `Sprite`，必须是 `Assets/` 下的资产**。运行时 `Sprite.Create` 的只能用于临时显示。

### 10.3 WorldSpace Canvas 的 CanvasScaler 无效

**现象**：世界空间 UI 的 `scaleFactor` 设了没用，400×120 的气泡横跨 25 个屏幕。

**原因**：`RenderMode.WorldSpace` 下 `CanvasScaler` 被忽略。

**解法**：缩放在 `transform.localScale` 上做（`UIUtility.WorldUiScale = 0.01f`）。

### 10.4 `Image` 用 `Type.Filled` 必须挂 Sprite

**现象**：进度条 `fillAmount = 0.3`，画出来却是满的。

**原因**：`Image` 的 `sprite` 为 null 时，`Filled` 类型不生效。

**解法**：挂 `UIUtility.WhiteSprite`。项目里所有进度条都已按此处理。

### 10.5 Play 模式下不能 `EditorSceneManager.OpenScene`

**现象**：MCP/编辑器脚本在 Play 模式调用 `OpenScene` 抛 `InvalidOperationException`。

**解法**：先 `EditorApplication.isPlaying = false`，等退出 Play 再操作场景。

### 10.6 多人/多 Agent 并发操作 Unity

**现象**：另一个 Agent 中途切场景或进 Play，自己的 `OpenScene` 被抢走，改到了错的场景上。

**解法**：
- **open → 改 → save 必须写在同一次调用里原子完成**，中间不 `yield`。
- 改之前判 `GetActiveScene().isDirty`，脏就跳过。
- 改完用 `grep` 场景文件校验落盘（不依赖 Unity 的内存态）。
- 验证前先在运行时读 `GetActiveScene().name`，别假设当前场景是哪个。

### 10.7 事件忘退订

**现象**：切关后旧场景的对象仍在响应事件，报 `MissingReferenceException`，或旧 HUD 继续刷数字。

**解法**：`OnEnable` 订阅 / `OnDisable` 退订，严格成对。`EventCenter.ClearAll()` 只在场景切换时调。

### 10.8 `Input` 注入在 Play 测试里驱动不了状态转移

**现象**：用代码写 `HorizontalInput` 想让角色跑起来，`Update` 里的 `ReadInput` 会把它覆盖掉。

**解法**：帧末（`Application.onBeforeRender`）注入只能被 `FixedUpdate` 读到；要测状态转移必须走真实输入或用反射直接改字段并禁用对应组件（`PlayerCombat.enabled = false` 的同类手法）。

### 10.9 uGUI 中文渲染成方块

**现象**：所有中文变成 □□□。

**原因**：回退到了 Unity 内置 `LegacyRuntime.ttf`（无 CJK 字形）。

**解法**：`Assets/Resources/Fonts/NotoSansSC-Regular.otf` 必须存在且路径正确（`Resources.Load<Font>("Fonts/NotoSansSC-Regular")`）。

---

## 十一、提交与审查

### 11.1 提交前必查

- [ ] 编译 0 error（允许 warning，但不新增）
- [ ] `Assets/Scenes/` 与 `Assets/Prefabs/` 的改动**有明确原因**，不是"随手保存"
- [ ] 新增序列化字段有默认值
- [ ] 新加的事件订阅有配对退订
- [ ] 新加的池化对象在 `OnEnable` 里重置状态
- [ ] 改了序列化字段名 → 已加 `[FormerlySerializedAs]` + 改了所有 `FindProperty`
- [ ] 文档同步（改了关卡/数值/接口 → 改对应 `.md`）

### 11.2 禁止事项

- **禁止**在运行时脚本里 `using UnityEditor`。
- **禁止**对池化对象 `Destroy`。
- **禁止**在 `Update` 里 `FindObjectOfType` / `GetComponent`（每帧分配 + 慢）。`DialogueTrigger` 曾每帧 `FindObjectOfType`（40 B/帧），已修。
- **禁止**在 `Update` 里做字符串拼接（`BossHealthBar` 曾每帧插值，已修）。
- **禁止**把数值硬编码进 `MonoBehaviour`（放 ScriptableObject）。

---

## 十二、性能基线

| 指标 | 目标 | 实测 |
|---|---|---|
| Build 版每帧 GC 分配 | **0 B** | 0 B（`性能实测报告.md`） |
| Editor 下每帧分配 | 不作为判定口径 | ~375 次/帧，属编辑器底噪 |
| 目标帧率 | 60 FPS @1080p | 达成 |
| 单帧 `Update` 总耗时 | < 8 ms | 未做细粒度采样 |

> **重要**：GC 判定**必须用 Build 版**。Editor 下的数字包含编辑器自身的分配，用它做优化会南辕北辙。

---

## 十三、变更记录

| 版本 | 日期 | 变化 | 原因 |
|---|---|---|---|
| v1.0 | 2026-10-30 | 首版。三层程序集与 15 个命名空间映射、依赖方向硬规则、命名约定（字段/方法/生命周期顺序）、序列化字段规范（改名=丢数据的处理流程）、事件系统 5 条规则、单例与对象池规范、两条建场景路线的取舍、**9 条已踩坑清单**、提交前 7 项检查、性能基线 | 项目已有 `TDD.md`（技术方案）和 `specs/01~10`（逐功能规范），但没有一份**"写代码时怎么下手"**的约定。实际后果：序列化字段命名不统一、事件忘退订、运行时 Sprite 被序列化导致木筏隐形、sortingOrder 被兜底改成 15 等，都是"没有规范所以每人/每轮各写一套"造成的 |
