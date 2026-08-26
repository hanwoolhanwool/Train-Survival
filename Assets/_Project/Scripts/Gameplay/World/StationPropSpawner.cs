using System.Collections.Generic;
using Game.Core.Logging;
using Game.Core.Services;
using Game.Gameplay.Inventory;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 역 소품을 앵커 자리에 심는다 (기차역 2차) —
    /// [기차역 이벤트 구현 계획](docs/plans/features/기차역-이벤트-구현-계획.md) §4.3.
    ///
    /// <para><b>호스트 전용인데 NetworkBehaviour가 아니다.</b> 스폰 계약
    /// (<see cref="IStationPropSpawner"/>)이 <b>서버에서만</b> ServiceLocator에 등록되므로,
    /// 클라이언트에서는 조회가 실패해 이 컴포넌트가 조용히 아무것도 하지 않는다. 네트워크 상태를
    /// 하나도 갖지 않는데 <c>NetworkObject</c>를 요구할 이유가 없다.</para>
    ///
    /// <para><b>타일이 켜질 때 그 타일 것을 전부 심는다.</b> 자원 스포너가 주행 거리마다 하나씩
    /// 심는 것과 다른 주기다 — 그래서 앵커 목록도 따로 둔다(<see cref="StationPropAnchor"/>).
    /// 풀에서 타일이 다시 켜지면 앵커가 리셋되므로 <b>같은 역을 두 번째 지나가면 소품도 다시 있다</b>.
    /// 열차는 되돌아가지 않으니 플레이어가 같은 역을 두 번 볼 일은 없고, 타일 재사용을 막으면
    /// 스트리밍이 깨진다.</para>
    /// </summary>
    public sealed class StationPropSpawner : MonoBehaviour
    {
        [Tooltip("소품 종류별 전리품 표. 종류당 하나씩 — 없는 종류의 앵커는 조용히 건너뛴다.")]
        [SerializeField] private StationLootTable[] _tables;

        [Tooltip("한 프레임에 스폰할 최대 개수. 역 5장이 한꺼번에 켜지면 소품이 20개를 넘을 수 있다.")]
        [SerializeField, Min(1)] private int _maxSpawnsPerFrame = 4;

        // 내용물 조립용 재사용 버퍼 — 소품마다 List를 새로 만들지 않는다.
        private static readonly List<HotbarSlotView> ContentBuffer = new List<HotbarSlotView>(8);

        private const string MissingTableKey = "world.station-loot-table-missing";
        private const int MissingTableLimit = 4;

        private void Update()
        {
            if (_tables == null || _tables.Length == 0)
            {
                return;
            }

            // 서버에만 등록된다 — 클라이언트에서는 여기서 끝난다.
            if (!ServiceLocator.TryGet(out IStationPropSpawner spawner))
            {
                return;
            }

            int budget = Mathf.Max(1, _maxSpawnsPerFrame);
            IReadOnlyList<StationPropAnchor> anchors = StationPropAnchor.Active;

            for (int i = 0; i < anchors.Count && budget > 0; i++)
            {
                StationPropAnchor anchor = anchors[i];
                if (anchor == null || anchor.IsUsed)
                {
                    continue;
                }

                // 성공하든 말든 이 활성 구간에서는 다시 보지 않는다 —
                // 실패를 매 프레임 재시도하면 로그와 비용만 쌓인다.
                anchor.MarkUsed();

                if (TrySpawnAt(spawner, anchor))
                {
                    budget--;
                }
            }
        }

        private bool TrySpawnAt(IStationPropSpawner spawner, StationPropAnchor anchor)
        {
            if (StationLootLogic.RollEmpty(anchor.EmptyChance, UnityEngine.Random.value))
            {
                return false;
            }

            StationLootTable table = FindTable(anchor.Kind);
            if (table == null || !table.HasAnyEntry)
            {
                GameLog.WarnLimited(LogCategory.World, MissingTableKey, MissingTableLimit,
                    $"역 소품 표가 없습니다: {anchor.Kind} — 그 자리는 비워 둡니다.", anchor);
                return false;
            }

            HotbarSlotView[] contents = BuildContents(table);
            if (contents == null)
            {
                return false;
            }

            return spawner.ServerSpawnProp(
                contents, anchor.transform.position, StationLootLogic.RequiredTierFor(anchor.Kind));
        }

        /// <summary>
        /// 표에서 슬롯을 채운다. 호스트만 도는 경로라 <b>결정론이 필요 없다</b> —
        /// 뽑은 결과가 <c>NetworkList</c>로 복제되므로 전 피어가 같은 내용물을 본다.
        /// </summary>
        private static HotbarSlotView[] BuildContents(StationLootTable table)
        {
            float[] weights = table.GetWeights();
            if (weights == null)
            {
                return null;
            }

            int slots = StationLootLogic.RollRange(table.MinSlots, table.MaxSlots, UnityEngine.Random.value);

            ContentBuffer.Clear();
            for (int i = 0; i < slots; i++)
            {
                int picked = StationLootLogic.RollEntry(weights, UnityEngine.Random.value);
                if (picked < 0)
                {
                    continue;
                }

                StationLootTable.Entry entry = table.GetEntry(picked);
                if (entry == null)
                {
                    continue;
                }

                int count = StationLootLogic.RollRange(entry.MinCount, entry.MaxCount, UnityEngine.Random.value);
                ContentBuffer.Add(new HotbarSlotView(entry.ItemType, count, entry.Resource));
            }

            return ContentBuffer.Count == 0 ? null : ContentBuffer.ToArray();
        }

        private StationLootTable FindTable(StationPropKind kind)
        {
            for (int i = 0; i < _tables.Length; i++)
            {
                if (_tables[i] != null && _tables[i].Kind == kind)
                {
                    return _tables[i];
                }
            }

            return null;
        }
    }
}
