using System;
using Game.Gameplay.Inventory;
using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 파지 자세 카테고리 — Hold 레이어의 포즈 클립 묶음 키 (품질 업그레이드 계획 기술 확정 ④).
    /// 복제된 <see cref="HotbarItemType"/>에서 각 피어가 로컬 유도한다 (동기화 채널 0).
    /// </summary>
    public enum WeaponHoldPose
    {
        /// <summary>파지 없음 — Hold 레이어를 내린다 (빈손·자원·UI 열림·사망).</summary>
        None = 0,

        /// <summary>한손 조준 — 리볼버.</summary>
        OneHandAim = 1,

        /// <summary>양손 조준 — 샷건·집게.</summary>
        TwoHandAim = 2,

        /// <summary>한손 하단 파지 — 망치·근접 (팔을 내린 채 쥔다).</summary>
        OneHandLow = 3,
    }

    /// <summary>
    /// 무기 손 파지 표현 데이터 (무기 손 파지 계획 §2.2) — 판정 무관, 전부 표현 층위.
    /// 무기 추가·자세 조정은 이 에셋의 엔트리 편집만으로 끝난다 (OCP — 파지 계획 §4):
    /// <see cref="HeldWeaponSocket"/>·<see cref="WeaponHoldIk"/>는 <see cref="HotbarItemType"/>
    /// 키로 엔트리를 조회할 뿐 무기 컨트롤러 구체 타입을 모른다.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponHoldSettings", menuName = "Game/Weapon Hold Settings")]
    public sealed class WeaponHoldSettings : ScriptableObject
    {
        /// <summary>무기 1종의 파지 데이터 — 소켓 오프셋(오른손 본 로컬) + 홀드 타깃(조준 피벗 로컬).</summary>
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private HotbarItemType _itemType;

            [Header("소켓 오프셋 (오른손 소켓 로컬)")]
            [SerializeField] private Vector3 _socketLocalPosition;
            [SerializeField] private Vector3 _socketLocalEulerAngles;
            [SerializeField] private Vector3 _socketLocalScale = Vector3.one;

            [Header("조준 자세 (총기만 — 파지 계획 결정 ③)")]
            [Tooltip("켜면 드는 동안 항상 팔을 들어 겨눈다. 꺼진 무기는 소켓 부착만 한다 (망치·근접).")]
            [SerializeField] private bool _aimPose;

            [Tooltip("왼손도 홀드 타깃에 붙인다 (샷건·집게).")]
            [SerializeField] private bool _twoHanded;

            [SerializeField] private Vector3 _rightHandLocalPosition;
            [SerializeField] private Vector3 _rightHandLocalEulerAngles;
            [SerializeField] private Vector3 _leftHandLocalPosition;
            [SerializeField] private Vector3 _leftHandLocalEulerAngles;

            [Header("손목 펴기 (포즈 편집 계획 E5)")]
            [Tooltip("켜면 손 회전을 실제 전완 방향에서 계산한다 — 팔이 어떻게 서든 손목이 펴진다. " +
                     "위의 손 오일러값은 쓰이지 않고 아래 롤·굽힘이 대신한다.")]
            [SerializeField] private bool _straightenWrist;

            [Tooltip("전완 축 둘레 회전 (도) — 0이면 손등이 하늘. 총을 세우려면 ±90 부근.")]
            [SerializeField, Range(-180f, 180f)] private float _rightWristRollDegrees;

            [Tooltip("손목 굽힘 (도) — +면 손등 쪽으로 젖힌다. 0이 곧게 편 상태.")]
            [SerializeField, Range(-60f, 60f)] private float _rightWristBendDegrees;

            [SerializeField, Range(-180f, 180f)] private float _leftWristRollDegrees;
            [SerializeField, Range(-60f, 60f)] private float _leftWristBendDegrees;

            [Header("팔꿈치 힌트 (조준 피벗 로컬 — 포즈 편집 계획 E3)")]
            [Tooltip("오른 팔꿈치가 향할 지점 — 손 목표와 같은 조준 피벗 로컬 좌표계.")]
            [SerializeField] private Vector3 _rightElbowHintLocalPosition;

            [Tooltip("0이면 힌트를 쓰지 않는다 — 내장 IK의 기본 스윙이 그대로 남는다.")]
            [SerializeField, Range(0f, 1f)] private float _rightElbowHintWeight;

            [SerializeField] private Vector3 _leftElbowHintLocalPosition;
            [SerializeField, Range(0f, 1f)] private float _leftElbowHintWeight;

            [Header("Hold 레이어 (품질 업그레이드 계획 C축)")]
            [Tooltip("이 무기가 쓰는 파지 포즈 카테고리 — Hold 레이어의 클립 묶음을 고른다.")]
            [SerializeField] private WeaponHoldPose _pose = WeaponHoldPose.None;

            [Tooltip("포즈 클립이 자세를 담당할 때 남기는 IK 비율 — 클립이 없으면 1(IK 단독).")]
            [SerializeField, Range(0f, 1f)] private float _ikResidualWeight = 1f;

            [Tooltip("왼손 전용 IK 비율 — 무기를 쥔 오른손과 달리 왼손은 총열에 붙어야 해서 더 높다.")]
            [SerializeField, Range(0f, 1f)] private float _leftIkResidualWeight = 1f;

            public HotbarItemType ItemType => _itemType;
            public Vector3 SocketLocalPosition => _socketLocalPosition;
            public Quaternion SocketLocalRotation => Quaternion.Euler(_socketLocalEulerAngles);
            public Vector3 SocketLocalScale => _socketLocalScale;
            public bool AimPose => _aimPose;
            public bool TwoHanded => _twoHanded;
            public Vector3 RightHandLocalPosition => _rightHandLocalPosition;
            public Quaternion RightHandLocalRotation => Quaternion.Euler(_rightHandLocalEulerAngles);
            public Vector3 LeftHandLocalPosition => _leftHandLocalPosition;
            public Quaternion LeftHandLocalRotation => Quaternion.Euler(_leftHandLocalEulerAngles);

            /// <summary>
            /// 손목을 편 자세로 손을 잡을지 — 켜면 <see cref="WeaponHoldMath.StraightWristRotation"/>이
            /// 실제 전완 방향에서 손 회전을 만든다. 손 오일러를 절대각으로 고정하면 팔이 조금만
            /// 달리 서도 그 차이가 손목 꺾임으로 남는다 (포즈 편집 계획 E5).
            /// </summary>
            public bool StraightenWrist => _straightenWrist;

            /// <summary>오른 손목 롤 (도) — 전완 축 둘레. 0이면 손등이 하늘, 총기는 ±90 부근.</summary>
            public float RightWristRollDegrees => _rightWristRollDegrees;

            /// <summary>오른 손목 굽힘 (도) — +면 손등 쪽. 0이 곧게 편 상태.</summary>
            public float RightWristBendDegrees => _rightWristBendDegrees;

            /// <summary>왼 손목 롤 (도) — 미러라 부호는 <see cref="WeaponHoldMath"/>가 뒤집는다.</summary>
            public float LeftWristRollDegrees => _leftWristRollDegrees;

            /// <summary>왼 손목 굽힘 (도).</summary>
            public float LeftWristBendDegrees => _leftWristBendDegrees;

            /// <summary>
            /// 오른 팔꿈치 힌트 위치 (조준 피벗 로컬) — 손 목표만으로는 팔꿈치가 어느 쪽으로
            /// 굽을지 정해지지 않는(자유도 1) 것을 데이터로 잡는다 (포즈 편집 계획 §2.3).
            /// 손 목표와 같은 좌표계라 피치를 함께 탄다.
            /// </summary>
            public Vector3 RightElbowHintLocalPosition => _rightElbowHintLocalPosition;

            /// <summary>오른 팔꿈치 힌트 가중치 — 0이면 내장 IK 기본 스윙을 그대로 둔다.</summary>
            public float RightElbowHintWeight => _rightElbowHintWeight;

            /// <summary>왼 팔꿈치 힌트 위치 (조준 피벗 로컬) — 양손 무기에서만 의미가 있다.</summary>
            public Vector3 LeftElbowHintLocalPosition => _leftElbowHintLocalPosition;

            /// <summary>왼 팔꿈치 힌트 가중치 — 0이면 내장 IK 기본 스윙을 그대로 둔다.</summary>
            public float LeftElbowHintWeight => _leftElbowHintWeight;

            /// <summary>Hold 레이어 포즈 카테고리 (C축) — 클립 반입 전에는 <see cref="WeaponHoldPose.None"/>.</summary>
            public WeaponHoldPose Pose => _pose;

            /// <summary>
            /// 포즈 클립 위에 남기는 IK 비율 — 클립이 "자세"를, IK가 "피치 추종·그립 밀착"을 맡는
            /// 합성 규약(업그레이드 계획 §2.1). 클립 반입 전에는 1이라 A안과 동일하게 동작한다.
            /// </summary>
            public float IkResidualWeight => _ikResidualWeight;

            /// <summary>
            /// 왼손 IK 잔여 비율 — 오른손은 무기가 소켓으로 이미 붙어 있어 클립에 맡겨도 되지만,
            /// <b>왼손은 무기 총열까지 가야 그립이 성립</b>하므로 별도로 더 높게 둔다.
            /// </summary>
            public float LeftIkResidualWeight => _leftIkResidualWeight;
        }

        [SerializeField] private Entry[] _entries;

        [Header("TP 조준 피벗 (플레이어 루트 로컬 — 가슴 높이)")]
        [SerializeField] private Vector3 _aimPivotLocalPosition = new Vector3(0f, 1.25f, 0f);

        [Header("IK 가중치")]
        [SerializeField, Range(0f, 1f)] private float _aimHandWeight = 1f;

        [Tooltip("파지 전환 시 팔이 튀지 않게 하는 가중치 블렌드 반감기 (초).")]
        [SerializeField, Min(0f)] private float _ikBlendHalfLifeSeconds = 0.1f;

        [Header("Hold 레이어 (품질 업그레이드 계획 C축)")]
        [Tooltip("포즈 클립 반입 전에는 0 — 빈 레이어가 상체를 덮어써 T포즈로 굳는 것을 막는다.")]
        [SerializeField, Range(0f, 1f)] private float _holdLayerWeight;

        [Tooltip("Hold 레이어 가중치 블렌드 반감기 (초).")]
        [SerializeField, Min(0f)] private float _holdLayerBlendHalfLifeSeconds = 0.15f;

        /// <summary>TP 조준 피벗의 플레이어 루트 로컬 위치 — 복제 피치 회전의 중심 (파지 계획 §2.3).</summary>
        public Vector3 AimPivotLocalPosition => _aimPivotLocalPosition;

        /// <summary>조준 자세일 때 손 IK 목표 가중치 — 애니 에셋 반입 시 데이터로 내리는 스위치 (§2.5).</summary>
        public float AimHandWeight => _aimHandWeight;

        /// <summary>IK 가중치 블렌드 반감기 (초) — <see cref="WeaponHoldMath.StepWeight"/>에 쓴다.</summary>
        public float IkBlendHalfLifeSeconds => _ikBlendHalfLifeSeconds;

        /// <summary>
        /// 파지 중 Hold 레이어 목표 가중치 (C축). <b>포즈 클립을 반입하기 전에는 0</b>이어야 한다 —
        /// 모션이 빈 스테이트가 가중치를 얻으면 상체가 바인드 포즈로 덮어써진다.
        /// 클립을 얹은 뒤 1로 올리고 엔트리의 <see cref="Entry.IkResidualWeight"/>를 내린다.
        /// </summary>
        public float HoldLayerWeight => _holdLayerWeight;

        /// <summary>Hold 레이어 가중치 블렌드 반감기 (초).</summary>
        public float HoldLayerBlendHalfLifeSeconds => _holdLayerBlendHalfLifeSeconds;

        /// <summary>든 아이템의 파지 엔트리 조회 — 없는 아이템(자원 등)은 false.</summary>
        public bool TryGetEntry(HotbarItemType itemType, out Entry entry)
        {
            if (_entries != null && itemType != HotbarItemType.None)
            {
                for (int i = 0; i < _entries.Length; i++)
                {
                    if (_entries[i] != null && _entries[i].ItemType == itemType)
                    {
                        entry = _entries[i];
                        return true;
                    }
                }
            }

            entry = null;
            return false;
        }
    }
}
