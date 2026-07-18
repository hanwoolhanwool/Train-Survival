namespace Game.Core.Pooling
{
    /// <summary>
    /// 풀에서 스폰/디스폰될 때 콜백을 받는 컴포넌트 계약.
    /// 풀 재사용 시 상태 초기화는 Awake/OnDestroy가 아니라 이 콜백에서 처리한다.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>풀에서 꺼내져 활성화된 직후 호출된다.</summary>
        void OnSpawned();

        /// <summary>풀로 반환되어 비활성화되기 직전 호출된다.</summary>
        void OnDespawned();
    }
}
