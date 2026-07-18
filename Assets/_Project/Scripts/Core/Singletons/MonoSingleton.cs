using UnityEngine;

namespace Game.Core.Singletons
{
    /// <summary>
    /// 씬에 하나만 존재하는 MonoBehaviour 베이스.
    /// 상속 타입이 Awake/OnDestroy를 오버라이드하면 base 호출을 누락하지 않는다 (LSP).
    /// </summary>
    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<T>();
                }

                return _instance;
            }
        }

        public static bool HasInstance => _instance != null;

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"[{typeof(T).Name}] 인스턴스가 이미 존재하여 중복 오브젝트를 파괴합니다.", this);
                Destroy(gameObject);
                return;
            }

            _instance = (T)this;
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
