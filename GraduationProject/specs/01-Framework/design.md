# 技术方案（SDD）：01-Framework

> 对应 `requirements.md` R1~R5 | 命名空间 `StarEco.Framework` | 8 文件 / 908 行
> 版本：v2.0（M1 骨架 → 落地后全量重写） | 2026-10-30
> 上游：`docs/SystemOverview.md` §2（分层）/ §3.1（单例与自举）
> 下游：`02-PlayerController`、`03-Combat`、`05-EcoSystem` 等全部依赖本层

---

## 1. 结构

### 1.1 与里程碑的边界

框架层是**唯一无场景依赖**的一层：所有管理器都是懒加载单例，因此 **Boot 场景至今没有建，也不需要建**——任何脚本在任何场景里都能直接 `XxxManager.Instance`。

| 复用 | 新建 | 为何不重构 |
|---|---|---|
| — | `MonoSingleton<T>`、`EventCenter`、`ObjectPool<T>`、`PoolManager`、`SaveSystem`、`AudioManager`、`GameAudioLibrary`、`FrameworkSelfTest` | M1 从零起，无历史包袱；这五件套从 M1 至今**零改动**（只有 `AudioManager` 在 M10 加过 SFX 通道），说明接口设计成立 |

### 1.2 文件职责表

| 文件 | 行 | 职责 | 关键类型 |
|---|---:|---|---|
| `MonoSingleton.cs` | 81 | CRTP 单例基类 | `MonoSingleton<T>`（抽象） |
| `EventCenter.cs` | 95 | 类型安全的发布订阅 | 静态类 |
| `ObjectPool.cs` | 85 | 单类型对象池 | `ObjectPool<T> where T : Component` |
| `PoolManager.cs` | 83 | 按预制体分池的池注册表 | `PoolManager : MonoSingleton<PoolManager>` |
| `SaveSystem.cs` | 83 | JsonUtility 存/读/删/存在性 | 静态类 |
| `AudioManager.cs` | 251 | BGM 淡入淡出 + SFX OneShot | `AudioManager : MonoSingleton<AudioManager>` |
| `GameAudioLibrary.cs` | 108 | `SfxId` / `BgmId` 枚举 → `AudioClip` 映射 | `ScriptableObject` |
| `FrameworkSelfTest.cs` | 122 | 启动自检（EDITOR 条件编译） | `[RuntimeInitializeOnLoadMethod]` |

### 1.3 依赖关系

```
MonoSingleton<T>  ←── PoolManager / AudioManager
      ▲
      │（仅这两个需要"有生命周期"）
EventCenter（静态，无生命周期）
SaveSystem（静态，无生命周期）
ObjectPool<T>（纯 C# 类，被 PoolManager 持有）
GameAudioLibrary（SO，被 AudioManager 读）
```

**无环、无相互依赖**：`EventCenter` 不知道 `AudioManager`，`SaveSystem` 不知道任何人。框架层内部零耦合。

---

## 2. 关键机制

### 2.1 MonoSingleton&lt;T&gt;（R1）

```csharp
public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
```

- **CRTP 约束** `where T : MonoSingleton<T>`：编译期保证 `Instance` 返回具体子类型，不需要强制转换。
- **双重检查**：`Instance` 属性内 `if (_instance == null)` → `FindObjectOfType<T>()` 兜底 → 仍为 null 则 `new GameObject(typeof(T).Name)` 挂上去。**场景里找不到就自建**，这是"不挂场景也能用"的实现方式。
- **`_quitting` 标志**：`OnApplicationQuit` 置位，堵"退出时某脚本访问 `Instance` → 触发复活 → 引擎已销毁 → 报错"的经典漏洞。
- **域重载安全**：`OnDestroy` 里 `if (_instance == this) _instance = null`，否则 Unity 编辑器关闭 Domain Reload 时会残留失效引用。

### 2.2 EventCenter（R2）

- 存储：`Dictionary<Type, List<Delegate>>`。
- `Subscribe<T>(Action<T>)` / `Unsubscribe<T>(Action<T>)` / `Publish<T>(T)`。
- **倒序遍历**分发：`for (int i = list.Count - 1; i >= 0; i--)`，两个目的：
  1. 订阅者在回调里 `Unsubscribe` 自己不会打乱遍历；
  2. 回调里 `Publish` 另一个事件（嵌套发布）时，只要不是同一个事件，不会抛 `CollectionModifiedException`。
- **Unity 伪 null 清理**：`Delegate.Target as UnityEngine.Object` 若已销毁（`== null` 但 `ReferenceEquals` 不为 null），倒序移除。这样"对象被 Destroy 但忘了 Unsubscribe"不会在下次 Publish 时抛 `MissingReferenceException`。
- **同步、无队列、无优先级**：`Publish` 当场执行完所有订阅者。这是本项目最重要的语义约定——见 `SystemOverview.md` §6。

