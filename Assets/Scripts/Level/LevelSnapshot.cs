using System;
using UnityEngine;

namespace StarEco.Level
{
    /// <summary>
    /// 关卡快照（检查点单槽位，R4）：玩家状态 + 统计基准。
    /// 注意：不含世界净化值 —— 已净化目标不回滚（design §2.4），
    /// 且 PuritySystem.Current 只增不减，扣减会与已净化目标数量矛盾。
    /// </summary>
    [Serializable]
    public class LevelSnapshot
    {
        /// <summary>玩家位置。</summary>
        public Vector2 PlayerPosition;

        /// <summary>玩家 HP。</summary>
        public int PlayerHP;

        /// <summary>净化剂余量。</summary>
        public int PurifyTank;

        /// <summary>击杀数。</summary>
        public int KillCount;

        /// <summary>拾取数。</summary>
        public int PickupCount;

        /// <summary>提交快照时的已耗时。</summary>
        public float ElapsedSeconds;

        /// <summary>提交快照的检查点 Id（调试/日志用）。</summary>
        public string CheckpointId;
    }
}
