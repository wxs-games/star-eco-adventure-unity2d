using System.Collections.Generic;
using UnityEngine;

namespace StarEco.EnemyAI
{
    /// <summary>Animator 参数名常量：代码与动画资源之间唯一的事实来源。</summary>
    public static class EnemyAnimParams
    {
        public static readonly int Speed = Animator.StringToHash("Speed");
        public static readonly int IsMove = Animator.StringToHash("IsMove");
        public static readonly int IsAiming = Animator.StringToHash("IsAiming");
        public static readonly int Attack = Animator.StringToHash("Attack");
        public static readonly int Fire = Animator.StringToHash("Fire");
        public static readonly int Hurt = Animator.StringToHash("Hurt");
        public static readonly int Die = Animator.StringToHash("Die");
        public static readonly int Spawn = Animator.StringToHash("Spawn");

        // M8 Boss 专属：技能 B / 特殊招 / 阶段狂暴（三个 Trigger，其余沿用上面通用参数）
        public static readonly int AttackB = Animator.StringToHash("AttackB");
        public static readonly int Special = Animator.StringToHash("Special");
        public static readonly int Enrage = Animator.StringToHash("Enrage");
    }

    /// <summary>
    /// 敌人动画驱动：每帧将控制器事实写入 Animator 参数。
    /// 状态机逻辑与动画资源完全解耦（仅通过参数通信）。
    /// <para>
    /// 写入前先校验参数是否存在（2026-10-31）：此前所有 <c>SetXxx</c> 都无条件写，
    /// 遇到 Controller 里没有的参数，Unity 每次调用都打一条
    /// <c>Parameter 'Hash xxx' does not exist</c>。实测就是 <c>IsAiming</c>——
    /// 非远程敌人的 Controller 根本没有这个参数，而 <see cref="ResetParams"/> 在池复用、
    /// 每次 OnEnable 都会写它，控制台因此被刷成一片黄字（答辩演示时会被看见）。
    /// 现在改为：启动时缓存 Controller 的实际参数集，缺失则跳过写入；
    /// 每个 Controller 的每个缺失参数只告警一次（全局去重），保留可发现性但不刷屏。
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class EnemyAnimatorDriver : MonoBehaviour
    {
        [SerializeField] private EnemyControllerBase _controller;

        private Animator _animator;

        /// <summary>当前 Controller 实际拥有的参数哈希。</summary>
        private readonly HashSet<int> _paramHashes = new HashSet<int>();

        /// <summary>已告警过的 (Controller, 参数) 组合，全局去重，避免每个敌人实例各报一遍。</summary>
        private static readonly HashSet<string> WarnedMissing = new HashSet<string>();

        /// <summary>缓存对应的 Controller，用于识别运行时换控制器。</summary>
        private RuntimeAnimatorController _cachedController;

        /// <summary>是否已建过缓存（Controller 为 null 时也要算已建，否则每帧重扫）。</summary>
        private bool _cacheBuilt;

        private static readonly Dictionary<int, string> ParamNames = new Dictionary<int, string>
        {
            { EnemyAnimParams.Speed, nameof(EnemyAnimParams.Speed) },
            { EnemyAnimParams.IsMove, nameof(EnemyAnimParams.IsMove) },
            { EnemyAnimParams.IsAiming, nameof(EnemyAnimParams.IsAiming) },
            { EnemyAnimParams.Attack, nameof(EnemyAnimParams.Attack) },
            { EnemyAnimParams.Fire, nameof(EnemyAnimParams.Fire) },
            { EnemyAnimParams.Hurt, nameof(EnemyAnimParams.Hurt) },
            { EnemyAnimParams.Die, nameof(EnemyAnimParams.Die) },
            { EnemyAnimParams.Spawn, nameof(EnemyAnimParams.Spawn) },
            { EnemyAnimParams.AttackB, nameof(EnemyAnimParams.AttackB) },
            { EnemyAnimParams.Special, nameof(EnemyAnimParams.Special) },
            { EnemyAnimParams.Enrage, nameof(EnemyAnimParams.Enrage) }
        };

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_controller == null)
            {
                _controller = GetComponentInParent<EnemyControllerBase>();
            }

