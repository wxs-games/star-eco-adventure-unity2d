using UnityEngine;

namespace StarEco.Player
{
    /// <summary>
    /// 玩家手感参数配置（ScriptableObject 数据驱动）。
    /// 创建：Assets → Create → StarEco → Player Config
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerConfig", menuName = "StarEco/Player Config", order = 0)]
    public class PlayerConfig : ScriptableObject
    {
        [Header("移动")]
        [Tooltip("地面水平移速 (u/s)")]
        public float MoveSpeed = 6f;

        [Tooltip("空中水平控制力比例（1 = 与地面相同）")]
        [Range(0f, 1.5f)] public float AirControlRatio = 0.85f;

        [Tooltip("水平加速平滑（越大越跟手）")]
        public float Acceleration = 60f;

        [Header("跳跃")]
        [Tooltip("起跳初速度 (u/s)")]
        public float JumpVelocity = 12f;

        [Tooltip("松开跳跃键时的速度衰减比例（可变跳高，0.5 = 短跳）")]
        [Range(0.1f, 1f)] public float JumpCutMultiplier = 0.5f;

        [Header("重力")]
        [Tooltip("常规重力倍率")]
        public float GravityScale = 3f;

        [Tooltip("下落重力倍率（相对 GravityScale 的乘数，下落更快更利落）")]
        public float FallGravityMultiplier = 1.6f;

        [Tooltip("最大下落速度 (u/s)")]
        public float MaxFallSpeed = 20f;

        [Header("手感窗口")]
        [Tooltip("土狼时间：离地后仍可起跳的窗口 (s)")]
        public float CoyoteTime = 0.1f;

        [Tooltip("跳跃缓冲：落地前按跳的预输入窗口 (s)")]
        public float JumpBufferTime = 0.1f;

        [Header("落地")]
        [Tooltip("落地动画锁定的时长 (s)")]
        public float LandLockTime = 0.06f;

        [Header("受击")]
        [Tooltip("受击无敌时长 (s)")]
        public float HurtInvincibleTime = 1f;

        [Tooltip("受击击退速度 (u/s)")]
        public float HurtKnockback = 6f;

        [Header("碰撞")]
        [Tooltip("地面层掩码（默认 Ground）")]
        public LayerMask GroundLayer = ~0;

        [Tooltip("地面检测射线额外长度")]
        public float GroundCheckSkin = 0.05f;

        /// <summary>配置资产缺失时的代码兜底（保证游戏不因漏配而崩溃）。</summary>
        public static PlayerConfig CreateDefault()
        {
            var config = CreateInstance<PlayerConfig>();
            Debug.LogWarning("[PlayerConfig] 未找到配置资产，已使用代码默认值。");
            return config;
        }
    }
}
