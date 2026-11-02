using UnityEngine;
using StarEco.Framework;

namespace StarEco.Eco
{
    /// <summary>
    /// 通用一次性特效：播放方经对象池取出后 Init 注入来源 prefab，动画播完自动归还池。
    /// 不绑定具体特效（M6+ 可复用于任意 fx_* 预制体）。
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class FxOneShot : MonoBehaviour
    {
        private Animator _animator;
        private FxOneShot _sourcePrefab;
        private SpriteRenderer _renderer;
        private bool _resolved;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _renderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            _resolved = false;
        }

        /// <summary>池取出后由播放方调用：注入来源预制体（播完按此归还）。</summary>
        public void Init(FxOneShot sourcePrefab)
        {
            _sourcePrefab = sourcePrefab;
        }

        /// <summary>池取出后由播放方调用：注入来源预制体 + 运行时层级。</summary>
        /// <param name="sourcePrefab">来源预制体（播完按此归还池）。</param>
        /// <param name="sortingOrder">运行时 sortingOrder。§5.2：特效前 21 / 特效后 20。
        /// 传 -1 表示不改，沿用 prefab 资产上的值。</param>
        public void Init(FxOneShot sourcePrefab, int sortingOrder)
        {
            _sourcePrefab = sourcePrefab;

            if (sortingOrder >= 0 && _renderer != null)
            {
                _renderer.sortingOrder = sortingOrder;
            }
        }

        /// <summary>便捷播放：经对象池取出、摆位、注入 prefab（prefab 为空静默跳过）。</summary>
        public static FxOneShot Play(FxOneShot prefab, Vector3 position)
        {
            return Play(prefab, position, -1);
        }

        /// <summary>
        /// 便捷播放（带层级）：经对象池取出、摆位、注入 prefab、设定 sortingOrder。
        /// </summary>
        /// <param name="prefab">特效 prefab（为空静默跳过）。</param>
        /// <param name="position">播放位置。</param>
        /// <param name="sortingOrder">运行时 sortingOrder；-1 表示沿用 prefab 上的值。</param>
        /// <returns>取出的特效实例（调用方可据此设置缩放等运行时属性），prefab 为空时返回 null。</returns>
        public static FxOneShot Play(FxOneShot prefab, Vector3 position, int sortingOrder)
        {
            if (prefab == null || PoolManager.Instance == null)
            {
                return null;
            }

            var fx = PoolManager.Instance.Get(prefab);
            fx.transform.position = position;
            fx.Init(prefab, sortingOrder);
            return fx;
        }

        private void Update()
        {
            if (_resolved)
            {
                return;
            }

            if (_animator != null && _animator.isActiveAndEnabled
                && _animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f)
            {
                Resolve();
            }
        }

        private void Resolve()
        {
            _resolved = true;

            if (_sourcePrefab != null && PoolManager.Instance != null)
            {
                PoolManager.Instance.Release(_sourcePrefab, this);
            }
            else
            {
                // 兜底：无池引用时直接隐藏（与 M4 Recycle 兜底同策略）
                gameObject.SetActive(false);
                if (_sourcePrefab == null)
                {
                    Debug.LogWarning("[FxOneShot] 缺少来源 prefab 引用，特效直接隐藏未归池。");
                }
            }
        }
    }
}
