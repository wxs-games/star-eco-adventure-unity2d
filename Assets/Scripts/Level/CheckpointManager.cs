using System;
using System.Collections.Generic;
using StarEco.Eco;
using StarEco.Eco.Events;
using StarEco.EnemyAI;
using StarEco.Framework;
using StarEco.Level.Events;
using StarEco.Player;
using UnityEngine;

namespace StarEco.Level
{
    /// <summary>
    /// 检查点管理器（场景级单例）：捕获关卡全部可恢复运行时状态，在玩家死亡时把
    /// 关卡回滚到最近一次检查点，而非直接进入失败结算。
    ///
    /// 捕获范围（单向运行时状态）：
    ///   • 玩家世界位置
    ///   • PuritySystem.Current（净化当前值）
    ///   • LevelObjectiveTracker 当前目标序号与是否链完成
    ///   • LevelStats 耗时/击杀/拾取（回滚后从检查点时点重新累积）
    ///   • LevelSpawner 触发过的波次、各条目剩余/已生成/存活数、右岸到达与净化状态
    ///   • 已净化 IPurifiable 目标（GarbagePile / WaterBlockage / TowerPurifiable，按 TargetId）
    ///   • 已解救 RescueTarget（按 TargetId）
    ///   • 已关闭 Tower01Controller（按 EnemyTypeId/name）
    ///
    /// 恢复策略：先把全部可恢复对象重置为“未完成”态，再按快照重新标记已完成者，
    /// 保证不遗留半完成脏状态。静态配置（LevelConfig、预制体引用）不保存，
    /// 恢复时沿用当前场景配置。
    ///
    /// 使用流程（L2）：
    /// 1. 场景中放置一个或多个 CheckpointTrigger（如木筏解锁后的右岸入口处）。
    /// 2. 玩家触发检查点 → CheckpointManager.Save(CaptureAll())。
    /// 3. 玩家死亡 → LevelManager 检测 IsDead，若 HasCheckpoint 则 RestoreAll +
    ///    RestorePlayerAt，继续 Playing；否则走原有 FinishLevel(false)。
    /// </summary>
    public class CheckpointManager : MonoSingleton<CheckpointManager>
    {
        private CheckpointSnapshot _snapshot;
        private StarEco.Player.PlayerController _player;
        private PuritySystem _purity;
        private LevelObjectiveTracker _tracker;
        private LevelStats _stats;
        private LevelSpawner _spawner;

        /// <summary>当前是否存在可用的检查点快照。</summary>
        public bool HasCheckpoint => _snapshot != null;

        /// <summary>最近一次保存的快照（供 LevelManager 读取并回滚）。</summary>
        public CheckpointSnapshot SavedSnapshot => _snapshot;

        protected override void OnSingletonAwake()
        {
            _snapshot = null;
        }

        protected override void OnDestroy()
        {
            _snapshot = null;
        }

        // ===== 对外 API =====

        /// <summary>保存快照（CheckpointTrigger 调用）。</summary>
        public void Save(CheckpointSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            _snapshot = snapshot;
            Debug.Log($"[CheckpointManager] 已保存检查点（目标序号={_snapshot.ObjectiveIndex}，净化={_snapshot.PurityCurrent}）");
        }

        /// <summary>清空快照（关卡重开或通关后调用）。</summary>
        public void Clear()
        {
            _snapshot = null;
        }

        /// <summary>捕获当前场景全部可恢复状态并返回快照（不自动保存）。</summary>
        public CheckpointSnapshot CaptureAll()
        {
            ResolveDependencies();

            var snap = new CheckpointSnapshot
            {
                PlayerPosition = _player != null ? _player.transform.position : Vector3.zero,
                PurityCurrent = _purity != null ? _purity.Current : 0,
                ObjectiveIndex = _tracker != null ? _tracker.CurrentIndex : -1,
                ChainCompleted = _tracker != null && _tracker.ChainCompleted,
                ElapsedSeconds = _stats != null ? _stats.ElapsedSeconds : 0f,
                KillCount = _stats != null ? _stats.KillCount : 0,
                PickupCount = _stats != null ? _stats.PickupCount : 0,
            };

            CaptureSpawnerState(snap);
            CapturePurifiableStates(snap);
            CaptureRescueStates(snap);
            CaptureTowerStates(snap);

            return snap;
        }

