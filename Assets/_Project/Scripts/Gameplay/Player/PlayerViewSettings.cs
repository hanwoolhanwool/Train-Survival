using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 시점 모드 표현 데이터 (1인칭 통합 시점 전환 계획 §3.1·§3.4) — 판정 무관, 전부 표현 층위.
    /// 두 모드의 값을 <b>나란히</b> 들고 있어야 QA가 한 키로 오갈 수 있다 (§4.1).
    /// 통합 모드가 합격하면 분리 쪽 값과 함께 이 에셋도 정리된다 (§8).
    ///
    /// <para>몸 표시는 여기에 없다 — 소유자 몸은 모드와 무관하게 항상 그림자만 남기기 때문이다
    /// (2026-08-19 사용자 확정). 화면에 보이는 것은 손에 쥔 무기뿐이다.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerViewSettings", menuName = "Game/Player View Settings")]
    public sealed class PlayerViewSettings : ScriptableObject
    {
        [Header("기본 동작")]
        [Tooltip("스폰 시 적용할 모드 — 개발 중 기본은 분리(현행)다 (기술 확정 ⑨).")]
        [SerializeField] private PlayerViewMode _defaultMode = PlayerViewMode.SplitFpTp;

        [Tooltip("F10 전환 허용 (기술 확정 ⑩) — 끄면 기본 모드로 고정된다.")]
        [SerializeField] private bool _debugToggleEnabled = true;

        [Header("카메라 — 분리 모드 (현행 프리팹 값)")]
        [SerializeField, Min(0.01f)] private float _splitNearClip = 0.3f;
        [SerializeField, Range(30f, 110f)] private float _splitFieldOfView = 60f;
        [SerializeField] private Vector3 _splitCameraLocalOffset = Vector3.zero;

        [Header("카메라 — 통합 1인칭")]
        [Tooltip("손 무기의 개머리판·그립이 카메라 앞으로 파고든다 — 0.3이면 잘린다 (§3.4).")]
        [SerializeField, Min(0.01f)] private float _unifiedNearClip = 0.08f;

        [Tooltip("60에서 §1.4의 배치가 성립한다. 팔 도달이 빠듯하면 65~70까지 올려 본다 (R1).")]
        [SerializeField, Range(30f, 110f)] private float _unifiedFieldOfView = 60f;

        [Tooltip("카메라 미세 조정 여지 — 무기가 화면에 놓이는 위치를 손대지 않고 옮길 때 쓴다.")]
        [SerializeField] private Vector3 _unifiedCameraLocalOffset = Vector3.zero;

        /// <summary>스폰 시 적용할 모드 — 통합이 합격하면 이 한 필드를 뒤집는다 (§8).</summary>
        public PlayerViewMode DefaultMode => _defaultMode;

        /// <summary>F10 런타임 전환 허용 여부.</summary>
        public bool DebugToggleEnabled => _debugToggleEnabled;

        /// <summary>모드별 근평면 (m).</summary>
        public float GetNearClip(PlayerViewMode mode)
        {
            return mode == PlayerViewMode.UnifiedFirstPerson ? _unifiedNearClip : _splitNearClip;
        }

        /// <summary>모드별 수직 시야각 (도).</summary>
        public float GetFieldOfView(PlayerViewMode mode)
        {
            return mode == PlayerViewMode.UnifiedFirstPerson ? _unifiedFieldOfView : _splitFieldOfView;
        }

        /// <summary>모드별 카메라 로컬 오프셋 (피벗 기준 m) — 분리 모드는 프리팹 값 그대로 0이다.</summary>
        public Vector3 GetCameraLocalOffset(PlayerViewMode mode)
        {
            return mode == PlayerViewMode.UnifiedFirstPerson ? _unifiedCameraLocalOffset : _splitCameraLocalOffset;
        }
    }
}
