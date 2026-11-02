using UnityEngine;
using StarEco.Player;
using StarEco.UI;

namespace StarEco.Combat
{
    /// <summary>
    /// 玩家战斗组件（T3）：持有 HP、实现 IHittable、采集攻击输入。
    /// 伤害入口唯一：TakeDamage → PlayerController.ApplyHit（M2 的受击/无敌/死亡流水线）。
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerCombat : MonoBehaviour, IHittable
    {
        [SerializeField] private CombatConfig _config;

        private PlayerController _player;

        public CombatConfig Config => _config;
        public System.Action<int> HealthChanged;

        /// <summary>当前 HP（整数定点，单位「半心点」：2 点 = 1 颗心；远程 1 点伤害 = 半颗心）。</summary>
        public int CurrentHP { get; private set; }

        public int MaxHP => _config != null ? _config.MaxHP : 10;

        /// <summary>本帧攻击输入。状态机各状态每帧消费；未被消费则随下帧刷新（不会带过下一帧）。</summary>
        public bool AttackPressedThisFrame { get; private set; }

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            if (_config == null)
            {
                _config = Resources.Load<CombatConfig>("CombatConfig") ?? CombatConfig.CreateDefault();
            }

            CurrentHP = MaxHP;
            HealthChanged?.Invoke(CurrentHP);
        }

        private void Update()
        {
            // 每帧刷新：死亡/锁定输入时不接收（InputBlocked 同时涵盖对话等外部强制锁）
            // B5 修复：去掉 Fire1（Unity 默认=左 Ctrl）——左 Ctrl 已被 PlayerController 用作冲刺，
            // 共用会导致按左 Ctrl 同时冲刺+攻击。保留 J 键与鼠标左键（原 Fire1 的 altPositiveButton）攻击。
            AttackPressedThisFrame = _player != null && !_player.InputBlocked
                && (Input.GetKeyDown(KeyCode.J) || Input.GetMouseButtonDown(0));
        }

        /// <summary>状态机消费攻击输入：仅当前帧有效，消费即清。</summary>
        public bool ConsumeAttackPressed()
        {
            if (!AttackPressedThisFrame)
            {
                return false;
            }

            AttackPressedThisFrame = false;
            return true;
        }

        // ===== IHittable =====

        public void TakeDamage(DamageInfo info)
        {
            if (_player == null || _player.IsDead)
            {
                return;
            }

            // 对话期间玩家免伤：攻击侧（弹体/判定盒/状态机）已有门控，
            // 这里在受击入口再兜一层，覆盖环境伤害等所有漏网来源。
            if (DialoguePanel.IsDialogueActive)
            {
                return;
            }

            // 无敌期忽略伤害（R5）——但致命伤害除外。
            // 曾被"无敌"吃掉过：玩家带受击/翻滚无敌从边缘掉落，坠落区 999 点伤害被这里
            // 直接 return 丢掉，人穿过 4 单位高的判定区后一路下坠再没有伤害来源，表现为
            // "掉出关卡却不死"。无敌只应豁免非致命伤害，摔死不受无敌保护。
            if (_player.InvincibleTimer > 0f && info.Amount < CurrentHP)
            {
                return;
            }

            int newHp = Mathf.Max(0, CurrentHP - info.Amount);
            CurrentHP = newHp;

            // 记录死因，供结算面板区分坠落、溺水与普通受击死亡。
            _player.MarkDeathCause(info.IsFall, info.IsDrowning);

            // 击退方向：无来源方向时按垂直上抛兜底
            Vector2 from = info.SourceDirection.sqrMagnitude > 0.0001f ? info.SourceDirection : Vector2.up;
            _player.ApplyHit(from, newHp, MaxHP);
        }
    }
}
