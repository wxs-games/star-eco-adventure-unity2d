using UnityEngine;

namespace StarEco.Combat
{
    /// <summary>
    /// 统一伤害数据：任何攻击（玩家夹钳、M5 净化喷雾、M4 敌人反击）命中时携带的信息。
    /// </summary>
    public struct DamageInfo
    {
        /// <summary>伤害值。</summary>
        public int Amount;

        /// <summary>指向伤害来源的方向（受击者 → 来源），击退按其反方向施加。</summary>
        public Vector2 SourceDirection;

        /// <summary>伤害来源对象（用于计算击退方向与归属，可为 null）。</summary>
        public GameObject Source;

        /// <summary>附带击退速度（0 = 不附带，受击方自行决定击退）。</summary>
        public float KnockbackPower;

        public DamageInfo(int amount, Vector2 sourceDirection, GameObject source)
        {
            Amount = amount;
            SourceDirection = sourceDirection;
            Source = source;
            KnockbackPower = 0f;
        }
    }
}
