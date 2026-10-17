using UnityEngine;

namespace StarEco.Level
{
    /// <summary>
    /// 2D 相机跟随（M9 R5）：Level02 横向 56u，静态相机会让玩家走出视口。
    /// 最简实现：仅跟随 X（Y 固定），按关卡边界与视口半宽动态钳制；
    /// 关卡片段内生效，不影响 M6/M8 场景（它们不挂本组件）。
    /// </summary>
    public class CameraFollow2D : MonoBehaviour
    {
        [Tooltip("跟随目标（为空则自动找 Player）")]
        [SerializeField] private Transform _target;

        [Tooltip("X 平滑系数（越大越紧跟）")]
        [SerializeField] private float _smoothX = 5f;

        [Tooltip("固定 Y")]
        [SerializeField] private float _fixedY = 2.2f;

        [Tooltip("关卡左边界 X")]
        [SerializeField] private float _levelLeft = -28f;

        [Tooltip("关卡右边界 X")]
        [SerializeField] private float _levelRight = 28f;

        private Camera _cam;
        private Transform _self;

        private void Awake()
        {
            _self = transform;
            _cam = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            if (_cam == null || _self == null)
            {
                return;
            }

            if (_target == null)
            {
                var pc = FindObjectOfType<StarEco.Player.PlayerController>();
                if (pc == null)
                {
                    return;
                }
                _target = pc.transform;
            }

            float halfWidth = _cam.orthographicSize * _cam.aspect;
            float minX = _levelLeft + halfWidth;
            float maxX = _levelRight - halfWidth;

            float targetX = _target.position.x;
            if (maxX > minX)
            {
                targetX = Mathf.Clamp(targetX, minX, maxX);
            }

            float newX = Mathf.Lerp(_self.position.x, targetX, Mathf.Clamp01(_smoothX * Time.deltaTime));
            _self.position = new Vector3(newX, _fixedY, _self.position.z);
        }
    }
}
