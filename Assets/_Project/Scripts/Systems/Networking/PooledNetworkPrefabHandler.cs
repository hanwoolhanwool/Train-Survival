using System;
using Game.Core.Pooling;
using Unity.Netcode;
using UnityEngine;

namespace Game.Systems.Networking
{
    /// <summary>
    /// NGO의 네트워크 스폰/디스폰을 <see cref="PoolManager"/>로 라우팅하는 프리팹 핸들러.
    /// 프리팹당 하나씩 생성해 <c>NetworkManager.PrefabHandler.AddHandler</c>에 등록한다
    /// (일괄 등록은 <see cref="NetworkPrefabPoolRegistrar"/> 사용).
    /// </summary>
    /// <remarks>
    /// - 서버에서 인스턴스를 풀로 반환하려면 <c>NetworkObject.Despawn(destroy: true)</c>를 호출해야 한다.
    ///   destroy가 false면 NGO가 <see cref="Destroy"/>를 호출하지 않아 풀로 돌아오지 않는다.
    /// - 풀링 프리팹은 <c>NetworkObject.AutoObjectParentSync</c>를 꺼야 한다.
    ///   PoolManager가 디스폰 시 인스턴스를 풀 오브젝트 아래로 재부모화하는데,
    ///   부모 동기화가 켜져 있으면 NGO가 이를 되돌리고 에러를 낸다 (디스폰 상태 재부모화 금지 규칙).
    /// </remarks>
    public sealed class PooledNetworkPrefabHandler : INetworkPrefabInstanceHandler
    {
        private readonly GameObject _prefab;

        /// <param name="prefab"><see cref="NetworkObject"/>를 가진 네트워크 프리팹.</param>
        public PooledNetworkPrefabHandler(GameObject prefab)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            NetworkObject networkObject = prefab.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                throw new ArgumentException($"프리팹에 NetworkObject 컴포넌트가 없습니다: {prefab.name}", nameof(prefab));
            }

            if (networkObject.AutoObjectParentSync)
            {
                throw new ArgumentException(
                    $"풀링 프리팹은 NetworkObject의 AutoObjectParentSync를 꺼야 합니다: {prefab.name}. " +
                    "켜져 있으면 PoolManager의 디스폰 재부모화를 NGO가 되돌리고 에러를 냅니다.",
                    nameof(prefab));
            }

            _prefab = prefab;
        }

        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            GameObject instance = PoolManager.Spawn(_prefab, position, rotation);
            return instance.GetComponent<NetworkObject>();
        }

        public void Destroy(NetworkObject networkObject)
        {
            PoolManager.Despawn(networkObject.gameObject);
        }
    }
}
