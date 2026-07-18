using System;

namespace Game.Core.Events
{
    /// <summary>
    /// 시스템 간 통신용 정적 이벤트 버스. 이벤트 타입 <typeparamref name="T"/>별로 독립된 채널을 가진다.
    /// 이벤트는 readonly struct로 정의해 발행 시점의 값 스냅숏을 전달한다.
    /// </summary>
    /// <example>
    /// <code>
    /// public readonly struct PlayerDiedEvent { public readonly int Score; ... }
    ///
    /// EventBus&lt;PlayerDiedEvent&gt;.Subscribe(OnPlayerDied);
    /// EventBus&lt;PlayerDiedEvent&gt;.Publish(new PlayerDiedEvent(score));
    /// EventBus&lt;PlayerDiedEvent&gt;.Unsubscribe(OnPlayerDied); // OnDisable 등에서 반드시 해제
    /// </code>
    /// </example>
    public static class EventBus<T> where T : struct
    {
        private static event Action<T> OnEvent;

        public static void Subscribe(Action<T> handler)
        {
            OnEvent += handler;
        }

        public static void Unsubscribe(Action<T> handler)
        {
            OnEvent -= handler;
        }

        public static void Publish(T payload)
        {
            OnEvent?.Invoke(payload);
        }

        /// <summary>모든 구독을 해제한다. 씬 리로드·테스트 정리 용도.</summary>
        public static void Clear()
        {
            OnEvent = null;
        }
    }
}
