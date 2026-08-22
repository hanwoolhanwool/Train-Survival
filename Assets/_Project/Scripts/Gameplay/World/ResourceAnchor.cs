using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 자원 앵커가 놓인 지형의 성격 — 자원 종류와 짝지어 "자원은 지형의 결과"를 성립시킨다
    /// (레벨 디자인 가이드 §4.4·§5.2).
    /// </summary>
    public enum ResourceAnchorKind : byte
    {
        /// <summary>평범한 지면 — 목재·돌·식재료·벼.</summary>
        Ground = 0,

        /// <summary>노출 암반·절개면 아래 — 돌·광맥·희귀 금속.</summary>
        Rock = 1,

        /// <summary>물가 — 원목·식재료·얼음.</summary>
        Water = 2,

        /// <summary>잔해·폐허 — 고철·유적 부품·소금.</summary>
        Wreck = 3,
    }

    /// <summary>
    /// 지형 세그먼트가 제공하는 자원 배치 지점 (레벨 디자인 가이드 §4.4).
    /// 스포너가 좌표를 랜덤으로 만드는 대신 <b>이 앵커를 고른다</b> — 지형 높이가 이미 반영된
    /// 위치이므로 자원이 바위를 뚫거나 절개면 공중에 뜨지 않는다.
    ///
    /// <para>앵커는 <b>호스트의 로컬 판단</b>에만 쓰인다. 자원 위치는 스폰 시점의 (위치, 누적 거리)
    /// 바인딩만 복제되고 이후는 각 피어가 공통 누적 거리로 유도하므로, 앵커가 피어마다 달라도
    /// 심어진 자원은 전 피어가 같은 곳에서 본다 (가이드 §5.3).</para>
    /// </summary>
    public sealed class ResourceAnchor : MonoBehaviour
    {
        [Tooltip("이 지점의 지형 성격 — 어울리는 자원 종류를 고르는 데 쓴다.")]
        [SerializeField] private ResourceAnchorKind _kind = ResourceAnchorKind.Ground;

        // 활성 앵커 레지스트리 — 타일이 풀에서 켜지고 꺼질 때마다 갱신된다.
        // FindObjectsOfType 대신 이 목록을 쓴다 (매 스폰마다 씬 전체를 뒤지지 않는다).
        private static readonly List<ResourceAnchor> ActiveAnchors = new List<ResourceAnchor>(64);

        public ResourceAnchorKind Kind => _kind;

        /// <summary>이번 활성 구간에서 이미 자원이 심긴 앵커인지. 타일이 재사용되면 리셋된다.</summary>
        public bool IsUsed { get; private set; }

        public static IReadOnlyList<ResourceAnchor> Active => ActiveAnchors;

        /// <summary>테스트 전용 — 정적 레지스트리를 비운다 (EditMode TearDown 규약).</summary>
        public static void ClearRegistry()
        {
            ActiveAnchors.Clear();
        }

        public void MarkUsed()
        {
            IsUsed = true;
        }

        /// <summary>
        /// 자원 종류에 어울리는 지형 성격 (레벨 디자인 가이드 §5.2) — 순수 함수.
        /// "바위는 절개면 아래, 나무는 물가, 광맥은 노출 암반"을 데이터가 아니라 규칙으로 고정한다.
        /// 미등재 종류는 <see cref="ResourceAnchorKind.Ground"/>라 어느 앵커든 쓸 수 있다.
        /// </summary>
        public static ResourceAnchorKind PreferredKindFor(Inventory.ResourceType type)
        {
            switch (type)
            {
                case Inventory.ResourceType.Stone:
                case Inventory.ResourceType.Niter:
                case Inventory.ResourceType.OreVein:
                case Inventory.ResourceType.RareMetal:
                    return ResourceAnchorKind.Rock;

                case Inventory.ResourceType.Timber:
                case Inventory.ResourceType.Ice:
                    return ResourceAnchorKind.Water;

                case Inventory.ResourceType.Scrap:
                case Inventory.ResourceType.Salt:
                case Inventory.ResourceType.RelicPart:
                    return ResourceAnchorKind.Wreck;

                default:
                    return ResourceAnchorKind.Ground;
            }
        }

        /// <summary>
        /// 앵커가 이 스폰에 쓸 수 있는 자리인지 — 순수 판정.
        /// 측면 대역(<paramref name="minLateral"/>~<paramref name="maxLateral"/>)은 집게 사거리와
        /// 갑판 폭이 정한 클리어 존이고(가이드 §4.2), Z는 목표 지점에서 너무 멀면 "그 자리에 심은 것"이
        /// 아니게 되므로 제한한다.
        /// </summary>
        public static bool IsEligible(
            Vector3 position, float targetZ, float minLateral, float maxLateral, float maxZDistance)
        {
            float lateral = Mathf.Abs(position.x);
            if (lateral < minLateral || lateral > maxLateral)
            {
                return false;
            }

            return Mathf.Abs(position.z - targetZ) <= maxZDistance;
        }

        /// <summary>
        /// 목표 Z에 가장 가까운 미사용 앵커를 고른다. 조건에 맞는 것이 없으면 null —
        /// 호출자는 기존 랜덤 좌표로 폴백한다 (팔레트가 없는 지역에서도 자원이 끊기지 않는다).
        /// </summary>
        /// <param name="preferredKind">우선 종류. 같은 조건이면 이 성격의 앵커를 먼저 고른다.</param>
        public static ResourceAnchor TryPick(
            float targetZ, float minLateral, float maxLateral, float maxZDistance,
            ResourceAnchorKind preferredKind)
        {
            ResourceAnchor bestPreferred = null;
            ResourceAnchor bestAny = null;
            float nearestPreferred = float.MaxValue;
            float nearestAny = float.MaxValue;

            for (int i = 0; i < ActiveAnchors.Count; i++)
            {
                ResourceAnchor anchor = ActiveAnchors[i];
                if (anchor == null || anchor.IsUsed)
                {
                    continue;
                }

                Vector3 position = anchor.transform.position;
                if (!IsEligible(position, targetZ, minLateral, maxLateral, maxZDistance))
                {
                    continue;
                }

                float distance = Mathf.Abs(position.z - targetZ);
                if (distance < nearestAny)
                {
                    nearestAny = distance;
                    bestAny = anchor;
                }

                if (anchor.Kind == preferredKind && distance < nearestPreferred)
                {
                    nearestPreferred = distance;
                    bestPreferred = anchor;
                }
            }

            return bestPreferred != null ? bestPreferred : bestAny;
        }

        private void OnEnable()
        {
            // 풀에서 다시 켜진 타일의 앵커는 "아직 안 쓴 자리"로 돌아간다 —
            // 리셋을 빠뜨리면 재사용 타일에 자원이 영원히 안 심긴다.
            IsUsed = false;
            ActiveAnchors.Add(this);
        }

        private void OnDisable()
        {
            ActiveAnchors.Remove(this);
        }
    }
}
