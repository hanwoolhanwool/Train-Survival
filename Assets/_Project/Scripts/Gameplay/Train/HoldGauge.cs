using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 짧은 홀드 + 게이지 조작의 누적 상태 (건축 개편 2·3차 — 결정 ④: 오철거 방지).
    /// 건축물 철거와 판자 철거가 같은 규약(같은 홀드 시간, 표적이 바뀌거나 키를 놓으면 리셋)을 쓰므로
    /// 상태 기계를 한 곳에 둔다 — 표적은 호출부가 정하는 <b>토큰</b> 하나로 식별한다.
    /// Unity 비의존 순수 구조체라 EditMode로 검증한다.
    /// </summary>
    public struct HoldGauge
    {
        private float _elapsed;
        private int _target;
        private bool _active;

        /// <summary>
        /// 한 프레임 갱신 — <paramref name="active"/>가 거짓이거나 표적 토큰이 바뀌면 누적을 버린다.
        /// <paramref name="holdSeconds"/>를 채우면 <paramref name="completed"/>가 참이 되고 누적이 리셋된다.
        /// 반환 = 게이지 진행도 0~1 (완료 프레임과 비활성은 0).
        /// </summary>
        public float Update(bool active, int target, float holdSeconds, out bool completed)
        {
            return Update(active, target, holdSeconds, Time.deltaTime, out completed);
        }

        /// <summary>시간 증분을 직접 주는 검증용 오버로드 — 위 오버로드가 <see cref="Time.deltaTime"/>으로 부른다.</summary>
        public float Update(bool active, int target, float holdSeconds, float deltaTime, out bool completed)
        {
            completed = false;

            if (!active || holdSeconds <= 0f)
            {
                Reset();
                return 0f;
            }

            if (!_active || _target != target)
            {
                _active = true;
                _target = target;
                _elapsed = 0f;
            }

            _elapsed += deltaTime;
            if (_elapsed >= holdSeconds)
            {
                completed = true;
                Reset();
                return 0f;
            }

            return Mathf.Clamp01(_elapsed / holdSeconds);
        }

        /// <summary>누적을 버린다 — 입력 게이트가 닫히거나(사망·도구 해제) 표적을 잃었을 때.</summary>
        public void Reset()
        {
            _elapsed = 0f;
            _target = 0;
            _active = false;
        }
    }
}
