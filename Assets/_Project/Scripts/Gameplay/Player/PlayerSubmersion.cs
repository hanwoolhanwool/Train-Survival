using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 물에 잠긴 동안의 이속 저하와 체온 급락 (북극 지역 구현 계획 §8.1 ② · 결정 ③ "중간 — 탈출 가능").
    ///
    /// <para><b>복제가 없다.</b> 발 높이와 지역 물면은 이미 모든 피어가 같은 값을 갖고 있으므로
    /// 각자 유도한다 — <see cref="SwimMotion"/>·<see cref="World.WaterSurfaceQuery"/>와 같은 규약이고
    /// <c>NetworkVariable</c>도 RPC도 만들지 않는다. 소유자는 이속을, 서버는 체온을 같은 값에서 읽는다.</para>
    ///
    /// <para><b>두 계약을 함께 구현하는 이유.</b> 침수는 <b>한 가지 사건</b>이고 이속·체온은 그 결과다.
    /// 둘을 나누면 같은 잠김 깊이를 두 컴포넌트가 각자 계산하게 되고, 경계에서 한쪽만 켜지는
    /// 어긋남이 생긴다 — 물에 절반 잠겨 느려졌는데 체온은 멀쩡한 상태가 그것이다.</para>
    ///
    /// <para><b>얕은 물에서도 걸린다.</b> 이 축이 수영(<c>IsSwimming</c>)이 아니라 <b>잠김 깊이</b>를
    /// 보는 것이 설계의 핵심이다 — 얼음 틈은 잠김 0.8 m 라 수영 진입(1.0)에 못 미쳐 걷기가 유지되고
    /// (§5.4), 그래도 처벌은 받아야 "빠졌다"가 성립한다.</para>
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerSubmersion : MonoBehaviour, IMoveSpeedModifier, IAmbientTemperatureOverride
    {
        [Tooltip("이 깊이(m)부터 잠긴 것으로 본다. 발끝이 스치는 것까지 세면 물가에서 깜빡인다.")]
        [SerializeField, Min(0f)] private float _enterDepth = 0.25f;

        [Tooltip("이 깊이(m) 아래로 나와야 잠김이 풀린다 — 진입보다 얕게 둬 경계 깜빡임을 막는다 " +
                 "(SwimMotion 히스테리시스와 같은 이유).")]
        [SerializeField, Min(0f)] private float _exitDepth = 0.12f;

        [Tooltip("잠긴 동안의 이동속도 배율 (계획 결정 ③ — 0.7). 동상 ×0.8과 곱해져 ×0.56이 된다.")]
        [SerializeField, Range(0.1f, 1f)] private float _moveSpeedMultiplier = 0.7f;

        [Tooltip("물의 온도(℃). 극지 해수의 어는점이 −1.8 ℃라 그 부근에 둔다. " +
                 "북극 밤(−32 ℃)보다 오히려 따뜻하지만, 단열이 무효라 실제 하강은 훨씬 빠르다.")]
        [SerializeField] private float _waterTemperature = -2f;

        private NetworkObject _networkObject;
        private bool _submerged;
        private float _depth;

        /// <summary>지금 잠겨 있는가 — 검수·디버깅용.</summary>
        public bool IsSubmerged => _submerged;

        /// <summary>발 기준 잠김 깊이(m). 물 밖이면 0 이하.</summary>
        public float Depth => _depth;

        public float MoveSpeedMultiplier => _submerged ? Mathf.Clamp(_moveSpeedMultiplier, 0.1f, 1f) : 1f;

        /// <summary>
        /// 잠김 판정 — 히스테리시스로 경계에서 켜고 꺼짐이 반복되지 않게 한다.
        /// 순수 함수라 EditMode 가 고정한다.
        /// </summary>
        public static bool IsSubmergedAt(float depth, bool wasSubmerged, float enterDepth, float exitDepth)
        {
            return wasSubmerged ? depth > exitDepth : depth >= enterDepth;
        }

        public bool TryGetAmbient(out float ambientCelsius, out bool ignoresInsulation)
        {
            ambientCelsius = _waterTemperature;

            // 젖은 방한복은 단열하지 않는다. 이 플래그가 없으면 풀셋(0.9)이 침수 처벌을 통째로 지운다.
            ignoresInsulation = true;
            return _submerged;
        }

        private void Awake()
        {
            _networkObject = GetComponent<NetworkObject>();
        }

        private void OnDisable()
        {
            _submerged = false;
            _depth = 0f;
        }

        private void Update()
        {
            // 스폰 전에는 판정하지 않는다 — 배치가 끝나기 전 좌표가 물속일 수 있다.
            if (_networkObject != null && !_networkObject.IsSpawned)
            {
                _submerged = false;
                _depth = 0f;
                return;
            }

            if (!World.WaterSurfaceQuery.TryGetWaterSurfaceY(out float waterSurfaceY))
            {
                _submerged = false;
                _depth = 0f;
                return;
            }

            // 발 높이는 CharacterController 규약상 transform.position.y 다 (SwimMotion 주석).
            _depth = SwimMotion.SubmergeDepth(transform.position.y, waterSurfaceY);
            _submerged = IsSubmergedAt(_depth, _submerged, _enterDepth, _exitDepth);
        }
    }
}