### 2.3 ObjectPool&lt;T&gt; + PoolManager（R3）

- `ObjectPool<T>` 是**非 Mono 的纯 C# 类**，内部 `Stack<T>` 空闲队列 + 每池一个根节点 GameObject（挂在 `PoolManager` 下，避免场景层级散乱）。
- `PoolManager` 用 `Dictionary<GameObject, object>` 按**预制体引用**缓存池；值装的是泛型池的**非泛型包装**（`object` 装箱），对外 API 仍泛型：`Get<T>(prefab)` / `Release<T>(prefab, obj)`。
- **重复 Release 防御**：池内维护 `HashSet<T>` 记录"已借出"，`Release` 时若不在已借出集合中则直接丢弃（防止同一个对象被两处 `Release` 导致队列里出现两份、`Get` 出来两个相同引用）。
- 实际使用方：`EnemyProjectile`、`FxOneShot`、`BreakableDebris`（碎裂块）。

### 2.4 SaveSystem（R4）

- 静态类，`JsonUtility` 序列化，路径 `Application.persistentDataPath/<slot>.json`。
- API：`Save<T>(slot, data)` / `Load<T>(slot)` / `Exists(slot)` / `Delete(slot)`。
- **损坏容错**：`Load` 用 try/catch 包裹，异常返回 `new T()`；文件不存在也返回 `new T()`。调用方永远不需要判空。
- **约束**：数据类必须 `[Serializable]` + 只含 `JsonUtility` 支持的类型（基础类型 / `List<T>` / 可序列化类）。**不支持 `Dictionary`**——这是本项目"存档数据结构全是 List + 查表"的原因。
- 4 个槽的归属见 `SystemOverview.md` §8.5。

> ⚠️ **没有版本号字段**。改动存档结构后旧档会反序列化成"字段缺失 = 默认值"，不会报错。当前体量下靠"改结构后手动删档"渡过，未做迁移。

### 2.5 AudioManager（R5，M10 扩展）

- `MonoSingleton<AudioManager>`，`Awake` 里建**两个子 AudioSource**：
  - `_bgmSource`：`loop = true`，播 BGM，单条
  - `_sfxSource`：`PlayOneShot`，可叠加，不打断
- **BGM 切歌协程**：当前音量线性降到 0（0.8s）→ 换 clip → 升到目标音量。`yield return null` 逐帧插值，不依赖 `Time.deltaTime` 累加，避免暂停时卡住。
- **音量持久化**：`GameSettingsData { float bgmVolume; float sfxVolume; }` 存 `settings` 槽，`Awake` 时读回。
- **命名 API**（M10 加）：`PlaySfx(SfxId id, float pitchVariation = 0f)` / `PlayBgm(BgmId id)`。`GameAudioLibrary` 是 `SfxId → AudioClip` 的数组映射，由 `Editor/AudioLibrarySetup.cs` 按**文件名约定**自动回填（`Assets/Audio/Sfx/<SfxId>.wav`）。

> ⚠️ **缺项静默降级**：`GameAudioLibrary` 查不到 clip 时 `PlaySfx` 直接 return，不打警告。这是刻意的（避免 22 个音效缺 1 个就刷满 Console），代价是"改文件名导致静默失声"很难发现。排查失声时先查 `GameAudioLibrary.asset` 对应项是否为空。

> ⚠️ **没有 AudioMixer 分组**。BGM/SFX 各走一个 `AudioSource.volume`，没有 LowPass/Compressor 组，因此"污染度影响混音"这类效果无法实现（详见 `AudioDesign.md` P1 节）。

### 2.6 FrameworkSelfTest（开发期）

- `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` + `#if UNITY_EDITOR` 包裹，**只在编辑器跑**，不进 Build。
- 自检 5 项：事件订阅/发布、池取还、存档读写删、BGM 音量、SFX 库完整性。
- 结果打 Console，不阻塞流程。

---

## 3. 数据流

### 3.1 一次事件发布（最核心的路径）

```
Publisher.Publish(new PurityChangedEvent{...})
  → EventCenter._map[typeof(PurityChangedEvent)] 取 List<Delegate>
  → 倒序 for：
       handler = list[i] as Action<PurityChangedEvent>
       target  = handler.Target as UnityEngine.Object
       if (target == null && handler.Target is UnityEngine.Object) → list.RemoveAt(i); continue
       handler.Invoke(evt)
  → 返回（同步，调用方继续执行）
```

**关键性质**：`Publish` 返回时所有订阅者已执行完。所以"发事件之后立刻读某个订阅者改过的状态"是合法的——但也意味着**订阅者的异常会中断发布者的流程**（项目里订阅者都用 try 之外的方式规避，未加统一 try/catch）。

### 3.2 一次对象池取还

