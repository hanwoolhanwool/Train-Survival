using Game.Core.Services;
using UnityEngine;

namespace Game.Systems.Loading
{
    /// <summary>
    /// 씬에 놓이는 프리로드 스텝의 공통 뼈대 —
    /// [인게임 진입 로딩 구현 계획](docs/plans/features/인게임-진입-로딩-구현-계획.md) §6.2.
    ///
    /// <para><b>등록·해제만 맡는다.</b> 무엇을 만드는지는 파생 클래스가 알고,
    /// 코디네이터는 등록된 것을 돌릴 뿐 무엇인지 모른다.</para>
    ///
    /// <para><b>등록을 <c>OnEnable</c>과 <c>Start</c> 양쪽에서 시도하는 이유</b>: Boot에 놓인 스텝은
    /// 코디네이터와 <b>같은 오브젝트</b>에 있을 수 있고, 그때 <c>OnEnable</c> 순서는 컴포넌트
    /// 나열 순서에 좌우된다. 스텝이 먼저 깨면 등록부가 아직 없다 — <c>Start</c>가 그 경우를 받는다.
    /// 등록은 멱등이므로 두 번 불려도 안전하다.</para>
    ///
    /// <para><b>해제는 <c>OnDisable</c>에서 반드시 한다.</b> 인게임 씬의 스텝은 씬과 함께
    /// 사라지는데 등록부에 남아 있으면 다음 로딩이 <b>파괴된 오브젝트를 돌린다.</b></para>
    /// </summary>
    public abstract class SessionPreloadStepBehaviour : MonoBehaviour, ISessionPreloadStep
    {
        private bool _registered;

        public abstract PreloadPhase Phase { get; }

        public abstract int Total { get; }

        public abstract int Done { get; }

        public abstract void Advance();

        protected virtual void OnEnable()
        {
            TryRegister();
        }

        protected virtual void Start()
        {
            TryRegister();
        }

        protected virtual void OnDisable()
        {
            if (!_registered)
            {
                return;
            }

            if (ServiceLocator.TryGet(out ISessionPreloadRegistry registry))
            {
                registry.Unregister(this);
            }

            _registered = false;
        }

        private void TryRegister()
        {
            if (_registered || !ServiceLocator.TryGet(out ISessionPreloadRegistry registry))
            {
                return;
            }

            registry.Register(this);
            _registered = true;
        }
    }
}