            EnsureParamCache();
        }

        private void Update()
        {
            if (_controller == null || _animator == null)
            {
                return;
            }

            float speed = 0f;
            if (_controller.Body != null)
            {
                speed = Mathf.Abs(_controller.Body.velocity.x);
            }

            SetFloat(EnemyAnimParams.Speed, speed);
        }

        /// <summary>触发一次性动画（攻击/受击/死亡/出场/开火）。供状态 Enter 调用。</summary>
        public void FireOneShot(int triggerHash)
        {
            if (_animator == null || !HasParam(triggerHash, true))
            {
                return;
            }

            _animator.SetTrigger(triggerHash);
        }

        public void SetBool(int paramHash, bool value)
        {
            if (_animator == null || !HasParam(paramHash, true))
            {
                return;
            }

            _animator.SetBool(paramHash, value);
        }

        public void SetFloat(int paramHash, float value)
        {
            if (_animator == null || !HasParam(paramHash, true))
            {
                return;
            }

            _animator.SetFloat(paramHash, value);
        }

        /// <summary>
        /// 复位所有参数（池复用时调用）。
        /// 复位是「尽力而为」语义：控制器没有的参数（Boss01 就没有 IsAiming）
        /// 直接跳过、<b>不告警</b>——否则每次 Boss 启用 / 池复用都会在控制台刷一条。
        /// 该告警的是状态机主动触发却没配的参数，见 <see cref="FireOneShot"/> 等。
        /// </summary>
        public void ResetParams()
        {
            ResetFloat(EnemyAnimParams.Speed, 0f);
            ResetBool(EnemyAnimParams.IsMove, false);
            ResetBool(EnemyAnimParams.IsAiming, false);
        }

        private void ResetBool(int paramHash, bool value)
        {
            if (_animator == null || !HasParam(paramHash, false))
            {
                return;
            }

            _animator.SetBool(paramHash, value);
        }

        private void ResetFloat(int paramHash, float value)
        {
            if (_animator == null || !HasParam(paramHash, false))
            {
                return;
            }

            _animator.SetFloat(paramHash, value);
        }

        // ===== 参数存在性 =====

        /// <summary>
        /// 重建参数缓存。Controller 未变时直接返回；运行中换 Controller 会自动重建。
        /// </summary>
        private void EnsureParamCache()
        {
            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
            }

            var controller = _animator != null ? _animator.runtimeAnimatorController : null;
            if (_cacheBuilt && _cachedController == controller)
            {
                return;
            }

            _cacheBuilt = true;
            _cachedController = controller;
            _paramHashes.Clear();

            if (_animator == null || controller == null)
            {
                return;
            }

            var parameters = _animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                _paramHashes.Add(parameters[i].nameHash);
            }
        }

        /// <param name="warnIfMissing">
        /// 缺失参数时是否告警。<c>false</c> 用于复位这类「尽力而为」的写入。
        /// </param>
        private bool HasParam(int paramHash, bool warnIfMissing)
        {
            EnsureParamCache();

            if (_paramHashes.Contains(paramHash))
            {
                return true;
            }

            // 没有挂 Controller 的占位敌人不告警（可能动画还没配）
            if (_cachedController == null || !warnIfMissing)
            {
                return false;
            }

            string key = _cachedController.name + "|" + NameOf(paramHash);
            if (WarnedMissing.Add(key))
            {
                Debug.LogWarning(
                    $"[EnemyAnimatorDriver] 动画控制器 \"{_cachedController.name}\" 没有参数 " +
                    $"\"{NameOf(paramHash)}\"，该参数写入已跳过（每个控制器只提示一次）。" +
                    $"若该敌人本应有此参数，请补齐动画资源。", this);
            }

            return false;
        }

        private static string NameOf(int hash)
        {
            return ParamNames.TryGetValue(hash, out string paramName) ? paramName : "Hash " + hash;
        }
    }
}
