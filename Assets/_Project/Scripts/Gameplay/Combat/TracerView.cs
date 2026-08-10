using Game.Core.Pooling;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// 펠릿 트레이서 뷰 (M5 8차 — 전투 연출) — 풀링 비네트워크 코스메틱.
    /// 발사 순간의 (총구, 종점)을 받아 짧은 페이드(선폭 축소) 후 스스로 풀로 돌아간다.
    /// 총 공유 1줄이 아니라 펠릿마다 하나씩 스폰돼 산탄 퍼짐이 눈에 보인다.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class TracerView : MonoBehaviour, IPoolable
    {
        private LineRenderer _line;
        private float _baseWidth;
        private float _fadeSeconds;
        private float _elapsed;
        private bool _active;

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _baseWidth = _line.widthMultiplier;
            _line.positionCount = 2;
            _line.useWorldSpace = true;
        }

        /// <summary>트레이서를 표시하고 페이드를 시작한다 — 페이드가 끝나면 자동 회수된다.</summary>
        public void Show(Vector3 start, Vector3 end, float fadeSeconds)
        {
            _line.SetPosition(0, start);
            _line.SetPosition(1, end);
            _line.widthMultiplier = _baseWidth;
            _line.enabled = true;
            _fadeSeconds = Mathf.Max(0.02f, fadeSeconds);
            _elapsed = 0f;
            _active = true;
        }

        private void Update()
        {
            if (!_active)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _fadeSeconds);
            _line.widthMultiplier = _baseWidth * (1f - t);

            if (t >= 1f)
            {
                _active = false;
                PoolManager.Despawn(gameObject);
            }
        }

        public void OnSpawned()
        {
        }

        public void OnDespawned()
        {
            _active = false;
            if (_line != null)
            {
                _line.enabled = false;
                _line.widthMultiplier = _baseWidth;
            }
        }
    }
}
