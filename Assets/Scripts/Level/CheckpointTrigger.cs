using UnityEngine;

namespace StarEco.Level
{
    /// <summary>
    /// 检查点触发器：场景中摆放的触发体，玩家进入后把当前关卡全部可恢复状态
    /// 保存到 CheckpointManager。供 L2 中段检查点使用（如木筏解锁后的右岸入口处）。
    ///
    /// 使用：
    /// 1. 将本组件挂到场景中一个带 2D 触发器碰撞体的空对象（如 Checkpoint_RightBank）。
    /// 2. 把碰撞体设为 IsTrigger = true，体积覆盖玩家可经过的区域。
    /// 3. 玩家角色需带 Tag = "Player"，触发后自动保存快照并可选播放视觉反馈。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class CheckpointTrigger : MonoBehaviour
    {
        [Tooltip("检查点标识，仅用于调试日志。同一关卡多个检查点可用不同标识区分。")]
        [SerializeField] private string _checkpointId = "checkpoint";

        [Tooltip("触发后是否播放保存反馈（简易闪烁，由子类或外部系统扩展）。")]
        [SerializeField] private bool _playFeedback = true;

        private bool _triggered;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_triggered) return;
            if (!other.CompareTag("Player")) return;

            var manager = CheckpointManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning($"[CheckpointTrigger] 场景中未找到 CheckpointManager，快照未保存（{_checkpointId}）。");
                return;
            }

            var snapshot = manager.CaptureAll();
            manager.Save(snapshot);
            _triggered = true;

            if (_playFeedback)
            {
                Debug.Log($"[CheckpointTrigger] 已触发检查点 [{_checkpointId}]，已保存关卡状态。");
            }
        }
    }
}
