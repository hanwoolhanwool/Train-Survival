using Game.Core.Events;
using Game.Core.Services;
using Unity.Netcode;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 팀 공유 자원 카운터 — 호스트 권위 NetworkVariable.
    /// 값 변경(동기화 수신) 시점에 권위 이벤트 <see cref="ResourceAcquiredEvent"/>를 발행한다.
    /// Game 씬에 1개 배치한다.
    /// </summary>
    public sealed class SharedResourceCounter : NetworkBehaviour, ISharedResourceCounter
    {
        private readonly NetworkVariable<int> _total = new NetworkVariable<int>();

        public int Total => _total.Value;

        public override void OnNetworkSpawn()
        {
            _total.OnValueChanged += OnTotalChanged;

            if (!ServiceLocator.IsRegistered<ISharedResourceCounter>())
            {
                ServiceLocator.Register<ISharedResourceCounter>(this);
            }
        }

        public override void OnNetworkDespawn()
        {
            _total.OnValueChanged -= OnTotalChanged;

            if (ServiceLocator.TryGet(out ISharedResourceCounter counter) && ReferenceEquals(counter, this))
            {
                ServiceLocator.Unregister<ISharedResourceCounter>();
            }
        }

        public void AddResource()
        {
            if (IsServer)
            {
                _total.Value += 1;
            }
        }

        private static void OnTotalChanged(int previous, int current)
        {
            EventBus<ResourceAcquiredEvent>.Publish(new ResourceAcquiredEvent(current));
        }
    }
}
