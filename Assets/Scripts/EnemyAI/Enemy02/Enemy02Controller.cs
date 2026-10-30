using UnityEngine;
using StarEco.Eco;
using StarEco.Framework;

namespace StarEco.EnemyAI
{
    /// <summary>
    /// Enemy02 变异毒植物：远程毒气。注册共享状态 Idle/Chase/Attack(Ranged)/Hurt/Die。
    /// HitOn 事件帧取 FirePoint 生成毒气弹体。
    /// </summary>
    public class Enemy02Controller : EnemyControllerBase
    {
        [SerializeField] private Transform _firePoint;
        [SerializeField] private EnemyProjectile _projectilePrefab;

        [Tooltip("喷毒特效（§AnimationVFXSpec §8.3：Enemy02.Attack 时播在判定框中心，order 21）")]
        [SerializeField] private FxOneShot _fxGasPrefab;

        protected override void BuildStateMachine()
        {
            Machine = new EnemyStateMachine();
            Machine.Register(new EnemyIdleState(this, Machine));
            Machine.Register(new EnemyPatrolState(this, Machine));
            Machine.Register(new EnemyChaseState(this, Machine));
            Machine.Register(new EnemyRangedAttackState(this, Machine));
            Machine.Register(new EnemyHurtState(this, Machine));
            Machine.Register(new EnemyDieState(this, Machine));
        }

        /// <summary>Animation Event: ProjectileSpawn — 生成毒气弹体。</summary>
        public void OnProjectileSpawn()
        {
            if (!HasTarget || _projectilePrefab == null || _firePoint == null) return;

            Vector2 dir = FacingDirection;
            var projectile = PoolManager.Instance.Get(_projectilePrefab, Config.ProjectilePrewarm);
            projectile.transform.position = (Vector2)transform.position + new Vector2(
                dir.x * _firePoint.localPosition.x, _firePoint.localPosition.y);
            projectile.Launch(new EnemyProjectile.ProjectileParams
            {
                Prefab = _projectilePrefab,
                Direction = dir,
                Speed = Config.ProjectileSpeed,
                MaxRange = Config.ProjectileMaxRange,
                Damage = Config.AttackDamage,
                Knockback = Config.AttackKnockback,
                Source = gameObject,
                OwnerLayer = gameObject.layer
            });

            // 喷毒特效：播在弹体生成点（判定框中心），§5.2 特效前 = 21
            if (_fxGasPrefab != null)
            {
                FxOneShot.Play(_fxGasPrefab, projectile.transform.position, 21);
            }
        }
    }
}
