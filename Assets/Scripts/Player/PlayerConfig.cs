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

        [Header("闪避（翻滚）")]
        [Tooltip("翻滚总时长 (s)，与 Roll 动画等长")]
        public float DodgeDuration = 0.333f;

        [Tooltip("翻滚总位移距离 (u)")]
        public float DodgeDistance = 3f;

        [Tooltip("翻滚期间无敌时长 (s)")]
        public float DodgeInvincibleTime = 0.33f;

        [Tooltip("翻滚冷却 (s)")]
        public float DodgeCooldown = 0.6f;

        [Tooltip("空中每段允许的翻滚次数（落地重置）")]
        public int AirDodgeCount = 1;

        [Tooltip("翻滚穿透层：翻滚期间本体不与该层发生物理碰撞（可穿过怪物、不被顶回）")]
        public LayerMask DodgePassThroughLayers = 1 << 9;

        /// <summary>翻滚水平速度（由距离/时长推导，避免两处手填不一致）。</summary>
        public float DodgeSpeed => DodgeDuration > 0f ? DodgeDistance / DodgeDuration : 0f;

        [Header("碰撞")]
        [Tooltip("地面层掩码（默认 Ground）")]
        public LayerMask GroundLayer = 1 << 10;

        [Tooltip("地面检测射线额外长度")]
        public float GroundCheckSkin = 0.05f;

        /// <summary>配置资产缺失时的代码兜底（保证游戏不因漏配而崩溃）。</summary>
        public static PlayerConfig CreateDefault()
        {
            var config = CreateInstance<PlayerConfig>();
            config.GroundLayer = 1 << 10;
            Debug.LogWarning("[PlayerConfig] 未找到配置资产，已使用代码默认值。");
            return config;
        }
    }
}
