using UnityEngine;

namespace StarEco.Player
{
    /// <summary>Animator 参数名常量：代码与动画资源之间唯一的事实来源。</summary>
    public static class PlayerAnimParams
    {
        public static readonly int Speed = Animator.StringToHash("Speed");
        public static readonly int IsGrounded = Animator.StringToHash("IsGrounded");
        public static readonly int VelocityY = Animator.StringToHash("VelocityY");
        public static readonly int Hurt = Animator.StringToHash("Hurt");
        public static readonly int Die = Animator.StringToHash("Die");
        public static readonly int Attack = Animator.StringToHash("Attack");       // M3 使用
        public static readonly int Interact = Animator.StringToHash("Interact");   // M5 使用
    }

    /// <summary>
    /// 动画驱动：每帧将控制器事实写入 Animator 参数。
    /// 状态机逻辑与动画资源完全解耦（仅通过参数通信）。
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimatorDriver : MonoBehaviour
    {
        [SerializeField] private PlayerController _player;

        private Animator _animator;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_player == null)
            {
                _player = GetComponentInParent<PlayerController>();
            }
        }

        private void Update()
        {
            if (_player == null || _animator == null)
            {
                return;
            }

            _animator.SetFloat(PlayerAnimParams.Speed, Mathf.Abs(_player.Body.velocity.x));
            _animator.SetBool(PlayerAnimParams.IsGrounded, _player.IsGrounded);
            _animator.SetFloat(PlayerAnimParams.VelocityY, _player.VerticalVelocity);

            // 受击/死亡触发器：由状态机在 Enter 时调用 FireOneShot 触发
        }

        /// <summary>触发一次性动画（受击/死亡/攻击/互动）。供状态 Enter 调用。</summary>
        public void FireOneShot(int triggerHash)
        {
            _animator?.SetTrigger(triggerHash);
        }
    }
}
