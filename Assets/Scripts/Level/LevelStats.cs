using UnityEngine;
using StarEco.Framework;
using StarEco.Eco.Events;
using StarEco.EnemyAI.Events;

namespace StarEco.Level
{
    /// <summary>
    /// 关卡内统计（击杀/拾取/耗时）：被动订阅 M4/M5 事件流计数，不轮询场景（R8）。
    /// 生命周期由 LevelManager 持有并在 Restart 时 Reset。
    /// 耗时用内部累加（不用 timeSinceLevelLoad：同场景重开时它不归零，会让计时错乱）。
    /// </summary>
    public class LevelStats : MonoBehaviour
    {
        private float _accumulated;

        /// <summary>本局击杀数。</summary>
        public int KillCount { get; private set; }

        /// <summary>本局拾取物数量。</summary>
        public int PickupCount { get; private set; }

        /// <summary>检查点快照中的耗时基准（恢复后继续累加，见 design §2.4）。</summary>
        public float SnapshotElapsed { get; private set; }

        /// <summary>本局已耗时（秒）= 快照基准 + 本次存活时长。</summary>
        public float ElapsedSeconds => SnapshotElapsed + _accumulated;

        private void OnEnable()
        {
            EventCenter.Subscribe<EnemyDeathEvent>(OnEnemyDied);
            EventCenter.Subscribe<PickupCollectedEvent>(OnPickupCollected);
        }

        private void OnDisable()
        {
            EventCenter.Unsubscribe<EnemyDeathEvent>(OnEnemyDied);
            EventCenter.Unsubscribe<PickupCollectedEvent>(OnPickupCollected);
        }

        private void Start()
        {
            Reset();
        }

        private void Update()
        {
            _accumulated += Time.deltaTime;
        }

        /// <summary>归零全部统计（R8：重启关卡）。</summary>
        public void Reset()
        {
            KillCount = 0;
            PickupCount = 0;
            SnapshotElapsed = 0f;
            _accumulated = 0f;
        }

        /// <summary>检查点提交快照：把当前耗时记为基准（R4）。</summary>
        public void CommitSnapshotElapsed()
        {
            SnapshotElapsed = ElapsedSeconds;
            _accumulated = 0f;
        }

        /// <summary>把快照中的统计值写回（检查点恢复时用）。</summary>
        public void RestoreFromSnapshot(int kills, int pickups, float elapsed)
        {
            KillCount = Mathf.Max(0, kills);
            PickupCount = Mathf.Max(0, pickups);
            SnapshotElapsed = Mathf.Max(0f, elapsed);
            _accumulated = 0f;
        }

        private void OnEnemyDied(EnemyDeathEvent evt)
        {
            KillCount++;
        }

        private void OnPickupCollected(PickupCollectedEvent evt)
        {
            PickupCount++;
        }
    }
}
