using UnityEngine;

namespace StarEco.Level.Events
{
    /// <summary>
    /// 检查点激活事件：Checkpoint 广播，供 UI/音效消费（R4）。
    /// struct 零 GC。
    /// </summary>
    public struct CheckpointActivatedEvent
    {
        /// <summary>检查点唯一标识。</summary>
        public string CheckpointId;

        /// <summary>检查点位置（恢复传送点）。</summary>
        public Vector2 Position;
    }
}
