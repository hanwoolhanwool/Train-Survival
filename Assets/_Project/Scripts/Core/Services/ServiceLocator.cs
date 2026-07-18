using System;
using System.Collections.Generic;

namespace Game.Core.Services
{
    /// <summary>
    /// 전역 서비스 레지스트리. 서비스는 가능하면 인터페이스 타입으로 등록해
    /// 사용처가 구현체가 아닌 추상에 의존하게 한다 (DIP).
    /// </summary>
    /// <example>
    /// <code>
    /// ServiceLocator.Register&lt;ISaveService&gt;(new FileSaveService());
    /// var save = ServiceLocator.Get&lt;ISaveService&gt;();
    /// </code>
    /// </example>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>();

        /// <summary>서비스를 등록한다. 같은 타입을 중복 등록하면 예외를 던진다.</summary>
        public static void Register<T>(T service) where T : class
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            Type type = typeof(T);
            if (Services.ContainsKey(type))
            {
                throw new InvalidOperationException($"이미 등록된 서비스입니다: {type.Name}");
            }

            Services.Add(type, service);
        }

        public static void Unregister<T>() where T : class
        {
            Services.Remove(typeof(T));
        }

        /// <summary>등록된 서비스를 반환한다. 미등록이면 예외를 던진다.</summary>
        public static T Get<T>() where T : class
        {
            if (!Services.TryGetValue(typeof(T), out object service))
            {
                throw new InvalidOperationException($"등록되지 않은 서비스입니다: {typeof(T).Name}");
            }

            return (T)service;
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (Services.TryGetValue(typeof(T), out object found))
            {
                service = (T)found;
                return true;
            }

            service = null;
            return false;
        }

        public static bool IsRegistered<T>() where T : class
        {
            return Services.ContainsKey(typeof(T));
        }

        /// <summary>모든 서비스를 해제한다. 씬 리로드·테스트 정리 용도.</summary>
        public static void Clear()
        {
            Services.Clear();
        }
    }
}