        /// <summary>把快照恢复到当前场景（LevelManager 在死亡回滚时调用）。</summary>
        public void RestoreAll(CheckpointSnapshot snap)
        {
            if (snap == null)
            {
                Debug.LogWarning("[CheckpointManager] RestoreAll 收到 null 快照，已忽略。");
                return;
            }

            ResolveDependencies();

            // 1. 重置全部可恢复对象到未完成态
            ResetPurifiables();
            ResetRescues();
            ResetTowers();

            // 2. 恢复净化系统：归零后把已净化目标重新净化（ReceivePurify 幂等，补足进度即触发 CompletePurify）
            if (_purity != null)
            {
                _purity.ResetProgress();
            }
            RestorePurifiedTargets(snap.PurifiedTargetIds);

            // 3. 恢复已解救与已关闭目标
            RestoreRescuedTargets(snap.RescuedTargetIds);
            RestoreDisabledTowers(snap.DisabledTowerIds);

            // 4. 恢复目标链索引与链完成标志
            if (_tracker != null)
            {
                _tracker.ResetChain();
                SetTrackerIndex(_tracker, snap.ObjectiveIndex);
                var chainField = typeof(LevelObjectiveTracker).GetField("ChainCompleted",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                chainField?.SetValue(_tracker, snap.ChainCompleted);
            }

            // 5. 统计值从检查点时点重新累积（不直接回写击杀/拾取，避免与 LevelStats 事件订阅冲突）
            if (_stats != null)
            {
                _stats.Reset();
            }

            // 6. 恢复刷怪器波次状态
            RestoreSpawnerState(snap);

            _snapshot = snap;
            Debug.Log("[CheckpointManager] 已回滚至检查点快照。");
        }

        /// <summary>把玩家瞬移到快照记录的位置，解锁输入并复位死亡标志。</summary>
        public void RestorePlayerAt(CheckpointSnapshot snap)
        {
            if (_player == null)
            {
                ResolveDependencies();
            }

            if (_player != null)
            {
                _player.transform.position = snap.PlayerPosition;
                _player.InputLocked = false;
                _player.ResetAfterCheckpoint();
            }
        }

        // ===== 依赖解析 =====

        private void ResolveDependencies()
        {
            if (_player == null) _player = FindObjectOfType<StarEco.Player.PlayerController>();
            if (_purity == null) _purity = FindObjectOfType<PuritySystem>();
            if (_tracker == null) _tracker = FindObjectOfType<LevelObjectiveTracker>();
            if (_stats == null) _stats = FindObjectOfType<LevelStats>();
            if (_spawner == null) _spawner = FindObjectOfType<LevelSpawner>();
        }

        // ===== 私有：捕获子状态 =====

        private void CaptureSpawnerState(CheckpointSnapshot snap)
        {
            if (_spawner == null) return;

            var waveTriggered = GetPrivateField<bool[]>(_spawner, "_waveTriggered");
            if (waveTriggered != null)
            {
                snap.WaveTriggered = (bool[])waveTriggered.Clone();
            }

            var waveRemaining = GetPrivateField<int[][]>(_spawner, "_waveRemaining");
            if (waveRemaining != null)
            {
                var flatRemaining = new List<int>();
                var flatEntry = new List<int>();
                var flatWave = new List<int>();
                for (int w = 0; w < waveRemaining.Length; w++)
                {
                    if (waveRemaining[w] == null) continue;
                    for (int e = 0; e < waveRemaining[w].Length; e++)
                    {
                        flatRemaining.Add(waveRemaining[w][e]);
                        flatEntry.Add(e);
                        flatWave.Add(w);
                    }
                }
                snap.WaveRemainingFlat = flatRemaining.ToArray();
                snap.WaveEntryIndexFlat = flatEntry.ToArray();
                snap.WaveIndexFlat = flatWave.ToArray();
            }

            snap.RightBankReached = GetPrivateField<bool>(_spawner, "_rightBankReached");
            snap.RightBankTargetPurified = GetPrivateField<bool>(_spawner, "_rightBankTargetPurified");

            var aliveCounts = GetPrivateField<int[]>(_spawner, "_aliveCounts");
            if (aliveCounts != null) snap.AliveCounts = (int[])aliveCounts.Clone();

            var spawnedTotals = GetPrivateField<int[]>(_spawner, "_spawnedTotals");
            if (spawnedTotals != null) snap.SpawnedTotals = (int[])spawnedTotals.Clone();
        }

        private void CapturePurifiableStates(CheckpointSnapshot snap)
        {
            var behaviours = FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var p = behaviours[i] as IPurifiable;
                if (p == null || string.IsNullOrEmpty(p.PurifyTargetId)) continue;
                if (p.IsPurified) snap.PurifiedTargetIds.Add(p.PurifyTargetId);
            }
        }

