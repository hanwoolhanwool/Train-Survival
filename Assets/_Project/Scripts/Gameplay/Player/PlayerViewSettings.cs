using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 시점 모드 표현 데이터 (1인칭 통합 시점 전환 계획 §3.1·§3.4) — 판정 무관, 전부 표현 층위.
    /// 두 모드의 값을 <b>나란히</b> 들고 있어야 QA가 한 키로 오갈 수 있다 (§4.1).
    /// 통합 모드가 합격하면 분리 쪽 값과 함께 이 에셋도 정리된다 (§8).
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

        [Tooltip("카메라를 눈보다 살짝 앞으로 — 목·턱이 화면에 침범하는 것을 줄인다 (결정 ③ ⓑ 몫).")]
        [SerializeField] private Vector3 _unifiedCameraLocalOffset = new Vector3(0f, 0f, 0.06f);

        [Header("머리 은닉 (결정 ③ ⓒ — 본 축소 + near clip 병용)")]
        [Tooltip("통합 모드에서 머리 본을 축소해 카메라 시야에서 치운다. 끄면 near clip에만 의존한다.")]
        [SerializeField] private bool _hideHeadBone = true;

        [Tooltip("0이 아닌 극소값을 쓴다 — 완전한 0은 스키닝 법선을 퇴화시킬 수 있다.")]
        [SerializeField, Range(0.0001f, 0.2f)] private float _headHiddenScale = 0.001f;

        [Header("QA 표시")]
        [Tooltip("모드 전환 토스트 유지 시간 (초) — 0이면 표시하지 않는다 (§4.1).")]
        [SerializeField, Min(0f)] private float _modeToastSeconds = 2f;

        /// <summary>스폰 시 적용할 모드 — 통합이 합격하면 이 한 필드를 뒤집는다 (§8).</summary>
        public PlayerViewMode DefaultMode => _defaultMode;

        /// <summary>F10 런타임 전환 허용 여부.</summary>
        public bool DebugToggleEnabled => _debugToggleEnabled;

        /// <summary>모드 전환 토스트 유지 시간 (초).</summary>
        public float ModeToastSeconds => _modeToastSeconds;

        /// <summary>통합 모드에서 머리 본을 축소해 은닉할지.</summary>
        public bool HideHeadBone => _hideHeadBone;

        /// <summary>은닉된 머리 본의 로컬 스케일 배율.</summary>
        public float HeadHiddenScale => _headHiddenScale;

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
