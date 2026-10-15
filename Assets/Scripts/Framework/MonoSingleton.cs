using UnityEngine;

namespace StarEco.Framework
{
    /// <summary>
    /// MonoBehaviour 泛型单例基类（CRTP）。
    /// 首次访问 Instance 时自动创建并 DontDestroyOnLoad；重复实例自动销毁。
    /// </summary>
    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
    {
        private static T _instance;
        private static readonly object _lock = new object();
        private static bool _quitting;

        /// <summary>全局唯一实例。应用退出期返回 null 以防止复活。</summary>
        public static T Instance
        {
            get
            {
                if (_quitting)
                {
                    return null;
                }

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        var go = new GameObject(typeof(T).Name);
                        _instance = go.AddComponent<T>();
                    }

                    return _instance;
                }
            }
        }

        /// <summary>实例是否已存在（不触发创建）。</summary>
        public static bool HasInstance => _instance != null;

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = (T)this;

            // 只对根节点做跨场景持久（子节点 DontDestroyOnLoad 会警告）
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            OnSingletonAwake();
        }

        /// <summary>子类初始化钩子（替代重写 Awake，避免破坏单例逻辑）。</summary>
        protected virtual void OnSingletonAwake() { }

        protected virtual void OnApplicationQuit()
        {
            _quitting = true;
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
