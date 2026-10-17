using UnityEngine;
using StarEco.Framework;
using StarEco.Level.Events;
using StarEco.Player;

namespace StarEco.Level
{
    /// <summary>
    /// 检查点（R4）：Trigger 区域，玩家首次进入时向 LevelManager 提交快照并广播激活事件。
    /// Collider2D 必须是 IsTrigger，且挂在本物体上（OnTriggerEnter2D 只达自身组件）。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Checkpoint : MonoBehaviour
    {
        [Tooltip("检查点唯一标识（同一关内重复进入只提交一次）")]
        [SerializeField] private string _checkpointId = "CP_1";

        [Tooltip("玩家恢复时的位置偏移（通常把玩家抬到地面上方一点）")]
        [SerializeField] private Vector2 _respawnOffset = new Vector2(0f, 1f);

        private Collider2D _trigger;
        private bool _submitted;

        /// <summary>检查点 Id。</summary>
        public string CheckpointId => _checkpointId;

        /// <summary>恢复传送位置。</summary>
        public Vector2 RespawnPosition => (Vector2)transform.position + _respawnOffset;

        private void Awake()
        {
            _trigger = GetComponent<Collider2D>();
            _trigger.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_submitted)
            {
                return;
            }

            // 玩家控制器可能在父物体上（Player 结构约定）
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null)
            {
                return;
            }

            _submitted = true;

            AudioManager.Instance?.PlaySfx(SfxId.Checkpoint);   // M10

            if (LevelManager.HasInstance)
            {
                LevelManager.Instance.CommitCheckpoint(this);
            }

            EventCenter.Publish(new CheckpointActivatedEvent
            {
                CheckpointId = _checkpointId,
                Position = RespawnPosition
            });
        }

        /// <summary>重置提交状态（关卡重开时由 LevelManager 调用，让检查点可再次触发）。</summary>
        public void ResetSubmission()
        {
            _submitted = false;
        }
    }
}
