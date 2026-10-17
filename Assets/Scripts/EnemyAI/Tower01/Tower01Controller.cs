using UnityEngine;
using StarEco.Combat;
using StarEco.Framework;
using StarEco.EnemyAI.Events;

namespace StarEco.EnemyAI
{
    /// <summary>
    /// Tower01 毒气防御塔：固定单位，Idle→Aim→Fire→Aim 循环，Disable 终态。
    /// IsStatic=true，KnockbackResponse=0，无 Rigidbody2D，默认不可死。
    /// </summary>
    public class Tower01Controller : EnemyControllerBase
    {
        [SerializeField] private Transform _firePoint;
        [SerializeField] private EnemyProjectile _projectilePrefab;

        private bool _isDisabled;
        private bool _disableBroadcasted;

        protected override System.Type InitialStateType => typeof(Tower01IdleState);

        protected override void BuildStateMachine()
        {
            Machine = new EnemyStateMachine();
            Machine.Register(new Tower01IdleState(this, Machine));
            Machine.Register(new Tower01AimState(this, Machine));
            Machine.Register(new Tower01FireState(this, Machine));
            Machine.Register(new Tower01DisableState(this, Machine));
        }

        public bool IsDisabled => _isDisabled;

        /// <summary>塔不可死：免疫一切伤害与硬直，Disable 仅经 RequestDisable（M6 检查点）触发。</summary>
        public override void TakeDamage(DamageInfo info)
        {
        }

        /// <summary>外部请求关闭塔（幂等：不重播/不报错/不重复广播）。</summary>
        public void RequestDisable()
        {
            if (_isDisabled) return;
            _isDisabled = true;
            Machine.SwitchState<Tower01DisableState>();
        }

        /// <summary>M6 检查点恢复用空壳（M4 不自行调用）。</summary>
        public void RequestReactivate() { }

        /// <summary>广播关闭事件（只广播一次）。</summary>
        public void BroadcastDisableOnce()
        {
            if (_disableBroadcasted) return;
            _disableBroadcasted = true;
            EventCenter.Publish(new TowerDisableEvent
            {
                TowerTypeId = Config.EnemyTypeId,
                Position = transform.position
            });
        }

        /// <summary>Animation Event: FireSpawn — 生成毒气弹体。</summary>
        public void OnFireSpawn()
        {
            if (_isDisabled || _projectilePrefab == null || _firePoint == null) return;

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
        }

        /// <summary>Animation Event: AimEnd — Aim → Fire。</summary>
        public void OnAimEnd()
        {
            if (Machine.CurrentType == typeof(Tower01AimState))
            {
                Machine.SwitchState<Tower01FireState>();
            }
        }

        /// <summary>Animation Event: FireEnd — Fire → 冷却后 Aim/Idle。</summary>
        public void OnFireEnd()
        {
            if (Machine.CurrentType == typeof(Tower01FireState))
            {
                if (HasTarget)
                    Machine.SwitchState<Tower01AimState>();
                else
                    Machine.SwitchState<Tower01IdleState>();
            }
        }

        public override void ResetForReuse()
        {
            base.ResetForReuse();
            _isDisabled = false;
            _disableBroadcasted = false;
        }
    }
}
