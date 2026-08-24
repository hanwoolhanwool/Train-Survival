using System.Collections.Generic;
using Game.Core.Pooling;
using Game.Core.Services;
using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 거치 무기 실물의 표현 계층 (M7 4차 §2.4) — 요크 회전·총구/좌석 앵커 제공.
    /// <see cref="StructureView"/>(피해·수명)와 <b>역할을 나눠</b> 같은 프리팹 루트에 붙는다:
    /// 저쪽은 건축물로서의 생사를, 이쪽은 무기로서의 자세를 맡는다.
    /// 비네트워크다 — 상태는 <see cref="MountedWeaponHost"/>의 복제 리스트와 표현 캐시에 있다.
    /// <para>
    /// 조준각은 <b>표현 전용</b>이라 보간으로 흡수한다. 요크는 열차 프레임(칸의 자식)에 고정이라
    /// 관찰자 이동과 위상차가 생기지 않는다 — 지상 원격 떨림 계열의 문제가 이 축에는 없다 (리스크 2).
    /// </para>
    /// </summary>
    [RequireComponent(typeof(StructureView))]
    public sealed class MountedWeaponView : MonoBehaviour, IPoolable
    {
        [Tooltip("좌우 회전 축 — 거치대 로컬 yaw를 받는다. 비면 회전 표현을 건너뛴다.")]
        [SerializeField] private Transform _yawPivot;

        [Tooltip("상하 회전 축 — 앙각을 받는다(화면 좌표계라 부호가 뒤집힌다). 비면 yaw만 돈다.")]
        [SerializeField] private Transform _pitchPivot;

        [Tooltip("총구 앵커(MuzzleTip) — 트레이서가 나가는 지점. 비면 요크 위치로 물러선다.")]
        [SerializeField] private Transform _muzzle;

        [Tooltip("좌석 앵커 — 점유자의 눈이 놓이는 지점. 비면 설정의 좌석 오프셋으로 물러선다.")]
        [SerializeField] private Transform _seat;

        [Tooltip("조준 보간 속도 — 클수록 중계 계단이 덜 보이고, 작을수록 유실에 둔감하다.")]
        [SerializeField, Min(1f)] private float _aimLerpSpeed = 14f;

        // 건축물 Id → 실물. 조작 계층이 좌석·총구를 찾는 표현 조회면이다(권위 조회가 아니다).
        private static readonly Dictionary<int, MountedWeaponView> Views =
            new Dictionary<int, MountedWeaponView>();

        private StructureView _structureView;
        private int _registeredId;
        private float _displayYaw;
        private float _displayPitch;

        /// <summary>Id로 실물을 찾는다 — 스폰되지 않았거나 회수됐으면 false.</summary>
        public static bool TryGet(int structureId, out MountedWeaponView view)
        {
            return Views.TryGetValue(structureId, out view) && view != null;
        }

        /// <summary>바인딩된 건축물 Id — 아직 바인딩 전이면 0.</summary>
        public int StructureId => _structureView != null ? _structureView.StructureId : 0;

        /// <summary>좌석 트랜스폼 — 앵커가 없으면 루트로 물러선다(점유자가 무기 안에 서게 되지만 붙기는 한다).</summary>
        public Transform Seat => _seat != null ? _seat : transform;

        /// <summary>총구 지점 — 트레이서의 출발점. 판정 원점이 아니다(판정은 좌석 기준이다).</summary>
        public Vector3 MuzzlePosition => _muzzle != null ? _muzzle.position : transform.position + Vector3.up;

        private void Awake()
        {
            _structureView = GetComponent<StructureView>();
        }

        private void OnDisable()
        {
            Unregister();
        }

        public void OnSpawned()
        {
            // 풀 재사용 — 이전 개체의 자세가 새지 않게 되돌린다. Id는 StructureView.Bind가 새로 문다.
            _displayYaw = 0f;
            _displayPitch = 0f;
            ApplyPivots();
        }

        public void OnDespawned()
        {
            Unregister();
        }

        private void Update()
        {
            // Bind는 스폰 직후 스포너가 호출하므로 Id는 첫 프레임에 늦게 확정된다 — 바뀌는 순간 등재한다.
            int id = StructureId;
            if (id != _registeredId)
            {
                Unregister();
                if (id > 0)
                {
                    Views[id] = this;
                    _registeredId = id;
                }
            }

            float targetYaw = 0f;
            float targetPitch = 0f;
            if (_registeredId > 0
                && ServiceLocator.TryGet(out IMountedWeapons mounted)
                && mounted.TryGetAim(_registeredId, out float yaw, out float pitch))
            {
                targetYaw = yaw;
                targetPitch = pitch;
            }

            float step = _aimLerpSpeed * Time.deltaTime;
            _displayYaw = Mathf.LerpAngle(_displayYaw, targetYaw, step);
            _displayPitch = Mathf.LerpAngle(_displayPitch, targetPitch, step);
            ApplyPivots();
        }

        private void ApplyPivots()
        {
            if (_yawPivot != null)
            {
                _yawPivot.localRotation = Quaternion.Euler(0f, _displayYaw, 0f);
            }

            if (_pitchPivot != null)
            {
                // 앙각(위 +)을 화면 좌표계(아래 +)로 옮긴다 — MountedAimMath와 같은 규약.
                _pitchPivot.localRotation = Quaternion.Euler(-_displayPitch, 0f, 0f);
            }
        }

        private void Unregister()
        {
            if (_registeredId > 0 && Views.TryGetValue(_registeredId, out MountedWeaponView view)
                && ReferenceEquals(view, this))
            {
                Views.Remove(_registeredId);
            }

            _registeredId = 0;
        }
    }
}
