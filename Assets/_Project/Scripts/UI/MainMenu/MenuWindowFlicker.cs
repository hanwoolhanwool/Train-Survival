using UnityEngine;

namespace Game.UI.MainMenu
{
    /// <summary>
    /// 객차 창문 불빛을 느리게 흔든다 — [로비·메인 메뉴 구현 계획](docs/plans/features/로비-메인메뉴-구현-계획.md) §6.2.
    ///
    /// <para>발광은 <c>T_Train_Menu_Emission</c>이 이미 창문과 헤드램프만 골라내고 있으므로
    /// (2차 §13 ⑪), 여기서는 <b>그 세기만 흔든다.</b> 차체 전체가 밝아지지 않는 이유가 그 마스크다.</para>
    ///
    /// <para><b>머티리얼을 복제하지 않는다.</b> <see cref="MaterialPropertyBlock"/>으로 인스턴스별
    /// 값만 덮어쓰므로 <c>M_Train_Locomotive_Menu</c> 에셋이 더럽혀지지 않고 배치도 하나로 유지된다.</para>
    ///
    /// <para>꺼지지는 않는다 — 하한이 0보다 크다. 밤 화면에서 창문이 사라지면 기차가 죽은 것처럼 보인다.</para>
    /// </summary>
    public sealed class MenuWindowFlicker : MonoBehaviour
    {
        /// <summary>URP Lit의 발광 색 프로퍼티.</summary>
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField]
        [Tooltip("비우면 자신·자식에서 찾는다.")]
        private Renderer _target;

        [SerializeField]
        [Tooltip("가장 어두울 때의 배율. 0보다 커야 창문이 꺼지지 않는다.")]
        private float _min = 0.86f;

        [SerializeField]
        [Tooltip("가장 밝을 때의 배율.")]
        private float _max = 1.14f;

        [SerializeField]
        [Tooltip("한 번 오가는 대략의 주기 (초). 짧으면 깜빡임으로, 길면 멈춘 것으로 보인다.")]
        private float _period = 9f;

        [SerializeField]
        private float _seed = 2f;

        private MaterialPropertyBlock _block;
        private Color _baseEmission;
        private bool _ready;

        private void OnEnable()
        {
            if (_target == null)
            {
                _target = GetComponentInChildren<Renderer>();
            }

            if (_target == null || _target.sharedMaterial == null ||
                !_target.sharedMaterial.HasProperty(EmissionColorId))
            {
                _ready = false;
                return;
            }

            _baseEmission = _target.sharedMaterial.GetColor(EmissionColorId);
            _block = new MaterialPropertyBlock();
            _ready = true;
        }

        private void OnDisable()
        {
            if (_ready && _target != null)
            {
                _target.SetPropertyBlock(null);
            }
        }

        private void LateUpdate()
        {
            if (!_ready)
            {
                return;
            }

            float weight = MenuNoise.Flicker(Time.unscaledTime, _min, _max, _period, _seed);

            _target.GetPropertyBlock(_block);
            _block.SetColor(EmissionColorId, _baseEmission * weight);
            _target.SetPropertyBlock(_block);
        }
    }
}
