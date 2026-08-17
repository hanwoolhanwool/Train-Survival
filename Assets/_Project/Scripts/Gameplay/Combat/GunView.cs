using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// 총기 FP 뷰모델 (M8 1차 — 에셋 적용). 해당 총기 슬롯을 드는 동안 소유자 화면에만 보인다.
    /// 리볼버·샷건이 같은 <see cref="GunController"/> 클래스라 표현 대상은 직렬화 참조로 구분한다.
    /// 원격 피어의 표현은 손 소켓의 TP 월드모델(<see cref="Player.HeldWeaponSocket"/>)이 맡는다
    /// (무기 손 파지 계획 §2.2 — FP/TP 분리).
    /// Player 프리팹에서 무기 모델의 부모 피벗에 부착한다.
    /// </summary>
    public sealed class GunView : MonoBehaviour
    {
        [Tooltip("이 뷰모델이 표현하는 총기 — 같은 클래스의 총기가 여럿이라 참조로 지정한다.")]
        [SerializeField] private GunController _gun;

        private Renderer[] _renderers;
        private bool _visible;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            _visible = true;
            SetVisible(false);
        }

        private void Update()
        {
            bool visible = _gun != null && _gun.IsSpawned && _gun.IsOwner && _gun.InputEnabled;
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
