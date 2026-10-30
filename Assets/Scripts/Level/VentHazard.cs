using UnityEngine;
using StarEco.Combat;
using StarEco.Framework;
using StarEco.Player;

namespace StarEco.Level
{
    /// <summary>
    /// 污染喷口（新环境元素 · 验证版，2026-10-30）：节奏型危险区。
    ///
    /// 状态机：预警(0.6s 静默准备) → 预警闪烁(1.2s) → 喷射(1.8s) → 冷却(2.6s) 循环。
    /// - 喷射期间把气柱内的玩家持续向上顶（只改 y 速度，不碰 x，避免和玩家移动逻辑打架）
    /// - 伤害按 _damageTick 节流，仍走既有 IHittable 流水线，不另开死亡逻辑
    /// - **只结算到玩家身上**（_damagePlayerOnly，默认开启）：喷口是给玩家的关卡危险区，
    ///   Boss / 小怪踩进来不受伤、也不被顶飞。此前按 IHittable 无差别结算，第三关 Boss 的
    ///   碰撞体与 Env_Vent（x=64.4~66.0）重叠 0.18u，导致 Boss 被自己的喷气口持续顶飞 + 掉血
    ///   （实测 144.7s 掉到 54/80），是 P0 级事故。语义上陷阱的施加对象本就只有玩家。
    ///
    /// 与 SpikeHazard 的差别：地刺是瞬间结算、没有可预判的窗口；喷口有明显预警 + 持续推顶，
    /// 玩家的应对是「等灯灭再冲」而不是「跳过去」。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class VentHazard : MonoBehaviour
    {
        private enum Phase
        {
            Warmup,
            Warn,
            Emit,
            Cool
        }

        [Tooltip("开局静默时间（秒），避免玩家刚进场景就被喷")]
        [SerializeField] private float _warmup = 0.6f;

        [Tooltip("预警时长（秒）：此刻不伤人，只闪灯")]
        [SerializeField] private float _warnTime = 1.2f;

        [Tooltip("喷射时长（秒）：顶飞 + 结算伤害")]
        [SerializeField] private float _emitTime = 1.8f;

        [Tooltip("冷却时长（秒）")]
        [SerializeField] private float _coolTime = 2.6f;

        [Tooltip("喷射时的向上速度（世界单位/秒）")]
        [SerializeField] private float _liftSpeed = 7f;

        [Tooltip("每次伤害结算（半心点：2 点 = 1 颗心）")]
        [SerializeField] private int _damage = 1;

        [Tooltip("伤害结算间隔（秒）：进入气柱后每隔这么久扣一次")]
        [SerializeField] private float _damageTick = 0.6f;

        [Tooltip("只结算到玩家：喷口是给玩家的危险区，Boss/小怪踩到既不受伤也不被顶飞。" +
                 "关闭后回到旧行为（任何 IHittable 都吃伤害），除非关卡刻意设计成『靠喷口伤敌』否则不要关")]
        [SerializeField] private bool _damagePlayerOnly = true;

        private static readonly Color IdleColor = new Color(0.35f, 0.40f, 0.32f);
        private static readonly Color WarnColor = new Color(0.95f, 0.72f, 0.20f);
        private static readonly Color EmitColor = new Color(0.45f, 0.85f, 0.35f);

        private Collider2D _collider;
        private SpriteRenderer _sprite;
        private Phase _phase = Phase.Warmup;
        private float _timer;
        private float _damageTimer;

        public bool IsEmitting => _phase == Phase.Emit;

        private void Awake()
        {
            _collider = GetComponent<Collider2D>();
            // 视觉是子物体（根节点只放碰撞体与逻辑），所以从子层取渲染器
            _sprite = GetComponentInChildren<SpriteRenderer>();
            if (_sprite != null)
            {
                _sprite.color = IdleColor;
            }
        }

        private void Update()
        {
            _timer += Time.deltaTime;

            switch (_phase)
            {
                case Phase.Warmup:
                    if (_timer >= _warmup) Enter(Phase.Warn);
                    break;
                case Phase.Warn:
                    // 灯闪：越接近喷射闪得越快
                    if (_sprite != null)
                    {
                        float t = _warnTime > 0f ? Mathf.Clamp01(_timer / _warnTime) : 1f;
                        float blink = Mathf.PingPong(_timer * Mathf.Lerp(4f, 12f, t), 1f);
                        _sprite.color = Color.Lerp(IdleColor, WarnColor, blink);
                    }
                    if (_timer >= _warnTime) Enter(Phase.Emit);
                    break;
                case Phase.Emit:
                    if (_timer >= _emitTime) Enter(Phase.Cool);
                    break;
                case Phase.Cool:
                    if (_timer >= _coolTime) Enter(Phase.Warn);
                    break;
            }
        }

        private void Enter(Phase next)
        {
            _phase = next;
            _timer = 0f;
            _damageTimer = 0f;

            if (_sprite != null)
            {
                _sprite.color = next == Phase.Emit ? EmitColor : IdleColor;
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (_phase != Phase.Emit)
            {
                return;
            }

            // 敌我筛选放在取 IHittable 之前：不满足就直接返回，连顶飞也不做
            // （否则 Boss 站在喷口上会被持续抬高，垂直震荡同样会让它无法正常追击）
            if (_damagePlayerOnly && other.GetComponentInParent<PlayerController>() == null)
            {
                return;
            }

            var hittable = other.GetComponentInParent<IHittable>();
            if (hittable == null)
            {
                return;
            }

            // 只改竖直速度：水平速度归玩家控制器管，硬写会造成抖动
            var body = other.attachedRigidbody;
            if (body != null && !body.isKinematic)
            {
                body.velocity = new Vector2(body.velocity.x, Mathf.Max(body.velocity.y, _liftSpeed));
            }

            // 伤害节流：用 fixedDeltaTime 累计，避免帧率影响结算频率
            _damageTimer += Time.fixedDeltaTime;
            if (_damageTimer >= _damageTick)
            {
                _damageTimer = 0f;
                hittable.TakeDamage(new DamageInfo(_damage, Vector2.up, gameObject));
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.45f, 0.85f, 0.35f, 0.3f);
            var col = GetComponent<Collider2D>();
            if (col != null)
            {
                Gizmos.DrawCube(col.bounds.center, col.bounds.size);
            }
        }
    }
}
