using UnityEngine;

namespace StarEco.Eco
{
    /// <summary>
    /// 可净化目标统一接口：垃圾堆（GarbagePile）、塔适配器（TowerPurifiable）等实现。
    /// 净化与攻击是两条通道：净化走本接口，喷壶对污染生物的伤害走 StarEco.Combat.IHittable。
    /// </summary>
    public interface IPurifiable
    {
        /// <summary>该目标的标识（如 GarbagePile / WaterBlock / Tower01），用于事件携带与检查点快照对齐。</summary>
        string PurifyTargetId { get; }

        /// <summary>接收一次净化量（amount ≥ 0）。已净化的目标应幂等忽略。</summary>
        void ReceivePurify(int amount, GameObject source);

        /// <summary>是否已完成净化（完成后不再参与任何净化结算）。</summary>
        bool IsPurified { get; }

        /// <summary>净化完成后贡献的净化值（计入 PuritySystem 总量）。</summary>
        int PurityValue { get; }
    }
}
