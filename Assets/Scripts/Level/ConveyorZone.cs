using UnityEngine;
using StarEco.Player;

namespace StarEco.Level
{
    /// <summary>
    /// 传送带 / 气流管道（新环境元素 · 验证版，2026-10-30）：单向推速区。
    ///
    /// 为什么不用 AreaEffector2D：玩家控制器每帧用 Mathf.MoveTowards 把水平速度
    /// 拉回目标速度（PlayerController.ApplyHorizontalMovement），外力会被直接覆盖掉，
    /// 所以这里改成「向玩家注入一份环境速度」，由控制器在算目标速度时叠加。
    ///
    /// 玩家离开区域后，注入值为 0（控制器每次算完就清零），速度会自然衰减回去。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class ConveyorZone : MonoBehaviour
    {
        [Tooltip("推力速度（世界单位/秒）：正 = 向右推，负 = 向左推（逆流）")]
        [SerializeField] private float _speed = 3.5f;

        private void OnTriggerStay2D(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null)
            {
                return;
            }

            player.SetExternalSpeed(_speed);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.35f, 0.75f, 1f, 0.3f);
            var col = GetComponent<Collider2D>();
            if (col != null)
            {
                Gizmos.DrawCube(col.bounds.center, col.bounds.size);

                // 箭头示意推力方向
                Vector3 c = col.bounds.center;
                Gizmos.color = new Color(0.35f, 0.75f, 1f, 1f);
                Gizmos.DrawRay(c, Vector3.right * Mathf.Sign(_speed) * 1f);
            }
        }
    }
}
