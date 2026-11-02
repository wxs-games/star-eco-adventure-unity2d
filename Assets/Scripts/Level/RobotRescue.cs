using UnityEngine;
using StarEco.Level;
using StarEco.Level.Events;
using StarEco.UI;
using StarEco.UI.Events;
using StarEco.Framework;
using System.Reflection;

namespace StarEco.Level
{
    /// <summary>
    /// 工厂NPC救援剧情（L2工厂关）：NPC_Robot_FactoryIntro 被困在地刺 SpikeField 中。
    /// 流程：
    ///  1. 地刺未收起：NPC 对话只说"被困求救"，按 E 无法获得钥匙。
    ///  2. 玩家上二楼扳拉杆 LeverSwitch_03 → 地刺 SpikeField 收起。
    ///  3. 地刺收起后：NPC 对话切换为"获救，给钥匙"，按 E 对话后获得钥匙。
    ///  4. 广播 AnimalRescuedEvent，计入 LevelObjectiveTracker 的 RescueTargets 目标。
    ///  5. 救援完成后标记 _rescued = true，不可重复触发。
    /// </summary>
    [RequireComponent(typeof(DialogueTrigger))]
    [RequireComponent(typeof(Collider2D))]
    public class RobotRescue : MonoBehaviour
    {
        [Tooltip("被困的地刺（收起后切换对话内容）")]
        [SerializeField] private SpikeHazard _spike;

        [Tooltip("被困阶段的对话 ID（Resources/Data/dialogues.json）")]
        [SerializeField] private string _trappedDialogueId = "robot_factory_trapped";

        [Tooltip("获救阶段的对话 ID（最后一句需携带 EventId=robot_give_key）")]
        [SerializeField] private string _rescuedDialogueId = "robot_factory_rescued";

        [Tooltip("救援目标标识（AnimalRescuedEvent.TargetId，供 LevelObjectiveTracker 计数）")]
        [SerializeField] private string _rescueTargetId = "Robot_FactoryIntro";

        private DialogueTrigger _dialogueTrigger;
        private bool _rescued;
        private bool _keyGiven;

        private void OnEnable()
        {
            EventCenter.Subscribe<DialogueEventIdEvent>(OnDialogueEventId);
        }

        private void OnDisable()
        {
            EventCenter.Unsubscribe<DialogueEventIdEvent>(OnDialogueEventId);
        }

        private void Awake()
        {
            _dialogueTrigger = GetComponent<DialogueTrigger>();
            SetDialogueId(_trappedDialogueId);
            ResetHasTalkedOnce();
        }

        private void Update()
        {
            if (_rescued)
            {
                return;
            }

            // 地刺收起后，切换对话为获救阶段（仅切换一次）
            if (_spike != null && _spike.IsRetracted && !_keyGiven)
            {
                SetDialogueId(_rescuedDialogueId);
                ResetHasTalkedOnce();
                _keyGiven = true; // 标记已切换，避免每帧反射
                Debug.Log("[RobotRescue] 地刺已收起，NPC 对话切换为获救阶段");
            }
        }

        /// <summary>通过反射设置 DialogueTrigger 的私有字段 _dialogueId。</summary>
        private void SetDialogueId(string id)
        {
            if (_dialogueTrigger == null)
            {
                return;
            }
            var field = typeof(DialogueTrigger).GetField("_dialogueId", BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(_dialogueTrigger, id);
        }

        /// <summary>通过反射重置 DialogueTrigger 的 _hasTalkedOnce，确保切换对话后首次靠近可重新自动对话。</summary>
        private void ResetHasTalkedOnce()
        {
            if (_dialogueTrigger == null)
            {
                return;
            }
            var field = typeof(DialogueTrigger).GetField("_hasTalkedOnce", BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(_dialogueTrigger, false);
        }

        /// <summary>由 DialogueTrigger 的对话结束事件（EventId=robot_give_key）调用，完成救援并给予钥匙。</summary>
        public void OnRescueComplete()
        {
            if (_rescued)
            {
                return;
            }
            _rescued = true;

            // 发放钥匙：让 KeyDoor 知道玩家已持有钥匙
            KeyDoor.SetHasKey(true);

            // 广播 AnimalRescuedEvent，让 LevelObjectiveTracker 计入 RescueTargets
            EventCenter.Publish(new AnimalRescuedEvent
            {
                TargetId = _rescueTargetId,
                Position = transform.position
            });

            // 播放获得物品 UI（钥匙）
            ItemGetUI.Show(null, 5f, null);

            AudioManager.Instance?.PlaySfx(SfxId.Rescue);

            Debug.Log($"[RobotRescue] NPC {_rescueTargetId} 已获救，发放钥匙，广播 AnimalRescuedEvent");
        }

        private void OnDialogueEventId(DialogueEventIdEvent evt)
        {
            if (_rescued || evt.DialogueId != _rescuedDialogueId)
            {
                return;
            }
            if (evt.EventId == "robot_give_key")
            {
                OnRescueComplete();
            }
        }
    }
}
