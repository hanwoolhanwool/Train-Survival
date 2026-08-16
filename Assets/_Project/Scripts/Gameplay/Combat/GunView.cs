using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// 총기 1인칭 뷰모델 (M8 1차 — 에셋 적용). 해당 총기 슬롯을 드는 동안만 보인다.
    /// 리볼버·샷건이 같은 <see cref="GunController"/> 클래스라 표현 대상은 직렬화 참조로 구분한다.
    /// 표시 조건은 소유자 게이트(<see cref="GunController.InputEnabled"/>)를 그대로 따르므로
    /// 원격 피어에서는 항상 꺼져 있다(TP 무기 표현은 1차 범위 밖 — RepairHammerView와 같은 계약).
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
