using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarEco.Level
{
    /// <summary>
    /// 检查点快照：记录关卡中所有单向运行时状态，用于玩家死亡后回溯。
    /// 字段全部可序列化，便于后续扩展为磁盘存档。
    /// </summary>
    [Serializable]
    public class CheckpointSnapshot
    {
        /// <summary>玩家在检查点触发时的世界位置。</summary>
        public Vector3 PlayerPosition;

        /// <summary>净化系统当前净化值。</summary>
        public int PurityCurrent;

        /// <summary>目标链当前序号（-1 = 未开始/链已完成）。</summary>
        public int ObjectiveIndex;

        /// <summary>目标链是否全部完成。</summary>
        public bool ChainCompleted;

        /// <summary>本局已耗时（秒）。</summary>
        public float ElapsedSeconds;

        /// <summary>本局击杀数。</summary>
        public int KillCount;

        /// <summary>本局拾取物数量。</summary>
        public int PickupCount;

        /// <summary>各一次性波次是否已触发（与 LevelConfig.SpawnWaves 一一对应）。</summary>
        public bool[] WaveTriggered;

        /// <summary>各波次各敌人条目剩余生成数（展平为一维，与 WaveEntryIndexFlat / WaveIndexFlat 一一对应）。</summary>
        public int[] WaveRemainingFlat;

        /// <summary>与 WaveRemainingFlat 对应的敌人条目序号（EntryIndices 中的位置，非实际条目索引）。</summary>
        public int[] WaveEntryIndexFlat;

        /// <summary>与 WaveRemainingFlat 对应的波次序号。</summary>
        public int[] WaveIndexFlat;

        /// <summary>右岸是否已到达。</summary>
        public bool RightBankReached;

        /// <summary>右岸区域是否已净化。</summary>
        public bool RightBankTargetPurified;

        /// <summary>各敌人条目当前同时存活数。</summary>
        public int[] AliveCounts;

        /// <summary>各敌人条目累计生成数（含重生）。</summary>
        public int[] SpawnedTotals;

        /// <summary>已净化的 IPurifiable 目标标识集合（GarbagePile / WaterBlockage / TowerPurifiable TargetId）。</summary>
        public List<string> PurifiedTargetIds;

        /// <summary>已解救的 RescueTarget 目标标识集合。</summary>
        public List<string> RescuedTargetIds;

        /// <summary>已关闭的 Tower01Controller 塔标识集合（EnemyTypeId）。</summary>
        public List<string> DisabledTowerIds;

        public CheckpointSnapshot()
        {
            PurifiedTargetIds = new List<string>();
            RescuedTargetIds = new List<string>();
            DisabledTowerIds = new List<string>();
        }
    }
}
