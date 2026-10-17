using UnityEngine;

namespace StarEco.Boss
{
    /// <summary>
    /// Boss 配置（M8 R1）：继承 M4 `EnemyConfig`，复用 HP/移速/攻击/感知/弹幕等通用字段，
    /// 额外提供阶段阈值、双技能、特殊招与净化倍率。
    /// 创建：Assets → Create → StarEco → Boss Config
    /// </summary>
    [CreateAssetMenu(fileName = "BossConfig", menuName = "StarEco/Boss Config", order = 3)]
    public class BossConfig : StarEco.EnemyAI.EnemyConfig
    {
        [Header("阶段阈值（HP 百分比）")]
        [Tooltip("降到该百分比以下切入第二阶段（过场 + 狂暴）")]
        [Range(1f, 99f)] public float Phase2HpPercent = 60f;

        [Tooltip("降到该百分比以下切入第三阶段（过场 + 最终形态）")]
        [Range(1f, 99f)] public float Phase3HpPercent = 30f;

        [Tooltip("阶段切换过场时长（秒），期间无敌")]
        public float PhaseTransitionSeconds = 0.8f;

        [Header("技能 A · 近战")]
        [Tooltip("近战触发距离")]
        public float AttackARange = 2.2f;

        [Tooltip("近战冷却（秒），实际间隔再乘阶段系数")]
        public float AttackAInterval = 2.0f;

        [Header("技能 B · 弹幕")]
        [Tooltip("弹幕冷却（秒）")]
        public float AttackBInterval = 3.0f;

        [Tooltip("一次发射的弹数（P1 起）")]
        public int ProjectileCount = 3;

        [Tooltip("扇形弹幕张角（度）")]
        public float ProjectileSpreadDegrees = 24f;

        [Header("特殊招（仅 P3）")]
        [Tooltip("特殊招冷却（秒）")]
        public float SpecialInterval = 6.0f;

        [Tooltip("特殊招弹数（环绕扇形）")]
        public int SpecialProjectileCount = 5;

        [Tooltip("特殊招张角（度）")]
        public float SpecialSpreadDegrees = 140f;

        [Header("狂暴成长")]
        [Tooltip("P2/P3 移速倍率")]
        public float EnrageSpeedMultiplier = 1.3f;

        [Tooltip("P2 攻击间隔倍率")]
        public float Phase2IntervalScale = 0.8f;

        [Tooltip("P3 攻击间隔倍率")]
        public float Phase3IntervalScale = 0.7f;

        [Header("净化伤害")]
        [Tooltip("净化喷雾对 Boss 的伤害倍率（M8 R4：两种伤害都吃，喷雾加倍）")]
        public float PurifyDamageMultiplier = 1.5f;

        /// <summary>配置缺失时的代码兜底（GDD §八 数值表初值）。</summary>
        public static new BossConfig CreateDefault()
        {
            var config = CreateInstance<BossConfig>();
            config.EnemyTypeId = "Boss01";
            config.MaxHP = 30;
            Debug.LogWarning("[BossConfig] 未找到配置资产，已使用代码默认值（HP 30 / 60% / 30%）。");
            return config;
        }
    }
}