        private void CaptureRescueStates(CheckpointSnapshot snap)
        {
            var behaviours = FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var r = behaviours[i] as RescueTarget;
                if (r == null || string.IsNullOrEmpty(r.RestorableId)) continue;
                if (r.IsRescued) snap.RescuedTargetIds.Add(r.RestorableId);
            }
        }

        private void CaptureTowerStates(CheckpointSnapshot snap)
        {
            var behaviours = FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var t = behaviours[i] as Tower01Controller;
                if (t == null || string.IsNullOrEmpty(t.RestorableId)) continue;
                if (t.IsDisabled) snap.DisabledTowerIds.Add(t.RestorableId);
            }
        }

        // ===== 私有：恢复子状态 =====

        private void RestoreSpawnerState(CheckpointSnapshot snap)
        {
            if (_spawner == null) return;

            if (snap.WaveTriggered != null)
            {
                SetPrivateField(_spawner, "_waveTriggered", (bool[])snap.WaveTriggered.Clone());
            }

            if (snap.WaveRemainingFlat != null && snap.WaveEntryIndexFlat != null && snap.WaveIndexFlat != null)
            {
                var currentRemaining = GetPrivateField<int[][]>(_spawner, "_waveRemaining");
                if (currentRemaining != null)
                {
                    for (int i = 0; i < snap.WaveRemainingFlat.Length; i++)
                    {
                        int w = snap.WaveIndexFlat[i];
                        int e = snap.WaveEntryIndexFlat[i];
                        if (w < currentRemaining.Length && currentRemaining[w] != null &&
                            e < currentRemaining[w].Length)
                        {
                            currentRemaining[w][e] = snap.WaveRemainingFlat[i];
                        }
                    }
                }
            }

            SetPrivateField(_spawner, "_rightBankReached", snap.RightBankReached);
            SetPrivateField(_spawner, "_rightBankTargetPurified", snap.RightBankTargetPurified);

            if (snap.AliveCounts != null)
            {
                SetPrivateField(_spawner, "_aliveCounts", (int[])snap.AliveCounts.Clone());
            }
            if (snap.SpawnedTotals != null)
            {
                SetPrivateField(_spawner, "_spawnedTotals", (int[])snap.SpawnedTotals.Clone());
            }
        }

        private void RestorePurifiedTargets(List<string> targetIds)
        {
            if (targetIds == null || _purity == null) return;

            var behaviours = FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var p = behaviours[i] as IPurifiable;
                if (p == null || string.IsNullOrEmpty(p.PurifyTargetId)) continue;
                if (targetIds.Contains(p.PurifyTargetId) && !p.IsPurified)
                {
                    p.ReceivePurify(p.PurityValue, null);
                }
            }
        }

        private void RestoreRescuedTargets(List<string> targetIds)
        {
            if (targetIds == null) return;

            var behaviours = FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var r = behaviours[i] as RescueTarget;
                if (r == null || string.IsNullOrEmpty(r.RestorableId)) continue;
                if (targetIds.Contains(r.RestorableId) && !r.IsRescued)
                {
                    r.ForceCompleteRescue();
                }
            }
        }

        private void RestoreDisabledTowers(List<string> towerIds)
        {
            if (towerIds == null) return;

            var behaviours = FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var t = behaviours[i] as Tower01Controller;
                if (t == null || string.IsNullOrEmpty(t.RestorableId)) continue;
                if (towerIds.Contains(t.RestorableId) && !t.IsDisabled)
                {
                    t.RequestDisable();
                }
            }
        }

        private void ResetPurifiables()
        {
            var behaviours = FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var p = behaviours[i] as IResettablePurifiable;
                if (p != null) p.ResetPurify();
            }
        }

        private void ResetRescues()
        {
            var behaviours = FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var r = behaviours[i] as RescueTarget;
                if (r != null) r.ResetRescue();
            }
        }

        private void ResetTowers()
        {
            var behaviours = FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var t = behaviours[i] as Tower01Controller;
                if (t != null) t.ResetDisableForCheckpoint();
            }
        }

        // ===== 工具：反射读写私有字段（LevelSpawner 的波次状态不改公开 API）=====

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field != null ? (T)field.GetValue(target) : default;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }

        private static void SetTrackerIndex(LevelObjectiveTracker tracker, int index)
        {
            var field = typeof(LevelObjectiveTracker).GetField("_index",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(tracker, Mathf.Clamp(index, -1, 99));
        }
    }
}