```
PoolManager.Get<EnemyProjectile>(prefab)
  → 查 Dictionary<GameObject, object>：无则 new ObjectPool<EnemyProjectile>(prefab)
  → 池内 Stack 有 → Pop + SetActive(true)
  → 池内空 → Instantiate(prefab) 挂到池根节点下
  → 记入 _borrowed

PoolManager.Release(prefab, obj)
  → 若 obj 不在 _borrowed → 丢弃（防重复）
  → SetActive(false) + 移出 _borrowed + Push 回 Stack
```

### 3.3 一次存档往返

```
SaveSystem.Save("settings", data)
  → JsonUtility.ToJson(data) → File.WriteAllText(persistentDataPath/settings.json)

SaveSystem.Load<GameSettingsData>("settings")
  → 文件不存在 → new GameSettingsData()
  → try { JsonUtility.FromJson<T>(text) } catch { new GameSettingsData() }
```

---

## 4. 设计取舍

| 取舍 | 选择 | 理由 | 代价 |
|---|---|---|---|
| 单例 vs 依赖注入 | **懒加载单例** | 毕设体量下 DI 容器是过度设计；且单例让"任何脚本任何时刻可用"，符合 Unity 工作流 | 全局状态不可测；`LevelManager` 在 Title 场景为 null（已在 SystemOverview §3.1 标注） |
| 事件中心 vs 直接引用 | **事件中心** | 论文可写"观察者模式"；实际收益是 `Eco` 与 `EnemyAI` 零互相引用 | 20 个事件里 6 个孤儿——事件容易发了没人接，且编译器不会报错 |
| 静态 vs 单例 | `EventCenter` / `SaveSystem` 用**静态类** | 它们无生命周期需求（不需要 `Update`、不需要 GameObject），静态更轻 | 无法在 Inspector 里配；编辑器中静态状态跨 Play 残留（`EventCenter` 有 `ClearAll` 但只在测试用） |
| JsonUtility vs Newtonsoft | **JsonUtility** | 内置零依赖，答辩不会被问"用了多少第三方库" | 不支持 `Dictionary`、不支持多态、无版本迁移 |
| 池 vs 直接 Instantiate | **池化**（弹体/FX/碎块） | 性能红线"每帧零 GC"的前提 | 池对象状态必须手动重置，漏一个字段就会"复用的弹体带着上次的伤害方向飞" |

---

## 5. 风险

| # | 风险 | 触发条件 | 当前对策 |
|---|---|---|---|
| 1 | 单例在场景切换时被销毁 | `DontDestroyOnLoad` **没有调用**——所有单例都随场景卸载销毁 | 懒加载会在下次访问时重建；但 `AudioManager` 重建会**丢 BGM 播放位置**（切场景音乐从头开始）。当前接受 |
| 2 | `EventCenter` 跨场景残留订阅 | 场景卸载时对象被 Destroy，但静态字典里的 `Delegate` 仍在 | 靠"Unity 伪 null 清理"兜底；未订阅 `sceneUnloaded` 主动清空 |
| 3 | 存档结构变更导致旧档异常 | 改 `[Serializable]` 类字段 | 无版本号、无迁移，只能手删档 |
| 4 | 音效缺失静默 | `GameAudioLibrary` 对应项为空 | 无警告；靠 `FrameworkSelfTest` 在编辑器启动时检查完整性 |
| 5 | `PoolManager` 按预制体引用分池，同预制体不同配置共用一池 | 运行时改预制体参数 | 池对象 `Get` 时调用方需自行 `Reset`；项目里每个使用方都实现了 `IResettable` 风格的 `OnSpawn` |

---

## 6. 与 requirements 的偏差说明

| 编号 | requirements 原文 | 实现 | 说明 |
|---|---|---|---|
| R1 | 提供全局单例基类，首次访问自动创建 | ✅ 一致 | 额外加了 `_quitting` 与域重载处理（原需求未提） |
| R2 | 提供类型安全的事件发布订阅，支持取消订阅 | ✅ 一致 | 额外加了"Unity 伪 null 自动清理"（原需求未提，实测必需） |
| R3 | 提供按预制体复用的对象池 | ✅ 一致 | `PoolManager` 的对外 API 是泛型的，内部用 `object` 装箱——原需求未规定，属实现细节 |
| R4 | 提供 JSON 存档，4 个槽 | ✅ 一致 | 原需求写"3 槽"，实际 4 槽（多一个 `level01_result`），已回写 requirements |
| R5 | 提供 BGM + SFX 双通道音频 | ✅ 一致 | M10 扩展为 `SfxId` 枚举 + `GameAudioLibrary` SO，原需求只要求"能播" |
| — | （原需求未提） | `FrameworkSelfTest` | M1 无场景对象，没有它无法验证；现已作为回归工具保留 |
