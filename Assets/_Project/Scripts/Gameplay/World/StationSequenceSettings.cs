using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 기차역 시퀀스의 저작 데이터 — 몇 장짜리 역을 얼마나 드물게 놓는가
    /// ([기차역 이벤트 구현 계획](docs/plans/features/기차역-이벤트-구현-계획.md) §4.2).
    ///
    /// <para><b>역은 팔레트에 넣지 않는다.</b> <see cref="TerrainSegmentPalette"/>는 한 장씩
    /// 가중 추첨하는 물건이라 <b>연속 5장</b>을 표현할 수 없다. 그래서 별도 데이터로 두고,
    /// 스트리머가 팔레트보다 <b>먼저</b> 이쪽을 본다.</para>
    ///
    /// <para><b>비어 있으면 현행 그대로다.</b> 이 SO가 배선되지 않았거나 프리팹이 없으면
    /// <see cref="IsEnabled"/>가 거짓이 되어 스트리머·프리웜 모두 기존 팔레트 경로로만 돈다 —
    /// 레벨 디자인 계획이 지켜 온 "팔레트가 비면 현행 동작" 회귀 방어선을 그대로 잇는다.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "StationSequenceSettings", menuName = "Game/Station Sequence Settings")]
    public sealed class StationSequenceSettings : ScriptableObject
    {
        [Tooltip("역 배치를 켠다. 끄면 지형이 현행(팔레트 단독) 그대로 굴러간다.")]
        [SerializeField] private bool _enabled = true;

        [Tooltip("역 하나가 들어가는 블록 크기(타일 장수). 블록마다 정확히 한 번 역이 놓인다.\n" +
                 "지역 하나가 약 1,296장이므로 260이면 지역당 5회 · 최소 간격 130장(5.2 km)이다.")]
        [SerializeField, Min(2)] private int _blockSize = 260;

        [Tooltip("역을 이루는 타일 프리팹을 진행 순서대로. 계획 기준은 5장 —\n" +
                 "접근 · 본체 A · 본체 B · 본체 C · 이탈. 비어 있으면 역이 배치되지 않는다.")]
        [SerializeField] private GameObject[] _stagePrefabs;

        /// <summary>역 시퀀스의 장수 — 프리팹 배열 길이가 곧 길이다.</summary>
        public int StageCount => _stagePrefabs == null ? 0 : _stagePrefabs.Length;

        public int BlockSize => _blockSize;

        /// <summary>
        /// 역 배치가 실제로 도는가 — 스위치·프리팹·블록 크기가 <b>전부</b> 성립해야 한다.
        /// 하나라도 어긋나면 조용히 꺼져 현행 경로로 돌아간다(회귀 방어선).
        /// </summary>
        public bool IsEnabled => _enabled
            && StageCount > 0
            && StationSequenceLogic.IsValidConfig(_blockSize, StageCount)
            && HasAnyPrefab();

        /// <summary>단계의 타일 프리팹. 범위 밖이거나 비었으면 null — 그 장은 일반 지형으로 폴백한다.</summary>
        public GameObject GetStagePrefab(int stage)
        {
            if (_stagePrefabs == null || stage < 0 || stage >= _stagePrefabs.Length)
            {
                return null;
            }

            return _stagePrefabs[stage];
        }

        private bool HasAnyPrefab()
        {
            for (int i = 0; i < _stagePrefabs.Length; i++)
            {
                if (_stagePrefabs[i] != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
