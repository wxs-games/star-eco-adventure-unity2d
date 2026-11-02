using UnityEngine;

namespace StarEco.Level
{
    /// <summary>
    /// 可在检查点快照中保存/恢复运行时状态的组件接口。
    /// 实现此接口的组件会在 CheckpointManager.CaptureAll / RestoreAll 时被自动发现。
    /// RestorableId 必须在同一关卡场景中唯一，用于快照里的键值对齐。
    /// </summary>
    public interface ICheckpointRestorable
    {
        /// <summary>该实例在关卡内的唯一标识（通常是对象名或 TargetId）。</summary>
        string RestorableId { get; }

        /// <summary>把当前运行时状态打包为可序列化的对象。</summary>
        object CaptureState();

        /// <summary>把之前 CaptureState 打包的状态恢复到本实例。</summary>
        void RestoreState(object state);
    }
}
