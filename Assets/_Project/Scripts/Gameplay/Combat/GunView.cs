using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// 총기 FP 뷰모델 (M8 1차 — 에셋 적용). 해당 총기 슬롯을 드는 동안 소유자 화면에만 보인다.
    /// 리볼버·샷건이 같은 <see cref="GunController"/> 클래스라 표현 대상은 직렬화 참조로 구분한다.
    /// 원격 피어의 표현은 손 소켓의 TP 월드모델(<see cref="Player.HeldWeaponSocket"/>)이 맡는다
    /// (무기 손 파지 계획 §2.2 — FP/TP 분리).
    /// Player 프리팹에서 무기 모델의 부모 피벗에 부착한다.
    ///
    /// <para><b>통합 1인칭</b>에서는 이 화면 전용 뷰모델을 띄우지 않는다 — 손에 쥔 무기가 그 자리를
    /// 대신한다 (1인칭 통합 시점 전환 계획 §3.2). 원격 프록시의 모드는 항상 분리에 머물러 있어
    /// (<see cref="Player.PlayerViewModeController.CanDrive"/>) 이 조건은 자기 화면에만 걸린다.</para>
    /// </summary>
    public sealed class GunView : MonoBehaviour
    {
        [Tooltip("이 뷰모델이 표현하는 총기 — 같은 클래스의 총기가 여럿이라 참조로 지정한다.")]
        [SerializeField] private GunController _gun;

        private Player.IPlayerViewMode _viewMode;
        private Renderer[] _renderers;
        private bool _visible;

        private void Awake()
        {
            _viewMode = GetComponentInParent<Player.IPlayerViewMode>();
            _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            _visible = true;
            SetVisible(false);
        }

        private void Update()
        {
            bool unified = _viewMode != null
                && _viewMode.Mode == Player.PlayerViewMode.UnifiedFirstPerson;
            bool visible = !unified && _gun != null && _gun.IsSpawned
                && _gun.IsOwner && _gun.InputEnabled;
            if (visible != _visible)
            {
                SetVisible(visible);
            }
        }

        private void SetVisible(bool visible)
        {
            if (_visible == visible)
            {
                return;
            }

            _visible = visible;
            for (int i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].enabled = visible;
            }
        }
    }
}
