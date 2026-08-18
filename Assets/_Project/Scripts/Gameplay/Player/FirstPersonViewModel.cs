using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 화면 전용 무기 뷰모델의 공통 규약 (1인칭 통합 시점 전환 계획 §3.2) — 표현 전용.
    /// AimPivot 아래 무기 피벗에 붙어 <b>카메라 앞에 띄운 화면 장치</b>로서 세 가지를 똑같이 건다:
    ///
    /// <list type="number">
    /// <item><b>그림자를 내지 않는다</b> — 실제 위치에 있는 물체가 아니다. 그림자는 손에 쥔
    /// TP 월드모델(<see cref="HeldWeaponSocket"/>)이 담당하며, 둘 다 내면 같은 무기의 그림자가
    /// 두 개로 보인다 (1차 검증 버그).</item>
    /// <item><b>통합 1인칭에서는 표시하지 않는다</b> — 손에 쥔 무기가 그 자리를 대신한다.
    /// 원격 프록시의 모드는 분리에 머물러 있어(<see cref="PlayerViewModeController.CanDrive"/>)
    /// 이 조건은 자기 화면에만 걸린다.</item>
    /// <item><b>드는 동안만 보인다</b> — 그 판정만 파생이 자기 컨트롤러로 내린다.</item>
    /// </list>
    ///
    /// <para>파생이 정하는 것은 <see cref="IsHeldByOwner"/> 하나뿐이다. 무기가 늘어도 이 클래스는
    /// 변하지 않는다 (OCP). <see cref="Awake"/>를 오버라이드하면 <c>base.Awake()</c> 호출을
    /// 누락하지 않는다 (LSP — <c>MonoSingleton&lt;T&gt;</c>와 같은 규약).</para>
    /// </summary>
    public abstract class FirstPersonViewModel : MonoBehaviour
    {
        private IPlayerViewMode _viewMode;
        private Renderer[] _renderers;
        private bool _visible;

        /// <summary>
        /// 소유자가 이 뷰모델의 무기를 지금 들고 있는가 — 파생이 자기 컨트롤러의 입력 게이트로 판정한다.
        /// 원격 피어의 표현은 손 소켓의 TP 월드모델이 맡으므로 여기서는 소유자만 따진다
        /// (무기 손 파지 계획 §2.2 — FP/TP 분리).
        /// </summary>
        protected abstract bool IsHeldByOwner { get; }

        protected virtual void Awake()
        {
            _viewMode = GetComponentInParent<IPlayerViewMode>();
            _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);

            // 그림자 차단은 한 번만 걸면 된다 — 표시 토글과 무관하다.
            for (int i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].shadowCastingMode = ShadowCastingMode.Off;
            }

            _visible = true;
            SetVisible(false);
        }

        private void Update()
        {
            bool unified = _viewMode != null
                && _viewMode.Mode == PlayerViewMode.UnifiedFirstPerson;
            SetVisible(!unified && IsHeldByOwner);
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
