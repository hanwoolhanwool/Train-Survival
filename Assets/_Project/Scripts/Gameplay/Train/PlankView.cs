using Game.Core.Pooling;
using Game.Core.Services;
using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 칸 옆면 판자 1열의 실물 (건축 개편 3차 — 계획서 §2.9). 프리팹 루트에 BoxCollider와 함께 붙는다 —
    /// NetworkObject 없음: 판자 열 수는 <see cref="CarState"/>가 복제하므로 각 피어가
    /// <see cref="PlankViewSpawner"/>를 통해 PoolManager로 로컬 스폰한다.
    /// 판자는 파괴 대상이 아니므로(계획서 §2.9 — 1차 결정) <see cref="Game.Gameplay.Combat.IDamageable"/>을
    /// 구현하지 않는다. 칸 자식으로 스폰돼 이탈 이동을 그대로 따라가고, 소실 거리에서 표현만 끈다
    /// (<see cref="StructureView"/>·<see cref="CarView"/>와 같은 규약).
    /// </summary>
    public sealed class PlankView : MonoBehaviour, IPoolable
    {
        [Tooltip("이탈 칸이 뒤로 이만큼(m) 멀어지면 표현을 끈다 — CarView의 소실 표현과 같은 거리로 맞춘다.")]
        [SerializeField, Min(5f)] private float _ejectHideMeters = 50f;

        private Renderer[] _renderers;
        private Collider[] _colliders;
        private int _carIndex = -1;
        private bool _visible = true;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            _colliders = GetComponentsInChildren<Collider>(includeInactive: true);
        }

        /// <summary>스폰 직후 스포너가 호출한다 — 어느 칸에 얹힌 판자인지 물린다.</summary>
        public void Bind(int carIndex)
        {
            _carIndex = carIndex;
        }

        private void Update()
        {
            if (_carIndex < 0 || !ServiceLocator.TryGet(out ITrainState train))
            {
                return;
            }

            SetPresentation(train.GetEjectOffset(_carIndex) < _ejectHideMeters);
        }

        public void OnSpawned()
        {
            // 풀 재사용 — 이전 개체의 숨김 상태가 새지 않게 보이는 상태로 되돌린다.
            _visible = false;
            SetPresentation(true);
        }

        public void OnDespawned()
        {
            _carIndex = -1;
        }

        private void SetPresentation(bool visible)
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

            for (int i = 0; i < _colliders.Length; i++)
            {
                _colliders[i].enabled = visible;
            }
        }
    }
}
